# Roguelike Map 全节点真实等距 TileMapLayer 改造计划

## Summary

把 Pure Run 当前彼此割裂的“自绘战斗棋盘、占位 Adventure TileMap、圆点路线总览”统一为一套真实 Godot 等距 `TileMapLayer` 地图系统：路线范围内所有节点以轻量 Preview 地图组成可拖动的地图集，当前节点以相同模板切换为唯一 Active 地图；战斗、事件、角色、对象、出口、迷雾和残局都投影在同一地图基础设施上。

成功标准是：当前地图达到现有战斗场景的占屏尺寸并使用真实 `GodotUnitActor`；开局可像《杀戮尖塔》一样规划完整路线；出口与目标地图、连线和常驻情报一一对应；普通/精英/Boss 进入后原地开战，事件战斗在事件原图切换；节点结果与路线迁移原子保存，重启后按模板槽位恢复；四层自动门禁通过，canonical Editor 人工验收仍明确待用户确认。

交付采用多个可独立验证、可依次合并的 PR。不得把自动测试、截图或 Headless 运行写成人工视觉通过。

## Current State

- `GodotIsometricBattleBoard` 是 `Control`，通过 `_Draw()` 手绘菱形；它使用共享 10×10、96×48 等距投影和真实 `GodotUnitActor`，但不是 `TileMapLayer`。
- `GodotAdventureBoardView` 使用真实 `TileMapLayer`，但 TileSet 是运行时生成的双色占位图，角色和对象是显示 ID 的 `Label`；没有战斗棋盘的适配、排序和真实角色表现。
- `GodotPlayableRunMain` 程序化创建开始营地及各类节点，布局大多是相同外围阻挡；营地候选槽硬编码为四个位置，普通节点只移动领队。
- `GodotRogueMapView` 只绘制圆点、文字和连线并保存临时 pan/hover；权威拓扑来自 `PureRunMapDefinition`，节点状态来自 `PureRunFlowProjector`，删除该 View 不会丢失业务数据。
- `RunAdventureState` 当前持久化角色格位；已确认的新恢复合同只保存节点级语义和领队，重启时从模板入口/战斗槽重建位置。
- 现有自动测试证明投影 round-trip、100 个 Tile 单元和输入目标，但没有证明真实 Actor、地图占屏比例、Preview/Active 隔离、正式对象、同图战斗或地图集可读性。
- `.agents/docs/project-known-gaps.md` 已记录 Adventure 对象、节点构图、状态变化、转场和音频仍主要是功能占位；`.agents/docs/manual-acceptance.md` 的 Tile Readability、Input、Scene Change、Route Misclick和 Start Camp仍需人工处理。
- 当前工作树包含任务外 `godot/project.godot` 修改以及未跟踪 session 文档；所有 PR必须按精确路径暂存，禁止覆盖、暂存或提交这些内容。

## Relevant Context

- 唯一项目是 `godot/project.godot`，Godot 版本为项目固定的 4.7 C#；Editor只通过 `Tools/godot/Open-GodotDev.ps1` 启动。
- Core/Application保持 Godot-free；TileSet、TileMapLayer、相机、Canvas和 Node生命周期只存在于 Adapter。
- `.tres/.tscn` 不得手写，地图模板和场景只能由 ResourceSaver、PackedScene或受测生成器产生。
- `GRID-PROJECTION-001`、`GRID-HIT-ROUNDTRIP-001`、`GRID-LOGIC-AUTHORITY-001` 继续成立；需新增并批准地图模板、Preview/Active、同图战斗、出口情报和节点级恢复合同。
- 运行数据流固定为：Core地图/Run状态 → Application投影与事务 → Adapter模板资源/地图实例 → Preview或Active控制器；Adapter输入只能调用 Application/Core服务，不直接改资源文件或权威状态。
- 位置分两类：节点内移动格位是内存态，重建 Main后丢弃；节点解决、对象结果、发现、路线、领队和遭遇入口是存档态。

## Public Contracts and Data Flow

