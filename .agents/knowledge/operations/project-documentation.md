---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/.agents/docs
title: Project Documentation
description: 当前设计、活跃计划、统一缺口与 OKF 综合层的文档生命周期。
tags: [operations, documentation, plans, knowledge]
timestamp: "2026-10-22T16:00:44+08:00"
status: active
catalog_scope: project-documentation
repo_paths:
  - .agents/docs
  - .agents/plans
  - .agents/skills/project-doc-organization/SKILL.md
  - .agents/skills/plan-mode-plan-writer/SKILL.md
  - .agents/skills/manual-qa-handoff/SKILL.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:d2557e410860f5f4d93ce23bdaedcd2cdc40d346d4f37677f159bd228d7307be
---

# Current State

Act改造计划顶部持续记录切片证据：目前正式模板生成、Main准备期与营地借用已验证；普通/Elite/Boss借用常驻Surface已有生产呈现与组件证据，身份模型/V12编码切片已有证据，Pending旧档投影和内存归一化已验证，生产启动请求防线含旧队伍修复投影回归；完整旧Pending实际变体已支持有限恢复和正式目录加载，但生产接入、其它阶段迁移与原子入口仍待解决；事件同图战斗、战后/完整V12恢复未完成。合同仍为approved_target，整计划保持活跃，自动UI通过不替代manual_qa_pending。

`.agents/docs/` 主要保存当前设计与使用指南；同一主题优先维护一份权威文档。`brainstorm.md` 是唯一的临时灵感收集箱，不属于设计真相源或实施承诺。想法成熟后迁入主题设计、[项目已知缺口](../plans/project-known-gaps.md)或正式计划。

魔剑士 `Demonbound` 已从职业 brainstorm 迁入 `.agents/docs/demonbound-class-design.md` 作为唯一权威设计，并由 `.agents/plans/demonbound-loop-development.md` 持续跟踪非大师实现、自动样本与人工门禁。三个大师技能及正式美术/完整表现仍由[项目已知缺口](../plans/project-known-gaps.md)导航；在人工账本通过前不得把自动绿灯表述为体验验收。

`.agents/plans/` 只保存仍需执行且 decision-complete 的计划。实现完成并验证后，长期规则回写 docs，未实施项进入已知缺口或经批准的新计划，completed plan 随后删除并由 Git 保留历史。

`.agents/docs/roguelike-act-tilemap-gap-review.md` 记录 2026-10-21 全 TileMap 化缺口审查与 S1–S6 重排切片，是当前执行顺序的讨论依据。当前 `.agents/plans/roguelike-act-node-scene-architecture.md` 修订父计划的执行顺序与生命周期：Demo 仅一个 Act、全节点正式底图常驻、Preview/Active 同实例。`roguelike-map-real-tilemap-atlas.md` 继续作为内容覆盖的父计划，跟踪共享 TileMap 基础、地图集、战斗与事件同图、节点级恢复和最终人工收口；其移动子计划 `adventure-leader-movement-parity.md` 冻结全部 Active Board 的逐格 Tween、独立 Adventure 寻路政策、格中心改道、交互锁与最终单次提交，长期合同位于 `.agents/docs/adventure-movement-contract.md`。首个开始营地切片或移动子切片完成不等于整份地图计划完成；后续里程碑仍须独立 review、自动门禁与人工账本判定。

Runtime UI的Main/Page/Run/Open Session/Frame所有权与Reset/Refresh/Resume重入政策集中在`.agents/docs/runtime-ui-lifecycle-contract.md`，输入专项合同只引用该边界而不建立并列真相源。

`.agents/knowledge/` 负责跨系统摘要、关系和导航，不复制完整设计或已完成计划。代码、Godot Resource 和测试仍是当前行为的最终事实源。

当前 Godot 人工验收状态由 `.agents/docs/manual-acceptance.md` 以稳定 ID 维护；需要继续开发或决策的 TODO 则只进入 `.agents/docs/project-known-gaps.md`，不另建并列总清单。实现通过 code review 与自动门禁后，`manual-qa-handoff` 只重开受本轮行为、UI、表现、流程或 Editor 生命周期影响的项目，并输出本轮重点、累计待验收、自动覆盖边界和最短操作旅程；自动证据不能把人工项晋升为 passed，只有用户明确反馈可以更新人工结论。直接打开 Editor 的恢复提示、剪贴板命令和经支持入口恢复 Tooling 的 GUI 边界由 `MQA-GODOT-DIRECT-LAUNCH-GUARD` 独立跟踪。

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
