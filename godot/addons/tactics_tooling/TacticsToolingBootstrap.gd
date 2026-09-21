@tool
extends EditorPlugin

const LAUNCH_ARGUMENT := "--tactics-open-godot-dev=v1"
const AUTO_BUILD_PROBE_ARGUMENT := "--tactics-tooling-auto-build-probe=v1"
const EXTERNAL_BUILD_PROBE_PREFIX := "--tactics-tooling-external-build-probe="
const CONTROLLER_SCRIPT_PATH := "res://addons/tactics_tooling/TacticsEditorController.cs"
const BUILD_SCRIPT_PATH := "res://../Tools/godot/Build-GodotDevAssembly.ps1"
const CONTROLLER_INITIALIZED_METADATA := "tactics_tooling_initialized"
const WORKBENCH_NODE_NAME := "TacticsContentWorkbench"
const SHELL_NODE_NAME := "TacticsToolingBootstrapShell"
const BUILD_REQUIRED_DIAGNOSTIC := "TACTICS_TOOLING_BUILD_REQUIRED"
const BUILD_FAILED_DIAGNOSTIC := "TACTICS_TOOLING_BUILD_FAILED"
const RESTART_REQUIRED_DIAGNOSTIC := "TACTICS_TOOLING_RESTART_REQUIRED"
const RELOAD_TIMEOUT_MSEC := 30000
const RESULT_TIMEOUT_MSEC := 5000


enum BootstrapState {
	CHECKING_CONTROLLER,
	BUILD_REQUIRED,
	BUILDING,
	WAITING_FOR_ASSEMBLY_RELOAD,
	READY,
	BUILD_FAILED,
	RESTART_REQUIRED,
}


var _prepared_launch := OS.get_cmdline_user_args().has(LAUNCH_ARGUMENT)
var _auto_build_probe := DisplayServer.get_name() == "headless" and OS.get_cmdline_user_args().has(AUTO_BUILD_PROBE_ARGUMENT)
var _external_build_probe_token := ""
var _state := BootstrapState.CHECKING_CONTROLLER
var _controller: Node
var _shell: Control
var _status_label: Label
var _build_button: Button
var _retry_button: Button
var _copy_button: Button
var _state_detail := ""
var _requested_main_screen_visible := false
var _build_pid := -1
var _build_token := ""
var _build_result_path := ""
var _build_log_path := ""
var _build_command := ""
var _reload_deadline_msec := 0
var _result_deadline_msec := 0


func _enter_tree() -> void:
	print("[Tactics Tooling Bootstrap] Entering tree. prepared=%s" % _prepared_launch)
	set_process(false)
	if DisplayServer.get_name() == "headless":
		for argument in OS.get_cmdline_user_args():
			if argument.begins_with(EXTERNAL_BUILD_PROBE_PREFIX):
				_external_build_probe_token = argument.trim_prefix(EXTERNAL_BUILD_PROBE_PREFIX)
	_state = BootstrapState.CHECKING_CONTROLLER
	if _try_start_controller():
		return
	_enter_build_required()


func _exit_tree() -> void:
	set_process(false)
	_shutdown_controller()
	_remove_shell()
	_build_pid = -1
	_build_token = ""
	_delete_build_result()
	_build_result_path = ""
	_build_log_path = ""
	print("[Tactics Tooling Bootstrap] Exiting tree.")


func _process(_delta: float) -> void:
	match _state:
		BootstrapState.BUILDING:
			_poll_build_process()
		BootstrapState.WAITING_FOR_ASSEMBLY_RELOAD:
			_poll_controller_reload()


func _has_main_screen() -> bool:
	# Godot queries this before _enter_tree(). The GDScript shell is always able to
	# own the Main Screen even when the C# assembly has never been built.
	return true


func _make_visible(visible: bool) -> void:
	_requested_main_screen_visible = visible
	if _state == BootstrapState.READY and _controller != null and is_instance_valid(_controller):
		if _controller.has_method(&"SetMainScreenVisible"):
			_controller.call(&"SetMainScreenVisible", visible)
	elif _shell != null and is_instance_valid(_shell):
		_shell.visible = visible


