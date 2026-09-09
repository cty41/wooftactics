---
type: Operational Playbook
resource: https://github.com/cty41/tactics/tree/main/Tools/artworks
title: Pure Run Artwork Pipeline
description: Pure Run 角色美术的生成、去幕、尺寸校准、Review 与提交入口。
tags: [operations, pure-run, artwork, sprite, godot]
timestamp: "2026-09-09T14:43:17+08:00"
status: active
catalog_scope: pure-run-artwork
repo_paths:
  - .agents/docs/pure-run-artwork-guidelines.md
  - .agents/skills/pure-run-artwork-pipeline
  - Tools/artworks
  - godot/assets
  - Tools/public-release/asset-provenance.json
verified_revision: c68dbebe
source_fingerprint: sha256:e340151bad80706a17dab2f9b92a97af1b3faa90a3ac184186bc6e4b8d8fd7b4
---

# Pure Run 角色美术流水线

> 公开边界：`Tools/artworks` 保留项目自有的 GPT 生成母图、候选、反例、校准稿与提示词。其中的媒体文件必须在 `Tools/public-release/asset-provenance.json` 中逐文件登记并通过公开发布校验；`approved` 来源状态只表示允许公开分发，不改变 `candidates`、`rejected` 或 `superseded` 的制作评审语义。运行时资产仍以 `godot/assets` 为准，恢复制作资产不代表运行时接入或人工视觉验收通过。

## Current State

