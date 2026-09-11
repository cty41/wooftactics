# Tactics Manual Acceptance Ledger

This is the current cross-project manual acceptance state. Stable IDs are authoritative; list numbers are temporary.

## Pending

### MQA-GODOT-POET-RUNTIME — 诗人开局、技能表现与临时分身

- Status: `pending`
- Source: 第五职业诗人、六套技能与临时分身功能实现
- Action: 使用三个 disposable Run，在 Start Camp 确认五名候选均有独立站位；每次选择“诗人”与另外两人，并分别从剑仙、诗仙、酒仙起步。进入战斗后至少实际使用一次侠客行、剑雨、将进酒、行路难、月下独酌和山中与幽人对酌；观察冲锋/范围攻击、三次回合开始治疗、敏捷提升、自疗净化，以及按施法前朝向后退并在原地留下分身。
- Expected: 候选和战斗 UI 显示“诗人”，第五候选不重叠或崩溃；已批准五红土松 Idle 在南/西使用 DR、北/东使用 UL，只有东/西镜像，正式颜色不受额外蓝色 tint 污染。缺失动作安全回退到相应方向 Idle，Death 暂用不镜像 DR；分身继承同一诗人身份但以半透明青色明显区分。技能按钮、目标范围、动作反馈、HP/MP/状态变化容易理解；后退和生成原子完成，敌人在合法直接攻击范围内优先攻击分身。
- Observe: Start Camp 候选区、Battle action bar、棋盘单位/状态层、HP/MP、事件日志、CheatConsole 与 Godot Output。
- Preserve on failure: Run seed、起始分支、技能等级、双方格子与朝向、施法前后 HP/MP/状态、截图或短视频、battle checkpoint/save 副本和完整 Output。
- Save boundary: 选择队伍、成长和战斗会修改当前 Run；只使用 disposable/隔离存档，不覆盖生产存档。
- Automated evidence: Core 205/205、Application 199/199、Poet focused 13/13、Resource/Catalog 185、Poet/Initiative/Actor GdUnit 10/10 与 29/29 Gameplay journeys已通过；运行时 DR/UL 副本哈希绑定获批母图，ResourceSaver 断言白色 tint、方向回退、Death 占位与分身清理。技能公式、合法性、持续时间、硬 AI 分身优先和五候选结构由自动化覆盖；动作可读性、半透明分身辨识度和职业体验仍需人工判断。
- User verdict: none.

### MQA-GODOT-INITIATIVE-HUD — 动态先攻头像条、Hover 联动与输入锁

- Status: `pending`
- Source: Core 权威动态先攻与右上角圆形头像行动条实现
- Action: 在 disposable battle 中依次完成普通行动、施放行路难改变未行动单位敏捷、召唤或替换召唤物、击杀一个单位，并让一个带不可行动状态的单位轮到行动；悬停友方和敌方头像，再把队列扩展到十个以上单位观察省略号。
- Expected: 右上条只显示当前与未行动单位，当前头像为金框、友敌底色明确、相同先攻保持玩家方优先；已行动头像消失，未行动头像按新先攻平滑重排，召唤物按当前先攻插入。队列最多显示九个头像和不可交互的“…”；隐藏队列变化时省略号脉冲。Hover 显示白框，并在棋盘对应单位上显示阵营色轮廓及 HP/MP；约 0.22 秒 Tween 期间点击不会重复提交，结束后输入立即恢复。不可行动单位自动跳过但只消费一次 EndTurn/状态时长。
- Observe: 右上 initiative strip、Round 标签、头像框与省略号、棋盘单位轮廓/HP/MP、action bar、事件日志、CheatConsole 和 Godot Output。
- Preserve on failure: 短视频、Round、完整可见/隐藏队列、各单位先攻与 acted 状态、悬停单位 ID、点击时刻、事件日志、Run seed/checkpoint 和 Output。
- Save boundary: 战斗行动和召唤会修改 disposable Run；悬停本身只读，失败时先复制 checkpoint 再继续。
- Automated evidence: Core/Application 覆盖逐轮重排、未行动队列同步、召唤插入/替换、昏迷自动 EndTurn 和无重复行动；Godot tests 覆盖九头像省略、0.22 秒过渡、五候选和生产输入锁，Gameplay journeys 29/29 通过。动画手感、Hover 对应关系、视觉层级和高速连续操作仍需人工验收。
- User verdict: none.

### MQA-GODOT-MAW-BAT-SLICE — 大嘴蝠、浅水与 N2 实战纵切

- Status: `pending`
- Source: 大嘴蝠 EnemySlice、通用移动地形与 N2 浅水布局实现
- Action: 使用隔离存档进入 N2，观察大嘴蝠待机、跨越地面单位/浅水移动、咬击命中与吸血、受击和死亡；分别悬停浅水与普通地面，并至少完成三局相同队伍/不同 seed 的 N2。
- Expected: 大嘴蝠稳定悬浮且移动时显示在地面单位上方，咬击使用双方向专用动作并在伤害后显示有效吸血，死亡时停止悬浮、短暂下落后落尸；浅水呈蓝绿色静态波纹且悬停显示“浅水”。Land 进入浅水明显消耗 2 移动力，Air 可越过但不能停在占位/障碍上；Predatory Diver 优先可击杀的低 HP 目标，低血时不撤退。三局无卡死，并记录 N2 难度相对原体验是否明显漂移。
- Observe: N2 棋盘、单位 Body/Shadow/状态层、悬停 Tooltip、Turn Order、战斗事件日志、CheatConsole 与 Godot Output。
- Preserve on failure: Run seed、双方阵容与格子、行动前后 HP、目标选择、短视频或连续截图、战斗 checkpoint/save 副本和完整 Output。
- Save boundary: 战斗和结算会修改当前 Run；必须使用 disposable/隔离存档，不覆盖生产存档，失败时先复制 checkpoint 再重试。
- Automated evidence: Core 146 项与 Application 183 项覆盖移动成本、Air 穿越/落点、绝对障碍、召唤落点、吸血事件顺序和真实 Transition 可击杀排序；Resource/Catalog、AI Encounter、Unit 与 Playable UI headless 验证通过。悬浮节奏、素材接触、浅水可读性和 N2 体验平衡仍只能人工判断。
- User verdict: none.

### MQA-GODOT-UNIT-VISUAL — Unit Gallery and Spawn framing baseline

- Status: `pending`
- Source: 大嘴蝠加入后 13-unit Gallery/Spawn 布局更新
- Reopen reason: Unit Catalog 新增大嘴蝠，Gallery 从 4×3 改为 5×3、缩放与间距变化，SpawnFixture 增加第 13 个位置。
- Action: 打开 Unit Gallery 与 Unit SpawnFixture，依次切换四方向和死亡模式，重点检查大嘴蝠及最外圈单位。
- Expected: 13 个单位及标签均在 1600×900 安全区内，无重叠、裁切或明显比例跳变；旧单位方向、羊魔染色和死亡显示保持原基线，大嘴蝠素材在两个夹具中均正确装载。
- Observe: Unit Gallery、Unit SpawnFixture、画布边缘和 Godot Output。
- Preserve on failure: 当前夹具截图、单位 ContentId/索引、方向/死亡模式及第一条 Output 异常。
- Save boundary: 只读预览，不修改 Run 存档或正式 Resource。
- Automated evidence: 13 个唯一 Spawn cell、资源类型、纹理与边界检查已自动覆盖；新版 5×3 视觉密度与标签可读性仍需人工复验。
- User verdict: 原 Phase 4 基线曾通过；本次因布局与内容变化重新 pending。

