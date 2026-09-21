---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/src/Tactics.Core/Runs
title: Roguelike Run
description: Godot Pure Run 的七层路线、节点事务、队伍成长、存档和终局主链。
tags: [gameplay, roguelike, map, progression, godot]
timestamp: "2026-09-21T10:55:43+08:00"
status: active
catalog_scope: roguelike-run
repo_paths:
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - src/Tactics.Core/Runs
  - src/Tactics.Application/Runs
  - src/Tactics.Core.Tests/RunAdventureTransitionServiceTests.cs
  - src/Tactics.Application.Tests/RunSaveDocumentV9Tests.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotAdventureBoardView.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs
  - Tests/gameplay-specs/godot
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:ec378fdba989acd77c9c42d13b873f443ca84ca13ef83f4d837685d6f8c305cc
---

# Current State

Pure Run 使用七层只前进路线、稳定节点 ID 和持久化 `RunAdventureState`。每个已结算节点的 Tile 场景只展示当前节点的直接后继出口；点击已解锁出口后领队按最短稳定路径自动接近并在提交移动后选择目标，不存在开局两组路线预提交。Application transition 统一移动、即时出口、节点事务、战斗请求/结算、成长、Inventory 与终局；成功事务使用稳定 key 防止重入时重复扣款、发奖或结算。新地图合同增加 Planning、TacticalPreview、Current、Completed 情报状态，并用可选持久化字段保持旧 Save 编码稳定。

Godot Adventure Board 使用正式 Tile 投影和生产输入链呈现出口、队伍位置与节点状态。所有 Active Board（含 Start Camp）使用真实 `GodotUnitActor` 与逐格 Tween；每段朝向按真实等距投影后的屏幕运动方向解析：`+X→右上(UL镜像)`、`-X→左下(DR镜像)`、`+Y→左上(UL)`、`-Y→右下(DR)`。移动期间仅合法空格可替换目标，当前段先精确落在格中心再重算，角色/对象/出口和页面导航均拒绝；手动retarget或resize中断会清除待触发出口。普通 Adventure 只在最终 settle 后提交一次 `MoveLeader`；Viewport resize中断会先把最后格中心同步到process-local session再重排，Start Camp 保持非持久化；V11 丢弃格位后由带 RunId 隔离的 `AdventureExplorationSession` 从入口槽恢复 process-local 位置。冻结规则见 `.agents/docs/adventure-movement-contract.md`。共享 `GodotIsometricTileMapSurface` 提供 10×10、96×48 的 Terrain/Decoration/Mask/Overlay TileMapLayer 与确定性拾取；Active Adventure Board按实际Tile边界等比填充近全屏安全区并精确居中，标题/Run摘要/Leader/Inventory压缩覆盖菱形四角空白，Actor与指针逆变换共享同一fit。应用启动不再经过 Home：可恢复状态直接 Continue，无有效存档创建进程内 Setup 草稿并进入开始营地。营地点击顺序、队伍和部分起始技能只存在于 `PureRunSessionService` 实例；最后一个必需技能完成时才基于起始 revision 一次 CAS 保存完整 Active Run。Replay 前的未完成草稿不会恢复，历史 `PendingRunSetup` 在 V11 读取时丢弃，替换草稿也不会提前覆盖旧 Active Run。`GodotPlayableRunMain`持有跨页唯一Pause Shell，在GUI控件之前的全局`_Input`阶段捕获Esc；每次打开都以当前context重建Root、恢复GUI命中顺序并只取得一次暂停ownership，Options/Back不重入Open，Close清理临时子树。Progression/Inventory临时状态由RunId隔离，Page status不再引用旧页；页面只声明Setup、Adventure或Battle context。`GodotStartCampView` 持有实际 GUI InputSurface，以完整 Transform 把 Control-local 输入映射到地图并优先解析不透明 Sprite，再使用边界容错；每次左键在 Debug Output 与测试 Probe 留下有界结构化诊断，三人满员才解锁出口。Start 地图集使用 Current/Overview 双状态：Current 完整框住真实 Start 并允许选人/移动；Overview 采用固定可读缩放，仅允许 Preview、右键与键盘平移，不提供滚轮缩放。`M` 全局切换双态，`F/Home` 返回领队。每个非战斗节点的标题、类型与层数锚定在自身10×10 TileMap上方中央；模式提示与固定UI独立于地图变换。确定性Battle、Elite、Boss节点选择后直接建立encounter，不再显示预战斗TileMap或encounter marker；事件对象产生的条件战斗保持原交互。战斗棋盘共享TileMap表面仍由后续切片处理。

当前生产存档采用 V11、revision、payload hash、temp 重读、backup 回退与损坏文件隔离；兼容读取保留节点、领队、对象结果、发现、路线与遭遇 checkpoint，丢弃旧 ActorCells 和历史 Setup 草稿，并明确不保存相机、Tooltip、弹窗、选人/起始技能草稿或逐行动战斗状态。V11 writer 对 `PendingRunSetup` fail-closed；Continue 从模板槽重建 Actor 与相机，PendingBattle 以已保存 Encounter、Seed 和入口 checkpoint 重开。

当前可选角色、战斗、事件、休息、商店、宝箱、Elite、Boss、成长和终局均由代码、typed Resource 与测试共同定义。自动旅程验证逻辑和持久化边界；地图可读性、操作手感和视觉反馈由人工验收账本负责。

# Relationships

- 战斗节点使用 [Battle System](battle.md)。
- 技能成长与消耗品使用 [SkillGraph](skill-graph.md)。
- 固定 Seed 旅程由 [Gameplay Test Framework](gameplay-test-framework.md)验证。

# Verification Guidance

实现判断应核对 Run transition、存档 schema、Godot runtime、Resource/Catalog 与测试。必须覆盖事务重载、幂等、损坏回退和终局消费；截图不能替代玩家流验收。
