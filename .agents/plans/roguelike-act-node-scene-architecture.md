# 单 Act Demo：全节点常驻 TileMap 与统一节点宿主架构改造计划

## 执行进度（持续更新，不代表整计划完成）

- 第8轮由正式launcher启动Editor PID19188（started，MCP readiness未验证）；第9轮复核已无Godot进程，旧PID关闭命令报告不存在。未强杀/另起实例；本轮按初始关闭状态保持关闭，验证后单独构建production Adapter，下一轮重新核对。
- Task 1 部分：新增纯Core Act/Node定义、完整模板映射/身份/策略/双向可达与Demo DAG校验；ActId遵循ContentId小写连字符，使用 `act-01`。已接入Main的准备阶段与营地借用模式；战斗/Adventure同Surface控制器仍待迁移。
- Task 2 业务前置：Setup最终技能一次CAS保存PendingBattle与首战checkpoint，成功才清草稿/发请求；修复FullRunTransition保存失败仍发遭遇请求。已通过真实最后技能按钮路由测试，无InitialAdventureBoard。
- 新Run首战随机初态采用单CAS revision基线，与旧两阶段创建不同；旧PendingBattle不修改。已补V11真实编解码后checkpoint/party/RNG语义比较。
- 首轮定向证据：Core 182/182、Application 197/197、UI 45/45；此前已通过Core 193/193、地图9/9、常驻营地2/2、UI 47/47（含加载中abandon晚回调、同seed草稿取消隔离、真实末技能按钮保持同Host/Surface）。TestHost build 0 warning/error。UI仍报告4个孤儿节点警告，未宣称零泄漏。
- Gameplay起始技能旅程已改为等待battleReady并由compiler生成plan；完整Gameplay批次尚未执行。
- 完整verifier使用已安装GUI executable显式参数运行（默认console executable缺失）：第11轮完整门禁复验Core 231、Application 388、FrozenOracle 15通过；仍在 `test_public_root_reconstruction_preserves_ignored_tracked_files` 因临时source缺 `Tools/public-release/Verify-PublicRelease.ps1` 停止，未达到后续完整Godot/Gameplay阶段。此门禁问题没有被修复或绕过。
- 合同validate/coverage有效，但部分新目标仍missing-spec/unsupported；OKF 16 tests及bundle通过。
- 最新复验：第11轮Core231、Application388、目录2、UI49通过（4 orphan warning仍在）；第7轮目录缓存1、UI47连续两次独立进程通过。此前隔离Godot宿主9、浅水3、全Act模板7、常驻营地2、地图9通过；OKF16项与bundle有效。修正了无上限headless帧率下以帧数等待Tween和在ProcessFrame信号处过早检查QueueFree的测试时序，保留原行为断言。
- 待继续：同Surface战斗/Adventure、战后与事件状态、旧创建入口/InitialExploration迁移及V12；完整Gameplay/性能/人工门禁。生成器修改保护拒绝/回滚故障注入尚待完整覆盖。不得把当前切片写成整计划完成。

### 常驻宿主与生产营地切片（未完成同图战斗）

- 新增 `GodotActWorldHost`：增量注册正式节点、全Act出口/目标入口/终局交叉校验、唯一Active租约（宿主InstanceId+RunScope+NodeId+generation）、释放时失效所有租约。扩展MapInstance：Preview禁用Process，owned地图禁止绕过宿主激活或重配。
- 新增 `GodotActLoadCoordinator`：主线程分帧构建、4ms预算目标、进度/取消/失败；进度回调QueueFree宿主或loader后不得发Prepared。Main已接入正式资源读取、加载页/重试和独立于_page的Host；IsReadyForInput等待准备完成。
- Core/Resource新增向后兼容 `IsTerminal=false`：终局必须零出口/连线/Exit对象；探索入口槽必须可达，防止恢复时落入孤岛。
- 工厂新增纯 `CreateNodeTemplate`：显式真实后继出口、按类型对象、正式Encounter/Layout战斗槽、最终Boss无假出口。Build已通过ResourceSaver生成22个正式变体，local catalog保留legacy2合计24项；staging、SHA修改保护、异常回滚和最后写ledger，人工状态manual_qa_pending。当前Run只加载其16个选定模板而非全部22变体。
- 浅水已实现：Core/Resource保留真实N2水格（可走、不改blocked），Surface绘制atlas5；缺少水atlas时在清理旧Surface前失败。新TileSet工厂为六格，旧无水资源仍兼容；浅水3/3、模板6/6、宿主9/9隔离进程测试通过。
- 新增纯Core `PureRunActBlueprint`：复用旧seed选择，当前Act选16个节点模板；构建期枚举22个静态内容变体（包含晚战e1/e2与三种事件），不靠随机采样找内容。工厂新重载和 `CreateActTemplates`读取canonical catalog，Mystery显式绑定事件与n1遭遇，N4按现有正式payload→split-flank规则转换。全16节点交叉准备测试已通过；Main营地借用现有地图，不再生产PlanningPreview替身。成功Setup通过明确提交结果收养原Host，取消同seed草稿/终局/退出释放旧Host；加载迟到回调复核页面与持久Run，StartBattle拒绝陈旧checkpoint。
- 曾一次组合测试进程以 `-1073741795` 退出且外层testhost未结算，已停止该等待进程；隔离宿主9项通过。尚无可靠根因，不将一次重跑通过写成引擎问题已修复。
- 生产接入审计：StartCamp.Configure/ConfigureRoutePreviews每次造新地图；AdventureBoard另造TileMap并使用不同局部变换；Main.CreatePage释放页面，故ActHost必须是Main直属稳定子树、地图背景不得遮挡它。禁止把host挂到_page或StartCampAtlasWorld后宣称常驻。
- 下步优先：Adventure/Battle输入与表现借用同一Surface，显式节点状态/结算/事件原图控制器与V12恢复。当前营地→技能→首战入口已保持原16张地图，但战斗画面仍用旧board，不能声称同图战斗/战后探索已完成。
- UID生成诊断（本机4.7.1 Runtime）：最小GDScript probe证明GetResourceUid可能返回旧正缓存或-1，不能用作磁盘落盘证据；只读解析ResourceSaver写出的头部验证UID，绝不手改资源文本。修复后独立Editor扫描+Runtime目录UID测试通过，重复生成ledger（含每个target SHA与semantic）字节一致。早期user:// staging产物无UID的问题已由受控重新生成纠正；不要再根据内存注册表宣称UID已落盘。

