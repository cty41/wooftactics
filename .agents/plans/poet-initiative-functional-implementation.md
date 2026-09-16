# 诗人第五职业、权威先攻系统与图标化行动条实施计划

## Summary

先收敛战斗先攻双模型并修复生产链，再实现第五职业“诗人”的六技能、Core 权威朝向、通用临时属性、分身 AI、五选三 Run 集成及右上角图标行动条。

成功标准：

- `InitiativeRoundState` 成为生产战斗唯一的本轮行动资格模型。
- 每轮既有可行动单位最多行动一次；轮中只重排 remaining；新轮按当前有效先攻全量重建。
- 状态、召唤、死亡和 AI 均原子同步单位事实与行动队列。
- 诗人以 `6/5/5/4/6/4`、Strength 主属性加入五选三，六技能按已批准设计运行。
- 右上角行动条使用十槽圆形头像、稳定右对齐、Tween 和悬停战场联动。
- 不实现实际诗人装备；只写后续完整装备设计。
- 定向测试、Gameplay Spec 与 `Tools/godot/Verify-GodotProject.ps1` 全部通过。

## Frozen Decisions

### Initiative

- 排序：有效先攻降序 → `PlayerNumber` → `SpawnOrdinal` → `UnitId` ordinal；同值玩家优先。
- current/acted 不因轮中变化重复行动；remaining 稳定重排；新轮全量重建。
- 新行动召唤物按先攻进入当前轮 remaining，不能插到 current 前。
- 召唤替换完全开放：旧召唤即使已行动，新 InstanceId 仍视为未行动，可在资源允许时同轮多次行动。
- 非行动分身排除；不能行动状态仍进入队列，轮到时自动跳过并消费 EndTurn。
- 死亡立即移出 eligible；reload 是重开遭遇，不是 mid-battle resume。

### 行路难

- Lv1/Lv2 敏捷 +2/+4、8 MP、自身中心曼哈顿半径 2。
- 敏捷只派生先攻和命中：Lv1 +4 initiative/+10 accuracy，Lv2 +8/+20。
- 持续到目标下一次 EndTurn：施法者当前 EndTurn 到期；pending ally 本轮 EndTurn 到期；acted ally 保留到下一轮自身 EndTurn。
- 同名取最高修正并刷新，不叠加。

### Initiative HUD

- 右上角，Round 标签在左；只显示 current+remaining，acted 退出。
- 十个视觉槽：最多 9 个单位＋不可交互 `…`；隐藏队列变化时 `…` 闪动。
- 全部头像同尺寸；current 金框，hover 白色粗框；友方绿色圆底、敌方红色圆底。
- 不显示 initiative 数字；最右边界固定，增减单位时左扩右收。
- hover 联动战场阵营色 Shader outline 与 HP/MP meter，不改变目标/选择/Core。
- 200–250ms Cubic Out Tween 纳入表现输入锁；Core 先提交，连续变化取消旧 Tween 并追赶最新 snapshot。
- 死亡在命中/死亡表现后缩小淡出；召唤淡入；新轮更新 Round 并 Tween 重排。

## Current State

- 生产 `BattleState` 使用固定 `TurnOrder + ActiveIndex`，而较完整的 `InitiativeRoundState` 只在 helper/golden 测试中运行。
- 状态会改变 `Unit.Initiative`，普通技能施加与状态到期却只 `WithUnit`，实际顺序不变。
- 新轮不按当前 initiative 重建；召唤替换可使 ActiveIndex 错位；新召唤仅追加队尾。
- Godot HUD 当前在 `GodotPlayableRunMain` 中拼接内部 ID 文本。
- Facing 目前是 Godot 表现状态；状态系统没有通用六维临时修正。
- Pure Run 只接受 3/4 候选；装备已有背包/槽位/投影，但没有类型、职业许可或起始装备。

## Implementation

### 1. Contracts and documentation

- 新建 `.agents/docs/poet-class-design.md`、`.agents/docs/battle-initiative-rules.md`、`.agents/docs/equipment-system-future-design.md`。
- 更新 attribute/buff/facing/skill/run 文档并新增：
  - `TURN-INITIAL-ORDER-001`
  - `TURN-DYNAMIC-INITIATIVE-001`
  - `STATUS-INITIATIVE-SYNC-001`
  - `TURN-SUMMON-ELIGIBILITY-001`
  - `TURN-DEFEAT-ELIGIBILITY-001`
  - `TURN-AI-PROGRESS-001`
  - `TURN-CHECKPOINT-RELOAD-001`
  - `INITIATIVE-HUD-QUEUE-001`
  - `INITIATIVE-HUD-INTERACTION-001`
  - `INITIATIVE-HUD-TWEEN-001`
  - `INITIATIVE-HUD-RIGHT-ALIGN-001`
  - `ATTR-CLASS-IDENTITY-002`
  - `ATTR-TEMPORARY-MODIFIER-001`
  - `FACING-CORE-AUTHORITY-001`
  - 六个 `POET-*` 技能合同及 `POET-RUN-001`。

### 2. Core initiative authority

- 将 `InitiativeRoundState` 纳入 `BattleState`，集中管理 Current/Remaining/Acted；兼容投影不得成为第二套 mutation authority。
- 建立 `BattleTurnEligibility`，保证 acting live unit 恰好一次进入 eligible；非行动 summon/死亡排除。
- 新轮从当前 eligible/effective initiative 重建；状态和属性变化统一走 state-level initiative-aware replacement。
- 修复召唤替换 active identity；新 summon 使用新 InstanceId 并按开放行动语义加入 remaining。
- AI active identity 只能经正式 `TurnAdvancedEvent` 转移。

### 3. Attributes, statuses and facing

