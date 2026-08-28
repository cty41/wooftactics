import re
import unittest
from pathlib import Path


class GodotDevLauncherPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(__file__).resolve().parents[3]
        cls.launcher = (cls.root / "Tools/godot/Open-GodotDev.ps1").read_text(encoding="utf-8")
        cls.session = (cls.root / "Tools/godot/GodotDevSession.psm1").read_text(encoding="utf-8")
        cls.verifier = (cls.root / "Tools/godot/Verify-GodotProject.ps1").read_text(encoding="utf-8")
        cls.windows_builder = (cls.root / "Tools/godot/Build-GodotWindows.ps1").read_text(encoding="utf-8")
        cls.reload_smoke = (cls.root / "Tools/godot/Test-TacticsToolingAssemblyReload.ps1").read_text(encoding="utf-8")
        cls.bootstrap_probe = (cls.root / "Tools/godot/Test-TacticsToolingBootstrap.ps1").read_text(encoding="utf-8")
        cls.bootstrap = (cls.root / "godot/addons/tactics_tooling/TacticsToolingBootstrap.gd").read_text(encoding="utf-8")
        cls.plugin = (cls.root / "godot/addons/tactics_tooling/plugin.cfg").read_text(encoding="utf-8")

    def test_launcher_always_builds_production_adapter_serially(self):
        self.assertRegex(self.launcher, r"dotnet build \$adapterProject -c Debug -m:1")
        self.assertIn("Tactics.Godot.Adapter.dll", self.launcher)
        self.assertIn("GetAssemblyName", self.launcher)
        self.assertIn("expectedFeatureBand", self.launcher)
        self.assertIn("GetVersionInfo", self.launcher)

    def test_launcher_resolves_dotnet_sdk_and_build_from_repository_root(self):
        self.assertGreaterEqual(self.launcher.count("Push-Location $repoRoot"), 2)
        self.assertGreaterEqual(self.launcher.count("Pop-Location"), 2)
        self.assertIn("$actualSdkText = (dotnet --version).Trim()", self.launcher)
        self.assertIn("$buildExitCode = $LASTEXITCODE", self.launcher)

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

    def test_main_screen_registration_uses_launch_time_state(self):
        self.assertIn("var _supported_launch := OS.get_cmdline_user_args().has(LAUNCH_ARGUMENT)", self.bootstrap)
        self.assertRegex(self.bootstrap, r"func _has_main_screen\(\) -> bool:\s+#.*\s+return _supported_launch")
        self.assertIn("_requested_main_screen_visible = visible", self.bootstrap)
        self.assertIn('_controller.call(&"SetMainScreenVisible", _requested_main_screen_visible)', self.bootstrap)

    def test_start_process_paths_are_quoted_and_probe_logs_are_unique(self):
        self.assertIn("$quotedProjectRoot", self.launcher)
        self.assertIn("$quotedProjectRoot", self.reload_smoke)
        self.assertIn("$quotedLogPath", self.reload_smoke)
        self.assertIn("$quotedProjectRoot", self.bootstrap_probe)
        self.assertIn("$quotedLogPath", self.bootstrap_probe)
        self.assertIn("$worktreeIdentity.Key", self.bootstrap_probe)
        self.assertIn("[guid]::NewGuid()", self.bootstrap_probe)

    def test_tactics_tooling_uses_gdscript_bootstrap_without_preloading_csharp(self):
        self.assertIn('script="TacticsToolingBootstrap.gd"', self.plugin)
        self.assertIn('load(CONTROLLER_SCRIPT_PATH)', self.bootstrap)
        self.assertNotIn('preload(CONTROLLER_SCRIPT_PATH)', self.bootstrap)
        self.assertIn('TACTICS_TOOLING_BLOCKED_DIRECT_LAUNCH', self.bootstrap)
        self.assertIn('_controller.call(&"Configure", get_undo_redo())', self.bootstrap)
        self.assertLess(
            self.bootstrap.index('_controller.call(&"Configure", get_undo_redo())'),
            self.bootstrap.index('add_child(_controller)'),
        )
        for prompt_fragment in (
            "正常关闭当前 Godot Editor",
            "Tools/godot/Open-GodotDev.ps1",
            "等待 C# Build 显示 0 errors",
            "不要手工重新启用报错的 addon",
            "复制启动命令",
        ):
            self.assertIn(prompt_fragment, self.bootstrap)

    def test_verifier_rebuilds_a_native_host_that_removed_its_temp_assembly(self):
        self.assertIn("Rebuild isolated GdUnit4Net test host", self.verifier)
        self.assertIn("Test-Path -LiteralPath $testHostAssembly", self.verifier)


if __name__ == "__main__":
    unittest.main()
