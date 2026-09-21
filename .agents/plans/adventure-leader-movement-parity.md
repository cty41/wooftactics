# Adventure 领队逐格移动、格中心改道与全 Active Board 统一计划

## Summary

将 Start Camp 及所有 Active Adventure Board 的领队移动从瞬移改为与战斗相同的逐格表现：复用 `StandardUnitPresentationResource` 的移动周期、身体起伏/摆动/倾斜、逐格朝向、飞行覆盖层和 settle。移动中点击新的空地目标时采用 latest-wins，但只在角色到达当前格中心后重新寻路，避免半格漂移。

寻路采用“共享无领域规则的确定性最短路引擎 + 战斗/Adventure 独立 policy”：共享稳定优先队列、came-from 与回溯，战斗继续定义移动类型/地形代价/占位规则，Adventure 独立定义四方向、均匀代价、对象阻挡与固定队友占位。当前两域平局方向对齐，但允许未来独立分化。

成功标准：

- Start Camp 和全部 Active Adventure Board 均以真实 `GodotUnitActor` 表现队员；Preview 仍无运行时 Actor。
- 领队沿 Core 路径逐格移动，无瞬移；视觉参数与战斗完全复用。
- 移动中点击新空地时，当前段走到格中心后按最后一个合法目标改道。
- 移动中角色、对象、出口点击返回 `adventure.movement_in_progress`，不排队、不交互。
- 普通 Adventure 只在最终停稳后提交一次 `MoveLeader`；中途格位、Tween 和改道目标不写 Save。Start Camp 是临时位置例外。
- 暂停会暂停 Tween；页面退出、Reload、Abandon 或异常取消动画并阻止迟到回调提交。
- 战斗 Golden、现有战斗移动表现与 `IPathfinder` 公共接口零漂移。

## Current State

- `GodotBattlePresentationPlayer` 已有完整逐格移动表现，但一次预建整条 Tween，不能中途改道。
- `GodotStartCampView` 只检查 walkable/occupied 后直接设置 Actor 最终坐标。
- 普通 Adventure 由 `WireAdventureMovement` 先同步提交最终格，再整页 rerender，因此必然瞬移。
- `GodotAdventureBoardView` 当前以 `Label` 表示队员，需由 Main 注入 Unit Resource 和表现 profile，View 不自行加载 Catalog。
- `AdventureBoardPathfinder` 是独立 BFS，邻居顺序与返回约定不同于战斗 Dijkstra。
- 用户已确认 Start Camp 角色选择正确；当前人工失败边界是领队瞬移且寻路/动画未与战斗一致。
- 工作树包含上一轮未提交点击修复及任务外 `godot/project.godot` 修改；不得覆盖、清理或暂存后者。

## Contracts and Data Flow

保留 `GRID-PROJECTION-001`、`GRID-HIT-ROUNDTRIP-001`、`GRID-LOGIC-AUTHORITY-001`，新增：

1. `ADVENTURE-PATH-POLICY-001`：共享确定性搜索机械实现，战斗与 Adventure 各自拥有邻居、代价、占位和通行 policy。
2. `ADVENTURE-REPATH-CENTER-001`：新合法空地目标 latest-wins，只在下一格中心生效；不可达目标不替换旧目标。
3. `ADVENTURE-MOVE-COMMIT-001`：最终停稳后只提交一次目标格；中断不提交逐格进度，失败按权威 snapshot 回滚。
4. `ADVENTURE-MOVE-LOCK-001`：移动和提交期间锁定领队切换、对象及出口交互；Camera/Overview 可只读，Pause 真暂停。

数据流：

`Control-local click → GridPoint target → Adventure policy path → one-cell Tween → reached cell center → optional replan → settle → one final Core mutation → rerender authoritative snapshot`

Start Camp 使用同一 player，但终点只更新本页临时 `_candidateCells`。

## Public API / Schema Decisions

- 不改变 `IPathfinder`、`DeterministicDijkstraPathfinder.FindPath` 或 `AdventureBoardPathfinder.FindPath` 公共签名。
- 共享搜索引擎为 Core `internal`；Adventure wrapper 保留包含 origin 的旧约定，共享引擎统一返回 origin-excluded、destination-included。
- 不修改 Save schema，不持久化 Tween、当前段、desired target、replan count 或逐格进度。
- Adapter 新增 `GodotAdventureMovementCompletion` 与有界 `GodotAdventureMovementDiagnostic`。
- Test Probe 新增 movement playing/locked/current/desired/replan/outcome；transient 字段不进入业务 StateHash，但 watchdog 感知 playing/locked。

## File Structure