### MQA-GODOT-AGENT-FIRST-EDITOR — Clean worktree Editor and Godot AI Dock startup

- Status: `pending`
- Source: Unified multi-worktree Godot development launcher
- Action: In a fresh worktree, start with `Tools/godot/Open-GodotDev.ps1`; after the first `CODEX_RESTART_REQUIRED`, restart Codex once, rerun the entry, and inspect the Editor and Godot AI Dock. Repeat while another Godot worktree Editor is open.
- Expected: The correct project opens without manually running `dotnet build`; both C# EditorPlugins remain enabled; the Godot AI Dock is visible; the Agent session routes only to this worktree; the other worktree Editor remains running and unchanged.
- Observe: Editor title/project path, Project Settings plugin list, Godot AI Dock/session list, `.godot/tactics-dev-session.json`, and Output errors.
- Preserve on failure: Launcher output, both worktree paths/PIDs, plugin state, Dock screenshot, session listing, and first Godot error.
- Save boundary: Use worktree-isolated user data; do not use `SharedManualQA` for Agent acceptance.
- Automated evidence: Vendor hash, launcher policy, Codex bootstrap/profile, production assembly identity, release exclusion and same-worktree mutex checks are automated. Real Dock visibility, Editor reload and two-window routing remain human-only.
- User verdict: none.

### MQA-GODOT-DEMONBOUND-ACTION-ART — Demonbound native pose integration

- Status: `pending`
- Source: Approved Demonbound Idle, Melee, Cast, Hit and Death artwork integration
- Action: In a representative battle, rotate Demonbound between DR/UL, trigger melee, cast and hit in both native directions, then defeat the unit.
- Expected: Every state uses Demonbound artwork rather than Hunter placeholders; action poses return to the correct Idle, Cast/Hit sword layering remains intact, and Death is centered without unexpected mirroring or scale jumps.
- Observe: Body silhouette, sword/hand occlusion, feet, transition offsets and corpse placement at normal Game View scale.
- Preserve on failure: Screenshot, facing, action state and first incorrect texture/offset.
- Save boundary: Use an isolated test Run; do not overwrite the production save.
- Automated evidence: ResourceSaver-generated `PureRunDemonbound.tres` binds nine Demonbound textures, texture-copy hashes match approved sources, and automated Resource loading/assertions pass. Automation does not establish visual transition quality.
- User verdict: Pending runtime visual verification after integration.

### MQA-GODOT-DEMONBOUND-HUD — Active-unit card, corruption meter and hover tooltip

- Status: `pending`
- Source: Demonbound active-unit status-card implementation
- Action: Enter a disposable battle containing Demonbound. Observe at least one player turn, one enemy turn, corruption values in each visual band (0–4, 5–8, 9–10), and the possessed state. Hover units, empty cells, legal/illegal skill targets and one LOS blocker near every screen edge.
- Expected: The top-left card always follows only the current actor, including enemies during their turns; portrait, name, HP and MP remain readable; units without a special resource omit the third row; Demonbound shows a continuous `N/10` corruption bar with three clear risk colors and a legible possessed pulse. Hover detail follows the pointer, stays inside the canvas, preserves targeting/LOS information and never blocks board input.
- Observe: Top-left BattleUnitPanel, corruption bar, turn order, pointer tooltip, target highlights, CheatConsole and Godot Output.
- Preserve on failure: Screenshot or short video, resolution, active unit ID, corruption value, hovered cell/target, exact pointer position, Run seed and Output excerpt.
- Save boundary: Ordinary battle actions mutate the current Run; use a disposable Run or preserve the battle checkpoint before testing possession.
- Automated evidence: Godot UI tests cover active-unit/faction projection, portrait and value binding, missing-special-row collapse, 0/4/5/8/9/10 stage mapping, possessed pulse state, tooltip canvas clamping and `MouseFilter.Ignore`. PlayableRun UI 39/39 and Gameplay Spec journeys 15/15 passed. Visual hierarchy, portrait crop, color discrimination, pulse comfort and pointer interference remain manual. The unified verifier later stopped on an unrelated dirty artwork provenance failure, not this UI slice.
- User verdict: none.

### MQA-GODOT-DEMONBOUND-BANE — Two-cell purple crescent readability and timing

- Status: `pending`
- Source: Demonbound Bane active-skill redesign
- Action: In a disposable battle, cast Hex: Bane in all four directions. Include one cast through a wall, one with only a far-cell enemy, and one with enemies in both the first and second cells. Repeat once at 2× or 4× speed and once while Demonbound is possessed.
- Expected: The actor uses the melee swing pose; a purple crescent travels exactly two cells, passes through walls and the first target, triggers the first-cell hit before continuing to the second-cell hit, and disappears only after the second cell. No persistent blade glow remains. Possession changes target hostility without changing the visual path.
- Observe: Battle board actors, target highlights, damage numbers, Debuff status overlay, corruption bar and Godot Output.
- Preserve on failure: Short video, direction, unit cells, wall cell, speed, corruption, possession state, Run seed and Output excerpt.
- Save boundary: Skill use mutates the disposable battle and Run resources; preserve the pre-battle checkpoint if testing possession.
- Automated evidence: Core/Application cover two-cell legality, wall piercing, near-to-far independent rolls, level damage/status progression and main-attribute scaling. Godot tests cover the two-segment programmatic FX queue and transient cleanup. Crescent shape, timing clarity and speed-dependent feel remain manual.
- User verdict: none.

### MQA-GODOT-DEMONBOUND-RUN — Three-party full-Run balance baseline

- Status: `pending`
- Source: Demonbound continuous-loop implementation and fixed-seed automation checkpoint
- Action: Use three disposable Runs containing Demonbound, once each with Mage+Amazon, Mage+Necromancer, and Amazon+Necromancer. Complete the Run while recording the seeded starting branch, peak corruption, Meditation count, first possession timing, friendly damage/Down/permanent death, and any dominant or unusable skill.
- Expected: Four-select-three and the seeded starting skill are understandable; Charisma growth and all non-master skill chains remain usable; corruption creates deliberate risk; Meditation choices are legible; possession cannot silently change faction or stall battle/settlement.
- Observe: New Run party selection, progression cards, Battle HUD corruption and disabled reasons, skill effects, settlement, terminal Summary, CheatConsole and Godot Output.
- Preserve on failure: Run seed, party, growth choices, encounter/node, turn/action sequence, corruption history, save/backup copy, screenshot or short video, and Output excerpt.
- Save boundary: Each journey mutates and completes a Run; use disposable saves and do not overwrite a diagnostic failure before copying it.
- Automated evidence: Core/Application, Resource/Catalog, Workbench round-trip, Gameplay Spec v3 and the unified verifier cover deterministic legality and state transitions. The 3x10 fixed-seed suite is an automated balance signal only; it does not prove full-Run feel.
- User verdict: none.

