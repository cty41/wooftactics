"""Synchronize the machine-local Codex godot-ai entry into this worktree.

The Godot dock owns launcher discovery because its Windows attach entry contains
an exact consoleless ``pythonw.exe`` bootstrap.  This module imports that one
generated table, applies the project tool policy, and removes only the generated
table from the user config.  All other user configuration bytes are preserved.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tomllib
import uuid
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any


_PUBLIC_PYPI_INDEX = "https://pypi.org/simple"
# 与 godot-ai 4.x uv_resolution_policy.gd 的生产参数保持一致。
_UVX_REQUIRED_FLAGS = ("--isolated", "--no-config", "--no-env-file", "--no-sources", "--no-build")
_UVX_REQUIRED_OPTIONS = (
    ("--index-strategy", "first-index"),
    ("--keyring-provider", "disabled"),
    ("--index", _PUBLIC_PYPI_INDEX),
    ("--default-index", _PUBLIC_PYPI_INDEX),
    ("--find-links", "https://pypi.org/simple/godot-ai/"),
)
# 生成器与校验共用同一份定义，避免两边漂移。
_UVX_ISOLATION_ARGS = (
    *_UVX_REQUIRED_FLAGS,
    *(value for pair in _UVX_REQUIRED_OPTIONS for value in pair),
)
# Dock 生成的启动器会附带 --link-mode copy；可有可无，但只允许 copy。
_UVX_ALLOWED_LINK_MODE = "copy"
SERVER_NAME = "godot-ai"
SERVER_HEADER = '[mcp_servers."godot-ai"]'
_SERVER_HEADER_PATTERN = re.compile(
    r'^\s*\[\s*mcp_servers\s*\.\s*(?:"godot-ai"|godot-ai|godot_ai)\s*\]\s*(?:#.*)?$',
    re.IGNORECASE,
)
_ANY_TABLE_HEADER_PATTERN = re.compile(r"^\s*\[")
_ALLOWED_IMPORTED_FIELDS = {
    "args",
    "command",
    "enabled",
    "startup_timeout_sec",
    "tool_timeout_sec",
}
_EXPECTED_PROJECT_FIELDS = _ALLOWED_IMPORTED_FIELDS | {"enabled_tools"}


class CodexGodotAiConfigError(RuntimeError):
    """Raised when project-local MCP configuration cannot be changed safely."""


@dataclass(frozen=True)
class ServerBlock:
    start: int
    end: int
    text: str


@dataclass(frozen=True)
class CodexMcpPolicy:
    version: str
    config_relative_path: str
    http_port: int
    websocket_port: int
    startup_timeout_sec: int
    tool_timeout_sec: int
    default_profile: str
    profile_order: tuple[str, ...]
    profiles: Mapping[str, Mapping[str, Any]]
    forbidden_tools: frozenset[str]


@dataclass(frozen=True)
class SyncResult:
    profile: str
    project_changed: bool
    user_changed: bool
    enabled_tools: tuple[str, ...]


_WINDOWS_NO_CONSOLE_BOOTSTRAP = (
    "import subprocess,sys;"
    "sys.exit(subprocess.call(sys.argv[1:], creationflags=0x08000000))"
)


def load_policy(root: Path) -> CodexMcpPolicy:
    manifest_path = root / "Tools" / "migration" / "manifest" / "godot-tooling.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    godot_ai = manifest.get("godotAi")
    if not isinstance(godot_ai, dict):
        raise CodexGodotAiConfigError("godot-tooling manifest has no godotAi object")
    raw = godot_ai.get("codexMcp")
    if not isinstance(raw, dict):
        raise CodexGodotAiConfigError("godot-tooling manifest has no godotAi.codexMcp policy")

    tag = str(godot_ai.get("tag", ""))
    if not re.fullmatch(r"v\d+\.\d+\.\d+", tag):
        raise CodexGodotAiConfigError(f"invalid pinned godot-ai tag: {tag!r}")
    if raw.get("scope") != "project" or raw.get("transport") != "attach":
        raise CodexGodotAiConfigError("Codex MCP policy must use project-scoped attach transport")

    profiles = raw.get("profiles")
    profile_order = tuple(str(value) for value in raw.get("profileOrder", []))
    if not isinstance(profiles, dict) or not profiles:
        raise CodexGodotAiConfigError("Codex MCP policy has no profiles")
    if set(profile_order) != set(profiles):
        raise CodexGodotAiConfigError("Codex MCP profileOrder must contain every profile exactly once")

    policy = CodexMcpPolicy(
        version=tag.removeprefix("v"),
        config_relative_path=str(raw.get("configPath", "")),
        http_port=int(raw.get("httpPort", 0)),
        websocket_port=int(raw.get("websocketPort", 0)),
        startup_timeout_sec=int(raw.get("startupTimeoutSec", 0)),
        tool_timeout_sec=int(raw.get("toolTimeoutSec", 0)),
        default_profile=str(raw.get("defaultProfile", "")),
        profile_order=profile_order,
        profiles=profiles,
        forbidden_tools=frozenset(str(value) for value in raw.get("alwaysForbiddenTools", [])),
    )
    _validate_policy(policy)
    return policy


def resolve_profile_tools(policy: CodexMcpPolicy, profile: str) -> tuple[str, ...]:
    if profile not in policy.profiles:
        raise CodexGodotAiConfigError(
            f"unknown godot-ai tool profile {profile!r}; expected one of {policy.profile_order}"
        )

    resolved: set[str] = set()
    visiting: set[str] = set()

    def visit(name: str) -> None:
        if name in visiting:
            raise CodexGodotAiConfigError(f"cyclic godot-ai tool profile inheritance at {name!r}")
        if name not in policy.profiles:
            raise CodexGodotAiConfigError(f"unknown inherited godot-ai tool profile {name!r}")
        visiting.add(name)
        entry = policy.profiles[name]
        if not isinstance(entry, Mapping):
            raise CodexGodotAiConfigError(f"godot-ai tool profile {name!r} must be an object")
        parent = entry.get("extends")
        if parent is not None:
            visit(str(parent))
        tools = entry.get("tools")
        if not isinstance(tools, list) or not all(isinstance(tool, str) and tool for tool in tools):
            raise CodexGodotAiConfigError(f"godot-ai tool profile {name!r} has invalid tools")
        if len(tools) != len(set(tools)):
            raise CodexGodotAiConfigError(f"godot-ai tool profile {name!r} contains duplicate tools")
        resolved.update(tools)
        visiting.remove(name)

    visit(profile)
    forbidden = resolved.intersection(policy.forbidden_tools)
    if forbidden:
        raise CodexGodotAiConfigError(
            f"godot-ai tool profile {profile!r} enables forbidden tools: {sorted(forbidden)}"
        )
    return tuple(sorted(resolved))


def import_generated_user_entry(
    root: Path,
    user_config_path: Path,
    profile: str,
    *,
    failure_injector: Callable[[str], None] | None = None,
    check_command_exists: bool = True,
) -> SyncResult:
    root, policy, project_config_path = _resolve_context(root)
    enabled_tools = resolve_profile_tools(policy, profile)
    _assert_project_config_is_local(root, project_config_path)

    user_original = _read_optional_bytes(user_config_path)
    project_original = _read_optional_bytes(project_config_path)
    user_text = _decode_config(user_original, user_config_path)
    project_text = _decode_config(project_original, project_config_path)
    user_block = find_server_block(user_text)
    project_block = find_server_block(project_text)

    if user_block is None and project_block is None:
        raise CodexGodotAiConfigError(
            "no generated godot-ai entry was found; open the canonical Godot project and use Clients > Codex > Configure once"
        )

    if user_block is not None:
        user_server = parse_server_table(user_block.text)
        _validate_generated_server(user_server, policy, check_command_exists=check_command_exists)
        launch_source = user_server
        if project_block is not None:
            project_server = parse_server_table(project_block.text)
            _validate_project_server(
                project_server,
                policy,
                enabled_tools=None,
                check_command_exists=check_command_exists,
            )
            if _launch_identity(project_server) != _launch_identity(user_server):
                raise CodexGodotAiConfigError(
                    "user and project godot-ai entries have different launch commands; refusing dual-config drift"
                )
        rendered = render_project_server(launch_source, policy, enabled_tools)
        new_project_text = replace_or_append_server(project_text, project_block, rendered)
        new_user_text = remove_server_block(user_text, user_block)
    else:
        project_server = parse_server_table(project_block.text)
        _validate_project_server(
            project_server,
            policy,
            enabled_tools=None,
            check_command_exists=check_command_exists,
        )
        rendered = render_project_server(project_server, policy, enabled_tools)
        new_project_text = replace_or_append_server(project_text, project_block, rendered)
        new_user_text = user_text

    new_project = new_project_text.encode("utf-8")
    new_user = new_user_text.encode("utf-8") if user_original is not None else None
    return _apply_transaction(
        project_config_path,
        project_original,
        new_project,
        user_config_path,
        user_original,
        new_user,
        profile,
        enabled_tools,
        failure_injector,
    )


def apply_profile(
    root: Path,
    user_config_path: Path,
    profile: str,
    *,
    check_command_exists: bool = True,
) -> SyncResult:
    root, policy, project_config_path = _resolve_context(root)
    enabled_tools = resolve_profile_tools(policy, profile)
    _assert_project_config_is_local(root, project_config_path)
    user_text = _decode_config(_read_optional_bytes(user_config_path), user_config_path)
    if find_server_block(user_text) is not None:
        raise CodexGodotAiConfigError(
            "a user-level godot-ai entry still exists; import it before applying a project profile"
        )

    original = _read_optional_bytes(project_config_path)
    text = _decode_config(original, project_config_path)
    block = find_server_block(text)
    if block is None:
        raise CodexGodotAiConfigError("project godot-ai entry is missing; run -ImportFromUser first")
    server = parse_server_table(block.text)
    _validate_project_server(
        server,
        policy,
        enabled_tools=None,
        check_command_exists=check_command_exists,
    )
    rendered = render_project_server(server, policy, enabled_tools)
    new_payload = replace_or_append_server(text, block, rendered).encode("utf-8")
    changed = original != new_payload
    if changed:
        _atomic_write(project_config_path, new_payload)
    return SyncResult(profile, changed, False, enabled_tools)


def bootstrap_configuration(
    root: Path,
    user_config_path: Path,
    profile: str,
    *,
    python_executable: Path | None = None,
    uvx_executable: Path | None = None,
    check_command_exists: bool = True,
) -> SyncResult:
    """Create the project-scoped Attach entry without a one-off Dock export."""
    root, policy, project_config_path = _resolve_context(root)
    enabled_tools = resolve_profile_tools(policy, profile)
    _assert_project_config_is_local(root, project_config_path)
    user_text = _decode_config(_read_optional_bytes(user_config_path), user_config_path)
    if find_server_block(user_text) is not None:
        raise CodexGodotAiConfigError(
            "a user-level godot-ai entry exists; import it before bootstrapping project config"
        )

    python_source = (python_executable or Path(sys.executable)).resolve()
    pythonw = python_source if python_source.name.lower() == "pythonw.exe" else python_source.with_name("pythonw.exe")
    uvx_value = uvx_executable or (Path(found).resolve() if (found := shutil.which("uvx")) else None)
    if check_command_exists and not pythonw.is_file():
        raise CodexGodotAiConfigError(f"pythonw.exe was not found beside Python: {pythonw}")
    if uvx_value is None or (check_command_exists and not uvx_value.is_file()):
        raise CodexGodotAiConfigError("uvx executable was not found on PATH")

    launch_source = {
        "command": str(pythonw),
        "args": [
            "-c", _WINDOWS_NO_CONSOLE_BOOTSTRAP, str(uvx_value),
            *_UVX_ISOLATION_ARGS,
            "--from", f"godot-ai=={policy.version}", "godot-ai", "attach",
            "--port", str(policy.http_port), "--ws-port", str(policy.websocket_port),
        ],
    }
    _validate_launch(launch_source, policy, check_command_exists=check_command_exists)
    original = _read_optional_bytes(project_config_path)
    text = _decode_config(original, project_config_path)
    block = find_server_block(text)
    if block is not None:
        existing = parse_server_table(block.text)
        _validate_project_server(existing, policy, enabled_tools=None, check_command_exists=check_command_exists)
        launch_source = existing
    new_payload = replace_or_append_server(
        text, block, render_project_server(launch_source, policy, enabled_tools)
    ).encode("utf-8")
    changed = original != new_payload
    if changed:
        _atomic_write(project_config_path, new_payload)
    return SyncResult(profile, changed, False, enabled_tools)


def check_configuration(
    root: Path,
    user_config_path: Path,
    profile: str | None,
    *,
    check_command_exists: bool = True,
) -> SyncResult:
    root, policy, project_config_path = _resolve_context(root)
    _assert_project_config_is_local(root, project_config_path)
    user_text = _decode_config(_read_optional_bytes(user_config_path), user_config_path)
    if find_server_block(user_text) is not None:
        raise CodexGodotAiConfigError(
            "user-level godot-ai entry exists; project-only MCP policy is not satisfied"
        )
    project_text = _decode_config(_read_optional_bytes(project_config_path), project_config_path)
    block = find_server_block(project_text)
    if block is None:
        raise CodexGodotAiConfigError("project godot-ai entry is missing")
    server = parse_server_table(block.text)
    if profile is None:
        configured_tools = server.get("enabled_tools")
        matches = [
            candidate
            for candidate in policy.profile_order
            if list(resolve_profile_tools(policy, candidate)) == configured_tools
        ]
        if len(matches) != 1:
            raise CodexGodotAiConfigError(
                "project godot-ai enabled_tools does not match exactly one manifest profile"
            )
        profile = matches[0]
    enabled_tools = resolve_profile_tools(policy, profile)
    _validate_project_server(
        server,
        policy,
        enabled_tools=enabled_tools,
        check_command_exists=check_command_exists,
    )
    return SyncResult(profile, False, False, enabled_tools)


def find_server_block(text: str) -> ServerBlock | None:
    lines = text.splitlines(keepends=True)
    offsets: list[int] = []
    cursor = 0
    for line in lines:
        offsets.append(cursor)
        cursor += len(line)

    starts = [index for index, line in enumerate(lines) if _SERVER_HEADER_PATTERN.match(line.rstrip("\r\n"))]
    if len(starts) > 1:
        raise CodexGodotAiConfigError("multiple godot-ai MCP tables were found in one config")
    if not starts:
        return None

    start_line = starts[0]
    end_line = len(lines)
    for index in range(start_line + 1, len(lines)):
        if _ANY_TABLE_HEADER_PATTERN.match(lines[index]):
            end_line = index
            break
    start = offsets[start_line]
    end = offsets[end_line] if end_line < len(offsets) else len(text)
    return ServerBlock(start=start, end=end, text=text[start:end])


def parse_server_table(block: str) -> dict[str, Any]:
    try:
        document = tomllib.loads(block)
    except tomllib.TOMLDecodeError as error:
        raise CodexGodotAiConfigError(f"invalid godot-ai TOML table: {error}") from error
    servers = document.get("mcp_servers")
    if not isinstance(servers, dict):
        raise CodexGodotAiConfigError("godot-ai TOML table has no mcp_servers object")
    for key in (SERVER_NAME, "godot_ai"):
        server = servers.get(key)
        if isinstance(server, dict):
            return dict(server)
    raise CodexGodotAiConfigError("godot-ai TOML table could not be parsed")


def render_project_server(
    launch_source: Mapping[str, Any], policy: CodexMcpPolicy, enabled_tools: Sequence[str]
) -> str:
    command = str(launch_source["command"])
    args = [str(value) for value in launch_source["args"]]
    lines = [
        SERVER_HEADER,
        f"command = {_toml_string(command)}",
        "args = [",
        *(f"  {_toml_string(value)}," for value in args),
        "]",
        "enabled = true",
        f"startup_timeout_sec = {policy.startup_timeout_sec}",
        f"tool_timeout_sec = {policy.tool_timeout_sec}",
        "enabled_tools = [",
        *(f"  {_toml_string(tool)}," for tool in enabled_tools),
        "]",
        "",
    ]
    return "\n".join(lines)


def replace_or_append_server(text: str, block: ServerBlock | None, rendered: str) -> str:
    if block is not None:
        return text[: block.start] + rendered + text[block.end :]
    if not text:
        return rendered
    separator = "" if text.endswith(("\n", "\r")) else "\n"
    if not text.endswith(("\n\n", "\r\n\r\n")):
        separator += "\n"
    return text + separator + rendered


def remove_server_block(text: str, block: ServerBlock) -> str:
    return text[: block.start] + text[block.end :]


def _validate_policy(policy: CodexMcpPolicy) -> None:
    if policy.config_relative_path != ".codex/config.toml":
        raise CodexGodotAiConfigError("Codex MCP project config path must be .codex/config.toml")
    if policy.default_profile not in policy.profiles:
        raise CodexGodotAiConfigError("default godot-ai profile is not defined")
    if policy.http_port <= 0 or policy.websocket_port <= 0:
        raise CodexGodotAiConfigError("godot-ai MCP ports must be positive")
    if policy.startup_timeout_sec <= 0 or policy.tool_timeout_sec <= 0:
        raise CodexGodotAiConfigError("godot-ai MCP timeouts must be positive")
    for profile in policy.profile_order:
        resolve_profile_tools(policy, profile)


def _validate_generated_server(
    server: Mapping[str, Any],
    policy: CodexMcpPolicy,
    *,
    check_command_exists: bool,
) -> None:
    unknown = set(server).difference(_ALLOWED_IMPORTED_FIELDS)
    if unknown:
        raise CodexGodotAiConfigError(
            f"generated user godot-ai entry contains unsupported fields: {sorted(unknown)}"
        )
    _validate_launch(server, policy, check_command_exists=check_command_exists)
    _validate_common_fields(server, policy)


def _validate_project_server(
    server: Mapping[str, Any],
    policy: CodexMcpPolicy,
    *,
    enabled_tools: Sequence[str] | None,
    check_command_exists: bool,
) -> None:
    unknown = set(server).difference(_EXPECTED_PROJECT_FIELDS)
    if unknown:
        raise CodexGodotAiConfigError(
            f"project godot-ai entry contains unsupported fields: {sorted(unknown)}"
        )
    _validate_launch(server, policy, check_command_exists=check_command_exists)
    _validate_common_fields(server, policy)
    configured_tools = server.get("enabled_tools")
    if not isinstance(configured_tools, list) or not all(
        isinstance(tool, str) and tool for tool in configured_tools
    ):
        raise CodexGodotAiConfigError("project godot-ai entry has no valid enabled_tools list")
    if len(configured_tools) != len(set(configured_tools)):
        raise CodexGodotAiConfigError("project godot-ai enabled_tools contains duplicates")
    forbidden = set(configured_tools).intersection(policy.forbidden_tools)
    if forbidden:
        raise CodexGodotAiConfigError(
            f"project godot-ai entry enables forbidden tools: {sorted(forbidden)}"
        )
    if enabled_tools is not None and tuple(configured_tools) != tuple(enabled_tools):
        raise CodexGodotAiConfigError(
            "project godot-ai enabled_tools does not match the requested profile"
        )


def _validate_launch(
    server: Mapping[str, Any], policy: CodexMcpPolicy, *, check_command_exists: bool
) -> None:
    command = server.get("command")
    args = server.get("args")
    if not isinstance(command, str) or not command:
        raise CodexGodotAiConfigError("godot-ai command is missing")
    command_path = Path(command)
    if not command_path.is_absolute() or command_path.name.lower() != "pythonw.exe":
        raise CodexGodotAiConfigError("Windows godot-ai Attach command must be an absolute pythonw.exe")
    if check_command_exists and not command_path.is_file():
        raise CodexGodotAiConfigError(f"configured pythonw.exe does not exist: {command_path}")
    if not isinstance(args, list) or not all(isinstance(value, str) for value in args):
        raise CodexGodotAiConfigError("godot-ai args must be a string array")
    if "-c" not in args or not any("creationflags=0x08000000" in value for value in args):
        raise CodexGodotAiConfigError("godot-ai command is missing the Windows no-console bootstrap")
    # godot-ai 4.x 要求 uvx 隔离解析：忽略用户/项目 uv 配置并只使用公共 PyPI。
    for flag in _UVX_REQUIRED_FLAGS:
        if flag not in args:
            raise CodexGodotAiConfigError(f"godot-ai launcher is missing isolation flag {flag}")
    for name, expected_value in _UVX_REQUIRED_OPTIONS:
        _require_argument(args, name, expected_value)
    if "--link-mode" in args:
        _require_argument(args, "--link-mode", _UVX_ALLOWED_LINK_MODE)
    _require_argument(args, "--from", f"godot-ai=={policy.version}")
    if "godot-ai" not in args or "attach" not in args:
        raise CodexGodotAiConfigError("godot-ai command is not the pinned attach launcher")
    _require_argument(args, "--port", str(policy.http_port))
    _require_argument(args, "--ws-port", str(policy.websocket_port))


def _validate_common_fields(server: Mapping[str, Any], policy: CodexMcpPolicy) -> None:
    if server.get("enabled") is not True:
        raise CodexGodotAiConfigError("godot-ai MCP entry must be enabled")
    if server.get("startup_timeout_sec") != policy.startup_timeout_sec:
        raise CodexGodotAiConfigError("godot-ai startup timeout does not match the manifest")
    if server.get("tool_timeout_sec") != policy.tool_timeout_sec:
        raise CodexGodotAiConfigError("godot-ai tool timeout does not match the manifest")


def _require_argument(args: Sequence[str], name: str, expected_value: str) -> None:
    try:
        index = args.index(name)
    except ValueError as error:
        raise CodexGodotAiConfigError(f"godot-ai args are missing {name}") from error
    if index + 1 >= len(args) or args[index + 1] != expected_value:
        actual = args[index + 1] if index + 1 < len(args) else None
        raise CodexGodotAiConfigError(
            f"godot-ai {name} mismatch: expected {expected_value!r}, found {actual!r}"
        )


def _launch_identity(server: Mapping[str, Any]) -> tuple[str, tuple[str, ...]]:
    return str(server.get("command", "")), tuple(str(value) for value in server.get("args", []))


def _resolve_context(root: Path) -> tuple[Path, CodexMcpPolicy, Path]:
    root = root.resolve()
    if not (root / "godot" / "project.godot").is_file():
        raise CodexGodotAiConfigError(
            f"canonical Godot project is missing under migration root: {root}"
        )
    policy = load_policy(root)
    project_config = (root / policy.config_relative_path).resolve()
    try:
        project_config.relative_to(root)
    except ValueError as error:
        raise CodexGodotAiConfigError(
            f"project Codex config resolves outside the migration root: {project_config}"
        ) from error
    return root, policy, project_config


def _assert_project_config_is_local(root: Path, project_config: Path) -> None:
    tracked = subprocess.run(
        ["git", "-C", str(root), "ls-files", "--error-unmatch", "--", ".codex/config.toml"],
        capture_output=True,
        check=False,
    )
    if tracked.returncode == 0:
        raise CodexGodotAiConfigError(".codex/config.toml must remain untracked")
    ignored = subprocess.run(
        ["git", "-C", str(root), "check-ignore", "--no-index", "--quiet", "--", ".codex/config.toml"],
        capture_output=True,
        check=False,
    )
    if ignored.returncode != 0:
        raise CodexGodotAiConfigError(".codex/config.toml is not ignored by Git")
    if project_config != (root / ".codex" / "config.toml").resolve():
        raise CodexGodotAiConfigError("unexpected project Codex config path")


def _apply_transaction(
    project_path: Path,
    project_original: bytes | None,
    project_new: bytes,
    user_path: Path,
    user_original: bytes | None,
    user_new: bytes | None,
    profile: str,
    enabled_tools: Sequence[str],
    failure_injector: Callable[[str], None] | None,
) -> SyncResult:
    project_changed = project_original != project_new
    user_changed = user_original != user_new
    if not project_changed and not user_changed:
        return SyncResult(profile, False, False, tuple(enabled_tools))

    try:
        if project_changed:
            _atomic_write(project_path, project_new)
        if failure_injector:
            failure_injector("after_project_replace")
        if user_changed and user_new is not None:
            _atomic_write(user_path, user_new)
        if failure_injector:
            failure_injector("after_user_replace")
    except Exception:
        _restore_optional(user_path, user_original)
        _restore_optional(project_path, project_original)
        raise
    return SyncResult(profile, project_changed, user_changed, tuple(enabled_tools))


def _atomic_write(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f"{path.name}.{os.getpid()}.{uuid.uuid4().hex}.tmp")
    try:
        with temporary.open("xb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def _restore_optional(path: Path, payload: bytes | None) -> None:
    if payload is None:
        path.unlink(missing_ok=True)
    else:
        _atomic_write(path, payload)


def _read_optional_bytes(path: Path) -> bytes | None:
    return path.read_bytes() if path.is_file() else None


def _decode_config(payload: bytes | None, path: Path) -> str:
    if payload is None:
        return ""
    try:
        return payload.decode("utf-8")
    except UnicodeDecodeError as error:
        raise CodexGodotAiConfigError(f"Codex config is not UTF-8: {path}") from error


def _toml_string(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def _result_document(result: SyncResult, mode: str) -> str:
    return json.dumps(
        {
            "status": "ok",
            "mode": mode,
            "profile": result.profile,
            "projectChanged": result.project_changed,
            "userChanged": result.user_changed,
            "enabledToolCount": len(result.enabled_tools),
        },
        sort_keys=True,
    )


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--user-config", type=Path, required=True)
    parser.add_argument("--profile")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--import-from-user", action="store_true")
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--bootstrap", action="store_true")
    arguments = parser.parse_args(argv)

    try:
        policy = load_policy(arguments.root.resolve())
        if arguments.bootstrap:
            profile = arguments.profile or policy.default_profile
            result = bootstrap_configuration(arguments.root, arguments.user_config, profile)
            selected_mode = "bootstrap"
        elif arguments.import_from_user:
            profile = arguments.profile or policy.default_profile
            result = import_generated_user_entry(arguments.root, arguments.user_config, profile)
            selected_mode = "import"
        elif arguments.check:
            result = check_configuration(arguments.root, arguments.user_config, arguments.profile)
            selected_mode = "check"
        else:
            profile = arguments.profile or policy.default_profile
            result = apply_profile(arguments.root, arguments.user_config, profile)
            selected_mode = "profile"
        print(_result_document(result, selected_mode))
        return 0
    except (CodexGodotAiConfigError, OSError, ValueError) as error:
        print(f"GODOT_AI_CODEX_CONFIG_ERROR: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
