# Manual QA Output Contract

Read this file only when changing `.agents/docs/manual-acceptance.md` or producing a manual acceptance handoff.

## Ledger Schema

Use stable IDs shaped as `MQA-<SYSTEM>-<SUBSYSTEM>`. Each item is a level-three heading and contains:

- `Status`: `pending`, `passed`, `failed`, `deferred`, or `blocked`.
- `Source`: first introduction or latest reopen commit/task.
- `Reopen reason`: only when a previously passed item is reopened.
- `Action`, `Expected`, `Observe`, `Preserve on failure`, and `Save boundary`.
- `Automated evidence`: what automation proves and the remaining human boundary.
- `User verdict`: latest explicit user conclusion, `none`, or the automated-evidence basis when a fully asserted item is closed by rule.

Keep items under the matching status section. 观感、操作手感、难度体验和编辑器实际操作仍须用户明确说通过才能改为 `passed`。预期结果已全部被测试断言的事实（页面文字、存档字段、导航落点）不新增人工条目；已有条目被测试完全覆盖后可改为 `passed`，并在 `User verdict` 写明依据是自动证据和本规则。Preserve pending items when plans are archived.

`## Last Emitted Order` maps the most recent numbered output to stable IDs:

```markdown
1. `MQA-GODOT-BOARD-FIT`
2. `MQA-GODOT-INVENTORY`
```

Interpret `1–4 OK`, `5 failed: ...`, and `6 deferred` through this mapping. Reject ambiguous numbering when no valid mapping exists.

## Reopen Rules

- Reopen only if the current change touches the accepted behavior or its presentation/input/lifecycle dependency.
- Record the change and reason. Do not reopen neighboring or unrelated items.
- A fixed `failed` item becomes `pending` only after its review and required gates pass.
- A failing gate produces a blocked handoff, not a ready-for-QA list.

## Output Structure

Always use these sections when a formal checklist is emitted:

1. `本轮重点`: new work, fixes, and reopened regressions.
2. `累计待验收`: older pending/deferred work not directly changed this round.
3. `无需重复人工验证`: logic and structural results adequately covered by automation.
4. `环境与收尾`: shortest setup, Editor/scene state, Reload/log checks, and cleanup.

Order steps as one user journey. Avoid repeated Editor restarts, Run creation, or navigation.

Each numbered human step must say:

- **操作**: exact user action.
- **预期**: visible or experiential result.
- **观察**: HUD、Godot Output、Inspector 或其他明确命名的位置。
- **失败保留**: screenshot, exact log, current scene/Run, seed, or save copy.
- **存档边界**: production save, isolated Run, read-only scene, or destructive consequence.

End with:

```text
1–4 OK
5 failed：实际现象……
6 deferred
Output：无异常
```

## Scenario Decisions

| Scenario | Required behavior |
|---|---|
| Godot fix is reviewed and verified | Emit changed/reopened items plus older Inventory pending. |
| Multiple Godot surfaces have pending items | Keep them separate and name the exact Editor/runtime observation surface. |
| Required automated gate fails | Report the gate and do not say acceptance can start. |
| User reports partial numbered results | Resolve numbers through `Last Emitted Order`, then update stable IDs. |
| A related presentation change follows a pass | Reopen only the affected visual/input item with a reason. |
| An unrelated code change follows a pass | Keep the passed item closed. |
| A plan is archived | Retain all unresolved ledger items. |
| Logic is fully asserted by automation | Put it under `无需重复人工验证`, not the human list. |

Explicit and implicit `$manual-qa-handoff` invocation must produce the same structure.
