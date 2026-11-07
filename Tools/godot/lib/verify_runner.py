"""Execute Verify step definitions for cross-platform orchestration."""

from __future__ import annotations

import hashlib
import os
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

from Tools.godot.lib import resolve_python
from Tools.godot.lib.godot_editor_probe import list_godot_editor_processes
from Tools.godot.lib.skip_log import log_skip_windows_only
from Tools.godot.lib.tool_probe import (
    DotnetNotFoundError,
    NodeNotFoundError,
    NpmNotFoundError,
    resolve_dotnet_executable,
    resolve_node_executable,
    resolve_npm_executable,
    try_resolve_godot_executable,
)
from Tools.godot.lib.verify_steps import PROFILE_WINDOWS, VerifyStepDefinition
from Tools.godot.lib.worktree_lock import WorktreeLock, acquire_worktree_lock

WINDOWS_EXTENDED_SCRIPT = Path("Tools/godot/Verify-GodotProject.WindowsExtended.ps1")
CODEX_SKILLS = (
    "godot-workflow",
    "godot-csharp-development",
    "godot-content-migration",
    "godot-editor-tooling",
    "godot-editor-lifecycle",
    "godot-testing-diagnostics",
    "godot-ai-workflow",
    "manual-qa-handoff",
)


@dataclass
class VerifyContext:
    repo_root: Path
    profile: str
    env: dict[str, str]
    python_exe: str
    dotnet_exe: str
    node_exe: str
    npm_exe: str
    godot_executable: str | None
    run_on_windows: bool
    require_artwork: bool = False
    maliang_available: bool = False


class VerifyStepFailed(RuntimeError):
    def __init__(self, title: str, exit_code: int) -> None:
        super().__init__(f"{title} failed with exit code {exit_code}")
        self.title = title
        self.exit_code = exit_code


def _run_checked(title: str, argv: list[str], *, cwd: Path, env: dict[str, str]) -> None:
    print(f"== {title} ==", flush=True)
    completed = subprocess.run(argv, cwd=cwd, env=env, check=False)
    if completed.returncode != 0:
        raise VerifyStepFailed(title, completed.returncode)


def _resolve_pwsh_executable(env: dict[str, str]) -> str:
    for key in ("PWSH", "POWERSHELL"):
        value = env.get(key, "").strip()
        if value:
            return value
    for candidate in ("pwsh", "powershell"):
        resolved = shutil.which(candidate, path=env.get("PATH"))
        if resolved:
            return resolved
    raise RuntimeError("PowerShell (pwsh or powershell) is required for Windows extended verification.")


def _assert_maliang_policy(ctx: VerifyContext) -> None:
    marker = ctx.repo_root / "Tools/vendor/maliang/src/maliang_art/__init__.py"
    ctx.maliang_available = marker.is_file()
    if ctx.maliang_available:
        return
    if ctx.require_artwork:
        raise RuntimeError(
            "MaLiang submodule is not initialized but --require-artwork was set. "
            "Run: git submodule update --init Tools/vendor/maliang"
        )
    print(
        "SKIP: MaLiang submodule not initialized; artwork pipeline unittest and "
        "artwork state registry check will be skipped in Windows extended verification.",
        flush=True,
    )


