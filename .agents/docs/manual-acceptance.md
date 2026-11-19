# 人工验收账本

当前只看一局。稳定 ID 仍是身份。已通过的旧结论在 [manual-acceptance-history.md](manual-acceptance-history.md)。

## Pending

### MQA-GODOT-SINGLE-RUN — 打一局，顺手看

- Status: `pending`
- 看什么: 用隔离存档打一局法师、死灵、亚马逊。开局选人页看顶栏有没有给右上角的地图让位，右侧灰框里的说明有没有留在框内。再看主菜单是不是只有一个主动作更醒目，然后打开路线图，缩小后看节点上的字会不会叠住、精英和宝箱能不能认出来。战斗里看左上单位卡的数字、头顶血条和飘字。路上看走位和出口连线、浅水和大嘴蝠、加点卡和背包、地图镜头、团灭或结算过场。愿意的话关一次进程再打开，看是不是回到原来的节点。
- 怎样算过: 这一局看着顺、字认得清、路线图缩小时不糊成一团。点击、存档、升级选谁、出口能不能点已经由测试断言，不用对答案。关进程这一下做不到就留着，不另开一条。看起来不对的地方记下来，这次不把观感写成通过。
- User verdict: none.

## Passed

已通过的结论在 [manual-acceptance-history.md](manual-acceptance-history.md)，不在这里重复。

## Deferred or Blocked

下面这些旧项现在不看。并进打一局的，是这一局本来就会经过的观感。其余是当前三人单局覆盖不到的切片。暂缓不是做完。

### MQA-GODOT-EXPLORATION-WALK-ATLAS-LAYOUT — 探索走位、连线观感与页面排版

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-MAW-BAT-SLICE — 大嘴蝠悬浮、浅水观感与 N2 难度

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-MAW-BAT-POSES-DEATH — 蝙蝠姿态与死亡落点观感

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-FORMAL-UI — 整局界面是否是同一套

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-DAMAGE-NUMBERS — 治疗、回蓝和落空飘字的观感

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-DEFEAT-FLOW — 团灭过场顺不顺

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-INVENTORY — 背包数值好不好懂

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-OWNERSHIP-CONTENT — 三级技能、宝箱和地图的跨页可读性

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-TILE-READABILITY — 冒险格子不用调试文字也能分清

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-TILE-INPUT-FEEL — 点格子和换领队的手感

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-TILE-SCENE-CHANGE — 交互前后现场是否一眼能看出来

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-TILE-ROUTE-MISCLICK — 出口会不会一点就误离开

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-START-FLOW — 真正重启进程后的第一屏

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-MAP-CAMERA — 开局总览好不好读、镜头顺不顺

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-START-ESC — 营地菜单好不好读，以及真正退出再开

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-TILE-START-CAMP — 起始营地的选人、移动和技能衔接

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-PAUSE-MENU — Esc 的本局控制

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-STALE-SAVE-RESET — 真正关掉进程再打开

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-TILE-RESIDENT-EXPLORATION — 探索页地图和角色看着像战斗里那一套

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-SETTLEMENT-BATTLE-HUD-POLISH — 加点页、技能卡、战后镜头和战斗抬头好不好看

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。打一局已经会经过，并入 `MQA-GODOT-SINGLE-RUN`，不再单独验收。

### MQA-GODOT-UNIT-VISUAL — 单位图库与出生夹具的画面密度

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-AGENT-FIRST-EDITOR — 编辑器与 Godot AI 面板的真实启动

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-DEMONBOUND-ACTION-ART — 魔剑士动作是否像正式素材

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-DEMONBOUND-HUD — 当前行动者卡片、腐化条和悬停是否好读

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-DEMONBOUND-BANE — 祸源紫月刃的形状和时机

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-DEMONBOUND-RUN — 带魔剑士打完整局的平衡手感

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-DEMONBOUND-POSSESSION — 附身能不能一眼认出来

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-DEMONBOUND-WORKBENCH — 编辑器里改腐化消耗的真实重载

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-CONTENT-WORKBENCH — 编辑器里拖节点和检查器的手感

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-RELOAD-OUTPUT — 编辑器里真正重载一次

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-WINDOWS-RC — 没有编辑器的机器上启动安装包

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-AVAILABILITY-TURNS-LOS — 等距画面上的视线好不好认

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-TILE-ESCORT-DIFFICULTY — 迷路村民护送清不清晰、难不难

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-POET-RUNTIME — 诗人动作、分身和职业手感

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-INITIATIVE-HUD — 动态先攻头像条、悬停和输入锁

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-POET-DECOY-ART — 青川犬正式分身美术

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

### MQA-GODOT-AUDIO-ASSETS — 可再分发的音频和试听

- Status: `deferred`
- Source: 2026-11-16 收窄为当前三人单局
- Action: 现在不单独看。
- Expected: 暂缓不是做完。回到当前要玩的内容时，再并回打一局或另开一行。
- Observe: 不要求本轮观察。
- Preserve on failure: 不要求本轮留证。
- Save boundary: 不使用生产存档。
- Automated evidence: 原有测试仍在。本轮不请人按这条对答案。
- User verdict: 2026-11-16 确认收窄。当前法师、死灵、亚马逊单局不覆盖，暂缓。暂缓不是做完。

## Last Emitted Order

1. `MQA-GODOT-SINGLE-RUN` — 打一局，顺手看
