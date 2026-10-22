---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/src/Tactics.Core/Runs
title: Roguelike Run
description: Godot Pure Run 的七层路线、节点事务、队伍成长、存档和终局主链。
tags: [gameplay, roguelike, map, progression, godot]
timestamp: "2026-10-22T16:00:43+08:00"
status: active
catalog_scope: roguelike-run
repo_paths:
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - src/Tactics.Core/Runs
  - src/Tactics.Application/Runs
  - src/Tactics.Core.Tests/RunAdventureTransitionServiceTests.cs
  - src/Tactics.Application.Tests/RunSaveDocumentV9Tests.cs
  - src/Tactics.Application.Tests/RunSaveDocumentV11Tests.cs
  - src/Tactics.Application.Tests/RunSaveDocumentV12Tests.cs
  - .agents/docs/roguelike-act-node-scene-contract.md
  - .agents/docs/roguelike-act-tilemap-gap-review.md
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotAdventureBoardView.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs
  - Tests/gameplay-specs/godot
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:e84efc7d3a1aab9a0748ab7d3177615888e9ec6b60ed57ffec5372ec54ad07b9
---

# Current State

单 Act 常驻节点架构正在按 `.agents/plans/roguelike-act-node-scene-architecture.md` 逐切片实施，`.agents/docs/roguelike-act-node-scene-contract.md` 保持 approved_target。Core 已加入显式 Act/节点模板映射和可达性验证；Main准备期构建16节点常驻宿主，营地借用已有地图，战斗棋盘已将受租约约束的Surface借用接入普通、Elite、Boss及证据完整且模板匹配的Pending事件生产入口；binding/context匹配的事件探索也借正式Surface并可与Pending战斗保持同一实例，其余Adventure仍未接此模式；不能按重复Encounter反查节点。现有RunNodeSceneIdentity及V12编码边界保留当前场景/checkpoint身份和RNG，旧V10/V11仅标记待迁移，schema8/9丢弃旧Run时保留重开标志；44项新增Application回归通过。Pending身份投影与零写入归一化新增89项回归通过；已提交checkpoint可消解legacy Initial，所有revision/RNG不变。完整旧Pending实际事件现可在有限22变体中恢复明确节点绑定，默认assignment保持不变；V12往返后仍使用该实际变体。缺marker、事务冲突及不合法护送证据仍保留原数据拒绝，不能补造触发。Main启动已接请求防线：使用同一Application修复投影校验队伍/随机修订/身份，Prepared和Pending路由也不重发raw队伍；新增33项Application和2项UI通过（Application总391/UI总49），旧队伍恢复零写。生产Store仍V11；实际Pending变体已用于冷Host当前节点呈现计划和战斗入口；结构完整的Traveling护送也只读驱动目的节点Lost探索呈现，生产Store仍不写回V12，Ready/Resolved迁移、Offer/Travel和原子入口尚待接通。Setup 最后技能现在一次 CAS 保存首战 PendingBattle/checkpoint，成功才清草稿并发遭遇请求；FullRunTransition 保存失败不再发遭遇请求。2026-10-21 缺口审查（`.agents/docs/roguelike-act-tilemap-gap-review.md`）确认节点状态机与控制器层此前缺失，按“先骨架、后页面”重排为S1–S6。S1已新增Core `PureRunActState`（节点阶段Unentered/Exploring/InCombat/Resolved/Failed与已离开）、`PureRunNodeSceneTransitionService`、`PureRunLegacyActStateProjector`（唯一允许解析旧Board字符串，矛盾即拒绝；结算已存但Board未`.resolved`的历史中间态也拒绝；开发期 `ResumeRun` 仅遇到 `act_state.inconsistent_resolution` 即清档并返回 `run.reset_stale_save`，主页提示存档过旧）与`PureRunPhaseConsistency`；尚未接调用方、未进入`PureRunState`或存档，`.resolved`/Phase仍是事实权威，S2与V12生产切换一起接管。旧 StartNewRun 和旧 InitialExploration 存档路径尚待迁移，不能宣称所有旧路径已无占位图；V12、同图战斗与常驻性能均未完成。

Pure Run 使用七层只前进路线、稳定节点 ID 和持久化 `RunAdventureState`。每个已结算节点的 Tile 场景只展示当前节点的直接后继出口；点击已解锁出口后领队按最短稳定路径自动接近并在提交移动后选择目标，不存在开局两组路线预提交。Application transition 统一移动、即时出口、节点事务、战斗请求/结算、成长、Inventory 与终局；成功事务使用稳定 key 防止重入时重复扣款、发奖或结算。新地图合同增加 Planning、TacticalPreview、Current、Completed 情报状态，并用可选持久化字段保持旧 Save 编码稳定。

