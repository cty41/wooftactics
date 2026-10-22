---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot migration provenance
description: 已完成迁移的冻结来源、Godot ownership、生成批次与验证边界。
tags: [migration, godot, provenance, testing]
timestamp: "2026-10-22T16:00:45+08:00"
status: active
catalog_scope: godot-migration
repo_paths:
  - src/Tactics.Core
  - src/Tactics.Application
  - src/Tactics.FrozenOracle.Tests
  - godot
  - Tests/golden
  - Tools/migration
  - Tools/migration/manifest/retirement
verified_revision: 2b341cb3
source_fingerprint: sha256:d683fa906f9b88264d5913b2267b912ed36b5a9b8e882e7019c86e03be84cfa7
---

# Current State

当前追加开发为 `.agents/plans/roguelike-act-node-scene-architecture.md`；它不是重新迁移Unity。已新增纯Core定义与Setup首战原子提交，并经ResourceSaver生成22个正式Act变体（local catalog含legacy共24项）。schema2 ledger维护target SHA、磁盘UID和manual_qa_pending；重复生成字节一致。Main已准备16节点常驻Host并接营地借用；普通、Elite与Boss生产入口现按已验证节点身份借用常驻Surface，世界相机复用既有角色/VFX坐标且不重写资源；事件同图战斗、战后控制器仍未完成，不晋升整条体验验收状态。V12身份codec已验证，旧文档由codec标记待迁移，另有Pending内存归一化组件（不保存、不增revision，完整实际事件证据可恢复已知变体、矛盾仍拒绝）；生产仍V11，不把新codec或营地常驻当作恢复闭环。Act资源准备采用现有RequiredResourceLoader，缓存替身回归通过。2026-10-21新增纯Core节点状态机与旧字段推导（S1），不改资源、存档或Godot运行时。

Godot 4.7 C# 是唯一产品主线，`godot/project.godot` 是唯一项目。Core/Application 保持引擎无关，Godot Adapter 承担 Node、Resource、文件系统、UI、作者工具和运行时集成；Runtime UI以Main/Page/Run/Open Session/Frame显式边界治理重入状态，权威合同见`.agents/docs/runtime-ui-lifecycle-contract.md`。

迁移来源已冻结在最终 Tag、FrozenOracle、Golden、DTO、receipt、ownership ledger 与 retirement manifest 中。它们只用于来源审计、确定性回归和许可证证明，不提供 live 旧编辑器、旧 MCP 或旧资产写入路径。

内容生成只消费已绑定的冻结输入，经 Application typed draft、ResourceSaver/PackedScene、Catalog/UID、target hash、幂等与 rollback 门禁进入 Godot。自动验证不能替代视觉、操作、真实 Editor Reload 或干净 Windows 启动验收。

Roguelike 地图迁移的首个切片已建立 engine-neutral 10×10 模板、出生槽/入口/出口/状态层合同，以及由 `AdventureMapAssetBuilder` 通过 ResourceSaver 生成的共享等距 TileSet、开始营地和基础战斗模板。生成 manifest、固定 UID、Catalog 与连续两轮字节级幂等共同约束这些资源；运行时不再为 Adventure Board 临时绘制双色 TileSet。

开发工具链把固定的 MIT godot-ai v3.2.5 源码作为 Editor-only vendor 依赖纳入公开源码，并由 manifest 中的 tag、commit、tree digest 与 license digest 审计；它不进入游戏 PCK/Windows 包。Editor采用Human Direct与Prepared Agent双入口：人工Project Manager入口由无C#依赖的GDScript Shell始终注册Main Screen，缺Adapter assembly时提供显式异步Build和同PID reload；direct controller不启动可写authoring bridge。Agent、自动化和godot-ai/MCP仍通过prepared launcher完成production C# Build、worktree用户数据隔离、项目级Codex配置、可写bridge准入和会话租约，避免测试宿主程序集或另一个worktree窗口被误认。整个tooling addon同样由export preset与包审计排除在游戏PCK外。

# Relationships

- 当前开发与验证入口见 [Godot Agent Workflow](../operations/godot-agent-workflow.md)。
- 历史 Agent 工具退役索引见 [Archived Unity Agent Workflow](../operations/unity-agent-workflow.md)。
- 当前人工状态见 `.agents/docs/manual-acceptance.md`。

# Verification Guidance

迁移事实必须同时核对冻结来源、receipt/manifest、生成目标、Catalog/UID 与测试。禁止恢复已退役目录作为当前实现旁路，也禁止改写 retirement evidence 来迁就当前工作树。