### 第6轮：借用战斗棋盘组件（未接Main）

- `GodotIsometricBattleBoard`新增租约约束的借用模式：不重绘/清理底图，不改Surface变换，只画高亮与当前角色标记。跨父节点及CanvasLayer使用Viewport变换；不同blocked/water布局拒绝，旧页退出不释放地图，hover外部回调后重新检查绑定与租约。
- `ResidentBattleBoardGodotTests`4项通过：100格投影/拾取、实例与TileMap格不变、陈旧输入与旧页退出隔离、布局冲突、回调重绑定防线。旧棋盘24项及独立UI47项重跑通过，但曾出现空loader反射失败后原生runner退出；已加入诊断并记录 `.agents/incidents/godot/gdunit-native-illegal-instruction.md`，根因未确认，不能宣称该偶发问题已修复。
- Main接入前的必要前置已审计：真实出口链会写目标Board，但直接BeginLayerFive/BeginBoss保留旧Board；事件Board为可复用context且n1跨节点复用；普通结算推进Encounter但仍在原节点。不能把CurrentNodeId启发式或Encounter唯一反查接到Host。
- 下一个业务切片：持久节点身份进入checkpoint/request，普通/Elite/Boss入口在同一次CAS绑定目标；事件交叉校验PendingEventNodeId/选择事务。Resume和V11迁移须保留旧checkpoint revision、AdventureRevision及RNG；结算显式保留当前节点Resolved。随后Main才按已校验NodeId绑定该组件。Actor/VFX宿主与战后残局仍待迁移。

### 第7轮：节点身份/V12编码前置（生产Store仍V11）

- 新增`RunNodeSceneIdentity(ActId,DefinitionVersion,NodeId,TemplateId)`；当前Adventure与checkpoint各持一份，Resume请求透传checkpoint身份。旧serializer忽略字段以保持旧wire，V11编码拒绝新身份静默降级。
- `RunSaveDocumentV12`显式保存/校验身份一致性、原hash，复用旧字段兼容链；V10/V11有效ActiveRun只标`RequiresNodeMigration`，不猜节点、不改revision/RNG。尚未实现旧档定位与所有新入口绑定，未切换生产Store，也没有完成Task7整体。
- Review的两个继承性问题已补回归：非法party空项由领域/normalizer拒绝，不再漏出空引用异常；旧schema8/9清除ActiveRun时V12保留RequiresNewRun，避免静默消失。
- Core193、Application241（新增44项）通过。TestHost build无warning/error。UI回归再次暴露`act.catalog_missing`后原生退出：Act目录/模板现已复用Canonical RequiredResourceLoader（CacheMode.Ignore及路径/类型诊断），缓存替身测试1项通过，完整UI47项连续两次独立进程通过；原生退出根因仍observed，不能仅凭重跑宣称修复。原有4 orphan warning保留。
- 下一切片：PureRunNodeSceneProjector明确旧PendingBattle/Resolved/Initial的身份映射及冲突拒绝，再统一所有普通/Elite/Boss/事件入口的一次CAS绑定、生产V12 Store和Main同Surface接入。不得让任意旧Board字符串或复用Encounter取得地图控制权。

### 第8轮：Pending只读投影与内存归一化（尚未接生产）

