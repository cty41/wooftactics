---
name: godot-ai-workflow
description: Use when configuring or operating the canonical Godot project through the pinned project-scoped godot-ai v4.3.1 Attach server for scene, resource, run, log, screenshot, or lightweight editor smoke tasks.
---

# Godot AI Workflow

## Quick Reference

- Baseline: tracked `godot/addons/godot_ai`, vendored from tag `v4.3.1` and pinned by `Tools/migration/manifest/godot-tooling.json`.
- Project: `godot/project.godot` only.
- Config: local ignored `.codex/config.toml`; never leave godot-ai in the user-level Codex config.
- Profiles: `phase3-observe`, `content-authoring`, `ui-input`, `presentation`; each is cumulative.
- Launch: `Tools/godot/Open-GodotDev.ps1` is the supported Editor entry; it builds, isolates and records the session.
- Sync: `Tools/godot/Sync-GodotAiCodexConfig.ps1` bootstraps, validates and switches the exact allowlist.
- Custom Tactics tools remain deferred until the C# mutation kernel is stable.

## When to use

Use for validated, repeatable Godot Editor/MCP operations after Core/Application boundaries and tests are already established.

## Workflow

1. Verify the vendor tree, ports and selected profile in the manifest; never use the Dock self-updater. Refresh the vendor only as an explicit reviewed dependency update.
2. Run `Open-GodotDev.ps1 -Mode Agent -UserDataProfile Worktree -GodotAiProfile phase3-observe`. If it prints `CODEX_RESTART_REQUIRED`, restart the Codex task once from this worktree root.
3. Before every write sequence, call `session_manage` and `editor_state`. Require exactly one session, Godot 4.7.1 and the canonical project path; otherwise stop.
   Repository writes that require session count `0` must use `godot-editor-lifecycle` to close and conditionally restore the verified Editor; do not improvise process commands.
4. Use only tools exposed by the selected cumulative profile. See `references/tool-boundary.md` for the stage matrix and permanent deny-list.
5. Keep domain validation in C# services/CLI/tests. Run GdUnit and headless verification independently of godot-ai smoke.
6. After changing version, ports or profile, rerun sync/check and restart the Codex task. Stop and investigate any project-root or launch drift.
   After a godot-ai upgrade, every machine must rerun `Sync-GodotAiCodexConfig.ps1`: `.codex/config.toml` is gitignored and does not travel with the repository.

## Examples

- `phase3-observe`: read the catalog, run the project, collect logs/screenshots and stop it.
- `content-authoring`: create repetitive Node structure only after the Scene/Resource contract and tests exist.

## Anti-patterns

- Do not make godot-ai a Core, Application, runtime, catalog or migration-ledger dependency.
- Do not use generic MCP composition as proof of atomic domain mutation.
- Do not edit the pinned plugin to solve a project-specific workflow.
- Do not launch the Editor directly, share Agent user data, or overlap verifier/GdUnit with the same-worktree Editor.
- Do not use `script_*`, `filesystem_manage`, `client_manage` or `autoload_manage`.
  Cursor has no Codex-style `enabled_tools` allowlist; the project `.cursor/hooks.json` `beforeMCPExecution` guard hard-blocks only the permanent deny list from the manifest. It covers Agent Chat in a trusted workspace only.

## Checklist

- [ ] Pin, project-local config and active profile verified.
- [ ] Exactly one canonical Session and Editor root verified before writes.
- [ ] Generic operation is within the profile allowlist.
- [ ] Domain/test truth remains outside godot-ai.
- [ ] Independent tests and logs passed.
