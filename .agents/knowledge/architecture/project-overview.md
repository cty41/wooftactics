---
type: Project Architecture
resource: https://github.com/cty41/tactics
title: Tactics Project Overview
description: Tactics 的 Godot 产品主线、纯 .NET 分层、运行时和主要游戏系统总入口。
tags: [architecture, godot, agent-first]
timestamp: "2026-08-28T11:48:06+08:00"
status: active
catalog_scope: project-architecture
repo_paths:
  - README.md
  - AGENTS.md
  - .agents/ARCHITECTURE.md
  - Tactics.Godot.slnx
  - src/Tactics.Core
  - src/Tactics.Application
  - godot/src/Tactics.Godot.Adapter
  - godot/addons/tactics_tooling
  - godot/export_presets.cfg
  - godot/project.godot
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:4169e117c64f8fc886a15d3bdb8b8e6dc4d21b661846377947a1015f78c799a6
---

# Summary

Tactics 是 Agent 优先维护并准备公开发布的 Godot 4.7 C# 战棋项目。根 `README.md` 面向访问仓库的玩家与开发者介绍 Pure Run、环境、运行、验证、许可和两档 Windows 构建入口；本页继续负责架构综合。远程 `main` 是产品与治理权威，运行时由纯 .NET Core/Application、Godot Adapter 和唯一 `godot/project.godot` 组成。公开根采用 Apache-2.0 代码许可、逐文件登记的 CC BY 4.0 项目资产与独立商标边界；完整 Unity 历史仅保存在私有归档，Frozen Oracle、Golden、迁移 receipt 和 OKF 作为公开历史/测试证据。当前设计保存在 `.agents/docs/`，仍需执行的活跃计划保存在 `.agents/plans/`，当前行为由代码、Resource 和测试证明。

# Runtime Foundation

- `Tactics.Core` 与 `Tactics.Application` 是纯 .NET 9；Godot Node、Resource、Scene、文件系统和 UI 只进入 Adapter/Editor 层。
- 最终内容由 Godot Resource/PackedScene 与轻量 Catalog 驱动；迁移 DTO、Unity GUID 和历史 receipt 不进入运行时。
- Pure Run 使用确定性 Battle/Run 状态、版本化 Save 与单一 `Main.tscn`；开始营地现在由 engine-neutral 10×10 地图模板和 Godot Resource 适配层驱动，全部候选按声明式槽位实例化为真实 `GodotUnitActor`，选三人的业务事务仍由 Application/Core 裁决。下一版 Save 合同只保留节点级语义、领队和遭遇 checkpoint，不持久化格位或相机表现；新增的魔剑士非大师内容使用魅力主属性、腐化/冥想与附身控制合同；
  `battle-layout.pure-run.split-flank` 将原本只在运行时代码中补建的布局闭合为正式 Resource。
- Battle HUD 由 Godot Adapter 将 Application Snapshot 投影为当前行动者状态卡和鼠标 Hover 浮层；头像来自 typed Unit Resource，HP/MP/腐化只读展示，不参与战斗裁决或输入合法性。
- `Tools/godot/Verify-GodotProject.ps1` 是本地主线统一门禁；Windows RC 使用只读 staging、包审计和双 renderer EXE smoke。
- `Tools/public-release` 固定公开文件策略、资产来源哈希、依赖清单与单 root 候选重建；运行时不依赖这些审计工具。
- 固定的 godot-ai v3.1.2 源码是公开可审计的 MIT Editor-only 依赖，由统一入口按 worktree 隔离；导出与 Windows 运行时包继续排除该插件。
- Agent 默认在用户指定的 worktree 中完成审计和修复；新建、删除或切换 worktree 必须有活跃计划或用户明确授权。
- Godot 只使用 `godot/project.godot`；未知 Godot 行为先研究与本地复现，详细路由见 [Godot Agent Workflow](../operations/godot-agent-workflow.md)。
- Agent 默认禁止 Computer Use、窗口激活和真实鼠标键盘等前台交互；实现、截图、视觉 QA、测试或连接恢复不构成例外授权。后台验证不足时停止为人工验证待办，完整规则由 [Godot Agent Workflow](../operations/godot-agent-workflow.md) 导航。

# System Map

## Content Authoring

`Tactics Tooling` Main Screen 是当前 Godot 内容作者入口。纯 .NET
`src/Tactics.Application/Authoring` 定义规范化 Document、SHA-256 revision、typed ChangeSet、引用快照、沙盒
Session 与批量事务协调器；Godot Adapter 负责 Catalog/UID、EditorUndoRedo、SubViewport 和 ResourceSaver。
Map、Event、Treasure、Encounter/Layout、AI、Skill 与 Godot-native Presentation Profile 已接入同一
draft→validate→apply/revert 边界，QA 页显示作者 revision 和正反向引用审计。
Main Screen 的最外层由无 C# 依赖的 GDScript Bootstrap 承载：它用构造时的启动参数回答 Godot 早于 `_enter_tree()` 的 Main Screen 注册查询；只有受支持的开发入口完成 Build 并传入启动契约后才实例化 C# controller，直接打开项目时保持 Tooling fail-closed 并显示恢复提示。整个 `addons/tactics_tooling` 由 export preset 和包审计排除在运行时 PCK 外。Main Screen 与本机 authoring bridge 在 assembly reload 后先以 untyped probe 验证所有立即使用的 C# Resource；
匹配脚本但尚未恢复 typed 实例时延迟重试，真实脚本/schema 错误则 fail-closed。Presentation Preview 只在 Unit/Profile
全部 typed ready 后原子建立临时 actor，并在停止、切页或退出时清理。

开发期 `Tools/tactics-authoring-mcp` 通过唯一 canonical Editor 的本机命名管道 bridge 暴露六个作者工具；
project root、session token、reload state 与单 session 数量不匹配时 fail-closed。Server 不被产品 solution 或
Godot ExportRelease 引用，bridge 源仅在 `TOOLS` 条件下编译。能力与未完成人工闸门见
`.agents/docs/godot-content-workbench-capability-matrix.md`。

- [SkillGraph](../systems/skill-graph.md)负责技能资产和解释执行。
- [Monster AI](../systems/monster-ai.md)生成、过滤、评分并执行战斗意图。
- [Battle System](../systems/battle.md)承接棋盘、回合、单位、结算和战斗反馈。
- [Roguelike Run](../systems/roguelike-run.md)组织地图、节点、冒险状态和 run 内成长。
- [Godot Agent Workflow](../operations/godot-agent-workflow.md)定义 Agent 修改和验证项目的安全路径；[Archived Unity Agent Workflow](../operations/unity-agent-workflow.md)仅保留历史来源。
- [Project Documentation](../operations/project-documentation.md)定义设计、活跃计划、统一缺口和历史清理的生命周期。
- [OKF Maintenance](../operations/okf-maintenance.md)将实现和文档变更反向映射到需要更新的知识 scope。

# Citations

[1] [Tactics AGENTS.md](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/AGENTS.md)
[2] [Tactics architecture overview](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/ARCHITECTURE.md)
