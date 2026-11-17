---
name: manual-qa-handoff
description: Use when reviewed and automatically verified work still needs human visual, UI/Input, gameplay, Editor, Reload, or experiential acceptance; when the user asks to update or output manual QA, acceptance, visual QA, UI/Input QA, Reload smoke, 手动测试、人工验收、复验或测试 TODO; or when the user reports checklist items passed, failed, blocked, or deferred.
---

# Manual QA Handoff

## Quick Reference

- Treat `.agents/docs/manual-acceptance.md` as the current manual-acceptance ledger. Passed history lives in `.agents/docs/manual-acceptance-history.md` and is not re-emitted.
- Read [references/output-contract.md](references/output-contract.md) only when updating the ledger or rendering a handoff.
- Use the applicable Unity/Godot testing skill for automated evidence. Do not duplicate its commands here.
- Prefer evidence in this order: explicit user verdict, current diff/commit and gates, active plan, ledger, then OKF.

## When to use

Run explicitly whenever the user asks for a manual checklist or reports checklist results. Run proactively only after implementation, code review, and required automated gates have completed and a human boundary remains.

Do not announce that work is ready for acceptance while a required 自动门禁 or review is failing. Do not proactively emit a checklist during design, diagnosis, implementation, review-only work, or a fully automated change with no pending human boundary.

## Workflow

1. Read the ledger and map any ordinal feedback through `Last Emitted Order`.
2. Update only items supported by explicit feedback or the current verified change.
3. 新的人工边界默认写进 `MQA-GODOT-SINGLE-RUN`，不另开九字段条目。只有这一局里看不到的新观感，才另加一行短待验收。把用户报告的缺陷标为 `failed`；修复通过复查和门禁后，再带回打一局或那一行。
4. Mark `deferred` or `blocked` only from the user's statement or an evidenced external blocker.
5. 观感、操作手感、难度体验和编辑器实际操作仍须用户明确说通过才能改为 `passed`。预期结果已全部被测试断言的事实（页面文字、存档字段、导航落点）不新增人工条目，也不要求看截图；已有条目被测试完全覆盖后可改为 `passed`，`User verdict` 写明依据是自动证据和本规则，不写成用户看过。
6. Reopen a passed item only when its relevant behavior, UI, presentation, flow, or Editor lifecycle changed.
7. 交清单时只给这一局，不重复历史。用规定的四段写出最短旅程，再把 `Last Emitted Order` 换成这次发出的稳定 ID。

Keep the ledger compact: store current acceptance state and evidence boundaries, not full plans or test logs. Never operate foreground UI on the user's behalf unless separately authorized.

## Anti-patterns

- Repeating every historical passed item after an unrelated change.
- Presenting automated assertions as visual or experiential acceptance.
- Asking the user to observe an event without naming HUD, CheatConsole, Output, Console, or Editor panel.
- Losing pending items when an active plan is archived.
- Treating temporary list numbers as permanent identities.

## Checklist

- Review and required automated gates are green, or the response clearly reports the blocker.
- 发出的待验收写清看什么、怎样算过；不把历史结论或测试日志再列一遍。
- Current-round regressions precede cumulative pending work.
- Automated-only coverage is separated from human work.
- `Last Emitted Order` matches the final numbered list.
