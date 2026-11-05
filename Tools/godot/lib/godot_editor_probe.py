"""Detect Godot Editor processes bound to the canonical project root (cross-platform)."""

from __future__ import annotations

import os
import re
from dataclasses import dataclass


@dataclass(frozen=True)
class GodotEditorProcess:
    process_id: int
    name: str
    command_line: str


def _normalize_project_root(project_root: str) -> str:
    return os.path.abspath(project_root).replace("\\", "/").rstrip("/")


def _read_linux_cmdline(process_id: int) -> str | None:
    cmdline_path = f"/proc/{process_id}/cmdline"
    try:
        with open(cmdline_path, "rb") as handle:
            raw = handle.read()
    except OSError:
        return None
    if not raw:
        return None
    parts = raw.split(b"\0")
    text = " ".join(part.decode("utf-8", errors="replace") for part in parts if part).strip()
    return text or None


def _iter_process_records_linux() -> list[tuple[int, str, str]]:
    records: list[tuple[int, str, str]] = []
    proc_root = "/proc"
    if not os.path.isdir(proc_root):
        return records
    for entry in os.listdir(proc_root):
        if not re.fullmatch(r"\d+", entry):
            continue
        process_id = int(entry)
        command_line = _read_linux_cmdline(process_id)
        if not command_line:
            continue
        executable_token = command_line.split(None, 1)[0]
        name = os.path.basename(executable_token)
        if not name:
            continue
        records.append((process_id, name, command_line))
    return records


def _iter_process_records_windows() -> list[tuple[int, str, str]]:
    try:
        import subprocess

        result = subprocess.run(
            [
                "powershell",
                "-NoProfile",
                "-Command",
                (
                    "Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | "
                    "ForEach-Object { \"$($_.ProcessId)`t$($_.Name)`t$($_.CommandLine)\" }"
                ),
            ],
            capture_output=True,
            text=True,
            check=False,
        )
    except OSError:
        return []
    records: list[tuple[int, str, str]] = []
    for line in result.stdout.splitlines():
        parts = line.split("\t", 2)
        if len(parts) != 3:
            continue
        try:
            process_id = int(parts[0])
        except ValueError:
            continue
        records.append((process_id, parts[1], parts[2]))
    return records


def iter_process_records() -> list[tuple[int, str, str]]:
    if os.name == "nt":
        return _iter_process_records_windows()
    if os.name == "posix" and os.path.isdir("/proc"):
        return _iter_process_records_linux()
    return []


def list_godot_editor_processes(project_root: str) -> list[GodotEditorProcess]:
    normalized_root = _normalize_project_root(project_root)
    editors: list[GodotEditorProcess] = []
    for process_id, name, command_line in iter_process_records():
        if not name.startswith("Godot"):
            continue
        normalized_command = command_line.replace("\\", "/")
        if normalized_root.lower() not in normalized_command.lower():
            continue
        editors.append(
            GodotEditorProcess(process_id=process_id, name=name, command_line=command_line)
        )
    return editors