- 新增`PureRunNodeSceneProjector`与`PureRunPendingSceneRecovery`。按checkpoint index、绑定内容、事务/选择/Map和事件证据定位；实际Pending可残留Initial，不能混用Ready+Initial首战新建规则。恢复只补身份/正式Board/MapActive并清过程格位，所有revision、checkpoint内容与RNG不变，失败返回原Snapshot。
- 新增89项Application回归，总330；Core193/FrozenOracle15通过。完整verifier再次止于同一Windows public-root fixture缺Verify-PublicRelease.ps1，未绕过。Review补了unsupported合法Core拓扑的NotSupportedException归一化，保留原快照。
- 真实旧UI兼容缺口：Main L4固定Cursed/L6固定Altar，与seed默认assignment可能不同；缺marker不能补造。现在分别显式报event_variant_incompatible/event_state_incomplete并保留原数据，不宣称全部旧Pending可迁移。生产仍V11，Main尚未借用同Surface战斗。
- 后续必须区分默认节点模板与实际触发的事件/护送阶段，先落实版本化实际进入绑定，再统一新入口CAS与V12 Store。不能简单替换Main两处字符串：无EscortState时LostVillager的BeginBattle会失败。

### 第9轮：生产启动请求校验（仍为V11过渡）

- 新增`PureRunEncounterRequestValidator`，校验当前checkpoint/Run/Encounter、AdventureRevision、完整Party值和三端身份。全null身份明确保留V11路径；已绑定必须通过Act/模板投影，失败不启动。
- Main启动与Prepared/Pending路由统一使用Application Resume的零写修复投影；不能拿已修复请求去比较原始Store队伍。护送配置使用同一投影，失败Load即使带Snapshot也拒绝。Adapter请求helper透传checkpoint身份。
- Review发现并修复真实旧队伍零属性/缺StartingSkill导致的误拒，复核无残留确定问题；真实UI回归覆盖cold准备回调与warm同Host重启、源对象不变、零Save/RNG不变，另测伪造随机修订与失败Load。
- 新增33项Application（363总）及2项UI（49总）通过；production Adapter最后独立build 0 warning/error。完整门禁仍止于既有public-root fixture缺脚本，不声称全绿。没有切生产V12，也尚未完成同Surface战斗。

### 第10–11轮：实际Pending变体恢复与目录加载（未切生产）

- Core新增`PureRunActBlueprint.Restore`和只读恢复证据：仅接受当前22有限变体、正确Act/version/node/template，覆盖明确身份，不重抽默认选择、不改MapState。新增38项Core，总231通过。
- Recovery从完整实际事件marker/checkpoint/事务推导单节点候选，Projector核全部证据；默认assignment保持原值，实际context核恢复binding。已提交Mystery前驱的已知resolved alias不再误用seed默认事件拒绝Elite/Boss。V12往返与请求校验保持实际模板及全部revision/RNG。Application新增25项，总388通过。
- Review修复护送双向约束：BattlePending必须当前Lost、目的一致、NPC存活；不能将已死NPC恢复成活NPC，也不能让Cursed/Altar夹带待战护送。Completed/Traveling历史仍可保留。
- `GodotActContentCatalog.Load(blueprint)`加载恢复后的16个正式模板，不重新按seed覆盖选择；目录2项、模板7项、UI49项通过。Main仍未调用恢复重载，不能声称生产事件迁移完成。
- ResourceSaver正常刷新生成源码证据，两次生成ledger（含target hashes/UID/semantic）字节一致；随后完整verifier执行fresh Editor扫描。完整门禁Core231/Application388/FrozenOracle15通过，仍在同一public-root fixture缺脚本处停止，未绕过；UI仍4 orphan warning。
- 后续剩余：生产原子节点进入/持久身份、Ready/Resolved与事件结算中间态迁移、生产V12及Main同Surface；护送Offer/Travel和历史全节点残局未实现。当前恢复蓝图不等于全部历史节点状态。

### 第12轮：普通/Elite/Boss生产同Surface接入（事件与残局仍待）

- Main启动战斗先验证原请求，再用Pending Recovery从checkpoint/事务证据取得节点身份并复核绑定请求；不按Encounter反查节点，也不把内存身份写回V11。Mystery事件暂保留旧棋盘，避免把未完成事件阶段伪装成同图闭环。
- 普通、Elite和Boss在创建战斗前校验宿主Act/version/node/template及实际Encounter布局的blocked/water；失败保持旧页、旧battle和lease不变。成功后只激活对应节点并借用同一Surface，不重配、重建或释放terrain。
- 新世界相机仿射把Surface格点映射到既有`IsometricBattleBoardLayout`局部坐标，复用现有Actor、输入、VFX与数值层；Surface、node及terrain自身Transform和用格保持不变。CreatePage先停用旧lease，旧view退出不释放宿主。
- 新增4项生产呈现回归：N1、Elite、Boss冷/热路由，以及同TemplateId但runtime blocked/water失配拒绝。覆盖非零Position/非等比Scale、100格正反投影、16地图实例/terrain/atlas不变、唯一Active、零额外Save。组件4项与UI49项同时通过，仍报告既有4 orphan warning。
- Production Adapter 0 warning/error；完整verifier再次通过Core231/Application388/FrozenOracle15，仍在同一`test_public_root_reconstruction_preserves_ignored_tracked_files`因临时source缺`Verify-PublicRelease.ps1`停止，未执行后续完整Godot/Gameplay阶段，也未绕过该问题。
- 本切片不包含事件原图战斗、页级Actor/VFX长期归属、战后Resolved残局或失败/胜利完整Run；不得据此标记Task3/6完成或人工验收通过。

