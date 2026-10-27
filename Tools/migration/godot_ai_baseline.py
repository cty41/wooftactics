"""Read-only validation for the pinned local godot-ai checkout."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path

try:  # imported as a package by tests
    from Tools.migration.godot_ai_vendor import canonical_bytes
except ImportError:  # executed directly by setup_godot_ai.ps1
    from godot_ai_vendor import canonical_bytes

DEFAULT_MANIFEST = Path(__file__).resolve().parent / "manifest" / "godot-tooling.json"


def load_expected(manifest_path: Path = DEFAULT_MANIFEST) -> dict[str, str]:
    """The manifest is the single source of truth for the pinned tag, commit and plugin.cfg digest."""
    policy = json.loads(manifest_path.read_text(encoding="utf-8"))["godotAi"]
    return {
        "tag": policy["tag"],
        "commit": policy["commit"],
        "pluginCfgSha256": policy["pluginCfgSha256"],
    }


def validate_checkout(path: Path, manifest_path: Path = DEFAULT_MANIFEST) -> dict[str, str]:
    expected = load_expected(manifest_path)
    commit = subprocess.check_output(["git", "-C", str(path), "rev-parse", "HEAD"], text=True).strip()
    tag = subprocess.check_output(["git", "-C", str(path), "describe", "--tags", "--exact-match"], text=True).strip()
    plugin = path / "plugin" / "addons" / "godot_ai" / "plugin.cfg"
    # Hash LF-normalised bytes so Windows autocrlf checkouts match the manifest.
    digest = hashlib.sha256(canonical_bytes(plugin)).hexdigest()
    actual = {"tag": tag, "commit": commit, "pluginCfgSha256": digest}
    if actual != expected:
        raise RuntimeError(f"godot-ai baseline mismatch: expected={expected}, actual={actual}")
    return actual


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("path", type=Path)
    print(validate_checkout(parser.parse_args().path))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
