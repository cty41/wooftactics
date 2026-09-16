# Battle Initiative Rules

状态：`verified_current`

## 权威模型

Core `InitiativeRoundState` 是行动身份与顺序的唯一权威。完整轮次分区为 `ActedOrder + Current + Remaining`；Application 只投影 current 与 remaining，Godot Tween 只表现已经提交的结果，不能拥有或延迟玩法状态。

```gameplay-contract
id: INITIATIVE-DYNAMIC-ROUND-001
status: verified_current
statement: 每个新回合按当时有效先攻降序、PlayerNumber 升序、SpawnOrdinal 升序、UnitId ordinal 升序完整重建；轮中先攻变化只稳定重排尚未行动的 remaining，current 与 acted 不回队，因此既有单位每轮最多行动一次。相同先攻时玩家侧优先。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/BoardAndRulesTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: INITIATIVE-SUMMON-INSERTION-001
status: verified_current
statement: 轮中创建且参与行动的召唤实例以新 InstanceId 按有效先攻插入 remaining；召唤替换完全开放，旧实例即使已经行动，新实例仍视为未行动并可在资源允许时于同轮行动。非行动 Decoy 永远不进入先攻分区；死亡实例立即退出 eligible。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: INITIATIVE-INCAPACITATED-SKIP-001
status: verified_current
statement: 不能行动但仍存活的单位保留其队列位置；轮到时由 Application 通过正式 EndTurn 自动跳过，从而照常消费回合末法力恢复、状态持续时间与新回合推进，不允许 UI 或 AI 直接删除该实例。
verification:
  - layer: application_test
    path: src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: INITIATIVE-HUD-001
status: verified_current
statement: Godot 右上行动条固定右对齐，回合标签在队列左侧，只显示 current 与 remaining；头像为圆形脸部裁切，友方绿色背景、敌方红色背景，current 同尺寸金框、hover 白色粗框，不显示先攻数字。最多显示九个头像，存在更多实例时第十槽为不可交互省略号，隐藏队列变化使其脉冲；新增从左扩展、移除向右收拢。队列变化先提交 Core再执行 Tween并短暂锁定输入；hover 同步显示战场 HP/MP 条并用阵营色 shader 轮廓标记单位。
verification:
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs
  - layer: manual_qa
    path: .agents/docs/manual-acceptance.md
dsl_support: unsupported
```