- 区分 battle base/effective attributes；状态增加六维 `AttributeModifiers`，旧资源默认零。
- 状态施加、刷新、驱散、到期统一重算派生值和 queue；属性最低 1，上限下降钳制当前 HP/MP。
- 新增 TargetTurnStart tick 和 TargetEndTurn duration；确定性 harmful cleanse。
- Core 新增 `UnitFacing`/`FacingResolver`：玩家东、敌人西；移动按最终步，目标技能面向目标，自施法保持；预览不提交。
- 分身锁定施法前 Facing，向后退一格且本体/分身保持原面向；不实现背刺。

### 4. Poet skills

- `SkillRole.Poet`、Strength 主属性、近战普通攻击、明确 execution kinds/profile 参数。
- 侠客行：6 MP，轴向射程 3/4/5，首敌前停；Lv1 `1+STR`，Lv2/3 加 `max(0,STR-5)`；Lv3 直接击杀返 6 MP。
- 剑雨：12 MP，中心射程 4/LOS，曼哈顿半径 2；`3+floor(STR/2)`；Lv2 全局 25% 后相同区域 50% 追加，不递归。
- 将进酒：半径 2 友方，三次 TurnStart HoT；总量 `3+STR`/`6+STR`，余数优先早 tick；Lv3 每目标驱散最高优先 harmful。
- 行路难：按冻结敏捷、命中、queue 和 EndTurn 语义。
- 月下独酌：一次/回合；20%/40% MaxHP，Lv1 6 MP、Lv2/3 4 MP，Lv3 清除全部 harmful；无效果失败。
- 山中与幽人对酌：5 MP，后退留非行动分身；阻挡失败；单分身替换；距离 3 合法直接攻击优先；Lv1/2 1/2 successful direct hit charges。
- 抽取统一 direct damage segment 入口处理命中、暴击、击杀和分身 charge。

### 5. AI, Application and Run

- 分身优先只在距离≤3且有合法直接攻击时生效，并与护送 NPC/附身策略组合。
- Application snapshot 暴露 current/remaining queue、Facing、DefinitionId、阵营和状态摘要。
- Pure Run 扩展为 5 候选、active party 仍 3；加入诗人初始技能候选和 Strength 5/7 成长。
- 不新增 starting equipment，不修改 Inventory/Store/Equipment schema。

### 6. Godot initiative HUD

- 新建 `GodotBattleInitiativeStrip`，按 UnitInstanceId 持久化 icon view，禁止每帧全量重建。
- Unit presentation schema 增加可选 portrait texture 与 face crop/offset/scale；默认裁 idle，正式肖像可覆盖。
- 圆形 mask 合成 portrait 与阵营底色；临时诗人复用魔剑士纹理/tint。
- 新建不覆盖 Body material 的 sibling outline layer，跟随当前 pose/flip/scale，只描 body 不描 shadow。
- 合并 board hover 与 initiative hover 的 meter/outline 协调器。
- 替换现有 `_turnOrder` 文本；使用右上 anchors/safe margins，并迁移 Pause/Step/Speed。

### 7. Poet Resource generation

- 受测 `PoetAssetFactory/Builder` 通过 ResourceSaver 生成 13 个技能等级、诗人/分身单位、HoT/敏捷状态和 Catalog/Run/Balance 引用。
- 不手写 `.tres/.tscn`；重复生成保持 UID 和语义幂等。
- 临时复用魔剑士 Idle/Melee/Cast/Hit/Death 并使用独立冷色 tint/material；正式青川猎犬美术延期。

### 8. Future equipment design only

- 未来文档覆盖 EquipmentFamily、单双手/副手、五职业策略、起始装备、StarterOnly/Store pool、满背包与死亡退装、冻结 12 件分类、Resource/存档兼容、UI reason 和迁移里程碑。
- 本轮不修装备 bug，不生成诗人剑琴，不让技能依赖装备。

## Test Plan

- Core：生产链初始/动态/跨轮 initiative、current/pending/acted 状态变化、不能行动、死亡、召唤创建/替换/连续多次行动、Facing、六技能。
- Application：factory 完整性、AI EndTurn 连续性、queue snapshot、五选三及诗人成长/战斗投影。
- Godot：portrait schema、十槽右对齐、`…`、Tween、输入锁、outline/meter hover、ResourceSaver 幂等和 Catalog。
- Gameplay Spec：连续两轮验证行路难三种目标时序；真实输入验证诗人技能/分身/五选三；runtime 直接断言 current/remaining/effective attributes；reload 验证确定性重开。
- 顺序：focused Core → Application → Godot/GdUnit → Spec validate/compile/runtime → generation/scan → `Tools/godot/Verify-GodotProject.ps1`。

## Risks and Boundaries

- 召唤替换多次行动是明确玩法，不得被误修。
- initiative HUD 只投影 committed state，Tween 不拥有逻辑。
- 旧 `.tres/.tscn` 只能由 ResourceSaver/Editor 工具迁移。
- 不触碰用户已有 `godot/project.godot` 修改，除非后续确认它属于本任务且已读取。
- 不实现装备、背刺、master skills、音乐小游戏、正式美术/VFX/音频或 mid-battle save。

## Handoff and Completion

先读本计划、`.agents/docs/attribute-system-implementation-contract.md`、`buff-system-rules.md`、`battle-facing-rules.md`、`skill-graph-system.md` 及 battle/skill/run OKF 页面；先修 Core initiative，再做状态、技能和 HUD。

完成实现与验证后：

1. 将长期结论保留在 `.agents/docs/` 权威文档；
2. 未完成项进入统一缺口或经用户批准建立独立计划；
3. 运行 OKF impact，只同步本任务实际影响 scope；
4. 使用 `manual-qa-handoff` 记录视觉/手感人工验收；
5. 删除本活动计划，由 Git 保存历史。