- `src/Tactics.Core/Pathfinding/DeterministicGridPathSearch.cs` — 无领域规则的稳定 Dijkstra 引擎。
- `src/Tactics.Core/Pathfinding/DeterministicDijkstraPathfinder.cs` — 战斗 wrapper 保持 policy/API，仅委托共享引擎。
- `src/Tactics.Core/Runs/AdventureBoardDefinition.cs` — Adventure 独立 policy。
- `src/Tactics.Core/Runs/RunAdventureTransitionService.cs` — destination 等于当前格时 no-op。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotUnitMoveTweenBuilder.cs` — 单格移动与 settle builder，不持有业务状态。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotAdventureMovementPlayer.cs` — 一格一个 Tween 的 pause/cancel/repath player。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotBattlePresentationPlayer.cs` — 改用共享 builder，继续拥有 battle frame lifecycle。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotStartCampView.cs` — 接入 player、改道与交互锁。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotAdventureBoardView.cs` — 真实 Actor、语义格位、ZIndex 与 movement state。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs` — 注入资源/profile/speed，最终单次提交、失败回滚和 Pause。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunTestContext.cs`、`godot/tests/GameplaySpec/GodotGameplayRuntimeRunner.cs` — Probe/watchdog。
- Core/Godot 相关 tests — 路线、表现、改道、锁定、取消与生产 GUI 回归。
- `.agents/docs/adventure-movement-contract.md` — 权威移动合同。
- manual acceptance/checklist、父计划和相关 OKF — 验收与知识收尾。

## Scope

### In Scope

- Start Camp 和所有 Active Adventure Board 的真实队员 Actor、逐格移动、改道、输入锁、暂停、取消和最终提交。
- 共享 Tween 单格表现与无领域规则搜索机械实现。
- Adventure 独立 policy，当前在等价均匀棋盘上与战斗稳定路线对齐。
- `[AdventureMovement]` 结构化状态转换诊断；内存只保留最后状态，日志不逐帧输出。
- Core、Godot runtime、真实 Viewport 输入、Probe/watchdog、文档、OKF 和人工验收。

### Out of Scope

- Adventure 引入战斗 MP、地形移动代价、飞行/游泳规则或行动次数。
- 队友跟随、队形、多人同时移动。
- Preview 创建真实 Actor、AI、Tween 或交互。
- 每格写 Save、Reload 续播动画、半格位置持久化。
- 自动走到对象后触发交互、对象点击自动改道、超过 latest target 的输入队列。
- 战斗 TileMap 迁移、对象 Label 美术替换、地图模板/Resource 生成和构建工具链修改。

## Implementation Tasks

### Task 1：合同与共享确定性搜索

- 冻结四个 Adventure movement Contract ID。
- 新增 `DeterministicGridPathSearch`，两个 wrapper 提供独立 policy。
- 保持 Battle golden、移动成本、公共 API 与 Adventure 旧返回约定。
- Adventure blocked/occupied/tie/unreachable 有精确测试；等价均匀棋盘路径当前对齐。
- 用不同 policy fixture 证明未来可分化。
- `MoveLeader(origin)` 不增加 revision。

### Task 2：战斗同款单格动画与可改道 Player

- 抽取无业务状态的 `GodotUnitMoveTweenBuilder`。
- Battle player 调用 builder，仍自行拥有 sequence、baseline、frame completion 与 cleanup。
- Adventure player 一段一个 Tween，状态包含 committed origin、current centered cell、desired destination、path、leader、generation 与 moving。
- 每段到中心后更新 current，再读取 latest target 重规划。
- 相同目标不重启；不可达新目标不覆盖旧目标。
- Pause/Resume、Cancel/ExitTree、generation 防迟到 callback。

### Task 3：Start Camp 垂直切片

- 用 player 取代直接设置最终 Position。
- 真实输入启动逐格移动；段中空地点击在下一中心改道。
- 移动中候选/出口返回 `adventure.movement_in_progress`；Overview 不移动/改道。
- Camera `M/F/Home` 不改变路径；Pause 真暂停。
- 移动不修改 PendingRunSetup/Save，页面重建按模板槽恢复。

### Task 4：全部 Active Adventure Board

- Main 按 `run.Party` 注入 ActorId、Unit Resource、profile 与 speed。
- Initial、事件前后、Rest/Store/Treasure、战斗入口和 resolved Active Board 均使用真实 `GodotUnitActor`；对象 marker 可保留。
- 只有领队移动，队友固定占位；停稳后可切换领队。
- 动画期间 snapshot/Save 保持 origin；settle 后只执行一次 `MoveLeader`。
- 提交成功后终点不回跳；失败按权威 snapshot rerender并记录 `commit-failed`。
- 移动/提交期间对象、角色、出口不能触发；页面退出取消未提交动画。

### Task 5：诊断与分层回归

- Probe 暴露 Playing/Locked/Current/Desired/Replan/Outcome。
- Runner watchdog 将 Adventure movement 视为正常进度，但 StateHash 等到权威 commit。
- 非 identity transform 下用 `Viewport.PushInput` 启动移动并在第一段中注入第二目标。
- 断言视觉位置可处于中心之间，但逻辑 CurrentCell 只在中心改变。
- 覆盖 latest-wins、不可达、交互锁、pause/resume、ExitTree cancel、无迟到 callback/transient Tween。
- 普通 Board 断言真实 Actor、semantic pointer target、ZIndex 和固定队友。
- 至少一条正式 `Main.tscn` 生产输入测试证明 Initial Board 最终格、revision +1 和隔离 Save。
- gameplay spec compiler 不可用时不得手改 plan；保留 blocker并使用直接生产 Godot 测试。

### Task 6：门禁、人工验收与知识收尾

- 顺序运行 Core、Application、Godot build/tests，避免 shared obj/TestHost 竞争。
- 运行统一 verifier；任务外 vendor/checkpoint/compiler blocker保留第一条精确签名。
- 运行 diff-check、代码 review、OKF impact/sync/bundle validation，不吸收 `godot/project.godot`。
- Ledger 记录用户已确认选人子项正确，但不把包含相机/移动的整个 MQA 标 passed；Movement 经修复门禁后重开 pending。
- 人工复验 Start 和一个普通 Active 节点：远距离逐格动画、战斗同款姿态/朝向、快速改道、移动中交互锁、Pause/Continue、切页/Reload无幽灵提交。
- 生命周期 finally 只恢复本流程关闭的 canonical Editor，不注入前台输入。

## Test Plan

### Automated

1. Core shared search、Battle golden、Adventure policy、occupancy/tie/no-op。
2. Run transition：最终一次 revision、失败不变、no-op 不变。
3. Godot builder/player：逐格 Tween、pose/facing/ZIndex、repath、pause、cancel、generation。
4. Start Camp：真实 GUI input、Current/Overview、候选/出口锁、临时状态不写 Save。
5. 普通 Board：真实 Actor、production Main、最终 commit、watchdog/probe、隔离 Save。
6. 目标回归后运行统一 verifier并区分已知仓库阻断。

### Manual

- 隔离 Run 中依次测试 Start Camp 和一个普通 Active 节点。
- 点击至少三格外目标，观察每格节奏、身体起伏/倾斜、朝向与 settle。
- 一段移动中快速点击两个不同空地；必须到当前中心后转向最后目标，无斜切、半格转向或漂移。
- 移动时点击队员、对象和出口，停稳后交互恢复。
- Pause 冻结当前视觉位置；Continue 后完成。切页或 Reload 不保存中间格、不迟到提交。
- 失败保留 `[AdventureMovement]`、seed/revision、origin/current/desired、短视频与隔离 Save。

## Risks / Assumptions

- 共享的只有搜索机械实现，不共享领域 policy；Battle golden 是第一门禁。
- Probe transient movement 不进入业务 StateHash，避免 action 提前完成。
- 一段一个 Tween + generation token 防陈旧 callback。
- `_actorCells` 继续负责语义命中，Sprite 只负责表现。
- “全部 Adventure Board”指 Active 表面，不包括 Preview。
- Adventure 不引入 MP/地形代价；未来从独立 policy 扩展。
- 默认沿用 battle playback speed；动态 speed 同步当前 Adventure Tween。
- 不可达新目标保留旧路线；多个合法目标只保留最后一个。
- 当前 Save 仍可保存最终 ActorCells；只禁止逐格/动画进度持久化。

## Handoff Notes

- 父计划需链接本切片，并澄清“不持久化逐格进度”不等于“不播放逐格动画”。
- 先读 Core 两个 pathfinder、`GodotBattlePresentationPlayer`、`GodotStartCampView`、`GodotAdventureBoardView` 与 `WireAdventureMovement`。
- Adventure 不伪造/复用 Battle cue/frame；只共享单格 Tween builder。
- Builder 不持有 path、Run、commit、generation 或 event。
- View 不自行加载 Catalog；Main 注入 Unit Resource/profile。
- 不修改 `.tres/.tscn`、Save schema、程序集边界、构建工具或任务外 `godot/project.godot`。
- 完成实现与验证后，将长期合同并入 `.agents/docs/`，未完成项写统一缺口或新计划，更新 OKF，并删除此 completed child plan，由 Git 保存历史。
