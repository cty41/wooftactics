---
feature: AdventureBoard
scenario: StartCampPartyOrder
tags: [godot, player-input-e2e, adventure-board, isolated-save]
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
assertions:
  - kind: pendingPartyOrderEquals
    adapter: Map
    expected: [pure_run_amazon, pure_run_demonbound, pure_run_mage]
    parameters: {}
  - kind: runtimeHasNoErrors
    adapter: UI
    expected: true
    parameters: {}
  - kind: productionSaveUnchanged
    adapter: Map
    expected: true
    parameters: {}
timeoutMs: 30000
---

# Start camp party order

从空隔离存档自动进入真实 TileMapLayer 营地，通过生产鼠标依次选择 Amazon、Demonbound、Mage，再点击 Start 出口，并验证 PendingRunSetup 保留点击顺序且隔离存档没有污染生产主档。
