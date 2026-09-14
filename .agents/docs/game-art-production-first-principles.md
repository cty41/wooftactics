---
title: 游戏美术生产第一性原则
status: draft
verified_revision: c9ff3a710fd9
---

# 游戏美术生产第一性原则

本文定义下一代生产系统在选择生成模型、图片表示、状态 schema 或独立仓实现前必须服务的对象和成功标准。它不规定具体 AI、动画或修图路线。当前实现事实见[当前生图与游戏美术生产策略地图](generative-art-current-strategy-map.md)，复杂度与漂移证据见[生图状态机复杂性审计](generative-art-state-machine-complexity-audit.md)。

## 1. 根命题

**游戏美术不是图片库存，而是运行中的玩家信息与情绪体验系统。**

一项美术产出只有同时满足以下条件才具有完整游戏资产价值：

1. 对应真实 Gameplay Contract 和玩家问题；
2. 在实际摄像机、尺寸、背景、遮挡和时间窗口中可读；
3. 与同一角色、世界及相邻表现通道连续；
4. 能被运行时正确触发、退出、中断和恢复；
5. 能以可接受成本稳定生产、修改、验证和发布；
6. 来源、人工决定、rights、运行时绑定和 QA 可追溯。

所以“得到漂亮 PNG”“技术报告通过”“孤立看图满意”都不是完整 Definition of Done。

## 2. 第一性问题链

在不固化 schema 的前提下，正确顺序是：

```text
Gameplay Contract
→ Player Question / Art Need
→ Information Ownership
→ Character State Coverage
→ Time Contract
→ Runtime Context Fixture
→ Cost Envelope
→ Acceptance Evidence
→ Release Binding
```

手绘、外包、程序化、3D 转 2D、生成式工具或混合路线只能在这条问题链之后选择。

## 3. 游戏美术先回答玩家问题

每个视觉状态先说明它帮助玩家回答什么：

- **身份：** 这是谁，属于什么物种、职业和个体？
- **状态：** 它在待机、蓄力、攻击、施法、受击、死亡还是被控制？
- **方向：** 面向哪里，行为将影响哪里？
- **意图与时点：** 危险何时发生，命中是否结算？
- **关系：** 它与武器、目标、Tile、队友、敌人和交互物是什么关系？
- **结果：** 行为成功、失败、被格挡、治疗还是产生状态变化？
- **情绪与世界观：** 玩家感受到什么性格、力量和叙事？

战棋中的即时身份、方向、危险和交互信息通常高于服装纹样等近看奖励；项目美术圣经已有的信息层级继续成立。

## 4. 最小设计单位是 Visual Moment

这里使用概念词，不提前定义为 schema：

> **Visual Moment**：由一个游戏状态或事件触发，在明确时间窗口内，通过多个表现通道向玩家传递指定信息的完整表现时刻。

一个 Visual Moment 可能共同包含：

```text
角色 Sprite / 骨骼姿态
+ Transform / Tween
+ 武器、投射物与 VFX
+ Tile、范围与目标提示
+ UI、数字、音效与镜头反馈
+ Begin、Release、Impact、Recovery 与中断时序
```

不能默认让单张动作 Sprite 承担全部信息。生产前先分配信息职责，再决定必须制作哪些资产。

## 5. 角色是一套视觉状态系统

角色可概念化为：

```text
Character Identity
× Gameplay State / Action
× Facing / View
× Equipment State
× Temporary Phase / Condition
× Runtime Context
```

### 跨状态稳定项

- 物种与核心拓扑；
- 脸、耳、主色和关键身份标记；
- 职业与个体符号；
- 不应随动作消失的装备或随身物；
- 战场相对体量和视觉权重；
- 项目轮廓、材质和细节语言。

### 动作允许或必须改变的内容

- 力线、重心、压缩、伸展和剪影张力；
- 手脚位置、遮挡和前后层；
- 装备持有、出鞘、脱手和旋转状态；
- 表情、视线和局部形变；
- 与 Tween/VFX 组合后的屏幕占用。

“保持身份”不等于逐像素冻结身体；“动作夸张”也不授权角色重设计。两者边界必须由完整状态集定义，而不是每次 prompt 临时猜测。

### 下游合同不能创造上游视觉事实

概念上的事实优先级为：

```text
正式角色/世界定义 + 获批母图事实
→ Gameplay State 与已授权变化
→ Brief / Contract 的任务投影
→ Production Strategy / Prompt
→ Candidate Feedback
```

Brief、Contract、Prompt、Feedback 和 Reviewer 只能投影或补充任务约束，不能把源中不存在的服装、道具或形态写成“必须恢复”的身份事实。Hash 与不可变 receipt 证明记录未被篡改，不证明内容正确。

## 6. 动作美术是时间设计

即使运行时只切一张静态图，也必须知道它位于哪一段：

```text
Idle → Anticipation / Telegraph → Action Peak
→ Release / Contact / Hit → Follow-through → Recovery
→ Idle 或新的 Gameplay State
```

每个动作至少回答：

- 玩家最晚何时必须读懂意图？
- 静态图代表前摇、峰值、命中还是后摇？
- Release/Impact 与姿态切换的关系是什么？
- 中断、死亡或装备状态变化时如何恢复？
- 哪些信息由轮廓表达，哪些交给运动、VFX、UI 或音效？

动作设计先确定力线、重心、压缩/伸展、支撑脚、反向惯性和负形，再由 Technical Art 定义画布安全区、握点、武器端点和检测窗口。只有刀轴与端点区域不能证明关键姿态成立。

