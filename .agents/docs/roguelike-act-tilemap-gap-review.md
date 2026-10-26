# 单 Act 全 TileMap 化：缺口与实现错误审查（2026-10-19）

- 性质：只读 code review 结论，供讨论；不修改实现、不代替合同与计划。
- 基线：HEAD `fa57406f` + 当前 dirty worktree（82 项未提交改动）。
- 权威定义：[场景合同](roguelike-act-node-scene-contract.md)、[架构计划](../plans/roguelike-act-node-scene-architecture.md)“已确认决策”。
- 证据等级：**确定**＝读代码可证；**推测**＝风险，未复现。行号以审查时 dirty 文件为准。

## 一句话结论

地图宿主层（全 Act 分帧构建、唯一实例、lease/generation）已基本落地；**业务状态机层与节点控制器层尚未开工**，Main 仍在页面级拼流程并为多个页面自造占位图。继续逐页迁移只是在旧结构上打补丁，应先讨论是否先补状态机/控制器骨架。

## 定义条款对照

| # | 定义条款 | 状态 | 关键证据 |
|---|---|---|---|
| 1 | 开局分帧构建当前 Act 全部正式 TileMap；可取消；缺资源失败不 fallback | 已完成 | `GodotActLoadCoordinator.cs:35–115`；`GodotActContentCatalog.cs:20–42`；`GodotPlayableRunMain.ActWorld.cs:120–173` |
| 2 | Act 内切换不读模板、不重建底图 | 部分 | 正式路径只切模式（`GodotActWorldHost.cs:92–104`）；但 Rest/Store/Treasure、普通战后、Initial、事件不匹配仍 `new GodotAdventureBoardView` 写格（`GodotAdventureBoardView.cs:122–131`） |
| 3 | 每节点唯一实例；Preview/探索/战斗/战后同实例 | 部分 | 注册表唯一（`GodotActWorldHost.cs:17–61`）；营地、普通/Elite/Boss 战斗、匹配事件已借 Surface；普通战后与非战斗节点未借 |
| 4 | 唯一 Active lease + generation fencing | 已完成（宿主层） | `GodotActWorldHost.cs:92–116`；消费侧 `GodotIsometricBattleBoard.cs:25–49`、`GodotAdventureBoardView.cs:95–106` |
| 5 | 非当前节点真正停用（Process/输入/碰撞/Tween），Visible≠停用 | 部分 | `GodotAdventureMapInstance.ApplyMode`（:63–68）只设 ProcessMode 与遮罩；无碰撞/Tween/控制器停用；Main 另用 Visible 藏兄弟图 |
| 6 | 普通/Elite/Boss 进入即战斗 | 部分 | 生产已直达（`RouteRunState` :2074–2080）；`ShowInitialAdventure` 占位开局仍可达（:738–773, :2073）；`StartNewRun` 仍写 `InitialExploration`（`PureRunSessionService.cs:83–84`） |
| 7 | 事件原图探索→原图战斗 | 部分 | 匹配模板时成立（:1508–1598）；不匹配静默退回旧 perimeter 棋盘（:1547–1557）；护送 Offer/Travel 无生产入口 |
| 8 | 非终胜一次提交后原图战后探索、出口离开 | 部分/错误 | 事件战后可复用 Surface；普通战后 `ShowResolvedAdventureNode` 新建占位图（:1998–2024）；普通/L4/L5/L6 结算 **双 CAS**（`CompleteBattle` :1159–1221：结算 + `ResolveAdventureBoard`） |
| 9 | 离开节点不可回访、保留只读残局 | 缺失 | 无残局状态层或只读宿主模式，离开即销毁页面 |
| 10 | Overview 观察真实宿主 | 部分 | 营地 atlas 用真实宿主（`GodotStartCampView.cs:280–348`）；`ShowRunMap` 仍 `GodotRogueMapView`（:1435–1452） |
| 11 | Rest/Store/Treasure 正式化 | 缺失 | `ShowAdventureNodeEntry` 硬编码占位图（:1674–1714），交互后进独立 UI 页（:2109–2197） |
| 12 | Core Act/Node 状态机（Unentered/Exploring/InCombat/Resolved/Failed + 已离开） | 缺失 | `PureRunActState`、`PureRunNodeSceneState`、`PureRunNodeSceneTransitionService` 均不存在 |
| 13 | 显式节点身份，不从 Board 字符串推断 | 部分/错误 | `RunNodeSceneIdentity` 已有但多数入口不写；`RunAdventureTransitionService.CurrentNodeId`（:102–125）仍解析 `.node.`/`.resolved`，Main 4 处调用（:1528/1623/1707/2000）；`IsExitUnlocked`、`ResolveBoard`、`ResolveEventBattle`、事件幂等判断均依赖 `.resolved` |
| 14 | PureRunPhase 仅兼容，由单一映射器派生 | 缺失 | 无映射器；Session/Settlement/LayerFour/FullRun/FlowProjector 各自改 Phase；Phase、AdventureState、NodeTransaction、SceneIdentity 多权威并行 |
| 15 | Application 单一事务入口（RunId/ActId/NodeId/revision/幂等 key，一次 CAS） | 缺失 | 入口散落 `BeginEncounter`/`Apply*`/`ApplyMutation`；单方法内各一次 Save，但跨方法组合由 Main 串联 |
| 16 | Setup 末技能一次 CAS 直达首战 | 部分 | 一次 Save + checkpoint 已成立（`AdvanceSetup` :439–453）；仍经 `CreateInitial` 语义且**不写 SceneIdentity** → 将来 V12 Encode 会拒绝 |
| 17 | 探索/战斗控制器、Atlas、Camera 承担职责 | 缺失 | 四个类均不存在；相机仿射散在 `ResidentBattle.cs:49–61` 与 `FitBoardToViewport` |
| 18 | Main 只编排不造图；旧 Board/RogueMapView 退役 | 部分 | Main 仍造 Initial/Node/Event/Resolved 占位图；每战 `new GodotIsometricBattleBoard`；非 resident 仍画多边形底图；`GodotRogueMapView` 仍在生产 |
| 19 | V12 保存节点状态并切生产 | 部分 | codec 与 Pending 恢复在 Application；生产 `GodotRunSaveStore.cs:91,147` 仍 V11 |