- 项目级美术方向现由 schema v4 数据链治理：当前 Manifest 固定 Project Art Direction Profile、Material Language、Actor/Equipment/World/Interactable/Prop/VFX Family Profile 与 Brief 作者源 SHA；只有 `cty41` 签发的窄职责 `approved-anchor` 可绑定合同。每个 v4 候选先记录 Brief 声明的 Acceptance Case，自动事实只产生 passed/warning/failed 维度而不汇总美学总分；pending 人工证据可以先进入固定 Review，但只有全部最新结果为 `cty41 passed`、Review/候选/合同哈希一致且具有正式 ArtDirectionVerdict 时，普通 approval 与 promotion 才能通过。历史 rejected、retry 和 superseded 资产只允许作为带 `NEGATIVE / DO NOT USE AS INPUT` 标识的 Review 证据。
- 首个端到端试点是五红土松诗人 Idle DR：合同 `contract-57bf5c0526078c9d` 绑定诗人 v03 身份职责、魔剑士实装近战装备细节职责、已批准环首唐刀 v2 形制职责，以及十项 `POET-*` 用例。用户选择 75% 唐刀与 C 型低位放松握持，并在尾巴/脚爪切坏和旧爪脏区两次回归修正后明确批准十项；候选 SHA `e2246e0e...dfb3` 由 `art-direction-verdict-c53b61dc2d7da984`、`approval-3affe3151e3652b6` 绑定并离线晋升为 `Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png`。该事实不授权或实施 Godot 运行时接入；古琴保持移除，酒囊与出鞘唐刀继续延期。
- Reviewer MVP 已完成历史 Feedback/Case Fitness、Rule/Case/CompiledPolicy、不可变 Packet/Invocation/Result/Audit、Prompt-only 对照、显式 Shadow、按规则三档资格/替代/撤权、受控经验和 `medium → high → xhigh` 三轮熔断。Shadow 历史图严格保持 review-only；旧弱资格通过显式 supersession 保留而不再生效。已 selected 生成图可用 `recontract-reviewed-attempt` 保留原 Invocation/Delivery 并绑定诚实新合同与确定性 processing，不得伪造 invocation。五红土松诗人环首唐刀 Idle UL 已由 `cty41` 批准并仅离线晋升为 `Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_ul_v07.png`（SHA `519e288f…512`，Verdict `art-direction-verdict-b196da86a5b54f86`，Approval `approval-a71f2eecb8e0ec5e`）；未修改或授权 Godot 运行时接入。
- 人形像素 Amazon 探索已因重复误选正式退休：`Tools/artworks/amazon/**` 的 31 个文件被删除，22 个公开 provenance/legacy 活跃索引条目被移除，完整文件路径与 SHA 保存在 `Tools/artworks/pure_run/art_direction/references/retired_humanoid_amazon_v1.json`，历史 job/packet 仅为已有 attempt 审计而保留。管线即使看到从 Git 恢复的旧路径也会硬拒绝。项目中“Amazon”唯一指赤柴 Hunter；DR 尺寸与 Tilemap 对比权威图为 `Tools/artworks/doge/calibrated/doge_capsule_hunter_color_calibrated_v01.png`。
- Pure Run 装备图使用独立 v2 生产策略层：`equipmentProductionSpec` 绑定共享基础画风、weapon/shield/armor/jewelry/consumable 品类锚点及目标尺寸，新装备强制正式 ImageGen invocation 和逐版结构化 feedback。默认 `prepare-equipment-candidate` 保留原始色阶，只做去幕、透明 RGB、真实 Alpha 裁切、等比缩放和基线定位；色阶/渐变指标降为 advisory，“AI 味”由绑定候选与固定锚点 Review 哈希的 `cty41` style verdict 决定。技术量化只允许通过 child remediation attempt，不能覆盖原始输出。第三方截图只登记无绝对路径的本机 descriptor（职责、来源标签、文件名、SHA），不复制、不进入公开 provenance；新装备禁止 reviewed import，历史 `styleSpec` 记录继续只读兼容。当前正式装备包含亚马逊长矛/圆盾、法师橡木杖、死灵匕首、魔剑士绑定剑、普通铁剑，以及皮甲 V04、铁头盔 V04、皮靴 V05、暗影斗篷 V04、法师帽 V04、诗人环首唐刀、紧凑旅行古琴与皮革酒囊；平涂酒葫芦保留为未来正式装备备用，不代表已晋升或运行时接入。
- Job 绑定的 PNG 等二进制输入继续要求逐字节 SHA-256；Markdown/JSON/YAML/TXT 作者输入在 strict check 中只额外接受 LF 与 CRLF 的纯换行等价，适配仓库 `eol=lf` 而不放宽内容漂移。5 个既有装备 ready job 的非换行 prompt 漂移已在用户明确授权后通过 `migrate-ready-job-bindings` 正式迁移：旧 job 与其 prepared attempt 保持不可变，新 job 绑定当前 prompt，migration receipt 固定旧/新 job、变更与历史 attempt 哈希。
- `Tools/artworks/pipeline` 读取 schema v1/v2/v3 且不改写历史记录。v2 对高风险动作增加 `compositionSpec`、确定性 pose guide、candidate annotations、编译 prompt、ImageGen invocation/delivery/failure receipt、结构化 feedback 与非绑定 advisory。v3 增加胶囊角色受控 `body/equipment/paw_overlay/foot_overlay` 组件与 `assembled_sprite`：新 Assembly 必须包含远/近脚、远/近手、身体和装备六个 role 各一次，`layers` 数组按姿态声明由后到前的深度计划，near/far 不再机械绑定身体前/后层；完整获批姿势的正式语义蒙版可确定性派生身体、装备、手爪和脚爪，派生组件以源 approval、derivation receipt 与通过的验证报告为门禁，独立生成组件仍需 `cty41` approval，旧组件迁入需 migration receipt。Assembly 绑定全部输入、蒙版、顺序和变换哈希，并拒绝 role/kind 不匹配、语义污染、未标注主体像素、缺失/重复 role 与手脚断裂接触；`assemblyLayerReview` 同时展示单层和累积合成。组件永不直接晋升，最终 Sprite 必须另行验证并获得独立 `cty41` receipt。动作、死亡、遮挡或含姿态参考的生成 job 没有构图规范和导引时不得创建；`equipmentProductionSpec` 装备合同使用品类锚点和尺寸合同，不强制绑定角色姿态 composition。成功输出必须匹配 invocation 才能 ingest，调用失败不产生 raw SHA。Agent/advisory 不能批准。相同 raw 的技术重处理不增加唯一输出数，series strict feedback 只要求产生新 raw 的非技术 attempt，技术 remediation 继承父 attempt 的结构化反馈；唯一几何例外仍是 `core_size_out_of_tolerance`，且不能覆盖其他技术或语义问题。纯美术任务默认不运行完整 Godot Verify。
- Demonbound UL 六姿态 Tilemap 对比已登记为 supporting review。Idle UL 与 Melee UL 可从已晋升姿势及正式语义蒙版继续确定性拆层，但不得自动覆盖现有 Sprite。Cast UL v08 的离线视觉排列已获用户人工通过：两手完全由身体遮挡，两脚只露身体下缘，装备直接复用正式 Cast DR 剑并位于身体后层，仅露双耳之间的剑尖；该 verdict 不代表运行时接入，组件和最终 Assembly 仍需按状态机落成正式证据。
- Demonbound 九姿态 series 的顺序固定为 Idle、Melee、Cast、Hit 的 DR/UL 与单张 Death。Melee DR v9 已正式晋升；Melee UL 采用用户选定的 v7，保持 raw SHA `d1f47af...3ddf`，经技术重处理后只豁免核心宽度相对 Idle UL 锚点 `-4px` 的尺寸差，并以 `approval-b416727c3f90417d` 晋升。v6 以 Human feedback addendum 保留为 backup。Series 已推进到 Cast DR；它是首个原生 v2 job，已具备 composition、pose guide、compiled prompt、invocation 和 delivery receipt，真实 v1 候选等待逐版本人工视觉审核，不得自动批准。
- Demonbound 的 Hunter bootstrap 与 Idle DR 已获 `cty41` receipt，Idle DR 已晋升为后续唯一身份/核心锚点。用户要求后续每个版本逐次人工审核。Idle UL v1/v2 各占一个唯一输出并保持失败：v1 方向和体型漂移；v2 虽形成背向轮廓，但剑与持剑爪错误位于前层且缺少远手。剩余版本必须先通过新增的 behind-core 遮挡合同、统一核心校准和 depth Review。
- 背向合同可将近手、远手和装备声明为 `behind-core` 并限制可见面积；验证拒绝后层标签侵入批准核心、核心逐行断裂、完整外露手爪或缺失 depth Review。`calibrate-core` 将高分辨率 prepared 图与同坐标语义蒙版用同一统一比例和位移输出到 `256×256`，保留原始证据且禁止单轴拉伸。
- `c68dbebe` 是当前已提交角色美术的初始验证锚点；设计层正式资产由 Doge `calibrated` 与敌人 `approved` 共同组成，旧版本保留在 `rejected/superseded`，不得作为母图。
- Cast DR 的重制使用独立 v2 合同 `contract-69c81766e320c2d4` 与确定性中心线举剑构图：正式 Idle DR 只承担身份/核心锚点，双手夹持胸口中线剑柄，窄刃可经过面部中央窄带但不得进入左右眼区，护手固定在口鼻与领圈之间。Cast Sprite 不含剑鞘或静态魔法效果；剑尖聚能 FX 的挂点、翻转与 `charge/release/recover` 时序仅记录在延期设计，尚未接入运行时。旧 Cast DR v1 及其 retry feedback 保留，不能作为母图。
- 运行时标准角色纹理为 `128 PPU`，根节点与 `Sprite` 子节点均为 `localScale = 1`；单位状态由等距 Tile 高亮而非角色方形 Marker 表达。标准 `Sprite` 表现高度使用根空间 `localY=0.15`，降低原 `0.25` 带来的脚底悬空感；逻辑 Root、格子坐标、occupancy、碰撞与排序点不变。阴影继续锚定单位根节点代表的 Tile 几何落点，不跟随 Sprite Tween。
- 已确认的单格单位通用阴影以 `Tools/artworks/pure_run/shadows/approved/pure_run_unit_shadow_1x1_v01.png` 为设计源，并进入 `godot/assets/units/pure_run_unit_shadow_1x1_v01.png`。它是屏幕水平的 `64×32` 等距软椭圆；正式 Unit Resource 统一引用该纹理，并以 Tile 几何中心和脚底锚点决定地面/飞行偏移，不从动作 Sprite 的临时位置反推落点。
- 羊魔 `down-right v05 / up-left v01` 与蝙蝠 `down-right v06 / up-left v01` 已通过人工 Review，并从 `candidates` 升级到 `Tools/artworks/pure_run/enemies/approved`。小型蝙蝠按普通单位约 `75%` 的球核体量校准，球核中心在垂直方向对齐地面胶囊体上部圆帽中心，翅膀属于外部轮廓，球核中心、虚拟落点与 Tile 中心保持同轴。
- 蝙蝠风刃攻击 `down-right` 单帧姿态 `tomb_maw_bat_wind_blade_attack_dr_v03` 已获人工批准并于 2026-08-06 从 `candidates` 升级到 `approved`，作为当前生成状态下的临时收尾：双翼同步横扫、球核仅轻微反向旋转，设计与验收契约见 `.agents/docs/2026-08-06-pure-run-bat-wind-blade-pose-design.md`。`up-left` 姿态、飞行单位专用 Tween/Profile 与运行时接入均未开始；`v01/v02/v04` 失败稿保留在 `rejected/superseded`，`v03` 的色幕源图保留在 `concepts`。
- `Tools/artworks/amazon` 的旧人形/像素亚马逊资产家族已整体废弃，仅保留历史 provenance 与审计；不得再用于身份母图、姿态/尺寸参考、Tilemap 对比或运行时。当前 Pure Run 亚马逊视觉权威是 `Tools/artworks/doge/calibrated` 的已批准赤柴猎人 DR/UL；跨角色对比仍只提供姿态或标准单位体量，不替代怪物身份锚点。
- 方向变体以同角色已确认的 `down-right` 为唯一体量锚点；纯核心主体蒙版排除耳朵、口鼻、手脚、装备与特效，只用于测量和 QA，不参与成品合成。验收同时比较上下缘、中心、最大宽度与上中下三个截面，避免窄柱体或梨形下段。采用无手臂策略时，手掌必须以多像素接触面直接嵌入主体边缘，不能浮空或用细线连接。
- 死亡状态必须先按核心拓扑分类。参考几何只提供完整姿态生成后的角度、压扁度与尺寸验收目标；身体、耳朵和相连四爪必须保持连续生成轮廓，禁止分层旋转、非等比压缩或重投影。只有真正脱手的装备和限定眼区 X 眼可独立合成。旧 `render-death-recipe` 因导致轮廓碎裂已从公开 CLI 退役，历史 recipe 仍可读取。对已逐轮人工确认但缺少生成前 invocation 的精确成图，只能以 `render-size-comparison` 冻结四栏证据，再用受限 `reviewed_import` 诚实收编，禁止倒填调用记录；详细复盘见 `.agents/docs/2026-08-20-death-pose-deterministic-shaping-design.md`。
- 魔剑士死亡图 Round v04 已绑定身份源、256 候选、128 预览和四栏尺寸对比，由 `cty41` receipt 批准并以 `reviewed_import` 晋升为 `Tools/artworks/doge/calibrated/doge_capsule_demonbound_death_v01.png`；该事实不等于 Godot 运行时接入授权。
- 魔剑士 Cast UL v08、Hit DR v02 与 Hit UL v03 已在既有人工通过基础上完成透明 RGB 规范化、尺寸证据、`reviewed_import` approval 与正式晋升。Idle、Melee、Cast、Hit 的 DR/UL 和 Death 九张正式纹理已按字节一致副本接入 `godot/assets/units`，`DemonboundAssetFactory` 通过 ResourceSaver 将其绑定到 `PureRunDemonbound.tres`，并清除了 Amazon 模板遗留的 Ranged 占位引用；运行时动作切换与尸体观感仍由 `MQA-GODOT-DEMONBOUND-ACTION-ART` 人工复验。
- 赤柴 `doge_capsule_hunter_death_color_v04`、死灵 `doge_capsule_necromancer_death_color_v05`、法师 `doge_capsule_mage_death_color_v04` 与羊魔 `splitjaw_goat_charger_death_color_v03` 已获人工授权并复制为运行时死亡纹理。它们使用 `256×256`、`128 PPU`、中心 Pivot 与 Tight Mesh，由单位视觉配置传给通用 `Corpse`；尸体通过 `Sprite.bounds.center` 抵消透明画布偏移，不按生前朝向镜像，羊魔尸体继承生前材质以保留六种职责换色。
- 骷髅战士、骷髅法师和火魔属于召唤物，死亡后不生成尸体，因而没有配置死亡纹理；蝙蝠仍无运行时 Prefab。
- 蝙蝠死亡图 `tomb_maw_bat_ranged_death_color_v02` 当前位于 `Tools/artworks/pure_run/enemies/candidates`：保持近圆球核并缩小到活体球核之下，耳朵与脸部线索朝画面右上，双翼随朝向旋转后贴地瘫软；赤柴只提供屏幕朝向，不能提供胶囊体轮廓或细长身体轴。`v01` 保留为球核过大的历史候选；两版均未接入 Unity。
- 无脚底尸体使用完整死亡尸体 AABB 中心对齐 Tile，不沿用站立脚底或活体悬浮锚点；道具必须脱手，默认移除常驻职业特效。未经人工确认或未获得运行时授权的死亡图继续留在 `concepts/candidates`。
- 法师基础奥术弹 `doge_capsule_mage_arcane_bolt_projectile_color_v02` 已通过人工尺寸 Review并接入运行时：使用短梭形蓝紫轮廓、单帧中心锚点，在 `_128` 中约 `22×10 px`，对应法师主体宽度约 `42%`。`v01` 是偏大的历史候选；奥术、火焰和冰霜 Profile 共用该 Sprite 并通过 Tint 区分。
- 死灵基础投射物以静态鬼火和飞行版分工：`doge_capsule_necromancer_pale_orb_projectile_color_v02` 保留近圆核心与向上火舌，作为静态造型锚点；正式飞行版 `v03` 朝右、亮核略靠前、短火舌向左后拖曳，`_128` 可见 AABB 约 `22×13 px`，继续用于死灵基础魔法表现，不再作为 Bone Spear 的运行时 Sprite。粗黑圆环的 `v01` 已归入 `rejected/superseded`。
- 骨矛实体 Sprite `doge_capsule_necromancer_bone_spear_projectile_color_v01` 已完成中心校准、Tile Review 和人工确认：母版约 `66×14 px`、`_128` 约 `34×8 px`。运行时使用独立 `pure_run_bone_spear_projectile.png`、中心 Pivot、`128 PPU`、`Scale=1` 和切线旋转；最多两个短残影由 Profile 驱动，交叉闪光与骨屑继续由 Skill VFX Recipe 表达。
- 赤柴长矛 `doge_capsule_hunter_spear_projectile_color_v01`、法师奥术弹 `v02`、死灵飞行能量球 `v03` 与骨矛 `v01` 的运行时 PNG 由幂等配置器从批准源复制并做内容/导入约束校验。物理基础、普通/毒矛和羊魔临时物理远程复用长矛；毒矛只使用绿色 Tint，不新增专用 Sprite 或 Shader。
- 三组代表性正反案例覆盖核心胶囊体、远近手/装备层级和飞行球核。案例快照只用于 Review，正式原图路径与禁止复用的反例路径由 skill 的 `examples/cases.json` 管理。
- 设计、尺寸和目录语义见 `.agents/docs/pure-run-artwork-guidelines.md`，执行、案例库与只读校验见 `.agents/skills/pure-run-artwork-pipeline/SKILL.md`。
- 已接入视觉使用两张原生图补齐四向：East/up-left 镜像、West/down-right 镜像、North/up-left、South/down-right。该映射遵循 Godot 等距网格轴，不直接把原画文件名当作逻辑方向；显示层不改变 Core 朝向、移动、技能或 AI。蝙蝠仍是设计层资产，尚未接入运行时。
- 标准地面单位现共用一套 `StandardUnitTweenProfile`，主 `Sprite` Transform 承担 Idle、移动、攻击、施法和受击纸片 Tween；Shadow 与逻辑 Root 不参与。运行时已具备 `UnitPoseFamily` 与 `UnitActionPoseProfile` 的单帧姿态切换、`Default/Unarmed` 状态、双原生方向解析和安全回退；Sprite 可配置化切换，但 Material、Color、Sorting、Transform、Shadow、死亡图与 VFX 链保持独立。赤柴已接入空手 idle、近战/投掷复用、无矛施法和无矛受击共 4 对运行时图；Hit 的 `Default/Unarmed` 共用同一方向对并在恢复段按权威长矛状态回 idle。羊魔资产必须等待赤柴受击真实战斗 QA 通过。
- Pure Run 运行时视觉 QA 只使用 Godot 后台测试、生产输入注入链或已有截图；运行时接入、补截图、点击技能或补齐代表单位都不授权 Computer Use、窗口激活或真实输入。后台无法构造目标状态时标记 `manual_visual_qa_pending` 并交由用户手动确认，完整边界见[前台交互与焦点保护规则](https://github.com/cty41/tactics/blob/main/.agents/rules/foreground-interaction.md)。
- `Tactics/Pure Run/Presentation Graph Editor` 是新的统一表现编排入口：GraphView 连接 Tween、投射物、第三方 Prefab FX 与程序化 Recipe，隔离舞台以固定随机种子和运行时采样逻辑预览完整语义子图，并标记 Release/Impact。旧 Tween Preview 与 Skill VFX Preview 暂时保留为叶资产调试入口；蝙蝠专用悬浮/翼展动画仍为后续任务。
- 旧 `Tactics/Pure Run/Tween Preview` 作为叶资产调试入口继续复用运行时动作与姿态解析，支持 Pose Family、`Default/Unarmed`、四方向、实际回退、`0.5×/1×/4×` 以及 Release/Pose Restore 标记；复杂语义子图仍由 Presentation Graph Editor 负责。
- 赤柴猎人与裂颚羊魔的可复用单帧动作提示词库已分别保存到 `Tools/artworks/doge/hunter` 与 `Tools/artworks/pure_run/enemies/splitjaw_goat`。赤柴 `ThrownAttack` 保留独立 Release 退出语义但复用已批准的 `MeleeAttack` 方向 Sprite，`Cast` 与 `Hit` 的 `Default / Unarmed` 分别共用各自一对无矛 Sprite；羊魔四对动作图尚未生产，仍受逐方向人工批准门禁约束。
- 8 个 Lightning 实例在 640×360 RenderTexture、正交相机和显式逐帧渲染下的 Profiler 样本为 66 Draw Calls、10 Batches、10 SetPass、514 Triangles、1030 Vertices；同路径空相机基线为 0。原始 Draw Calls 严格 `<10` 的目标尚未满足，Frame Debugger 在 Test Runner 手动渲染路径没有提供事件，因此 overdraw 仍需真实 Game View/目标设备人工采样。不得把暖池 Rent/Return 的 0 B 回归或混合帧 GC 数字替代为渲染性能结论。

## Workflow

先从案例清单选择 `calibrated/approved` 中的唯一母图，并检查适用反例；一次只生成一个角色、变体或投射物，参考图只承担犬种、武器、姿态或配色的局部信息。方向图从同角色正确基础图原生重绘，再用纯核心主体蒙版做双色叠加与三截面验收；出现双轮廓、后脑鼓包或局部变胖时回到正式母图重生，禁止通过蒙版合成或擦线修补。死亡图先分类为胶囊地面单位或球形飞行单位，再分别锁定胶囊核心或球核；角度和压扁程度必须由完整姿态生成解决，状态机只做去幕、等比尺寸校准、AABB 居中、对比和证据登记，不再拆分核心、耳朵或四爪进行几何重投影。跨角色死亡参考不能替代身份母图。投射物使用画布中心锚点，与施法者 `_128` 主体同屏校准，并在 Tilemap 中按真实攻击方向旋转；前一张未获人工确认前不开始下一张。完成去幕、alpha 检查、母版定位和预览缩小后，再按资产类型使用脚底、虚拟落点、尸体 AABB 中心或投射物中心完成 Tile Review。先完成并人工确认单图，再从成功过程提炼最小通用规则；不要在成图前用未验证抽象驱动生产。

## Relationships

- 设计契约：`.agents/docs/pure-run-artwork-guidelines.md`
- 执行 skill：`.agents/skills/pure-run-artwork-pipeline`
- 正反案例：`.agents/skills/pure-run-artwork-pipeline/references/review-casebook.md`
- 死亡状态 Sprite 约束：`.agents/skills/pure-run-artwork-pipeline/references/death-state-sprites.md`
- 投射物 Sprite 约束：`.agents/skills/pure-run-artwork-pipeline/references/projectile-sprites.md`
- 正式母图清单：`.agents/skills/pure-run-artwork-pipeline/examples/cases.json`
- 相关候选与审计资产位于 `Tools/artworks/amazon`、`Tools/artworks/doge`、`Tools/artworks/pure_run`；获准接入的运行时纹理位于 `godot/assets/units`，正式绑定由 `godot/content/units` 的 typed Resource 与 Godot 运行时测试验证。
- 第三方 VFX 适配构建入口：`Tactics/Tools/Pure Run/Rebuild Piloto VFX Sample Assets`；生成器只重建毒矛、闪电、诅咒回退稿与正式三层 V2 法阵，以及对应的代表技能表现图，不批量重写其他职业资产。
- 提示词库边界：可复用 GPT Image 提示词文档由 `artworks-prompt-library` skill 维护，本 scope 只维护项目执行和验收状态。
- 前台交互边界：[前台交互与焦点保护规则](https://github.com/cty41/tactics/blob/main/.agents/rules/foreground-interaction.md)；本 scope 只补充 Pure Run 视觉 QA 的具体停止条件，不另行定义授权例外。

## Verification Guidance

```powershell
python .agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py --root . check --strict
python -m unittest discover -s .agents/skills/pure-run-artwork-pipeline/tests -p "test_*.py"
python .agents/skills/pure-run-artwork-pipeline/scripts/validate_sprite_assets.py --root Tools/artworks --strict --review-examples
python Tools/okf/catalog_impact.py report --worktree
python Tools/okf/catalog_impact.py sync --worktree --scope pure-run-artwork --write
python Tools/okf/validate_bundle.py
python -m unittest discover Tools/okf -p "test_*.py"
```

校验脚本只读 PNG 并输出机器可读摘要；`--review-examples` 同时验证正式母图清单、正反路径状态和案例快照。Godot 测试必须覆盖当前单位的纹理绑定、Land/Air 参数、Tile 落点和动作期间阴影稳定性。候选资产需使用 `--include-candidates` 额外查看，但外部武器轮廓不会被错误地当成发布尺寸失败。Git 提交前按路径暂存并排除 `.hermes/`、`tmp/` 和未授权运行时文件。

Artwork 校验的 Pillow 版本固定在 `.agents/skills/pure-run-artwork-pipeline/requirements.txt`；Windows CI 在统一 verifier 前显式安装该 requirements，避免依赖 runner 的预装状态。

死亡图仍以人工 QA 为发布门槛：胶囊单位检查与赤柴同向且平直短厚，球形单位检查近圆球核与头部朝右上；两类都只以核心体量比较大小，检查脱手道具层级、去幕后的 RGBA/透明四角、无精确色幕残边，以及以死亡尸体 AABB 中心完成的 Tile 居中。未经人工确认和明确运行时授权不得接入 Unity。

投射物同样以逐图人工 QA 为门槛：检查与施法者的相对体量、中心偏差、Tile 攻击轴、精确色幕和透明像素 RGB；确认前不得开始下一职业或接入运行时。


## Citations

暂无外部引用；当前状态以仓库中的指南、PNG 和验证命令为准。
