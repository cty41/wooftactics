"""Unified Windows-only SKIP logging for cross-platform Verify orchestration."""

from __future__ import annotations

import sys

SKIP_WINDOWS_ONLY_PREFIX = "SKIP (Windows-only):"


def format_skip_windows_only(step_name: str, reason: str) -> str:
    return f"{SKIP_WINDOWS_ONLY_PREFIX} {step_name} — {reason}"


def log_skip_windows_only(step_name: str, reason: str, *, stream=None) -> None:
    line = format_skip_windows_only(step_name, reason)
    target = sys.stderr if stream is None else stream
    print(line, file=target)
