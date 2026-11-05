"""Resolve a Python interpreter for Tools/godot orchestration (Verify, Sync, future CLI)."""

from __future__ import annotations

import argparse
import os
import shutil
import sys
from collections.abc import Mapping, Sequence


class PythonNotFoundError(RuntimeError):
    """Raised when no interpreter can be resolved."""


def _is_executable_file(path: str) -> bool:
    if not os.path.isfile(path):
        return False
    if os.name == "nt":
        return True
    return os.access(path, os.X_OK)


def resolve_python_executable(
    *,
    env: Mapping[str, str] | None = None,
    path: str | None = None,
) -> str:
    """Return an absolute path to a Python executable.

    Resolution order: ``PYTHON`` env → ``python3`` on PATH → ``python`` on PATH.
    """
    environment: Mapping[str, str] = os.environ if env is None else env
    search_path = environment.get("PATH", "") if path is None else path

    python_from_env = environment.get("PYTHON", "").strip()
    if python_from_env:
        if _is_executable_file(python_from_env):
            return os.path.abspath(python_from_env)
        raise PythonNotFoundError(
            f"PYTHON is set but not an executable file: {python_from_env}"
        )

    for name in ("python3", "python"):
        found = shutil.which(name, path=search_path or None)
        if found:
            return os.path.abspath(found)

    raise PythonNotFoundError(
        "No Python interpreter found. Set PYTHON to an executable path or install python3."
    )


def python_argv(*args: str, env: Mapping[str, str] | None = None) -> list[str]:
    """Build argv with a resolved interpreter as the first element."""
    return [resolve_python_executable(env=env), *args]


def _parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Resolve Python for Tools/godot scripts.")
    parser.add_argument(
        "--print-executable",
        action="store_true",
        help="Print the resolved interpreter path to stdout and exit 0.",
    )
    return parser.parse_args(list(argv))


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(sys.argv[1:] if argv is None else argv)
    if args.print_executable:
        sys.stdout.write(resolve_python_executable())
        return 0
    print("Use --print-executable", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
