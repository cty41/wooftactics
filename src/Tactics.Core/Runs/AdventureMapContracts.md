# Roguelike Map engine-neutral contracts

Status: `approved_target`

These contracts freeze the Core/Application boundary for the real TileMap atlas migration. They do not prescribe Godot scene or resource structure.

```gameplay-contract
id: ROGUELIKE-START-FLOW-001
status: approved_target
statement: 启动时自动继续可恢复状态；无有效存档时直接进入营地。营地按点击顺序增量持久化最多三名成员，第一名是不可更换的领队；选择不可撤销，满员后才能由出口进入起始技能选择。
verification:
  - layer: application_test
    path: src/Tactics.Application.Tests/PureRunStartFlowTests.cs
  - layer: godot_test
    path: godot/tests/PlayableRunStartFlowGodotTests.cs
dsl_support: partial
```

```gameplay-contract
id: ROGUELIKE-MAP-CAMERA-001
status: approved_target
statement: 地图图集只允许一个 Active TileMap，其余节点为无玩法进程的 Preview；右键拖动、指针中心滚轮缩放、WASD/方向键、M 总览和 F/Home 聚焦均受地图边界约束，左键只用于当前地图交互或预览摘要。
verification:
  - layer: godot_test
    path: godot/tests/AdventureMapAtlasGodotTests.cs
dsl_support: none
```

## ROGUELIKE-MAP-TEMPLATE-001

- Every playable map has a fixed 10x10 `AdventureBoardDefinition` logical grid.
- A template declares unique candidate, party-entry, player-battle, and enemy-battle slots.
- It also declares entry, target-bound exit, connection, camera-focus, and atlas-bounds anchors.
- All slots and anchors are in bounds; every entry can reach every exit.
- State layers use the closed identifiers declared by `AdventureMapStateLayers`; required planning, tactical-preview, current, and completed layers cannot be omitted.

## ROGUELIKE-NODE-INTEL-001

- `Planning` is the baseline for every route node and exposes node kind and topology.
- Only directly reachable/available nodes advance to `TacticalPreview` and may expose tactical categories.
- `Current` is the sole fully active node; `Completed` retains a read-only resolved projection.
- Exit intelligence binds one exit to one direct target and carries only information allowed by the target node's intelligence state.

## ROGUELIKE-NODE-RECOVERY-001

- Run saves persist node-level facts: route/current node, discovery state, committed object and reward results, leader identity, and encounter checkpoint.
- Actor grid cells, per-cell movement, camera state, and transition animation are process-local and never encoded by schema V11.
- V10 actor cells are accepted only as legacy input and discarded during V11 normalization.
- Continue rebuilds actor placement from template slots. An invalid or dead saved leader falls back to the first living party member in party order.
