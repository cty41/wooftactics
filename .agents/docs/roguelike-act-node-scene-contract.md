# Run / Act / Node 场景合同

批准的实现目标见 [架构计划](../plans/roguelike-act-node-scene-architecture.md)。本 Demo 仅一个 Act；以下目标不能因为模型类型已添加便标记为完整实现。

```gameplay-contract
id: ROGUELIKE-ACT-RESIDENCY-001
status: approved_target
statement: 当前Act的每个拓扑节点在开放操作前拥有唯一正式地图宿主；Preview和Active使用相同实例；只有当前节点可运行玩法，隐藏不等于停用；本Demo仅一个Act。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/PureRunActDefinitionTests.cs
  - layer: godot_test
    path: godot/tests/ActWorldHostGodotTests.cs
  - layer: godot_test
    path: godot/tests/ActMapTemplateGodotTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: ROGUELIKE-SETUP-FIRST-ENCOUNTER-001
status: approved_target
statement: 开局最后一个必需技能完成时只进行一次CAS，同时保存完整队伍和首战checkpoint；成功后才发出EncounterRequest并清除Setup草稿，失败不发请求且保留草稿；不得经过InitialExploration占位地图。
verification:
  - layer: application_test
    path: src/Tactics.Application.Tests/PureRunSessionServiceTests.cs
dsl_support: partial
```

```gameplay-contract
id: ROGUELIKE-NODE-SAME-SCENE-001
status: approved_target
statement: 普通Elite及Boss节点进入即战斗；事件在正式原图触发战斗；非最终胜利提交一次结果后在原图战后探索，离开后不可回访但保留只读残局；最终Boss进入Run胜利摘要。
verification:
  - layer: godot_test
    path: godot/tests/PlayableRunUiGodotTests.cs
  - layer: godot_test
    path: godot/tests/ResidentBattleBoardGodotTests.cs
  - layer: godot_test
    path: godot/tests/ResidentBattleProductionGodotTests.cs
  - layer: godot_test
    path: godot/tests/ResidentEventBattleProductionGodotTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/PureRunNodeSceneTransitionServiceTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/PureRunActStateTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/PureRunLegacyActStateProjectorTests.cs
dsl_support: partial
```

```gameplay-contract
id: ROGUELIKE-NODE-RECOVERY-001
status: approved_target
statement: 已提交PendingBattle按checkpoint和节点事务的交叉证据恢复场景身份，保持既有checkpoint、AdventureRevision、队伍和随机初态；不能按复用Encounter猜节点或补造事件触发；冲突与不兼容明确失败并保留原档。
verification:
  - layer: application_test
    path: src/Tactics.Application.Tests/RunSaveDocumentV12Tests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/PureRunNodeSceneProjectorTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/PureRunPendingSceneRecoveryTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/PureRunEncounterRequestValidatorTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/PureRunActBlueprintTests.cs
  - layer: godot_test
    path: godot/tests/ActContentCatalogGodotTests.cs
dsl_support: unsupported
```

## 保持的合同

沿用 [移动合同](adventure-movement-contract.md)、[等距锚点合同](isometric-grid-anchor-contract.md) 和 [朝向合同](battle-facing-rules.md)。`PURE-RUN-BATTLE-DIRECT-ENTRY-001` 此前只覆盖部分已选节点，未覆盖Setup后的InitialExploration；本合同明确补足，不能继续把旧验证范围描述为全流程完成。

## 正式地图准备与终局

Act宿主分阶段注册所有正式地图后才允许激活；资源缺失或模板映射不一致必须失败，不生成fallback。节点租约绑定宿主实例、RunScope、NodeId与generation，防止重建后的迟到回调重新获得控制权。Preview默认禁用Process，控制器与碰撞的完整停用还须在生产接入时验证，不能仅凭该标记宣称完成。

最终Boss模板显式 `IsTerminal=true`，必须有入口且没有出口/向外连线锚点。旧模板默认false继续要求出口/连线，禁止为兼容旧校验而伪造下一Act出口。

## 单 Act Demo 路线校验

当前不可回访Demo只接受有向无环路线，起点无入边、终点无出边，所有节点从起点可达且可到终点。仅检查普通图可达不足以保证不回访时可通关：例如进入支路后只能退回已访问节点的环必须拒绝。此限制针对当前前进型Demo，不定义未来其他世界模式。

## 事务与随机边界

保存失败不能释放EncounterRequest；checkpoint作为恢复权威。新Run一次CAS创建首战时checkpoint revision等于首次保存revision；这是新入口的确定性基线，不承诺与旧两次/多次提交路径的随机流一致。旧PendingBattle必须保持原checkpoint、AdventureRevision与EffectiveRandomState，不重掷。