### 第14轮：Pending事件战斗使用正式Surface（探索与结算仍待）

- Projector现在按事件context精确校验对象身份：Cursed→`cursed-chest`、Altar→`fallen-altar`、Lost→`lost-villager`；大小写、空白和错误对象均拒绝且不改存档或随机状态。
- Act Host的“呈现内容计划”与持久恢复证据分离：Setup/Ready继续显式准备现有生产策略的L4 Cursed和L6 Altar；冷Pending只用Recovery已证明的当前节点身份覆盖该节点。它不修改`MysteryEventAssignments`，也不把未来节点呈现覆盖交给Projector当作存档事实。
- Mystery Pending在Host模板与实际身份一致时借正式Surface；同模板还必须通过事件对象、对象类型、对象格(5,5)、有序玩家/敌人出生槽及blocked/water校验。已准备Host与实际Lost变体不一致时保留legacy战斗板，不热重载或重配常驻地图。
- 新增4项生产回归：Cursed、Altar、Lost冷恢复（含Lost存活保护NPC）及warm匹配/不匹配过渡；事件4项、普通/Elite/Boss4项、UI49项通过。Production Adapter构建0警告/0错误；完整verifier通过Core231/Application391/FrozenOracle15后，仍在既有public-root临时source缺`Verify-PublicRelease.ps1` fixture停止，未绕过。
- 当前只是Pending战斗阶段使用正式Surface；事件探索仍是临时AdventureBoard（对象格7,5且外围阻挡），探索→战斗InstanceId不变、Offer/Travel、结算中间态和Resolved残局均未完成。

### 第15轮：匹配事件的探索→战斗共享正式Surface

- `GodotAdventureBoardView`新增resident模式：保留owned TileMap作为legacy后备但隐藏且不写格；坐标拾取、fit和标记通过完整canvas仿射映射到Host Surface，只改Host相机变换，不改MapInstance/Surface/Terrain内部变换或正式用格。
- 事件探索在已准备Host的binding/context匹配时使用正式template Board、正式对象格(5,5)、blocked/exit与PartyEntrySlots；warm变体不匹配继续legacy，不热重配Host。
- 探索View持有generation lease fence；页面切换先取消移动再失活旧lease，迟到输入/移动完成不得保存或触发业务。进入Pending战斗重新激活同节点，正式Surface InstanceId保持不变。
- 生产回归新增Cursed warm探索→真实对象点击→Pending战斗同Surface，并覆盖旧View延迟销毁窗口零额外保存、16地图内部身份/变换/用格不变。此轮当时尚未覆盖Lost cold Ready探索计划；该项已在第16轮补齐，事件战后结算及Resolved残局仍待。

### 第16轮：持久Traveling证据驱动Lost冷探索呈现

- 冷恢复处于Resolving L4/L6时，Host呈现计划可依据已有Traveling escort覆盖目的Mystery节点为Lost模板；要求alive、目的节点/未提交事务/map Selected三元一致、transaction key未应用、map身份与前驱合法、phase层级与定义一致，以及Quest/Accepted节点结构有效。失败安全保留baseline/legacy，不抛猜测性变体。
- 该覆盖仍只是Adapter呈现计划：不修改`MysteryEventAssignments`，不写存档，不交给Pending Projector作证据，也不补造Accept/BeginTravel。进入战斗后仍由正式BeginBattle和Pending Recovery证明Lost身份。
- 生产回归覆盖L6 Traveling冷恢复→Lost正式探索→对象点击→同Surface Pending战斗及保护NPC。Offer/Travel生产入口、结算与Resolved残局仍待迁移。

### 第17轮：匹配事件的warm战后场景回到正式Surface

- `ShowPostEventScene`在现有Host binding/context与结算事件一致时重新激活同一节点并借用正式template Board、对象格(5,5)、出口和PartyEntrySlots；探索、战斗、warm战后页面保持同一Surface实例。模板不匹配继续legacy，不热重配。
- Resolved状态仍由现有Adventure/Escort状态派生（宝箱Defeated、祭坛Purified、村民Safe/Down），本轮不更改奖励或护送裁决，不改正式Terrain/Surface内部状态。
- 生产回归新增Cursed Resolved页重用正式Surface。当前事件结算仍由ApplyLayerFourBattleResult、可选Escort.ResolveBattle、ResolveEventBattle分2—3次CAS保存；save#1后已提交节点却仍有Pending marker，Lost save#2后重试还会因Escort不再BattlePending而失败，重启路由也不会续跑，属于既有P0恢复缺口。冷启动Resolved身份恢复、失败中间态归一化和真正原子结算仍未完成，下一切片优先处理。