### MQA-GODOT-DEMONBOUND-POSSESSION — Possession readability and terminal behavior

- Status: `pending`
- Source: Demonbound possession AI, possessed-form boost/projection, unified target pool, permanent-death luck correction and special-victory implementation (2026-08-22 P1–P3)
- Execution checklist: [`demonbound-possession-manual-checklist.md`](demonbound-possession-manual-checklist.md)
- Action: In a disposable battle, push Demonbound to 10 corruption and observe the possessed form: the actor tint changes to the possessed-form color, the corruption bar pulses `POSSESSED`, boosted HP/MP and projected skill levels are visible on the status card, and the possessed AI chooses from a unified pool (allies, enemies and summons) by value rather than always preferring allies. Confirm the AI can attack allies and enemies in the same battle, never changes faction, and a possessed sole survivor still wins after enemies are defeated. If practical, replay a fixed setup until both ordinary Down and permanent-death outcomes are observed, and verify a permanently dead character does not appear in the next battle while staying on the terminal Summary.
- Expected: The possessed actor is immediately distinguishable (form tint, POSSESSED pulse, boosted resources/skill levels); unified targeting never hard-codes a faction; friendly lethal damage rolls permanent death exactly once with the lucky-correction formula and settlement clearly reflects the result; permanently dead characters are excluded from subsequent battles but remain as tombstones in the Summary.
- Observe: Actor glow/tint, corruption/POSSESSED label, boosted HP/MP and skill levels, target choice, event log, Down/permanent roster state, settlement and terminal Summary, and the next battle's party composition.
- Preserve on failure: RNG seed/state, unit cells and HP, selected skill, event sequence, battle checkpoint, save/backup copy, screenshot/video and full Output.
- Save boundary: Permanent death mutates the current Run roster; use a disposable Run and preserve the pre-battle checkpoint.
- Automated evidence: Possessed-form state/boost idempotency, skill projection, unified target pool, luck-corrected one-time permanent-death settlement, dead-character battle exclusion and tombstone preservation are covered by Core/Application/GdUnit tests and the fixed-seed probe. Visual distinction, unified-target readability and cross-battle roster clarity remain manual.
- User verdict: none.

### MQA-GODOT-DEMONBOUND-WORKBENCH — Corruption-cost authoring round-trip

- Status: `pending`
- Source: Demonbound typed Resource and Skill Workbench integration
- Action: Duplicate one Demonbound skill to a disposable authoring resource, change `executionProfile.corruptionCost` and `executionProfile.damageScaling`, Validate and Apply, Reload the C# assembly, confirm both values, then Undo/Redo and delete the disposable copy through the Workbench transaction.
- Expected: Catalog, typed Resource and Workbench preserve both values through Apply, Reload, Undo and Redo; validation failures or cancellation create no partial resource, UID or reference side effects.
- Observe: Skill page field, diagnostics, Catalog/reference audit, filesystem result and Godot Output.
- Preserve on failure: Source/destination ContentId and path, revision, exact action, resource copy, screenshot and Output.
- Save boundary: Never edit a canonical Demonbound resource for this acceptance; use a disposable duplicate and Workbench lifecycle actions only.
- Automated evidence: Authoring compiler, ResourceSaver generation, byte-idempotency, Catalog ownership and semantic round-trip tests pass; real Editor Reload interaction remains manual.
- User verdict: none.

### MQA-GODOT-OWNERSHIP-CONTENT — Lv3, Treasure and authoritative Map journey

- Status: `pending`
- Source: `82c073f5`, `78f032a3`, ownership closure content checkpoints
- Action: In a disposable Run, obtain and use one player Lv3, resolve a Treasure node, Reload, and continue through the authoritative Map.
- Expected: Lv2 upgrades to the implemented Lv3 contract; Treasure resolves once without rerolling or duplicate rewards; Map route, pending node and current V10 identity survive Reload.
- Observe: Progression cards/current skills, Battle HUD and CheatConsole, Treasure result, Rogue Map node state, Inventory and Godot Output.
- Preserve on failure: Run seed/revision, selected skill/branch, Treasure node/result, save and backup copy, screenshot and Output excerpt.
- Save boundary: This journey mutates progression, rewards and the current V10 save; use a disposable Run or preserve the current save first.
- Automated evidence: Core/Application cover all nine player Lv3 contracts, Skeleton Warrior Lv3, deterministic Treasure rewards/idempotency, arbitrary Map topology and current V10 round-trip; historical migration behavior has separate compatibility tests. Catalog, ResourceSaver idempotency, Gameplay Specs and both renderers cover the structural boundary. Gameplay feel and cross-page readability remain manual.
- User verdict: none.

### MQA-GODOT-CONTENT-WORKBENCH — Unified authoring and fixture shell

- Status: `pending`
- Source: `6cf74ce0`, unified Godot Content Workbench checkpoint; 2026-08-16 reload-safe Resource loading repair; 2026-08-17 unified authoring kernel and Unity-style hierarchy; 2026-08-18 three-surface navigation, Event/Treasure graph and TS→MCP authoring chain
- Reopen reason: Event/Treasure navigation and graph interaction changed after the earlier partial pass.
- Action: Open Tactics Tooling and confirm the only top-level tabs are Map, Event and Skill / Presentation. In Event, open one Event and one Treasure: inspect the constrained Event choice graph, drag nodes, toggle a Check between None and an attribute, confirm hidden Failure data returns, then edit/reorder a Treasure weighted row. Use disposable drafts on Map and Event and complete Validate All → Apply All → Undo → Redo. Finally preview one native Presentation, perform one C# Reload and repeat the preview.
- Expected: Treasure appears as an Event-page resource category rather than a top-level tab; Encounter, AI, Audio and QA do not instantiate visible pages. Event shows Start→Option→Check→Success/Failure→End without arbitrary Branch/cycle semantics; Auto Success hides but does not erase Failure data. Treasure shows Root→Gold/Equipment/Consumable/Buff and edits weighted rows in the Inspector. Layout survives Apply/Undo/Redo/Reload, the global batch remains one Undo action, and page switch/Reload leaves no temporary preview nodes or Output errors.
- Observe: Tactics Tooling top tabs, Event resource list, GraphEdit, right Inspector, global status, Presentation SubViewport and Godot Output.
- Preserve on failure: Tab name, selected ContentId/resource path, exact action, screenshot, full Output and whether Assembly Reload occurred.
- Save boundary: Use read-only validation/preview actions unless working on a disposable resource copy; do not overwrite canonical content during first acceptance.
- Automated evidence: Full `Verify-GodotProject.ps1` passes: Core 132, Application 176, Frozen Oracle 15, MCP protocol, TypeScript authoring compiler, isolated GdUnit (including Editor lifecycle 6/6 and authoring round-trip 6/6), Gameplay journeys 27/27, both renderers and Release isolation. Layout backward compatibility/revision, unknown node rejection, three-tab navigation, eight typed spec kinds, dependency cycles and Create `initialSnapshot` are covered. `PlayableRunUi` retains its separately tracked four-orphan-node infrastructure warning while passing 39/39. Graph readability, drag/Inspector feel and real Editor Reload remain human-only.
- User verdict: Partial pass: after rebuilding C# in the real Editor, the repeated `AuthoringWorkspaceCoordinator` Output errors stopped. Remaining page interaction and visual checks are still pending.

