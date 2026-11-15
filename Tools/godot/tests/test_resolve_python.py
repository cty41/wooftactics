import os
import tempfile
import unittest
from unittest import mock

from Tools.godot.lib import resolve_python


class ResolvePythonTests(unittest.TestCase):
    def test_python_env_wins_over_path(self):
        with tempfile.NamedTemporaryFile(mode="w", delete=False) as handle:
            handle.write("#!/bin/sh\n")
            custom = handle.name
        try:
            os.chmod(custom, 0o755)
            env = {"PYTHON": custom, "PATH": "/usr/bin"}
            with mock.patch.object(resolve_python.shutil, "which", return_value="/usr/bin/python3") as which:
                self.assertEqual(
                    resolve_python.resolve_python_executable(env=env),
                    os.path.abspath(custom),
                )
                which.assert_not_called()
        finally:
            os.unlink(custom)

    def test_prefers_python3_over_python_on_path(self):
        env = {"PATH": "/bin"}

        def fake_which(name, path=None):
            if name == "python3":
                return "/bin/python3"
            if name == "python":
                return "/bin/python"
            return None

        with mock.patch.object(resolve_python.shutil, "which", side_effect=fake_which):
            self.assertEqual(
                resolve_python.resolve_python_executable(env=env, path="/bin"),
                os.path.abspath("/bin/python3"),
            )

    def test_falls_back_to_python_when_python3_missing(self):
        env = {"PATH": "/bin"}

        def fake_which(name, path=None):
            if name == "python3":
                return None
            if name == "python":
                return "/bin/python"
            return None

        with mock.patch.object(resolve_python.shutil, "which", side_effect=fake_which):
            self.assertEqual(
                resolve_python.resolve_python_executable(env=env, path="/bin"),
                os.path.abspath("/bin/python"),
            )

    def test_raises_when_nothing_found(self):
        env = {"PATH": "/empty"}
        with mock.patch.object(resolve_python.shutil, "which", return_value=None):
            with self.assertRaises(resolve_python.PythonNotFoundError):
                resolve_python.resolve_python_executable(env=env, path="/empty")

    def test_invalid_python_env_raises(self):
        env = {"PYTHON": "/missing/interpreter", "PATH": "/usr/bin"}
        with self.assertRaises(resolve_python.PythonNotFoundError):
            resolve_python.resolve_python_executable(env=env)

    def test_python_argv_prefixes_interpreter(self):
        with tempfile.NamedTemporaryFile(mode="w", delete=False) as handle:
            custom = handle.name
        try:
            os.chmod(custom, 0o755)
            env = {"PYTHON": custom, "PATH": ""}
            self.assertEqual(
                resolve_python.python_argv("-m", "unittest", env=env)[0],
                os.path.abspath(custom),
            )
            self.assertEqual(
                resolve_python.python_argv("-m", "unittest", env=env),
                [os.path.abspath(custom), "-m", "unittest"],
            )
        finally:
            os.unlink(custom)


if __name__ == "__main__":
    unittest.main()
