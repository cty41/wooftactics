# Adventure Movement Contract

## Scope

All Active Adventure Boards, including Start Camp and every runtime node board, use `GodotUnitActor` and the shared single-cell Tween builder. Preview-only map nodes remain actor/Tween/input free.

## Contracts

### ADVENTURE-PATH-POLICY-001

Deterministic path search mechanics are shared with Battle, while policy is not. Adventure keeps uniform step cost, Adventure object blocking, idle-party occupancy, and an origin-inclusive public path. Battle keeps terrain/movement-kind policy and its origin-exclusive public path.

### ADVENTURE-REPATH-CENTER-001

A legal empty-cell click during movement replaces the desired destination. The current Tween segment must finish at the exact current cell center before a deterministic replan starts. Actor/object/exit clicks are rejected with `adventure.movement_in_progress`; an unreachable destination does not replace the current route.

### ADVENTURE-EXIT-AUTO-APPROACH-001

```gameplay-contract
id: ADVENTURE-EXIT-AUTO-APPROACH-001
status: verified_current
statement: 点击已解锁出口时，领队若未相邻则自动移动到最短可达相邻格，等长路径按固定Grid邻居顺序稳定选择；仅在最终移动提交成功、RunId与Page仍匹配且目的格仍邻接时触发出口，锁定或不可达出口不启动移动。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/AdventureBoardDefinitionTests.cs
  - layer: godot_test
    path: godot/tests/PlayableRunUiGodotTests.cs
dsl_support: partial
```

### PURE-RUN-BATTLE-DIRECT-ENTRY-001

```gameplay-contract
id: PURE-RUN-BATTLE-DIRECT-ENTRY-001
status: verified_current
statement: 选择类型为Battle、Elite或Boss的确定性地图节点后直接建立encounter并进入Battle，不显示可移动的预战斗TileMap或encounter marker；Mystery事件产生的条件战斗仍由事件对象交互触发。
verification:
  - layer: godot_test
    path: godot/tests/PlayableRunUiGodotTests.cs
  - layer: gameplay_spec
    path: Tests/gameplay-specs/godot/adventure-fixed-seed-full-run.gameplay-test.md
dsl_support: supported
```

### ADVENTURE-MOVE-COMMIT-001

Visual movement is transient. Normal Adventure commits exactly one final `MoveLeader` after settle; it never writes one save per cell. Save recovery intentionally strips actor cells, so `AdventureExplorationSession` reconstructs process-local entry positions and hydrates only the final authoritative transition. Start Camp never writes movement into the Run save.

### ADVENTURE-MOVE-LOCK-001

While moving, movement input is locked except legal empty-cell retargeting. Pause freezes the active Tween. Page teardown/reload cancels it and invalidates stale completion callbacks. A viewport resize interrupts at the last exact cell center and synchronizes that cell into the process-local `AdventureExplorationSession` before relayout, so visual and interaction authority cannot diverge; it does not persist a Run movement commit.

### ADVENTURE-BOARD-FIT-001

A runtime Adventure TileMap uses its actual 10×10 tile bounds, not a fixed pixel offset. It is uniformly scaled to fit a near-fullscreen safe area, and the transformed board center equals the safe-area center. Adventure HUD uses compact top-left, top-right, bottom-left and bottom-right overlays in the isometric diamond's empty corners rather than reserving horizontal/vertical bands. Actor size/position and pointer inverse mapping consume the same transform.

## Ownership

- Core: deterministic search mechanics, independent Battle/Adventure policy wrappers, process-local exploration cell session.
- Adapter: `GodotUnitActor`, hit/input translation, facing, single-cell Tween construction, segment generation and cancellation。等距主轴映射固定为 `+X→右上(UL镜像)`、`-X→左下(DR镜像)`、`+Y→左上(UL)`、`-Y→右下(DR)`。
- Main: Unit resource/profile injection and final one-shot Run mutation.
- Diagnostics: bounded `[AdventureMovement]` records expose generation, centered cell, desired destination, remaining path, replan count, and outcome.
