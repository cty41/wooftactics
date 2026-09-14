---
title: 当前生图与游戏美术生产策略地图
status: draft
verified_revision: c9ff3a710fd9
---

# 当前生图与游戏美术生产策略地图

本文只描述当前仓库已有策略、实体、状态与运行时边界，不为下一代系统选择 schema 或 AI 路线。目标原则见[游戏美术生产第一性原则](game-art-production-first-principles.md)，复杂度与实现差异见[生图状态机复杂性审计](generative-art-state-machine-complexity-audit.md)。

## 1. 当前端到端链

```text
项目美术圣经
  → active ArtDirectionManifest
  → Project / Material / Family Profile
  → Asset Brief / Scene Brief
  → Contract
  → Job / Attempt / 可选 Series
  → 外部 ImageGen
  → ingest / prepare / mask / calibrate / validate
  → Acceptance / ArtDirection / Model / Equipment Review
  → cty41 Approval
  → Promotion + Public Provenance
  → 已存在 Runtime Copy 的字节/rights 登记
  → 外部 Godot Resource 接入与测试
  → Manual QA
```

ImageGen 位于状态机外；本地记录、像素转换、报告和证据绑定应确定且可重放。Scene Brief 目前只有模板，现有 24 份 Brief 都是 Asset Brief，因此场景/运行时上下文尚未实际进入该链。

## 2. 当前职责地图

| 层次 | 主要实体 | 当前主要职责 | 当前不能证明 |
|---|---|---|---|
| 游戏/视觉需求 | 美术圣经、Asset/Scene Brief | 第一眼信息、项目风格、上下文、禁止项 | 完整 gameplay state coverage 和真实时序 |
| 视觉定义 | Manifest、Project/Material/Family Profile、AnchorVerdict | 项目、材质、家族和窄职责锚点 | 所有声明规则已进入 prompt 或运行时 |
| 离线素材合同 | Contract、Composition、Pose Guide | kind/direction/pose、画布、核心、握点、刀轴、输出路径 | Visual Moment 与通道职责分配 |
| 生产 | Job、Attempt、Series、Prompt、Invocation/Delivery | 固定输入职责、SHA、模型调用和版本预算 | 模型内部真的隔离参考职责 |
| 技术处理 | prepared、mask、calibrated、Report | Alpha、色幕、体量、拓扑、基线、annotations | 身份、动作、审美和战场可读性 |
| Review | Feedback、Acceptance、AD/Equipment Verdict、Model Reviewer | 结构化缺陷、人工审美、用例证据、模型建议 | 单一总分或自动人工批准 |
| 审批发布 | Approval、Promotion、Public Provenance | 人工批准、正式输出、分发 rights | Runtime 授权和可玩 QA |
| Runtime | Godot Resource、Player、测试、Manual QA | 纹理绑定、方向、Tween、VFX、fallback | 未执行的人工体验判断 |
| 历史恢复 | Legacy、Migration、Reviewed Import/Recontract、Transaction | 保留历史并避免伪造调用 | 历史数据自动成为新正向来源 |

## 3. 当前主要生产策略

### 3.1 完整 Sprite 生成

以获批身份/方向图为母图，辅以装备或动作参考，生成完整角色候选。适合需要较大姿态变化的任务，但身份、拓扑、装备和动作可能同时漂移。

### 3.2 局部组件与确定性 Assembly

将 body、equipment、near/far paws、near/far feet 按明确层序组合。诗人装备版 Idle DR 证明该策略适合冻结身份后添加稳定装备关系，但它不能自动解决大幅动作设计。

### 3.3 原生方向重投影

DR/UL 是两个原生视图，East/West 由运行时镜像补齐。诗人 Idle UL 的有效经验是接受真实遮挡、逐项冻结层序，而不是强迫背向图展示整件装备。

### 3.4 技术 remediation

同一 raw 的去幕、蒙版、校准、透明 RGB 清理等技术子 Attempt 不消耗新生成轮。它只能修复可重放技术问题，不得伪装成新视觉候选。

