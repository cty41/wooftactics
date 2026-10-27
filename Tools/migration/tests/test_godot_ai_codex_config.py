from __future__ import annotations

import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from Tools.migration.godot_ai_codex_config import (
    CodexGodotAiConfigError,
    apply_profile,
    bootstrap_configuration,
    check_configuration,
    find_server_block,
    import_generated_user_entry,
    load_policy,
    parse_server_table,
    resolve_profile_tools,
)


class GodotAiCodexConfigTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        (self.root / "godot").mkdir()
        (self.root / "godot" / "project.godot").write_text("[application]\n", encoding="utf-8")
        (self.root / ".codex").mkdir()
        (self.root / ".gitignore").write_text("/.codex/config.toml\n", encoding="utf-8")
        manifest_directory = self.root / "Tools" / "migration" / "manifest"
        manifest_directory.mkdir(parents=True)
        self.manifest_path = manifest_directory / "godot-tooling.json"
        self.manifest_path.write_text(
            json.dumps(self._manifest(), indent=2) + "\n", encoding="utf-8"
        )
        subprocess.run(["git", "init", "--quiet", str(self.root)], check=True)
        self.user_config = self.root / "user-config.toml"

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_import_moves_only_generated_table_and_applies_observe_profile(self) -> None:
        prefix = '[mcp_servers.context7]\ncommand = "npx"\n\n'
        suffix = '[projects."d:\\\\"]\ntrust_level = "trusted"\n'
        self.user_config.write_text(prefix + self._generated_block() + suffix, encoding="utf-8")

        result = import_generated_user_entry(
            self.root, self.user_config, "phase3-observe", check_command_exists=False
        )

        self.assertTrue(result.project_changed)
        self.assertTrue(result.user_changed)
        self.assertEqual(self.user_config.read_text(encoding="utf-8"), prefix + suffix)
        project = self._project_server()
        self.assertEqual(project["command"], r"C:\Tools\pythonw.exe")
        self.assertEqual(project["enabled_tools"], sorted(self._observe_tools()))
        self.assertNotIn("client_manage", project["enabled_tools"])

    def test_repeated_import_is_a_no_op(self) -> None:
        self.user_config.write_text(self._generated_block(), encoding="utf-8")
        import_generated_user_entry(
            self.root, self.user_config, "phase3-observe", check_command_exists=False
        )
        project_before = self._project_path().read_bytes()
        user_before = self.user_config.read_bytes()

        result = import_generated_user_entry(
            self.root, self.user_config, "phase3-observe", check_command_exists=False
        )

        self.assertFalse(result.project_changed)
        self.assertFalse(result.user_changed)
        self.assertEqual(self._project_path().read_bytes(), project_before)
        self.assertEqual(self.user_config.read_bytes(), user_before)

    def test_profiles_are_cumulative_and_forbidden_tools_never_appear(self) -> None:
        policy = load_policy(self.root)
        observe = set(resolve_profile_tools(policy, "phase3-observe"))
        content = set(resolve_profile_tools(policy, "content-authoring"))
        ui = set(resolve_profile_tools(policy, "ui-input"))
        presentation = set(resolve_profile_tools(policy, "presentation"))

        self.assertLess(observe, content)
        self.assertLess(content, ui)
        self.assertLess(ui, presentation)
        for tools in (observe, content, ui, presentation):
            self.assertFalse(tools.intersection(policy.forbidden_tools))

    def test_profile_switch_updates_only_the_project_entry(self) -> None:
        self.user_config.write_text(self._generated_block(), encoding="utf-8")
        import_generated_user_entry(
            self.root, self.user_config, "phase3-observe", check_command_exists=False
        )
        user_before = self.user_config.read_bytes()

        result = apply_profile(
            self.root, self.user_config, "content-authoring", check_command_exists=False
        )

        self.assertTrue(result.project_changed)
        self.assertFalse(result.user_changed)
        self.assertEqual(self.user_config.read_bytes(), user_before)
        policy = load_policy(self.root)
        self.assertEqual(
            self._project_server()["enabled_tools"],
            list(resolve_profile_tools(policy, "content-authoring")),
        )

    def test_check_rejects_a_remaining_user_level_entry(self) -> None:
        self.user_config.write_text(self._generated_block(), encoding="utf-8")
        import_generated_user_entry(
            self.root, self.user_config, "phase3-observe", check_command_exists=False
        )
        self.user_config.write_text(self._generated_block(), encoding="utf-8")

        with self.assertRaisesRegex(CodexGodotAiConfigError, "user-level"):
            check_configuration(
                self.root, self.user_config, "phase3-observe", check_command_exists=False
            )

    def test_check_infers_the_active_profile(self) -> None:
        self.user_config.write_text(self._generated_block(), encoding="utf-8")
        import_generated_user_entry(
            self.root, self.user_config, "phase3-observe", check_command_exists=False
        )
        apply_profile(
            self.root, self.user_config, "content-authoring", check_command_exists=False
        )

        result = check_configuration(
            self.root, self.user_config, None, check_command_exists=False
        )

        self.assertEqual(result.profile, "content-authoring")

    def test_import_rejects_launch_contract_drift(self) -> None:
        variants = {
            "pythonw": self._generated_block(command=r"C:\Tools\python.exe"),
            "version": self._generated_block(version="3.1.1"),
            "http": self._generated_block(http_port=8001),
            "websocket": self._generated_block(websocket_port=9501),
            "bootstrap": self._generated_block(bootstrap="print('visible console')"),
            "link-mode": self._generated_block(link_mode="symlink"),
            **{
                f"missing {name}": self._generated_block(drop=(name,))
                for name in (
                    "--isolated",
                    "--no-config",
                    "--no-env-file",
                    "--no-sources",
                    "--no-build",
                    "--index-strategy",
                    "--keyring-provider",
                    "--index",
                    "--default-index",
                    "--find-links",
                )
            },
        }
        for name, block in variants.items():
            with self.subTest(name=name):
                self.user_config.write_text(block, encoding="utf-8")
                with self.assertRaises(CodexGodotAiConfigError):
                    import_generated_user_entry(
                        self.root,
                        self.user_config,
                        "phase3-observe",
                        check_command_exists=False,
                    )
                self._project_path().unlink(missing_ok=True)

    def test_import_accepts_launcher_with_or_without_link_mode_copy(self) -> None:
        for link_mode in ("copy", None):
            with self.subTest(link_mode=link_mode):
                self.user_config.write_text(self._generated_block(link_mode=link_mode), encoding="utf-8")
                import_generated_user_entry(
                    self.root,
                    self.user_config,
                    "phase3-observe",
                    check_command_exists=False,
                )
                self._project_path().unlink(missing_ok=True)

    def test_dual_config_launch_drift_is_rejected(self) -> None:
        self.user_config.write_text(self._generated_block(), encoding="utf-8")
        import_generated_user_entry(
            self.root, self.user_config, "phase3-observe", check_command_exists=False
        )
        self.user_config.write_text(
            self._generated_block(uvx=r"C:\Other\uvx.exe"), encoding="utf-8"
        )

        with self.assertRaisesRegex(CodexGodotAiConfigError, "dual-config drift"):
            import_generated_user_entry(
                self.root, self.user_config, "phase3-observe", check_command_exists=False
            )

    def test_failed_transaction_restores_both_files(self) -> None:
        original_user = ("# personal settings\n" + self._generated_block()).encode("utf-8")
        self.user_config.write_bytes(original_user)

        def fail(stage: str) -> None:
            if stage == "after_project_replace":
                raise RuntimeError("injected")

        with self.assertRaisesRegex(RuntimeError, "injected"):
            import_generated_user_entry(
                self.root,
                self.user_config,
                "phase3-observe",
                failure_injector=fail,
                check_command_exists=False,
            )

        self.assertEqual(self.user_config.read_bytes(), original_user)
        self.assertFalse(self._project_path().exists())

    def test_import_rejects_a_tracked_project_config(self) -> None:
        self.user_config.write_text(self._generated_block(), encoding="utf-8")
        self._project_path().write_text("# tracked by mistake\n", encoding="utf-8")
        subprocess.run(
            ["git", "-C", str(self.root), "add", "-f", ".codex/config.toml"], check=True
        )

        with self.assertRaisesRegex(CodexGodotAiConfigError, "untracked"):
            import_generated_user_entry(
                self.root, self.user_config, "phase3-observe", check_command_exists=False
            )

    def test_bootstrap_creates_project_entry_without_user_export(self) -> None:
        result = bootstrap_configuration(
            self.root,
            self.user_config,
            "phase3-observe",
            python_executable=Path(r"C:\Tools\python.exe"),
            uvx_executable=Path(r"C:\Tools\uvx.exe"),
            check_command_exists=False,
        )

        self.assertTrue(result.project_changed)
        server = self._project_server()
        self.assertEqual(server["command"], r"C:\Tools\pythonw.exe")
        self.assertIn("creationflags=0x08000000", server["args"][1])
        self.assertIn("godot-ai==4.3.1", server["args"])
        self.assertEqual(server["enabled_tools"], sorted(self._observe_tools()))

    def test_bootstrap_rejects_user_level_entry(self) -> None:
        self.user_config.write_text(self._generated_block(), encoding="utf-8")
        with self.assertRaisesRegex(CodexGodotAiConfigError, "user-level"):
            bootstrap_configuration(
                self.root,
                self.user_config,
                "phase3-observe",
                python_executable=Path(r"C:\Tools\python.exe"),
                uvx_executable=Path(r"C:\Tools\uvx.exe"),
                check_command_exists=False,
            )

    def test_manifest_profile_cycle_is_rejected(self) -> None:
        manifest = self._manifest()
        profiles = manifest["godotAi"]["codexMcp"]["profiles"]
        profiles["phase3-observe"]["extends"] = "presentation"
        self.manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

        with self.assertRaisesRegex(CodexGodotAiConfigError, "cyclic"):
            load_policy(self.root)

    def _project_path(self) -> Path:
        return self.root / ".codex" / "config.toml"

    def _project_server(self) -> dict:
        block = find_server_block(self._project_path().read_text(encoding="utf-8"))
        self.assertIsNotNone(block)
        return parse_server_table(block.text)

    @staticmethod
    def _observe_tools() -> list[str]:
        return [
            "api_manage",
            "editor_reload_plugin",
            "editor_screenshot",
            "editor_state",
            "logs_read",
            "node_find",
            "node_get_properties",
            "project_manage",
            "project_run",
            "resource_manage",
            "scene_get_hierarchy",
            "scene_open",
            "session_activate",
            "session_manage",
            "test_manage",
            "test_run",
        ]

    def _manifest(self) -> dict:
        return {
            "schemaVersion": 1,
            "godotAi": {
                "tag": "v4.3.1",
                "codexMcp": {
                    "scope": "project",
                    "configPath": ".codex/config.toml",
                    "transport": "attach",
                    "httpPort": 8000,
                    "websocketPort": 9500,
                    "startupTimeoutSec": 60,
                    "toolTimeoutSec": 360,
                    "defaultProfile": "phase3-observe",
                    "profileOrder": [
                        "phase3-observe",
                        "content-authoring",
                        "ui-input",
                        "presentation",
                    ],
                    "profiles": {
                        "phase3-observe": {"extends": None, "tools": self._observe_tools()},
                        "content-authoring": {
                            "extends": "phase3-observe",
                            "tools": [
                                "batch_execute",
                                "node_create",
                                "node_manage",
                                "node_set_property",
                                "scene_manage",
                                "scene_save",
                                "signal_manage",
                            ],
                        },
                        "ui-input": {
                            "extends": "content-authoring",
                            "tools": [
                                "game_manage",
                                "input_map_manage",
                                "theme_manage",
                                "ui_manage",
                            ],
                        },
                        "presentation": {
                            "extends": "ui-input",
                            "tools": [
                                "animation_create",
                                "animation_manage",
                                "audio_manage",
                                "camera_manage",
                                "material_manage",
                                "particle_manage",
                            ],
                        },
                    },
                    "alwaysForbiddenTools": [
                        "autoload_manage",
                        "client_manage",
                        "filesystem_manage",
                        "script_attach",
                        "script_create",
                        "script_patch",
                    ],
                },
            },
        }

    @staticmethod
    def _generated_block(
        *,
        command: str = r"C:\Tools\pythonw.exe",
        uvx: str = r"C:\Tools\uvx.exe",
        version: str = "4.3.1",
        http_port: int = 8000,
        websocket_port: int = 9500,
        bootstrap: str = (
            "import subprocess,sys; raise SystemExit(subprocess.call(sys.argv[1:], "
            "stdin=sys.stdin, stdout=sys.stdout, stderr=sys.stderr, creationflags=0x08000000))"
        ),
        link_mode: str | None = "copy",
        drop: tuple[str, ...] = (),
    ) -> str:
        isolation = [
            "--isolated",
            "--no-config",
            "--no-env-file",
            "--no-sources",
            "--no-build",
            "--index-strategy",
            "first-index",
            "--keyring-provider",
            "disabled",
            "--index",
            "https://pypi.org/simple",
            "--default-index",
            "https://pypi.org/simple",
            "--find-links",
            "https://pypi.org/simple/godot-ai/",
        ]
        for name in drop:  # remove a flag, or an option together with its value
            index = isolation.index(name)
            width = 1 if name in {"--isolated", "--no-config", "--no-env-file", "--no-sources", "--no-build"} else 2
            del isolation[index : index + width]
        if link_mode is not None:
            isolation += ["--link-mode", link_mode]
        args = [
            "-c",
            bootstrap,
            uvx,
            *isolation,
            "--from",
            f"godot-ai=={version}",
            "godot-ai",
            "attach",
            "--port",
            str(http_port),
            "--ws-port",
            str(websocket_port),
        ]
        lines = [
            '[mcp_servers."godot-ai"]',
            f"command = {json.dumps(command)}",
            "args = [",
            *(f"  {json.dumps(value)}," for value in args),
            "]",
            "enabled = true",
            "startup_timeout_sec = 60",
            "tool_timeout_sec = 360",
            "",
        ]
        return "\n".join(lines)


class GodotAiCodexRepositoryPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = Path(__file__).resolve().parents[3]

    def test_repository_manifest_profiles_are_cumulative_and_safe(self) -> None:
        policy = load_policy(self.root)
        previous: set[str] = set()
        for profile in policy.profile_order:
            current = set(resolve_profile_tools(policy, profile))
            self.assertTrue(previous.issubset(current))
            self.assertFalse(current.intersection(policy.forbidden_tools))
            previous = current

    def test_repository_project_config_is_ignored_and_untracked(self) -> None:
        config_path = ".codex/config.toml"
        ignored = subprocess.run(
            ["git", "-C", str(self.root), "check-ignore", "--no-index", "--quiet", "--", config_path],
            check=False,
        )
        tracked = subprocess.run(
            ["git", "-C", str(self.root), "ls-files", "--error-unmatch", "--", config_path],
            capture_output=True,
            check=False,
        )

        self.assertEqual(ignored.returncode, 0)
        self.assertNotEqual(tracked.returncode, 0)

    @unittest.skipUnless(sys.platform == "win32", "PowerShell wrapper is Windows-only")
    def test_powershell_wrapper_resolves_its_default_project_root(self) -> None:
        pythonw = Path(sys.executable).with_name("pythonw.exe")
        if not pythonw.is_file() or shutil.which("powershell") is None:
            self.skipTest("Windows Python or PowerShell launcher is unavailable")

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            migration = root / "Tools" / "migration"
            godot_tools = root / "Tools" / "godot"
            manifest = migration / "manifest"
            manifest.mkdir(parents=True)
            godot_tools.mkdir(parents=True)
            (root / "godot").mkdir()
            (root / "godot" / "project.godot").write_text("[application]\n", encoding="utf-8")
            (root / ".gitignore").write_text("/.codex/config.toml\n", encoding="utf-8")
            shutil.copy2(
                self.root / "Tools" / "godot" / "Sync-GodotAiCodexConfig.ps1",
                godot_tools / "Sync-GodotAiCodexConfig.ps1",
            )
            shutil.copy2(
                self.root / "Tools" / "migration" / "godot_ai_codex_config.py",
                migration / "godot_ai_codex_config.py",
            )
            shutil.copy2(
                self.root / "Tools" / "migration" / "manifest" / "godot-tooling.json",
                manifest / "godot-tooling.json",
            )
            subprocess.run(["git", "init", "--quiet", str(root)], check=True)
            user_config = root / "user.toml"
            user_config.write_text("", encoding="utf-8")

            policy = load_policy(root)
            generated = GodotAiCodexConfigTests._generated_block(command=str(pythonw))
            server = parse_server_table(find_server_block(generated).text)
            project_config = root / ".codex" / "config.toml"
            project_config.parent.mkdir()
            project_config.write_text(
                self._render_repository_project_entry(server, policy), encoding="utf-8"
            )

            completed = subprocess.run(
                [
                    "powershell",
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    str(godot_tools / "Sync-GodotAiCodexConfig.ps1"),
                    "-Check",
                    "-UserConfig",
                    str(user_config),
                ],
                cwd=root,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertIn('"mode": "check"', completed.stdout)

    @staticmethod
    def _render_repository_project_entry(server: dict, policy) -> str:
        from Tools.migration.godot_ai_codex_config import render_project_server

        return render_project_server(
            server,
            policy,
            resolve_profile_tools(policy, policy.default_profile),
        )


if __name__ == "__main__":
    unittest.main()
