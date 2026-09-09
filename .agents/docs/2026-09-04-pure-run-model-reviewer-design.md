---
title: Pure Run 订阅驱动多模态 Reviewer 设计
status: approved
verified_revision: null
---

# Pure Run 订阅驱动多模态 Reviewer 设计

## 1. 目标

在现有 Pure Run artwork schema v4 状态机之上增加生成后审核层，使身份、拓扑、方向、装备状态和遮挡等明确合同错误尽量在进入 `cty41` 人工审美审核前被发现并受控重试。

系统不要求 ImageGen 幂等，而是要求：同一合同下，不满足已确认硬规则的候选稳定地不能被自动视为可批准。多模态 Reviewer 永远不能批准资产；`cty41` 仍是唯一艺术终审者。

首个回归目标是诗人 Idle UL 已发生的问题：出现胳膊和腿、胶囊核心漂移、唐刀因过度展示而读成两截、实装环首角度过正，以及人工语义蒙版可能掩盖图像事实。

## 2. 非目标与边界

- 不修改 Godot 运行时资源或游戏代码。
- 不开发 DSH 核心插件。
- 不让 Python 管线读取、保存或转发 ChatGPT/Codex OAuth 凭据。
- 不把 ChatGPT Pro 订阅当作 OpenAI API credits；API 计费与 ChatGPT 订阅分离。
- 不引入数据库或常驻服务。
- 不让 Reviewer 给图片输出综合审美分数。
- 不把 rejected、retry、superseded 图重新作为 ImageGen 输入。
- 不回填历史 Attempt 的模型审核记录。

## 3. 模型与认证

第一阶段通过 DeepSeek Harness 中已配置且经预检确认为 ChatGPT/Codex OAuth 的路由启动全新子 Agent，目标模型为 `gpt-5.6-sol`。当前会话显示的模型名称本身不能证明计费来源；每次启用前必须确认 credential 类型。Python 状态机只创建和验证不可变审核包与结果，不直接调用模型。

当前运行必须记录实际 Provider、实际模型标识、推理等级、子会话 ID、审核 Prompt SHA、输入 Packet SHA 和原始结果 SHA。若无法证明实际路由符合 Reviewer Policy，候选进入 `human_review_required`，不得静默跳过。

未来可以增加单独计费的 Responses API Adapter，但必须复用相同 Packet/Result 协议；它不是第一阶段依赖。

## 4. 权限模型

自动判断分三层：

1. **确定性门禁**：路径、SHA、透明度、画布、蒙版颜色、baseline、引用职责和退休资源等可证明事实；可以硬拒绝。
2. **多模态 Reviewer**：读取候选和证据，只有对已获资格的明确合同硬规则才能驱动自动 retry；不能批准。
3. **人工终审**：风格、可爱程度、姿态自然度、合理方案间的偏好及所有模型不确定项；由 `cty41` 决定。

Reviewer 只允许输出：

- `retry`
- `escalate_to_human`
- `pass_to_human`

`pass_to_human` 仅表示未发现有权限处理的明确合同违反，不表示资产通过。

## 5. 三轮预算

每个 Job 最多三个自动候选轮次：

| Attempt | Reviewer 推理等级 | 失败后的行为 |
|---|---|---|
| `a001` | `medium` | 有资格的硬失败可自动生成 `a002` |
| `a002` | `high` | 有资格的硬失败可自动生成 `a003` |
| `a003` | `xhigh` | 不自动生成 `a004`，转人工 |

Attempt 的 `ordinal` 继续表示不可变记录顺序；另以显式 `generationRound` 表示真正消耗 ImageGen 的候选轮次。透明度、中心、色幕等可逆技术修复可以创建新 Attempt，但继承父级 `generationRound`，不消耗新的 ImageGen 轮次。身份、拓扑、方向、遮挡和装备连续性问题必须重新生成并递增 `generationRound`。自动链在 generationRound 3 熔断；第四个生图轮次及以后只可由 `cty41` 明确扩展预算。

## 6. 状态流

```text
ready
  -> generation_started
  -> ingested
  -> prepared / annotated / calibrated
  -> deterministic_validation
     -> technical remediation                    (可逆技术问题)
     -> automatic retry or human_review_required (确定性硬失败)
     -> model_review_packet_created               (确定性通过)
  -> model_review_pending
  -> model_reviewed
     -> automatic retry                           (已获资格硬规则，a001/a002)
     -> human_review_required                     (主动交人工或 a003 失败)
     -> review_pending                            (pass_to_human)
  -> cty41 Art Direction Review / Verdict
  -> approved / rejected
  -> promoted
```

