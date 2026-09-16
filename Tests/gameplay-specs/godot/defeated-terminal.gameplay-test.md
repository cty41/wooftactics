---
feature: GodotPendingAcceptance
scenario: DefeatedTerminal
tags: [godot, defeat, terminal, isolated-save]
requiredAdapters: [Map, PlayerInput, Battle, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: defeat-no-summon-v1, path: "validated://defeat-no-summon-v1", semanticHash: 57f955cffb989e9a44aac824568b88509c0756f19279652a965beb74e58ccc8b }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: endTurnOnlyUntilTerminal
    adapter: Battle
    parameters: {}
assertions:
  - kind: terminalSummaryOutcomeEquals
    adapter: Map
    expected: Defeated
    parameters: {}
  - kind: activeRunExistsEquals
    adapter: Map
    expected: false
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

# Defeated terminal through Main

从低生命、无召唤物的合法战前 checkpoint 开始，只提交生产 UI 允许的 End Turn，验证全灭后唯一 Defeated Summary。
