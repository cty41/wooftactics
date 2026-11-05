#!/usr/bin/env python3
"""Cross-platform Verify entry for Linux / cloud agents (Slice 2 — agent profile).

Windows daily development should continue using ``Tools/godot/Verify-GodotProject.ps1``.
This script runs a documented portable subset and logs ``SKIP (Windows-only): …`` for
steps that remain on the full Windows path.

Example (from repository root, with PYTHONPATH=.):

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
from Tools.godot.lib.verify_steps import PROFILE_AGENT, steps_for_profile


def _default_repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Cross-platform Godot Verify orchestration.")
    parser.add_argument(
        "--profile",
        default=PROFILE_AGENT,
        choices=[PROFILE_AGENT],
        help="Verification profile (agent = portable Linux/cloud subset).",
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
        help="Godot binary for optional GdUnit subset (else GODOT_EXECUTABLE / GODOT_BIN).",
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
    )


if __name__ == "__main__":
    raise SystemExit(main())
