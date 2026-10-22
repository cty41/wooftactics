# Roguelike 单 Act 节点宿主：Review / 测试迭代交接

> 用途：把当前 Goal 的可执行上下文交给另一位 Agent，继续 code review、失败注入、测试与修复迭代。
>
> 本文是一次性交接快照，不是设计真相源。权威执行入口是 [`../plans/roguelike-act-node-scene-architecture.md`](../plans/roguelike-act-node-scene-architecture.md)，合同见 [`roguelike-act-node-scene-contract.md`](roguelike-act-node-scene-contract.md)，实现事实以代码、Godot Resource 与测试为准。人工 verdict 只写入 [`manual-acceptance.md`](manual-acceptance.md)。

## 1. 当前 Goal

```text
按 .agents/plans/roguelike-act-node-scene-architecture.md 实施单 Act Demo 全节点常驻 TileMap 与统一节点宿主改造；逐切片实现、验证与记录，保护任务外修改，人工验收保持真实 pending。
```

Goal 仍为 active，不得标记完成。当前已推进到第17轮；主要剩余项：

1. **最高优先级：事件战斗结算的多 CAS/save 崩溃恢复缺口。**
2. 冷启动 Resolved 事件身份与正式模板恢复。
3. Rest / Store / Treasure 等非战斗节点迁入统一正式 Host Surface。
4. 更广泛 Adventure controller / 页面动态表现归属迁移。
5. 生产 Store 从 V11 迁移到 V12，并完成旧档恢复闭环。
6. 完整 Run、性能、Reload 与人工 play-test；人工状态必须保持 `manual_qa_pending`，直到真人确认。

## 2. 仓库与保护边界

- Worktree：`D:\codes\tactics-worktrees\feat-gd1`
- 分支：`feat/gd1`
- HEAD：`5d1a73c1`
- 唯一 Godot 项目：`godot/project.godot`
- 不创建、删除或切换 worktree；不要 merge 其他分支。
- 工作区有大量本 Goal 修改和既有任务外 dirty 文件。接手后先运行 `git status --short`，**禁止 reset/clean/stash/覆盖/批量格式化**。
- 特别保护：`godot/project.godot`、`.agents/docs/manual-acceptance.md` 以及任何未被当前切片明确涉及的修改。
- `.tres/.tscn` 只能经 ResourceSaver、Editor API 或受测转换器生成，禁止手写。
- Core/Application 不得引用 Godot。
- 不抢占前台，不注入真实输入；自动 QA 使用后台测试链。

## 3. 已实现架构

### 3.1 Act / Node / Host

- Demo 只有一个 Act；当前 Act 16 个节点全部在可交互前准备。
- `GodotActWorldHost` 位于页面树之外，持有所有正式 `GodotAdventureMapInstance` / `GodotIsometricTileMapSurface`。
- `GodotNodeActivationLease` 保证唯一 active node；隐藏节点同时禁用业务输入/处理。
- Setup 草稿 Host 只有在最终一次 CAS 提交成功后才被 Run 收养；取消、终局与迟到回调有隔离。
- `PureRunActDefinition`、`PureRunActBlueprint`、`RunNodeSceneIdentity` 提供拓扑、显式 StartNodeId、模板绑定和22种确定性内容变体。

关键入口：

- `src/Tactics.Core/Runs/PureRunActDefinition.cs`
- `src/Tactics.Core/Runs/PureRunActBlueprint.cs`
- `src/Tactics.Core/Runs/RunNodeSceneIdentity.cs`
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotActWorldHost.cs`
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotActLoadCoordinator.cs`
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotActContentCatalog.cs`
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.ActWorld.cs`

### 3.2 Setup、普通战斗与恢复

- Setup 最后一个必需技能执行一次 CAS，保存完整队伍和首战 checkpoint；成功后才清草稿并发请求。
- 普通、Elite、Boss 已借用对应 Host 的原始 Surface；Actor/Input/VFX 仍保持原棋盘局部坐标，通过 Host 世界仿射对齐。
- 模板与 runtime layout 会验证 blocked、水域、**有序**玩家/敌人出生槽；失败发生在创建战斗和取得有效租约之前。
- V12 identity codec 已存在，但生产 Store 仍为 V11。
- Pending 恢复采用零写投影与请求交叉校验；保持 checkpoint、revision、AdventureRevision、队伍和 RNG。

