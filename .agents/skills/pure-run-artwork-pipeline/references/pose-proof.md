# 纯橙火柴人 Pose Proof

Pose Proof 是正式 ImageGen 之前的低成本动作设计门禁。它只确认冻结时刻、力线、重心、接触、负形和粗略占位，不承担角色身份、装备造型、材质、画风或精确技术几何。

## 标准流程

1. 先确认真实 gameplay consumer、动作阶段，以及 Sprite/Tween/VFX 各自负责什么。
2. 在临时 JSON 中描述 1–4 个方向明确的姿态方案。
3. 运行 `pose_proof.py render-options`，在看板及 128 预览中比较方案。
4. 每次只要求 `cty41` 选择一个 option；若全部不合格，丢弃临时 Draft 并重新设计，不生成伪选择记录。
5. 选择后运行 `select-option`，只保存选中方案完整几何及未选方案的简短否决理由。
6. 将 Action Card 绑定到 schema v3 Composition；再把抽象姿态翻译为 baseline、握点、端点、遮挡层和安全区。
7. 正式 ImageGen 只接收身份锚点、装备锚点和 Pose Guide，不直接接收火柴人看板。

```powershell
python .agents/skills/pure-run-artwork-pipeline/scripts/pose_proof.py render-options `
  --spec <draft.json> --output .agents/tmp/pose-proofs/options.png `
  --preview-dir .agents/tmp/pose-proofs/previews

python .agents/skills/pure-run-artwork-pipeline/scripts/pose_proof.py select-option `
  --spec <draft.json> --option-id B --output-card Tools/artworks/<character>/pose-proofs/<pose>.json `
  --reviewer cty41 --reason "..." --rejected-reason "A=..." --selected-at "<ISO-8601>"

python .agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py --root . `
  create-pose-proof-exemption --asset-id <asset> --pose <pose> --direction <direction> `
  --consumer "..." --phase "..." --sprite-role "..." --category existing-approved-pose `
  --approval-id <approval-id> --reviewer cty41 --reason "..." --decided-at "<ISO-8601>"
```

## Draft v1

Draft 固定 `canvas=[256,256]`，并包含：

- 精确目标 `assetId`、`characterId`、`poseId`、`direction`；
- `visualMoment.consumer/phase/spriteRole`；
- 1–4 个 option；
- 每个 option 的 head 圆环、spine、抽象 segments、support/contact points 及可选 equipment axis。

颜色、线宽、背景与标签均由渲染器固定。Draft 不允许任意绘图指令或身份配色。对于无手臂胶囊角色，segments 是动作和力线抽象，不是允许最终成图生成人形手臂。

## Action Card

Action Card 是轻量设计决定，不是 Attempt、Series 或生成 receipt。它包含稳定 `poseProofId`、Visual Moment、选中 option、`cty41` 决定与未选摘要。未选方案完整几何及临时 PNG 不进入 Git；若改变选择，应生成新版本 Card，不覆盖旧决定。

## Composition v3

新动作 Composition 必须声明与 Action Card 完全一致的 `poseProofContext: {consumer, phase, spriteRole}`，并二选一：

- `poseProofDecision`: `{path, sha256, poseProofId}`；
- `poseProofExemption`: `{path, sha256, exemptionId}`，绑定 `Tools/artworks/pipeline/pose-proof-exemptions/` 下的不可变 receipt。

Decision 的 `assetId/pose/direction/visualMoment` 必须分别匹配 Composition、Contract 和 `poseProofContext`，禁止借用另一角色或另一运行时时点的 Card。豁免 receipt 同样绑定目标 asset、pose、direction、Visual Moment、cty41、时间与固定类别，并必须至少引用一份哈希有效、decision=approved、reviewer=cty41 的历史 Approval receipt；裸图片不能作为豁免证据。豁免仅限 `existing-approved-pose`、`technical-remediation`、`idle-adjustment`。确定性 Assembly 只有在同一目标 asset/pose/direction 已具有完整批准链时才能按 `existing-approved-pose` 豁免；否则仍需至少建立单方案 Action Card。

历史 schema v2 兼容范围冻结在 `Tools/artworks/pipeline/pose-proof-grandfathers.json`，该清单绑定启用前 revision 及每份 Composition SHA；清单外 v2 无法建立或通过新的动作合同门禁。

## 停止条件

姿态未选定、来源职责冲突、运行时时点错误、连续两轮重复同类语义失败、模型重画冻结区，或确定性 Assembly 已可完成时，停止 ImageGen 并返回对应决策层。普通/equipment retry 尚未全部实现代码级三轮熔断，因此 generation round 3 当前是流程预算，不冒充统一硬门禁。
