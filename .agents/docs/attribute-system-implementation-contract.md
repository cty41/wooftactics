# 属性系统实现合同

## 文档定位

本文档是 [属性与技能数值设计](attribute-system-design.md) 的程序落地层，保存类型标识、精确算式、内容编译要求、合同状态、验证路径和迁移清单。

玩法意图和平衡数值以设计文档为准；本文档不得独立改变玩法语义。

## 现有派生值合同

```gameplay-contract
id: ATTR-DERIVED-STATS-001
status: verified_current
statement: 战斗单位只消费 UnitDerivedStats 中显式且已校验的最大生命、最大法力、初始法力、移动范围和先攻；Godot Adapter 不得从六项主属性重新推导这些数值。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/UnitDefinitionTests.cs
  - layer: godot_test
    path: godot/tests/UnitBatchGodotTests.cs
dsl_support: partial
```

```gameplay-contract
id: ATTR-DERIVED-STATS-002
status: verified_current
supersedes:
  - ATTR-DERIVED-STATS-001
statement: 最大生命、最大法力、初始法力、移动范围和先攻由战斗有效六维属性及固定单位移动特性统一派生；最大生命为体质乘四且最低一，最大法力为魅力乘三，初始法力为魅力，移动为二加体质二分之一向下取整再加移动特性并限制在二至五，先攻为敏捷乘二。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/UnitCombatStatRulesTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/UnitDefinitionCompilerTests.cs
  - layer: godot_test
    path: godot/tests/UnitBatchGodotTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: ATTR-VALUE-BOUNDS-001
status: verified_current
statement: 六项主属性不得为负；最大生命必须大于零，最大法力和移动范围不得为负，初始法力必须位于零到最大法力之间，先攻必须是非负有限数。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/UnitDefinitionTests.cs
dsl_support: unsupported
```

## 类型与公式映射

可缩放效果节点使用以下稳定标识：

| 设计类型 | 程序标识 |
|---|---|
| 近战物理 | `MeleePhysical` |
| 远程物理 | `RangedPhysical` |
| 魔法 | `Magical` |
| 治疗 | `Healing` |
| 护盾 | `Shield` |
| 持续伤害 | `DamageOverTime` 加一个底层效果类型 |

定义：

- `P`：当前战斗有效主属性。
- `N = 6`。
- `InitialAverage = floor(InitialPermanentAttributeTotal / N)`。
- `EffectiveAdded = CurrentEffectiveAttributeTotal - InitialAttributeTotal`。

主属性职业：

- `MeleePhysical | Healing | Shield => max(1, P)`。
- `RangedPhysical | Magical => max(1, floor(P / 2))`。

全才职业：

- `MeleePhysical | Healing | Shield => max(1, floor(InitialAverage / 2 + EffectiveAdded))`。
- `RangedPhysical | Magical => max(1, floor(InitialAverage / 2 + EffectiveAdded / 2))`。

统一效果公式：

`FinalRawValue = SkillBase + AttributeContribution + ExplicitUniqueScaling`

多段每段使用 `floor(AttributeContribution / 2)`。持续伤害总值使用 `TotalBaseDot + floor(UnderlyingAttributeContribution / 2)`。

### 通用二级属性目标公式

- `MaxMana = max(0, Charisma * 3)`。
- `StartingMana = clamp(Charisma, 0, MaxMana)`。
- `ManaRecoveryPerTurn = max(0, Intelligence)`。
- `PostBattleHealthRecovery = max(0, Constitution * 2)`。
- `PostBattleManaRecovery = max(0, Charisma)`。
- `Initiative = Agility * 2`。
- `MoveRange = clamp(2 + floor(Constitution / 2), 2, 5)`。
- `Accuracy = 100 + (Agility - 5) * 5`，单位为百分点。
- `Dodge = max(0, 5 + (Luck - 5) * 5)`，单位为百分点。
- `CriticalChance = max(0, 10 + (Luck - 5) * 3)`，单位为百分点。
- `CriticalMultiplier = clamp(1.5 + (Strength - 5) * 0.05, 1.25, 2.0)`。
- `FinalHitChance = clamp((Accuracy - TargetDodge) * SkillAccuracyFactor, 0, 100)`。

`SkillAccuracyFactor` 默认为 `1.0`，作为命中结算的最后乘法因素。`Accuracy` 在该乘法和最终 clamp 之前不提前截止为 100，以便高敏捷抵消闪避与低精度。

## 目标合同

