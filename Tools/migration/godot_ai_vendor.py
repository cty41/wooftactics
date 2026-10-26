#!/usr/bin/env python3
"""Validate the checked-in Godot AI plugin against the pinned manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def canonical_bytes(path: Path) -> bytes:
    """Match the repository's text eol=lf contract across checkout platforms."""
    payload = path.read_bytes()
    if b"\0" in payload:
        return payload
    return payload.replace(b"\r\n", b"\n")


def tree_digest(root: Path) -> tuple[int, str]:
    files = sorted(
        (path for path in root.rglob("*") if path.is_file()),
        key=lambda path: path.relative_to(root).as_posix(),
    )
    digest = hashlib.sha256()
    for path in files:
        digest.update(path.relative_to(root).as_posix().encode("utf-8"))
        digest.update(b"\0")
        digest.update(hashlib.sha256(canonical_bytes(path)).digest())
    return len(files), digest.hexdigest()


def validate(root: Path) -> dict:
    manifest = json.loads((root / "Tools/migration/manifest/godot-tooling.json").read_text(encoding="utf-8"))
    policy = manifest["godotAi"]
    vendor = root / policy["vendorPath"]
    if not vendor.is_dir():
        raise ValueError(f"vendored Godot AI directory is missing: {vendor}")
    count, digest = tree_digest(vendor)
    if count != policy["vendorFileCount"] or digest != policy["vendorTreeSha256"]:
        raise ValueError(
            f"vendored Godot AI tree drifted: files={count}, sha256={digest}"
        )
    plugin_hash = hashlib.sha256(canonical_bytes(vendor / "plugin.cfg")).hexdigest()
    if plugin_hash != policy["vendorPluginCfgSha256"]:
        raise ValueError(f"vendored plugin.cfg drifted: sha256={plugin_hash}")
    license_hash = hashlib.sha256(canonical_bytes(vendor / "LICENSE")).hexdigest()
    if license_hash != policy["vendorLicenseSha256"]:
        raise ValueError(f"vendored Godot AI license drifted: sha256={license_hash}")
    return policy


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    args = parser.parse_args()
    try:
        policy = validate(args.root.resolve())
    except (OSError, KeyError, ValueError, json.JSONDecodeError) as error:
        print(f"GODOT_AI_VENDOR_ERROR: {error}")
        return 1
    tag = policy["tag"].removeprefix("v")
    files = policy["vendorFileCount"]
    print(f"GODOT_AI_VENDOR_OK version={tag} files={files}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