### MQA-GODOT-FORMAL-UI — Formal Pure Run visual shell

- Status: `pending`
- Source: `3c9a29bc` through `422b9caa`, Godot root UI closure
- Action: Traverse Home, Options, Rogue Map, Progression, Inventory, Battle HUD, Settlement, Pause and Terminal Summary in one disposable Run and judge the shared hierarchy and readability.
- Expected: Near-black background, translucent panels, orange focus/accent and white/gray text form one coherent UI; Map detail, growth cards and Inventory three-column layout remain readable; no decorative panel blocks a button or board input.
- Observe: Main scene pages, focus/hover/pressed/disabled states, Battle HUD safe areas, Pause overlay and Godot Output.
- Preserve on failure: Screenshot with page name and resolution, exact control/action that was obscured or blocked, current Run state and Output excerpt.
- Save boundary: New Run, purchases and progression mutate the active save; use a disposable Run or preserve a copy before the journey.
- Automated evidence: Theme states, semantic panels, Control bounds, mouse filters, production input nodes, five isolated Gameplay Specs, Compatibility/Forward+ and Catalog 131 are asserted. Visual balance, text density and interaction feel remain manual.
- User verdict: none after the formal Theme and page-shell pass.

### MQA-GODOT-DAMAGE-NUMBERS — Floating combat feedback

- Status: `pending`
- Source: Phase 8E camera/menu/damage/growth transaction
- Reopen reason: Poison tick now has an explicit committed Impact cue instead of falling through at frame completion.
- Action: Visually sample Miss, healing, and MP recovery once at a comfortable speed; normal, Poison and critical feedback no longer need repeating.
- Expected: Healing shows green `+N`, MP recovery blue `+N MP`, and Miss gray; each appears above the affected unit at its committed impact, pauses with presentation, and leaves no residue.
- Observe: Unit head anchor, animation timing, HUD speed, and Godot Output.
- Preserve on failure: Screenshot/video plus event log and speed/pause state.
- Save boundary: Ordinary battle state changes apply; use a disposable Run if exact replay matters.
- Automated evidence: Gameplay Specs now execute deterministic Miss and Mana recovery through Main.tscn and assert event identity, color node, pause/speed behavior, cleanup and production-save isolation. Healing event mapping is covered at the Application/presentation layer; only readability and animation feel remain manual.
- User verdict: Partial pass: normal damage, Poison tick and gold critical numbers were observed; Miss, healing and MP recovery remain unconfirmed.

### MQA-GODOT-RELOAD-OUTPUT — Reload and diagnostics cleanup

- Status: `pending`
- Source: Phase 7B–8E combined acceptance; 2026-08-16 ExportRelease dependency-graph isolation repair
- Action: Trigger one real Godot C# Assembly Reload, Continue the active Run, replay one battle action, and inspect logs.
- Expected: No stale Tween, temporary node, duplicate signal, input lock, corrupted save, or Unicode/NUL error.
- Observe: Godot Output and CheatConsole; verify current page and actor state visually.
- Preserve on failure: Full Output excerpt, page/encounter, save copy, and reproduction order.
- Save boundary: Reload may restart the current battle from its committed checkpoint; retain save evidence on failure.
- Automated evidence: Gameplay Specs restart the real Main scene/process, Continue a PendingBattle, verify normalized state, input and zero temporary presentation nodes, and prove the production save unchanged. ExportRelease now uses an isolated Godot project/artifacts root and preserves the Editor dependency graph hash; the verifier preflight, build, GdUnit suites and 15 Gameplay Spec journeys passed with `GodotSharpEditor/4.7.1` present. Two later full-verifier attempts were interrupted by unrelated native-host exits after those assertions passed, so they are not recorded as complete green runs. Only the real Editor Assembly Reload lifecycle remains manual.
- User verdict: none after latest lifecycle-affecting changes.

### MQA-GODOT-DEFEAT-FLOW — Party defeat terminal flow

- Status: `pending`
- Source: Phase 7B–8E summon AI and defeat-flow repair
- Reopen reason: The prior Elite run stalled after the visible party died; friendly summon ownership and terminal submission now share the automatic controller path.
- Action: In one disposable encounter, let the final player-faction entity die and observe the visible transition into Defeated Summary and Home.
- Expected: The last defeat presentation reads naturally, the Summary appears once, and Return Home has no visible hitch or duplicate page.
- Observe: Battle phase, Turn Order, presentation queue, terminal summary, Home, CheatConsole, and Godot Output.
- Preserve on failure: Keep the battle open; record surviving units including summons, current actor, playback pause state, event log, Run seed/revision, and Output.
- Save boundary: The current Elite checkpoint is valuable diagnostic evidence; do not overwrite or abandon it before copying the save.
- Automated evidence: Gameplay Specs and Application tests cover with/without summon survival, automatic summon turns, one-shot BattleResult, presentation drain, Defeated Summary, Return Home cleanup and production-save isolation. Only visual transition feel remains manual.
- User verdict: Failed before this repair: after the whole visible party died in the first Elite battle, the battle remained stuck instead of showing defeat and returning Home.

### MQA-GODOT-INVENTORY — Backpack and loadout workflow

- Status: `pending`
- Source: Phase 7B–8E Inventory projector and navigation parity repair
- Reopen reason: Inventory now consumes Application-owned base/bonus/total/derived projections and is reachable only from Rogue Map.
- Action: From the Rogue Map, inspect one purchased Equipment item, equip it, and judge whether the base/bonus/total and slot presentation are easy to understand.
- Expected: The selected item, affected slot and positive/negative deltas are readable without guessing; Inventory remains a Map-only workflow.
- Observe: Inventory item detail, equipment slots, character stats/derived stats, Rogue Map entry, Home menu, and Output.
- Preserve on failure: Screenshot, item definition/instance ID, character before/after values, source route, save copy, and Output.
- Save boundary: Purchases and loadout operations persist; keep a copy of the Store save.
- Automated evidence: Gameplay Specs now equip through the production Main UI, enter a real battle, compare BattleUnitState against Application base/bonus/total and derived projection, Reload, and prove instance uniqueness plus production-save isolation. Only UI readability and interaction feel remain manual.
- User verdict: Partial pass: Inventory operation and equipment detail UI are OK; the resulting battle-stat increase has not yet been verified in combat.