### 后续事件策略审计（只读发现，部分Pending呈现已实现）

- Tile事件实际PendingEventContext控制结算/护送分支；通用战斗奖励不读取MysteryEventAssignments。默认assignment还影响旧Mystery菜单与下一层排除池，禁止为了修复实际context矛盾而改assignment。
- 当前未找到Escort.Accept/BeginTravel生产入口，只有测试注入；Core Accept不校验拓扑。先分清Offer/Travel/Ambush，不能把seed的LostVillager直接当当前节点战斗。
- 原事件结算分ApplyLayerFour、Escort.Resolve、ResolveEventBattle多次保存，存在中间快照；Ready/Resolved迁移不能一概视为事件已完全处理。完整事件变体策略与生产接入仍待实现。

### 第18轮：缺口审查与 S1 Core 节点状态机（未接调用方，未持久化）

- 更正：事件结算已由 `PureRunSessionService.ApplyEventBattleResult` 在内存合并后单次保存，Main 事件分支已调用；第17轮“2—3次CAS”只描述历史存档中间态。普通/L4/L5/L6 胜利仍为结算 + `ResolveAdventureBoard` 两次 CAS。完整缺口见 [缺口审查](../docs/roguelike-act-tilemap-gap-review.md)。
- 用户确认顺序：先状态骨架、后页面；新节点状态取代 `.resolved` 与 `PureRunPhase` 的权威地位；V12 生产切换提前到 S2。
- S1 新增 Core `PureRunActState`/`PureRunNodeSceneState`/`PureRunNodeScenePhase`、`PureRunNodeSceneTransitionService`（Start/Enter/BeginCombat/Resolve/Fail/CanExit/ImmediateSuccessors，拒绝码不抛业务异常，重复调用标记 duplicate）、`PureRunLegacyActStateProjector`（唯一允许解析旧 Board 字符串处，矛盾即拒绝）与 `PureRunPhaseConsistency`。
- 未给 `PureRunState` 加字段、未改任何调用方或存档；新状态尚非权威。Core 263/263（新增32），Application 396/396；本机缺 global.json 的 SDK 9.0.312，本轮用已安装 SDK 10.0.302 直接执行，正式门禁仍需 9.0.312。
- 2026-10-22 决策更正：结算已提交但 Board 未 `.resolved` 的历史中间态由推导器拒绝（`act_state.inconsistent_resolution`），不再按 Resolved 接受。开发期读档遇到 `act_state.inconsistent_resolution` 即清档并提示（其余推导拒绝因仍有旧合法形态未覆盖，暂不清档）（`run.reset_stale_save`）；最小化重置策略留待发布前设计。

## Summary 与批准范围

将当前页面切换式 Pure Run 改为 Run → Act → Node；当前 Act 的全部正式节点地图初始化时构建，Preview、探索、战斗、战后使用同一地图宿主。用户已确认架构决策，本计划已通过计划审批。当前正在按切片实施，以上执行进度为最新证据；不把计划批准或自动测试通过当成人工验收完成。

本计划是 [原 TileMap 父计划](roguelike-map-real-tilemap-atlas.md) 的架构修订与执行入口。两者冲突时，以本文的单 Act 范围、同宿主生命周期和任务顺序为准；父计划的内容覆盖和人工验收要求仍保留。

### 已确认决策

1. 完整 Run 架构支持多个 Act；本次 Demo 只配置、制作、测试一个 `act-01`。
2. 初始化只加载当前 Act 的全部正式节点地图，不加载全部内容库或未来 Act。
3. 当前 Act 内切换不重建目标底图；Preview/Active 是同一个实例的模式。
4. 普通/Elite/Boss 进入即战斗，没有临时探索图。
5. 特殊事件先探索正式地图，触发后在原图切换战斗状态。
6. 非最终战斗胜利后原图结算、战后探索，通过出口继续。
7. 最终 Boss 完成结果处理后进入 Run 胜利摘要，不制作下一 Act 出口。
8. 已离开的节点不可回访，但保留只读地图和残局。
9. 至多一个节点持有玩法控制权；常驻、可见、运行是独立维度。
10. 暂不合入其他分支；先完成 Demo 功能、Play Test 和人工验收。

## Current State：必须纠正的上下文

- `GodotPlayableRunMain.ShowInitialAdventure()` 仍创建用户截图中的 `PURE RUN — TILE ADVENTURE`。
- `CreateInitialAdventureBoard()` 硬编码 `exit:layer_01_battle`。
- `RunAdventureTransitionService.CreateInitial()` 创建 `InitialExploration`；Application 创建 Run 仍调用它。因此此前“战斗直接进入已完成”的结论遗漏开局路径。
- `CurrentNodeId()` 从 Board Content ID 字符串解析节点；`ResolveBoard()` 使用 `.resolved` 后缀。新状态机不能继续依赖这些推断。
- `GodotAdventureMapInstance` 已拥有共享 Surface，但 Activate/Deactivate 只改枚举，尚没有控制器、输入与资源停用约束。扩展它，不另造平行宿主。
- V11 已剔除 ActorCells/表现状态；Setup 是进程内草稿。保留节点级恢复原则。
- 复用现有真实 Actor、逐格移动、格中心改道、出口自动接近、Pause、Facing 和 BattlePresentationPlayer。
- 历史基线 HEAD 为 `5d1a73c1`，任务外 `godot/project.godot` 曾为 dirty；实施前重新核查，不把历史状态当当前事实。
- 历史定向测试通过不代表当前所有流程正确。完整 verifier 曾受公共发布脚本缺失阻断，实施时重新确认。

