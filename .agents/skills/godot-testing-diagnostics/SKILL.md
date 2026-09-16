---
name: godot-testing-diagnostics
description: Use when building or testing Tactics Core, Application, Godot C#, GdUnit4Net, headless runtime/editor, migration tools, skills, Incidents, or diagnosing engine errors.
---

# Godot Testing Diagnostics

## Quick Reference

Start with the narrowest local test. For product-code completion, run `Tools/godot/Verify-GodotProject.ps1` at most once from repository root; it is deliberately sequential because Core and Godot builds previously contended for shared `obj` outputs. Do not trigger hosted CI or remote exports unless the user explicitly asks.

## When to use

Use after migration code/tool/policy changes or whenever build, GdUnit, headless, plugin, reload or resource validation fails.

## Workflow

1. Capture the first exact error signature, command, context and version.
2. Reproduce with the narrowest applicable step; do not hide it with a full clean.
3. If it is engine/version-sensitive, route through Research Guide.
4. Fix the cause and run the narrow test again.
5. Escalate validation by cost: narrow test → related local gate → one unified sequential verifier when product code is ready. Documentation-only changes stop at their policy/docs validators.
6. Treat hosted work as opt-in: do not call `gh workflow run`, rerun GitHub Actions, or start remote RC/export/artifact jobs without an explicit user request or an approved plan item. Observe automatically triggered required checks; if a missing remote check blocks merge, ask for authorization instead of dispatching it.
7. Record an engine/toolchain issue as an Incident with local evidence; ordinary syntax mistakes stay in normal task history.

## Examples

- File-lock errors mentioning `Tactics.Core/obj` route to the parallel-build Incident.
- `ScriptTypeBiMap.Add` duplicate keys route to the C# assembly-reload Incident.

## Anti-patterns

- Do not run Core and Godot `dotnet test` in parallel.
- Do not delete `.godot` or all build outputs before preserving the first failure signature.
- Do not treat `Build succeeded` as GdUnit/editor/runtime validation.
- Do not dispatch or repeat expensive hosted CI merely to refresh evidence after documentation, QA-ledger, or PR-description changes.

## Checklist

- [ ] Exact signature/context/version captured.
- [ ] Narrow reproduction performed.
- [ ] Validation stopped at the cheapest sufficient local gate; product-code completion used no more than one unified verifier.
- [ ] No hosted CI/export was triggered without explicit authorization.
- [ ] Relevant Incident updated only when evidence warrants it.
