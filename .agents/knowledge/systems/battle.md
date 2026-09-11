---
type: Game System
resource: https://github.com/cty41/tactics
title: Battle System
description: Godot Pure Run 的棋盘、回合、技能、状态、AI 合法性、结算与表现投影主链。
tags: [gameplay, battle, turn-based, godot]
timestamp: "2026-09-11T11:08:38+08:00"
status: active
catalog_scope: battle-system
repo_paths:
  - .agents/docs/battle-line-of-sight-rules.md
  - .agents/docs/three-class-skill-design.md
  - .agents/docs/attribute-system-design.md
  - .agents/docs/buff-system-rules.md
  - .agents/docs/battle-facing-rules.md
  - .agents/docs/battle-initiative-rules.md
  - .agents/docs/poet-class-design.md
  - .agents/docs/isometric-grid-anchor-contract.md
  - .agents/docs/maw-bat-enemy-slice-design.md
  - src/Tactics.Core/Battle
  - src/Tactics.Core/Board
  - src/Tactics.Core/Pathfinding
  - src/Tactics.Core/Skills
  - src/Tactics.Application/Battle
  - src/Tactics.Core.Tests/BoardAndRulesTests.cs
  - src/Tactics.Application.Tests/PlayableBattleSessionServiceTests.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs
  - godot/tests/CoreGoldenVectorGodotTests.cs
verified_revision: 04c75ec4
source_fingerprint: sha256:b0cf670d2f9c81cc3b7d9f8dafb55fc7ea62386cbcc8a1844c5d00846d2d40bb
---

# Current State

Gameplay runner 的 Battle/Adventure 生产指针坐标统一转换到 Canvas 全局坐标后交给 Viewport；测试仍通过真实 `_gui_input`、PointerPressed 与状态变化证明提交，不直接调用玩法动作。

当前产品战斗主线分为三层：`Tactics.Core` 持有不可变战斗状态、命令、事件、技能解释、回合、路径和视线；
`Tactics.Application` 组合遭遇、玩家意图、AI 候选和 UI Snapshot；Godot Adapter 只负责输入、Resource 映射、
棋盘绘制和 committed event 表现，不重新裁决玩法结果。

跨引擎沿用的属性、状态、朝向与等距投影规则已整理为带稳定 Contract ID 的 Godot 权威文档。新角色或机制必须显式引用相关合同；旧 Unity 类名和编辑器结构不再作为规则来源。

六维统一战斗投影已支持状态携带有符号临时属性修正：有效属性从 BaseAttributes 与全部活动修正重算，敏捷同步派生命中和先攻，状态移除后恢复。回合顺序由 Core `InitiativeRoundState` 分区维护；新回合完整排序，轮中只重排 remaining，行动召唤可按新 InstanceId 插入，Decoy 不入队。持续治疗冻结施加时总量并按前高后低的未来 TurnStart tick 分配。

固定战场使用 10×10、零基坐标。单位实例身份使用 `UnitInstanceId`，不能用内容 `ContentId` 代替。合法性预览、
AI 和真实 Transition 必须复用 Core 规则；表现 cue、Tween、伤害数字和 Sprite 姿态不能修改战斗状态。

## Line of Sight

当前 LoS 合同为 `godot-los-shadow-cone-v1`，完整规则见 `.agents/docs/battle-line-of-sight-rules.md`。
阻挡格的开放内部从观察者方向向后形成遮挡锥：目标中心射线进入格内才阻挡，仅擦过格边或格角时放行。
中间存活单位和阻挡地形参与判断；施法者、最终目标、尸体与落矛不作为普通阻挡物。Bone Spear 保留首敌
截获语义。`LineOfSightResult` 为 Godot 悬停详情提供最近阻挡格、类型和单位身份。

最终 Unity 的 diagonal supercover 仍保存在 FrozenOracle、Golden 和 `oracle-matrix.json` 中，作为退役产品的
历史事实；`contract-decisions.json` 显式记录它已被当前 Godot 合同替代，不能再约束当前 Core 行为。

## Battle Runtime

Adventure 事件战的随机状态会从 checkpoint revision 中扣除 Adventure 专属 revision，确保领队移动、路线点击和场景切换不会改变相同 run seed 的战斗序列。护送 NPC 的生命值读取正式 UnitDefinition 派生值，不再使用硬编码 12 HP；其存活、团灭与事件失败仍由现有 Protected NPC 胜负规则裁决。

战斗命令失败时保持源状态并发出稳定 rejection；成功状态在表现事件消费前已经完整。技能次数、Mana、移动、
状态触发、召唤上限、尸体消费、长矛和战后结果均由 Core/Application 事务维护。Godot 只消费 Snapshot 和事件，
缺失表现资源或取消表现不得改变命中、伤害、状态与终局。