## Scope / Permissions

### In Scope

Run/Act/Node 契约；单 Act 映射；全量初始化；唯一节点宿主与 Active lease；战斗 TileMap；地图集；正式非战斗节点；事件原图战斗；原子迁移/结算；V12 与旧存档兼容；自动测试、性能和人工验收。

审批范围包括直接相关 Core/Application 公共契约、Adapter 运行时、现有地图生成器模板输出和对应测试。资源只能通过现有受测生成器、ResourceSaver/Editor API 产生。

### Out of Scope

第二个 Act、跨 Act Loading 产品流程、无限世界/动态卸载、回访、远端 AI、队友跟随、逐行动存档、无关战斗规则与角色平衡、新美术音频生产、工具链重构、插件升级、程序集重组及其他分支 merge。

## Relevant Context 与架构

### Core：定义、状态、身份

新增 engine-neutral：

- `PureRunActDefinition`：ActId、现有拓扑、起终点、节点定义集合。
- `PureRunNodeSceneDefinition`：NodeId、TemplateId、进入策略、遭遇/事件引用和地图状态映射。
- `PureRunActState`：ActId、定义版本、CurrentNodeId、节点状态集合。
- `PureRunNodeSceneState`：NodeId、阶段、访问记录、地图状态键、事件结果、遭遇入口引用。

进入策略为 Exploration 或 ImmediateCombat；营地继续由 Setup 草稿驱动。

阶段：`Unentered / Exploring / InCombat / Resolved / Failed`。是否已离开单独记录；Resolved 当前节点仍允许战后探索，离开后才只读。Preview/Active、可见性、加载进度不进入业务存档。

现有 PureRunPhase 暂作兼容接口，通过单一映射器派生/校验；禁止新旧状态分别修改。现有层数专用服务继续承载 Demo 规则，由 Application 统一适配，不重写整个结算系统。

### Application：唯一事务入口

节点进入、事件开战、离开命令携带 RunId、ActId、NodeId、expected revision、幂等 key。

`输入意图 → 验证 → Core 转移 → CAS 保存 → 场景投影 → Adapter 表现`

- 离开要求目标为直接后继、未访问、当前允许离开、无阻塞事务。
- 普通战斗进入在同一事务保存目标、InCombat 与 checkpoint。
- 事件战斗在同一节点保存触发结果、InCombat 与 checkpoint。
- 奖励、事件结果、Resolved/Failed 一次提交；提交成功后再开放出口和显示残局。
- 相机没有业务权威；转场中断按保存目标恢复。
- 准备失败不提交；CAS 失败保留旧状态；提交后表现失败锁输入并由快照重建，不能重复发命令。

### Adapter：统一宿主

`GodotActWorldHost` 拥有 `(ActId, NodeId)` 注册表、布局和唯一 Active lease。

扩展 `GodotAdventureMapInstance`：Surface、静态对象/状态层、Actor/VFX 宿主、锚点、控制器挂接点、成对激活/暂停/停用/清理和 generation fencing。

探索控制器复用 MovementPlayer 与最终单次提交；战斗控制器复用 BattlePresentationPlayer 与业务。

非当前节点关闭玩法 Process、AI、交互、碰撞和运行 Tween；Visible=false 不等于停用。残局使用静态状态层/视觉，不保留运行中战斗 Actor。

Main 只保留启动、全局 Pause、库存/结算 overlay 与 Session 编排；打开 overlay 或 Overview 不卸载 Act。

### 初始化与加载

1. 固定 seed/内容版本确定 Act 拓扑、模板、节点种子。
2. 去重加载模板、TileSet、静态对象及本 Act 必需角色/战斗资源。
3. 主线程按时间预算分帧构建全部正式宿主，显示进度并允许取消。
4. 校验覆盖、槽位、出口、模板和遭遇容量。
5. 准备当前节点动态实例；全部静态地图准备完才开放操作。

营地属于当前 Act；Setup 完成只建立 Active Run 并激活首战，不重建 Act。取消草稿、新 Run 或放弃释放旧宿主。

Act 内点击节点后禁止同步读取模板或构建底图；动态 Actor 提前准备再激活。资源缺失显示明确 NodeId/TemplateId，不退回占位图。预加载不能保证 GPU 上传/首个特效无卡顿，必须测量。

### V12 存档与恢复

复用 V11 解码兼容链，然后迁移单 Act 状态。