### MQA-GODOT-WINDOWS-RC — Windows export and clean launch

- Status: `pending`
- Source: `eba98f9b`, GitHub Actions run `31889338418`
- Action: Download `tactics-godot-windows-eba98f9b-19` and launch its EXE/PCK on a Windows machine without the Godot or Unity Editor installed.
- Expected: The package contains production managed assemblies and no GdUnit/TestPlatform payload; Main opens, New Run works, and exit leaves no startup/resource errors.
- Observe: GitHub Actions artifact/build manifest or local `Build/Godot/Windows`, launched game window, and process/console output.
- Preserve on failure: Workflow run URL or local build log, build manifest, artifact file list/hashes and startup Output.
- Save boundary: Use a clean user-data directory; do not point the RC at the current production save.
- Automated evidence: GitHub Actions run `31889338418` passed the Godot-owned verifier, Windows ExportRelease, 199-file package audit, Compatibility/default renderer EXE startup, and artifact upload. Artifact ID `9248204605`, archive digest `9eaf62b652eee81dbfb18e74f702c3c8a016903876fe9336fe815ffb506f1456`, semantic manifest SHA-256 `dff242be1586f85e99cd1fa2f84ebd734306d1286145aa3679b2dcf6373d39ca`.
- User verdict: Pending one clean-machine download, launch, New Run, and exit smoke; automated CI must not mark this passed.

### MQA-GODOT-AVAILABILITY-TURNS-LOS — Skill availability, defeated turns, and LOS

- Status: `pending`
- Source: 2026-08-16 Godot shadow-cone LoS contract repair
- Reopen reason: Current Godot LoS changed from diagonal supercover to open-interior shadow-cone geometry, so the previously accepted corner-blocking behavior is intentionally different.
- Action: Recreate the battle arrangement from the reported screenshot, select the Mage's Ice Bolt, target the nearest upper-left enemy across the allied unit's corner, then compare with a second arrangement where a living unit clearly occupies the ray interior.
- Expected: The corner-touching target is legal and Ice Bolt can be committed; the true interior blocker remains illegal and Hover identifies the nearest blocking cell/unit. Corpses and dropped spears remain non-blocking.
- Observe: Target highlights, Hover rejection/detail, committed Ice Bolt action, CheatConsole and Godot Output.
- Preserve on failure: Screenshot, actor/target/blocker cells, skill ID, Hover text, event log and full Output excerpt.
- Save boundary: Ordinary battle mutation; use a disposable Run if the exact encounter state must be replayed.
- Automated evidence: Core covers corner/edge tangency, non-axial interior crossing and nearest blocker; Application proves Ice Bolt preview and commit through a corner-touching ally; GdUnit covers the Godot contract boundary. Visual targeting and the reported isometric arrangement remain manual.
- User verdict: none after the shadow-cone contract change.

### MQA-GODOT-TILE-READABILITY — Adventure Tile readability

- Status: `pending`
- Source: Gameplay-Test-Driven Tile Adventure Goal
- Action: From Start Camp, traverse every implemented adventure node type and inspect the 10×10 isometric board at the supported window sizes.
- Expected: Walkable cells, blocked cells, current position, actors, interaction objects, route exits, and node state changes remain distinguishable without relying on debug text.
- Observe: Adventure board, tile highlights, actor/object silhouettes, overlay, and Godot Output.
- Preserve on failure: Screenshot with window size, node ID, leader cell, pointer position, and Output; do not continue to the next node.
- Save boundary: Visual inspection and hover do not mutate the Run; entering or resolving a node does.
- Automated evidence: Projection round trips, board readiness, cell coordinates, object state, and node lifecycle assertions pass through the formal Main scene.
- User verdict: None; pending human acceptance.

### MQA-GODOT-TILE-INPUT-FEEL — Adventure click and leader-switch feel

- Status: `pending`
- Source: Gameplay-Test-Driven Tile Adventure Goal
- Action: Click near cell edges and occupied cells, move the leader along short and long valid paths, switch portraits, and repeat movement with each available leader.
- Expected: Intended cells and actors are selected consistently; paths commit once; the selected leader changes clearly; fixed Idle companions do not appear to accept movement.
- Observe: Pointer highlight, route preview, leader portrait, actor motion, final cell, and Godot Output.
- Preserve on failure: Short video or sequential screenshots, clicked screen position, expected/actual cell, leader ID, and Output; stop before another input hides the state.
- Save boundary: Leader selection is transient; committed movement and interactions may mutate the Run checkpoint.
- Automated evidence: AdventureCell and AdventureActor production-input targets, leader-switch checkpoints, and final coordinate assertions pass.
- User verdict: None; pending human acceptance.

### MQA-GODOT-TILE-SCENE-CHANGE — Interaction and event-battle scene changes

- Status: `pending`
- Source: Gameplay-Test-Driven Tile Adventure Goal
- Action: Resolve a normal treasure, cursed chest, and guarded altar, including the transition into battle and return to the changed adventure scene.
- Expected: Each interaction has an unambiguous before/after state; event battle opens once with the correct context; victory returns to the same node with the chest or altar visibly resolved.
- Observe: Interaction object, scene overlay, battle context, post-battle board, reward state, and Godot Output.
- Preserve on failure: Pre/post screenshots, node and object IDs, battle context, reward summary, save/backup copy, and Output; do not retry the interaction.
- Save boundary: Interaction and battle settlement mutate the Run; preserve the pre-event checkpoint and production backup.
- Automated evidence: Interaction-settled, Event Battle Ready, overlay-updated, event-result, battle-context, and idempotent-state assertions pass.
- User verdict: None; pending human acceptance.

### MQA-GODOT-TILE-ROUTE-MISCLICK — Immediate exits and accidental transition risk

- Status: `pending`
- Source: Gameplay-Test-Driven Tile Adventure Goal; `781fd5fd`, immediate-successor Tile exits
- Reopen reason: The earlier Route Overview and two-route submission flow was removed; route choice now occurs through visible exits inside the current node scene.
- Action: At every implemented multi-exit node, identify each visible destination, click near an exit edge, between adjacent exits and on unrelated board/background cells, then deliberately enter one exit.
- Expected: Only the current node's directly reachable successors appear; near-miss/background clicks do not transition; a valid exit click transitions immediately and exactly once to the indicated node, without an overview or separate submit step.
- Observe: Adventure Board exit objects/labels, pointer highlight, destination node/overlay, lifecycle state and Godot Output.
- Preserve on failure: Screenshot or short video, pointer position, source/expected/actual node IDs, visible exit set, save/backup copy and Output; stop before another transition.
- Save boundary: Inspection and near-miss clicks should not mutate the Run; a valid exit immediately commits the next node, so preserve the pre-exit checkpoint.
- Automated evidence: Formal Main-scene input proves that each exit targets only an immediate successor, locked/non-successor nodes cannot be selected, and the transition commits once. Destination readability and accidental-click risk remain human-only.
- User verdict: None; pending human acceptance.

