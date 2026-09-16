---
feature: GodotPendingAcceptance
scenario: ReloadCleanup
tags: [godot, reload, cleanup, isolated-save]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: reload-pending-battle-v1, path: "validated://reload-pending-battle-v1", semanticHash: 13c911dd9ed54586e3dc160a86e03bdde1a9301b8bbc6c0702962f4a3038e6a0 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: restartGodotMain
    adapter: UI
    parameters: {}
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: battleReady, maximumFrames: 300 }
assertions:
  - kind: checkpointRevisionEquals
    adapter: Map
    expected: 6
    parameters: {}
  - kind: presentationNodeCountEquals
    adapter: UI
    expected: 0
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

# Scene/process-style reload and cleanup

销毁并重新实例化正式 Main，复用同一隔离 Store，从 PendingBattle checkpoint 恢复且不残留临时表现节点。