Godot Adventure Board 使用正式 Tile 投影和生产输入链呈现出口、队伍位置与节点状态。所有 Active Board（含 Start Camp）使用真实 `GodotUnitActor` 与逐格 Tween；每段朝向按真实等距投影后的屏幕运动方向解析：`+X→右上(UL镜像)`、`-X→左下(DR镜像)`、`+Y→左上(UL)`、`-Y→右下(DR)`。移动期间仅合法空格可替换目标，当前段先精确落在格中心再重算，角色/对象/出口和页面导航均拒绝；手动retarget或resize中断会清除待触发出口。普通 Adventure 只在最终 settle 后提交一次 `MoveLeader`；Viewport resize中断会先把最后格中心同步到process-local session再重排，Start Camp 保持非持久化；V11 丢弃格位后由带 RunId 隔离的 `AdventureExplorationSession` 从入口槽恢复 process-local 位置。冻结规则见 `.agents/docs/adventure-movement-contract.md`。共享 `GodotIsometricTileMapSurface` 提供 10×10、96×48 的 Terrain/Decoration/Mask/Overlay TileMapLayer 与确定性拾取；Active Adventure Board按实际Tile边界等比填充近全屏安全区并精确居中，标题/Run摘要/Leader/Inventory压缩覆盖菱形四角空白，Actor与指针逆变换共享同一fit。应用启动不再经过 Home：可恢复状态直接 Continue，无有效存档创建进程内 Setup 草稿并进入开始营地。营地点击顺序、队伍和部分起始技能只存在于 `PureRunSessionService` 实例；最后一个必需技能完成时才基于起始 revision 一次 CAS 保存完整 Active Run。Replay 前的未完成草稿不会恢复，历史 `PendingRunSetup` 在 V11 读取时丢弃，替换草稿也不会提前覆盖旧 Active Run。`GodotPlayableRunMain`持有跨页唯一Pause Shell，在GUI控件之前的全局`_Input`阶段捕获Esc；每次打开都以当前context重建Root、恢复GUI命中顺序并只取得一次暂停ownership，Options/Back不重入Open，Close清理临时子树。Progression/Inventory临时状态由RunId隔离，Page status不再引用旧页；页面只声明Setup、Adventure或Battle context。`GodotStartCampView` 持有实际 GUI InputSurface，以完整 Transform 把 Control-local 输入映射到地图并优先解析不透明 Sprite，再使用边界容错；每次左键在 Debug Output 与测试 Probe 留下有界结构化诊断，三人满员才解锁出口。Start 地图集使用 Current/Overview 双状态：Current 完整框住真实 Start 并允许选人/移动；Overview 采用固定可读缩放，仅允许 Preview、右键与键盘平移，不提供滚轮缩放。`M` 全局切换双态，`F/Home` 返回领队。每个非战斗节点的标题、类型与层数锚定在自身10×10 TileMap上方中央；模式提示与固定UI独立于地图变换。确定性Battle、Elite、Boss节点选择后直接建立encounter，不再显示预战斗TileMap或encounter marker；事件对象产生的条件战斗保持原交互。战斗棋盘共享TileMap表面仍由后续切片处理。

`GodotActWorldHost` 与 `GodotActLoadCoordinator` 已接Main准备期（唯一租约、全图交叉校验、分帧/取消），Host在_page外。`PureRunActBlueprint`复用已有seed规则，当前Run选择16节点；ResourceSaver已生成22个正式变体，local catalog含legacy共24项，ledger保持manual_qa_pending并校验target SHA、磁盘UID和重复生成一致性。模板支持终局零出口、探索槽可达、真实布局/事件及N2浅水atlas5。营地视图只借用已有MapInstance/Surface，切技能页/首战入口不重建地图；只有明确成功的Setup提交可以收养草稿Host，取消同seed草稿/终局释放旧Host，迟到Prepared与陈旧遭遇请求被拒绝。UI49项通过；普通/Elite/Boss及匹配的Pending事件战使用宿主Surface；Lost cold Ready探索已由持久Traveling证据接通，且成功保存后的warm战后页回到同Surface，但Offer/Travel生产入口、变体不匹配warm护送、冷Resolved恢复、历史事件结算中间态迁移与V12仍未闭环（事件胜利结算现已由`ApplyEventBattleResult`单次保存；普通/L4/L5/L6胜利仍为结算+`ResolveAdventureBoard`两次CAS）；不得将营地常驻与资源生成写成整计划完成。4.7.1本机probe表明GetResourceUid有旧正/负缓存，生成器只读检查ResourceSaver头部UID，不机械修改资源文本。

当前生产存档采用 V11、revision、payload hash、temp 重读、backup 回退与损坏文件隔离；兼容读取保留节点、领队、对象结果、发现、路线与遭遇 checkpoint，丢弃旧 ActorCells 和历史 Setup 草稿，并明确不保存相机、Tooltip、弹窗、选人/起始技能草稿或逐行动战斗状态。V11 writer 对 `PendingRunSetup` fail-closed；Continue 从模板槽重建 Actor 与相机，PendingBattle 以已保存 Encounter、Seed 和入口 checkpoint 重开。

当前可选角色、战斗、事件、休息、商店、宝箱、Elite、Boss、成长和终局均由代码、typed Resource 与测试共同定义。自动旅程验证逻辑和持久化边界；地图可读性、操作手感和视觉反馈由人工验收账本负责。

# Relationships

- 当前Act改造目标与未完成边界见 `.agents/docs/roguelike-act-node-scene-contract.md`（repo_paths中的权威来源）。

- 战斗节点使用 [Battle System](battle.md)。
- 技能成长与消耗品使用 [SkillGraph](skill-graph.md)。
- 固定 Seed 旅程由 [Gameplay Test Framework](gameplay-test-framework.md)验证。

# Verification Guidance

实现判断应核对 Run transition、存档 schema、Godot runtime、Resource/Catalog 与测试。必须覆盖事务重载、幂等、损坏回退和终局消费；截图不能替代玩家流验收。