```gameplay-contract
id: ATTR-CLASS-IDENTITY-001
status: verified_current
statement: 五个玩家职业 Lv1 初始六维总和均为 30；法师为 4/5/3/6/6/6 且以智力为主属性，死灵法师为 5/3/6/6/6/4 且以体质为主属性，魔剑士为 6/4/6/6/6/2 且以魅力为主属性，亚马逊为六维全 5 且无主属性的全才职业，诗人为 6/5/5/4/6/4 且以力量为主属性。此规则不改变现有 MP 派生公式。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/UnitDefinitionTests.cs
  - layer: godot_test
    path: godot/tests/UnitBatchGodotTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: ATTR-TEMPORARY-MODIFIER-001
status: verified_current
statement: 战斗状态可携带六维有符号临时修正；所有活动状态按属性求和并以 UnitState.BaseAttributes 为基线生成 EffectiveAttributes，单项最低为 1。施加、刷新、驱散和到期都统一重算最大 HP/MP、法力恢复、移动、先攻、命中及其他六维派生值；上限下降时当前 HP/MP 钳制到新上限。旧式显式单位通过公式差值保持其原始基线数值。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StatusItemRuntimeTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: ATTR-SECONDARY-STATS-001
status: approved_target
statement: 独立速度属性被移除；敏捷派生先攻和命中率并承担远程物理成长，体质派生移动范围并承担生命与生命恢复，幸运派生闪避率和统一暴击率，力量派生统一暴击伤害倍率。命中与闪避每点变化 5 个百分点，暴击不区分物理与法术。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/UnitDefinitionTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/StartingSkillRuntimeTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: SKILL-HIT-RESOLUTION-001
status: approved_target
statement: 最终命中率先用攻击者命中率减去目标闪避率，再乘技能命中系数，最后限制在 0%至100%；多段每段、范围每目标独立判定，命中后状态与伤害共用命中结果。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StartingSkillRuntimeTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/StatusItemRuntimeTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: SKILL-PASSIVE-CONFIG-001
status: approved_target
statement: 被动技能的数值只读取各技能等级的基础配置，不应用主属性、全才成长或其他属性贡献；被动触发的独立标准动作仅按该动作自身规则结算一次属性。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StartingSkillRuntimeTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/DemonboundMeditationTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: ATTR-EFFECT-NODE-TYPE-001
status: approved_target
statement: 每个可应用属性贡献的效果节点必须显式标记近战物理、远程物理、魔法、治疗、护盾或带底层类型的持续伤害；缺失类型的可成长节点不得通过内容编译。
verification:
  - layer: application_test
    path: src/Tactics.Application.Tests/SkillDefinitionCompilerTests.cs
  - layer: godot_test
    path: godot/tests/StartingSkillBatchGodotTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: ATTR-COMBAT-EFFECT-SCALING-001
status: approved_target
statement: 技能数值保留技能基础值，再按效果节点类型叠加单一属性贡献；主属性职业的近战、治疗和护盾使用当前战斗有效主属性，远程和魔法使用其二分之一；全才职业使用初始六维平均值的二分之一加战斗有效属性总增量，远程和魔法只获得总增量的二分之一。计算向下取整且贡献最低为 1。战斗有效属性包含永久属性、装备和当前生效的临时属性变化。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StartingSkillRuntimeTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: ATTR-MULTI-EFFECT-SCALING-001
status: approved_target
statement: 单段效果对每个目标应用完整属性贡献，多段效果的每段只应用向下取整的半额贡献；持续伤害总值只应用底层类型半额贡献并在施加时冻结，余数优先分配给较早 tick。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StartingSkillRuntimeTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: ATTR-PERMANENT-PROGRESSION-001
status: approved_target
statement: 高级和大师技能解锁只使用角色永久属性；装备、Buff、Debuff、临时事件、变身和临时覆写不参与解锁。允许永久奖惩但单项属性最低为 1，禁止主动洗点、退还和免费重分配。已学技能在属性下降后保持可用，未学候选按当前永久属性判定；全才永久总增量 2 解锁高级、4 解锁大师。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/RunInventoryProgressionTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: SKILL-BASE-VALUES-001
status: approved_target
statement: 法师火球、闪电、寒冰直接伤害基础值分别为 3、2、1，死灵骨矛为 2，亚马逊突刺为 3、连续刺击每段为 1、毒矛直接伤害为 4 且 DoT 每次基础为 1、召唤长矛各级无伤害，魔剑士厄运魔刃、横扫、魔炎斩（原地狱冲击）和地狱火的直接伤害基础值均为 4。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StartingSkillRuntimeTests.cs
  - layer: godot_test
    path: godot/tests/StartingSkillBatchGodotTests.cs
  - layer: godot_test
    path: godot/tests/UnitBatchGodotTests.cs
dsl_support: unsupported
```

## 内容编译门禁

