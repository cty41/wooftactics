"""Open-GodotDev orchestration (cross-platform, mirrors Open-GodotDev.ps1 semantics)."""

from __future__ import annotations

import json
import os
import re
import subprocess
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

from Tools.godot.lib import resolve_python
from Tools.godot.lib.godot_editor_probe import list_godot_editor_processes
from Tools.godot.lib.tool_probe import (
    GodotNotFoundError,
    resolve_dotnet_executable,
    resolve_godot_executable,
)
from Tools.godot.lib.skip_log import log_skip_windows_only
from Tools.godot.lib.worktree_lock import WorktreeLock, acquire_worktree_lock, worktree_identity


class OpenGodotDevError(RuntimeError):
    """User-facing failure from Open-GodotDev orchestration."""


@dataclass(frozen=True)
class OpenGodotDevOptions:
    mode: str = "Agent"
    user_data_profile: str = "Worktree"
    godot_ai_profile: str = "phase3-observe"
    godot_executable: str | None = None
    no_launch: bool = True
    repo_root: Path | None = None


def _load_manifest(repo_root: Path) -> dict:
    manifest_path = repo_root / "Tools/migration/manifest/godot-tooling.json"
    with manifest_path.open(encoding="utf-8") as handle:
        return json.load(handle)


def _parse_semver(text: str) -> tuple[int, int, int]:
    match = re.match(r"(\d+)\.(\d+)\.(\d+)", text.strip())
    if not match:
        raise OpenGodotDevError(f"Could not parse version: {text!r}")
    return int(match.group(1)), int(match.group(2)), int(match.group(3))


def _assert_godot_version(godot_executable: str, manifest: dict) -> None:
    expected = str(manifest["godotVersion"]).split("-", 1)[0]
    completed = subprocess.run(
        [godot_executable, "--version"],
        capture_output=True,
        text=True,
        check=False,
    )
    if completed.returncode != 0:
        raise OpenGodotDevError(
            f"Godot --version failed with exit code {completed.returncode}: {godot_executable}"
        )
    actual_match = re.match(r"(\d+\.\d+\.\d+)", completed.stdout.strip())
    if not actual_match:
        raise OpenGodotDevError(
            f"Could not parse Godot --version output: {completed.stdout.strip()!r}"
        )
    actual = actual_match.group(1)
    if actual != expected:
        raise OpenGodotDevError(f"Expected Godot {expected}, found {actual} at {godot_executable}.")


def _assert_dotnet_sdk(manifest: dict, *, env: dict[str, str], dotnet_exe: str) -> None:
    expected_major, expected_minor, expected_patch = _parse_semver(str(manifest["dotnetSdk"]))
    expected_band = expected_patch // 100
    completed = subprocess.run(
        [dotnet_exe, "--version"],
        capture_output=True,
        text=True,
        check=False,
        env=env,
    )
    if completed.returncode != 0:
        raise OpenGodotDevError(f"dotnet --version failed with exit code {completed.returncode}")
    actual_text = completed.stdout.strip()
    actual_major, actual_minor, actual_patch = _parse_semver(actual_text)
    actual_band = actual_patch // 100
    if (
        actual_major != expected_major
        or actual_minor != expected_minor
        or actual_band != expected_band
    ):
        raise OpenGodotDevError(
            "Expected .NET SDK feature band "
            f"{expected_major}.{expected_minor}.{expected_band}xx, found {actual_text}."
        )


def _required_paths(repo_root: Path, godot_executable: str) -> None:
    project_root = repo_root / "godot"
    required = [
        Path(godot_executable),
        project_root / "project.godot",
        project_root / "Tactics.Godot.Adapter.csproj",
        project_root / "addons/godot_ai/plugin.cfg",
    ]
    for path in required:
        if not path.is_file():
            raise OpenGodotDevError(f"Required Godot development file is missing: {path}")


def _write_override_cfg(project_root: Path, user_directory: str) -> None:
    override_path = project_root / "override.cfg"
    body = (
        "[application]\n\n"
        "config/use_custom_user_dir=true\n"
        f'config/custom_user_dir_name="{user_directory}"\n'
    )
    override_path.write_text(body, encoding="utf-8", newline="\n")


def _user_codex_config_path() -> Path:
    home = os.environ.get("USERPROFILE") or os.environ.get("HOME") or ""
    return Path(home).expanduser() / ".codex" / "config.toml"


def _sync_codex_config(
    *,
    repo_root: Path,
    profile: str,
    bootstrap: bool,
    env: dict[str, str],
    python_exe: str,
) -> None:
    helper = repo_root / "Tools/migration/godot_ai_codex_config.py"
    argv = [
        python_exe,
        str(helper),
        "--root",
        str(repo_root),
        "--user-config",
        str(_user_codex_config_path()),
        "--profile",
        profile,
    ]
    if bootstrap:
        argv.append("--bootstrap")
    completed = subprocess.run(argv, cwd=repo_root, env=env, check=False)
    if completed.returncode != 0:
        raise OpenGodotDevError(
            f"Godot AI Codex configuration failed with exit code {completed.returncode}."
        )


def _maybe_sync_codex_config(
    *,
    repo_root: Path,
    profile: str,
    env: dict[str, str],
    python_exe: str,
) -> bool:
    config_path = repo_root / ".codex" / "config.toml"
    created_config = not config_path.is_file()
    if os.name != "nt":
        log_skip_windows_only(
            "Godot AI Codex configuration",
            "project-scoped MCP sync requires Windows pythonw.exe Attach bootstrap",
        )
        return False
    _sync_codex_config(
        repo_root=repo_root,
        profile=profile,
        bootstrap=created_config,
        env=env,
        python_exe=python_exe,
    )
    return created_config


