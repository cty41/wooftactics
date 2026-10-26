"""Read-only validation for the pinned local godot-ai checkout."""

from __future__ import annotations

import argparse
import hashlib
import subprocess
from pathlib import Path


EXPECTED_TAG = "v4.3.1"
EXPECTED_COMMIT = "35da2c2fe1e2c20e11b5be01841e966951d6314b"
EXPECTED_PLUGIN_SHA256 = "6290daf20479a1ad215abce7b489f78bd6b43154909fd7317ee3f4551c299514"


def validate_checkout(path: Path) -> dict[str, str]:
    commit = subprocess.check_output(["git", "-C", str(path), "rev-parse", "HEAD"], text=True).strip()
    tag = subprocess.check_output(["git", "-C", str(path), "describe", "--tags", "--exact-match"], text=True).strip()
    plugin = path / "plugin" / "addons" / "godot_ai" / "plugin.cfg"
    digest = hashlib.sha256(plugin.read_bytes()).hexdigest()
    if tag != EXPECTED_TAG or commit != EXPECTED_COMMIT or digest != EXPECTED_PLUGIN_SHA256:
        raise RuntimeError(f"godot-ai baseline mismatch: tag={tag}, commit={commit}, plugin={digest}")
    return {"tag": tag, "commit": commit, "pluginCfgSha256": digest}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("path", type=Path)
    print(validate_checkout(parser.parse_args().path))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
