import json
import os
import tempfile
import threading
import unittest
from pathlib import Path
from unittest import mock

from Tools.godot.lib.open_godot_dev_runner import (
    OpenGodotDevError,
    OpenGodotDevOptions,
    run_open_godot_dev,
)
from Tools.godot.lib.worktree_lock import acquire_worktree_lock, worktree_identity
from Tools.godot.open_godot_dev import parse_cli_args


class OpenGodotDevPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(__file__).resolve().parents[3]

    def test_agent_cannot_use_shared_manual_qa_profile(self):
        with self.assertRaises(OpenGodotDevError) as ctx:
            run_open_godot_dev(
                OpenGodotDevOptions(
                    mode="Agent",
                    user_data_profile="SharedManualQA",
                    repo_root=self.root,
                    godot_executable="/tmp/godot",
                )
            )
        self.assertIn("SharedManualQA is a Human-only profile", str(ctx.exception))

    def test_cli_requires_no_launch_flag(self):
        with mock.patch("sys.argv", ["open_godot_dev.py"]):
            options = parse_cli_args([])
        self.assertFalse(options.no_launch)


class OpenGodotDevOrchestrationTests(unittest.TestCase):
    def test_parallel_lock_acquisition_fails(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            first = acquire_worktree_lock(temp_dir)
            try:
                with self.assertRaises(RuntimeError) as ctx:
                    acquire_worktree_lock(temp_dir)
                self.assertIn("Another build, verification, or Editor launch owns this worktree", str(ctx.exception))
            finally:
                first.release()

    def test_worktree_identity_matches_psm1_shape(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            identity = worktree_identity(temp_dir)
            self.assertEqual(len(identity.key), 24)
            self.assertTrue(identity.user_directory_name.startswith("TacticsGodotDev/"))
            self.assertEqual(identity.user_directory_name, f"TacticsGodotDev/{identity.key}")

    def test_no_launch_writes_override_and_session(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            project_root = repo_root / "godot"
            project_root.mkdir(parents=True)
            (project_root / "project.godot").write_text("config_version=5\n", encoding="utf-8")
            (project_root / "Tactics.Godot.Adapter.csproj").write_text("<Project Sdk=\"Microsoft.NET.Sdk\" />", encoding="utf-8")
            (project_root / "addons/godot_ai").mkdir(parents=True)
            (project_root / "addons/godot_ai/plugin.cfg").write_text("[plugin]\n", encoding="utf-8")
            (repo_root / "Tools/migration/manifest").mkdir(parents=True)
            (repo_root / "Tools/migration/manifest/godot-tooling.json").write_text(
                json.dumps(
                    {
                        "godotVersion": "4.7.1-stable",
                        "dotnetSdk": "9.0.312",
                    }
                ),
                encoding="utf-8",
            )
            godot_bin = repo_root / "godot-bin"
            godot_bin.write_text("#!/bin/sh\necho 4.7.1.stable\n", encoding="utf-8")
            godot_bin.chmod(0o755)
            assembly_path = (
                project_root / ".godot/mono/temp/bin/Debug/Tactics.Godot.Adapter.dll"
            )
            assembly_path.parent.mkdir(parents=True, exist_ok=True)
            assembly_path.write_bytes(b"MZ")

            def fake_run(argv, *args, **kwargs):
                completed = mock.Mock()
                completed.returncode = 0
                completed.stdout = ""
                completed.stderr = ""
                if argv and argv[0].endswith("dotnet") and len(argv) > 1 and argv[1] == "--version":
                    completed.stdout = "9.0.312\n"
                elif argv and argv[-1] == "--version":
                    completed.stdout = "4.7.1.stable.mono\n"
                elif len(argv) >= 3 and argv[1] == "run":
                    completed.stdout = (
                        "Tactics.Godot.Adapter, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\n"
                    )
                return completed

            with mock.patch(
                "Tools.godot.lib.open_godot_dev_runner.subprocess.run", side_effect=fake_run
            ), mock.patch(
                "Tools.godot.lib.open_godot_dev_runner._maybe_sync_codex_config",
                return_value=False,
            ), mock.patch(
                "Tools.godot.lib.open_godot_dev_runner._build_adapter",
                return_value=assembly_path,
            ), mock.patch(
                "Tools.godot.lib.open_godot_dev_runner.list_godot_editor_processes",
                return_value=[],
            ), mock.patch(
                "Tools.godot.lib.open_godot_dev_runner.acquire_worktree_lock",
                side_effect=lambda repo: acquire_worktree_lock(repo),
            ), mock.patch(
                "Tools.godot.lib.open_godot_dev_runner.resolve_python.resolve_python_executable",
                return_value="/usr/bin/python3",
            ), mock.patch(
                "Tools.godot.lib.open_godot_dev_runner.resolve_dotnet_executable",
                return_value="/usr/bin/dotnet",
            ):
                exit_code = run_open_godot_dev(
                    OpenGodotDevOptions(
                        repo_root=repo_root,
                        godot_executable=str(godot_bin),
                        no_launch=True,
                    )
                )

            self.assertEqual(exit_code, 0)
            override = (project_root / "override.cfg").read_text(encoding="utf-8")
            self.assertIn('config/custom_user_dir_name="TacticsGodotDev/', override)
            session = json.loads(
                (project_root / ".godot/tactics-dev-session.json").read_text(encoding="utf-8")
            )
            self.assertEqual(session["schemaVersion"], 1)
            self.assertTrue(session["noLaunch"])
            self.assertNotIn("editorPid", session)


class OpenGodotDevLockContentionTests(unittest.TestCase):
    def test_second_runner_waits_on_lock_failure(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            repo_root = Path(temp_dir)
            project_root = repo_root / "godot"
            project_root.mkdir(parents=True)
            (project_root / "project.godot").write_text("config_version=5\n", encoding="utf-8")
            (project_root / "Tactics.Godot.Adapter.csproj").write_text("<Project Sdk=\"Microsoft.NET.Sdk\" />", encoding="utf-8")
            (project_root / "addons/godot_ai").mkdir(parents=True)
            (project_root / "addons/godot_ai/plugin.cfg").write_text("[plugin]\n", encoding="utf-8")
            (repo_root / "Tools/migration/manifest").mkdir(parents=True)
            (repo_root / "Tools/migration/manifest/godot-tooling.json").write_text(
                json.dumps({"godotVersion": "4.7.1-stable", "dotnetSdk": "9.0.312"}),
                encoding="utf-8",
            )
            godot_bin = repo_root / "godot-bin"
            godot_bin.write_text("#!/bin/sh\necho 4.7.1.stable\n", encoding="utf-8")
            godot_bin.chmod(0o755)

            hold = acquire_worktree_lock(str(repo_root))
            errors: list[str] = []

            def worker() -> None:
                try:
                    run_open_godot_dev(
                        OpenGodotDevOptions(
                            repo_root=repo_root,
                            godot_executable=str(godot_bin),
                            no_launch=True,
                        )
                    )
                except OpenGodotDevError as exc:
                    errors.append(str(exc))
                except RuntimeError as exc:
                    errors.append(str(exc))

            thread = threading.Thread(target=worker)
            thread.start()
            thread.join(timeout=5)
            hold.release()

            self.assertFalse(thread.is_alive())
            self.assertTrue(
                any("owns this worktree" in message for message in errors),
                errors,
            )


if __name__ == "__main__":
    unittest.main()
