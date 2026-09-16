---
feature: GodotPendingAcceptance
scenario: PresentationMiss
tags: [godot, presentation, numbers, isolated-save]
requiredAdapters: [Map, PlayerInput, Battle, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: numbers-miss-v1, path: "validated://numbers-miss-v1", semanticHash: 9a6718898e311e99470ff6f55ad67ad76baf22a715ab0d5e4108af613ab3e0e4 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: endTurnUntilPresentationNumber
    adapter: Battle
    parameters: { kind: Miss, maximumActions: 8 }
assertions:
  - kind: presentationNumberEquals
    adapter: UI
    expected: Miss
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

# Committed miss number

Amazon 以 Combat Techniques 作为起始分支且是唯一存活角色；生产 End Turn 输入驱动固定 RNG=6 的敌方攻击，必须产生 committed dodge 和灰色 Miss 数字。
