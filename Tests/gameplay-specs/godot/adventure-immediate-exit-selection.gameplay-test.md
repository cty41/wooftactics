---
feature: AdventureBoard
scenario: ImmediateSuccessorExitSelection
tags: [godot, adventure-board, isolated-save, validated-checkpoint]
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
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: exitCommitted, maximumFrames: 180 }
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

# Immediate successor exit selection

从 Layer 3 已结算的 Tile 场景只显示五个直接后继出口。直接点击 Rest 出口后，领队自动接近并立即进入该节点场景，不存在全局路线预提交或额外确认。