### 3.5 Reviewed Import / Recontract

对已有人工确认但缺少 Invocation 的图使用受限 reviewed import；对已有 selected Invocation/Delivery 的图用 recontract 切换诚实合同。两者都禁止倒填不存在的模型调用。

### 3.6 自动 Reviewer

确定性门禁先判断像素事实；多模态 Reviewer 仅在有资格的原子规则上建议 retry 或转人工，不能批准。`medium → high → xhigh` 是审核预算，不代表生产必须固定生成三张图。

## 4. 当前状态不是一条线

Attempt 枚举包含 ready、ingested、prepared、annotated、calibrated、review_pending、模型 Review 中间态、approved、rejected、promoted、technical_failed。实际数据中没有 `state=rejected` 的 Attempt。

真实分支应理解为：

```text
技术处理分支：ready → ... → technical_failed
审核分支：review_pending → approved → promoted
                         ↘ rejected（终点）
```

但这仍不足以表达全部事实：

- Job、Attempt、Feedback、Series pose 各有状态；
- Approval、Art Direction Verdict、Equipment Verdict 与 Public Provenance 是独立记录；
- Runtime Copy 当前没有独立 authorization receipt；
- Manual QA 又是项目级人工账本。

当前 219 个 Job 的 `state` 全部仍为 `ready`，包括其 Attempt 已 promoted 或 technical_failed 的 Job；Job state 更像创建投影，不是生产聚合结论。

## 5. 当前 Godot 运行时事实

当前 Godot 由 `BattlePresentationCue.Kind` 选择 `GodotUnitActionPose.Melee/Ranged/Cast/Hit`；`GodotBattlePresentationPlayer` 直接编排姿态切换、Tween、技能 FX 与恢复，时长来自 `StandardUnitPresentationResource`。`GodotUnitActor` 根据 DR/UL 与镜像解析动作 Texture，缺图时回退当前方向 Idle。

`UnitDefinitionResource.ValidateVisualContract`：

- 要求 Idle DR/UL、Shadow；
- 已填写动作时要求方向成对；
- 不根据角色技能强制 Melee/Ranged/Cast/Hit 覆盖；
- 不判断 Idle fallback 是否是有意产品决定；
- 不验证纹理必须为 256×256、离线 AABB 或 `y=236` baseline。

当前棋盘投影为 `96×48`，战斗 Actor 实例化时缩放为 `.34`。现行管线仍产生 128 预览和 `64×32` Tile panel；这是仍在执行但尚未与当前 Game View 重新对齐的 Pure Run 适配规则。

Melee 还有直接时序错位：Brief 将目标图定义为“下劈峰值”，Player 却在 windup 前切入 Melee Texture，让同一图覆盖 windup、lunge 与 impact hold，之后才 recover。离线 peak 语义没有独立运行时显示时点。

旧[单帧动作姿态设计](pure-run-single-frame-action-pose-design.md)中的 `UnitPoseFamily/ReleaseTime/PoseRestoreTime` 属于退役 Unity/FrozenOracle，不是当前运行时权威。

## 6. 当前规模快照

以 `c9ff3a710fd9` 为已提交基线：

- 主 CLI 约 6,450 行，纯 Reviewer 核心约 780 行；
- parser 实际展开 81 个 CLI 子命令；源码有 75 处 `add_parser(...)`，循环额外展开；
- `Tools/artworks/pipeline` 约 4,830 个文件：3,303 JSON、1,525 PNG；
- 219 Job、380 Attempt、187 Feedback、86 Approval；
- Attempt：`technical_failed=115`、`prepared=82`、`review_pending=77`、`promoted=68`、`annotated=20`、`approved=13`、`ready=3`、`calibrated=2`；
- Feedback 有效结果：`retry=102`、`technical_failed=52`、`selected=21`、`exhausted=9`、`backup=3`；旧 schema 使用 verdict，新 schema 使用 disposition；
- 252 份技术报告为 `passed=131`、`failed=121`；
- `check --strict` 当前仍因 8 个已 promoted 装备 Attempt 及其正式 master 合计 16 条 `equipment_exact_chroma_residue` 失败。

