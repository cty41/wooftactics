import unittest

from Tools.godot.lib.skip_log import (
    SKIP_WINDOWS_ONLY_PREFIX,
    format_skip_windows_only,
)
from Tools.godot.lib.verify_steps import PROFILE_AGENT, agent_profile_steps, steps_for_profile


class VerifyStepsRegistryTests(unittest.TestCase):
    def test_agent_profile_is_non_empty(self):
        steps = steps_for_profile(PROFILE_AGENT)
        self.assertGreater(len(steps), 0)

    def test_agent_profile_includes_windows_only_skip_declarations(self):
        steps = agent_profile_steps()
        windows_skips = [step for step in steps if step.kind == "windows_only_skip"]
        self.assertGreaterEqual(len(windows_skips), 4)
        titles = {step.title for step in windows_skips}
        self.assertIn("Validate Codex/OpenCode project skills", titles)
        self.assertIn("Test-TacticsToolingAssemblyReload.ps1", titles)

    def test_skip_log_prefix_is_grep_friendly(self):
        line = format_skip_windows_only("Example step", "reason")
        self.assertTrue(line.startswith(SKIP_WINDOWS_ONLY_PREFIX))
        self.assertIn("Example step", line)
        self.assertIn("reason", line)


if __name__ == "__main__":
    unittest.main()