## 借用战斗视图边界

`GodotIsometricBattleBoard.BindResidentSurface`只借用当前节点租约与Surface；不得重配、重建、重挂或释放底图，颜色反馈只画高亮。拾取、角色标记使用双方到同一Viewport的变换；布局/浅水不匹配必须拒绝，不能用旧多边形底图覆盖错误地图。租约失效、View退出及外部hover回调中切换绑定后，不再发布旧输入/hover事件。

节点身份必须成为运行时checkpoint及请求的不变量；同一n1/e1可以出现在多个节点，不能按Encounter反查唯一节点，也不能用旧Board字符串优先级掩盖新事务。V11过渡期只允许先验证原请求，再以完整已保存checkpoint/事务证据进行零写Recovery，绑定并复核派生请求；不改原修订/RNG/Party，不把内存投影声称为已切V12持久化。普通/Elite/Boss的Main接入使用这份身份校验宿主模板及实际布局后借Surface；事件仍为后续切片。相机调整Host使Surface与既有board-local角色/VFX坐标一致，禁止修改terrain/node变换。结算推进下一Encounter不能改变刚完成节点的当前地图；页级Actor/VFX和原图战后状态迁移尚未完成。

## 存档身份边界

`RunNodeSceneIdentity`包含ActId、定义版本、NodeId及TemplateId；当前场景与checkpoint各持一份，V12要求一致。旧schema直接序列化Core记录，因此新字段由V12显式读写、对旧serializer忽略；V11编码器拒绝带新身份的隐式降级。Resume请求透传checkpoint身份，不重新计算节点或推进修订。

V12对V11等旧文档仅报告`RequiresNodeMigration`，不会猜节点、重建checkpoint或改原字符串；自动身份迁移与所有创建/进入事务仍待完成。V12编码禁止缺身份或仍为InitialExploration的ActiveRun。当前生产Store仍使用V11；只有节点迁移、原子入口和内容版本校验接通后才能切换，不能把这个codec切片称为完整V12恢复。

### 已提交Pending的内存恢复切片

`PureRunNodeSceneProjector`只按checkpoint index/Encounter、事务与Map选择、Act/模板版本交叉定位，不按Encounter唯一反查。已有Pending可以从旧直接API保留Initial占位字段；该字段不压过有效checkpoint。Ready+Initial创建首战是另一事务，不能混用这条规则。

`PureRunPendingSceneRecovery`只做内存表示归一化：写入两端身份、正式Board与MapActive、清除旧过程格位；不调用Store、不创建战斗、不改变任何revision或RNG。冲突返回同一原Snapshot；已经绑定的合法状态保持原对象。

事件marker完全缺失返回`scene.event_state_incomplete`。直接将默认seed蓝图用于不一致的旧实际context仍返回`scene.event_variant_incompatible`；显式Recovery现在可用完整checkpoint/事务/实际marker证据选择有限已知变体，再交Projector验证。原assignment、context、修订和RNG保持不变，不补造escort。护送待战状态必须与当前Lost上下文、目的节点及存活NPC双向一致；历史Completed/Traveling不机械拒绝。此恢复尚未接生产Store，仍禁止宣称全部阶段迁移完成。

### 实际节点变体恢复目标（approved_target）

默认seed选择与已保存实际进入绑定分开。恢复只能从当前Demo的22个已知内容变体中，按明确Act/version/node/template身份恢复；完整旧Pending事件的实际context、node/object marker、checkpoint与选中事务可作为该节点变体的迁移证据，不重抽、不改原MysteryEventAssignments（它仍影响后续选择池）。未知模板/版本、部分marker或矛盾必须拒绝。护送状态只保留，不自动Accept/Travel/BeginBattle；没有已保存护送战斗状态不能补造NPC。

此目标只解决当前Pending的内容绑定；未记录的历史节点变体、事件结算中间态和Offer/Travel流程仍须单独实现，不能借此宣称全存档迁移完成。

### 启动请求防线

Main启动战斗前必须使用成功的Application Resume修复投影校验RunId、checkpoint/Encounter、AdventureRevision及完整队伍值；不能把已修复请求与原始存档Party混比。失败Load即使带旧Snapshot也不能启动。护送上下文取同一投影，准备回调及Pending路由也通过Resume获取最新投影和请求，修复本身保持零写入。三份场景身份任一存在时必须完整一致并经Projector验证；全部为空仍走显式V11过渡路径，不将尚未解决的旧事件变体迁移强加到生产。

## 验收边界

自动证据、实际运行接入、人工体验分别记录。全Act常驻、同实例探索/战斗与无占位页必须在生产路径验证；纯Core模型测试不证明这些运行时目标成立。