Pure Run 当前 Godot 由 `PresentationCueKind`、`GodotUnitActionPose`、`GodotBattlePresentationPlayer` 和 `StandardUnitPresentationResource` 直接编排姿态、Tween、效果与恢复；离线生产尚未围绕这套真实 choreography 建立完整 Visual Moment。旧 `UnitPoseFamily/ReleaseTime/PoseRestoreTime` 仅属于 FrozenOracle/退役 Unity。

### 6.1 低成本 Pose Proof 先于生成

当冻结时刻、力线、重心、接触或负形尚未被人确认时，先用最终方向和大致占位绘制少量纯橙单色火柴人草图，在真实战场尺度选择姿态，再投入身份化生成。草图只证明动作，不携带身份、配色、装备造型或检测框；精确 Composition 在姿态通过后才补充。这样可以在最低成本层否决错误动作，避免让生成模型同时承担动作设计、身份恢复、装备复现和几何校准而来回振荡。

最小人工决策链是 Visual Moment → Pose Proof → 来源职责 → 单张候选 → 完整上下文 → promotion/runtime 分离批准。未选的 pose option、临时 review board 与 prompt 修订不进入正式状态机；状态机从有成本且必须追责的 generation invocation、候选血缘、确定性处理、rights 和批准开始。

## 7. 运行时上下文高于孤立图

至少检查四类上下文：

1. **母版近看：** 图像完整性和身份细节；
2. **正常战场尺寸：** 第一眼身份、朝向、动作和装备；
3. **拥挤、低对比、相近色背景：** 轮廓、遮挡和注意力竞争；
4. **真实动态时序：** 切换、Release、命中、恢复、VFX 与 UI 组合。

当前 `256×256`、`y=236`、128 预览与 `64×32` Tile panel 是 Pure Run 项目适配规则，不属于通用独立仓规范。其中 `64×32` 仍被现行管线用于新生产，但当前 Godot 战场采用 `96×48` 投影并将 Actor 缩放为 `.34`；其运行时有效性需要重新验证。离线 panel 不能替代真实 Game View。

## 8. AI 是生产能力，不是设计权威

AI 可以根据已确认设计产生候选、在明确边界内探索、为确定性或人工流程提供中间材料，并在能力不适合任务时被替换。

AI 不应决定游戏需要什么动作、玩家必须读出什么、哪个风格获批、何时重新许可或接入运行时，以及失败策略是否值得继续投资。多参考职责声明只是生产假设，不能证明模型内部真的隔离身份、姿势和装备。

## 9. Production Strategy 是可证伪假设

未来每种生产策略至少回答：

- **目标：** 完成哪个 Visual Moment 或状态覆盖缺口？
- **假设：** 为什么能保持身份并产生所需变化？
- **输入职责：** 每个来源允许贡献和禁止迁移什么？
- **能力风险：** 身份、姿态、装备、方向、连通或风格中的最大不确定性是什么？
- **成本：** 模型调用、作者时间、标注、Review、Godot 接入、QA、返工、存储与许可成本是什么？
- **停止信号：** 什么证据说明应换策略而非继续 retry？
- **后备路线：** 假设失败后回到哪个决策层？

策略成功不只是选出一张图，还要证明它对目标状态集足够稳定，或明确它只是一次性例外。比较指标应是“玩家信息与体验收益 / 全生命周期成本”，不能压缩成自动审美总分。

## 10. 验收结论必须分层

1. **技术可审查：** 文件、画布、Alpha、蒙版、SHA、annotations 正确。
2. **单个 Visual Moment：** 在目标尺寸和时点可读，审美符合项目方向。
3. **状态集一致：** 与 Idle、相邻动作、方向、装备和死亡共同观看仍是同一角色。
4. **游戏上下文：** 与地图、Tween、VFX、UI 和战斗节奏组合后成立。
5. **治理与发布：** 人工决定、rights、promotion、runtime authorization 和版本血缘有效。
6. **运行时人工 QA：** 实际可玩行为和体验获得人工 verdict。

这些结论可处于不同状态，不能由一个 `approved` 或 `promoted` 覆盖。

## 11. 产品级 Definition of Done

一个角色视觉切片完成至少意味着：

- 游戏状态和玩家信息需求明确；
- Visual Moment 与表现通道分工明确；
- 状态覆盖和允许 fallback 明确；
- 正式资产在目标尺寸、集合对比和真实上下文中通过人工判断；
- 运行时触发、方向、时序、中断和恢复正确；
- 许可、来源、版本、授权和批准可追溯；
- 未完成项诚实标为 fallback、placeholder 或 `manual_qa_pending`。

仅有若干 promoted PNG 不等于角色视觉切片完成。

## 12. 已确认原则

2026-09-11，cty41 确认以下原则；本文仍保持 draft，待后续领域对象与状态关系设计完成后再决定是否升级为正式权威：

1. 以 Visual Moment 与 Character Visual State Set 为概念中心；
2. 上游正式事实高于 Brief、Prompt 与 Feedback；
3. 表现信息允许由 Sprite、Tween、VFX、UI 和音效分担；
4. 动作设计先于 Technical Art 检测框；
5. 资产语义必须绑定真实时点；
6. 当前 Godot Game View 高于历史/离线 panel；
7. fallback、placeholder 和缺口必须是显式产品状态；
8. 技术、视觉、状态集、运行时、rights、授权与发布结论分离；
9. 策略可证伪，但 AI/Agent 无人工投资终止权；
10. 当前不选择具体生产路线或 vNext schema。

独立仓只应拥有通用协议、引擎与合成测试；Pure Run 的视觉规则、资产、历史数据和上述尺度适配留在项目及其薄适配层。