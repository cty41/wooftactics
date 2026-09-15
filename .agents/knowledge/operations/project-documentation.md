---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/.agents/docs
title: Project Documentation
description: 当前设计、活跃计划、统一缺口与 OKF 综合层的文档生命周期。
tags: [operations, documentation, plans, knowledge]
timestamp: "2026-09-15T11:30:50+08:00"
status: active
catalog_scope: project-documentation
repo_paths:
  - .agents/docs
  - .agents/plans
  - .agents/skills/manual-qa-handoff/SKILL.md
verified_revision: c9ff3a710fd9
source_fingerprint: sha256:c7be57077ae35302c7f745c6c6f56734facf53b07ede491ddfb95fb9724a24b8
---

# Current State

`.agents/docs/` 主要保存当前设计与使用指南；同一主题优先维护一份权威文档。`brainstorm.md` 是唯一的临时灵感收集箱，不属于设计真相源或实施承诺。想法成熟后迁入主题设计、[项目已知缺口](../plans/project-known-gaps.md)或正式计划。

端到端指南 `.agents/docs/gameplay-design-to-development-workflow.md` 说明需求收束、玩法合同、可替换 Provider、受控 Draft、确定性编译、typed authoring、自动测试与人工验收。模型只生成带证据的候选；设计合同、代码、Resource 和测试继续承担权威，外接 LLM 不替代 Codex/开发者判断。

Pure Run 新装备的独立生产策略记录在 `.agents/docs/2026-08-25-pure-run-equipment-production-state-machine-design.md`：共享基础风格与品类锚点、正式生成血缘、本机第三方参考 descriptor、保真后处理、child remediation 和 cty41 风格 verdict 构成端到端门禁；历史装备记录保持兼容。

