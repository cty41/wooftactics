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

    def test_resolve_npm_returns_cmd_from_which(self):
        env = {"PATH": r"C:\Program Files\nodejs"}
        with mock.patch.object(
            tool_probe.shutil,
            "which",
            side_effect=[r"C:\Program Files\nodejs\npm.cmd", None],
        ) as which:
            self.assertEqual(
                tool_probe.resolve_npm_executable(env=env),
                os.path.abspath(r"C:\Program Files\nodejs\npm.cmd"),
            )
            which.assert_any_call("npm", path=r"C:\Program Files\nodejs")

    def test_resolve_npm_honors_npm_env(self):
        npm_path = r"C:\custom\npm.cmd"
        env = {"NPM": npm_path, "PATH": "/usr/bin"}
        with mock.patch.object(tool_probe.os.path, "isfile", return_value=True):
            self.assertEqual(
                tool_probe.resolve_npm_executable(env=env),
                os.path.abspath(npm_path),
            )

    def test_resolve_npm_raises_when_missing(self):
        env = {"PATH": "/empty"}
        with mock.patch.object(tool_probe.shutil, "which", return_value=None):
            with self.assertRaises(tool_probe.NpmNotFoundError) as ctx:
                tool_probe.resolve_npm_executable(env=env)
            self.assertIn("npm", str(ctx.exception).lower())

    def test_resolve_npm_windows_sibling_of_node(self):
        node = r"D:\nodejs\node.exe"
        npm_cmd = os.path.join(os.path.dirname(node), "npm.cmd")
        env = {"PATH": "/empty"}
        with (
            mock.patch.object(tool_probe.shutil, "which", return_value=None),
            mock.patch.object(tool_probe.os.path, "isfile", return_value=True),
            mock.patch.object(tool_probe.os, "name", "nt"),
        ):
            self.assertEqual(
                tool_probe.resolve_npm_executable(env=env, node_exe=node),
                os.path.abspath(npm_cmd),
            )


if __name__ == "__main__":
    unittest.main()
