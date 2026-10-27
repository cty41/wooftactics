"""Contract tests for the Cursor beforeMCPExecution godot-ai guard."""

from __future__ import annotations

import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
GUARD_SCRIPT = REPO_ROOT / "Tools" / "godot" / "Invoke-CursorMcpGuard.ps1"
HOOKS_JSON = REPO_ROOT / ".cursor" / "hooks.json"
MANIFEST = REPO_ROOT / "Tools" / "migration" / "manifest" / "godot-tooling.json"
FORBIDDEN = (
    "autoload_manage",
    "client_manage",
    "filesystem_manage",
    "script_attach",
    "script_create",
    "script_patch",
)


def _run_guard(payload: object | str, *, manifest: Path | None = None) -> subprocess.CompletedProcess[str]:
    stdin = payload if isinstance(payload, str) else json.dumps(payload)
    env = os.environ.copy()
    if manifest is not None:
        env["TACTICS_GODOT_AI_MANIFEST"] = str(manifest)
    else:
        env.pop("TACTICS_GODOT_AI_MANIFEST", None)
    return subprocess.run(
        [
            "powershell",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(GUARD_SCRIPT),
        ],
        input=stdin,
        text=True,
        capture_output=True,
        cwd=str(REPO_ROOT),
        env=env,
        check=False,
    )


def _decision(result: subprocess.CompletedProcess[str]) -> dict:
    stdout = (result.stdout or "").strip()
    assert stdout, f"guard produced no stdout; stderr={result.stderr!r} code={result.returncode}"
    # PowerShell may emit BOM or trailing newlines; take the last JSON object line.
    for line in reversed(stdout.splitlines()):
        line = line.strip()
        if line.startswith("{"):
            return json.loads(line)
    raise AssertionError(f"no JSON decision in stdout: {stdout!r}")


class CursorMcpGuardTests(unittest.TestCase):
    def test_hooks_json_points_at_fail_closed_guard(self) -> None:
        self.assertTrue(HOOKS_JSON.is_file())
        self.assertTrue(GUARD_SCRIPT.is_file())
        hooks = json.loads(HOOKS_JSON.read_text(encoding="utf-8"))
        entries = hooks["hooks"]["beforeMCPExecution"]
        self.assertEqual(len(entries), 1)
        entry = entries[0]
        self.assertTrue(entry.get("failClosed") is True)
        self.assertIn("Invoke-CursorMcpGuard.ps1", entry["command"])
        self.assertIn("Tools/godot/Invoke-CursorMcpGuard.ps1", entry["command"].replace("\\", "/"))

    def test_manifest_deny_list_matches_repository_contract(self) -> None:
        policy = json.loads(MANIFEST.read_text(encoding="utf-8"))["godotAi"]["codexMcp"]
        self.assertEqual(sorted(policy["alwaysForbiddenTools"]), sorted(FORBIDDEN))

    def test_each_forbidden_tool_is_denied(self) -> None:
        for name in FORBIDDEN:
            with self.subTest(tool=name):
                result = _run_guard({"tool_name": name, "mcp_server_name": "godot-ai"})
                decision = _decision(result)
                self.assertEqual(decision["permission"], "deny")
                self.assertEqual(result.returncode, 2)
                self.assertIn(name, decision.get("agent_message", ""))

    def test_observe_tools_are_allowed(self) -> None:
        for name in ("editor_state", "session_manage", "logs_read", "api_manage"):
            with self.subTest(tool=name):
                result = _run_guard({"tool_name": name, "mcp_server_name": "godot-ai"})
                decision = _decision(result)
                self.assertEqual(decision["permission"], "allow")
                self.assertEqual(result.returncode, 0)

    def test_malformed_json_is_denied(self) -> None:
        result = _run_guard("{not-json")
        decision = _decision(result)
        self.assertEqual(decision["permission"], "deny")
        self.assertEqual(result.returncode, 2)

    def test_missing_tool_name_is_denied(self) -> None:
        result = _run_guard({"mcp_server_name": "godot-ai"})
        decision = _decision(result)
        self.assertEqual(decision["permission"], "deny")
        self.assertEqual(result.returncode, 2)

    def test_empty_stdin_is_denied(self) -> None:
        result = _run_guard("")
        decision = _decision(result)
        self.assertEqual(decision["permission"], "deny")
        self.assertEqual(result.returncode, 2)

    def test_server_rename_does_not_bypass_deny(self) -> None:
        result = _run_guard({"tool_name": "script_create", "mcp_server_name": "renamed-server"})
        decision = _decision(result)
        self.assertEqual(decision["permission"], "deny")
        self.assertEqual(result.returncode, 2)

    def test_override_manifest_controls_deny_list(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            manifest_path = Path(tmp) / "godot-tooling.json"
            manifest_path.write_text(
                json.dumps(
                    {
                        "godotAi": {
                            "codexMcp": {
                                "alwaysForbiddenTools": ["editor_state"],
                            }
                        }
                    }
                ),
                encoding="utf-8",
            )
            denied = _run_guard(
                {"tool_name": "editor_state", "mcp_server_name": "godot-ai"},
                manifest=manifest_path,
            )
            allowed = _run_guard(
                {"tool_name": "script_create", "mcp_server_name": "godot-ai"},
                manifest=manifest_path,
            )
            self.assertEqual(_decision(denied)["permission"], "deny")
            self.assertEqual(denied.returncode, 2)
            self.assertEqual(_decision(allowed)["permission"], "allow")
            self.assertEqual(allowed.returncode, 0)

    def test_empty_forbid_list_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            manifest_path = Path(tmp) / "godot-tooling.json"
            manifest_path.write_text(
                json.dumps({"godotAi": {"codexMcp": {"alwaysForbiddenTools": []}}}),
                encoding="utf-8",
            )
            result = _run_guard(
                {"tool_name": "editor_state", "mcp_server_name": "godot-ai"},
                manifest=manifest_path,
            )
            self.assertEqual(_decision(result)["permission"], "deny")
            self.assertEqual(result.returncode, 2)


if __name__ == "__main__":
    unittest.main()
