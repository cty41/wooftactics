---
feature: AdventureBoard
scenario: LeaderSwitchAndFreePath
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
    target: 4,5
    parameters: { targetKind: AdventureCell }
assertions:
  - kind: pendingPartyOrderEquals
    adapter: Map
    expected: [pure_run_amazon, pure_run_demonbound, pure_run_mage]
    parameters: {}
  - kind: adventureActorCellEquals
    adapter: Map
    target: pure_run_amazon
    expected: 4,5
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

# Start camp leader free path

在起始营地选满三人后，队长（第一个选择的 Amazon）点击可达格触发确定性寻路。出发后不再有第二个起始探索页，进入第一场战斗由营地出口直接完成。