func _get_plugin_name() -> String:
	return "Tactics Tooling"


func _get_plugin_icon() -> Texture2D:
	return EditorInterface.get_editor_theme().get_icon("Node", "EditorIcons")


func _try_start_controller() -> bool:
	if _controller != null and is_instance_valid(_controller):
		return true
	var controller_script := ResourceLoader.load(
		CONTROLLER_SCRIPT_PATH,
		"",
		ResourceLoader.CACHE_MODE_REPLACE
	)
	if controller_script == null or not controller_script.can_instantiate():
		return false
	var candidate = controller_script.new()
	if not candidate is Node:
		if candidate != null and is_instance_valid(candidate):
			candidate.free()
		return false
	_controller = candidate as Node
	if not _controller.has_method(&"Configure") or not _controller.has_method(&"SetMainScreenVisible"):
		_controller.free()
		_controller = null
		return false
	_controller.call(&"Configure", get_undo_redo(), _prepared_launch)
	add_child(_controller)
	if not bool(_controller.get_meta(CONTROLLER_INITIALIZED_METADATA, false)):
		remove_child(_controller)
		_controller.free()
		_controller = null
		return false
	var recovered_build := _state == BootstrapState.WAITING_FOR_ASSEMBLY_RELOAD
	_state = BootstrapState.READY
	_state_detail = "C# tooling controller is ready."
	set_process(false)
	_remove_shell()
	_controller.call(&"SetMainScreenVisible", _requested_main_screen_visible)
	print("[Tactics Tooling Bootstrap] Controller ready. prepared=%s" % _prepared_launch)
	if recovered_build and not _build_log_path.is_empty() and FileAccess.file_exists(_build_log_path):
		DirAccess.remove_absolute(_build_log_path)
	return true


func _enter_build_required() -> void:
	_state = BootstrapState.BUILD_REQUIRED
	_state_detail = (
		"The C# tooling assembly is not available yet. Build it once, then this Editor will reload the Workbench."
	)
	print("%s prepared=%s" % [BUILD_REQUIRED_DIAGNOSTIC, _prepared_launch])
	_ensure_shell()
	_render_shell()
	if _auto_build_probe:
		call_deferred("_start_build")
	elif not _external_build_probe_token.is_empty():
		call_deferred("_watch_external_build_probe")


func _watch_external_build_probe() -> void:
	_build_token = _external_build_probe_token
	_build_result_path = ProjectSettings.globalize_path(
		"res://.godot/tactics-tooling-build-result-%s.json" % _build_token
	).simplify_path()
	_build_log_path = ProjectSettings.globalize_path(
		"res://.godot/tactics-tooling-build-%s.log" % _build_token
	).simplify_path()
	_state = BootstrapState.BUILDING
	_state_detail = "External verifier build is running…"
	_build_pid = -1
	_result_deadline_msec = Time.get_ticks_msec() + RELOAD_TIMEOUT_MSEC
	set_process(true)
	_render_shell()


func _start_build() -> void:
	if _state == BootstrapState.BUILDING:
		return
	var build_script := ProjectSettings.globalize_path(BUILD_SCRIPT_PATH).simplify_path()
	if not FileAccess.file_exists(build_script):
		_enter_build_failed("Build helper does not exist: %s" % build_script)
		return
	_build_token = str(Time.get_unix_time_from_system()).replace(".", "-") + "-" + str(randi())
	_build_result_path = ProjectSettings.globalize_path(
		"res://.godot/tactics-tooling-build-result-%s.json" % _build_token
	).simplify_path()
	_build_log_path = ProjectSettings.globalize_path(
		"res://.godot/tactics-tooling-build-%s.log" % _build_token
	).simplify_path()
	_build_command = "powershell -NoProfile -ExecutionPolicy Bypass -File '%s'" % build_script.replace("'", "''")
	var arguments := PackedStringArray([
		"-NoProfile",
		"-WindowStyle",
		"Hidden",
		"-ExecutionPolicy",
		"Bypass",
		"-File",
		build_script,
		"-Token",
		_build_token,
		"-ResultPath",
		_build_result_path,
		"-LogPath",
		_build_log_path,
	])
	# open_console=false is explicit so Godot uses a no-console Windows child.
	_build_pid = OS.create_process("powershell", arguments, false)
	if _build_pid <= 0:
		_enter_build_failed("Unable to start the C# build helper.")
		return
	_state = BootstrapState.BUILDING
	_state_detail = "Building Tactics.Godot.Adapter…\nThe Editor remains available while MSBuild runs."
	_result_deadline_msec = 0
	set_process(true)
	_render_shell()
	print("[Tactics Tooling Bootstrap] Build started. pid=%s token=%s" % [_build_pid, _build_token])


