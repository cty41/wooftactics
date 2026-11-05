#!/usr/bin/env python3
"""Cross-platform Verify orchestration (Slice 2 agent profile, Slice 5 windows profile).

Windows daily development should continue using ``Tools/godot/Verify-GodotProject.ps1``,
which forwards to this module with ``--profile windows``.

Linux / cloud agents:

    python Tools/godot/verify_godot_project.py --profile agent

With Godot for an optional GdUnit subset:

    export PATH="$HOME/.dotnet:$PATH"
    python Tools/godot/verify_godot_project.py --profile agent --godot-executable /path/to/Godot
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from Tools.godot.lib.tool_probe import try_resolve_godot_executable
from Tools.godot.lib.verify_runner import run_verify_profile
from Tools.godot.lib.verify_steps import PROFILE_AGENT, PROFILE_WINDOWS, steps_for_profile


def _default_repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Cross-platform Godot Verify orchestration.")
    parser.add_argument(
        "--profile",
        default=PROFILE_AGENT,
        choices=[PROFILE_AGENT, PROFILE_WINDOWS],
        help="Verification profile (agent = portable subset; windows = full Windows path).",
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=None,
        help="Repository root (defaults to parent of Tools/).",
    )
    parser.add_argument(
        "--godot-executable",
        default=None,
        help="Godot binary (required for windows profile; optional for agent GdUnit subset).",
    )
    parser.add_argument(
        "--require-artwork",
        action="store_true",
        help="Fail when MaLiang submodule is not initialized (Windows profile).",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = _parse_args(sys.argv[1:] if argv is None else argv)
    repo_root = args.repo_root or _default_repo_root()
    godot = try_resolve_godot_executable(cli_value=args.godot_executable)
    steps = steps_for_profile(args.profile)
    return run_verify_profile(
        repo_root=repo_root,
        profile=args.profile,
        steps=steps,
        godot_executable=godot,
        require_artwork=args.require_artwork,
    )


if __name__ == "__main__":
    raise SystemExit(main())
