---
name: godot-csharp-development
description: Use when adding or modifying Godot 4.7 C# Node, Resource, signal, lifecycle, tool script, runtime adapter, or project reference code. Resource here means the C# class; migration-batch asset generation belongs to godot-content-migration.
---

# Godot C# Development

## Quick Reference

- Core/Application: pure .NET; no `Godot`, `UnityEngine`, Node, Resource, Editor API, or migration DTO.
- Adapter: Godot objects remain here; `ContentSnapshot` remains pure .NET.
- Editor-only code: `[Tool]`, `#if TOOLS`, explicit cleanup.
- After `.cs` changes: build and run related Core/Application/Godot tests.

## When to use

Use for C# changes under `src/` or `godot/src/`, including Resource classes, lifecycle bridges, signals and assembly/reload work.

## Workflow

1. Identify the target layer and prove its allowed dependencies.
2. Search existing project types and APIs before adding names or `using` directives.
3. For uncertain Godot APIs, use the router Research Guide and verify against 4.7.1.
4. Keep gameplay transitions deterministic and engine-neutral; adapters translate Node/Resource state to commands/drafts and consume events.
5. Disconnect signals, cancel tracked work and release temporary Nodes/Resources on exit, exception and reload paths.
6. Build the unified solution sequentially, then run the relevant tests and engine smoke.

## Examples

- A Resource catalog loader returns a Godot-only registry plus a pure Application snapshot.
- A Node bridge registers async work with `BattleRuntimeScope` and cancels it in `_ExitTree`.

## Anti-patterns

- Do not store `Godot.Resource` in `ContentSnapshot`.
- Do not use static mutable state to survive assembly reload.
- Do not assume GDScript naming or lifecycle behavior maps directly to C#.

## Checklist

- [ ] Layer dependency is legal.
- [ ] Current 4.7 API verified when uncertain.
- [ ] Exit/reload/exception cleanup implemented.
- [ ] Build and targeted tests passed.
