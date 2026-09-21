import re
import unittest
from pathlib import Path


class GodotDevLauncherPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(__file__).resolve().parents[3]
        cls.launcher = (cls.root / "Tools/godot/Open-GodotDev.ps1").read_text(encoding="utf-8")
        cls.build_helper = (cls.root / "Tools/godot/Build-GodotDevAssembly.ps1").read_text(encoding="utf-8")
        cls.session = (cls.root / "Tools/godot/GodotDevSession.psm1").read_text(encoding="utf-8")
        cls.verifier = (cls.root / "Tools/godot/Verify-GodotProject.ps1").read_text(encoding="utf-8")
        cls.windows_builder = (cls.root / "Tools/godot/Build-GodotWindows.ps1").read_text(encoding="utf-8")
        cls.reload_smoke = (cls.root / "Tools/godot/Test-TacticsToolingAssemblyReload.ps1").read_text(encoding="utf-8")
        cls.bootstrap_probe = (cls.root / "Tools/godot/Test-TacticsToolingBootstrap.ps1").read_text(encoding="utf-8")
        cls.lifecycle = (cls.root / ".agents/skills/godot-editor-lifecycle/scripts/Invoke-GodotEditorLifecycle.ps1").read_text(encoding="utf-8")
        cls.lifecycle_skill = (cls.root / ".agents/skills/godot-editor-lifecycle/SKILL.md").read_text(encoding="utf-8")
        cls.workflow_rule = (cls.root / ".agents/rules/godot-agent-workflow.md").read_text(encoding="utf-8")
        cls.bootstrap = (cls.root / "godot/addons/tactics_tooling/TacticsToolingBootstrap.gd").read_text(encoding="utf-8")
        cls.controller = (cls.root / "godot/addons/tactics_tooling/TacticsEditorController.cs").read_text(encoding="utf-8")
        cls.plugin = (cls.root / "godot/addons/tactics_tooling/plugin.cfg").read_text(encoding="utf-8")

    def test_build_helper_builds_and_validates_production_adapter(self):
        self.assertRegex(self.build_helper, r"dotnet build \$adapterProject -c Debug -m:1")
        self.assertIn("Tactics.Godot.Adapter.dll", self.build_helper)
        self.assertIn("GetAssemblyName", self.build_helper)
        self.assertIn("expectedFeatureBand", self.build_helper)
        self.assertIn("$actualSdkText = (dotnet --version).Trim()", self.build_helper)
        self.assertIn("Push-Location $repoRoot", self.build_helper)
        self.assertIn("$exitCode = $LASTEXITCODE", self.build_helper)

    def test_build_helper_supports_locked_machine_readable_invocation(self):
        for parameter in ("$Token", "$ResultPath", "$LogPath", "$HeldOperationLock"):
            self.assertIn(parameter, self.build_helper)
        for field in (
            "schemaVersion", "token", "status", "exitCode", "assemblyPath",
            "assemblyIdentity", "firstError", "logPath", "completedAt",
        ):
            self.assertRegex(self.build_helper, rf"(?m)^\s*{field}\s*=")
        self.assertIn("Move-Item -LiteralPath $temporaryResultPath -Destination $ResultPath -Force", self.build_helper)
        self.assertIn("$HeldOperationLock.Identity.RepoRoot -ne $repoRoot", self.build_helper)
        self.assertIn("$HeldOperationLock.Identity.MutexName -ne $worktreeIdentity.MutexName", self.build_helper)
        self.assertIn("$HeldOperationLock.Mutex -isnot [Threading.Mutex]", self.build_helper)
        self.assertIn("$HeldOperationLock.Mutex.SafeWaitHandle.IsClosed", self.build_helper)
        self.assertNotIn("Start-Process", self.build_helper)
        self.assertNotIn("GodotExecutable", self.build_helper)
        self.assertNotIn("Sync-GodotAiCodexConfig", self.build_helper)

    def test_launcher_delegates_build_inside_its_operation_lock(self):
        self.assertIn("Build-GodotDevAssembly.ps1", self.launcher)
        self.assertIn("-HeldOperationLock $lock", self.launcher)
        self.assertLess(self.launcher.index("Enter-TacticsGodotOperationLock"), self.launcher.index("& $buildHelper"))
        self.assertNotRegex(self.launcher, r"dotnet build \$adapterProject")
        self.assertNotIn("GetAssemblyName", self.launcher)
        self.assertNotIn("expectedFeatureBand", self.launcher)
        self.assertIn("GetVersionInfo", self.launcher)

    def test_agent_cannot_use_shared_manual_qa_profile(self):
        self.assertIn("SharedManualQA is a Human-only profile", self.launcher)

    def test_worktree_identity_drives_mutex_and_user_directory(self):
        self.assertIn("TacticsGodotDev-$key", self.session)
        self.assertIn("TacticsGodotDev/$key", self.session)
        self.assertIn("override.cfg", self.launcher)
        self.assertIn("AbandonedMutexException", self.session)
        self.assertIn("CommandLine.Replace('\\', '/')", self.session)

    def test_launcher_bootstraps_project_codex_config_and_records_session(self):
        self.assertIn("-Bootstrap", self.launcher)
        self.assertIn("CODEX_RESTART_REQUIRED", self.launcher)
        self.assertIn("tactics-dev-session.json", self.launcher)

    def test_supported_editor_launches_pass_the_bootstrap_argument_after_separator(self):
        launch_argument = "--tactics-open-godot-dev=v1"
        self.assertIn(f"$script:TacticsGodotLaunchArgument = '{launch_argument}'", self.session)
        self.assertIn(f'const LAUNCH_ARGUMENT := "{launch_argument}"', self.bootstrap)
        self.assertIn("'--', $editorLaunchArgument", self.launcher)
        self.assertIn("-- $editorLaunchArgument", self.verifier)
        self.assertIn("-HeldOperationLock $operationLock", self.verifier)
        self.assertIn("[object]$HeldOperationLock", self.bootstrap_probe)
        self.assertIn("$HeldOperationLock.Identity.RepoRoot -ne $repoRoot", self.bootstrap_probe)
        self.assertIn("$HeldOperationLock.Identity.MutexName -ne $worktreeIdentity.MutexName", self.bootstrap_probe)
        self.assertIn("$HeldOperationLock.Mutex -isnot [Threading.Mutex]", self.bootstrap_probe)
        self.assertIn("$HeldOperationLock.Mutex.SafeWaitHandle.IsClosed", self.bootstrap_probe)
        self.assertIn("-- $editorLaunchArgument", self.windows_builder)
        self.assertIn("'--', $editorLaunchArgument", self.reload_smoke)

    def test_editor_lifecycle_reopen_delegates_to_supported_launcher(self):
        self.assertIn("Tools\\godot\\Open-GodotDev.ps1", self.lifecycle)
        self.assertIn("& $supportedLauncher", self.lifecycle)
        self.assertIn("GODOT_EDITOR_STARTED", self.lifecycle)
        self.assertIn("-Mode Agent", self.lifecycle)
        self.assertIn("-UserDataProfile Worktree", self.lifecycle)
        self.assertNotRegex(self.lifecycle, r"Start-Process\s+`?\s*-FilePath \$resolvedGodotExecutable")
        self.assertIn("禁止 Agent 直接执行 `godot --editor --path`", self.lifecycle_skill)
        self.assertIn("禁止 lifecycle 脚本或 Agent 以裸 `godot --editor --path`", self.workflow_rule)
        self.assertIn("人工可以从 Godot Project Manager 直接打开", self.workflow_rule)

    def test_main_screen_registration_is_owned_by_the_always_available_gdscript_shell(self):
        self.assertIn("var _prepared_launch := OS.get_cmdline_user_args().has(LAUNCH_ARGUMENT)", self.bootstrap)
        self.assertRegex(self.bootstrap, r"func _has_main_screen\(\) -> bool:\s+#.*(?:\n.*)*?\s+return true")
        self.assertIn("_requested_main_screen_visible = visible", self.bootstrap)
        self.assertIn('_controller.call(&"SetMainScreenVisible", _requested_main_screen_visible)', self.bootstrap)
        self.assertNotIn("BLOCKED_DIRECT_LAUNCH", self.bootstrap)

    def test_start_process_paths_are_quoted_and_probe_logs_are_unique(self):
        self.assertIn("$quotedProjectRoot", self.launcher)
        self.assertIn("$quotedProjectRoot", self.reload_smoke)
        self.assertIn("$quotedLogPath", self.reload_smoke)
        self.assertIn("$quotedProjectRoot", self.bootstrap_probe)
        self.assertIn("$quotedLogPath", self.bootstrap_probe)
        self.assertIn("$worktreeIdentity.Key", self.bootstrap_probe)
        self.assertIn("[guid]::NewGuid()", self.bootstrap_probe)

    def test_tactics_tooling_shell_self_heals_missing_csharp_without_preloading_it(self):
        self.assertIn('script="TacticsToolingBootstrap.gd"', self.plugin)
        self.assertIn('ResourceLoader.load(', self.bootstrap)
        self.assertNotIn('preload(CONTROLLER_SCRIPT_PATH)', self.bootstrap)
        self.assertIn('TACTICS_TOOLING_BUILD_REQUIRED', self.bootstrap)
        self.assertIn('TACTICS_TOOLING_BUILD_FAILED', self.bootstrap)
        self.assertIn('BUILD C# AND RELOAD', self.bootstrap)
        self.assertIn('Build-GodotDevAssembly.ps1', self.bootstrap)
        self.assertIn('OS.create_process("powershell", arguments, false)', self.bootstrap)
        self.assertIn('ResourceLoader.CACHE_MODE_REPLACE', self.bootstrap)
        self.assertIn('_controller.call(&"Configure", get_undo_redo(), _prepared_launch)', self.bootstrap)
        self.assertIn('SetMeta(AuthoringBridgeRequestedMetadata, requestAuthoringBridge)', self.controller)
        self.assertIn('AuthoringBridgeRequested && HasMatchingPreparedSession()', self.controller)
        self.assertIn('pidElement.GetInt32() != OS.GetProcessId()', self.controller)
        self.assertIn('ComputeWorktreeKey(repoRoot)', self.controller)
        self.assertIn('if (AuthoringBridgeEnabled) CreateBridge();', self.controller)
        self.assertNotIn('--build-poison-spear', self.controller)
        self.assertIn('Direct Human session: authoring MCP bridge disabled.', self.controller)
        self.assertLess(
            self.bootstrap.index('_controller.call(&"Configure", get_undo_redo(), _prepared_launch)'),
            self.bootstrap.index('add_child(_controller)'),
        )
        self.assertNotIn("Open-GodotDev.ps1 before opening this project", self.bootstrap)

    def test_verifier_rebuilds_a_native_host_that_removed_its_temp_assembly(self):
        self.assertIn("Rebuild isolated GdUnit4Net test host", self.verifier)
        self.assertIn("Test-Path -LiteralPath $testHostAssembly", self.verifier)


if __name__ == "__main__":
    unittest.main()