保存身份、ActId/定义版本、当前节点、节点结果/访问记录、领队、事件、checkpoint、队伍/库存；不保存 TileMap cell 副本、格位、相机、Tween、控制器或预热进度。

- 旧 InitialExploration → act-01 首战入口；不重发资源，正式事务建立或复用 checkpoint。
- 旧 PendingBattle 保持 Encounter/Seed/入口资源，不重掷。
- Resolved 未离开恢复当前图结果；角色回模板槽。
- 失败/胜利/放弃保持终局。
- 无法唯一映射或内容版本不兼容：拒绝恢复，保留原档，不猜测覆盖。

恢复顺序：构建 Act → 投影全部节点状态 → 激活当前节点。主档/backup、CAS 沿用现有存储。

## File Structure

### 修改/复用

- `src/Tactics.Core/Runs/PureRunModels.cs`、`PureRunMapModels.cs`：Act/节点模型接入。
- `src/Tactics.Core/Runs/RunAdventureTransitionService.cs`：退役字符串解析与 InitialExploration 运行路径。
- `src/Tactics.Application/Runs/PureRunSessionService.cs`：进入/恢复/结算事务。
- `src/Tactics.Application/Runs/PureRunFlowProjector.cs`、`RunSaveNormalizer.cs`：投影与兼容。
- `godot/src/Tactics.Godot.Adapter/Runtime/GodotAdventureMapInstance.cs`：唯一正式节点宿主。
- 同目录 `GodotIsometricTileMapSurface.cs`、`AdventureMapTemplateResource.cs`：状态层/模板。
- 同目录 `GodotPlayableRunMain.cs`、`GodotStartCampView.cs`、`GodotAdventureBoardView.cs`：移交造图职责。
- 同目录 `GodotIsometricBattleBoard.cs`、`GodotRogueMapView.cs`：等价能力迁移后退役。

### 新增，不新增程序集

- Core Runs：`PureRunActDefinition.cs`、`PureRunActState.cs`、`PureRunNodeSceneDefinition.cs`、`PureRunNodeSceneTransitionService.cs`。
- Application Runs：`PureRunNodeSceneProjector.cs`、`RunSaveDocumentV12.cs`。
- Adapter Runtime：`GodotActWorldHost.cs`、`GodotActLoadCoordinator.cs`、`GodotNodeExplorationController.cs`、`GodotNodeBattleController.cs`、`GodotRogueMapAtlas.cs`、`GodotMapCameraController.cs`。

生成器入口实施前按实际文件复核，不能创建第二套资源管线。

## Implementation：垂直切片

### Task 1：合同、失败证据与单 Act 模型

- 目标：冻结规则，捕获营地后错误占位图。
- 输入：创建 Run/Setup/首战链、截图、父计划。
- 输出：Act/Node 契约、单 Act 定义、失败测试、兼容表。
- 文件：Core 新模型、Application 测试、Gameplay 开局旅程、合同。
- 验收：每节点唯一 ActId/NodeId/TemplateId；模板/出口/槽位/遭遇容量非法有定位错误；开局测试禁止 InitialAdventureBoard。

### Task 2：全 Act 初始化与营地至首战

- 目标：所有正式底图初始化；营地直接进入首战。
- 输入：Act 定义、Surface/MapInstance、Setup 草稿。
- 输出：Loading、注册表、Active lease、首战切片。
- 文件：ActWorldHost、LoadCoordinator、MapInstance、Main、StartCampView、SessionService。
- 验收：操作营地前地图覆盖全部节点；每节点一 Surface；Setup→首战不替换地图；取消/重试/新 Run 无残留与迟到回调；选人/起始技能和草稿不持久化回归通过。

### Task 3：三类战斗与原图战后探索

- 目标：普通/Elite/Boss 共用 TileMap 战斗。
- 输入：现有战斗输入、Actor、播放、VFX、结算。
- 输出：BattleController、结算 overlay、Resolved 探索。
- 文件：控制器、Main、BattlePresentationPlayer 集成、模板与测试。
- 验收：投影、边缘拾取、Facing、Z 排序、Hover、移动/VFX 无漂移；进入即战斗；结算恰好一次；普通胜利原图出口、失败终局、最终 Boss 胜利摘要。全部生产引用迁移后才删旧 Board。

### Task 4：同世界地图集与出口事务

- 目标：Overview 观察真实宿主；出口完成正式迁移。
- 输入：拓扑、注册表、Current/Overview 约定。
- 输出：地图集、情报/遮罩、目标标识、只读浏览。
- 文件：Atlas、CameraController、Projector、迁移服务。
- 验收：保留 M、右键/WASD、F/Home 与禁用滚轮约定；远端点击不迁移；自动接近后确认，取消不保存、双击一次；旧节点停用后才移交 lease；不可回访；预览不泄露敌人数/出生格/奖励。

### Task 5：正式非战斗节点

