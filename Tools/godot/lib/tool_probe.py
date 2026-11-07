"""Resolve dotnet, node, and Godot executables for Tools/godot orchestration."""

from __future__ import annotations

import os
import shutil
from collections.abc import Mapping


class DotnetNotFoundError(RuntimeError):
    """Raised when the .NET SDK is not available."""


class NodeNotFoundError(RuntimeError):
    """Raised when node is not on PATH."""


class NpmNotFoundError(RuntimeError):
    """Raised when npm is not on PATH and not overridden by NPM."""


class GodotNotFoundError(RuntimeError):
    """Raised when no Godot binary was configured or discovered."""


def _path_entries(env: Mapping[str, str]) -> str:
    dotnet_root = env.get("DOTNET_ROOT", "").strip()
    path = env.get("PATH", "")
    if dotnet_root:
        sep = os.pathsep
        return f"{dotnet_root}{sep}{path}" if path else dotnet_root
    return path


def resolve_dotnet_executable(*, env: Mapping[str, str] | None = None) -> str:
    environment = os.environ if env is None else env
    search_path = _path_entries(environment)
    found = shutil.which("dotnet", path=search_path or None)
    if found:
        return os.path.abspath(found)
    raise DotnetNotFoundError(
        "No .NET SDK found on PATH. Install the SDK (https://dot.net/download) "
        "or set DOTNET_ROOT and ensure dotnet is on PATH."
    )


def resolve_node_executable(*, env: Mapping[str, str] | None = None) -> str:
    environment = os.environ if env is None else env
    found = shutil.which("node", path=environment.get("PATH") or None)
    if found:
        return os.path.abspath(found)
    raise NodeNotFoundError(
        "No node executable found on PATH. Install Node.js or set PATH so node is available."
    )


def resolve_npm_executable(
    *,
    env: Mapping[str, str] | None = None,
    node_exe: str | None = None,
) -> str:
    """Return absolute path to npm for subprocess argv[0].

    On Windows, ``subprocess.run([\"npm\", ...])`` can raise WinError 2 because the bare
    name is not resolved to ``npm.cmd`` the way a shell does. Prefer a concrete path from
    ``which`` or next to ``node.exe``.
    """
    environment = os.environ if env is None else env
    path_env = environment.get("PATH") or None

    npm_override = (environment.get("NPM") or "").strip()
    if npm_override:
        if os.path.isfile(npm_override):
            return os.path.abspath(npm_override)
        found = shutil.which(npm_override, path=path_env)
        if found:
            return os.path.abspath(found)
        raise NpmNotFoundError(
            f"NPM is set but does not resolve to an npm executable: {npm_override}"
        )

    for name in ("npm", "npm.cmd"):
        found = shutil.which(name, path=path_env)
        if found:
            return os.path.abspath(found)

    if os.name == "nt" and node_exe:
        sibling = os.path.join(os.path.dirname(node_exe), "npm.cmd")
        if os.path.isfile(sibling):
            return os.path.abspath(sibling)

    raise NpmNotFoundError(
        "No npm executable found on PATH. Install Node.js (npm ships with it), set PATH, "
        "or set NPM to the full path to npm (e.g. npm.cmd on Windows)."
    )


def resolve_godot_executable(
    *,
    cli_value: str | None = None,
    env: Mapping[str, str] | None = None,
) -> str:
    environment = os.environ if env is None else env
    for candidate in (
        (cli_value or "").strip(),
        environment.get("GODOT_EXECUTABLE", "").strip(),
        environment.get("GODOT_BIN", "").strip(),
    ):
        if not candidate:
            continue
        if os.path.isfile(candidate):
            return os.path.abspath(candidate)
        found = shutil.which(candidate, path=environment.get("PATH") or None)
        if found:
            return os.path.abspath(found)
        raise GodotNotFoundError(
            f"Godot executable was configured but is not a file: {candidate}"
        )
    raise GodotNotFoundError(
        "Godot executable not configured. Pass --godot-executable or set GODOT_EXECUTABLE / GODOT_BIN."
    )


def try_resolve_godot_executable(
    *,
    cli_value: str | None = None,
    env: Mapping[str, str] | None = None,
) -> str | None:
    try:
        return resolve_godot_executable(cli_value=cli_value, env=env)
    except GodotNotFoundError:
        return None
