#!/usr/bin/env python3
"""Synchronize public provenance for media expanded from pinned vendored repositories."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path


VENDOR_PREFIX = "Tools/vendor/maliang/"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git(repo: Path, *arguments: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(repo), *arguments],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or "git command failed")
    return result.stdout.strip()


def expected_entries(root: Path) -> list[dict[str, str]]:
    adapter = json.loads((root / "Tools/artworks/maliang.adapter.json").read_text(encoding="utf-8"))
    engine = adapter["engine"]
    vendor = root / engine["submodulePath"]
    pinned_commit = engine["commit"]
    actual_commit = git(vendor, "rev-parse", "HEAD")
    if actual_commit != pinned_commit:
        raise RuntimeError(f"MaLiang checkout is not at pinned commit {pinned_commit}: {actual_commit}")
    paths = [line for line in git(vendor, "ls-files", "*.png").splitlines() if line]
    entries: list[dict[str, str]] = []
    for relative in sorted(paths):
        if relative.startswith("examples/poet-cast/"):
            license_id = "CC-BY-4.0"
            rights_holder = "cty41"
        elif relative.startswith("examples/synthetic/"):
            license_id = "MIT"
            rights_holder = "MaLiang contributors"
        else:
            raise RuntimeError(f"unclassified vendored MaLiang PNG: {relative}")
        public_path = f"{VENDOR_PREFIX}{relative}"
        entries.append({
            "path": public_path,
            "sha256": sha256_file(root / public_path),
            "status": "approved",
            "rightsHolder": rights_holder,
            "license": license_id,
            "provenance": f"vendored-maliang-example@{pinned_commit}",
        })
    return entries


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    manifest_path = root / "Tools/public-release/asset-provenance.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    expected = expected_entries(root)
    current = [entry for entry in manifest["entries"] if entry["path"].startswith(VENDOR_PREFIX)]
    if args.check:
        if current != expected:
            print("Vendored asset provenance is stale.")
            return 1
        print("Vendored asset provenance is current.")
        return 0
    manifest["entries"] = [
        entry for entry in manifest["entries"] if not entry["path"].startswith(VENDOR_PREFIX)
    ] + expected
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Synchronized {len(expected)} vendored asset provenance entries.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
