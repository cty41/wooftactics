import re
import unittest
from pathlib import Path


class GodotDevLauncherPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(__file__).resolve().parents[3]
        cls.launcher = (cls.root / "Tools/godot/Open-GodotDev.ps1").read_text(encoding="utf-8")
        cls.session = (cls.root / "Tools/godot/GodotDevSession.psm1").read_text(encoding="utf-8")

    def test_launcher_always_builds_production_adapter_serially(self):
        self.assertRegex(self.launcher, r"dotnet build \$adapterProject -c Debug -m:1")
        self.assertIn("Tactics.Godot.Adapter.dll", self.launcher)
        self.assertIn("GetAssemblyName", self.launcher)
        self.assertIn("expectedFeatureBand", self.launcher)
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

    def test_python_entry_documents_no_launch_only(self):
        python_entry = (self.root / "Tools/godot/open_godot_dev.py").read_text(encoding="utf-8")
        self.assertIn("--no-launch", python_entry)
        self.assertIn("Open-GodotDev.ps1", python_entry)

    def test_verifier_rebuilds_a_native_host_that_removed_its_temp_assembly(self):
        extended = (
            self.root / "Tools/godot/Verify-GodotProject.WindowsExtended.ps1"
        ).read_text(encoding="utf-8")
        self.assertIn("Rebuild isolated GdUnit4Net test host", extended)
        self.assertIn("Test-Path -LiteralPath $testHostAssembly", extended)

    def test_verifier_entry_forwards_to_python(self):
        verifier = (self.root / "Tools/godot/Verify-GodotProject.ps1").read_text(encoding="utf-8")
        self.assertIn("verify_godot_project.py", verifier)
        self.assertIn("'windows'", verifier)


if __name__ == "__main__":
    unittest.main()
