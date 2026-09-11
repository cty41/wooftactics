---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/src/Tactics.Core/Runs
title: Roguelike Run
description: Godot Pure Run 的七层路线、节点事务、队伍成长、存档和终局主链。
tags: [gameplay, roguelike, map, progression, godot]
timestamp: "2026-09-11T11:08:42+08:00"
status: active
catalog_scope: roguelike-run
repo_paths:
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - .agents/docs/poet-class-design.md
  - src/Tactics.Core/Runs
  - src/Tactics.Application/Runs
  - src/Tactics.Core.Tests/RunAdventureTransitionServiceTests.cs
  - src/Tactics.Application.Tests/RunSaveDocumentV9Tests.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotAdventureBoardView.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs
  - Tests/gameplay-specs/godot
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:350e0ff06d4eb863b0bf5ea28b90b9ebf4221fcdca68564755b9fcefd19505f1
---

# Current State

Adventure Board 测试目标由 Board 本地格中心转换为 Canvas 全局坐标后注入 Viewport，确保窗口缩放和非单位 CanvasTransform 下仍命中同一生产输入格。

Pure Run 使用七层只前进路线、稳定节点 ID 和持久化 `RunAdventureState`。每个节点的 Tile 场景只展示当前节点的直接后继出口；领队移动到相邻格并点击后立即选择目标，不存在开局两组路线预提交。Application transition 统一移动、即时出口、节点事务、战斗请求/结算、成长、Inventory 与终局；成功事务使用稳定 key 防止重入时重复扣款、发奖或结算。

Godot Adventure Board 使用正式 Tile 投影和生产输入链呈现出口、队伍位置与节点状态；Rogue Map 仅作只读总览。战斗节点先进入 Tile 场景，胜利后恢复同一 resolved 场景再开放出口。存档采用 V10、revision、hash、temp 重读与 backup 回退；V9 活跃 Run/Pending Setup 要求新局，Terminal Summary 保留，损坏证据隔离保存且不静默覆盖。

当前候选池包含法师、死灵法师、亚马逊、魔剑士与诗人五名角色，开局仍必须选择恰好三人；SeededStartingSkill 可为魔剑士与诗人从各自三个候选技能确定性选择。战斗、事件、休息、商店、宝箱、Elite、Boss、成长和终局均由代码、typed Resource 与测试共同定义。自动旅程验证逻辑和持久化边界；地图可读性、操作手感和视觉反馈由人工验收账本负责。

成长阶段保存的六维是永久属性，装备只在战斗/面板有效属性投影中叠加。亚马逊以永久六维总增量 2/4 判定高级和大师候选；装备与临时状态不会解锁技能，但会影响实际技能数值和二级属性。当前仍禁止主动洗点、退款和自由重分配，永久惩罚不得把单项属性降到 1 以下。

# Relationships

- 战斗节点使用 [Battle System](battle.md)。
- 技能成长与消耗品使用 [SkillGraph](skill-graph.md)。
- 固定 Seed 旅程由 [Gameplay Test Framework](gameplay-test-framework.md)验证。

# Verification Guidance

实现判断应核对 Run transition、存档 schema、Godot runtime、Resource/Catalog 与测试。必须覆盖事务重载、幂等、损坏回退和终局消费；截图不能替代玩家流验收。
