---
feature: AdventureBoard
scenario: RestCampfireResolution
tags: [godot, adventure-board, isolated-save, validated-checkpoint]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters:
      id: layer4-choice-ready-v1
      path: validated://layer4-choice-ready-v1
      semanticHash: 8876d2ebebbd451ac2ae5b3be2d018b26c24eba29c1cc03bcd2a5bea848bdf19
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: 3,7
    parameters: { targetKind: AdventureCell }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_04_rest"
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
    target: rest-campfire
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: Confirm Rest
    parameters: { observable: uiElement, elementName: Confirm Rest, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Confirm Rest
    parameters: { targetKind: UiElement }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
assertions:
  - kind: partyResourceSummaryEquals
    adapter: Map
    expected: ["pure_run_mage:12/20:5/15", "pure_run_necromancer:12/20:6/18", "pure_run_amazon:12/20:5/15"]
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

# Rest campfire resolution

从经过哈希验证的 Layer 3 已结算场景点击直接后继 Rest 出口，进入 Tile 场景，点击火堆打开正式结算界面并确认恢复。
