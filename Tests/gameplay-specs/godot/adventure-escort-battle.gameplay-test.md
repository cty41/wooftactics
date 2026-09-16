---
feature: AdventureBoard
scenario: LostVillagerEscortBattle
tags: [godot, adventure-board, escort, isolated-save, validated-checkpoint, v8]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: layer6-escort-ready-v1, path: "validated://layer6-escort-ready-v1", semanticHash: db5796777319f9278eafaa394efaa98e153b97e009680b413c22fa9811ce5339 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: restartGodotMain
    adapter: UI
    parameters: {}
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 7,7
    parameters: { targetKind: AdventureCell }
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
    target: lost-villager
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
  - kind: escortStateEquals
    adapter: Map
    expected: Completed
    parameters: {}
  - kind: protectedNpcAliveEquals
    adapter: Map
    expected: true
    parameters: {}
  - kind: eventResolutionEquals
    adapter: Map
    expected: EscortCompleted
    parameters: {}
  - kind: adventureObjectStateEquals
    adapter: Map
    target: lost-villager
    expected: Safe
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
timeoutMs: 120000
---

# Lost villager escort battle

从 V8 Traveling 检查点真实重启 Main，进入后续 Layer 6 节点。村民由自动逃生 AI 控制，敌人将其视为高仇恨目标；只有清敌且村民存活才完成护送。