- 可缩放数值节点缺少显式类型时编译失败，不从射程、武器、动画或名称推断。
- 战斗数值读取战斗有效属性投影；装备、Buff、Debuff、临时事件、变身和 override 进入贡献公式，但不进入高级和大师解锁公式。
- 属性最低值为 1，但允许永久事件惩罚和未来 Down 代价降低属性。
- 不得提供 respec、refund 或 free redistribution 通路。
- 已学技能不因属性下降被禁用；未学候选使用当前永久属性。
- 召唤物不默认继承施法者贡献；装备效果不读取贡献；反射、复制、转移和分裂不重复应用贡献。
- 被动技能资产不得开启属性缩放；若触发独立动作，必须指向另一个显式动作定义。

## 职业专属资源映射

职业专属资源不加入 `UnitDerivedStats` 的通用数值集合，由可空的职业战斗状态承载。魔剑士当前使用 `DemonboundBattleState`：

- `Corruption`：范围 0–10。
- `MindfulnessLevel`：正念技能等级。
- `MeditationUsedThisTurn`、`BasicAttackUsedThisTurn`、`NonMeditationSkillUsedThisTurn`：冥想资格的回合内事实。
- `IsPossessed`：腐化到达 10 后的控制状态。

技能腐化配置存放在 `SkillExecutionProfile.CorruptionCost`。成功施放并完成效果后，使用以下规则提交：

`AppliedCorruption = IsPossessed ? 0 : max(0, CorruptionCost - (MindfulnessLevel >= 1 ? 1 : 0))`

`NewCorruption = clamp(CurrentCorruption + AppliedCorruption, 0, 10)`

当施放前尚未附身、新腐化到达 10 时，发布附身转换。取消、验证失败和非法目标不提交 MP、腐化或技能使用记录。

```gameplay-contract
id: ATTR-CLASS-SPECIFIC-RESOURCE-001
status: verified_current
statement: 职业专属资源使用可空的职业战斗状态承载，不隐式并入通用派生数值；魔剑士腐化是战斗局部 0至10 整数，技能成功结算后按配置增加，正念固定减少 1，达到 10 时在当次效果完成后进入附身，附身期间不再增加，战斗结束清除。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/DemonboundMeditationTests.cs
  - layer: godot_test
    path: godot/tests/UnitBatchGodotTests.cs
dsl_support: unsupported
```

## 实现分支迁移清单

1. 为技能效果节点增加显式类型与可缩放开关，对遗漏节点设编译门禁。
2. 建立单一属性贡献求值器，移除旧的攻击加值/技能加值拆分以及技能内隐式主属性公式。
3. 将死灵法师技能主属性改为体质，保持 MP 派生逻辑不变。
4. 将四职业 Lv1 初始六维迁移为总和 30 的目标表，亚马逊实现永久属性总增量与 2/4 技能解锁门槛。
5. 移除独立 `Speed`，用敏捷生成先攻与命中、用体质生成移动；迁移现有速度装备、减速状态、怪物和召唤物数值。
6. 实现统一暴击率/倍率和最后乘法的技能命中系数，重新校准旧技能命中惩罚。
7. 禁止被动技能读取属性贡献，保留被动触发独立标准动作的一次正常结算。
8. 按设计文档迁移技能基础值；各等级默认共用同一基础值。
9. 寒冰箭反弹使用独立节点，不对主目标已结算值重复加属性。
10. 毒矛 DoT 改为每次基础 1、基础 3 次，在施加时冻结加值并把余数优先分配到前置 tick。
11. 删除召唤长矛 Lv2 的相邻闪电伤害。
12. 保留厄运魔刃，把地狱冲击重命名为魔炎斩；魔剑士四个伤害技能全部标记为 `Magical`。
13. 恶魔再生保留 MaxHP 百分比特殊缩放，再加 `Healing` 类型的完整魅力贡献。
14. 骨盾在实现前先确定 `BaseShield`，再使用 `BaseShield + ConstitutionContribution`，不在旧 MP/魅力结果上叠加。
15. 同步 Core/Application 测试、Godot Resource 生成器、Catalog、当前数值表和 OKF；所有目标合同通过后才能从 `approved_target` 升级为 `verified_current`。

## 已知迁移风险

- 当前突刺 Lv1 Resource 与旧数值文档存在基础伤害漂移，目标统一为 3。
- 骨盾当前没有独立基础护盾值，该值仍是实现前的设计阻塞项。
- 当前横扫可能仍标记为物理伤害，实现目标是魔法伤害。
- 单段范围伤害对每个目标获得完整贡献，火球溅射、骨矛贯穿、横扫和地狱火需在实现后重点平衡验证。