Pure Run 三职业、敌人、召唤物和固定遭遇均从 Godot-owned Catalog/Resource 组合；N6/E2 使用的
`battle-layout.pure-run.split-flank` 已由正式 `BattleLayoutResource` 提供，不再由运行时硬编码补建。内容
ownership 与人工验收分开记录；自动测试通过不能把视觉或操作验收直接标为 passed。

棋盘地形现区分 Ground 与 ShallowWater，单位移动类型为 Land、Air、Swim。Land 进入浅水消耗 2 点，
Air/Swim 消耗 1 点；路径范围按累计点数裁决，但冲刺等按路径格数计数的机制继续读取实际经过格数。
Air 可越过动态占位与 flyover 障碍但不能停在其上；absolute 障碍不可穿越。移动、传送和召唤落点统一
使用 `CanStop` 语义。N2 的正式布局已加入浅水，并由棋盘 Snapshot 与 Godot 静态表现共同投影。

大嘴蝠是 N2 的 Air 敌人，咬击复用标准物理攻击链，并按最终实际 HP 伤害的 50% 向下取整吸血。
伤害、恢复、死亡结算保持稳定事件顺序；完整数值、AI 与表现合同见 `.agents/docs/maw-bat-enemy-slice-design.md`。

魔剑士 `Demonbound` 作为第四名开局候选但仍维持三人参战。Core 保存战斗局部 0–10 腐化、正念等级、冥想资格与附身控制状态；附身只切换控制者、不改变阵营，AI 使用敌友统一候选池（正式敌我+存活召唤物，排除自身与诱饵）按收益统一评分选目标，不再硬编码阵营优先级；附身形态施加一次六维+5 强化并把已学技能临时投影到分支最高等级。终局生成 `BattlePartyResult` 时会把强化后的 HP/MP 按比例还原到 checkpoint 上限，保证战斗局部投影不泄漏到 Run 或触发结算拒绝。友军致命伤进行一次确定性永久死亡判定（基线 25%，目标幸运 >5 每点 -2%，无下限钳 0），永久死亡者不进入后续战斗但保留 Summary 墓碑，队伍缺员继续；仅剩附身魔剑士且敌人全灭仍是玩家胜利。Godot 左上状态卡只绑定当前行动者，复用 Unit Resource 显示头像/名称/HP/MP，并由 nullable Corruption 投影可选连续特殊资源条；Hover/LOS 详情迁入鼠标旁输入穿透浮层。结构与语义已自动覆盖，视觉可读性仍待人工验收。

厄运魔刃使用相邻方向输入并按近到远命中前方两格；墙体和第一格单位都不截断半月斩。技能效果通过统一成长类型读取战斗有效属性，不再使用“高于中立值”的独立伤害加值。表现层把 Bane 编译为近战挥剑 Cue，并在半月斩抵达第一、第二格时依次插入受击和数字；规则提交仍早于表现，表现暂停或取消不改变结算。

固定种子数值循环已有 Core 规则层诊断代理：三种 Demonbound 队伍标签各跑相同 10 seed，复用正式 `AiDecisionService`、`AiTurnService` 与 `BattleTransitionService`，记录终局、腐化峰值、冥想、首次附身、友伤、Down、永久死亡和技能次数，并验证同 seed 重放一致。其无尸体诊断夹具和简化队友策略只用于证明采样管线及发现规则问题；未接入生产 Run 路线、Resource 数值和完整职业策略前，不得视为完整平衡证据或人工体验替代。

诗人作为第五名候选、仍只选三人出战，基础六维 `6/5/5/4/6/4` 且力量为主属性。剑仙、诗仙、酒仙共 15 个技能等级已通过 Core 执行器、Application snapshot/AI 和 Godot typed Resource 接入；后撤分身读取 Core Facing，分身不参与行动队列并以直接命中段作为吸收层。正式青川犬美术与诗人装备均未实现：当前仅使用差异化临时纹理，装备只有未来设计文档。

# Relationships

- 技能数据和执行图见 [Skill Graph](skill-graph.md)。
- 敌方候选与模式见 [Monster AI](monster-ai.md)。
- 战斗外状态和结算返回见 [Roguelike Run](roguelike-run.md)。
- Frozen Unity 到 Godot 的历史证据边界见 [Godot migration](../plans/godot-migration.md)。

# Verification Guidance

先运行 Core/Application 定向测试，再运行 `Tools/godot/Verify-GodotProject.ps1`。涉及输入、目标高亮、悬停信息
或实际战斗体验的改动，在自动门禁后仍需更新 `.agents/docs/manual-acceptance.md` 并由用户人工验收。

# Citations

- [Mewgenics Range and Area](https://mewgenics.wiki.gg/wiki/Range_and_Area) — 遮挡锥方向的外部机制参考；逐格边界由本项目合同定义。
