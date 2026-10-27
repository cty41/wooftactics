---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot migration provenance
description: 已完成迁移的冻结来源、Godot ownership、生成批次与验证边界。
tags: [migration, godot, provenance, testing]
timestamp: "2026-10-27T10:47:44+08:00"
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
source_fingerprint: sha256:f7fe18e3154733d11127366070867d40e1050d600d42208b1afe8e8c82628df7
---

# Current State

Godot 4.7 C# 是唯一产品主线，`godot/project.godot` 是唯一项目。Core/Application 保持引擎无关，Godot Adapter 承担 Node、Resource、文件系统、UI、作者工具和运行时集成。

迁移来源已冻结在最终 Tag、FrozenOracle、Golden、DTO、receipt、ownership ledger 与 retirement manifest 中。它们只用于来源审计、确定性回归和许可证证明，不提供 live 旧编辑器、旧 MCP 或旧资产写入路径。

内容生成只消费已绑定的冻结输入，经 Application typed draft、ResourceSaver/PackedScene、Catalog/UID、target hash、幂等与 rollback 门禁进入 Godot。诗人是 Godot-owned 新内容，不来自 Unity 冻结迁移：`PoetAssetFactory` 通过 ResourceSaver 生成 15 技能、2 状态、诗人单位和分身，canonical Catalog 当前为 185；已批准 Idle、Melee、Cast、Hit 的 DR/UL 与单张 Death 以逐字节副本导入并绑定到诗人 Resource，Ranged 继续回退 Idle，分身继续清空动作和死亡字段。公开 provenance 继承 approved 源的 `cty41 / CC-BY-4.0`，人工质量门禁仍与自动 ownership/生成证据分离。公开根缺少一次性 Unity DTO 时，已提交 Unit Resource 仍通过专用 ResourceSaver 升级入口应用当前属性派生合同并由主线门禁校验。自动验证不能替代视觉、操作、真实 Editor Reload 或干净 Windows 启动验收。

属性命中公式切换后的 `PresentationMiss` 使用固定 RNG state 6 继续验证生产 End Turn 输入产生 committed dodge 与灰色 Miss 数字；checkpoint 哈希和跟踪 plan 与该状态共同受门禁约束。

Adventure/Battle 生产指针目标先从 Control 本地坐标转换为 Canvas 与 Screen 坐标，再由 Viewport 按对应坐标空间注入；定位必须同时命中目标 Control 和精确本地点，PointerPressed 与状态变化继续作为提交证据，避免全屏 surface 的宽泛 Hover 掩盖错误格。

开发工具链把固定的 MIT godot-ai v4.3.1 源码作为 Editor-only vendor 依赖纳入公开源码，并由 manifest tree hash 审计；它不进入游戏 PCK/Windows 包。Editor 通过统一入口完成 production C# Build、worktree 用户数据隔离、项目级 Codex 配置和会话租约，避免测试宿主程序集或另一个 worktree 的窗口被误认成当前项目。

# Relationships

- 当前开发与验证入口见 [Godot Agent Workflow](../operations/godot-agent-workflow.md)。
- 历史 Agent 工具退役索引见 [Archived Unity Agent Workflow](../operations/unity-agent-workflow.md)。
- 当前人工状态见 `.agents/docs/manual-acceptance.md`。

# Verification Guidance

迁移事实必须同时核对冻结来源、receipt/manifest、生成目标、Catalog/UID 与测试。禁止恢复已退役目录作为当前实现旁路，也禁止改写 retirement evidence 来迁就当前工作树。