- 目标：Rest/Store/Treasure/普通事件去通用占位表现。
- 输入：现有业务、模板锚点、可审计素材。
- 输出：正式配置、非纯 ID 文本对象、状态层、三人探索。
- 文件：模板/现有生成器、ExplorationController、交互适配、旅程。
- 验收：三人入场仅领队移动；移动/改道/Pause/Facing 合同保持；对象邻接；状态/出口可读；奖励不重复；不用未经授权新素材。

### Task 6：事件原图战斗

- 目标：诅咒宝箱、祭坛守卫、护送伏击原图闭环。
- 输入：事件规则、探索/战斗槽、BattleController。
- 输出：阶段适配、地图状态变化。
- 文件：转移服务、控制器协调、状态层、事件旅程。
- 验收：地图/相机 InstanceId 不变；阻挡逻辑与表现一致；探索权先关闭；队伍重定位/遭遇生成；结果一次提交；失败终局；无第二棋盘。

### Task 7：V12 与恢复加固

- 目标：全部路径可靠恢复，旧占位状态可迁移。
- 输入：旧解码链、节点状态、CAS。
- 输出：V12、迁移测试、恢复与故障注入。
- 文件：SaveDocumentV12、Normalizer、SessionService、LoadCoordinator。
- 验收：覆盖 InitialExploration、普通/事件 PendingBattle、Resolved、终局；不重掷/部分保存；不兼容保留原档；overlay 不卸载 Act；Main 重建/新 Run/取消无重复信号与跨 Run 污染。

### Task 8：性能、完整 Run、人工与收尾

- 目标：验证实际体验及整个 Demo 完成度。
- 输入：完整单 Act、旅程、基线硬件。
- 输出：自动/性能证据、人工账本、修正交接。
- 文件：测试、既有性能采样入口、manual acceptance、合同、OKF。
- 验收：固定 seed、1600×900、同机 Release 非调试记录初始化耗时/峰值内存、Overview 帧耗、节点切换；构建分帧预算目标 4ms，记录不可分割超预算；Act 内 TileMap 构建/同步读取计数为零。切换窗口性能目标 P95≤16.7ms、最大≤33.3ms；未达标记录瓶颈、修复或取得明确性能例外，不能宣称无卡顿。

依赖：1→2→3→4→5→6→7→8。恢复基础从 Task 1 建立，每个切片补测试，不能等到 Task 7 才考虑事务正确性；Task 7 负责兼容与故障矩阵收口。

## Test Plan

- Core/Application：转移、直接后继、不可回访、CAS/幂等、奖励一次、阻挡一致、V12。
- GdUnit：100 格投影、Surface、InstanceId、唯一 Active、非当前无运行行为、切换/清理。
- Gameplay：营地直达首战、三类战斗、Rest/Store/Treasure、三事件、出口取消/双击/旧 revision、重启、最终 Boss 完整 Run。
- 故障：缺模板、加载取消/失败、CAS 冲突、保存后表现失败、迟到回调、版本不匹配。
- 回归：四方向 UL/DR 镜像、改道/resize/出口取消、Pause、Setup、Inventory、Golden/FrozenOracle。
- 门禁：定向→Core/Application→Godot/Gameplay→`Tools/godot/Verify-GodotProject.ps1`；历史 verifier 问题重新核实并精确报告。
- 人工：地图/对象辨识、输入、相机、战后探索、事件变化、帧 pacing、真实 Reload。verdict 只写 [人工账本](../docs/manual-acceptance.md)。

## Risks / Assumptions

- 全量常驻增加启动和内存；不未经确认改成流式卸载。
- 新旧状态双权威：仅统一 Application 适配器修改并断言一致。
- 常驻数据不代表可见情报；投影负责遮罩。
- 不回滚已保存奖励；表现失败从快照恢复。
- Pause 保持 Main 唯一 ownership；控制权切换可取消且受 generation 保护。
- 新类型/文件为批准的设计目标，不声称已实现。
- 性能数值为验收目标，不是当前实测承诺；机器信息必须写入证据。

## Handoff 与收尾

1. 先读 AGENTS、[父计划](roguelike-map-real-tilemap-atlas.md)、本文、投影/移动合同、人工账本；重新核查 git status/HEAD/Editor。
2. 更新父计划冲突的实例模型/顺序，以及 [旧交接](../docs/roguelike-tilemap-continuation-handoff.md) 的“已直接首战”错误结论；本文暂作为明确优先入口。
3. 加载适用 Godot/玩法合同/测试/Editor 生命周期 skills 后实施；正常关闭/恢复 Editor，不强杀、不抢前台。
4. 唯一项目 `godot/project.godot`；保护其既有 dirty 修改，不切 worktree，不 merge，不改第三方插件。
5. 每切片独立 review、自动证据和 manual handoff；精确 staged scope 与 diff 检查。
6. 文档/实现进入受监控范围时运行 OKF impact，更新权威正文，sync/test。
7. 全部范围实现与验收后，将长期规则并入 docs，真实缺口进入统一清单，更新 OKF，删除已完成计划由 Git 保留历史。pending 人工项不能以 Goal/自动通过代替。
