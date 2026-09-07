#!/usr/bin/env python3
"""Prepare a reviewed godot-ai vendor refresh and all derived metadata."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import tempfile
from pathlib import Path

try:
    from Tools.migration.godot_ai_baseline import canonical_bytes, git_output, plugin_version
    from Tools.migration.godot_ai_vendor import resolve_vendor_path, tree_digest, validate
except ModuleNotFoundError:  # Direct script execution from the repository root.
    from godot_ai_baseline import canonical_bytes, git_output, plugin_version
    from godot_ai_vendor import resolve_vendor_path, tree_digest, validate

EXPECTED_ORIGINS = {
    "https://github.com/hi-godot/godot-ai.git",
    "git@github.com:hi-godot/godot-ai.git",
    "ssh://git@github.com/hi-godot/godot-ai.git",
}
NOTICE_PATTERN = re.compile(r"^(\| Godot AI EditorPlugin \| )[^|]+(\| MIT \|.*)$", re.MULTILINE)


def inspect_source(source: Path, requested_tag: str) -> dict[str, str]:
    if not requested_tag.startswith("v"):
        raise ValueError("godot-ai refresh tag must be explicit and v-prefixed, for example v3.2.5")
    origin = git_output(source, "remote", "get-url", "origin").replace("\\", "/")
    if origin not in EXPECTED_ORIGINS:
        raise ValueError(f"unexpected godot-ai origin: {origin}")
    if git_output(source, "status", "--porcelain"):
        raise ValueError("godot-ai source checkout is dirty")
    actual_tag = git_output(source, "describe", "--tags", "--exact-match")
    if actual_tag != requested_tag:
        raise ValueError(f"godot-ai source tag mismatch: requested={requested_tag}, found={actual_tag}")
    commit = git_output(source, "rev-parse", "HEAD")
    tagged_commit = git_output(source, "rev-parse", f"refs/tags/{requested_tag}^{{commit}}")
    if tagged_commit != commit:
        raise ValueError(
            f"godot-ai tag does not resolve to HEAD: tag={requested_tag}, tagCommit={tagged_commit}, head={commit}"
        )
    version = plugin_version(source / "plugin" / "addons" / "godot_ai" / "plugin.cfg")
    if version != requested_tag.removeprefix("v"):
        raise ValueError(f"godot-ai source plugin version mismatch: tag={requested_tag}, plugin={version}")
    license_hash = hashlib.sha256(canonical_bytes(source / "LICENSE")).hexdigest()
    return {"tag": actual_tag, "commit": commit, "version": version, "licenseSha256": license_hash}


def render_notice(text: str, version: str, commit: str) -> str:
    replacement = rf"\g<1>{version} (`{commit[:8]}`) \g<2>"
    rendered, count = NOTICE_PATTERN.subn(replacement, text)
    if count != 1:
        raise ValueError(f"expected exactly one Godot AI notice row, found {count}")
    return rendered


def refresh(root: Path, source: Path, requested_tag: str) -> dict[str, object]:
    facts = inspect_source(source, requested_tag)
    manifest_path = root / "Tools/migration/manifest/godot-tooling.json"
    notice_path = root / "THIRD_PARTY_NOTICES.md"
    inventory_script = root / "Tools/public-release/generate_dependency_report.py"
    previous_manifest_text = manifest_path.read_text(encoding="utf-8")
    previous_notice_text = notice_path.read_text(encoding="utf-8")
    inventory_path = root / "Tools/public-release/dependency-inventory.json"
    previous_inventory_text = inventory_path.read_text(encoding="utf-8") if inventory_path.is_file() else None
    manifest = json.loads(previous_manifest_text)
    policy = manifest["godotAi"]
    vendor = resolve_vendor_path(root, policy["vendorPath"])

    with tempfile.TemporaryDirectory(prefix="godot-ai-refresh-") as temporary:
        staged = Path(temporary) / "godot_ai"
        shutil.copytree(source / "plugin" / "addons" / "godot_ai", staged)
        shutil.copy2(source / "LICENSE", staged / "LICENSE")
        count, tree_hash = tree_digest(staged)

        policy.update({
            "tag": facts["tag"],
            "commit": facts["commit"],
            "vendorTreeSha256": tree_hash,
            "licenseSha256": facts["licenseSha256"],
        })
        for redundant in (
            "pluginCfgSha256", "vendorFileCount", "vendorPluginCfgSha256", "vendorLicenseSha256"
        ):
            policy.pop(redundant, None)
        manifest_text = json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"
        notice_text = render_notice(
            notice_path.read_text(encoding="utf-8"), facts["version"], facts["commit"]
        )

        backup = Path(temporary) / "previous-godot_ai"
        if vendor.exists():
            shutil.move(vendor, backup)
        try:
            shutil.copytree(staged, vendor)
            manifest_path.write_text(manifest_text, encoding="utf-8", newline="\n")
            notice_path.write_text(notice_text, encoding="utf-8", newline="\n")
            subprocess.run(
                ["python", str(inventory_script), "--root", str(root)], check=True
            )
            validate(root)
        except Exception:
            shutil.rmtree(vendor, ignore_errors=True)
            if backup.exists():
                shutil.move(backup, vendor)
            manifest_path.write_text(previous_manifest_text, encoding="utf-8", newline="\n")
            notice_path.write_text(previous_notice_text, encoding="utf-8", newline="\n")
            if previous_inventory_text is None:
                inventory_path.unlink(missing_ok=True)
            else:
                inventory_path.write_text(previous_inventory_text, encoding="utf-8", newline="\n")
            raise

    return {**facts, "files": count, "vendorTreeSha256": tree_hash}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--tag", required=True)
    args = parser.parse_args()
    try:
        result = refresh(args.root.resolve(), args.source.resolve(), args.tag)
    except (OSError, KeyError, RuntimeError, ValueError, subprocess.CalledProcessError, json.JSONDecodeError) as error:
        print(f"GODOT_AI_REFRESH_ERROR: {error}")
        return 1
    print(
        f"GODOT_AI_REFRESH_OK version={result['version']} commit={result['commit']} "
        f"files={result['files']} sha256={result['vendorTreeSha256']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