- 扩展 `AdventureBoardDefinition`，保留10×10、阻挡、对象和寻路语义；出生槽、入口/出口/连线/相机锚点由新的 engine-neutral `AdventureMapTemplateDefinition` 表达。
- 新增 `AdventureMapTemplateResource` 作为 Godot Resource适配层，导出地形/装饰/遮罩 Tile层、状态层及逻辑锚点，并通过 `ToCoreDefinition()` 进行完整验证。
- 新增 `AdventureMapRuntimeMode`，只允许 `Preview` 与 `Active`；Preview禁止单位、AI、碰撞、寻路、交互和逐帧处理，Active在任一时刻只能有一个实例。
- 新增 `PureRunNodeIntelState`，保存 `Planning / TacticalPreview / Current / Completed`；开局 `Planning` 已公开节点类型和拓扑，`TacticalPreview` 才公开有限地形与威胁情报。
- 收敛 `RunAdventureState`：保留生命周期、Board Content ID、Leader ID、对象/事件/出口/Scene revision，不再序列化 Actor格位；新增 engine-neutral、非持久化的 `AdventureExplorationSession` 保存当前页面内的角色格位和领队移动。
- Save升级到下一个 schema版本：读取旧 ActorCells但归一化丢弃位置，保留合法 Leader与节点结果；PendingBattle只保存遭遇入口 checkpoint，不保存逐行动战斗状态。
- 出口模型保持 `TargetNodeId` 一一绑定，并补齐常驻 `ExitIntelSnapshot`；确认命令携带 expected revision，原子提交目标、发现、当前节点、模板和事务 key。
- 地图节点状态层使用语义 ID，例如 `base / active / combat / resolved`；Run只保存状态 ID及对象结果，不保存 TileMap cell副本。

## File Structure

