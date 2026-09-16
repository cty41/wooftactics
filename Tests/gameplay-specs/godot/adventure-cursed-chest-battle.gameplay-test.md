---
feature: AdventureBoard
scenario: CursedChestMimicBattle
tags: [godot, adventure-board, isolated-save, validated-checkpoint]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: layer4-event-ready-v1, path: "validated://layer4-event-ready-v1", semanticHash: a3fb1f889423aae1e4c53a8fba80d4c0bcfc966ec6b5872f80f7e7e7b3407c29 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 7,7
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_04_event"
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: cursed-chest
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: eventBattleReady, maximumFrames: 300 }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureSceneChanged, maximumFrames: 600 }
assertions:
  - kind: eventResolutionEquals
    adapter: Map
    expected: CursedChestMimicDefeated
    parameters: {}
  - kind: pendingBattleContextKindEquals
    adapter: Map
    expected: None
    parameters: {}
  - kind: adventureObjectStateEquals
    adapter: Map
    target: cursed-chest
    expected: Defeated
    parameters: {}
  - kind: runtimeHasNoErrors
    adapter: UI
    expected: true
    parameters: {}
  - kind: productionSaveUnchanged
    adapter: Map
    expected: true
    parameters: {}
timeoutMs: 120000
---

# Cursed chest mimic battle

从正式 Layer 4 Event Tile 点击诅咒宝箱，复用现有 N4 敌方阵容进入标准战斗。通过正式战斗输入获胜后返回同一 Tile 的变化场景，并断言宝箱怪已倒下。
