# Godot Editor Bootstrap Contract

## Scope

This contract separates the supported human Project Manager flow from the prepared Agent/automation flow without moving authoring rules out of the typed C# Application/Adapter layers.

## Human Direct Entry

- Opening `godot/project.godot` from Godot Project Manager is supported.
- The GDScript `TacticsToolingBootstrap` always owns the Tactics Main Screen tab.
- When `Tactics.Godot.Adapter.dll` is available and its controller can instantiate, the existing C# Workbench loads immediately.
- When the controller cannot instantiate, the Main Screen enters a non-error first-build state. It does not disable the addon or require a second Editor.
- The user explicitly starts the build with `BUILD C# AND RELOAD`; no external build starts merely because a project was opened.
- A direct Human session keeps the typed Workbench UI available, but its controller must not start the writable authoring named-pipe bridge. It does not prove worktree-isolated `user://`, Codex/godot-ai setup, operation-lock ownership, or an Agent session descriptor.

## Prepared Agent Entry

- Agents, lifecycle restoration, automation that needs an Editor, and godot-ai/MCP authoring continue to use `Tools/godot/Open-GodotDev.ps1`.
- The prepared launcher owns Godot/.NET version checks, serial Adapter build, worktree user data, project Codex configuration, operation lock, launch argument, and session record.
- `--tactics-open-godot-dev=v1` only requests prepared capabilities; it is neither controller admission nor bridge authorization.
- The writable bridge starts only after `.godot/tactics-dev-session.json` matches the current Editor PID, canonical repo/project paths, derived worktree key, mode, and a `startedAt` correlated with that PID's process start. Missing/mismatched evidence remains fail-closed.

## Shell Ownership

- `TacticsToolingBootstrap.gd` is the only `EditorPlugin` and Main Screen owner.
- The Shell owns first-build status, asynchronous helper execution, result-token validation, reload waiting, retry/copy diagnostics, and cleanup.
- `TacticsEditorController.cs` remains a `#if TOOLS`/`[Tool]` Node. C# keeps typed Workbench, ChangeSet, ResourceSaver, Undo/Redo, Preview and authoring bridge behavior.
- GDScript must not decide gameplay or authoring transaction legality.

## Build and Reload

- The Shell invokes only the repository-fixed `Tools/godot/Build-GodotDevAssembly.ps1` with fixed arguments and a random token.
- The helper serially builds the Debug Adapter, validates assembly identity, and atomically writes a tokenized result plus full log.
- Build runs outside the Editor process and must not freeze it. Repeated clicks cannot start concurrent builds.
- A successful build waits for Godot assembly reload and loads the controller in the same Editor PID when possible.
- If the controller remains unavailable after the bounded wait, the Shell asks the user to close and reopen the project; it never starts a duplicate Editor.
- Build failure keeps the Editor usable and exposes the first error, full log path, copyable command and retry.
- This contract handles missing or non-instantiable assemblies. It does not define source-versus-DLL staleness detection.

## Stable Diagnostics

- `TACTICS_TOOLING_BUILD_REQUIRED`
- `TACTICS_TOOLING_BUILD_FAILED`
- `TACTICS_TOOLING_RESTART_REQUIRED`
- `[Tactics Tooling Bootstrap] Controller ready.`