func _poll_build_process() -> void:
	if _build_pid > 0 and OS.is_process_running(_build_pid):
		return
	if FileAccess.file_exists(_build_result_path):
		_consume_build_result()
		return
	if _result_deadline_msec == 0:
		_result_deadline_msec = Time.get_ticks_msec() + RESULT_TIMEOUT_MSEC
		return
	if Time.get_ticks_msec() >= _result_deadline_msec:
		_enter_build_failed("Build process exited without writing its result contract. Log: %s" % _build_log_path)


func _consume_build_result() -> void:
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(_build_result_path))
	if not parsed is Dictionary:
		_enter_build_failed("Build result is invalid JSON. Log: %s" % _build_log_path)
		return
	var result := parsed as Dictionary
	if str(result.get("token", "")) != _build_token:
		_enter_build_failed("Build result token mismatch. Log: %s" % _build_log_path)
		return
	_delete_build_result()
	var status := str(result.get("status", ""))
	if status != "succeeded":
		var first_error := str(result.get("firstError", "C# build failed."))
		var result_log := str(result.get("logPath", _build_log_path))
		_enter_build_failed("%s\nLog: %s" % [first_error, result_log])
		return
	_state = BootstrapState.WAITING_FOR_ASSEMBLY_RELOAD
	_state_detail = "Build succeeded. Waiting for Godot to reload the C# assembly…"
	EditorInterface.get_resource_filesystem().scan()
	_reload_deadline_msec = Time.get_ticks_msec() + RELOAD_TIMEOUT_MSEC
	set_process(true)
	_render_shell()
	_poll_controller_reload()


func _poll_controller_reload() -> void:
	if _try_start_controller():
		return
	if Time.get_ticks_msec() < _reload_deadline_msec:
		return
	_state = BootstrapState.RESTART_REQUIRED
	_state_detail = (
		"C# build succeeded, but this Editor did not expose the new assembly in time.\n"
		+ "Close and reopen this project from Project Manager; the Workbench will load immediately.\n"
		+ "Log: %s" % _build_log_path
	)
	set_process(false)
	print("%s log=%s" % [RESTART_REQUIRED_DIAGNOSTIC, _build_log_path])
	_render_shell()


func _enter_build_failed(detail: String) -> void:
	_state = BootstrapState.BUILD_FAILED
	_state_detail = detail
	set_process(false)
	push_error("%s: %s" % [BUILD_FAILED_DIAGNOSTIC, detail])
	_ensure_shell()
	_render_shell()


