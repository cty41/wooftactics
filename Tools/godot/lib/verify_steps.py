"""Verify step registry for cross-platform orchestration (Slice 2 — agent profile)."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Literal

StepKind = Literal["run", "windows_only_skip", "conditional_skip"]

PROFILE_AGENT = "agent"


@dataclass(frozen=True)
class VerifyStepDefinition:
    step_id: str
    title: str
    kind: StepKind
    profiles: frozenset[str]
    skip_reason: str | None = None


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


def steps_for_profile(profile: str) -> tuple[VerifyStepDefinition, ...]:
    if profile == PROFILE_AGENT:
        return agent_profile_steps()
    raise ValueError(f"Unknown verify profile: {profile}")
