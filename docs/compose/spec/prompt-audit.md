---
feature: prompt-audit
status: in-progress
updated: 2026-11-11
source: feat/prompt-audit
---

# P4 Prompt Audit（旧模型规则清理）

## Report

## [S1] 问题

规则与 skill 文本积累自不同模型时代：过度具体的手把手指令、跨文件重复的警告、为旧模型失败模式打的防御补丁，如今只会拖慢或僵化更强的模型——文章洞察"过时规则会挡住新模型多干活"正是此意。需要一次系统性 prompt audit；但规则文本是行为指南，删错一句的代价远大于删错一个 description，处置必须保守。

## [S2] 设计

### S2.1 审计口径

**算旧模型补丁（候选）**——
1. 过度具体：模型自身能稳定判断的微观步骤（把"先 X 再 Y 再 Z"式机械指令简化为意图 + 验收）；
2. 冗余重复：同一禁令在 AGENTS.md、rules、skills 间多处重复表述；
3. 时代补丁：为旧模型典型失败写的通用防御句（"不要跳步/不要编造"类），且无事故溯源。

**不算补丁（一律不动）**——可溯源到事故记录（`.agents/incidents/`）、绝对禁止清单、验证阶梯、Godot 迁移审计的规则；OKF/门禁的操作细节；安全与合规边界。这类条目即使写得啰嗦也只列观察。

### S2.2 两段式执行

1. **初筛（拆给子代理，按 `.agents/rules/subagent-handback.md` 回执契约）**：分批扫描 `AGENTS.md` + `.agents/rules/*.md` + `.agents/skills/*/SKILL.md`，逐条标记候选，给出行号锚点与建议 diff；不直接改任何文件。
2. **主循环裁决与落地**：对照事故溯源逐条裁决（修/不修 + 理由），低风险直接修，其余进报告建议区。裁决记录写入审计报告。

### S2.3 处置边界

- 直接修：经裁决确认的冗余与过度具体表述的删减/简化，语义零丢失。
- 只建议：删除规则条目、合并规则文件、AGENTS.md 结构重排、任何事故溯源条目的修改。

### S2.4 交付物

审计报告 `.agents/docs/prompt-audit-2026-11.md`（候选清单含逐条裁决与理由、修订摘要）+ 修订 diff 本身。

### S2.5 工作区覆盖声明

隔离 worktree `.worktrees/prompt-audit` 开发（`agent-worktree.md` 例外条款 1：共享检出停留在其他会话的分支且带脏文件）。分支堆叠于 `feat/skill-audit`（PR #29），**PR 依赖 #29 先合**；与 PR #27 仅 `subagent-handback.md` 一段交集。

## [S3] Out of Scope

- 删除/合并规则文件；AGENTS.md 结构重排。
- 事故溯源规则的任何修改（只列观察不动手）。
- 用户级共享 skill（`~/.agents/skills`）。
- skill 正文中非规则性内容（教程、参考表）的重写；`Tools/**/README` 等工具文档。
- P5（扩 case、hillclimb、事件时间戳）。

## Tasks

- [ ] T1: 子代理初筛 — acceptance: 覆盖 AGENTS.md + 全部 rules + 15 个 SKILL.md；每条候选含行号锚点与建议 diff；回执四段齐备。(covers: S2.1, S2.2)
- [ ] T2: 主循环裁决与低风险修订 — acceptance: 每条候选有裁决（修/不修+理由）；修订语义零丢失；事故溯源条目零改动。(covers: S2.2, S2.3)(depends: T1)
- [ ] T3: 审计报告 — acceptance: 报告含候选清单/逐条裁决/修订摘要。(covers: S2.4)(depends: T2)
- [ ] T4: 独立 review 与收口 — acceptance: review 无关键缺口；Report 记录验证命令与结果。(covers: S2.5)(depends: T1, T2, T3)

## Journey log