func _ensure_shell() -> void:
	if DisplayServer.get_name() == "headless":
		return
	if _shell != null and is_instance_valid(_shell):
		return
	var existing := EditorInterface.get_editor_main_screen().get_node_or_null(SHELL_NODE_NAME)
	if existing != null and is_instance_valid(existing):
		existing.get_parent().remove_child(existing)
		existing.free()
	_shell = MarginContainer.new()
	_shell.name = SHELL_NODE_NAME
	_shell.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_shell.add_theme_constant_override("margin_left", 48)
	_shell.add_theme_constant_override("margin_top", 48)
	_shell.add_theme_constant_override("margin_right", 48)
	_shell.add_theme_constant_override("margin_bottom", 48)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_shell.add_child(center)
	var panel := PanelContainer.new()
	panel.custom_minimum_size = Vector2(720, 360)
	center.add_child(panel)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 32)
	margin.add_theme_constant_override("margin_top", 32)
	margin.add_theme_constant_override("margin_right", 32)
	margin.add_theme_constant_override("margin_bottom", 32)
	panel.add_child(margin)
	var content := VBoxContainer.new()
	content.add_theme_constant_override("separation", 16)
	margin.add_child(content)
	var title := Label.new()
	title.text = "TACTICS TOOLING — C# SETUP"
	title.add_theme_font_size_override("font_size", 28)
	content.add_child(title)
	_status_label = Label.new()
	_status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_status_label.custom_minimum_size = Vector2(640, 150)
	content.add_child(_status_label)
	_build_button = Button.new()
	_build_button.text = "BUILD C# AND RELOAD"
	_build_button.pressed.connect(_start_build)
	content.add_child(_build_button)
	_retry_button = Button.new()
	_retry_button.text = "RETRY BUILD"
	_retry_button.pressed.connect(_start_build)
	content.add_child(_retry_button)
	_copy_button = Button.new()
	_copy_button.text = "COPY BUILD COMMAND"
	_copy_button.pressed.connect(_copy_build_command)
	content.add_child(_copy_button)
	EditorInterface.get_editor_main_screen().add_child(_shell)
	_shell.visible = _requested_main_screen_visible


func _render_shell() -> void:
	_ensure_shell()
	if _shell == null or not is_instance_valid(_shell):
		return
	_status_label.text = _state_detail
	_build_button.visible = _state == BootstrapState.BUILD_REQUIRED
	_build_button.disabled = _state == BootstrapState.BUILDING
	_retry_button.visible = _state == BootstrapState.BUILD_FAILED
	_retry_button.disabled = _state == BootstrapState.BUILDING
	_copy_button.visible = _state in [
		BootstrapState.BUILD_REQUIRED,
		BootstrapState.BUILD_FAILED,
		BootstrapState.RESTART_REQUIRED,
	]
	_shell.visible = _requested_main_screen_visible


func _copy_build_command() -> void:
	if _build_command.is_empty():
		var build_script := ProjectSettings.globalize_path(BUILD_SCRIPT_PATH).simplify_path()
		_build_command = "powershell -NoProfile -ExecutionPolicy Bypass -File '%s'" % build_script.replace("'", "''")
	DisplayServer.clipboard_set(_build_command)
	print("[Tactics Tooling Bootstrap] Build command copied to clipboard.")


func _delete_build_result() -> void:
	if not _build_result_path.is_empty() and FileAccess.file_exists(_build_result_path):
		DirAccess.remove_absolute(_build_result_path)


func _shutdown_controller() -> void:
	if _controller == null or not is_instance_valid(_controller):
		_controller = null
		return
	if _controller.has_method(&"ShutdownForReload"):
		_controller.call(&"ShutdownForReload")
	if _controller.get_parent() == self:
		remove_child(_controller)
	_controller.free()
	_controller = null


func _remove_shell() -> void:
	if _shell != null and is_instance_valid(_shell):
		if _shell.get_parent() != null:
			_shell.get_parent().remove_child(_shell)
		_shell.free()
	_shell = null
	_status_label = null
	_build_button = null
	_retry_button = null
	_copy_button = null
	var orphaned_shell := EditorInterface.get_editor_main_screen().get_node_or_null(SHELL_NODE_NAME)
	if orphaned_shell != null and is_instance_valid(orphaned_shell):
		orphaned_shell.get_parent().remove_child(orphaned_shell)
		orphaned_shell.free()
	var orphaned_workbench := EditorInterface.get_editor_main_screen().get_node_or_null(WORKBENCH_NODE_NAME)
	if _state != BootstrapState.READY and orphaned_workbench != null and is_instance_valid(orphaned_workbench):
		orphaned_workbench.get_parent().remove_child(orphaned_workbench)
		orphaned_workbench.free()
