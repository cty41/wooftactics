---
id: godot-ai-project-run-cold-timeout
status: observed
signature: "Command run_project timed out after 5.0s on session <session_id>"
godot_version: 4.7.1-stable (official)
dotnet_sdk: 9.0.312
os: Windows
context: editor
language: mixed
last_verified: 2026-08-09
---

# godot-ai cold project run timeout with a live game

## Observed

The first `project_run(mode="main", autosave=false)` call in a newly restarted Codex task returned `Command run_project timed out after 5.0s on session godot@0869`. Immediate Session and Editor reads showed the same Editor in `playing` state with `game_status.status="live"`, `helper_live=true`, and `game_capture_ready=true`. Plugin logs contained the original `run_project` request, its deferred request ID, the transition to playing, and the game-helper hello event.

## Reproduction

The narrow retry after a successful plugin reload/reconnect did not reproduce the timeout: `project_run` returned normally after the helper became live in 2741 ms. The issue therefore remains `observed`, not `reproduced`.

## Cause and resolution

The cause is not established. Treat this signature as an ambiguous command deadline rather than proof that launch failed. Before retrying or launching a second game, call `session_manage(op="list")` and `editor_state`; if the same run is live, continue with logs/screenshot evidence and stop it normally. No godot-ai or project code was changed for this observation.

## Evidence

- `verified_local`: the initial tool result returned the exact 5.0-second timeout while the immediate `editor_state` reported the run live and capture-ready.
- `verified_local`: plugin logs recorded `run_project`, deferred execution, `play_state_changed -> playing`, and the matching game-helper hello.
- `verified_local`: game logs contained `Tactics Godot migration runtime ready`; the 1280×720 capture returned `stale_frame=false`, and `project_manage(op="stop")` restored Editor readiness.
- `upstream_source`: godot-ai v3.1.2 `tools/project.py`（观察时的版本；仓库现已钉到 v4.3.1，未重新复现） documents helper-readiness waiting and state polling for late transitions.

## Scope and invalidation

This applies only to the project-scoped godot-ai v3.1.2 Attach path (observed before the v4.3.1 refresh) on Windows. It does not establish a Godot runtime failure or a general retry policy. Re-evaluate after a stable reproduction, a godot-ai version change, or a change to command/helper readiness deadlines.