生成 raw 默认处于隔离状态。审计界面可以查看，但必须标记 `QUARANTINED`；只有完成确定性校验和模型审核后才作为正常人工候选展示。

## 7. 评审知识四层结构

### 7.1 Project Review Policy

仅保存项目最高层原则，例如人工终审权、禁止综合审美分数、退休资产拒绝、引用职责隔离和自动审核权限边界。

### 7.2 Review Rule Registry

保存原子、版本化、可复用的评审规则。每条规则至少包含：

- `ruleId` 与版本；
- 适用 project/family/topology/direction/pose/equipment state；
- `hard`、`advisory` 或 `human-only` 权限；
- 是否可申请自动 retry 资格；
- 必需证据；
- 来源文档、Feedback、Approval/Verdict；
- 正反案例 ID；
- 被替代关系。

首批规则族至少覆盖：

- `CAPSULE-CORE-SHAPE`
- `CAPSULE-NO-LIMBS`
- `PAW-DIRECT-CONTACT`
- `REAR-FOOT-OCCLUSION`
- `NATIVE-DIRECTION-PROJECTION`
- `BACK-VIEW-NO-FACE`
- `BEHIND-CORE-OCCLUSION`
- `EQUIPMENT-STATE`
- `EQUIPMENT-PROJECTION`
- `EQUIPMENT-VISIBILITY-RESTRAINT`
- `EQUIPMENT-VISUAL-CONTINUITY`
- `REFERENCE-ROLE-ISOLATION`
- `RETIRED-ASSET-REJECTION`
- `SEMANTIC-MASK-HONESTY`

### 7.3 Review Case Registry

每个案例绑定真实正例或反例、路径和 SHA、来源 Attempt/Feedback/Verdict、`cty41` 结论、适用范围和 `reviewOnly` 标记。Negative 案例只供 Reviewer 查看，永远不得成为生成输入。

案例状态：

- `active`
- `shadow-only`
- `needs-splitting`
- `historical-only`
- `superseded`
- `invalid`

### 7.4 Compiled Review Policy

每个 Attempt 按固定优先级选择相关子集：

```text
项目全局
+ asset family
+ topology
+ direction/view
+ pose
+ equipment category/state
+ 当前 Brief Acceptance Cases
+ 当前 Job Feedback 链
```

优先级为当前 Job 人工反馈、同资产同姿态、同 family/topology、项目全局。冲突在编译阶段失败，不让 Reviewer 自行猜测。相同 Registry 版本、合同和候选必须产生相同 Compiled Policy ID/SHA。

## 8. 历史知识与案例适用性审计

OKF 是导航和摘要层，详细事实仍来自：

- `.agents/knowledge/operations/pure-run-artwork.md`
- `.agents/docs/pure-run-artwork-guidelines.md`
- `.agents/skills/pure-run-artwork-pipeline/references/review-casebook.md`
- `.agents/skills/pure-run-artwork-pipeline/examples/cases.json`
- `Tools/artworks/pipeline/feedback/*.json`
- Approval、ArtDirectionVerdict 与实际 PNG

当前约 150 条 Feedback 中，主要类别为 processing 36、pose_axis 30、identity 27、core_geometry 24、topology 19、equipment_state 17、equipment_scale 11、occlusion 10、chroma 6、gaze 2。所有记录建立只读索引，但不自动成为 active case。

每个案例必须通过 Case Fitness Audit：

1. 图片存在且 SHA、Attempt 和人工结论有效；
2. 正例已批准、反例已拒绝或被替代；
3. 问题足够原子；多缺陷整图应裁成绑定原图的证据或降为 historical-only；
4. 正反例的 family、方向、姿态、装备状态和显示尺寸具有可比较性；
5. 跨角色引用只承担明确职责；
6. 适用范围可机器读取；
7. 问题在对应 Review 尺寸可观察；
8. 未被后续知识替代；
9. 不含退休资产的活跃引用；
10. 明确禁止 Negative 用于生成。

现有三组代表案例继续接受复核：核心胶囊体、远近手/装备层级、飞行球核。飞行球核不得进入地面诗人 Packet；层级规则必须按方向和合同限定；核心案例需补充语义蒙版不得掩盖视觉轮廓。

