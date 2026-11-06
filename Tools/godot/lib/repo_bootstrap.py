"""Standalone import path bootstrap for Tools/godot CLI scripts.

Must not import ``Tools.*`` — entry scripts load this file via ``importlib`` first.
"""

from __future__ import annotations

import sys
from pathlib import Path


def bootstrap_cli_entry(caller_file: str | Path) -> Path:
    """Prepend repository root to ``sys.path`` when missing (Windows CI / bare ``python script.py``)."""
    repo_root = Path(caller_file).resolve().parents[2]
    root_str = str(repo_root)
    if root_str not in sys.path:
        sys.path.insert(0, root_str)
    return repo_root