- `src/Tactics.Core/Runs/AdventureBoardDefinition.cs`、`PureRunMapModels.cs`、`RunAdventureTransitionService.cs` — engine-neutral模板、发现、出口和节点级恢复合同。
- `src/Tactics.Application/Runs/PureRunFlowProjector.cs`、`PureRunSessionService.cs`、`RunSaveDocumentV*.cs` — 地图集投影、原子迁移、Save升级与Continue恢复。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotIsometricTileMapSurface.cs` — 战斗与Adventure共用的真实等距 TileMap表面、投影、拾取和覆盖层宿主。
- `godot/src/Tactics.Godot.Adapter/Runtime/AdventureMapTemplateResource.cs`、`GodotAdventureMapInstance.cs` — 模板Resource适配与 Preview/Active实例生命周期。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotRogueMapAtlas.cs`、`GodotMapCameraController.cs`、`GodotMapPlanningOverlay.cs` — 多图布局、相机聚焦/拖动、连线、遮罩、徽标及详情面板。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotAdventureRuntimeController.cs`、`GodotMapExitPresenter.cs` — 非战斗领队移动、对象交互、出口常驻情报与确认。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs` — 只保留页面编排；把地图、相机、节点运行和战斗表面职责委托给新组件。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotIsometricBattleBoard.cs`、`GodotAdventureBoardView.cs`、`GodotRogueMapView.cs` — 在对应替代切片完成并通过回归后删除。
- `godot/src/Tactics.Godot.Adapter/Editor/AdventureMapAssetFactory.cs`、`AdventureMapAssetBuilder.cs` — 通过 ResourceSaver/PackedScene确定性生成模板、状态层和Catalog条目。
- `godot/tests/IsometricBattleBoardGodotTests.cs`、新增地图集/模板/生命周期GdUnit套件 — 真实 TileMap、投影、Actor、Preview/Active和清理断言。
- `godot/tests/GameplaySpec/**` — 地图集、出口、事件同图战斗、恢复、失败和不可回访的真实输入旅程。
- `Tools/godot/Verify-GodotProject.ps1`及Godot CI workflow — 生成器幂等、结构截图、Gameplay journeys和发布门禁入口。
- `.agents/docs/isometric-grid-anchor-contract.md`、`manual-acceptance.md`、`project-known-gaps.md`及受影响OKF正文 — 新合同、用户失败反馈、人工复验与长期状态。

## Scope

### In Scope

- 战斗棋盘和所有Roguelike节点统一为真实 Godot等距 `TileMapLayer`。
- 正式10×10地图模板、声明式状态层、生成器、Catalog和验证器。
- 开始营地放置全部真实候选角色，候选槽由模板定义。
- 非战斗节点放置三名真实队员，只允许当前领队移动和交互。
- 地图集多图同屏、拉远/拖动、全屏聚焦、只读规划信息和旧圆点View退役。
- 开局公开完整节点类型/拓扑；可进入节点追加有限地形、对象类别和威胁情报。
- 目标绑定出口、常驻目标徽标、到达确认、原子路线迁移及可中断相机表现。
- 普通/精英/Boss进入后原地开战；事件战斗在事件原TileMap切换并留下残局。
- Preview/Active严格单实例生命周期和远景降级策略。
- 节点级Save恢复、入口/战斗槽重置、遭遇入口重开、失败终局及禁止回访。
- Core/Application、GdUnit、Gameplay Spec、结构截图/性能证据、统一验证与人工验收账本。

### Out of Scope

- 战斗前对话、剧情、演出和战前探索。
- 队友跟随、刚性编队及全队探索移动。
- 节点内格位、逐格移动、相机动画和战斗逐行动持久化。
- 连续大世界、运行时程序化拼图、反向出口、已完成节点回访和快速旅行。
- 远端节点真实敌人/运行时Actor预加载。
- 转场Reload续播、全屏逐像素Golden、自动签发人工验收。
- 新的正式音频、完整VFX或与本地图改造无关的角色美术生产。

## Phases and Pull Requests

### Milestone 1 / PR 1：共享真实 TileMap 基础与开始营地垂直切片

#### Task 1: 冻结新地图、发现和恢复合同

- 目标：把16项产品决定变成可编译、可验证的 engine-neutral合同，并建立Save升级边界。
- 输入：现有 `AdventureBoardDefinition`、`PureRunMapState`、`RunAdventureState`、V10 Save和等距投影合同。
- 输出：模板/槽位/状态层/发现/出口情报类型，非持久化探索Session，以及下一版Save归一化规则。
- 涉及文件：Core Run模型与服务、Application投影/Save、合同文档和单元测试。
- 验收标准：
  - 模板验证拒绝非10×10、重复槽位、越界锚点、不可达入口/出口、缺少候选/队伍/战斗槽和未知状态层。
  - 开局所有节点投影为Planning并公开类型与拓扑；只有可进入节点投影TacticalPreview。
  - Save round-trip保留节点/领队/对象结果/发现/路线/遭遇checkpoint，不再写入ActorCells或相机状态。
  - V10读取后丢弃旧位置并从模板槽位恢复；非法Leader按首名合法存活角色回退。
  - 新增合同拥有明确 Contract ID，并通过OKF impact检查。

#### Task 2: 建立共享 TileMap表面和模板生成链

- 目标：用一个正式 `TileMapLayer`表面统一地图坐标、TileSet、拾取、覆盖层和地图边界。
- 输入：现有96×48投影、`IsometricBattleBoardLayout`、`GodotBattleBoardFitter`和现有生成器模式。
- 输出：`GodotIsometricTileMapSurface`、模板Resource、确定性Builder及首批营地/基础战斗模板。
- 涉及文件：Adapter Runtime/Editor、生成资产Catalog及GdUnit测试。
- 验收标准：
  - 100个cell center与既有 `IsometricGridProjection.GridToScreen`在同一局部坐标合同下逐格相等，边缘拾取仍确定性一致。
  - Builder连续运行两次所得Resource语义、UID和Catalog条目不漂移。
  - 生成模板包含地形/装饰/遮罩层以及全部必需锚点，且不存在运行时临时双色TileSet。
  - 资源仅由Builder写入，计划和实现不手写 `.tres/.tscn`。

#### Task 3: 交付真实开始营地

- 目标：用新地图表面交付第一个玩家可见垂直切片，证明不是缩略占位版。
- 输入：开始营地模板、全部Party候选和现有选三/起始技能事务。
- 输出：全尺寸营地、真实篝火、全部真实候选Actor、选择顺序和正式开始出口。
- 涉及文件：地图实例/运行控制器、Party Selection编排、模板资产和UI/GdUnit/Gameplay测试。
- 验收标准：
  - 实例化的 `GodotUnitActor`数量和Definition ID严格等于候选列表，测试覆盖超过四个候选时无数组越界。
  - 候选只占模板槽位，不允许移动；选择、取消、顺序和选择三人后出口解锁保持现有业务语义。
  - 角色、篝火和出口不依赖ID文本辨识；地图按1600×900安全区达到战斗尺度。
  - Start Camp Gameplay Spec通过真实Actor/出口输入完成组队，且生产存档隔离。

### Milestone 2 / PR 2：战斗棋盘 TileMap化与同图战斗基础

#### Task 4: 把普通战斗迁移到共享 TileMap表面

- 目标：删除战斗对自绘菱形 `Control`的依赖，同时保持全部战斗输入和表现语义。
- 输入：共享TileMap表面、BattleLayout/Encounter、现有单位/VFX/高亮/播放链。
- 输出：使用模板战斗槽的普通战斗Active地图，进入节点后立即开战。
- 涉及文件：战斗页面编排、TileMap表面、战斗呈现层及战斗GdUnit/Gameplay测试。
- 验收标准：
  - 战斗场景树包含共享 `TileMapLayer`，不再创建 `GodotIsometricBattleBoard`。
  - 玩家与敌人按模板槽和Encounter顺序生成真实Actor；投影、Facing、排序、Hover、目标选择、技能VFX和移动动画回归不变。
  - 进入普通战斗节点后没有探索输入或遭遇Label，战斗规则在地图聚焦完成后立即可用。
  - PendingBattle重建 Main后从遭遇入口重开；战斗失败仍只提交一次终局。

#### Task 5: 扩展精英、Boss与战斗残局

- 目标：让全部战斗类节点使用各自正式模板，并在地图集中留下轻量残局。
- 输入：精英/Boss Encounter、模板状态层、统一Settlement。
- 输出：普通/精英/Boss `active → combat → resolved`状态切换及不可回访投影。
- 涉及文件：模板资产、战斗结算协调、地图状态投影和回归测试。
- 验收标准：
  - 三类战斗模板分别验证玩家/敌方槽容量、阻挡和相机边界。
  - 胜利先原子提交奖励/节点结果，再切换尸体或清空状态层并解锁出口。
  - 失败后只显示Defeated终局；重启不能恢复战前或战斗入口。
  - 已完成战斗地图只保留Preview残局，无反向出口和运行控制器。

### Milestone 3 / PR 3：真实地图集、规划情报和相机

#### Task 6: 交付 Preview/Active 地图集生命周期

- 目标：把路线范围内所有节点组织为真实轻量Preview地图，并保证唯一Active实例。
- 输入：拓扑、Lane/Layer、模板映射、节点Intel状态和状态层。
- 输出：`GodotRogueMapAtlas`、地图实例Registry及Preview/Active切换。
- 涉及文件：Atlas/MapInstance/FlowProjector、Main编排和生命周期测试。
- 验收标准：
  - 地图集中每个拓扑节点恰有一个真实Preview TileMap，当前节点恰有一个Active控制器。
  - Preview节点没有 `GodotUnitActor`、AI、碰撞、交互信号、Process或动画播放器。
  - 激活/离开/重建Main后Actor、信号、临时节点和控制器计数无泄漏或重复。
  - 低性能模式只禁用远端装饰、遮罩动画和连线流动，不隐藏类型、拓扑、出口方向或已知情报。

#### Task 7: 替换圆点总览并交付地图集相机

- 目标：让地图集本身承担完整路线查看，退役独立圆点/连线View。
- 输入：Atlas世界边界、当前节点、徽标/连线锚点和1600×900安全区。
- 输出：拉远/拖动、全屏聚焦、返回当前地图及跨节点相机表现。
- 涉及文件：CameraController、PlanningOverlay、Main页面导航、旧RogueMapView及输入测试。
- 验收标准：
  - 地图集可拖动查看全部路线，节点Hover/点击只更新只读详情，不直接迁移节点。
  - 关闭地图集恢复当前节点聚焦与输入；战斗/阻塞事件期间地图集只读。
  - 出口提交成功后相机退远、沿连线、聚焦目标；中断后Reload直接聚焦已保存目标。
  - `GodotRogueMapView`及其测试/Runner目标在等价能力迁移后删除，无残余生产引用。

#### Task 8: 交付迷雾、徽标、有限情报和连线状态

- 目标：支持开局宏观规划，同时保护未公开的战术细节。
- 输入：NodeIntel、模板Preview层、Encounter类别和节点状态。
- 输出：Planning/TacticalPreview/Current/Completed遮罩、徽标、威胁和连线表现。
- 涉及文件：PlanningOverlay、FlowProjector、模板预览数据及状态/GdUnit/截图测试。
- 验收标准：
  - Planning显示类型、拓扑、入口方向和出口连接，但内部保持黑色。
  - TacticalPreview显示基础地形、阻挡、出口、对象类别剪影、敌族和威胁等级，不创建真实单位或泄露数量/出生格/奖励。
  - Current完整显示并可操作；Completed显示声明式残局且降低亮度。
  - 连线按黑暗短线、暗色完整线、可进入高亮流动线和已走稳定实线映射，截图锚点稳定。

### Milestone 4 / PR 4：非战斗节点、出口事务与节点级恢复

#### Task 9: 迁移休息、商店、宝箱及普通事件

- 目标：用正式模板和真实对象替换统一外围地图及Label对象。
- 输入：节点类型、模板入口槽、对象锚点、既有Rest/Store/Treasure/Event事务。
- 输出：各类Active地图、三名真实队员、只移动领队的探索Session及解决状态层。
- 涉及文件：AdventureRuntimeController、各模板、Main编排、Core/Application服务和旅程测试。
- 验收标准：
  - 三名队员按模板入口槽生成；只有领队可寻路，切换领队后从该角色当前内存格开始移动。
  - 对象交互只在领队相邻时触发；队友固定且不显示可移动反馈。
  - 商人、篝火、宝箱、祭坛和NPC使用正式对象节点/视觉，解决后切换对应状态层且不重复奖励。
  - 重建Main后保留Leader和节点结果，但三人按模板入口/战后槽重置，不读取旧移动格位。

#### Task 10: 交付目标绑定出口、常驻情报与原子迁移

- 目标：把多出口路线选择收口为可读、可取消、可幂等的节点迁移。
- 输入：直接后继、出口/目标入口锚点、ExitIntel、expected revision。
- 输出：出口常驻徽标/Tooltip、邻接确认对话框和原子迁移命令。
- 涉及文件：MapExitPresenter、RunAdventureTransitionService、SessionService、Save/Flow投影和Gameplay测试。
- 验收标准：
  - 每个出口只指向一个直接后继，连线端点与出口/目标入口锚点一致。
  - 解锁出口常驻显示目标类型、方向、威胁和已知情报；锁定状态解释原因。
  - 背景/近失点击不迁移；领队相邻后确认才提交，取消不改revision或存档。
  - 双击、重复回调和陈旧revision最多成功一次；任一验证失败不产生部分节点、奖励或发现状态。
  - 提交成功后创建目标节点状态并保存，随后才播放相机转场。

#### Task 11: 完成Save升级与节点级Continue恢复

- 目标：使退出/重启恢复符合已确认的节点级语义，不保存局部位置或表现。
- 输入：下一版Save、模板槽、Leader、节点/对象/发现/路线状态和遭遇checkpoint。
- 输出：向后兼容读取、归一化、Continue路由和恢复Gameplay journeys。
- 涉及文件：RunSaveDocument、Normalizer、SessionService、Main恢复及Application/Gameplay测试。
- 验收标准：
  - 主存档与backup原子写入、expected revision和幂等语义继续通过。
  - Save编码中不存在ActorCells、相机/动画/Tooltip/未确认弹窗字段。
  - 非战斗恢复到正确模板和状态层并使用预设槽；已提交奖励、库存和事件结果不重掷。
  - PendingBattle从同一Encounter/Seed/入口资源重新开始；Defeated只恢复摘要/Home。
  - Completed节点不能通过恢复、Atlas点击或构造反向出口再次激活。

### Milestone 5 / PR 5：事件原地战斗闭环

#### Task 12: 交付事件前/战斗中/战后同图生命周期

- 目标：让诅咒宝箱、祭坛守卫和护送伏击在事件原TileMap原地开战并恢复残局。
- 输入：事件模板探索/战斗槽、事件上下文、Encounter、NPC状态和对象变体。
- 输出：探索锁定、队伍重定位、敌人生成、同图Battle控制器和战后恢复。
- 涉及文件：事件模板、Adventure/Battle协调器、Settlement、Save和Gameplay journeys。
- 验收标准：
  - 领队相邻并确认后锁定探索、对象、出口和地图集；队伍重定位到战斗槽且无战前走位。
  - 同一TileMap和相机保留；敌人/NPC按模板与Encounter生成，战斗层不创建第二棋盘。
  - 宝箱拟态、祭坛守卫和护送NPC分别保留正确语义；战中Reload从事件遭遇入口重开。
  - 胜利提交一次奖励/结果，切换已开启/净化/NPC安全及尸体状态，再解锁出口。
  - 任一事件失败统一进入终局，不恢复触发前探索。

### Milestone 6 / PR 6：门禁、证据、人工验收与收尾

#### Task 13: 补齐四层自动门禁和统一验证入口

- 目标：让功能、结构、真实输入、截图和生命周期缺陷都能阻止错误合并。
- 输入：前五个里程碑的合同、场景和旅程。
- 输出：Core/Application、GdUnit、Gameplay Spec、结构截图/性能证据和Verifier/CI集成。
- 涉及文件：各测试项目、GameplaySpec、截图证据工具、Verify脚本和Godot CI workflow。
- 验收标准：
  - Core/Application覆盖发现、原子迁移、Save归一化、失败终局、不可回访和事件结果幂等。
  - GdUnit覆盖真实TileMap、投影等价、模板锚点、真实Actor、Preview/Active唯一性和零临时节点泄漏。
  - Gameplay Spec覆盖营地、地图集拖动/聚焦、多出口常驻信息与确认、三类战斗、三类事件战斗、Main重建和终局。
  - 固定Seed/窗口截图验证地图边界、当前占屏、徽标、遮罩和出口信息稳定锚点，不使用全屏逐像素Golden。
  - 性能证据记录Preview/Active/Actor/Process/碰撞计数及固定场景采样；慢设备降级不影响规划语义。
  - `Tools/godot/Verify-GodotProject.ps1`和相关CI调用新生成/验证/旅程入口，失败退出码可追溯到具体层。

#### Task 14: 人工验收、知识迁移与计划收尾

- 目标：把自动完成与真实视觉/手感验收分开记录，并清理活跃计划。
- 输入：完整自动证据、固定截图和用户当前失败反馈。
- 输出：人工账本、长期设计/缺口/OKF更新及完成计划删除。
- 涉及文件：manual acceptance、known gaps、grid/atlas合同、OKF scope及本计划。
- 验收标准：
  - Tile Readability与Start Camp记录“缩略占位版”用户失败来源；修复后状态为pending复验而非passed。
  - canonical Editor清单覆盖地图尺度、角色/对象辨识、Atlas拖动/聚焦、遮罩/连线、出口确认、事件同图战斗、转场、帧 pacing和真实Assembly Reload后Continue。
  - 自动门禁结果与人工 verdict分别记录，任何未执行人工项保持pending。
  - 按 `project-doc-organization` 将长期结论并入 `.agents/docs/`，真正未完成项进入统一缺口或经用户批准建立新计划。
  - 运行OKF impact/sync/test后更新受影响scope；全部实现和验收完成后删除本计划，由Git保留历史。

## Manual Acceptance Checklist

分阶段人工验收操作已独立维护在 [Roguelike Map 真实 TileMap 人工验收 Checklist](../docs/roguelike-map-manual-acceptance-checklist.md)。实际 verdict 仍只写入 [人工验收账本](../docs/manual-acceptance.md)。

## Test Plan

每个PR先运行直接受影响的最小测试，再运行对应扩大门禁；最终PR运行统一验证。实施者应从仓库脚本读取精确参数，不自行绕开隔离或Editor生命周期。

- Core：`dotnet test src/Tactics.Core.Tests/Tactics.Core.Tests.csproj`。
- Application：`dotnet test src/Tactics.Application.Tests/Tactics.Application.Tests.csproj`。
- Godot C#编译与GdUnit：通过 `Tools/godot/Verify-GodotProject.ps1` 中现有隔离入口执行；同一worktree的Editor、Verifier和GdUnit串行。
- Gameplay Spec：先validate/compile受影响spec，再运行开始营地、地图集/出口、战斗、事件战斗、Reload/终局定向journey，最终运行统一批次。
- 生成器：在隔离副本或受测Builder流程中验证ResourceSaver/PackedScene输出与Catalog幂等，不直接改写tracked资源做试验。
- 静态检查：精确staged scope、`git diff --cached --check`、无任务外 `project.godot`、无旧 `GodotRogueMapView`/`GodotIsometricBattleBoard`生产引用。
- OKF：若修改受监控scope，执行 `python Tools/okf/catalog_impact.py report --worktree`，更新权威正文后执行对应 `sync --worktree --scope <scope> --write`及OKF测试/bundle。
- 人工：只用 `Tools/godot/Open-GodotDev.ps1` 打开canonical项目；按更新后的manual acceptance逐项验收，截图/短视频和Output作为失败证据。

## Requirements Traceability

- 决策1、6、15 → Tasks 6–8：真实多图地图集、聚焦/拖动和Preview/Active。
- 决策2、4、5 → Task 8：开局规划、有限情报、徽标/实景双表达。
- 决策3及出口常驻修正、11 → Task 10：目标绑定、常驻信息、确认、原子提交和纯表现转场。
- 决策7 → Tasks 4–5：无战前探索、三类战斗进入即开战。
- 决策8 → Tasks 3、9：动态营地槽、全部候选真实Actor、只移动领队。
- 决策9 → Tasks 1–3、5、9、12：整图模板、声明式状态层及生成器。
- 决策10 → Task 12：事件原TileMap战斗与残局。
- 决策12 → Tasks 1、11：节点级恢复、槽位重置、遭遇入口重开。
- 决策13、14 → Tasks 5、11、12：失败终局和禁止回访。
- 决策16 → Tasks 13–14：四层自动门禁、稳定截图和人工账本。

## Risks and Open Questions

- TileMapLayer迁移可能改变共享边缘拾取、单位脚底锚点、VFX坐标和Canvas变换；PR 2必须在删除旧Board前证明逐格投影与完整输入回归。
- `GodotPlayableRunMain`当前职责过大；拆分必须逐切片迁移，禁止一次性重写导致现有Run路由难以审查。
- V10 Save含ActorCells而新版本不再保存位置；升级必须保留业务结果并明确旧进行中战斗只恢复到遭遇入口。
- Atlas中全量Preview虽轻量，但Godot TileMap draw call、Canvas排序和遮罩仍可能形成峰值；以实测证据决定只关闭非语义装饰，不改变已确认加载模型。
- 事件模板同时承担探索和战斗，出生槽、对象和阻挡容易冲突；生成器必须做连通性、容量和状态层一致性验证。
- 正式篝火、商人、宝箱、祭坛、NPC、出口和环境构图依赖可审计项目资产；若现有资产不足，当前PR只可使用项目自有、可发布且非文字占位的确定性视觉，不得越权生成或引入来源不明素材。
- 截图证据受字体、GPU和渲染器影响；只断言结构区域和稳定锚点，视觉质量保持人工门禁。
- `project.godot`当前任务外dirty修改必须在每个PR preflight和staged review中显式排除。

## Handoff Notes

- 开始前先读 `.agents/rules/godot-agent-workflow.md`、`.agents/docs/isometric-grid-anchor-contract.md`、本计划及 `.agents/docs/manual-acceptance.md`。
- 首次执行先记录 `git status --short`，确认并保护任务外 `godot/project.godot`和未跟踪session文件；始终使用显式 `git add -- <paths>`。
- PR 1先建立失败测试和新合同，再运行Builder；不得先编辑tracked `.tres/.tscn`。
- 任何C#、ResourceSaver、生成或reload-sensitive工作都遵循 `godot-editor-lifecycle`，正常关闭并恢复Editor；禁止强杀。
- 每个里程碑独立review、局部验证、统一相关门禁和manual handoff；后续PR只建立在前一PR已合并或明确基线之上。
- PR/merge前检查远端目标ref和现有PR状态；目标不存在时不得自行重建。
- 完成所有实现与验证后，按 `project-doc-organization`：合并长期结论、登记真实缺口、更新OKF、删除completed plan，由Git保存历史。