### MQA-GODOT-TILE-START-CAMP — Start Camp presentation and setup flow

- Status: `pending`
- Source: Gameplay-Test-Driven Tile Adventure Goal
- Action: Start a new Run, choose the party by clicking characters in order, choose each starting skill, and enter the adventure board.
- Expected: Selection order is readable and reversible before confirmation; each character receives exactly one visible starting skill choice; confirmation enters the camp/board without duplicate actors or stale prompts.
- Observe: Camp composition, order indicators, skill cards, confirmation state, initial board, and Godot Output.
- Preserve on failure: Screenshots of every setup page, selected order and skills, initial actor cells, save/backup copy, and Output.
- Save boundary: Draft selections remain transient until final confirmation; confirmation creates or replaces the active Run checkpoint.
- Automated evidence: Formal click-order setup, starting-skill selection, Board Ready, actor coordinates, and fixed-seed setup assertions pass.
- User verdict: None; pending human acceptance.

### MQA-GODOT-TILE-ESCORT-DIFFICULTY — Lost villager escort readability and difficulty

- Status: `pending`
- Source: Gameplay-Test-Driven Tile Adventure Goal
- Action: Accept the lost-villager escort, cross the relevant nodes, and play the escort battle while allowing enemies at least one opportunity to threaten the villager.
- Expected: Escort ownership and destination remain clear; villager AI behavior is understandable; enemy threat is noticeable but recoverable; villager death and party defeat produce the documented failure, while survival produces one reward and completion.
- Observe: Escort overlay, villager position/HP, enemy target choices, objective result, reward, node state, and Godot Output.
- Preserve on failure: Run seed, node/battle IDs, turn sequence, villager HP/cell, enemy targets, result, save/backup copy, and Output.
- Save boundary: Accepting, battle turns, and settlement mutate the Run; use the pre-battle checkpoint for replay and keep production backup unchanged.
- Automated evidence: Escort state, protected-NPC AI, enemy priority target, special victory/failure, reward, current V10 round-trip, and idempotent settlement assertions pass.
- User verdict: None; pending human acceptance.

## Passed

### MQA-ARTWORK-DEMONBOUND-DEATH — Rounded death Sprite identity and size

- Status: `passed`
- Source: Demonbound Death Round v04 visual review
- Action: Compare Demonbound Idle DR, previous Death v02, approved Hunter Death and Round v04 at the same 128-pixel scale.
- Expected: Round v04 preserves the Demonbound identity, X eyes, detached sword and cohesive rounded corpse; its centered `56×48` preview footprint does not introduce a visible scale problem relative to the approved references.
- Observe: `Tools/artworks/doge/reviews/doge_capsule_demonbound_death_round_size_compare_v05.png`.
- Preserve on failure: Comparison PNG, selected candidate and exact observed size or identity mismatch.
- Save boundary: Artwork review only; no Godot runtime Resource or save data is changed.
- Automated evidence: Artwork strict inventory reports 385 items with zero issues; release Sprite validation reports 129 files with zero failures. Automation covers RGBA, chroma residue, centered AABB and paired preview, not visual approval.
- User verdict: User explicitly reported `ok 没问题` after reviewing the four-way size comparison on 2026-08-20.

### MQA-ARTWORK-DEMONBOUND-HIT-UL — Hit UL recoil, rear layering and right-hand sword

- Status: `passed`
- Source: Demonbound Hit UL v3 visual review
- Action: Compare the approved Demonbound Melee UL sword reference, accepted swordless Hit UL v2 and right-hand sword Hit UL v3.
- Expected: The native rear three-quarter body recoils toward screen-left while both ears trail screen-right; the body covers both feet upper edges; exactly one visible eye and tear remain; the anatomical right hand at screen-left grips the approved compact narrow ancestral sword without changing the accepted body pose.
- Observe: `Tools/artworks/doge/reviews/doge_capsule_demonbound_hit_ul_sword_compare_v03.png`.
- Preserve on failure: The comparison image and the first incorrect ear, foot, hand, sword or body edge.
- Save boundary: Offline artwork review only; no runtime asset or Run save is modified.
- Automated evidence: Invocation/delivery and immutable human feedback are recorded, and artwork strict provenance validation passes. Semantic masking, deterministic calibration, complete bound reviews, approval receipt and runtime integration remain outside this visual verdict.
- User verdict: Passed explicitly on 2026-08-20; approval is limited to the Hit UL v3 visual arrangement.

### MQA-ARTWORK-DEMONBOUND-HIT-DR — Hit DR body pose and reaction language

- Status: `passed`
- Source: Demonbound Hit DR v2 visual review
- Action: Compare the approved Demonbound Idle DR identity, approved Hunter Hit reaction language, failed Hit DR v1 and swordless Hit DR v2.
- Expected: Demonbound keeps its gray-black identity and red collar while the rigid capsule core leans screen-right; folded ears, widened eyes, exactly two tear streaks and the tense mouth read as the approved hit peak; the incorrect generated sword is absent so the approved Demonbound sword can be assembled separately.
- Observe: `Tools/artworks/doge/reviews/doge_capsule_demonbound_hit_dr_identity_pose_review_v02.png`.
- Preserve on failure: The comparison image and the first identity, pose or expression mismatch.
- Save boundary: Offline artwork review only; no runtime asset or Run save is modified.
- Automated evidence: Invocation/delivery and immutable feedback receipts are recorded, and artwork strict provenance validation passes. Chroma cleanup, semantic masking, final equipment assembly and runtime integration remain outside this visual verdict.
- User verdict: Passed explicitly on 2026-08-20; approval is limited to the swordless Hit DR v2 body frame.

### MQA-ARTWORK-DEMONBOUND-CAST-UL — Cast UL component depth and silhouette

- Status: `passed`
- Source: Capsule sprite component assembly v08 visual review
- Action: Compare the approved Cast DR pose with Cast UL v08 and inspect the feet, hidden paws, reused sword silhouette and body occlusion.
- Expected: Both paws are fully hidden behind the back-facing body; the accepted DR sword keeps its width and scale, sits behind the body and exposes only the short tip between the ears; both feet retain the accepted partial body occlusion.
- Observe: `Tools/artworks/doge/reviews/doge_capsule_demonbound_cast_dr_ul_sword_depth_compare_wip_v08.png`.
- Preserve on failure: The comparison image, exact component transform and the first incorrect visible edge.
- Save boundary: Offline artwork review only; no runtime asset or Run save is modified.
- Automated evidence: Artwork strict provenance validation passes. Layering, silhouette readability and 3D interpretation remain human-only.
- User verdict: Passed explicitly on 2026-08-20; approval is limited to the Cast UL v08 visual arrangement and does not authorize runtime integration.

### MQA-GODOT-FULL-RUN — Complete Run shell and route recovery

