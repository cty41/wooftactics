---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot migration provenance
description: 已完成迁移的冻结来源、Godot ownership、生成批次与验证边界。
tags: [migration, godot, provenance, testing]
timestamp: "2026-08-28T11:46:27+08:00"
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
source_fingerprint: sha256:fc776ad396b026817e67683f6efe58cc49ea67db8cd8a71d28756e5673817279
---

# Current State

Godot 4.7 C# 是唯一产品主线，`godot/project.godot` 是唯一项目。Core/Application 保持引擎无关，Godot Adapter 承担 Node、Resource、文件系统、UI、作者工具和运行时集成。

迁移来源已冻结在最终 Tag、FrozenOracle、Golden、DTO、receipt、ownership ledger 与 retirement manifest 中。它们只用于来源审计、确定性回归和许可证证明，不提供 live 旧编辑器、旧 MCP 或旧资产写入路径。

内容生成只消费已绑定的冻结输入，经 Application typed draft、ResourceSaver/PackedScene、Catalog/UID、target hash、幂等与 rollback 门禁进入 Godot。自动验证不能替代视觉、操作、真实 Editor Reload 或干净 Windows 启动验收。

Roguelike 地图迁移的首个切片已建立 engine-neutral 10×10 模板、出生槽/入口/出口/状态层合同，以及由 `AdventureMapAssetBuilder` 通过 ResourceSaver 生成的共享等距 TileSet、开始营地和基础战斗模板。生成 manifest、固定 UID、Catalog 与连续两轮字节级幂等共同约束这些资源；运行时不再为 Adventure Board 临时绘制双色 TileSet。

开发工具链把固定的 MIT godot-ai v3.2.5 源码作为 Editor-only vendor 依赖纳入公开源码，并由 manifest 中的 tag、commit、tree digest 与 license digest 审计；它不进入游戏 PCK/Windows 包。Editor 通过统一入口完成 production C# Build、worktree 用户数据隔离、项目级 Codex 配置和会话租约，避免测试宿主程序集或另一个 worktree 的窗口被误认成当前项目。Tactics Tooling 的 GDScript Bootstrap 在缺少入口启动契约时不会加载 C# controller，而是 fail-closed 并给出恢复步骤，因此首次直接打开且 Adapter DLL 不存在时也不再由含糊的 C# addon 错误承担指导职责；受支持入口则以构造时参数完成早期 Main Screen 注册。整个 tooling addon 同样由 export preset 与包审计排除在游戏 PCK 外。

# Relationships

- 当前开发与验证入口见 [Godot Agent Workflow](../operations/godot-agent-workflow.md)。
- 历史 Agent 工具退役索引见 [Archived Unity Agent Workflow](../operations/unity-agent-workflow.md)。
- 当前人工状态见 `.agents/docs/manual-acceptance.md`。

# Verification Guidance

迁移事实必须同时核对冻结来源、receipt/manifest、生成目标、Catalog/UID 与测试。禁止恢复已退役目录作为当前实现旁路，也禁止改写 retirement evidence 来迁就当前工作树。
