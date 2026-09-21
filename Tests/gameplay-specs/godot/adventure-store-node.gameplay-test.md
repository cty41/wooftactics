---
feature: AdventureBoard
scenario: StoreMerchantPurchase
tags: [godot, adventure-board, isolated-save, validated-checkpoint]
requiredAdapters: [Map, PlayerInput, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: layer4-choice-ready-v1, path: "validated://layer4-choice-ready-v1", semanticHash: 2d0ab502e474b2c61c413be755279126fa509dcf9cfb5afdb9ce3f66b20f9ac2 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: adventureBoardReady, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_04_store"
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
    target: store-merchant
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    target: item.consumable.life-potion
    parameters: { observable: uiElement, elementName: item.consumable.life-potion, maximumFrames: 180 }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: item.consumable.life-potion
    parameters: { targetKind: UiElement }
assertions:
  - kind: storeOfferCountEquals
    adapter: Map
    expected: 3
    parameters: {}
  - kind: storeSoldOfferCountEquals
    adapter: Map
    expected: 1
    parameters: {}
  - kind: backpackContainsContentId
    adapter: Map
    target: item.consumable.life-potion
    expected: true
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

# Store merchant purchase

从 Layer 4 地图进入 Store Tile，点击商人打开正式持久化库存，购买生命药水并断言库存、售罄数量与无限背包奖励。