- Status: `passed`
- Source: Phase 8E Boss settlement transaction revision fix
- Reopen reason: The second live replay proved terminal detection and presentation drain completed; the actual failure was `save.non_increasing_revision` after terminal transition reused revision 148. The transition and settlement coordinator are now fixed and reviewed.
- Action: Continue the preserved Boss checkpoint, defeat the final enemy, inspect the terminal settlement lines, then use Return Home.
- Expected: The final animation completes, settlement advances from Submitting to Saved and NavigationCompleted, BossVictory Summary appears once, and Return Home ends the Run.
- Observe: Battle page, CheatConsole `BattleSettlementDiagnostic`, BossVictory Summary, Home Continue state, and Godot Output.
- Preserve on failure: Keep the page open; copy all CheatConsole logs, retain the production save and backup, and record the last settlement stage/error.
- Save boundary: This replay commits the terminal Summary and ends the Run.
- Automated evidence: Terminal revision increases through ApplyFullRunTransition and a controlled store; ActiveRun becomes null, Summary is one-shot, failed readback recovery and duplicate callbacks are guarded.
- User verdict: Passed in the latest report.

### MQA-GODOT-CHEAT-CONSOLE-COPY — Read-only battle log copying

- Status: `passed`
- Source: Phase 8E Boss settlement diagnostics and CheatConsole copy
- Action: Open CheatConsole, drag-select several lines and copy with Ctrl+C and the right-click menu; then test Copy Visible under a filter and Copy All.
- Expected: Selected text copies normally; Copy Visible contains only rendered filtered lines, Copy All contains all retained lines, and no console interaction triggers battle commands.
- Observe: CheatConsole selection/context menu/status text and an external text editor used only for paste verification.
- Preserve on failure: Screenshot, selected filter, copied text, and any Godot Output error.
- Save boundary: Console copying does not mutate the Run or save.
- Automated evidence: Selection mode, filtered/all rendering, injected clipboard behavior and gameplay input blocking are asserted.
- User verdict: Passed in the latest report.

### MQA-GODOT-MYSTERY-ADJUDICATION — Fixed-option deterministic event adjudicator

- Status: `passed`
- Source: Phase 7B–8E Mystery adjudicator redesign
- Action: Enter a Mystery node, note the assigned party member and all fixed options, Reload before choosing, then Continue and resolve one option.
- Expected: The assigned member, options, rates and eventual result remain deterministic across Reload.
- Observe: Mystery page, result page, Continue and Output.
- Preserve on failure: Run seed, event ID, assigned character, options/rates and save copy.
- Save boundary: Resolving an option mutates the Run.
- Automated evidence: Deterministic assignment, option attributes, roll and Save V5 round-trip are asserted.
- User verdict: Passed in the latest report.

### MQA-GODOT-SUMMON-CONTROL — Unity summon AI ownership parity

- Status: `passed`
- Source: Phase 7B–8E frozen summon AI and controller repair
- Reopen reason: Skeleton Warrior, Skeleton Mage and Fire Demon now resolve internal AI/loadout by Unit ContentId; Decoy is explicitly non-acting.
- Action: Summon Skeleton Warrior, Skeleton Mage and Fire Demon, then advance to each summon turn.
- Expected: AI-authored summons act automatically; Decoy remains non-attacking.
- Observe: Turn Order, input lock, CheatConsole AI log and unit actions.
- Preserve on failure: Summon type/level, current actor, AI log and Output.
- Save boundary: Ordinary battle mutation.
- Automated evidence: Internal resources, loadouts, automatic turns, input rejection and Decoy skip are asserted.
- User verdict: Passed with no anomaly in the latest report.

### MQA-GODOT-PROGRESSION-ATOMIC — Non-skippable atomic growth

- Status: `passed`
- Source: Phase 8E camera/menu/damage/growth transaction
- Action: Allocate an attribute, close before choosing a skill, Continue, then finish progression once.
- Expected: Continue restarts at attribute allocation; final confirmation applies once and unlocks the next node once.
- Observe: Progression pages, map node state and Home Continue.
- Preserve on failure: Save copy, screenshots and Output.
- Save boundary: PendingProgression persists; unfinished UI drafts are not written.
- Automated evidence: Revision, V5 normalization, one-shot transaction and unlock semantics are asserted.
- User verdict: Passed with no anomaly in the latest report.

### MQA-GODOT-TURN-PACING — Playable enemy initiative pacing

- Status: `passed`
- Source: Phase 7B–8E playable enemy speed profile
- Action: Observe the first two rounds of a normal encounter.
- Expected: Enemy archetypes retain relative differences without the entire enemy team consistently preceding all player characters.
- Observe: Turn Order and CheatConsole.
- Preserve on failure: Encounter ID, Turn Order and actor sequence.
- Save boundary: Ordinary battle mutation.
- Automated evidence: Speed profile generation, player preservation and derived-stat recomputation remain asserted.
- User verdict: Passed in the latest report.

### MQA-GODOT-L4-RECOVERY — Layer 4 legacy-save recovery

- Status: `passed`
- Source: Phase 7B–8E L4 map invariant repair
- Reopen reason: The live V5 save reached `AwaitingLayerFourChoice` with three completed battles but no MapState, leaving all Layer 4 routes locked.
- Action: Continue the preserved current Run and enter Layer 4.
- Expected: Layer 4 routes are reachable and the selected route advances to the first Elite battle.
- Observe: Rogue Map, selected route, Continue, and Godot Output.
- Preserve on failure: Save/backup, Run seed/revision, map screenshot, and Output.
- Save boundary: Uses the current persistent Run.
- Automated evidence: Legacy V5 repair, deterministic projection and invariant checks remain covered.
- User verdict: Passed; the Run has advanced to the first Elite encounter.

### MQA-GODOT-FIRE-DEMON-COMBAT — Dedicated Fire Demon attack

- Status: `passed`
- Source: Phase 7B–8E frozen Fire Demon attack migration
- Action: Summon a Fire Demon and use its dedicated attack.
- Expected: The attack targets correctly and follows its frozen damage, Ignite, range and per-turn contract.
- Observe: Fire Demon action, target HP/status, CheatConsole, and Output.
- Preserve on failure: Cells, HP before/after, log and current turn.
- Save boundary: Ordinary battle mutation.
- Automated evidence: Frozen contract, Resource identity, damage/status/no-crit and use limit remain covered.
- User verdict: Passed in the latest report.

### MQA-GODOT-HUD-SKILL-PRESENTATION — HUD and committed skill visuals

- Status: `passed`
- Source: Phase 8E compact action bar and committed-presentation highlight fix
- Reopen reason: Action buttons now use display names and compact second-line costs; active-tile highlighting is suppressed during committed action playback.
- Action: Check the compact action bar, vertical Lightning, overlapping alpha-aware selection, Poison Spear held/unarmed/drop/Pickup or Recover, HUD controls, and Backquote CheatConsole.
- Expected: Basic attacks have no MP line; skill IDs have no `skill.` prefix; positive MP cost is on line two; during action animation the acting unit has no selected tile. Remaining skill/HUD visuals follow their committed state.
- Observe: Action buttons, actor feet/tile, battle actors, HUD, hover detail, CheatConsole, and Output.
- Preserve on failure: Screenshot/video, selected actor ID/tile, button text, skill event log, and current encounter.
- Save boundary: Skills mutate the battle; use a replayable encounter checkpoint.
- Automated evidence: Label formatting, action-state marker suppression, event-derived targets, alpha hit ordering, spear lifecycle, console guards, and renderer smoke are asserted; sizing and visual parity remain manual.
- User verdict: Passed in the latest user report.