## 9. Model Review Packet

不可变 Packet 至少包含：

- Packet、Attempt、Contract、Brief 和 Compiled Policy ID/SHA；
- review round、要求模型与推理等级；
- raw、prepared、256、128、64x32 Tilemap；
- 语义蒙版与蒙版叠加；
- 核心轮廓与母图轮廓叠加；
- 装备可见区域和方向证据；
- `POSITIVE_IDENTITY_ANCHOR`、`APPROVED_COMPONENT`、`APPROVED_TOPOLOGY_COMPARISON`；
- `NEGATIVE_REVIEW_ONLY`、`FORBIDDEN_GENERATION_INPUT`；
- Acceptance Cases；
- 当前 Feedback 和 frozen invariants；
- Reviewer 权限与允许输出。

Reviewer 必须直接检查候选视觉事实，不能信任蒙版声明。如果图中出现胳膊而蒙版将其标为 hand/core，应同时报告视觉拓扑违反和蒙版误分类。

## 10. Model Review Invocation 与 Result

Invocation 记录：

- Packet ID/SHA；
- Provider、模型、推理等级；
- Fresh child session ID；
- Reviewer Prompt ID/SHA；
- 开始与结束时间；
- 原始返回 SHA；
- Result ID；
- 拒绝、截断或 schema 修复情况。

Result 包含：

- decision；
- summary；
- strengths；
- defects；
- frozen invariants；
- 受控修正操作。

每个 defect 必须绑定当前 Acceptance Case、active ReviewRule、观察事实、预期事实、Packet 内证据角色和归一化图片区域。自动 retry 还要求该 rule 已获资格、严重度为 hard、判断确定、修正不取消上层合同且不改变冻结项。

Result 不得包含 `approved`、人工 passed、综合审美分数、Packet 外证据、错误 SHA 或使用 rejected 图的要求。JSON 格式错误只允许同一 Reviewer 做一次纯格式修复；再次失败即交人工。

## 11. 受控修正操作

Reviewer 自由文本不直接追加到 ImageGen Prompt。只接受：

- `remove`
- `enforce`
- `adjust`
- `preserve`
- `hide`
- `restore_from_anchor`

状态机将操作确定性编译成下一 Attempt 的 `promptDelta`。默认禁止自由重写 Brief、身份母图、参考职责和冻结项。

本次诗人失败应表达为：删除所有肢段；恢复批准胶囊核心；隐藏下方鞘身/鞘尾；肩后只保留环首、短柄和少量连续鞘口；实装环首比独立 v03 略偏侧；保持原生背向 UL、无正脸和完全入鞘状态。

## 12. 按规则渐进授权

每条规则独立具有：

```text
unqualified -> shadow -> auto-retry-qualified -> suspended/superseded
```

“只记录、不执行模式”是 Reviewer 获得自动权限前的资格阶段，不是每个正式 Job 固定增加的首轮。它使用与拟授权正式配置相同的一次 Reviewer 调用并只记录结果，不产生 retry；规则取得资格后，正式 Job 才按 `a001/medium → a002/high → a003/xhigh` 最多执行三轮自动审核。

资格不采用固定案例数量或总体准确率。每条规则依据：

- 必要场景覆盖矩阵；
- 关键必过案例；
- 错误反例必须判 retry；
- 正确批准图不得被自动 retry；
- 边界案例必须主动交人工；
- 同 Packet 判断稳定；
- `cty41` 按规则签发 Reviewer Qualification。

模型自报的单一置信度不能独立授权。任何未获资格、证据不足、案例冲突或主观审美问题都进入 `human_review_required`。

资格绑定模型、Reviewer Prompt、Policy 和案例集合版本。模型或 Prompt 变化后相关规则退回 shadow。人工发现漏检或误杀时，对应规则立即 suspended，不影响其他已获资格规则。

## 13. 受控经验积累

这里的“经验积累”不更新 GPT‑5.6 Sol、GPT‑Image‑2 或其他基座模型的参数，也不声称进行微调、强化学习或通用模型训练。它只把经过人工确认的反馈提炼为项目内版本化规则与案例，在后续审核中按适用范围检索使用。

每次 `cty41` 人工反馈先写入当前 Job 的不可变 Feedback。随后可由全新的 Policy Curator 子 Agent 生成 `ReviewLessonCandidate`，但它不能修改 active policy。

