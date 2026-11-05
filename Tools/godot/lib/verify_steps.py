"""Verify step registry for cross-platform orchestration (Slice 2 agent, Slice 5 windows)."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Literal

StepKind = Literal["run", "windows_only_skip", "conditional_skip", "windows_extended_ps1"]

PROFILE_AGENT = "agent"
PROFILE_WINDOWS = "windows"


@dataclass(frozen=True)
class VerifyStepDefinition:
    step_id: str
    title: str
    kind: StepKind
    profiles: frozenset[str]
    skip_reason: str | None = None


def _with_profiles(step: VerifyStepDefinition, profile: str) -> VerifyStepDefinition:
    return VerifyStepDefinition(
        step_id=step.step_id,
        title=step.title,
        kind=step.kind,
        profiles=frozenset({profile}),
        skip_reason=step.skip_reason,
    )


def agent_profile_steps() -> tuple[VerifyStepDefinition, ...]:
    agent = frozenset({PROFILE_AGENT})
    return (
        VerifyStepDefinition("preflight", "Preflight repository and tool probes", "run", agent),
        VerifyStepDefinition(
            "godot_ai_vendor",
            "Validate pinned Godot AI vendor tree",
            "run",
            agent,
        ),
        VerifyStepDefinition(
            "dotnet_restore_build",
            "Restore and build Godot-owned test projects",
            "run",
            agent,
        ),
        VerifyStepDefinition("dotnet_core_tests", "Run Tactics.Core NUnit", "run", agent),
        VerifyStepDefinition(
            "dotnet_application_tests",
            "Run Tactics.Application NUnit",
            "run",
            agent,
        ),
        VerifyStepDefinition(
            "dotnet_frozen_oracle_tests",
            "Run repository-owned frozen source Oracle NUnit",
            "run",
            agent,
        ),
        VerifyStepDefinition(
            "python_agent_policy",
            "Run agent policy unittest",
            "run",
            agent,
        ),
        VerifyStepDefinition(
            "python_godot_incidents",
            "Validate Godot Incidents",
            "run",
            agent,
        ),
        VerifyStepDefinition(
            "python_content_ownership",
            "Validate canonical Godot content ownership",
            "run",
            agent,
        ),
        VerifyStepDefinition(
            "python_manual_qa_handoff",
            "Validate manual QA handoff policy",
            "run",
            agent,
        ),
        VerifyStepDefinition(
            "validate_codex_skills",
            "Validate Codex/OpenCode project skills",
            "windows_only_skip",
            agent,
            skip_reason="validate-skills.ps1 requires Windows PowerShell policy host",
        ),
        VerifyStepDefinition(
            "gameplay_spec_compiler",
            "Run gameplay spec compiler tests",
            "run",
            agent,
        ),
        VerifyStepDefinition(
            "gameplay_spec_batch_compile",
            "Batch compile Godot gameplay specs",
            "run",
            agent,
        ),
        VerifyStepDefinition(
            "gdunit_isolated_subset",
            "Run isolated GdUnit4Net subset",
            "conditional_skip",
            agent,
            skip_reason="Godot executable not configured or missing",
        ),
        VerifyStepDefinition(
            "tooling_assembly_reload_smoke",
            "Test-TacticsToolingAssemblyReload.ps1",
            "windows_only_skip",
            agent,
            skip_reason="NamedPipe and taskkill reload smoke is Windows-only",
        ),
        VerifyStepDefinition(
            "cursor_mcp_guard",
            "Invoke-CursorMcpGuard.ps1",
            "windows_only_skip",
            agent,
            skip_reason="Cursor MCP guard hook is Windows desktop only",
        ),
        VerifyStepDefinition(
            "windows_rc_pipeline",
            "Build-GodotWindows.ps1 / Test-GodotWindowsPackage.ps1",
            "windows_only_skip",
            agent,
            skip_reason="Windows RC and package smoke stay in Windows workflow",
        ),
        VerifyStepDefinition(
            "migration_generation_chain",
            "Full migration ResourceSaver generation chain",
            "windows_only_skip",
            agent,
            skip_reason="Headless Editor generation chain is not in Linux agent subset",
        ),
        VerifyStepDefinition(
            "python_godot_tooling_tests",
            "Run Godot tooling Python unittest",
            "run",
            agent,
        ),
    )


def windows_profile_steps() -> tuple[VerifyStepDefinition, ...]:
    """Windows full verify: shared portable steps, gap steps, then PowerShell extended body."""
    windows = frozenset({PROFILE_WINDOWS})
    shared_run_ids = {
        "preflight",
        "godot_ai_vendor",
        "dotnet_restore_build",
        "dotnet_core_tests",
        "dotnet_application_tests",
        "dotnet_frozen_oracle_tests",
        "python_agent_policy",
        "python_godot_incidents",
        "python_content_ownership",
        "python_manual_qa_handoff",
        "gameplay_spec_compiler",
        "gameplay_spec_batch_compile",
    }
    steps: list[VerifyStepDefinition] = []
    for step in agent_profile_steps():
        if step.step_id not in shared_run_ids:
            continue
        steps.append(_with_profiles(step, PROFILE_WINDOWS))

    steps.extend(
        (
            VerifyStepDefinition(
                "sync_godot_ai_codex_check",
                "Validate project-scoped godot-ai Codex configuration",
                "conditional_skip",
                windows,
                skip_reason="local .codex/config.toml is not present",
            ),
            VerifyStepDefinition(
                "dotnet_restore_test_host",
                "Restore isolated GdUnit4Net test host dependencies (locked)",
                "run",
                windows,
            ),
            VerifyStepDefinition(
                "assert_godot_editor_dependency_graph",
                "Assert GodotSharpEditor lock graph after restore",
                "run",
                windows,
            ),
            VerifyStepDefinition(
                "prime_godot_uid_cache",
                "Prime fresh Godot resource UID cache",
                "run",
                windows,
            ),
            VerifyStepDefinition(
                "dotnet_authoring_mcp_build",
                "Build development-only Tactics Authoring MCP",
                "run",
                windows,
            ),
            VerifyStepDefinition(
                "dotnet_authoring_mcp_tests",
                "Run Tactics Authoring MCP protocol tests",
                "run",
                windows,
            ),
            VerifyStepDefinition(
                "validate_codex_skills",
                "Validate Codex/OpenCode project skills",
                "run",
                windows,
            ),
            VerifyStepDefinition(
                "gameplay_plan_hash_contract",
                "Validate tracked Godot gameplay plan hashes",
                "run",
                windows,
            ),
            VerifyStepDefinition(
                "windows_extended_verification",
                "Windows extended verification (GdUnit, migration, release, OKF)",
                "windows_extended_ps1",
                windows,
            ),
            VerifyStepDefinition(
                "python_godot_tooling_tests",
                "Run Godot tooling Python unittest",
                "run",
                windows,
            ),
        )
    )
    return tuple(steps)


def steps_for_profile(profile: str) -> tuple[VerifyStepDefinition, ...]:
    if profile == PROFILE_AGENT:
        return agent_profile_steps()
    if profile == PROFILE_WINDOWS:
        return windows_profile_steps()
    raise ValueError(f"Unknown verify profile: {profile}")
