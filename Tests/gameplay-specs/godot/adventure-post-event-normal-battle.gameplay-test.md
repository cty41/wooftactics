---
feature: AdventureBoard
scenario: PostEventNormalBattleClearsContext
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
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 7,7
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_04_event"
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 6,5
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: cursed-chest
    parameters: { targetKind: AdventureObject }
  - kind: playBattleThroughInput
    adapter: PlayerInput
    parameters: { maximumActions: 100 }
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
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: battleReady, maximumFrames: 300 }
assertions:
  - kind: pendingBattleContextKindEquals
    adapter: Map
    expected: None
    parameters: {}
  - kind: runtimeHasNoErrors
    adapter: UI
    expected: true
    parameters: {}
  - kind: productionSaveUnchanged
    adapter: Map
    expected: true
    parameters: {}
timeoutMs: 150000
---

# Post-event normal battle

事件结算后进入下一场标准 Elite 战斗，断言已持久化清空事件上下文，普通战斗不再进入事件分支。
