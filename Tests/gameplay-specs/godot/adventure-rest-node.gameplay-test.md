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
