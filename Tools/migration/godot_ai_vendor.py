#!/usr/bin/env python3
"""Validate the checked-in Godot AI plugin against the pinned manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path

try:
    from Tools.migration.godot_ai_baseline import canonical_bytes, plugin_version
except ModuleNotFoundError:  # Direct script execution from the repository root.
    from godot_ai_baseline import canonical_bytes, plugin_version


def _is_link_or_reparse(path: Path) -> bool:
    attributes = getattr(path.lstat(), "st_file_attributes", 0)
    return path.is_symlink() or bool(attributes & getattr(os.stat_result, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def tree_digest(root: Path) -> tuple[int, str]:
    entries = list(root.rglob("*"))
    linked = [path for path in entries if _is_link_or_reparse(path)]
    if linked:
        raise ValueError(f"vendored Godot AI tree contains a link or reparse point: {linked[0]}")
    files = sorted(
        (path for path in entries if path.is_file()),
        key=lambda path: path.relative_to(root).as_posix(),
    )
    digest = hashlib.sha256()
    for path in files:
        digest.update(path.relative_to(root).as_posix().encode("utf-8"))
        digest.update(b"\0")
        digest.update(hashlib.sha256(canonical_bytes(path)).digest())
    return len(files), digest.hexdigest()


def resolve_vendor_path(root: Path, configured: object) -> Path:
    relative = Path(str(configured))
    if relative.is_absolute() or ".." in relative.parts:
        raise ValueError(f"godot-ai vendorPath must stay inside repository: {configured}")
    resolved_root = root.resolve()
    resolved = (resolved_root / relative).resolve()
    if resolved == resolved_root or resolved_root not in resolved.parents:
        raise ValueError(f"godot-ai vendorPath must stay inside repository: {configured}")
    return resolved


def validate(root: Path) -> dict[str, object]:
    manifest_path = root / "Tools/migration/manifest/godot-tooling.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    policy = manifest["godotAi"]
    tag = str(policy["tag"])
    commit = str(policy["commit"])
    expected_tree = str(policy["vendorTreeSha256"])
    expected_license = str(policy["licenseSha256"])
    if not tag.startswith("v") or not commit:
        raise ValueError("godot-ai manifest requires a v-prefixed tag and commit")

    vendor = resolve_vendor_path(root, policy["vendorPath"])
    if not vendor.is_dir():
        raise ValueError(f"vendored Godot AI directory is missing: {vendor}")
    version = plugin_version(vendor / "plugin.cfg")
    if version != tag.removeprefix("v"):
        raise ValueError(f"godot-ai manifest/plugin version mismatch: tag={tag}, plugin={version}")

    count, digest = tree_digest(vendor)
    if digest != expected_tree:
        raise ValueError(
            f"vendored Godot AI tree drifted: files={count}, sha256={digest}; "
            f"run Tools/migration/setup_godot_ai.ps1 with an explicitly reviewed tag"
        )
    license_hash = hashlib.sha256(canonical_bytes(vendor / "LICENSE")).hexdigest()
    if license_hash != expected_license:
        raise ValueError(
            f"vendored Godot AI license drifted: sha256={license_hash}; expected={expected_license}"
        )
    return {
        "version": version,
        "tag": tag,
        "commit": commit,
        "files": count,
        "treeSha256": digest,
        "licenseSha256": license_hash,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    args = parser.parse_args()
    try:
        result = validate(args.root.resolve())
    except (OSError, KeyError, RuntimeError, ValueError, json.JSONDecodeError) as error:
        print(f"GODOT_AI_VENDOR_ERROR: {error}")
        return 1
    print(f"GODOT_AI_VENDOR_OK version={result['version']} files={result['files']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
