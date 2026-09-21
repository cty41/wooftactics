---
id: direct-editor-launch-missing-adapter
status: superseded
signature: "Unable to load addon script from path: 'res://addons/tactics_tooling/TacticsEditorPlugin.cs'"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: editor
language: mixed
last_verified: 2026-09-02
---

# Direct Editor launch before the Adapter build

## Observed

Opening `godot/project.godot` directly before the production Adapter had been built left `godot/.godot/mono/temp/bin/Debug/Tactics.Godot.Adapter.dll` absent. Godot could not instantiate the C# addon entry, reported the signature above and disabled `tactics_tooling` before project C# code could explain the supported recovery path.

## Reproduction

1. Close the canonical Editor.
2. Ensure the Debug Adapter DLL is absent.
3. Open the project through the Project Manager or a direct `Godot --editor --path godot` command instead of `Tools/godot/Open-GodotDev.ps1`.
4. Observe the addon load failure during plugin initialization.

An old DLL can hide the immediate error while still bypassing the required worktree user-data, build, assembly identity and godot-ai bootstrap checks.

## Cause and resolution

The addon `plugin.cfg` previously pointed directly to a C# `EditorPlugin`, so no project-owned code could run when the project assembly was missing. The first mitigation added a dependency-free GDScript Bootstrap but over-corrected by blocking every direct Human open unless `--tactics-open-godot-dev=v1` was present. That launch guard is now superseded by `.agents/docs/godot-editor-bootstrap-contract.md`.

The current Bootstrap always owns the Main Screen. A direct Human open loads the C# Workbench immediately when the assembly is instantiable; otherwise it reports `TACTICS_TOOLING_BUILD_REQUIRED` and offers an explicit asynchronous fixed-helper Build. The helper validates the locked SDK and assembly identity, writes a tokenized result/log, and the same Editor polls `can_instantiate()` after filesystem scan. Prepared launch remains mandatory for Agent/godot-ai/MCP readiness; only prepared controllers start the writable authoring bridge. Export and package audits still exclude the entire Editor-only addon from `Tactics.pck`.

## Evidence

- `official_docs`: Godot user arguments are passed after `--` and read through `OS.get_cmdline_user_args()` — https://docs.godotengine.org/en/4.7/classes/class_os.html.
- `verified_local`: `Tools/godot/Test-TacticsToolingBootstrap.ps1` covers direct/prepared with a valid assembly, both missing-assembly `BUILD_REQUIRED` states, and direct same-PID build/result/reload. Standalone mode proves the Shell-created helper child; verifier-held-lock mode invokes the same helper under the verified live lock and still requires the same PID to reach Controller ready. No path contains the legacy addon signature.
- `verified_local`: the copied absolute launcher command was invoked with `-NoLaunch` from `C:\`; repository-root SDK resolution selected .NET 9 and the production Adapter build completed with 0 warnings and 0 errors.
- `verified_local`: the Windows package policy tests verify the export preset exclusion and reject a valid-format fixture whose PCK directory contains `addons/tactics_tooling`. A real Godot 4.7.1 `--export-pack` then passed the semantic directory-entry audit with no Editor/test entries; payload strings inside packed files are not treated as paths.
- `verified_real_reload`: `Tools/godot/Test-TacticsToolingAssemblyReload.ps1` first denied a prepared marker without matching dev-session, then accepted a current PID/path/worktree/mode/time session and completed same-PID reload, bridge pipe/token rotation and four Preview calls without forbidden reload signatures.
- `gate_boundary`: the complete verifier reached its pre-existing pinned godot-ai vendor check and stopped on an unrelated vendor-tree hash drift; this Incident does not claim a complete unified-gate pass.
- `manual_boundary`: the GUI dialog, clipboard contents and Main Screen appearance remain pending human acceptance.

## Scope and invalidation

This historical route applies only to the exact legacy C# addon-entry signature. Direct Human open is now supported and is not itself an incident. Other addon parse errors, helper failures, assembly unload failures and wrong-project-root cases retain their own routes. Revalidate after changing the addon entry, helper contract, Godot version or C# assembly-loading behavior.
