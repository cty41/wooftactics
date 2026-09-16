---
feature: AdventureBoard
scenario: FixedSeedCompleteRun
tags: [godot, player-input-e2e, adventure-board, fixed-seed, full-run, isolated-save]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: pure_run_amazon
    parameters: { targetKind: AdventureActor }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: pure_run_demonbound
    parameters: { targetKind: AdventureActor }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: pure_run_mage
    parameters: { targetKind: AdventureActor }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: start-exit
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: starting_skill__skill_amazon_thrust_lv1
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: starting_skill__skill_mage_fireball_lv1
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 7,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_01_battle"
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: encounter
    parameters: { targetKind: AdventureObject }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Continue — Progression
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ProgressionAttribute_Strength
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ProgressionSkillChoice_0
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Continue
    parameters: { targetKind: UiElement }

  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 8,7
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_02_battle"
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: encounter
    parameters: { targetKind: AdventureObject }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Continue — Progression
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ProgressionAttribute_Strength
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ProgressionSkillChoice_0
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Continue
    parameters: { targetKind: UiElement }

  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 8,7
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_03_battle"
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: encounter
    parameters: { targetKind: AdventureObject }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Continue — Progression
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ProgressionAttribute_Strength
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ProgressionSkillChoice_0
    parameters: { targetKind: UiElement }

  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 3,7
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_04_rest"
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: rest-campfire
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Confirm Rest
    parameters: { targetKind: UiElement }

  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 8,7
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_05_battle"
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: encounter
    parameters: { targetKind: AdventureObject }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Continue — Progression
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ProgressionAttribute_Strength
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: ProgressionSkillChoice_0
    parameters: { targetKind: UiElement }

  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 8,8
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_06_treasure"
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: treasure-chest
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Confirm Treasure
    parameters: { targetKind: UiElement }

  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 8,7
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_07_battle"
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: encounter
    parameters: { targetKind: AdventureObject }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
assertions:
  - kind: terminalSummaryOutcomeEquals
    adapter: Map
    expected: BossVictory
    parameters: {}
  - kind: runSaveSchemaVersionEquals
    adapter: Map
    expected: 10
    parameters: {}
  - kind: runtimeHasNoErrors
    adapter: UI
    expected: true
    parameters: {}
  - kind: productionSaveUnchanged
    adapter: Map
    expected: true
    parameters: {}
timeoutMs: 600000
---

# Fixed-seed complete Tile Adventure run

以固定种子从正式 Start Camp 开局，经每个节点场景中的即时后继出口、前三层战斗、第四层休息、第五层战斗、第六层宝箱和最终 Boss，所有操作均通过正式 Main.tscn 的生产输入链完成，并断言 V10 终局存档为 BossVictory。
