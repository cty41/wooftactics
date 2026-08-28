---
id: direct-editor-launch-missing-adapter
status: verified
signature: "Unable to load addon script from path: 'res://addons/tactics_tooling/TacticsEditorPlugin.cs'"
godot_version: 4.7.1-stable-mono
dotnet_sdk: 9.0.312
os: Windows
context: editor
language: mixed
last_verified: 2026-08-28
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

The addon `plugin.cfg` previously pointed directly to a C# `EditorPlugin`, so no project-owned code could run when the project assembly was missing. The addon now enters through a dependency-free GDScript Bootstrap. Supported Editor commands pass `--tactics-open-godot-dev=v1` after Godot's `--` user-argument separator. Without that contract, the Bootstrap does not touch the C# controller: GUI sessions receive a copyable four-step recovery message and headless sessions emit `TACTICS_TOOLING_BLOCKED_DIRECT_LAUNCH`.

After a supported launch, the Bootstrap dynamically instantiates the configured C# controller and proxies the Main Screen lifecycle. Godot can query `_has_main_screen()` before `_enter_tree()`, so the Bootstrap derives registration from immutable launch-time arguments and replays any early visibility request after the Controller is ready. It does not execute PowerShell, close the Editor or mutate the enabled-plugin list. Because the copied absolute command may be run from any shell directory, the launcher explicitly resolves `dotnet --version` and the Adapter build from the repository root so `global.json` selects the locked .NET 9 feature band instead of an unrelated caller SDK. The export preset and package audit both exclude/reject `addons/tactics_tooling` so the Editor-only Bootstrap is not shipped in `Tactics.pck`.

## Evidence

- `official_docs`: Godot user arguments are passed after `--` and read through `OS.get_cmdline_user_args()` — https://docs.godotengine.org/en/4.7/classes/class_os.html.
- `verified_local`: `Tools/godot/Test-TacticsToolingBootstrap.ps1` passed four probes both standalone and while reusing the verifier's live operation-lock object: direct launch with an old DLL, direct launch with the DLL temporarily absent, supported argument with the DLL absent, and supported argument with a valid DLL. A third run with a spaced `%TEMP%` path also passed; logs use worktree identity plus a random probe token. The negative/incomplete logs contained the project diagnostic and no legacy addon load signature; the positive log initialized the C# controller.
- `verified_local`: the copied absolute launcher command was invoked with `-NoLaunch` from `C:\`; repository-root SDK resolution selected .NET 9 and the production Adapter build completed with 0 warnings and 0 errors.
- `verified_local`: the Windows package policy tests verify the export preset exclusion and reject a valid-format fixture whose PCK directory contains `addons/tactics_tooling`. A real Godot 4.7.1 `--export-pack` then passed the semantic directory-entry audit with no Editor/test entries; payload strings inside packed files are not treated as paths.
- `verified_real_reload`: `Tools/godot/Test-TacticsToolingAssemblyReload.ps1` completed same-PID reload, bridge pipe/token rotation and four Preview calls without forbidden reload signatures.
- `gate_boundary`: the complete verifier reached its pre-existing pinned godot-ai vendor check and stopped on an unrelated vendor-tree hash drift; this Incident does not claim a complete unified-gate pass.
- `manual_boundary`: the GUI dialog, clipboard contents and Main Screen appearance remain pending human acceptance.

## Scope and invalidation

This route applies when the exact Tactics addon entry fails or when the project was opened without the supported launcher. Other addon parse errors, assembly unload failures and wrong-project-root cases retain their own Incident routes. Revalidate after changing the addon entry, launch argument, Godot version or C# assembly-loading behavior.
