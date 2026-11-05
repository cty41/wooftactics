import os
import unittest
from unittest import mock

from Tools.godot.lib import tool_probe


class ToolProbeTests(unittest.TestCase):
    def test_resolve_dotnet_honors_dotnet_root(self):
        env = {"DOTNET_ROOT": "/opt/dotnet", "PATH": "/usr/bin"}
        with mock.patch.object(tool_probe.shutil, "which", return_value="/opt/dotnet/dotnet") as which:
            self.assertEqual(
                tool_probe.resolve_dotnet_executable(env=env),
                os.path.abspath("/opt/dotnet/dotnet"),
            )
            which.assert_called_once()

    def test_resolve_dotnet_raises_with_message(self):
        env = {"PATH": "/empty"}
        with mock.patch.object(tool_probe.shutil, "which", return_value=None):
            with self.assertRaises(tool_probe.DotnetNotFoundError) as ctx:
                tool_probe.resolve_dotnet_executable(env=env)
            self.assertIn(".NET SDK", str(ctx.exception))

    def test_try_resolve_godot_returns_none_when_unset(self):
        self.assertIsNone(tool_probe.try_resolve_godot_executable(env={"PATH": "/usr/bin"}))


if __name__ == "__main__":
    unittest.main()
