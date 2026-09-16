---
feature: Demonbound
scenario: RuntimeState
tags: [godot, demonbound, corruption, possession, isolated-save]
requiredAdapters: [Map, PlayerInput, UI, Battle]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: demonbound-ready-v1, path: "validated://demonbound-ready-v1", semanticHash: 6a6cd1044c7a51332edc0d347970565df0b4f5dd22e08dd4ede2e316aef9c5df }
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
  - kind: useBattleSkillThroughInput
    adapter: PlayerInput
    parameters: { actorId: party-pure_run_demonbound, skillId: skill.demonbound.bane.lv1, maximumActions: 40 }
assertions:
  - kind: demonboundCorruptionEquals
    adapter: Battle
    target: party-pure_run_demonbound
    expected: 3
    parameters: {}
  - kind: demonboundPossessedEquals
    adapter: Battle
    target: party-pure_run_demonbound
    expected: false
    parameters: {}
  - kind: battleSkillReceiptEquals
    adapter: Battle
    expected:
      actorId: party-pure_run_demonbound
      skillId: skill.demonbound.bane.lv1
      corruption: 3
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

# Demonbound runtime state

从隔离 V7 checkpoint 进入正式战斗，通过 Viewport.PushInput 选择厄运魔刃及方向格，验证行动 receipt 与腐化状态，且不污染生产存档。