候选经验类型：

- 加入已有规则的新案例；
- 收窄已有规则适用范围；
- 提议带 scope 的例外；
- 提议新规则草案；
- 标记为 asset-specific/human-only；
- 拒绝学习。

默认优先顺序：增加案例、收窄作用域、创建新规则。只有反馈无法匹配、与旧规则矛盾，或用户明确表示“以后都应这样”时才完整提炼新规则。所有建议默认折叠显示。

任何案例或规则晋升必须由 `cty41` 明确确认。新规则从 draft/shadow 开始，不能因首个失败图立即获得自动 retry 权限。规则修改采用新版本和 `supersedes`，并使依赖旧版本的资格失效。

## 14. 人工审核体验

正常只向用户展示 `pass_to_human` 或 `human_review_required` 的候选。自动否决历史默认折叠，显示缩略图、命中规则和修正内容；用户可展开完整证据。

用户可以登记：同意、模型漏检、模型误杀、规则不适用或证据不足。漏检加入回归集并撤销对应规则资格；误杀保留后续候选但撤销对应规则资格。所有历史图和判断不可变，不删除、不覆盖。

## 15. CLI 与数据接口

建议新增：

```text
index-review-history
create-review-rule
promote-review-case
retire-review-case
compile-review-policy
create-model-review-packet
begin-model-review
record-model-review
apply-model-review
record-reviewer-qualification
suspend-reviewer-rule
record-model-review-audit
create-review-lesson-candidate
promote-review-lesson
```

`begin-model-review` 只登记调用意图；当前 DSH Agent 根据 Packet 启动全新子 Agent并将结果交给 `record-model-review`。Python 不接触 OAuth。

历史 Attempt 不补记录；schema v1-v4 继续读取；只有启用新 Policy 的新 Job 使用新状态。旧 `advisoryReviews` 保持原语义，不自动升级权限。

## 16. 测试与验收

普通离线测试使用 Fake Reviewer，覆盖：

- Policy/Case 索引与确定性选择；
- Case Fitness Audit 状态；
- Packet 和 Result schema；
- 路径/SHA/角色职责绑定；
- Negative 生成输入隔离；
- 资格阶段不计入正式 Job 轮次，以及 medium/high/xhigh 三轮映射；
- 技术修复不占生图轮次；
- 不合格规则不能自动 retry；
- a003 不自动生成 a004；
- 主动交人工；
- 规则撤权；
- 模型/Prompt 漂移使资格失效；
- 非法 approved/humanDecision/综合分数；
- 模型不可用和 JSON 修复失败；
- 经验建议不能自行晋升规则。

真实模型资格验证由 DSH 显式运行，不进入普通 CI，不自动消耗订阅额度。上线先使用只记录、不执行模式；`cty41` 按规则签发资格后才开放自动 retry。

验收要求不是固定案例总数，而是每条拟授权规则的场景覆盖完整、关键必过案例全部通过、批准正例零自动误杀、边界案例主动交人工。任何未闭合风险保持 shadow。

### Prompt-only 对照验证

Reviewer 闭环必须与“仅使用同一 Brief、Contract、批准锚点和编译 Prompt，然后直接交人工审核”的 Prompt-only 基线比较。对照验证不计算综合审美分数，只记录：

- 已知硬错误进入人工审核的次数；
- 同类硬错误重复发生的次数；
- Reviewer 漏检和误杀次数；
- 每张候选需要 `cty41` 重复指出硬错误的次数；
- 到达人工可审核候选所消耗的 ImageGen 轮次；
- Reviewer 主动交人工的规则与原因。

比较必须尽量保持资产类型、合同、输入锚点和生图预算一致，并分别报告每条规则和场景分组，不能用单一总体数掩盖关键错误。Reviewer 闭环若没有实证降低重复硬错误，或者误杀造成更多无效生图，就不得授予或扩大相关规则的自动 retry 权限。

## 17. 实施顺序

