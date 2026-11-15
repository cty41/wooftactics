import unittest
from pathlib import Path
from unittest import mock

from Tools.godot.lib.verify_runner import STEP_HANDLERS, run_verify_profile
from Tools.godot.lib.verify_steps import (
    PROFILE_AGENT,
    PROFILE_WINDOWS,
    steps_for_profile,
    windows_profile_steps,
)


class VerifyOrchestrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(__file__).resolve().parents[3]

    def test_windows_profile_ends_with_tooling_tests(self):
        steps = windows_profile_steps()
        self.assertEqual(steps[-1].step_id, "python_godot_tooling_tests")
        self.assertEqual(steps[-2].step_id, "windows_extended_verification")

    def test_windows_profile_includes_extended_ps1_step(self):
        kinds = {step.kind for step in windows_profile_steps()}
        self.assertIn("windows_extended_ps1", kinds)

    def test_agent_profile_unchanged_count(self):
        steps = steps_for_profile(PROFILE_AGENT)
        self.assertEqual(len(steps), 20)

    def test_every_dispatched_step_has_a_handler(self):
        dispatched = {
            step.step_id
            for profile in (PROFILE_AGENT, PROFILE_WINDOWS)
            for step in steps_for_profile(profile)
            if step.kind in ("run", "conditional_skip")
        }
        missing = sorted(dispatched - set(STEP_HANDLERS))
        self.assertEqual(
            missing,
            [],
            f"registry steps dispatched by the runner without a STEP_HANDLERS entry: {missing}",
        )

    def test_windows_profile_rejects_linux(self):
        exit_code = run_verify_profile(
            repo_root=self.root,
            profile=PROFILE_WINDOWS,
            steps=steps_for_profile(PROFILE_WINDOWS),
            godot_executable="/tmp/Godot",
        )
        self.assertEqual(exit_code, 1)

    def test_thin_verifier_forwards_to_python(self):
        ps1 = (self.root / "Tools/godot/Verify-GodotProject.ps1").read_text(encoding="utf-8-sig")
        self.assertIn("verify_godot_project.py", ps1)
        self.assertIn("--profile", ps1)
        self.assertIn("windows", ps1)
        self.assertNotIn("Invoke-IsolatedGdUnitSuite", ps1)

    def test_extended_script_keeps_gdunit_helpers(self):
        extended = (
            self.root / "Tools/godot/Verify-GodotProject.WindowsExtended.ps1"
        ).read_text(encoding="utf-8-sig")
        self.assertIn("Invoke-IsolatedGdUnitSuite", extended)
        self.assertIn("PortableStepsRanExternally", extended)
        self.assertIn("SkipWorktreeMutex", extended)

    def test_windows_profile_invokes_extended_script(self):
        steps = (
            steps_for_profile(PROFILE_WINDOWS)[-2],
        )
        with mock.patch("Tools.godot.lib.verify_runner.os.name", "nt"), mock.patch(
            "Tools.godot.lib.verify_runner.list_godot_editor_processes",
            return_value=[],
        ), mock.patch(
            "Tools.godot.lib.verify_runner.acquire_worktree_lock",
            return_value=mock.Mock(release=mock.Mock()),
        ), mock.patch(
            "Tools.godot.lib.verify_runner._preflight",
        ), mock.patch(
            "Tools.godot.lib.verify_runner._assert_maliang_policy",
        ), mock.patch(
            "Tools.godot.lib.verify_runner._invoke_windows_extended_verification"
        ) as invoke_extended, mock.patch(
            "Tools.godot.lib.verify_runner.subprocess.run",
            return_value=mock.Mock(returncode=0),
        ):
            exit_code = run_verify_profile(
                repo_root=self.root,
                profile=PROFILE_WINDOWS,
                steps=steps,
                godot_executable=str(self.root / "godot/project.godot"),
            )
        self.assertEqual(exit_code, 0)
        invoke_extended.assert_called_once()


if __name__ == "__main__":
    unittest.main()