def _build_adapter(*, repo_root: Path, dotnet_exe: str, env: dict[str, str]) -> Path:
    adapter_project = repo_root / "godot" / "Tactics.Godot.Adapter.csproj"
    completed = subprocess.run(
        [dotnet_exe, "build", str(adapter_project), "-c", "Debug", "-m:1"],
        cwd=repo_root,
        env=env,
        check=False,
    )
    if completed.returncode != 0:
        raise OpenGodotDevError(f"Godot Adapter Debug build failed with exit code {completed.returncode}.")
    assembly_path = (
        repo_root / "godot" / ".godot" / "mono" / "temp" / "bin" / "Debug" / "Tactics.Godot.Adapter.dll"
    )
    if not assembly_path.is_file():
        raise OpenGodotDevError(f"Production Adapter assembly is missing: {assembly_path}")
    return assembly_path


def _read_assembly_full_name(
    assembly_path: Path, *, repo_root: Path, dotnet_exe: str, env: dict[str, str]
) -> str:
    project = repo_root / "Tools/godot/lib/read_assembly_name/ReadAssemblyName.csproj"
    completed = subprocess.run(
        [
            dotnet_exe,
            "run",
            "--project",
            str(project),
            "-c",
            "Release",
            "--",
            str(assembly_path),
        ],
        cwd=repo_root,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )
    if completed.returncode != 0:
        detail = (completed.stderr or completed.stdout or "").strip()
        raise OpenGodotDevError(
            f"Could not read Adapter assembly identity ({assembly_path}): {detail}"
        )
    full_name = completed.stdout.strip()
    if not full_name.startswith("Tactics.Godot.Adapter,"):
        raise OpenGodotDevError(f"Unexpected Adapter assembly identity: {full_name}")
    return full_name


def _write_dev_session(
    *,
    session_path: Path,
    identity_key: str,
    repo_root: Path,
    project_root: Path,
    mode: str,
    user_data_profile: str,
    godot_ai_profile: str,
    editor_pid: int | None,
    no_launch: bool,
) -> None:
    session_path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "schemaVersion": 1,
        "worktreeKey": identity_key,
        "repoRoot": str(repo_root),
        "projectRoot": str(project_root),
        "mode": mode,
        "userDataProfile": user_data_profile,
        "godotAiProfile": godot_ai_profile,
        "startedAt": datetime.now(timezone.utc).isoformat(),
        "noLaunch": no_launch,
    }
    if editor_pid is not None:
        payload["editorPid"] = editor_pid
    session_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def run_open_godot_dev(options: OpenGodotDevOptions) -> int:
    repo_root = (options.repo_root or Path(__file__).resolve().parents[3]).resolve()
    project_root = repo_root / "godot"
    env = os.environ.copy()

    if options.mode == "Agent" and options.user_data_profile == "SharedManualQA":
        raise OpenGodotDevError(
            "SharedManualQA is a Human-only profile; Agent sessions must use worktree-isolated user data."
        )

    try:
        godot_executable = resolve_godot_executable(
            cli_value=options.godot_executable,
            env=env,
        )
    except GodotNotFoundError as exc:
        raise OpenGodotDevError(str(exc)) from exc

    manifest = _load_manifest(repo_root)
    _required_paths(repo_root, godot_executable)
    _assert_godot_version(godot_executable, manifest)

    python_exe = resolve_python.resolve_python_executable(env=env)
    dotnet_exe = resolve_dotnet_executable(env=env)
    _assert_dotnet_sdk(manifest, env=env, dotnet_exe=dotnet_exe)

    lock: WorktreeLock | None = None
    try:
        try:
            lock = acquire_worktree_lock(str(repo_root))
        except RuntimeError as exc:
            raise OpenGodotDevError(str(exc)) from exc
        identity = worktree_identity(str(repo_root))

        editors = list_godot_editor_processes(str(project_root))
        if len(editors) > 1:
            pids = ", ".join(str(item.process_id) for item in editors)
            raise OpenGodotDevError(f"Multiple Editors already target this worktree: {pids}")
        if len(editors) == 1:
            print(
                f"GODOT_EDITOR_ALREADY_OPEN pid={editors[0].process_id} project={project_root}",
                flush=True,
            )
            return 0

        user_directory = (
            "TacticsGodotManualQA"
            if options.user_data_profile == "SharedManualQA"
            else identity.user_directory_name
        )
        _write_override_cfg(project_root, user_directory)

        created_config = _maybe_sync_codex_config(
            repo_root=repo_root,
            profile=options.godot_ai_profile,
            env=env,
            python_exe=python_exe,
        )

        assembly_path = _build_adapter(repo_root=repo_root, dotnet_exe=dotnet_exe, env=env)
        assembly_full_name = _read_assembly_full_name(
            assembly_path,
            repo_root=repo_root,
            dotnet_exe=dotnet_exe,
            env=env,
        )

        session_path = project_root / ".godot" / "tactics-dev-session.json"

        if options.no_launch:
            _write_dev_session(
                session_path=session_path,
                identity_key=identity.key,
                repo_root=repo_root,
                project_root=project_root,
                mode=options.mode,
                user_data_profile=options.user_data_profile,
                godot_ai_profile=options.godot_ai_profile,
                editor_pid=None,
                no_launch=True,
            )
            print(
                "GODOT_DEV_READY "
                f"mode={options.mode} profile={options.user_data_profile} assembly={assembly_full_name}",
                flush=True,
            )
            if created_config:
                print("CODEX_RESTART_REQUIRED", flush=True)
            return 0

        raise OpenGodotDevError(
            "Launching the Editor from the Python entry is not supported; "
            "use Tools/godot/Open-GodotDev.ps1 on Windows or pass --no-launch on Linux/cloud agents."
        )
    finally:
        if lock is not None:
            lock.release()
