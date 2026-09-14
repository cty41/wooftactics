---
title: Pure Run 项目级美术方向状态机设计
status: active
---

# Pure Run 项目级美术方向状态机设计

> 本文继续描述当前 schema v4 的项目级设计，不因 vNext 研究而失效。下一代设计尚未确定；当前事实见[策略地图](generative-art-current-strategy-map.md)，复杂度见[复杂性审计](generative-art-state-machine-complexity-audit.md)，目标原则见[游戏美术生产第一性原则](game-art-production-first-principles.md)。在领域对象与状态关系另行确认前，不新增 vNext schema 或具体 AI 生产策略。

## 目标

在现有 artwork contract/job/attempt、技术校准、固定 Review、人工 approval 和不可变 receipt 之上，增加项目级美术方向与用例驱动验收，防止资产单独可用却在角色实装或最终场景中发生媒介、细节密度和注意力层级漂移。

人类可读的视觉权威是 [Pure Run 项目美术圣经](pure-run-art-direction-bible.md)；UI 专项规则见 [Pure Run UI Design Guide](pure-run-ui-design-guide.md)。机器负责绑定规则、生成证据、检测可测风险和阻止流程遗漏，不承担最终审美裁决。

## 非目标

- 不建立自动审美总分或机器 style approval。
- 不修改或伪造历史 receipt。
- 不一次性重审全部历史资产。
- 不预建完整世界 Theme Profile。
- 不修改 Godot runtime、Resource、场景和已接入纹理。
- 不在本版本实现 UI 皮肤、正式字体、完整 VFX 或运镜。

## 原则与优先级

```text
ArtDirectionManifest
  → ArtDirectionProfile
  → MaterialLanguage
  → FamilyProfile
  → optional ThemeProfile
  → AssetBrief / SceneBrief
  → Contract / Job / CompiledPrompt
  → Attempt / TechnicalReport
  → AdvisoryReport
  → AcceptanceCaseResult / ArtDirectionReview
  → ArtDirectionVerdict
  → Approval / Promotion
```

优先级固定为 Project → Material → Family → optional Theme → Brief → Feedback。下层只能具体化或收窄，不能取消上层。任一权威输入变化都会产生新 ID/hash；旧下游记录保持原绑定，不适用于改变后的输入。

## 不可变记录

### ArtDirectionManifest

指定新合同当前使用的 Project、Material 与已激活 Family Profile 版本。Manifest 发布后不可覆盖；切换采用新 Manifest，不改写历史合同。

### ArtDirectionProfile

包含项目视觉定位、六条视觉支柱、2D 视觉语法、颜色职责、注意力预算、屏幕空间信息层级和明确排除项。

### MaterialLanguage

包含八类材质配方、允许表现与禁止表现。它定义卡通化方法，不固定具体主题颜色。

### FamilyProfile

首期支持 `actor`、`equipment`、`world_environment`、`interactable`、`prop`、`vfx_readability`。UI 只绑定 UI Guide 的高层职责，首期不建立完整 UI 生产门禁。

### AssetBrief

记录职责、第一眼信息、家族、目标显示尺寸、使用上下文、身份不变量、形状/颜色/材质策略、注意力等级、允许变化、禁止项、参考职责、必需 Review 和 Acceptance Case。

### SceneBrief

记录玩法流程、叙事节拍、视线与移动路径、主焦点、最多两个次焦点、静区、Actor 状态、交互发现顺序、局部色材、动态强调、模块化/专属资产、禁止堆料区和必需 Review。

### AnchorVerdict

状态为 `candidate`、`approved-anchor`、`rejected-as-anchor` 或 `pending-more-evidence`。记录图片路径/hash、申请职责、适用家族、不适用范围、board hash、reviewer、reason 和 decidedAt。拒绝当锚点不改变资产原有 approval；反例不能进入生成输入。

### AcceptanceCaseResult

每项结果绑定：

