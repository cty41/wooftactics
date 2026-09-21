---
feature: AdventureBoard
scenario: FallenAltarGuardianBattle
tags: [godot, adventure-board, isolated-save, validated-checkpoint]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: layer6-event-ready-v1, path: "validated://layer6-event-ready-v1", semanticHash: 27961cf36a0c8d73c22bfb367d5005dfeedfc009229ba22f120b1821d3c01198 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_06_event"
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
    target: fallen-altar
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
    expected: FallenAltarGuardianDefeated
    parameters: {}
  - kind: pendingBattleContextKindEquals
    adapter: Map
    expected: None
    parameters: {}
  - kind: adventureObjectStateEquals
    adapter: Map
    target: fallen-altar
    expected: Purified
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

# Fallen altar guardian battle

从正式 Layer 6 Event Tile 点击堕落祭坛，进入标准战斗。通过正式战斗输入获胜后返回同一 Tile 的变化场景，并断言祭坛已净化。
