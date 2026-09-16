# 战斗朝向规则合同

## 定位

Facing 已提升为 Core `UnitState` 的权威玩法事实，供诗人后撤和未来背刺扩展使用。Godot 只投影已提交 Facing，并可做不写回 Core 的预览。

```gameplay-contract
id: FACING-CORE-AUTHORITY-001
status: verified_current
statement: 玩家编号 0 的单位初始朝东，其他阵营初始朝西；成功移动提交最后一段路径方向，成功定向目标技能提交施法者到目标的方向，自身技能保持朝向。诗人后撤锁定施法前朝向并在后撤后保持；失败动作和所有预览都不得修改 Core Facing。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BoardAndRulesTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/PoetSkillRuntimeTests.cs
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: FACING-DIRECTION-RESOLVE-001
status: verified_current
statement: 朝向由起点到终点的主轴决定；位移为零时保持当前朝向，横纵位移相等时优先保持与位移一致的当前轴向，否则按横向符号选择东西。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/BoardAndRulesTests.cs
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: FACING-PREVIEW-SOURCE-001
status: verified_current
statement: 移动预览使用路径最后一段决定预期朝向，技能预览使用施法者逻辑格到目标逻辑格；空路径和自身技能保持当前 Core Facing。预览仅修改 Godot actor 的临时表现并在取消或提交后恢复/刷新。
verification:
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
dsl_support: unsupported
```
