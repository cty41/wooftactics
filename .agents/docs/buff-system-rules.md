# 状态与 Buff 规则合同

## 定位

状态的施加、刷新、替换、触发与移除由 `StatusRuntimeService` 和战斗 transition 裁决。Resource 描述状态，Godot 只展示 committed 状态；UI 图标、动画或特效不能决定持续时间、层数或伤害。

```gameplay-contract
id: BUFF-REFRESH-STRATEGY-001
status: verified_current
statement: 燃烧重复施加时增加层数，毒素重复施加时累加持续回合，减速与眩晕重复施加时刷新持续回合；同类运行时状态不得并列产生多个独立实例。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StatusItemRuntimeTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
dsl_support: partial
```

```gameplay-contract
id: BUFF-POISON-SOURCE-001
status: verified_current
statement: 毒素重施会累加本次持续回合并以最新施加者作为状态来源，但不会把每回合伤害作为层数叠加。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
dsl_support: partial
```

```gameplay-contract
id: BUFF-SPEED-PROJECTION-001
status: verified_current
statement: 减速后的有效速度最低为一，移动范围为有效速度一半向上取整并限制在一到四，先攻为有效速度的两倍；该投影由 Core 统一计算。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StatusItemRuntimeTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: BUFF-ACTION-MODIFIER-001
status: approved_target
supersedes:
  - BUFF-SPEED-PROJECTION-001
statement: 减速不修改敏捷、体质、生命或命中，而是分别修改先攻与移动；旧的负二速度效果迁移为负四先攻和负一移动，最终移动仍遵守二至五的限制。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StatusItemRuntimeTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: BUFF-FROZEN-TOTAL-HEALING-001
status: verified_current
statement: TurnStart 持续治疗在施加时冻结总治疗量和 tick 数，每次按 ceil(剩余总量/剩余 tick) 调度，因此不能整除的余数优先较早 tick；过量治疗仍消耗本次调度量。同名 RefreshDuration 重施替换为新的完整总量和 tick 数，不叠加；该类状态只在目标未来 TurnStart 递减，施加当回合的 EndTurn 不提前消耗 tick。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StatusItemRuntimeTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/PoetSkillRuntimeTests.cs
dsl_support: unsupported
```

## 设计约束

- 新状态必须明确 polarity、effect kind、触发时点、刷新策略、来源与持续量纲。
- 新的同类合并规则属于合同变更，不能靠 UI 文案或资源命名隐式实现。
- 自动测试证明规则状态；状态图标可读性、反馈节奏仍需人工验收。