def _sync_godot_ai_codex_check(ctx: VerifyContext) -> None:
    config = ctx.repo_root / ".codex/config.toml"
    if not config.is_file():
        print(
            "SKIP: Validate project-scoped godot-ai Codex configuration — "
            "local .codex/config.toml is not present",
            flush=True,
        )
        return
    pwsh = _resolve_pwsh_executable(ctx.env)
    _run_checked(
        "Validate project-scoped godot-ai Codex configuration",
        [
            pwsh,
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            "Tools/godot/Sync-GodotAiCodexConfig.ps1",
            "-Check",
        ],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _dotnet_restore_test_host(ctx: VerifyContext) -> None:
    project_root = ctx.repo_root / "godot"
    project_root_sep = str(project_root.resolve()) + os.sep
    test_host = project_root / "tests/Tactics.Godot.TestHost.csproj"
    _run_checked(
        "Restore isolated GdUnit4Net test host dependencies (locked)",
        [
            ctx.dotnet_exe,
            "restore",
            str(test_host.relative_to(ctx.repo_root)).replace("\\", "/"),
            "--locked-mode",
            f"-p:GodotProjectDir={project_root_sep}",
        ],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _assert_godot_editor_dependency_graph(ctx: VerifyContext) -> None:
    assets_path = ctx.repo_root / "godot/.godot/mono/temp/obj/project.assets.json"
    if not assets_path.is_file():
        raise RuntimeError(
            f"Godot Editor dependency graph is missing after locked restore: {assets_path}"
        )
    assets_text = assets_path.read_text(encoding="utf-8")
    if "GodotSharpEditor/4.7.1" not in assets_text:
        raise RuntimeError(
            "Godot Editor dependency graph is missing GodotSharpEditor/4.7.1. "
            "Restore the Debug/Editor lock graph before building."
        )
    print("== Assert GodotSharpEditor lock graph after restore ==", flush=True)


def _prime_godot_uid_cache(ctx: VerifyContext) -> None:
    if not ctx.godot_executable:
        raise RuntimeError("Godot executable is required to prime the UID cache.")
    project_root = ctx.repo_root / "godot"
    _run_checked(
        "Prime fresh Godot resource UID cache",
        [
            ctx.godot_executable,
            "--headless",
            "--editor",
            "--path",
            str(project_root),
            "--import",
        ],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _dotnet_authoring_mcp_build(ctx: VerifyContext) -> None:
    _run_checked(
        "Build development-only Tactics Authoring MCP",
        [
            ctx.dotnet_exe,
            "build",
            "Tools/tactics-authoring-mcp/Tactics.Authoring.Mcp.csproj",
            "-c",
            "Release",
            "-m:1",
        ],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _dotnet_authoring_mcp_tests(ctx: VerifyContext) -> None:
    _python_module(ctx, "Run Tactics Authoring MCP protocol tests", "Tools.tactics-authoring-mcp.tests.test_protocol")


def _validate_codex_skills(ctx: VerifyContext) -> None:
    pwsh = _resolve_pwsh_executable(ctx.env)
    for skill in CODEX_SKILLS:
        _run_checked(
            f"Validate Codex/OpenCode skill: {skill}",
            [
                pwsh,
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                ".agents/scripts/validate-skills.ps1",
                "-SkillsRoot",
                f".agents/skills/{skill}",
            ],
            cwd=ctx.repo_root,
            env=ctx.env,
        )


def _gameplay_plan_hash_contract(ctx: VerifyContext) -> None:
    source = ctx.repo_root / "Tests/gameplay-specs/godot"
    output = ctx.repo_root / "artifacts/gameplay-specs/godot"
    print("== Validate tracked Godot gameplay plan hashes ==", flush=True)
    for spec in sorted(source.glob("*.gameplay-test.md")):
        name = spec.name[: -len(".gameplay-test.md")]
        generated = output / f"{name}.plan.json"
        tracked = source / f"{name}.plan.json"
        if not generated.is_file() or not tracked.is_file():
            raise RuntimeError(f"Godot gameplay plan is stale or missing: {name}")
        generated_hash = hashlib.sha256(generated.read_bytes()).hexdigest()
        tracked_hash = hashlib.sha256(tracked.read_bytes()).hexdigest()
        if generated_hash != tracked_hash:
            raise RuntimeError(f"Godot gameplay plan is stale or missing: {name}")


def _invoke_windows_extended_verification(ctx: VerifyContext) -> None:
    if not ctx.run_on_windows:
        raise RuntimeError("Windows extended verification requires Windows.")
    if not ctx.godot_executable:
        raise RuntimeError("Godot executable is required for Windows extended verification.")
    pwsh = _resolve_pwsh_executable(ctx.env)
    argv = [
        pwsh,
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(WINDOWS_EXTENDED_SCRIPT),
        "-GodotExecutable",
        ctx.godot_executable,
        "-PortableStepsRanExternally",
        "-SkipWorktreeMutex",
    ]
    if ctx.require_artwork:
        argv.append("-RequireArtwork")
    _run_checked("Windows extended verification (GdUnit, migration, release, OKF)", argv, cwd=ctx.repo_root, env=ctx.env)


def _preflight(ctx: VerifyContext) -> None:
    print("== Preflight repository and tool probes ==", flush=True)
    for retired in ("Assets", "Packages", "ProjectSettings", "UIElementsSchema"):
        if (ctx.repo_root / retired).exists():
            raise RuntimeError(
                f"Godot mainline verification requires retired Unity root to be absent: {retired}"
            )
    project_file = ctx.repo_root / "godot" / "project.godot"
    if not project_file.is_file():
        raise RuntimeError(f"Canonical Godot project missing: {project_file}")
    try:
        ctx.python_exe = resolve_python.resolve_python_executable(env=ctx.env)
        ctx.dotnet_exe = resolve_dotnet_executable(env=ctx.env)
        ctx.node_exe = resolve_node_executable(env=ctx.env)
        ctx.npm_exe = resolve_npm_executable(env=ctx.env, node_exe=ctx.node_exe)
    except (
        resolve_python.PythonNotFoundError,
        DotnetNotFoundError,
        NodeNotFoundError,
        NpmNotFoundError,
    ) as exc:
        raise RuntimeError(str(exc)) from exc
    print(f"Python: {ctx.python_exe}", flush=True)
    print(f"dotnet: {ctx.dotnet_exe}", flush=True)
    print(f"node: {ctx.node_exe}", flush=True)
    print(f"npm: {ctx.npm_exe}", flush=True)
    if ctx.godot_executable:
        print(f"Godot: {ctx.godot_executable}", flush=True)
    else:
        print("Godot: (not configured — GdUnit subset will be skipped)", flush=True)


def _godot_ai_vendor(ctx: VerifyContext) -> None:
    _run_checked(
        "Validate pinned Godot AI vendor tree",
        [ctx.python_exe, "Tools/migration/godot_ai_vendor.py", "--root", str(ctx.repo_root)],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _dotnet_restore_build(ctx: VerifyContext) -> None:
    runsettings = ctx.repo_root / "Tactics.Godot.runsettings"
    adapter_rel = "godot/Tactics.Godot.Adapter.csproj"
    projects = [
        "src/Tactics.Core.Tests/Tactics.Core.Tests.csproj",
        "src/Tactics.Application.Tests/Tactics.Application.Tests.csproj",
        "src/Tactics.FrozenOracle.Tests/Tactics.FrozenOracle.Tests.csproj",
        adapter_rel,
    ]
    print("== Restore locked Godot-owned dependencies ==", flush=True)
    for project in projects:
        _run_checked(
            f"dotnet restore {project}",
            [ctx.dotnet_exe, "restore", project, "--locked-mode"],
            cwd=ctx.repo_root,
            env=ctx.env,
        )
    print("== Build Godot-owned projects without Unity Oracle sources ==", flush=True)
    for project in projects:
        _run_checked(
            f"dotnet build {project}",
            [ctx.dotnet_exe, "build", project, "-c", "Debug", "--no-restore", "-m:1"],
            cwd=ctx.repo_root,
            env=ctx.env,
        )


def _dotnet_test(ctx: VerifyContext, title: str, project: str) -> None:
    runsettings = ctx.repo_root / "Tactics.Godot.runsettings"
    _run_checked(
        title,
        [
            ctx.dotnet_exe,
            "test",
            project,
            "-c",
            "Debug",
            "--no-restore",
            "--no-build",
            "--settings",
            str(runsettings),
            "--logger",
            "console;verbosity=minimal",
        ],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _python_discover(ctx: VerifyContext, title: str, directory: str) -> None:
    _run_checked(
        title,
        [ctx.python_exe, "-m", "unittest", "discover", "-s", directory, "-p", "test_*.py"],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _python_module(ctx: VerifyContext, title: str, module: str) -> None:
    _run_checked(
        title,
        [ctx.python_exe, "-m", "unittest", module],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _python_script(ctx: VerifyContext, title: str, script: str, *args: str) -> None:
    _run_checked(
        title,
        [ctx.python_exe, script, *args],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _ensure_gameplay_spec_dist(ctx: VerifyContext) -> None:
    cli = ctx.repo_root / "Tools/gameplay-test-spec/dist/src/cli.js"
    if cli.is_file():
        return
    prefix = ctx.repo_root / "Tools/gameplay-test-spec"
    if not (prefix / "node_modules").is_dir():
        print("== Restore gameplay spec compiler dependencies ==", flush=True)
        _run_checked(
            "npm ci (gameplay-test-spec)",
            [ctx.npm_exe, "--prefix", str(prefix), "ci", "--ignore-scripts"],
            cwd=ctx.repo_root,
            env=ctx.env,
        )
    print("== Build gameplay-test-spec dist ==", flush=True)
    _run_checked(
        "npm run build (gameplay-test-spec)",
        [ctx.npm_exe, "--prefix", str(prefix), "run", "build"],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _gameplay_spec_compiler(ctx: VerifyContext) -> None:
    prefix = ctx.repo_root / "Tools/gameplay-test-spec"
    if not (prefix / "node_modules").is_dir():
        print("== Restore gameplay spec compiler dependencies ==", flush=True)
        _run_checked(
            "npm ci (gameplay-test-spec)",
            [ctx.npm_exe, "--prefix", str(prefix), "ci", "--ignore-scripts"],
            cwd=ctx.repo_root,
            env=ctx.env,
        )
    env = dict(ctx.env)
    env["GODOT_OWNED_VERIFY"] = "1"
    _run_checked(
        "Run gameplay spec compiler tests",
        [ctx.npm_exe, "--prefix", str(prefix), "test"],
        cwd=ctx.repo_root,
        env=env,
    )


def _gameplay_spec_batch_compile(ctx: VerifyContext) -> None:
    _ensure_gameplay_spec_dist(ctx)
    source = ctx.repo_root / "Tests/gameplay-specs/godot"
    output = ctx.repo_root / "artifacts/gameplay-specs/godot"
    output.mkdir(parents=True, exist_ok=True)
    _run_checked(
        "Batch compile Godot gameplay specs",
        [
            ctx.node_exe,
            "Tools/gameplay-test-spec/dist/src/cli.js",
            "batch-compile",
            "-d",
            str(source),
            "-o",
            str(output),
            "--runtime",
            "godot",
        ],
        cwd=ctx.repo_root,
        env=ctx.env,
    )


def _discover_one_gdunit_suite(project_root: Path) -> str | None:
    tests_root = project_root / "tests"
    for path in sorted(tests_root.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        if "[TestSuite]" not in text:
            continue
        namespace_match = re.search(r"(?m)^namespace\s+([^;{]+)[;{]", text)
        class_match = re.search(
            r"(?ms)\[TestSuite\]\s*(?:\[[^\]]+\]\s*)*(?:public|internal)\s+(?:sealed\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)",
            text,
        )
        if not namespace_match or not class_match:
            continue
        suite = f"{namespace_match.group(1)}.{class_match.group(1)}"
        if suite in {
            "Tactics.Godot.Tests.GameplaySpec.GodotGameplayRuntimeRunnerTests",
            "Tactics.Godot.Tests.GameplaySpec.GodotDemonboundLongRunTests",
        }:
            continue
        return suite
    return None


def _gdunit_isolated_subset(ctx: VerifyContext) -> None:
    if not ctx.godot_executable:
        print(
            "SKIP: Run isolated GdUnit4Net subset — Godot executable not configured "
            "(set GODOT_EXECUTABLE or pass --godot-executable)",
            flush=True,
        )
        return
    project_root = ctx.repo_root / "godot"
    project_root_sep = str(project_root.resolve()) + os.sep
    test_host = project_root / "tests/Tactics.Godot.TestHost.csproj"
    runner_template = project_root / "tests/GdUnit4TestRunnerScene.cs.txt"
    runner_source = project_root / "gdunit4_testadapter_v5/GdUnit4TestRunnerScene.cs"
    created_runner = False
    if not runner_source.is_file():
        runner_source.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(runner_template, runner_source)
        created_runner = True
    try:
        env = dict(ctx.env)
        env["GODOT_BIN"] = ctx.godot_executable
        _run_checked(
            "Restore isolated GdUnit4Net test host dependencies",
            [
                ctx.dotnet_exe,
                "restore",
                str(test_host.relative_to(ctx.repo_root)).replace("\\", "/"),
                "--locked-mode",
                f"-p:GodotProjectDir={project_root_sep}",
            ],
            cwd=ctx.repo_root,
            env=env,
        )
        _run_checked(
            "Build isolated GdUnit4Net test host non-incrementally",
            [
                ctx.dotnet_exe,
                "build",
                str(test_host.relative_to(ctx.repo_root)).replace("\\", "/"),
                "-c",
                "Debug",
                "--no-restore",
                "--no-incremental",
                "-m:1",
                f"-p:GodotProjectDir={project_root_sep}",
            ],
            cwd=ctx.repo_root,
            env=env,
        )
        suite = _discover_one_gdunit_suite(project_root)
        if not suite:
            raise RuntimeError("No GdUnit4Net suite discovered for agent subset.")
        runsettings = ctx.repo_root / "Tactics.Godot.runsettings"
        _run_checked(
            f"Run isolated GdUnit4Net suite {suite}",
            [
                ctx.dotnet_exe,
                "test",
                str(test_host.relative_to(ctx.repo_root)).replace("\\", "/"),
                "-c",
                "Debug",
                "--no-restore",
                "--no-build",
                "--settings",
                str(runsettings),
                "--filter",
                f"FullyQualifiedName~{suite}",
                "--logger",
                "console;verbosity=minimal",
            ],
            cwd=ctx.repo_root,
            env=env,
        )
    finally:
        if created_runner and runner_source.is_file():
            runner_source.unlink()


def _execute_run_step(ctx: VerifyContext, step: VerifyStepDefinition) -> None:
    handlers = {
        "preflight": _preflight,
        "godot_ai_vendor": _godot_ai_vendor,
        "dotnet_restore_build": _dotnet_restore_build,
        "dotnet_core_tests": lambda c: _dotnet_test(
            c, "Run Tactics.Core NUnit", "src/Tactics.Core.Tests/Tactics.Core.Tests.csproj"
        ),
        "dotnet_application_tests": lambda c: _dotnet_test(
            c,
            "Run Tactics.Application NUnit",
            "src/Tactics.Application.Tests/Tactics.Application.Tests.csproj",
        ),
        "dotnet_frozen_oracle_tests": lambda c: _dotnet_test(
            c,
            "Run repository-owned frozen source Oracle NUnit",
            "src/Tactics.FrozenOracle.Tests/Tactics.FrozenOracle.Tests.csproj",
        ),
        "python_agent_policy": lambda c: _python_discover(c, "Run agent policy unittest", "Tools/agent-policy"),
        "python_godot_incidents": lambda c: _python_script(
            c, "Validate Godot Incidents", "Tools/agent-policy/validate_godot_incidents.py"
        ),
        "python_content_ownership": lambda c: _python_module(
            c, "Validate canonical Godot content ownership", "Tools.migration.tests.test_godot_content_ownership"
        ),
        "python_manual_qa_handoff": lambda c: _python_script(
            c, "Validate manual QA handoff policy", "Tools/agent-policy/validate_manual_qa_handoff.py"
        ),
        "gameplay_spec_compiler": _gameplay_spec_compiler,
        "gameplay_spec_batch_compile": _gameplay_spec_batch_compile,
        "gdunit_isolated_subset": _gdunit_isolated_subset,
        "sync_godot_ai_codex_check": _sync_godot_ai_codex_check,
        "dotnet_restore_test_host": _dotnet_restore_test_host,
        "assert_godot_editor_dependency_graph": _assert_godot_editor_dependency_graph,
        "prime_godot_uid_cache": _prime_godot_uid_cache,
        "dotnet_authoring_mcp_build": _dotnet_authoring_mcp_build,
        "dotnet_authoring_mcp_tests": _dotnet_authoring_mcp_tests,
        "validate_codex_skills": _validate_codex_skills,
        "gameplay_plan_hash_contract": _gameplay_plan_hash_contract,
        "python_godot_tooling_tests": lambda c: _python_discover(
            c, "Run Godot tooling Python unittest", "Tools/godot/tests"
        ),
    }
    handler = handlers.get(step.step_id)
    if handler is None:
        raise RuntimeError(f"No handler registered for step {step.step_id}")
    handler(ctx)


def run_verify_profile(
    *,
    repo_root: Path,
    profile: str,
    steps: tuple[VerifyStepDefinition, ...],
    godot_executable: str | None,
    require_artwork: bool = False,
) -> int:
    env = os.environ.copy()
    if godot_executable:
        env["GODOT_BIN"] = godot_executable
        env["GODOT_EXECUTABLE"] = godot_executable
    run_on_windows = os.name == "nt"
    if profile == PROFILE_WINDOWS and not run_on_windows:
        print(
            "Windows profile requires Windows; use --profile agent on Linux/cloud.",
            file=sys.stderr,
        )
        return 1
    if profile == PROFILE_WINDOWS and not godot_executable:
        print(
            "Windows profile requires --godot-executable (or GODOT_EXECUTABLE).",
            file=sys.stderr,
        )
        return 1
    ctx = VerifyContext(
        repo_root=repo_root.resolve(),
        profile=profile,
        env=env,
        python_exe="",
        dotnet_exe="",
        node_exe="",
        npm_exe="",
        godot_executable=godot_executable,
        run_on_windows=run_on_windows,
        require_artwork=require_artwork,
    )
    editors = list_godot_editor_processes(str(ctx.repo_root / "godot"))
    if editors:
        pids = ", ".join(str(item.process_id) for item in editors)
        print(
            f"Close the Godot Editor for this worktree before verification. PIDs: {pids}",
            file=sys.stderr,
        )
        return 1
    lock: WorktreeLock | None = None
    try:
        lock = acquire_worktree_lock(str(ctx.repo_root))
        if profile == PROFILE_WINDOWS:
            _assert_maliang_policy(ctx)
        for step in steps:
            if step.kind == "windows_only_skip":
                if profile == PROFILE_WINDOWS:
                    log_skip_windows_only(step.title, step.skip_reason or "Windows-only step")
                    continue
                if run_on_windows:
                    print(
                        f"== {step.title} ==",
                        flush=True,
                    )
                    print(
                        f"Step remains outside agent subset: {step.title}",
                        flush=True,
                    )
                    continue
                log_skip_windows_only(step.title, step.skip_reason or "Windows-only step")
                continue
            if step.kind == "windows_extended_ps1":
                _invoke_windows_extended_verification(ctx)
                continue
            if step.kind == "conditional_skip":
                if step.step_id == "gdunit_isolated_subset":
                    _execute_run_step(ctx, step)
                    continue
                if step.step_id == "sync_godot_ai_codex_check":
                    _execute_run_step(ctx, step)
                    continue
            if step.kind == "run":
                _execute_run_step(ctx, step)
                continue
        print(f"Godot project verification ({profile} profile) passed. Project: {ctx.repo_root / 'godot'}")
        return 0
    except VerifyStepFailed as exc:
        print(str(exc), file=sys.stderr)
        return exc.exit_code if exc.exit_code else 1
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        return 1
    finally:
        if lock is not None:
            lock.release()
