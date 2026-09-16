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
    target: start-exit
    parameters: { targetKind: AdventureObject }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: starting_skill__skill_amazon_thrust_lv1
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: starting_skill__skill_mage_fireball_lv1
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: pure_run_mage
    parameters: { targetKind: AdventureActor }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 4,5
    parameters: { targetKind: AdventureCell }
assertions:
  - kind: activeAdventureLeaderEquals
    adapter: Map
    expected: pure_run_mage
    parameters: {}
  - kind: adventureActorCellEquals
    adapter: Map
    target: pure_run_mage
    expected: 4,5
    parameters: {}
  - kind: adventureActorCellEquals
    adapter: Map
    target: pure_run_amazon
    expected: 2,5
    parameters: {}
  - kind: adventureActorCellEquals
    adapter: Map
    target: pure_run_demonbound
    expected: 1,4
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

# Leader switch and free path

完成正式开局后点击 Mage 头像切换领队，再点击可达格触发确定性寻路。断言仅领队移动，另外两名 Idle 队友保持固定位置。
