---
name: godot-editor-lifecycle
description: Use when an authorized canonical Godot modification task needs the Editor closed for C#, ResourceSaver, build, generation, or reload-sensitive work and restored afterward, or when the user explicitly requests a safe Godot Editor close/reopen cycle.
---

# Godot Editor Lifecycle

本 Skill 遵循[前台交互与焦点保护规则](../../rules/foreground-interaction.md)。它只允许对 canonical Godot Editor 做可验证的正常关闭和按原状态恢复，不授权前台输入或强制终止。

## Quick Reference

| Need | Action |
|---|---|
| Snapshot | `session_manage(op="list")` then `editor_state` |
| Quiesce | Stop project playback; save the current scene when `scene_save` is available |
| Close | `scripts/Invoke-GodotEditorLifecycle.ps1 -Action Close -EditorProcessId <pid>` |
| Reopen | `scripts/Invoke-GodotEditorLifecycle.ps1 -Action Open` then poll MCP |
| Safety | Never force-kill; never reopen an Editor that was initially closed |

## When to use

- A canonical Godot change requires MCP session count `0` before repository writes or generation.
- C# Tool/EditorPlugin changes need a full process restart instead of assembly hot reload.
- A task must restore the user's original canonical Editor session after background work.

## Workflow

1. Call `session_manage(op="list")`.
   - Count `0`: record `was_open=false`; do not close and do not open at task end.
   - Count other than `1`: stop without touching any process.
   - Count `1`: require Godot `4.7.1`, project path ending in the current worktree's `godot/`, and readiness `ready`. Record `session_id`, `editor_pid`, and `current_scene`.
2. Call `editor_state`. If playback is active, call `project_manage(op="stop")` and re-read state until stopped. If `scene_save` is exposed and a scene is open, save it and require success. If the task introduced unsaved MCP edits but cannot save them, stop.
3. Run the close action with the exact recorded PID:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  .agents/skills/godot-editor-lifecycle/scripts/Invoke-GodotEditorLifecycle.ps1 `
  -Action Close -EditorProcessId <pid>
```

4. Require the close command to return `status=closed`, the process to disappear, and `session_manage` to return count `0`. A timeout means an unsaved dialog or plugin blocked shutdown: preserve the Editor, do not force it, and stop the mutation.
5. Perform the authorized repository work inside a logical `try/finally`. Preserve `was_open` and `current_scene` even if implementation or tests fail.
6. In the logical `finally`, reopen only when `was_open=true`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  .agents/skills/godot-editor-lifecycle/scripts/Invoke-GodotEditorLifecycle.ps1 `
  -Action Open
```

   The visible Editor may briefly receive focus; do not activate it and do not inject input. Poll for up to 120 seconds without starting a second process. Require exactly one new MCP session with the canonical path, Godot `4.7.1`, plugin `4.3.1`, and readiness `ready`.
7. If the restored `current_scene` differs from the snapshot, reopen the recorded `res://` scene through `scene_open`. Read editor/plugin logs without clearing them. Report lifecycle restoration separately from the task's build/test result.

## Anti-patterns

- Do not use force termination, generic process-name matching, Computer Use, window activation, or keyboard shortcuts.
- Do not launch a second Editor because MCP attachment is slow; keep polling the started PID.
- Do not act on multiple sessions, another project root, another Godot version, or an unverified PID.
- Do not promote migration ownership or visual acceptance merely because restart and reconnect succeeded.

## Checklist

- [ ] Initial open/closed state and current scene were recorded.
- [ ] Playback stopped and known MCP edits were saved.
- [ ] Close targeted the exact canonical Editor PID and session reached `0`.
- [ ] No force-kill or foreground input was used.
- [ ] The Editor was reopened only if this workflow closed it.
- [ ] New session path/version/plugin/readiness and logs were checked.
