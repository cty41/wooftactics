"""Read-only validation for a manifest-pinned local godot-ai checkout."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path


def canonical_bytes(path: Path) -> bytes:
    payload = path.read_bytes()
    return payload if b"\0" in payload else payload.replace(b"\r\n", b"\n")


def plugin_version(plugin_cfg: Path) -> str:
    match = re.search(r'^version="([^"]+)"\s*$', plugin_cfg.read_text(encoding="utf-8"), re.MULTILINE)
    if match is None:
        raise RuntimeError(f"godot-ai plugin version is missing: {plugin_cfg}")
    return match.group(1)


def load_policy(manifest_path: Path) -> dict[str, object]:
    document = json.loads(manifest_path.read_text(encoding="utf-8"))
    policy = document.get("godotAi")
    if not isinstance(policy, dict):
        raise RuntimeError(f"godot-tooling manifest has no godotAi object: {manifest_path}")
    return policy


def git_output(path: Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", str(path), *args], text=True).strip()


def validate_checkout(path: Path, manifest_path: Path) -> dict[str, str]:
    policy = load_policy(manifest_path)
    expected_tag = str(policy["tag"])
    expected_commit = str(policy["commit"])
    commit = git_output(path, "rev-parse", "HEAD")
    tag = git_output(path, "describe", "--tags", "--exact-match")
    tagged_commit = git_output(path, "rev-parse", f"refs/tags/{expected_tag}^{{commit}}")
    dirty = git_output(path, "status", "--porcelain")
    if dirty:
        raise RuntimeError("godot-ai source checkout is dirty; vendor refresh requires an exact clean tag")

    plugin = path / "plugin" / "addons" / "godot_ai" / "plugin.cfg"
    license_path = path / "LICENSE"
    version = plugin_version(plugin)
    expected_version = expected_tag.removeprefix("v")
    license_digest = hashlib.sha256(canonical_bytes(license_path)).hexdigest()
    expected_license = str(policy["licenseSha256"])
    if tag != expected_tag or commit != expected_commit or tagged_commit != expected_commit:
        raise RuntimeError(
            f"godot-ai baseline mismatch: expected tag={expected_tag}, commit={expected_commit}; "
            f"found tag={tag}, commit={commit}"
        )
    if version != expected_version:
        raise RuntimeError(
            f"godot-ai manifest/plugin version mismatch: tag={expected_tag}, plugin={version}"
        )
    if license_digest != expected_license:
        raise RuntimeError(
            f"godot-ai license mismatch: expected sha256={expected_license}, found sha256={license_digest}"
        )
    return {"tag": tag, "commit": commit, "version": version, "licenseSha256": license_digest}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("path", type=Path)
    parser.add_argument(
        "--manifest",
        type=Path,
        default=Path.cwd() / "Tools/migration/manifest/godot-tooling.json",
    )
    args = parser.parse_args()
    print(validate_checkout(args.path.resolve(), args.manifest.resolve()))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
