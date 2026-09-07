#!/usr/bin/env python3
"""Validate manifest-derived third-party notice facts."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

NOTICE_PATTERN = re.compile(r"^\| Godot AI EditorPlugin \| ([^|]+) \| MIT \|", re.MULTILINE)


def validate(root: Path) -> None:
    manifest = json.loads(
        (root / "Tools/migration/manifest/godot-tooling.json").read_text(encoding="utf-8")
    )
    policy = manifest["godotAi"]
    expected = f"{str(policy['tag']).removeprefix('v')} (`{str(policy['commit'])[:8]}`)"
    notice = (root / "THIRD_PARTY_NOTICES.md").read_text(encoding="utf-8")
    matches = NOTICE_PATTERN.findall(notice)
    if matches != [expected]:
        actual = matches[0] if len(matches) == 1 else f"{len(matches)} matching rows"
        raise ValueError(f"Godot AI third-party notice is stale: expected {expected}, found {actual}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    args = parser.parse_args()
    try:
        validate(args.root.resolve())
    except (OSError, KeyError, ValueError, json.JSONDecodeError) as error:
        print(f"THIRD_PARTY_NOTICE_ERROR: {error}")
        return 1
    print("THIRD_PARTY_NOTICE_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