完整下一代生图状态机仍不进入 vNext schema 或代码设计。`.agents/docs/game-art-production-first-principles.md`、`.agents/docs/generative-art-current-strategy-map.md` 与 `.agents/docs/generative-art-state-machine-complexity-audit.md` 组成经 cty41 确认十项原则、但仍保持 draft 的分析集：以 Visual Moment、角色视觉状态集、真实 Godot 上下文、时间合同、成本和分层验收为中心。已按单独批准将现有通用 Pose Proof、Reviewer、record/workflow v0.1 核心抽取到公开 [MaLiang](https://github.com/cty41/maliang)，本仓通过 `Tools/vendor/maliang` 固定版本并用 `Tools/artworks/maliang.adapter.json` 注入 cty41 与 `256/128/y236/64×32` 项目规则；Pure Run 资产、历史和 Godot runtime context 仍留在本仓，其中 `64×32` 相对当前 `96×48`/.34 Game View 的有效性待重验。旧 `.agents/docs/pure-run-single-frame-action-pose-design.md` 的 Unity `UnitPoseFamily/ReleaseTime/PoseRestoreTime` 已标记为历史，不是当前 Godot 权威。当前动作生产的低成本 Pose Proof 门禁保持不变：兼容脚本在 ImageGen 前确定性输出 1–4 个纯橙火柴人方案及 128 预览，只确认动作时点、力线、重心和负形；cty41 选择后只保存 Action Card，未选完整几何/临时 PNG 不进入正式状态机或 Git。新 action_pose Composition schema v3 必须绑定 Card 或受控证据豁免，历史 v2 记录保持兼容；身份、装备、精确几何与最终批准继续职责分离。

魔剑士 `Demonbound` 已从职业 brainstorm 迁入 `.agents/docs/demonbound-class-design.md` 作为唯一权威设计，并由 `.agents/plans/demonbound-loop-development.md` 持续跟踪非大师实现、自动样本与人工门禁。腐化满后的恶魔失控形态规格（六维+5 派生重构、已学技能临时满级、敌友统一目标池、幸运修正永久死亡、墓碑记录、缺员继续）已实现在 `.agents/plans/demonbound-possessed-form-implementation.md`，六份 `DEMONBOUND-POSSESSED-*` 合同全部升级为 `verified_current`；30 固定样本已由自动测试覆盖，三局人工 Run 由 `.agents/docs/demonbound-possession-manual-checklist.md` 承接并在验收账本保持 `pending`。死亡来源的正式存档字段仍需单独立项。三个大师技能及正式美术/完整表现仍由[项目已知缺口](../plans/project-known-gaps.md)导航；在人工账本通过前不得把自动绿灯表述为体验验收。

诗人权威规则已收束在 `.agents/docs/poet-class-design.md`，动态行动顺序在 `.agents/docs/battle-initiative-rules.md`，Core Facing 合同已回写 `.agents/docs/battle-facing-rules.md`。本轮装备明确延期且没有 schema/Resource 实现，未来 family、资格、事务、背包满与存档迁移问题只记录在 `.agents/docs/equipment-system-future-design.md`；诗人已接入获批五红土松装备版 Idle DR/UL；Melee/Cast/Hit DR/UL 与通用 Death 已获批并仅离线晋升，尚未授权运行时绑定；正式分身美术仍是后续边界。

`.agents/plans/` 只保存仍需执行且 decision-complete 的计划。实现完成并验证后，长期规则回写 docs，未实施项进入已知缺口或经批准的新计划，completed plan 随后删除并由 Git 保留历史。

通用 Agent 技能（`brainstorming`、`make-dev-plan`、`plan-mode-plan-writer`、`project-doc-organization` 等）由用户级全局安装 `~/.agents/skills`（`cty41/skills`）提供；本仓库 `.agents/skills/` 只保留项目专属技能与两个特化技能（`knowledge-maintenance` 使用完整 `Tools/okf`、`manual-qa-handoff` 被 `Tools/agent-policy` 硬引用），契约见根 `AGENTS.md`。

`.agents/knowledge/` 负责跨系统摘要、关系和导航，不复制完整设计或已完成计划。代码、Godot Resource 和测试仍是当前行为的最终事实源。

可执行 skill 的 Python 依赖应在该 skill 目录内声明；调用统一 verifier 的 CI 必须显式安装所覆盖 skill 的 requirements，不能依赖开发机或 runner 的偶然全局包。

当前 Godot 人工验收状态由 `.agents/docs/manual-acceptance.md` 以稳定 ID 维护；需要继续开发或决策的 TODO 则只进入 `.agents/docs/project-known-gaps.md`，不另建并列总清单。实现通过 code review 与自动门禁后，`manual-qa-handoff` 只重开受本轮行为、UI、表现、流程或 Editor 生命周期影响的项目，并输出本轮重点、累计待验收、自动覆盖边界和最短操作旅程；自动证据不能把人工项晋升为 passed，只有用户明确反馈可以更新人工结论。

Agent-first Editor 开发入口及首次 Codex 重启、worktree 隔离、共享人工 QA 边界记录在 `.agents/docs/godot-agent-first-development.md`；真实 Dock 可见性和跨 worktree 路由继续由人工验收账本判定。

Godot Content Workbench 的能力防回退基线保存在
`.agents/docs/godot-content-workbench-capability-matrix.md`。它将最终 Unity 标签中的有效自定义编辑器能力逐项映射为
`implemented`、`partial`、`replacement` 或 `excluded`，并绑定当前 Godot 页面、Application 作者合同、自动证据
和人工收口条件；不得用笼统的 `replaced_by_godot_design` 代替实际证据。`partial` 只有在代码、自动门禁和真实
Editor 人验均完成后才能晋升。

迁移阶段计划和阶段审计已由当前 Godot 实现、权威设计与人工验收账本接管；完成计划由 Git 历史保留，不再作为当前文档入口。

# Relationships

- [OKF Maintenance](okf-maintenance.md)负责从路径变更反向同步知识 scope。
- [Godot Agent Workflow](godot-agent-workflow.md)定义当前代码、Resource 和验证安全边界；[Archived Unity Agent Workflow](unity-agent-workflow.md)仅用于历史追溯。
- [Project Known Gaps](../plans/project-known-gaps.md)集中保存尚未激活的真实缺口。
- [Godot Agent Workflow](godot-agent-workflow.md)导航 Research Guide、Incidents 与 verified 结论。

# Verification Guidance

整理文档时检查重复主题、失效链接、旧架构术语和计划状态；不以截图证明功能。删除历史文件后运行 OKF 影响检测与 bundle 校验。
