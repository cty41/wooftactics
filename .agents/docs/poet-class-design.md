# 诗人职业设计与实现合同

状态：`verified_current`（正式青川犬美术延期；当前复用魔剑士纹理并使用独立蓝色调）

## 职业定位

第五个 Pure Run 候选职业为诗人，原型取李白的诗、酒、剑与音乐意象。诗句只负责包装机制，不反向限制规则。职业主属性为力量，基础六维为 `6/5/5/4/6/4`，分支为剑仙、诗仙、酒仙。五名候选仍只选择三名出战。本轮不实现装备要求、节奏玩法、诗意/醉意资源、跨分支组合、大师技能或背刺。

## 技能表

- 剑仙：侠客行 Lv1–3；剑雨 Lv1–2。
- 诗仙：将进酒 Lv1–3；行路难 Lv1–2。
- 酒仙：月下独酌 Lv1–3；山中与幽人对酌 Lv1–2。
- 开局三个候选技能是侠客行、将进酒、月下独酌 Lv1；其余通过既有分支成长解锁。

```gameplay-contract
id: POET-XIAKE-XING-001
status: verified_current
statement: 侠客行消耗 6 MP，沿施法者选择的水平或垂直方向搜索射程内首个敌人；墙和存活友军阻挡，尸体与掉落长矛不阻挡；没有首敌或没有合法停格时原子失败。成功时停在首敌前并对其独立判定命中与暴击，Lv1 伤害为 1+STR，Lv2/3 再加 max(0,STR-5)，Lv3 直接击杀返还 6 MP且可无限形成击杀链。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/PoetSkillRuntimeTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: POET-SWORD-RAIN-001
status: verified_current
statement: 剑雨消耗 12 MP，中心射程 4 且中心需要视线，命中中心曼哈顿半径 2 内所有敌人，区域内部墙不遮挡；每段基础伤害为 3+floor(STR/2)。Lv2 在主轮全部结算后只进行一次全局 25% 追加判定，成功则对仍存活的原区域目标以 floor(主段原始伤害*50%) 分别重投命中与暴击，且不递归。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/PoetSkillRuntimeTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: POET-JIANG-JIN-JIU-001
status: verified_current
statement: 将进酒在施法瞬间捕获自身中心曼哈顿半径 2 内可接受常规治疗的自身与友军，为每个目标冻结三次未来 TurnStart 治疗总量；Lv1 为 3+STR且 6 MP，Lv2 为 6+STR且 6 MP，Lv3 同治疗且 5 MP，并按硬控、行动或伤害削弱、易伤、DoT、其他的稳定优先级为每个目标驱散一个 harmful。不能整除的余数优先分配到较早 tick，同名重施刷新完整三 tick而不叠加。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/PoetSkillRuntimeTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/StatusItemRuntimeTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: POET-XING-LU-NAN-001
status: verified_current
statement: 行路难消耗 8 MP，为自身中心曼哈顿半径 2 内自身与友军施加敏捷 Lv1 +2 或 Lv2 +4；敏捷只通过统一六维派生链改变命中与先攻。同名状态保留最高修正并刷新、不叠加；持续到每个目标下一次 EndTurn，因此施法者在当前 EndTurn到期、尚未行动友军在本轮自身 EndTurn到期、已行动友军在下一轮自身 EndTurn到期。轮中只重排 remaining，不让任何既有实例重复行动。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/StatusItemRuntimeTests.cs
  - layer: core_test
    path: src/Tactics.Core.Tests/BattleTransitionTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: POET-MOON-DRINK-001
status: verified_current
statement: 月下独酌是自身技能，每个自身回合最多成功一次；按施法前 HP 判定，HP 至少 50% 时恢复 floor(20% MaxHP)，否则恢复 floor(40% MaxHP)，最低 1。Lv1 消耗 6 MP，Lv2/3 消耗 4 MP，Lv3 在治疗后驱散全部 harmful；满血且没有该等级可移除状态时不可用，失败不扣 MP、不记录成功次数。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/PoetSkillRuntimeTests.cs
dsl_support: unsupported
```

```gameplay-contract
id: POET-MOUNTAIN-DIALOGUE-001
status: verified_current
statement: 山中与幽人对酌消耗 5 MP且每回合不限次数，锁定施法前 Core Facing，向其反方向后退一格并在原格生成同朝向分身；越界、阻挡或占用时原子失败。每个诗人最多一个分身，重施以新 InstanceId 替换；分身不进先攻、不接受治疗、不产尸体/战利品/队伍记录，Lv1/2 吸收 1/2 个成功直接伤害段，每段只消耗一层，miss、零最终伤害和 DoT 不消耗。敌人在曼哈顿 3 内且当前存在合法直接攻击时优先攻击分身。
verification:
  - layer: core_test
    path: src/Tactics.Core.Tests/PoetSkillRuntimeTests.cs
  - layer: application_test
    path: src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs
dsl_support: unsupported
```

## Godot 表现边界

诗人和分身均为 Godot-owned ResourceSaver 内容。正式青川犬角色图延期；当前复制魔剑士动作纹理并使用不同蓝色调，不能把该临时表现视作人工美术验收通过。技能逻辑不依赖剑或琴装备。