1. 审计当前 OKF、案例库和全部历史 Feedback，建立只读索引及 Case Fitness Audit。
2. 建立 Project Policy、Rule Registry、Case Registry 和 Compiled Policy。
3. 实现 Packet、Invocation、Result、Audit、Qualification 和 LessonCandidate schema/CLI。
4. 扩展 Attempt 状态和三轮 retry 规则，保持历史兼容。
5. 增加 Fake Reviewer 单元测试和 strict gate。
6. 建立 Prompt-only 基线，并与 Reviewer 闭环执行同条件对照验证。
7. 使用当前历史案例运行 GPT-5.6 Sol 只记录、不执行模式。
8. 由 `cty41` 按规则签发首批资格。
9. 重新建立诗人 Idle UL 合同，移除必须显示下方鞘尾的错误构图要求。
10. 使用新流程重新生成并人工审核诗人 Idle UL。
11. 只在最终人工批准后离线 promote；不修改 Godot 运行时资源。

## 18. 当前实现与严格收尾

Reviewer MVP 已落地 schema v4 的历史 Feedback/Case Fitness、Rule/Case/CompiledPolicy、Packet/Invocation/Result、Audit、按 rule+model+effort+prompt+policy+case-set 资格、撤权、受控经验、三轮 `medium → high → xhigh` 熔断、Prompt-only 对照和显式 Shadow。旧的弱资格通过 `supersede-reviewer-qualification` 保留历史并指向包含 Prompt-only 与负/正/边界审计的新资格；创建新资格时，审计 Result 的 Reviewer Prompt SHA 必须与待资格化 Prompt 完全一致。

Shadow Packet 不消费生成预算、不改 Attempt 状态，历史案例图只能是唯一的 `HISTORICAL_CASE_ARTIFACT` 且必须与 Case artifact 的 path/SHA 一致。早期、缺少 `reviewContext`/`historicalProvenance` 和显式 review-only flags 的不可变 Shadow Packet仅按严格的 legacy 形状只读兼容，不能成为 generation input；当前 Packet 必须使用完整上下文字段。

资格运行时按 rule 保留全部 effort 记录，不能让 xhigh 覆盖 medium/high；每次自动 retry 前都重新验证资格 stable ID、Rule/Policy/Prompt SHA、Prompt-only comparison 和至少三份 `cty41 confirmed` Audit。Audit 新记录使用明确 `confirmed|rejected` verdict；rejected 证据不能授予资格。自动 retry 同时受 `generationRound <= 3` 和 Attempt ordinal 限制，绝不创建 `a004` 或更高编号。

Strict 只从逐类型身份、backlink 和 artifact SHA 验证通过的 record 登记资产。Supporting artifact 必须由 `cty41` 绑定当前 Approval 与合同既有 rights；默认继承 `project-owned`，CC-BY 仍只能走独立 relicense receipt。遗留 transaction 的恢复结果写入不可变 `transaction-resolution`，部分或冲突证据 hard-fail；失败的旧 provenance sync receipt 通过不可变 invalidation 保留而不再代表成功。

已人工 selected 的生成图在合同语义修正但像素保持不变时，可以用 `recontract-reviewed-attempt` 绑定原 Attempt、Invocation/Delivery、新合同和确定性 processing；该 source mode 不执行 ImageGen，也不得伪造新 invocation。武器尖端只有在 Composition 显式声明 `weapon.tipMayBeOccluded: true` 时才允许真实 annotation 为 `null`。

五红土松诗人环首唐刀 Idle UL 最终离线文件为 `Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_ul_v07.png`，SHA-256 `519e288ff7f4ea2cb35de9393b4163a13c1fbfdca3b8fb5f83eff81733eec512`；Art Direction Verdict `art-direction-verdict-b196da86a5b54f86`，Approval `approval-a71f2eecb8e0ec5e`。`cty41` 批准当前前景尾巴、身体后的画面右侧手爪与脚爪，以及几乎完全藏在身体后的入鞘唐刀表现。该事实仅代表离线 promote，未授权或修改 Godot 运行时。

## 19. 外部参考

- [OpenAI：ChatGPT 与 API 分开计费](https://help.openai.com/en/articles/9039756)
- [OpenAI GPT-5.6 Sol 模型能力](https://developers.openai.com/api/docs/models/gpt-5.6-sol)
- [OpenAI Evals：基于评审规则与人类专家判断的 grader](https://evals.openai.com/gdpval/grading)
- [Anthropic：Evaluating AI systems](https://www.anthropic.com/news/evaluating-ai-systems)
- [Anthropic：Demystifying evals for AI agents](https://www.anthropic.com/engineering/demystifying-evals-for-ai-agents)
- [ICLR 2025：Cascaded Selective Evaluation](https://openreview.net/pdf?id=UHPnqSTBPO)
