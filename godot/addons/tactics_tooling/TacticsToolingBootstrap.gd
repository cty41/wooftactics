@tool
extends EditorPlugin

const LAUNCH_ARGUMENT := "--tactics-open-godot-dev=v1"
const CONTROLLER_SCRIPT_PATH := "res://addons/tactics_tooling/TacticsEditorController.cs"
const CONTROLLER_INITIALIZED_METADATA := "tactics_tooling_initialized"
const WORKBENCH_NODE_NAME := "TacticsContentWorkbench"
const BLOCKED_DIAGNOSTIC := "TACTICS_TOOLING_BLOCKED_DIRECT_LAUNCH"
const INCOMPLETE_DIAGNOSTIC := "TACTICS_TOOLING_BOOTSTRAP_INCOMPLETE"

enum BootstrapState {
	BLOCKED_DIRECT_LAUNCH,
	BOOTSTRAP_INCOMPLETE,
	READY,
}

var _supported_launch := OS.get_cmdline_user_args().has(LAUNCH_ARGUMENT)
var _state := BootstrapState.BLOCKED_DIRECT_LAUNCH
var _controller: Node
var _dialog: AcceptDialog
var _dialog_requested := false
var _launch_command := ""
var _requested_main_screen_visible := false


func _enter_tree() -> void:
	print("[Tactics Tooling Bootstrap] Entering tree.")
	if not _supported_launch:
		_block_direct_launch()
		return

	_start_controller()


func _exit_tree() -> void:
	if _controller != null and is_instance_valid(_controller):
		if _controller.has_method(&"ShutdownForReload"):
			_controller.call(&"ShutdownForReload")
		if _controller.get_parent() == self:
			remove_child(_controller)
		_controller.free()
	_controller = null

	var orphaned_workbench := EditorInterface.get_editor_main_screen().get_node_or_null(WORKBENCH_NODE_NAME)
	if orphaned_workbench != null and is_instance_valid(orphaned_workbench):
		orphaned_workbench.get_parent().remove_child(orphaned_workbench)
		orphaned_workbench.free()

	if _dialog != null and is_instance_valid(_dialog):
		if _dialog.get_parent() != null:
			_dialog.get_parent().remove_child(_dialog)
		_dialog.free()
	_dialog = null
	print("[Tactics Tooling Bootstrap] Exiting tree.")


func _has_main_screen() -> bool:
	# Godot queries this before _enter_tree(), so it must depend only on launch-time state.
	return _supported_launch


func _make_visible(visible: bool) -> void:
	_requested_main_screen_visible = visible
	if _state != BootstrapState.READY or _controller == null or not is_instance_valid(_controller):
		return
	if _controller.has_method(&"SetMainScreenVisible"):
		_controller.call(&"SetMainScreenVisible", visible)


func _get_plugin_name() -> String:
	return "Tactics Tooling"


func _get_plugin_icon() -> Texture2D:
	return EditorInterface.get_editor_theme().get_icon("Node", "EditorIcons")


func _block_direct_launch() -> void:
	_state = BootstrapState.BLOCKED_DIRECT_LAUNCH
	push_warning("%s: run Tools/godot/Open-GodotDev.ps1 before opening this project." % BLOCKED_DIAGNOSTIC)
	if DisplayServer.get_name() != "headless":
		_request_dialog(false)


func _start_controller() -> void:
	var controller_script := load(CONTROLLER_SCRIPT_PATH)
	if controller_script == null or not controller_script.can_instantiate():
		_mark_bootstrap_incomplete("The C# tooling script is unavailable after a supported launch.")
		return

	var candidate = controller_script.new()
	if not candidate is Node:
		if candidate != null and is_instance_valid(candidate):
			candidate.free()
		_mark_bootstrap_incomplete("The C# tooling script did not create a Node controller.")
		return

	_controller = candidate as Node
	if not _controller.has_method(&"Configure") or not _controller.has_method(&"SetMainScreenVisible"):
		_controller.free()
		_controller = null
		_mark_bootstrap_incomplete("The C# tooling controller does not expose its required Editor methods.")
		return
	_controller.call(&"Configure", get_undo_redo())
	add_child(_controller)
	if not bool(_controller.get_meta(CONTROLLER_INITIALIZED_METADATA, false)):
		remove_child(_controller)
		_controller.free()
		_controller = null
		_mark_bootstrap_incomplete("The C# tooling controller failed during initialization.")
		return

	_state = BootstrapState.READY
	_controller.call(&"SetMainScreenVisible", _requested_main_screen_visible)
	print("[Tactics Tooling Bootstrap] Controller ready.")


func _mark_bootstrap_incomplete(detail: String) -> void:
	_state = BootstrapState.BOOTSTRAP_INCOMPLETE
	push_error("%s: %s Close the Editor and rerun Tools/godot/Open-GodotDev.ps1." % [INCOMPLETE_DIAGNOSTIC, detail])
	if DisplayServer.get_name() != "headless":
		_request_dialog(true)


func _request_dialog(bootstrap_incomplete: bool) -> void:
	if _dialog_requested:
		return
	_dialog_requested = true
	call_deferred("_show_launch_dialog", bootstrap_incomplete)


func _show_launch_dialog(bootstrap_incomplete: bool) -> void:
	if not is_inside_tree() or _state == BootstrapState.READY:
		return

	var script_path := ProjectSettings.globalize_path("res://../Tools/godot/Open-GodotDev.ps1").simplify_path()
	_launch_command = "powershell -NoProfile -ExecutionPolicy Bypass -File '%s'" % script_path.replace("'", "''")
	_dialog = AcceptDialog.new()
	_dialog.title = "Tactics Tooling 启动准备未完成" if bootstrap_incomplete else "Tactics Tooling 未启动"
	_dialog.dialog_text = (
		"当前 Godot 未通过项目支持的启动入口打开，或启动准备没有完成。\n"
		+ "Tactics Tooling 已在本次会话中停用，以避免加载未构建或过期的 C# 程序集。\n\n"
		+ "后续步骤：\n"
		+ "1. 正常关闭当前 Godot Editor。\n"
		+ "2. 在仓库根目录运行 Tools/godot/Open-GodotDev.ps1。\n"
		+ "3. 等待 C# Build 显示 0 errors。\n"
		+ "4. 不要手工重新启用报错的 addon。"
	)
	_dialog.ok_button_text = "我知道了"
	_dialog.add_button("复制启动命令", true, "copy_launch_command")
	_dialog.custom_action.connect(_on_dialog_custom_action)
	EditorInterface.get_base_control().add_child(_dialog)
	_dialog.popup_centered(Vector2i(720, 360))


func _on_dialog_custom_action(action: StringName) -> void:
	if action != &"copy_launch_command":
		return
	DisplayServer.clipboard_set(_launch_command)
	print("[Tactics Tooling Bootstrap] Launch command copied to clipboard.")