关键入口：

- `src/Tactics.Application/Runs/PureRunPendingSceneRecovery.cs`
- `src/Tactics.Application/Runs/PureRunNodeSceneProjector.cs`
- `src/Tactics.Application/Runs/PureRunEncounterRequestValidator.cs`
- `src/Tactics.Application/Runs/RunSaveDocumentV12.cs`
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.ResidentBattle.cs`
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotIsometricBattleBoard.cs`

### 3.3 事件探索、战斗和 warm 战后 Surface

正式事件模板合同：

- `CursedChestMimic` → `cursed-chest` / Chest。
- `FallenAltarGuardian` → `fallen-altar` / Altar。
- `LostVillagerEscort` → `lost-villager` / Npc。
- 正式对象格为 `(5,5)`。
- 正式战斗布局使用 `encounter.pure-run.n1`；party slots `(1,4),(1,5),(2,4)`；enemy slots `(6,4),(7,3),(7,5),(8,4)`。

当前行为：

- Setup/普通 Ready Host baseline：L4 Cursed、L6 Altar。
- 冷 Pending：只用 `PureRunPendingSceneRecovery` 已证明的当前节点实际事件变体覆盖呈现计划。
- 冷 Resolving + 已持久化 Traveling Escort：只有在 phase、transaction、map identity/前驱、层级、transaction key、Quest/Accepted/Destination、alive 等结构都有效时，目的 Mystery 节点才以 Lost 模板准备；失败安全回落 baseline/legacy。该逻辑只影响 Adapter presentation，不修改 `MysteryEventAssignments`，不补造 Accept/BeginTravel。
- `GodotAdventureBoardView` 有 resident 模式：owned TileMap 隐藏且不写格；拾取/fit/标记通过完整 canvas 仿射映射到 Host Surface；不改 Map/Surface/Terrain 内部 transform 或 used cells。
- 事件探索采用正式 template Board、对象 `(5,5)`、blocked/exit 与 PartyEntrySlots。
- 探索页有 lease generation fence；页面切换先取消移动，迟到输入/移动完成不保存、不触发业务。
- 探索点击进入战斗时复用同一 Surface 实例。
- Pending 事件战模板/context匹配才借 Host；warm mismatch（例如 Host 已是 Altar，后来注入 Lost）保留 legacy board，不热重配。
- 全部结算保存成功后的 **warm** `ShowPostEventScene` 会回到同一正式 Surface；Cursed/Altar 保留已解决对象，Lost 失败或 NPC 死亡时不再投影活 NPC。

关键入口：

