---
feature: AdventureBoard
scenario: StartingSkillsEnterAdventureBoard
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
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: starting_skill__skill_amazon_thrust_lv1
    parameters: { observable: uiElement, elementName: starting_skill__skill_amazon_thrust_lv1, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: starting_skill__skill_amazon_thrust_lv1
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: starting_skill__skill_mage_fireball_lv1
    parameters: { observable: uiElement, elementName: starting_skill__skill_mage_fireball_lv1, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: starting_skill__skill_mage_fireball_lv1
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
assertions:
  - kind: activePartyStartingSkillIdsEqual
    adapter: Map
    expected: [skill.amazon.thrust.lv1, skill.demonbound.mindfulness.lv1, skill.mage.fireball.lv1]
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

# Starting skills enter the adventure board

从正式 Home 和 Start Camp 开始，按点击队伍顺序完成起始技能选择；魔剑士的种子技能由正式规则自动选择。最终进入正式 Tile Adventure Board，并从运行状态断言三人的起始技能顺序。
