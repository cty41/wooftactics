"""Load the pinned MaLiang source and Pure Run adapter policy."""
from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[4]
CONFIG_PATH = ROOT / "Tools" / "artworks" / "maliang.adapter.json"


def load_config() -> dict[str, Any]:
    config = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    if config.get("schemaVersion") != 1:
        raise RuntimeError("unsupported MaLiang adapter schemaVersion")
    engine = config.get("engine")
    if not isinstance(engine, dict) or engine.get("submodulePath") != "Tools/vendor/maliang":
        raise RuntimeError("invalid MaLiang adapter engine configuration")
    return config


def activate() -> dict[str, Any]:
    config = load_config()
    source = ROOT / config["engine"]["submodulePath"] / "src"
    if not (source / "maliang_art" / "__init__.py").is_file():
        raise RuntimeError("pinned MaLiang submodule is missing; run git submodule update --init")
    source_text = str(source)
    if source_text not in sys.path:
        sys.path.insert(0, source_text)
    import maliang_art
    imported = Path(maliang_art.__file__).resolve()
    if not imported.is_relative_to(source.resolve()):
        raise RuntimeError(f"MaLiang resolved outside the pinned submodule: {imported}")
    if maliang_art.__version__ != config["engine"]["version"]:
        raise RuntimeError(
            f"MaLiang adapter requires {config['engine']['version']}, found {maliang_art.__version__}"
        )
    return config
