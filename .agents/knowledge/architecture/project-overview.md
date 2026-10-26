---
type: Project Architecture
resource: https://github.com/cty41/tactics
title: Tactics Project Overview
description: Tactics 的 Godot 产品主线、纯 .NET 分层、运行时和主要游戏系统总入口。
tags: [architecture, godot, agent-first]
timestamp: "2026-10-26T10:33:23+08:00"
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
source_fingerprint: sha256:9cfcafe01c578089d5a18053b40368a81763113106f4d426441596ae6f0fea9f
---

# Summary

Tactics 是 Agent 优先维护并准备公开发布的 Godot 4.7 C# 战棋项目。根 `README.md` 面向访问仓库的玩家与开发者介绍 Pure Run、环境、运行、验证、许可和两档 Windows 构建入口；本页继续负责架构综合。远程 `main` 是产品与治理权威，运行时由纯 .NET Core/Application、Godot Adapter 和唯一 `godot/project.godot` 组成。公开根采用 Apache-2.0 代码许可、逐文件登记的 CC BY 4.0 项目资产与独立商标边界；完整 Unity 历史仅保存在私有归档，Frozen Oracle、Golden、迁移 receipt 和 OKF 作为公开历史/测试证据。当前设计保存在 `.agents/docs/`，仍需执行的活跃计划保存在 `.agents/plans/`，当前行为由代码、Resource 和测试证明。

# Runtime Foundation

Run/Act/Node 改造正在按 `.agents/plans/roguelike-act-node-scene-architecture.md` 实施（2026-10-21 起按缺口审查重排为“先状态骨架、后页面”，S1 Core节点状态机已就绪但未接调用方）：Core提供Act/模板映射校验，Setup原子保存首战checkpoint；Main准备期创建独立于页面的16节点Host，营地借用正式地图，成功提交保留实例、取消和终局隔离生命周期。22个正式模板变体已生成；Demo仅一个Act。战斗棋盘借用已由Pending身份投影接入Main的普通、Elite、Boss及证据完整且Host模板匹配的Pending事件入口：只激活对应宿主节点，用世界仿射复用旧Actor/VFX局部坐标，且在建战斗前校验正式模板与runtime布局。V12已具备显式身份codec与Resume透传，并有Pending只读投影与保修订的内存归一化；未切生产Store；完整旧Pending已支持实际变体优先于默认seed，资源目录可按恢复蓝图加载，Main冷Pending Host已按已证明的当前节点实际变体准备，Ready/Resolved和护送Offer/Travel仍待解决。生产启动防线已校验请求队伍/随机修订/身份，恢复与准备回调统一采用Application零写修复投影。事件探索到战斗同实例、Adventure控制器、战后状态和完整V12恢复仍未完成，不能把营地常驻切片视为整架构已验收。

- `Tactics.Core` 与 `Tactics.Application` 是纯 .NET 9；Godot Node、Resource、Scene、文件系统和 UI 只进入 Adapter/Editor 层。
- 最终内容由 Godot Resource/PackedScene 与轻量 Catalog 驱动；迁移 DTO、Unity GUID 和历史 receipt 不进入运行时。
- Pure Run 使用确定性 Battle/Run 状态、版本化 Save 与单一 `Main.tscn`；开始营地及全部 Active Adventure Board 由 engine-neutral 10×10 地图规则和 Godot Resource 适配层驱动，角色实例化为真实 `GodotUnitActor`。Core 共享确定性寻路机制但保留 Battle/Adventure 独立政策；Adapter 共享单格 Tween 构造，Adventure负责等距屏幕朝向、格中心改道、交互锁、出口自动接近和最终单次提交；确定性Battle/Elite/Boss节点选择后直接进入encounter，不创建预战斗TileMap。`GodotPlayableRunMain`统一拥有跨页Pause Shell和Esc输入路由，Root/Options内容与Battle暂停权属于单次open session；Run/Page UI状态按RunId和页面生命周期隔离，页面只声明暂停context。选人与起始技能是 Application 实例内草稿，最后一个必需技能才一次 CAS 保存完整 Active Run；V11 只保留节点级语义、领队和遭遇 checkpoint，不持久化 Setup 草稿、格位或相机表现，带 RunId 隔离的 process-local exploration session 从模板入口恢复格位。新增的魔剑士非大师内容使用魅力主属性、腐化/冥想与附身控制合同；
  `battle-layout.pure-run.split-flank` 将原本只在运行时代码中补建的布局闭合为正式 Resource。
- Battle HUD 由 Godot Adapter 将 Application Snapshot 投影为当前行动者状态卡和鼠标 Hover 浮层；头像来自 typed Unit Resource，HP/MP/腐化只读展示，不参与战斗裁决或输入合法性。
- `Tools/godot/Verify-GodotProject.ps1` 是本地主线统一产品门禁；Windows RC 使用只读 staging、包审计和双 renderer EXE smoke。
- `Tools/public-release/Verify-PublicRelease.ps1` 独立验证公开文件策略、资产来源哈希、依赖清单与第三方声明，并由单 root 候选重建入口在源树和候选树各调用一次；运行时不依赖这些审计工具。
- 固定的 godot-ai v4.3.1 源码是公开可审计的 MIT Editor-only 依赖；manifest 是 tag、commit、vendor tree 与 license digest 的单一真相源，显式 refresh 只生成待 review 的候选 diff。导出与 Windows 运行时包继续排除该插件。
- Agent 默认在当前 workspace checkout 开发，不另开 worktree；新建 worktree 仅限计划要求、用户明确要求或当前树无法安全保留。步骤见 `tactics-git-workspace`。
- Godot 只使用 `godot/project.godot`；未知 Godot 行为先研究与本地复现，详细路由见 [Godot Agent Workflow](../operations/godot-agent-workflow.md)。
- Agent 默认禁止 Computer Use、窗口激活和真实鼠标键盘等前台交互；实现、截图、视觉 QA、测试或连接恢复不构成例外授权。后台验证不足时停止为人工验证待办，完整规则由 [Godot Agent Workflow](../operations/godot-agent-workflow.md) 导航。

# System Map

## Content Authoring

`Tactics Tooling` Main Screen 是当前 Godot 内容作者入口。纯 .NET
`src/Tactics.Application/Authoring` 定义规范化 Document、SHA-256 revision、typed ChangeSet、引用快照、沙盒
Session 与批量事务协调器；Godot Adapter 负责 Catalog/UID、EditorUndoRedo、SubViewport 和 ResourceSaver。
Map、Event、Treasure、Encounter/Layout、AI、Skill 与 Godot-native Presentation Profile 已接入同一
draft→validate→apply/revert 边界，QA 页显示作者 revision 和正反向引用审计。
Main Screen 的最外层由无 C# 依赖的 GDScript Bootstrap 承载，并始终回答 Godot 早于 `_enter_tree()` 的 Main Screen 注册查询。人工从Project Manager直接打开时，有assembly即实例化C# controller；首次未编译则显示非错误构建页，经用户确认调用固定helper异步Build并优先在当前Editor reload。Prepared参数不再决定Tooling能否加载；可写authoring bridge还要求当前PID/path/worktree/mode及进程启动时间均匹配dev-session，公开参数不能单独授权。Direct Human只保留Workbench UI。整个 `addons/tactics_tooling` 由 export preset 和包审计排除在运行时 PCK 外。Main Screen 与本机 authoring bridge 在 assembly reload 后先以 untyped probe 验证所有立即使用的 C# Resource；
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
