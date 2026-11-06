"""Regression tests for Tools/godot CLI scripts run without PYTHONPATH=."""

from __future__ import annotations

import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


class GodotCliEntryBootstrapTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = Path(__file__).resolve().parents[3]
        cls.python = sys.executable

    def _env_without_pythonpath(self) -> dict[str, str]:
        env = os.environ.copy()
        env.pop("PYTHONPATH", None)
        return env

    def _run_script(self, script: Path, *args: str, cwd: Path | None = None) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [self.python, str(script), *args],
            cwd=str(cwd or self.root),
            env=self._env_without_pythonpath(),
            capture_output=True,
            text=True,
        )

    def test_verify_godot_project_help_without_pythonpath(self) -> None:
        script = self.root / "Tools/godot/verify_godot_project.py"
        completed = self._run_script(script, "--help")
        self.assertEqual(completed.returncode, 0, msg=completed.stderr or completed.stdout)
        self.assertNotIn("ModuleNotFoundError", completed.stderr)
        self.assertIn("--profile", completed.stdout)

    def test_open_godot_dev_help_without_pythonpath(self) -> None:
        script = self.root / "Tools/godot/open_godot_dev.py"
        completed = self._run_script(script, "--help")
        self.assertEqual(completed.returncode, 0, msg=completed.stderr or completed.stdout)
        self.assertNotIn("ModuleNotFoundError", completed.stderr)
        self.assertIn("--no-launch", completed.stdout)

    def test_verify_entry_from_foreign_cwd_without_pythonpath(self) -> None:
        script = (self.root / "Tools/godot/verify_godot_project.py").resolve()
        with tempfile.TemporaryDirectory() as temp_dir:
            completed = self._run_script(script, "--help", cwd=Path(temp_dir))
        self.assertEqual(completed.returncode, 0, msg=completed.stderr or completed.stdout)
        self.assertNotIn("ModuleNotFoundError", completed.stderr)

    def test_repo_bootstrap_unit(self) -> None:
        import importlib.util

        bootstrap_path = self.root / "Tools/godot/lib/repo_bootstrap.py"
        spec = importlib.util.spec_from_file_location("_test_repo_bootstrap", bootstrap_path)
        self.assertIsNotNone(spec)
        self.assertIsNotNone(spec.loader)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        fake_entry = self.root / "Tools/godot/verify_godot_project.py"
        repo = module.bootstrap_cli_entry(fake_entry)
        self.assertEqual(repo, self.root)


if __name__ == "__main__":
    unittest.main()
