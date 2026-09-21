---
feature: GodotPendingAcceptance
scenario: InventoryBattleProjection
tags: [godot, inventory, battle, isolated-save]
requiredAdapters: [Map, PlayerInput, Battle, UI]
setup:
  - kind: loadValidatedCheckpoint
    adapter: Map
    parameters: { id: inventory-store-ready-v1, path: "validated://inventory-store-ready-v1", semanticHash: f0178ece6249cfa8e978550fa36e3871ea2ecc2b7b05f138d1d76598e659d8b9 }
  - kind: initializePlayerInput
    adapter: PlayerInput
    parameters: {}
actions:
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Inventory
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: qa-armor
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Equip
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: Back
    parameters: { targetKind: UiElement }
  - kind: clickPointerTarget
    adapter: PlayerInput
    target: "exit:layer_01_battle"
    parameters: { targetKind: AdventureObject }
  - kind: waitForPlayerObservable
    adapter: PlayerInput
    parameters: { observable: battleReady, maximumFrames: 300 }
assertions:
  - kind: inventoryProjectionEnteredBattle
    adapter: Battle
    expected: true
    parameters: {}
  - kind: activeRunExistsEquals
    adapter: Map
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

# Inventory equipment projection into battle

通过正式 Map、Inventory 与棋盘输入链装备皮甲，再进入 N1；皮甲改变 Constitution/MaxHP，由 Application-owned 装备投影与真实 BattleUnitState 对账。
