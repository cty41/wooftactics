# 战斗朝向规则合同

## 定位

朝向是 Godot 表现状态，不参与 Core 命中、路径或伤害裁决。它必须由逻辑格坐标与已提交/预览的动作推导，不能反向修改玩法状态。

```gameplay-contract
id: FACING-INITIAL-SIDES-001
status: verified_current
statement: 玩家编号零的单位初始朝东，其他阵营单位初始朝西。
verification:
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
dsl_support: unsupported
```

### Retired: FACING-DIRECTION-RESOLVE-001

该历史合同曾把逻辑格主轴直接映射为可见朝向；它未考虑 Godot 等距投影，现由 `FACING-ISOMETRIC-MOTION-001` 替代，ID 不复用。

```gameplay-contract
id: FACING-ISOMETRIC-MOTION-001
status: verified_current
statement: 朝向按 GridPoint 位移经过等距投影后的屏幕运动方向解析；逻辑正X、负X、正Y、负Y分别显示为右上、左下、左上、右下，对应UL水平镜像、DR水平镜像、UL原图、DR原图；零位移保持当前朝向，双轴等量时优先保留与任一分量一致的当前朝向，否则稳定选择X分量方向。
verification:
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
  - layer: godot_test
    path: godot/tests/AdventureMovementGodotTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: FACING-PREVIEW-SOURCE-001
status: verified_current
statement: 移动预览使用路径第一步决定朝向，技能目标预览使用施法者逻辑格到目标逻辑格决定朝向；空路径保持当前朝向。
verification:
  - layer: godot_test
    path: godot/tests/IsometricBattleBoardGodotTests.cs
dsl_support: unsupported
```

## 设计约束

新增位移或目标机制时，应先产出逻辑路径/目标，再交给统一 resolver。Sprite 缺少某方向可在表现资源中回退，但不得因此改变合同方向。
