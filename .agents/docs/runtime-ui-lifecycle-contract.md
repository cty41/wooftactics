# Runtime UI Lifecycle Contract

## Scope

This contract covers interactive runtime UI under `godot/src/Tactics.Godot.Adapter/Runtime`. Editor workbenches have a separate Editor-session lifecycle.

## Reentry policies

```gameplay-contract
id: RUNTIME-UI-REENTRY-001
status: approved_target
statement: Every runtime UI reused through hide and show declares Reset, Refresh, or Resume semantics; mutable Godot child nodes are a projection of owned state and never the only navigation state.
verification:
  - layer: godot_test
    path: godot/tests/PlayableRunUiGodotTests.cs
dsl_support: partial
```

- **Reset**: a new open session starts at its canonical root and discards transient navigation/focus. Pause and confirmation flows use this policy.
- **Refresh**: every show/bind projects current authoritative data. Battle HUD, active-unit panel, tooltip, meters, skill panel, board overlays, dropped spears, and damage-number pool use this policy.
- **Resume**: an explicit workspace state survives reopening. Cheat Console retains logs, filter, and scroll position but clears one-shot copy feedback.

## Ownership

```gameplay-contract
id: RUNTIME-UI-OWNERSHIP-001
status: approved_target
statement: Runtime UI caches are owned by Frame, Page, Open Session, Run, or Main; Page replacement, Open close, and RunId change clear state that belongs to the narrower owner before it can affect the next owner.
verification:
  - layer: godot_test
    path: godot/tests/PlayableRunUiGodotTests.cs
dsl_support: partial
```

| Surface/state | Owner | Reentry | Required boundary |
|---|---|---|---|
| Pause Overlay shell | Main | Reset content | Shell survives pages; each open rebuilds Root |
| Pause Root/Options, focus, pause ownership | Open Session | Reset | Close clears content/focus and restores only owned pause |
| Progression attribute draft | Run + transaction | Resume within owner | Key includes RunId; Run change clears |
| Inventory character/tab/item selection | Run | Resume within owner | Run change resets defaults |
| Page status label | Page | Refresh | Page replacement clears reference; fallback belongs to current page |
| Cheat Console log/filter/scroll | Battle Page | Resume | Copy feedback clears on open |
| Pause Abandon ConfirmationDialog | Pause Open Session | Reset | Child of Pause shell; keeps pause ownership; confirm/cancel/Close frees before routing |
| Active unit/tooltip/meters/skill panel | Battle snapshot/Page | Refresh | Bind/show rebuilds from current snapshot |
| Damage numbers and skill FX | Frame/playback cue | Refresh | Reuse overwrites presentation; teardown cancels/frees |
| Adventure/Start Camp board and movement | Page + RunId | Refresh | Page teardown cancels; resize syncs the last centered cell before relayout; session match includes RunId |
| Battle session reference | Battle Page | Reset | Settlement/Summary transition clears `_battle` before non-battle input can route |

## Pause session order

A hidden-to-visible Pause transition performs exactly once: reset view to Root, render from current `RunInputContext`, move the persistent Overlay to the last Main child, establish focus, acquire pause side effects, then show. Root/Options navigation never re-enters Open. Close hides, releases only pause effects acquired by that session, clears transient content/focus, and resets the view.

## Review rule

Any new mutable runtime UI that uses `Visible`, pools controls, persists controller fields, or survives Page replacement must document its owner and policy and add a regression of the form: mutate internal state, cross the owner boundary, reenter, and assert Reset/Refresh/Resume. Real input surfaces use `Viewport.PushInput`; direct signal emission alone is insufficient evidence for input ordering or reentry.