- 稳定 `caseId`；
- attempt 与候选 hash；
- Profile、Brief 与适用 AnchorVerdict hash；
- 一张或多张证据图及 hash；
- 自动事实与 `passed|failed|warning|not-applicable`；
- 人工检查项、reviewer、结果和理由；
- 失败时唯一明确的下一版修正范围。

缺少必需结果、证据或人工结论时，Art Direction Review 不完整。

### ArtDirectionReview

绑定 attempt、候选、Profile、Brief、AnchorVerdict、AcceptanceCaseResult 和全部 panel hash。Review 是证据集合，不包含审美决定。

### ArtDirectionVerdict

`decision` 仅为 `approved|retry`，记录符合的视觉支柱、缺陷、已接受 warning 及理由、下一版修复范围、reviewer 和时间。只有 `cty41` 可以签发。

### ArtDirectionException

绑定具体上层条款、唯一资产/场景或明确集合、偏离范围、理由、证据和用户签名。它不得豁免技术、权利、provenance、hash 或必需 Review，也不自动成为全局先例。

## Contract schema

新合同采用下一个可用 schema 版本，并新增：

```json
{
  "artDirectionSpec": {
    "profileId": "pure-run-project-art-direction-v1",
    "path": "Tools/artworks/pure_run/art_direction/project_art_direction_v1.json",
    "sha256": "..."
  },
  "familyProfileSpec": {
    "profileId": "pure-run-actor-family-v1",
    "family": "actor",
    "path": "Tools/artworks/pure_run/art_direction/families/actor_v1.json",
    "sha256": "..."
  },
  "materialLanguageSpec": {
    "profileId": "pure-run-material-language-v1",
    "path": "Tools/artworks/pure_run/art_direction/material_language_v1.json",
    "sha256": "..."
  },
  "briefSpec": {
    "kind": "asset",
    "briefId": "...",
    "path": "...",
    "sha256": "..."
  },
  "themeProfileSpec": null,
  "anchorVerdictIds": [],
  "acceptanceCaseIds": [],
  "requiredReviewPanels": []
}
```

新规则激活后，普通新合同缺少 Project、Family、Material 或 Brief 绑定即失败。历史迁移只能走显式受限入口，不能静默创建旧式合同。

## CLI

新增命令：

- `register-art-direction-profile`
- `register-family-profile`
- `register-material-language`
- `create-asset-brief`
- `create-scene-brief`
- `render-anchor-board`
- `record-anchor-verdict`
- `record-acceptance-case-result`
- `render-art-direction-review`
- `record-art-direction-verdict`
- `approve-art-direction-exception`

现有 `create-contract`、`create-equipment-contract`、prompt 编译、`approve`、`promote` 和 `check --strict` 扩展新绑定检查。实现继续使用当前 Python/JSON store，不引入数据库、Web 服务或新第三方依赖。

## 规则编译

编译顺序固定为 Project → Material → Family → optional Theme → Brief → 当前 attempt 的结构化 Feedback。冲突必须显式失败并报告来源层。Feedback 只能添加或收窄修复要求，不能取消上层规则。相同输入必须产生相同 compiled prompt ID/hash。

## 锚点治理

候选板分为：

1. Actor 基础与叙事表现；
2. 敌对、大型、飞行等拓扑；
3. Equipment 孤立与角色实装；
4. World、Prop 与 Interactable。

候选按真实游戏尺寸或明确统一倍率展示。每项只申请具体职责，不能申请“代表全部项目画风”。用户签发 AnchorVerdict 后，正向锚点才可进入 prompt 和固定 Review。

本机第三方 UI 截图只登记游戏名、职责、文件名和 SHA-256，不进入 AnchorVerdict、不复制进仓库、不进入项目 provenance。

## Advisory Linter

公共 warning：颜色/色阶、平滑渐变、高频边缘、外轮廓相对内部线、缩小后特征存活、亮度/饱和度/对比热点数量。

家族 warning：

- Actor：脸部与身份可读、装备遮挡、主体/装备复杂度失衡；
- Equipment：孤立/实装轮廓差、是否压过 Actor、握点和装备轴风险；
- World/Prop：地面高频纹理、路径/阻挡对比、普通 Prop 与交互物显著性倒置；
- Scene：主焦点数量、静区、Actor/背景分离度、交互发现层级；
- VFX：覆盖面积、峰值时长、危险提示与伤害时点。