### MQA-GODOT-PAUSE-MENU — Esc pause and safe exit flow

- Status: `passed`
- Source: Phase 8E pause overlay hierarchy and menu scope fix
- Reopen reason: The reported overlay rendered below actors and exposed a noncanonical Save and Quit action; the fix raises the overlay and removes that action.
- Action: During a battle press Esc, exercise Continue, Options/Back and Main Menu, then open Esc again while targeting and while CheatConsole is visible.
- Expected: The dark overlay and menu cover all actors/HUD; only Continue, Options and Main Menu appear; targeting/Console take Esc precedence; Quit remains only on Home.
- Observe: Pause overlay, battle HUD, Home menu, playback state, and CheatConsole.
- Preserve on failure: Screenshot and exact Esc/menu action sequence; keep the Run and Output open.
- Save boundary: Main Menu preserves the Active Run and Continue restarts from the committed pre-battle checkpoint.
- Automated evidence: Z-order, menu actions, input blocking, pause ownership and absence of Save and Quit are asserted; visual stacking and interaction feel remain manual.
- User verdict: Passed in the latest user report.

### MQA-GODOT-BOARD-FIT — Isometric board framing and input

- Status: `passed`
- Source: Phase 8E camera/menu/damage/growth transaction
- Action: Open a battle, resize across 16:9, 16:10, and 21:9, then hover and click edge and overlapping units.
- Expected: The complete 10×10 board is centered without the old right-side reserve; HUD stays clear and pointer selection remains accurate.
- Observe: Battle board, HUD bounds, hover details, and selected tile/unit.
- Preserve on failure: Screenshot with resolution, selected coordinate, and current encounter.
- Save boundary: Uses the current Run; resizing and selection do not mutate the save.
- Automated evidence: Board AABB fitting and screen/grid round trips are asserted; visual framing and pointer feel remain manual.
- User verdict: Passed in the latest user report.

### MQA-GODOT-PROGRESSION — Growth priority and three-card UI

- Status: `passed`
- Source: Phase 7B–8E selected-starting-branch and class-attribute fixes
- Action: Historical growth priority, requirements, card details and current skills acceptance.
- Expected: Three unique candidates with correct branch priority and class attribute requirements.
- Observe: Progression attribute and skill pages.
- Preserve on failure: Screenshot, character, starting skill, attributes and all cards.
- Save boundary: Drafts remain transient until final confirmation.
- Automated evidence: Ordering, uniqueness, metadata, branches and requirements remain covered.
- User verdict: Passed in the latest user report.

### MQA-GODOT-UNIT-MOTION-CONTACT — Unit motion, hit, defeat, and ground contact

- Status: `passed`
- Source: Phase 8 presentation parity fix after user screenshot feedback
- Reopen reason: Fourteen approved Mage/Necromancer/Amazon Cast/Hit/Melee/Thrown textures are now migrated and combined with the existing programmatic motion.
- Action: In one battle, move at least two cells; cast with Mage and Necromancer; use Amazon Thrust and Poison Spear; receive a nonlethal hit; defeat one unit; pause midway, then resume at 0.5× and 4×.
- Expected: Player Body switches to the correct directional action pose during the authored window and returns to idle at Release/Recovery; enemies/summons safely use programmatic fallback. Move sway, hit recoil, lethal collapse and corpse landing remain serial; Shadow and overlays do not inherit Body deformation; dead units show no status icons.
- Observe: Unit Body, feet, Shadow, status layer and HP/MP anchor during Move, Hit and Defeat.
- Preserve on failure: Short video or sequential screenshots, actor/unit, speed, cue type, and Godot Output.
- Save boundary: Uses the current battle checkpoint; presentation speed and pause do not alter gameplay state.
- Automated evidence: Hash-bound 14-texture converter, ResourceSaver references, directional pose/fallback tests, Body-only transforms, interrupt cleanup, serial cue order, death status cleanup, GdUnit, Compatibility and Forward+ are asserted; motion feel and contact spacing remain manual.
- User verdict: Passed in the latest user report.

## Deferred or Blocked

### MQA-GODOT-POET-DECOY-ART — 青川犬正式分身美术

- Status: `deferred`
- Source: 诗人功能优先范围决定；当前分身继承获批诗人 Idle 并使用半透明青色占位
- Action: 后续美术任务中完成青川犬身份、四方向、受击/消散与 Tile 落点设计，经逐图人工审核后再接入分身 Resource。
- Expected: 正式分身在正常战斗缩放下可立即识别为青川犬，与诗人本体、敌方和其他召唤物不混淆；方向、脚底锚点、直接命中段数反馈和消失表现清晰。
- Observe: Artwork review panel、Unit Gallery/SpawnFixture、Battle board、分身受击/替换/消失和 Godot Output。
- Preserve on failure: 母图/候选版本、prompt/job/attempt ID、方向、Tile 对比截图、运行时截图与 Output。
- Save boundary: 美术生产只能通过 artwork pipeline；未获人工批准不得替换当前运行时素材或改写生产存档。
- Automated evidence: 当前半透明诗人 Idle 占位的 Resource 类型、装载、显式动作/死亡清理、分身规则和战斗清理已自动验证；独立正式分身美术尚未生产，不能自动验收。
- User verdict: Deferred by the user; formal Qingchuan Hound artwork is outside this implementation round.

### MQA-GODOT-AUDIO-ASSETS — Licensed audio payload and listening pass

- Status: `deferred`
- Source: `0b2a3c81`, Godot audio framework checkpoint
- Action: Provide or approve an audio pack whose license permits redistribution, then audition Music/SFX/UI cues through the Audio Workbench and one complete Run.
- Expected: File-level provenance/hash is recorded; Master/Music/SFX/UI buses, volume/mute, concurrency and cleanup sound correct without changing gameplay timing.
- Observe: Audio Workbench, bus meters/settings, battle/page transitions and Godot Output.
- Preserve on failure: Asset/license identity, cue/bus, reproduction step and Output.
- Save boundary: Audio settings use their own versioned settings file and do not modify Run Save.
- Automated evidence: Bus creation, independent settings, cue/profile validation, concurrency and cleanup framework are tested. No redistributable audio payload is currently registered, so content migration and listening acceptance cannot be claimed.
- User verdict: Deferred by the user for this version; the Windows RC is intentionally silent and does not require an audio payload.

## Last Emitted Order

1. `MQA-GODOT-POET-RUNTIME` — 诗人开局、六套技能与临时分身
2. `MQA-GODOT-INITIATIVE-HUD` — 动态先攻头像条、Hover 与输入锁
3. `MQA-GODOT-POET-DECOY-ART` — 青川犬正式美术（deferred）