## 确定的实现错误（相对目标）

1. 普通/L4/L5 非 Boss/L6 Elite 胜利结算固定两次 CAS（结算后再 `ResolveAdventureBoard`）。
2. Setup 首战与多数入口不写 `SceneIdentity`，与 V12 编码前置条件冲突。
3. `StartNewRun` 与 `ShowInitialAdventure` 仍保留被合同禁止的占位开局路径。
4. 事件模板不匹配时静默退回旧棋盘，违反“资源/模板不一致必须失败，不生成 fallback”。
5. 非当前节点停用只做到 ProcessMode，未满足“隐藏≠停用”的完整停用合同。

## 推测风险（需故障注入或运行验证）

- 两次 CAS 之间中断 → 已结算但未 `.resolved`，重启路由依赖 Phase/字符串启发式。
- 营地 atlas 与战斗相机反复改 `host.Transform`，可能互相污染。
- 每个 `CreatePage` 都 `DeactivateCurrent` 抬 generation，漏检 lease 的路径会静默吞事件。
- 冷启动 Lost 护送依赖 Traveling 启发式呈现，易与存档事实漂移。

## 测试证据强度

- RESIDENCY-001：宿主层内部测试；“仅一 Act”无断言。
- SETUP-FIRST-ENCOUNTER-001：Application 单元；“不经占位图”仅弱断言，UI 直达测试不在 verification 清单。
- NODE-SAME-SCENE-001：普通/Elite/Boss 偏路由级生产测试；**战后原图探索仅内部显示调用**（`ResolvedEventSceneReturnsToTheSameFormalSurface` 未经 `CompleteBattle`）；只读残局、终 Boss 摘要在本块无测试。
- NODE-RECOVERY-001：全为 Application 单元，未接生产 Store。

## 文档偏差（待实现切片时一并修正）

- `roguelike-tilemap-continuation-handoff.md:15–16,102–119`、架构计划 :101/:107、`knowledge/systems/roguelike-run.md:34`：仍写事件结算 2–3 次 CAS；实际 `ApplyEventBattleResult` 已单次。真实缺口是冷 Resolved 恢复与旧中间态迁移，以及普通战斗的双 CAS。
- 架构计划第17轮把事件战后同 Surface 写成生产闭环，测试只是内部显示调用，夸大完成度。
- `manual-acceptance.md:304,312–313`：仍描述“最终技能进入 Adventure / 首个 Adventure 点出口”，与直达首战不符。

## 运行观察（godot-ai）

- MCP 核对通过：单会话、Godot 4.7.1、canonical 项目路径、插件 v4.1.0 与 manifest 一致且未改动。
- `project_run` 后游戏未进入 live 即停止，三路日志无错误；疑似 Editor 程序集与 dirty C# 不同步（该 Editor 非 `Open-GodotDev.ps1` 启动）。未获得运行截图与节点树。需按 `godot-editor-lifecycle` 正常关闭并经 `Open-GodotDev.ps1` 重开后复测。

## 供讨论的重新切片方向

依赖关系：控制器与页面迁移都需要“当前节点/阶段”的单一权威，否则每迁一页都要继续读 `.resolved` 与 Phase。

1. **状态骨架**：Core `PureRunActState`/`PureRunNodeSceneState` + Phase 单一映射器；Application 节点进入/结算/离开单一事务（顺带消除普通战斗双 CAS 与 Setup 缺身份）。
2. **宿主停用与控制器骨架**：`ApplyMode` 完整停用；抽出 Exploration/Battle 控制器与 Camera，把 Main 造图职责移出。
3. **页面迁移**：普通战后原图探索 → Overview 用真实宿主 → Rest/Store/Treasure → 事件去 fallback + 护送生产入口 → 只读残局。
4. **退役与存档**：删除 Initial/占位 Board、多边形底图、`GodotRogueMapView`；V12 切生产与恢复故障矩阵。
5. 性能、完整 Run、人工验收。