阈值从已批准锚点区间推导。无法可靠测量的维度标记为未激活，不输出假数据。Warning 不改变技术报告 passed、不创建 approval、不汇总为总分。

## Fixed Review Packet

共同面板：候选原图、透明背景检查、标准/较小显示尺寸、最近邻放大、职责锚点、真实上下文、Acceptance Case 证据与 advisory。只有与当前失败模式相关时才加入反例。

- Actor：身份母图、方向/动作、装备遮挡、Tile Review；
- Equipment：库存单图、128px、目标 Actor 实装、握点/装备轴、同类锚点；
- World/Scene：无单位底图、路径/阻挡覆盖层、完整构图、焦点热区；
- Interactable：普通 Prop 同屏、默认/靠近/悬停状态；
- VFX：预警/发动/命中/消散和遮挡覆盖。

## Approval 与 Promotion

新合同的 `approve` 必须验证：

- 技术报告通过；
- 必需 Acceptance Case 和 panel 齐全；
- Review 与候选/Profile/Brief/AnchorVerdict hash 一致；
- 每个必需 case 没有 failed 或缺失人工结果；
- 所有 warning 均被 verdict 接受或已通过 child attempt 修复；
- 同一 Review 上存在 `cty41` 签发的 approved ArtDirectionVerdict；
- 没有未授权 exception。

任一候选、Profile、Brief、锚点、用例证据或 Review 图片变化，旧 verdict 自动失效。

## 兼容与失败行为

采用“前向强制＋逐步审计”：旧 contract/attempt/approval/promotion 保持有效、只读和可复现；新合同必须绑定当前 Manifest；旧资产只有获得 AnchorVerdict 才能指导新资产；修改或新版本生产时进入新 schema。审计创建新记录，不回填或伪造历史 verdict。

以下情况硬失败：必需记录缺失/hash drift；下层弱化上层；新合同缺绑定；反例进入生成输入；错误 reviewer；跨候选 verdict；Review 或 Acceptance Case 不完整；exception 范围宽泛或试图豁免技术/权利/血缘问题。

Advisory 超界本身不硬失败，但必须进入人工 Review 并在 verdict 中处理。

## 诗人 Idle DR 试点用例

试点先登记用例，再生成候选：

- `POET-STYLE-001`：候选、诗人母图与魔剑士同尺寸比较；
- `POET-IDENTITY-001`：脸、耳、五红、核心体量和脚底保持；
- `POET-EQUIPMENT-001`：直身、完全入鞘、空心粗环首、无第二武器；
- `POET-POSE-001`：环首朝画面左肩，刀鞘低位落向右下，姿态闲适；
- `POET-GRIP-001`：远侧左爪在柄—鞘口自然包握；
- `POET-FREE-PAW-001`：近侧右爪放松且不误握；
- `POET-OCCLUSION-001`：六个 role 单层与累计遮挡正确；
- `POET-COMPOSITION-001`：256 母版、128 预览、64×32 Tile 与画布几何；
- `POET-EXCLUSION-001`：无古琴、酒囊、裸刃或第二装备；
- `POET-REGRESSION-001`：旧写实、浮爪和完整爪贴武器问题不复发。

门禁顺序：已批准唐刀 v2 组件 → Brief/Contract → 技术 Assembly → 十项证据 → 自动报告 → 用户 verdict → 全部通过后 approve/promote。任一 case 失败都形成结构化 retry，冻结已通过优点，只修失败项。

## 测试边界

工具测试只验证 schema、哈希、状态、证据完整性和门禁不可绕过；不建立重型随机/property/覆盖率工程。Golden Review 只断言面板组成、标签、顺序和绑定，不评价“好看”。

美术验证以 Acceptance Case 的真实证据和用户 verdict 为权威。自动技术通过、warning、Agent review 和 `check --strict` 均不能替代人工结果。
