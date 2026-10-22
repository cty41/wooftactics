# Run Input and Setup Transaction Contract

## Global input routing

```gameplay-contract
id: RUN-INPUT-ROUTER-001
status: approved_target
statement: GodotPlayableRunMain is the only keyboard routing authority; Escape is captured in the global _Input phase before focused GUI controls can consume ui_cancel, then resolves Console close, Pause close, Battle targeting cancel, or current-page pause in that order; pages only declare input capabilities.
verification:
  - layer: godot_test
    path: godot/tests/PlayableRunUiGodotTests.cs
dsl_support: partial
```

```gameplay-contract
id: RUN-PAUSE-SHELL-001
status: approved_target
statement: One Pause Overlay shell survives Current Page replacement for the lifetime of Main; every open starts a fresh Root session, rebuilds actions from the current context, restores the shell to the last Main child, and acquires pause ownership exactly once; Options and Back never reenter Open, while Close clears transient view/focus and restores only effects owned by that session.
verification:
  - layer: godot_test
    path: godot/tests/PlayableRunUiGodotTests.cs
dsl_support: partial
```

## New Run setup transaction

```gameplay-contract
id: ROGUELIKE-SETUP-COMMIT-001
status: approved_target
statement: Party and starting-skill choices remain process-local until the final required skill atomically saves one complete Active Run; incomplete setup is never resumed after process restart.
verification:
  - layer: application_test
    path: src/Tactics.Application.Tests/PureRunSessionServiceTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/RunSaveDocumentV11Tests.cs
  - layer: godot_test
    path: godot/tests/GameplaySpec/GodotGameplayRuntimeRunnerTests.cs
dsl_support: partial
```

## Failure and recovery semantics

- Incomplete setup performs no `IRunSaveStore.Save` and does not advance the persisted revision.
- The final required skill uses the draft's base revision for one compare-and-swap save, including the first battle checkpoint and PendingBattle state. Failure leaves the prior save authoritative, retains the draft and publishes no EncounterRequest. See [node-scene contract](roguelike-act-node-scene-contract.md).
- A replacement draft never hides or overwrites an existing Active Run until final commit.
- Historical persisted `PendingRunSetup` is discarded during compatibility recovery.
- Setup quit/discard creates neither an Active Run nor an Abandoned summary.
