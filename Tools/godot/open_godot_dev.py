#!/usr/bin/env python3
"""Cross-platform Open-GodotDev entry (Slice 4 — ``-NoLaunch`` preparation).

Windows daily development should continue using ``Tools/godot/Open-GodotDev.ps1``.
On Linux / cloud agents, this script mirrors the ``-NoLaunch`` branch: worktree lock,
override.cfg, Codex project config sync, serial Adapter build, and session JSON.

Example (from repository root, with PYTHONPATH=.):

    export PATH="$HOME/.dotnet:$PATH"
    export GODOT_EXECUTABLE=/path/to/Godot_v4.7.1-stable_mono_linux.x86_64/Godot_v4.7.1-stable_mono_linux.x86_64
    python Tools/godot/open_godot_dev.py --no-launch

Attach / godot-ai: transport and launcher discovery remain platform-specific per
``Tools/migration/manifest/godot-tooling.json`` (Windows attach uses consoleless
``pythonw.exe`` bootstrap from the Dock; Linux agents typically prepare with
``--no-launch`` and attach only when a compatible Editor session is already running).
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from Tools.godot.lib.open_godot_dev_runner import (
    OpenGodotDevError,
    OpenGodotDevOptions,
    run_open_godot_dev,
)


def _default_repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def parse_cli_args(argv: list[str] | None = None) -> OpenGodotDevOptions:
    parser = argparse.ArgumentParser(description="Cross-platform Open-GodotDev orchestration.")
    parser.add_argument(
        "--mode",
        default="Agent",
        choices=["Agent", "Human"],
        help="Session mode (default: Agent).",
    )
    parser.add_argument(
        "--user-data-profile",
        default="Worktree",
        choices=["Worktree", "SharedManualQA"],
        help="Godot user data isolation profile (default: Worktree).",
    )
    parser.add_argument(
        "--godot-ai-profile",
        default="phase3-observe",
        choices=["phase3-observe", "content-authoring", "ui-input", "presentation"],
        help="Codex godot-ai tool profile (default: phase3-observe).",
    )
    parser.add_argument(
        "--godot-executable",
        default=None,
        help="Godot binary (else GODOT_EXECUTABLE / GODOT_BIN).",
    )
    parser.add_argument(
        "--no-launch",
        action="store_true",
        help="Prepare Adapter DLL and session files without starting the Editor.",
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=None,
        help="Repository root (defaults to parent of Tools/).",
    )
    args = parser.parse_args(argv)
    return OpenGodotDevOptions(
        mode=args.mode,
        user_data_profile=args.user_data_profile,
        godot_ai_profile=args.godot_ai_profile,
        godot_executable=args.godot_executable,
        no_launch=args.no_launch,
        repo_root=args.repo_root,
    )


def main(argv: list[str] | None = None) -> int:
    if argv is None:
        argv = sys.argv[1:]
    parsed = parse_cli_args(argv)
    if not parsed.no_launch:
        print(
            "Only --no-launch is supported on the Python entry; "
            "use Tools/godot/Open-GodotDev.ps1 to start the Editor on Windows.",
            file=sys.stderr,
        )
        return 2
    try:
        return run_open_godot_dev(parsed)
    except OpenGodotDevError as exc:
        print(str(exc), file=sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
