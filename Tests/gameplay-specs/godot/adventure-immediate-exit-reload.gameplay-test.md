---
feature: AdventureBoard
scenario: ImmediateExitReload
tags: [godot, adventure-board, isolated-save, validated-checkpoint, reload]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters:
      id: layer4-choice-ready-v1
      path: validated://layer4-choice-ready-v1
      semanticHash: 2d0ab502e474b2c61c413be755279126fa509dcf9cfb5afdb9ce3f66b20f9ac2
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_04_rest"
    parameters: { targetKind: AdventureObject }
  - kind: restartGodotMain
    adapter: UI
    parameters: {}
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
assertions:
  - kind: runNodeLifecycleEquals
    adapter: Map
    expected: MapActive
    parameters: {}
  - kind: immediateSuccessorNodeIdsEqual
    adapter: Map
    expected: [layer_05_battle]
    parameters: {}
  - kind: adventureObjectStateEquals
    adapter: Map
    target: rest-campfire
    expected: Ready
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

# Immediate exit reload

选择当前节点的直接后继 Rest 出口后重启 Main，仍恢复 Rest Tile 场景及其未结算交互状态，不恢复任何预提交路线总览。