- `godot/src/Tactics.Godot.Adapter/Runtime/GodotAdventureBoardView.cs`
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs`
- `godot/tests/ResidentEventBattleProductionGodotTests.cs`

## 4. 下一位 Agent 必须优先处理的 P0

### 4.1 根因：事件结算是2或3次独立 CAS/save

`GodotPlayableRunMain.CompleteBattle` 的事件分支当前顺序：

1. `ApplyLayerFourBattleResult`：合并战斗队伍、奖励、掉落、金币，提交 NodeTransaction / MapState；但 Adventure 仍保留 PendingEvent marker，Lost Escort 仍为 `BattlePending`。
2. 仅 Lost：`ApplyMutation(PureRunEscortService.ResolveBattle)`，将 Escort 置为 Completed/Failed。
3. `ApplyMutation(RunAdventureTransitionService.ResolveEventBattle)`，board id 加 `.resolved`，清三个 PendingEvent 字段。

### 4.2 已确认的故障窗口

- save#1 成功后崩溃：节点已提交，但 PendingEvent marker 仍存在；重启 `RouteRunState` 不续跑事件结算，而走 generic resolved 页面。Lost 仍可能卡在 `BattlePending`。
- Lost save#2 成功、save#3 失败：重试 `CompleteBattle` 时第一步可能 duplicate，但再次 `ResolveBattle` 会因 Escort 已非 `BattlePending` 返回 `escort.battle_unavailable`，无法继续清 marker。
- 最终 save 成功后重启：RouteRunState 只看到 `.resolved`，走 generic `ShowResolvedAdventureNode`，不会恢复事件专属残局/context，也无法为冷 Host 选择实际事件模板。
- 当前进程内 warm 成功路径能显示正式 Surface，但这**不是**崩溃恢复或原子结算闭环。

### 4.3 推荐下一切片

优先在 Application 层建立单次 CAS 的事件结算操作，或建立明确、幂等、可续跑的结算阶段；不要只在 Godot Main 里串联更多保存。

建议目标：

1. 一个 Application command 接收战斗结果与 `protectedNpcAlive`，在纯内存中依次完成战斗结果、Escort 结果、Adventure event resolve，再由 SessionService 做一次 CAS。
2. duplicate/retry 必须幂等；已保存的完整结果不会重复奖励、重复应用 transaction key 或重复变更随机状态。
3. 如果必须兼容历史中间态，增加只读检测/归一化策略，明确哪些状态可自动续跑，哪些必须保留原档并报稳定错误。
4. 为最终 Resolved 状态保留足够的显式事件身份；不要从复用 Encounter ID、默认 assignment 或页面文案猜变体。
5. 冷启动 Resolved 应准备实际事件模板并路由事件专属残局；模板证据不足时安全 legacy/fail，不热重配已准备 Host。

建议先读：

- `GodotPlayableRunMain.cs`：`CompleteBattle`、`HandleSettlementFailure`、`RecoverSavedSettlementNavigation`、`RouteRunState`、`ShowPostEventScene`。
- `PureRunSessionService.cs`：CAS 与 duplicate 语义。
- `PureRunLayerFourNodeService.cs`：`ApplyBattleResult` / `Commit`。
- `PureRunEscortService.cs`。
- `RunAdventureTransitionService.cs`：`BeginEventBattle` / `ResolveEventBattle`。

## 5. 推荐失败优先测试矩阵

先写 Application 测试，再改生产代码：

1. Cursed / Altar：事件结算只保存一次，奖励、transaction、Adventure resolved 同时可见。
2. Lost 胜利且 NPC alive：一次保存后 Escort Completed、alive=true、Adventure resolved、节点 committed。
3. Lost 胜利但 NPC dead：Escort Failed、alive=false；Resolved 页面不投影活 NPC。
4. 玩家失败：保持现有 Defeated 语义，不错误开放出口或奖励。
5. CAS write failure：原 Snapshot 完全不变；重试只应用一次。
6. revision conflict：不产生半完成事件状态。
7. duplicate request：不重复奖励、金币、掉落、Escort transition、Adventure revision 或 transaction key。
8. 历史 save#1 中间态、Lost save#2 中间态：明确自动续跑或稳定拒绝，原档不被破坏。
9. 冷最终 Resolved 恢复：实际 Cursed/Altar/Lost 模板、正式对象残局、相同节点 Surface；缺身份/冲突证据明确失败或 legacy。
10. Godot production：exploration → battle → resolved 保持 Surface InstanceId；16张地图的 Map/Surface/Terrain identity、内部 transforms、used cells 不变；旧 lease 输入零保存。

现有测试入口：

- `src/Tactics.Application.Tests/PureRunSessionServiceTests.cs`
- `src/Tactics.Application.Tests/PureRunPendingSceneRecoveryTests.cs`
- `src/Tactics.Application.Tests/PureRunNodeSceneProjectorTests.cs`
- `godot/tests/ResidentEventBattleProductionGodotTests.cs`
- `godot/tests/ResidentBattleProductionGodotTests.cs`
- `godot/tests/PlayableRunUiGodotTests.cs`

## 6. 当前验证证据

最近定向结果：

- Core：231/231（最近完整 verifier 到达该阶段）。
- Application：391/391（最近完整 verifier 到达该阶段）。
- FrozenOracle：15/15。
- Resident ordinary/Elite/Boss production：4/4。
- Resident event production：7/7（含 Cursed warm探索→战斗、Lost冷探索→保护NPC战斗、warm Resolved正式Surface）。
- AdventureMovement：4/4。
- AdventureMapTileMap：9/9。
- PlayableRunUi：49/49；仍有既有 4 orphan warning。
- Production Adapter：0 warning / 0 error。
- OKF tests：16/16；bundle valid。
- Gameplay contract validation：valid。
- `git diff --check`：通过，仅有既有 CRLF→LF 提示。

完整 `Tools/godot/Verify-GodotProject.ps1` 尚不能宣称全绿。它在既有测试
`test_public_root_reconstruction_preserves_ignored_tracked_files` 失败，因为临时 reconstructed source 缺：

```text
Tools/public-release/Verify-PublicRelease.ps1
```

在失败之前 Core/Application/FrozenOracle 均通过。不要绕过、伪绿或把它当本 Goal 修复，除非用户扩大范围；但每次报告时必须如实说明。

常用环境：

```powershell
$env:ProgramFiles='C:\Program Files'
${env:ProgramFiles(x86)}='C:\Program Files (x86)'
$env:ProgramW6432='C:\Program Files'
$env:GODOT_BIN='D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe'
```

推荐构建：

```powershell
dotnet build godot/tests/Tactics.Godot.TestHost.csproj -c Debug --no-incremental -m:1 '-p:GodotProjectDir=D:\codes\tactics-worktrees\feat-gd1\godot\'
dotnet build godot/Tactics.Godot.Adapter.csproj -c Debug --no-incremental -m:1
```

推荐定向测试：

```powershell
dotnet test godot/tests/Tactics.Godot.TestHost.csproj -c Debug --no-build --filter 'FullyQualifiedName~ResidentEventBattleProductionGodotTests'
dotnet test godot/tests/Tactics.Godot.TestHost.csproj -c Debug --no-build --filter 'FullyQualifiedName~PlayableRunUiGodotTests'
dotnet test src/Tactics.Application.Tests/Tactics.Application.Tests.csproj -c Debug
```

## 7. Review 要点

Review 时优先检查：

- CAS/revision/RNG/checkpoint 是否保持；不得为了附加场景身份增加无意义保存或重建 checkpoint。
- `EncounterRequest.EffectiveRandomState = CheckpointRevision - AdventureRevision` 是否未漂移。
- Presentation Blueprint 与持久恢复证据是否继续分离；未来节点呈现覆盖不能提交给 Projector 当作 save 事实。
- `MysteryEventAssignments` 是否保持历史/domain 状态，不被 Host presentation 修改。
- Pending、Traveling、Resolved 三类证据是否各自严格，不能互相替代。
- 模板 mismatch 是否安全 legacy/fail，而不是热重载或重配常驻 Host。
- 页面退出、QueueFree、迟到回调是否受 lease generation fence；旧 View 不能使新 Battle/Resolved lease 失效。
- 正式 Surface 内部 Terrain/Surface/Map transform、used cells、UID 不得被运行时页面重写。
- Lost 不得补造 Offer/Travel；必须来自合法持久 Escort 证据。
- 自动测试不能把人工验收改为 passed。

## 8. 知识与文档同步

每个切片完成后：

1. 更新 [`../plans/roguelike-act-node-scene-architecture.md`](../plans/roguelike-act-node-scene-architecture.md) 的精确证据和未完成边界。
2. 更新受影响的 OKF 正文，通常至少包括：
   - `../knowledge/systems/battle.md`
   - `../knowledge/systems/roguelike-run.md`
   - 必要时 `../knowledge/architecture/project-overview.md`
3. 执行：

```powershell
python Tools/okf/catalog_impact.py report --worktree
python Tools/okf/catalog_impact.py sync --worktree --scope battle-system --write
python Tools/okf/catalog_impact.py sync --worktree --scope roguelike-run --write
python -m unittest discover Tools/okf -p 'test_*.py'
python Tools/okf/validate_bundle.py
node Tools/gameplay-test-spec/dist/src/cli.js validate-contracts --doc .agents/docs/roguelike-act-node-scene-contract.md
git diff --check
```

人工清单与 verdict 不得由自动化代签；保持 `manual_qa_pending`。

## 9. 给下一位 Agent 的直接指令

> 阅读 `.agents/docs/roguelike-tilemap-continuation-handoff.md`、`.agents/plans/roguelike-act-node-scene-architecture.md` 和合同；先用失败测试复现事件结算 save#1/save#2 崩溃中间态，再把事件战斗结果、Escort resolve、Adventure resolve 收敛为 Application 单次 CAS 或显式幂等可续跑事务。保护整个 dirty worktree，不改任务外文件，不手写 Godot Resource。定向 Application 测试通过后，再验证 Cursed/Altar/Lost exploration→battle→resolved 的同一 Surface 与冷恢复；人工验收继续 pending，Goal 不得提前完成。