这些数量不是成功率：同一 raw 可以有多个技术 Attempt，历史标签也不自动成为重新验证后的语义事实。

## 7. 诗人试点

### 7.1 Idle DR/UL

离线生产有效经验：

- 先锁身份与核心，再处理装备；
- 正式维护独立 DR/UL 源，不以 DR 镜像冒充 UL；
- 冻结已通过部分，一次只修一个变量；
- 允许确定性 Assembly 与诚实 recontract；
- 用人工 Acceptance/Verdict/Approval 和 SHA 冻结结果。

但产品切片未完成：`PureRunPoet.tres` 的动作 pair 全空，缺图回退 Idle，Death 是 Idle DR placeholder；`MQA-GODOT-POET-RUNTIME` 仍为 pending。Idle 只能称为离线身份/生产成功及自动接入已完成，不能称为完整运行时体验通过。

### 7.2 Melee 双参考

`job-a6df55a1b4f8ac96` 使用装备版 Idle 和独立出鞘唐刀，`series=null`。最后一条 Feedback 由 `pipeline-agent` / `authorType=agent` 写入 `exhausted`；它不是人类策略终止 receipt。用户后来暂停并归档美术工作是独立决定。

更重要的是，Melee Brief/Prompt 把帽、衣、葫芦写成身份不变量，但正式基础身份明确无 clothing/equipment，已批准装备版 Idle 也禁止 wineskin/gourd。因此该历史首先证明合同与批准来源发生漂移；“缺帽衣葫芦”不是有效模型缺陷。

仍有效的问题是四爪/无臂拓扑、唐刀形制和下劈动作张力。Pose Guide 主要规定胶囊框、握点与刀端，没有充分表达力线、重心、压缩/伸展、支撑脚和反向惯性。

### 7.3 Melee 三参考

`job-50532e1e9fe7c307` 额外加入 Demonbound Melee，仅有 a001；Attempt 为 `technical_failed`，报告问题是 `annotations_missing` 与 `core_row_disconnected`，没有 Feedback。它局部改善无臂握持拓扑，但参考动作轴与目标陡斜下劈并不同构。

准确结论是“首稿技术失败并暂停”，不能说三参考视觉策略已经耗尽或充分证伪。

## 8. Runtime Copy 的真实边界

`register-runtime-copy` 当前：

- 读取已存在 source 与 target；
- 验证 SHA 完全一致；
- 要求 source public provenance 为 approved 且有 rightsHolder/license；
- 将 target rightsHolder/license 设为 source 值；
- 将 target provenance 固定为 `project-owned-migrated-runtime-art`；
- 更新 `Tools/public-release/asset-provenance.json`。

它不复制文件、不记录 `authorizedBy/reason/decidedAt`、不生成 runtime authorization receipt、不绑定 Resource 字段、Cue、自动测试或 Manual QA。

## 9. 当前值得保留的基础

- Hash-bound 输入、候选、报告、Review 与决定；
- 不原地伪造历史记录；
- Agent/模型不能签人工 Approval；
- 技术 remediation 与新生成轮分离；
- Rights、Approval 与 Promotion 分离；
- 失败、rejected、superseded 资产不得成为正向输入这一政策；
- 确定性处理和恢复证据；
- `artwork_review.py` 的纯确定性 Reviewer 核心；
- 真实战场上下文高于放大母版这一原则。

## 10. 本轮不决定

- 不选择完整生成、组件、骨骼、人工清理或混合路线；
- 不定义 vNext schema、状态枚举或迁移格式；
- 不创建独立仓、不移动历史数据、不接入 Alfred；
- 不生成、重试、批准、晋升或接入任何新图片；
- 不把现有全部复杂度当成未来必须保留的协议。