## 讨论结论与实现切片（2026-10-19 用户确认）

决策：**先骨架、后页面**；允许较大重构，由新节点状态取代 `.resolved` 字符串与 `PureRunPhase` 的权威地位，旧字段只作兼容派生。

| 切片 | 内容 | 完成判据 |
|---|---|---|
| S1 Core 状态机 | `PureRunActState`/`PureRunNodeSceneState`（阶段 + 已离开）与 `PureRunNodeSceneTransitionService`；Phase/`RunAdventureState` 改由单一映射器派生；`CurrentNodeId`、`IsExitUnlocked` 改读节点状态 | Core 测试覆盖合法/非法转移；全仓不再以 `.resolved`/Board 字符串作权威判断（兼容读取除外） |
| S2 Application 单一事务 + V12 切生产 | `PureRunState` 增加 ActState 字段（仅 V12 读写）；生产 Store 切 V12，旧档经 `PureRunLegacyActStateProjector` 迁移、不可唯一映射则保留原档拒绝；节点进入、事件开战、结算、离开统一命令（RunId/ActId/NodeId/expected revision/幂等 key），一次 CAS；Setup 首战与所有入口写 `SceneIdentity`；普通/L4/L5/L6 结算合并为单次保存；`StartNewRun` 不再写 `InitialExploration` | 每个命令 SaveCount==1；重复/冲突/保存失败测试；生产 Store 读写 V12；新状态成为权威，`PureRunPhaseConsistency` 迁移期断言 |
| S3 宿主停用与控制器骨架 | `ApplyMode` 成对停用 Process/输入/碰撞/Tween；新增 Exploration/Battle 控制器、Camera 控制器；Main 改为订阅 Application 投影，不再自造地图 | Godot 测试：非当前节点无输入/碰撞/Tween；Main 不再 `new GodotAdventureBoardView`（迁移中的页面除外，逐项清单跟踪） |
| S4 页面迁移 | 普通战后原图探索 → Overview（Atlas）替代 `GodotRogueMapView` → Rest/Store/Treasure → 事件去 fallback + 护送 Offer/Travel/Ambush → 只读残局 | 每页经生产路径（`CompleteBattle`/命令）测试，不接受直接调用显示方法作证据 |
| S5 退役与恢复加固 | 删 Initial/占位 Board、多边形底图、`GodotRogueMapView`；冷启动恢复与存档故障矩阵（含原“事件结算失败证据”测试） | 重建 Main 恢复测试；完整 verifier |
| S6 收尾 | 性能、完整 Run、godot-ai 冒烟截图、人工验收交接、OKF 与文档偏差修正 | 架构计划 Task 8 判据 |

2026-10-21 补充决策：V12 生产切换从 S5 提前到 S2，因为 V11 拒绝新身份字段，不切 V12 新状态就无法成为权威。

### 进度

- **S1 已完成（2026-10-21，未接调用方）**：`src/Tactics.Core/Runs/PureRunActState.cs`、`PureRunNodeSceneTransitionService.cs`、`PureRunLegacyActStateProjector.cs`（含 `PureRunPhaseConsistency`）；测试 `PureRunActStateTests`、`PureRunNodeSceneTransitionServiceTests`、`PureRunLegacyActStateProjectorTests` 共 32 例。Core 263/263、Application 396/396。验证使用已安装 SDK 10.0.302 直接执行（本机缺 global.json 要求的 9.0.312），正式门禁待补。条款 12 由“缺失”变为“模型就绪、未成为权威”；条款 13/14 待 S2。
- 2026-10-22 用户决策：结算已保存但同节点 Board 尚未 `.resolved`（历史双 CAS 中间态，含普通战斗与 L4/L6 非战斗节点）一律以 `act_state.inconsistent_resolution` 拒绝，不可恢复该局；不自动重打。开发期处理：`ResumeRun` 仅对 `act_state.inconsistent_resolution` 直接 CAS 清档（其他推导拒绝仍覆盖 PendingSceneRecovery 接受的旧形态，如 Initial Board 的晚期 checkpoint，暂不清档；事件战斗 PendingBattle 已按 PendingEventNodeId 推导为 InCombat）（无 Summary），返回 `run.reset_stale_save`，主页提示 "Save is from an older build and was cleared."；非当前 Demo 拓扑的地图跳过该检查。待办：正式发布前设计最小化重置策略。S2 合并为单次保存后新存档不再产生此状态。

每个切片开工前单独出细化计划；修改 Godot C# 时按 `godot-editor-lifecycle` 关闭并经 `Open-GodotDev.ps1` 重开编辑器（同时解决本次 `project_run` 无法 live 的问题）；每切片结束同步本报告、架构计划执行进度与 OKF。
