---
title: Pure Run 角色美术指南
status: active
verified_revision: c9ff3a710fd9
---

# Pure Run 角色美术指南

这份文档是 Pure Run 角色 Sprite 的项目适配与离线生产契约。项目级视觉宪法、资产家族关系、材质抽象与审美克制由 [Pure Run 项目美术圣经](pure-run-art-direction-bible.md) 统领；本文件记录可复用的角色尺寸、目录和验收规则，不复制完整生成提示词或一次性候选讨论，实际 PNG 仍是外观真相源。项目级哈希绑定、职责锚点、Acceptance Case 与人工风格 verdict 见 [Pure Run 项目级美术方向状态机设计](pure-run-art-direction-state-machine-design.md)。

> **当前 Godot 边界：** 本文仍混有退役 Unity 的 PPU、Prefab、SpriteRenderer、`.meta`、`localScale`、`FourDirectionSpriteVisual` 和旧 Grid 映射，这些不是当前运行时权威。现行棋盘投影为 `96×48`，战斗 Actor 以 `.34` 缩放，方向与动作由 `GodotUnitActor`、`GodotUnitActionPose` 和 `GodotBattlePresentationPlayer` 处理。`256×256`、`y=236`、128 预览和 `64×32` panel 仍是当前管线执行的 Pure Run 项目适配规则，但 `64×32` 对当前 Godot Game View 的有效性待重新验证，不能作为通用独立仓规范或实际运行时验收替代。运行时事实以代码、Resource、测试和[等距棋盘投影与锚点合同](isometric-grid-anchor-contract.md)为准；本轮不改写历史 receipt。

## 合同状态机

- `Tools/artworks/pipeline` 是制作状态与血缘的机器权威；PNG 是像素真相源，两者必须以 SHA-256 绑定。ImageGen 仅是 `create-job` 与 `ingest` 之间的外部非确定性步骤。
- 读取器保留旧 schema 兼容且禁止原地批量重写历史记录；active Art Direction Manifest 下的项目级新合同设计目标是绑定 v4 Manifest/Profile/Material/Family/Brief，但当前实现仍可在缺少 Family/Brief 时建立 v2/v3，属于已知门禁缺口而非推荐路径。动作、死亡、遮挡或使用姿态参考的高风险任务必须先保存 `compositionSpec` 并确定性渲染 pose guide；规范固定核心轴、基线、隐藏握点、武器出口/尖端、禁入区、装备状态与可见面积上限。
- `compile-prompt` 将合同不变量、参考职责、冻结项和待修项确定性合并；`begin-generation` 在调用 ImageGen 前生成 invocation receipt。成功输出必须匹配 invocation 才能 ingest；工具失败或交付丢失只写 failure receipt，不产生 raw SHA，也不增加唯一输出数。
- Feedback v2 区分 Human 与 Agent，支持结构化缺陷和 backup disposition。视觉模型报告只能作为 `advisoryReviews`，不改变状态；正式 approval 只能由 `cty41` 签发并绑定候选、蒙版、annotations、报告和 Review 哈希。
- Attempt 的简化主分支为 `ready -> ingested -> prepared -> annotated -> review_pending -> approved -> promoted`；`rejected` 与 `technical_failed` 都是终止分支，不能进入 promoted。实际实现另有 calibrated 与 Model Review 中间态，Job/Feedback/Series/Approval/Provenance 也是独立状态轴，详见[当前策略地图](generative-art-current-strategy-map.md)。技术失败后必须新建 retry，不能修改失败 attempt 或把它作为母图。
- `prepare` 只负责确定性去幕、RGBA 与透明 RGB；核心体量必须由同坐标语义蒙版检查。聊天中的“通过”必须由 CLI 写成同时绑定候选和蒙版哈希的人工 receipt 才生效。
- `legacy-assets.json` 逐文件登记历史 PNG。缺失可证血缘的 `legacy-unresolved` 只可保留和查看；目录名不能让它自动成为母图。正式旧图只有补充审核过的核心蒙版后，才能成为新任务的几何锚点。
- 晋升只能由状态机写入 `calibrated/approved` 并同步公开 provenance。纯美术任务使用 Artwork、公开发布、LFS 与 OKF 门禁；完整 Godot Verify 仅在同时修改运行时或 Godot 代码时运行。
- 多姿态生产必须使用 versioned series：固定姿态顺序，并为每个姿态选择无限迭代或显式正整数输出预算；每个不同 ImageGen raw SHA 均有不可变 feedback。相同 SHA 的重复处理不增加版本数；没有上一版 feedback 不得 retry 或推进。预算变化必须通过状态机命令留下审核记录。只有有限预算耗尽的首姿态允许显式选择 provisional anchor，但所有下游结果仅是概念稿，禁止 approval/promotion，直至正式首姿态锚点修复。
- 背向三分之四图必须把合同指定的手爪与装备置于核心后层：语义蒙版中的后层标签不得侵入批准核心区域，核心逐行保持连续，只允许手爪与装备在身体轮廓外露出受限外弧。高分辨率 ImageGen 输出只能按源/锚点核心蒙版统一等比校准，不得使用完整 AABB 或单轴拉伸。
- 普通批准仍要求验证报告完全通过。若报告唯一问题是 `core_size_out_of_tolerance`，且继续等比校准会破坏另一核心尺寸，`cty41` 可通过 `approve-exception` 对该 attempt 签发限定几何例外。Receipt 必须保留失败报告并绑定候选、蒙版、报告、合同、全部 Review 哈希、实际/锚点核心尺寸、差值、容差和理由；任一绑定内容变化后自动失效。例外不得修改全局容差，也不得覆盖基线、Alpha、透明 RGB、色幕、蒙版、缺爪、接触、错误侧、梨形、遮挡、裁切、路径或哈希问题。

## 身体与屏幕契约

- 角色采用右下方 `45°` 等距视角，胶囊体、短手、短脚和粗深色轮廓构成统一的屏幕语言。角色身体锚点居于画布 `x=128`，脚掌沿等距方向错开但必须接触同一地面基线。
- 发布母版为 `256×256 RGBA`，完整可见轮廓高 `122 px`，底部基线为 `y=236`。游戏预览为 `128×128 RGBA`，按等比缩小后目标轮廓约 `62 px`，基线为 `y=118`。
- `64×32` 是 Tile Review 的参考占用，不是角色母版尺寸。武器、耳朵、翅膀或法术等外部轮廓可以改变横向包围盒，但不能借此缩放或漂移胶囊身体；未校准外部轮廓只能进入候选目录。

## 历史 Unity 运行时映射（非当前 Godot 权威）

- `godot/assets/units` 中用于标准胶囊角色的 `256×256` 纹理保持统一尺寸，并由 Unit Resource 的脚底锚点与显示比例校准；以 `122 px` 可见轮廓计算，角色约为两格 Tile 高。
- `64×32` 地面 Tile 继续使用 `64 Pixels Per Unit`，对应 Grid 的 `(1, 0.5, 1)` Cell Size。不要为了匹配角色而改变 Tile PPU、Grid 或相机。
- 角色纹理保持底部 pivot `(0.5, 0.078125)`；单位 Prefab 根节点与 Sprite 子节点保持 `localScale = (1, 1, 1)`。新角色在接入 Prefab 前先设置 PPU，不用 Prefab 缩放补救导入比例。
- 单位状态不使用角色子节点上的方形 `Marker`。待命、选中、已行动和可攻击状态由 `ProceduralTileHighlightRenderer` 在 `CurrentCell` 的等距 Tile 面上绘制；友方为低饱和蓝灰，选中为柔和琥珀金，已行动为弱灰蓝，敌对/攻击范围为暖红。
- Sprite 的底部 pivot 是运行时脚底锚点。阴影必须从主 `Sprite` 子节点的该锚点加极小向下偏移定位；不要按整张透明画布的 `Sprite.bounds.min` 或固定负 Y 值定位。

## 双原生图四向显示（方向关系保留，Unity 实现术语历史）

- 已接入的胶囊体单位只维护两张原生 `256×256` Sprite：`down-right` 与 `up-left`；`_128` 仅作设计层 QA，不进入运行时纹理目录。
- `FourDirectionSpriteVisual` 只接管单位根节点下名为 `Sprite` 的主 `SpriteRenderer`，不改变 Transform scale、`Shadow` 或爆炸动画 Renderer。逻辑 `FacingDirection` 与视觉映射固定如下：

| 逻辑朝向 | 原生图 | `flipX` |
| --- | --- | --- |
| East | up-left | `true` |
| West | down-right | `true` |
| North | up-left | `false` |
| South | down-right | `false` |

- 逻辑方向沿 Godot 等距网格轴解释：East 在屏幕上朝右上，West 朝左下，North 朝左上，South 朝右下。水平镜像方向明确接受矛、盾、匕首、鬼火、法杖和斧头的视觉换手；不得借此改变移动、技能、AI 或 Core 朝向语义。
- 运行时两张原生纹理均为 `256×256`、Single Sprite、`128 PPU`、底部 Pivot `(0.5, 0.078125)`。已有 down-right 纹理保留 `.meta` 与 GUID，仅更新像素内容；新增 up-left 纹理单独导入。

## Tile 表面契约

- Pure Run 地面 Tile 使用严格 `64×32` 的平面菱形，四角透明、无侧壁；暖灰与冷蓝灰版本必须共享完全一致的 alpha、轮廓和岩面位置。
- 菱形边缘保留约 1px 的低对比细边，颜色接近各自底色，不使用深炭粗描边或亮色外框。
- 顶面使用单一低饱和底色表达简化岩石；禁止内部亮暗色块、渐变、镜面高光、裂缝、苔藓、颗粒噪声、随机污渍和独立装饰插画。
- 颜色变化只承担地面节奏和格子辨识，不改变 Grid Cell Size、Tilemap 几何、相机或角色逻辑占位；角色向上超出 Tile 的可见图像属于正常视觉范围。

## 单格单位脚底阴影契约

- Pure Run 单格单位统一使用等距视角下的屏幕水平软椭圆阴影；阴影本身不带方向性投影，不随角色朝向翻转。批准源为 `Tools/artworks/pure_run/shadows/approved/pure_run_unit_shadow_1x1_v01.png`，运行时副本为 `godot/assets/units/pure_run_unit_shadow_1x1_v01.png`，两者必须保持原字节一致。
- 纹理固定为 `64×32 RGBA`、Single Sprite、`64 PPU`、中心 Pivot、Full Rect、Bilinear、Clamp，关闭 Mipmap、压缩、Read/Write 与 fallback physics shape，并启用 Alpha Is Transparency。Sprite 世界尺寸为 `(1, 0.5)`，与单格等距 Tile 几何一致。
- 地面单位的 `Shadow` 保持 `localScale = (1, 1, 1)`、Renderer RGB 白色且 alpha `1.0`；飞行单位使用同一 PNG，固定 `localScale = (0.75, 0.75, 0.75)`、Renderer RGB 白色且 alpha `0.60`。两者的虚拟落点都是单位所在 `64×32` Tile 几何中心，飞行阴影不随可见身体下沿或悬浮高度移动。
- Unit actor 根节点只表示 Tile Ground Center；`Sprite2D` 与 Shadow 的局部基线由 Godot Unit Resource 和运行时 actor 合同共同确定。阴影位置不得从会被 Idle/动作表现改写的 Sprite 姿态推导；飞行参数是可执行的作者契约，不代表蝙蝠已接入运行时。
- 当前共享 `Fighter.prefab` 的阴影供其 Pure Run 派生链及接受该共享变更的其他派生单位使用；`PureRunGoatSupport`、`PureRunSkeletonMage` 与 `PureRunSkeletonWarrior` 直接保存同一新 Sprite 引用。12 个 Pure Run 单位的 Shadow 都必须默认激活；`PureRunNecromancer` 不得保留禁用覆盖。独立 legacy `Skeleton.prefab`、GroundTiles Palette 和第三方示例继续保留历史资源。
- 所有 Pure Run 阴影必须使用 Godot 项目自有的静态 CanvasItem 表现，并由 Unit Resource/运行时设置颜色与透明度；不得引入会摆动顶点或忽略节点 modulation 的旧第三方 Shader。

## 母图、参考图与候选

- 母图（source/mother image）锁定角色身份、身体比例、脚位、盾牌和已确认的构图。局部编辑必须声明“保持不变”的区域，不得让 ImageGen 重新发明整个人物。
- 参考图只提供犬种特征、武器结构、姿态骨架或色彩启发，不迁移对方的固定比例、装备、材质、地图或画风。提示词中应明确每张输入图片的职责。
- 候选图用于比较和复盘；失败图必须隔离，不能被后续任务误当作母图或可用 Sprite。一次只生成一个角色或一个变体，先确认身体再迭代脸部、武器和层级。
- `Tools/artworks/amazon` 整个旧人形/像素亚马逊家族已废弃，仅保留历史 provenance 与审计用途，不得再作为身份母图、姿态/尺寸参考、Tilemap 对比或运行时资产。当前 Pure Run 亚马逊视觉权威是 `Tools/artworks/doge/calibrated` 下已批准的赤柴猎人 DR/UL；怪物对比引用赤柴版本，但仍不能把跨角色参考当作怪物身份或校准锚点。

## 死亡状态静态图

- 死亡图是独立静态状态，不是将站立图旋转或压扁。生成前先分类核心拓扑：胶囊地面单位仰面平躺、头朝画面右上、身体轴约 `60°` 且保持短厚平直；球形飞行单位保持近圆球核，只让耳朵、脸等头部线索朝右上，双翼瘫软贴地。
- 赤柴死亡图对胶囊单位提供姿态和紧凑体量参考；对球形单位只提供屏幕方向，不能迁移胶囊轮廓、四肢或细长身体轴。
- 尺寸校准只比较胶囊核心或球核，排除耳朵、四肢、盾牌、武器、法杖、翅膀和特效。完整 alpha AABB 只用于安全边距、裁切和 Tile 占用，不得驱动单轴拉伸。
- 死亡道具必须脱手；默认移除鬼火、火焰、光晕和粒子等常驻职业特效。任务明确批准的单件道具可贴近手掌或紧凑搭在尸体表面。
- 无脚底尸体与落地球形单位都使用完整死亡尸体 AABB 中心对准 `64×32` Tile 中心，不沿用站立脚底锚点或活体悬浮锚点。Tile Review 使用 `_128` 预览。
- 未经人工确认的死亡图只进入 `concepts` 或 `candidates`，不进入 `calibrated/approved`，也不接入 Unity。详细流程、当前锚点和失败清单见 [死亡状态 Sprite 约束](../skills/pure-run-artwork-pipeline/references/death-state-sprites.md)。

## 基础投射物静态图

- 基础投射物采用单帧中心锚点素材：母版 `256×256 RGBA` 的中心为 `(128,128)`，预览中心为 `(64,64)`；不使用角色脚底基线。
- 一次只生成并 Review 一个投射物，上一张未获人工确认前不得开始下一个职业。施法者正式图只提供配色、线宽和法术语言，不能带入角色、手掌或装备。
- 尺寸必须与施法者 `_128` 主体和真实 `64×32` Tilemap 同屏判断。有方向轮廓保留一张朝右原图，Tile Review 时按攻击轴旋转；未来平移与旋转由运行时负责。
- 手持或悬浮的静态火焰允许短火舌向上；飞行火焰必须让火舌逆飞行方向后掠，同时保留圆钝能量核心。流线化只调整外焰，不把核心拉成尖头或长彗星。
- 去幕和 Mitchell 缩小后都要重新合成到透明黑底，保证精确色幕为零且所有 `alpha=0` 像素 RGB 清零。详细流程见 [投射物 Sprite 约束](../skills/pure-run-artwork-pipeline/references/projectile-sprites.md)。
- 当前运行时正式投射物源为赤柴长矛 `v01`、法师奥术弹 `v02`、死灵飞行能量球 `v03` 和骨矛 `v01`。运行时副本必须与设计源 PNG 内容一致，统一导入为 `256×256`、Single Sprite、`128 PPU`、中心 Pivot、无 Mipmap/压缩；颜色职业变体由 `ProjectileVisualProfile.Tint` 表达，不复制新 Sprite。骨矛使用原生比例、沿飞行切线旋转，并以最多两个短残影提供运动反馈；残影不烘进 PNG，也不阻塞技能结算。
- 已明确批准接入运行时的死亡图复制为独立 `256×256`、Single Sprite、`128 PPU`、中心 Pivot `(0.5, 0.5)` 与 Tight Mesh 纹理；它们配置在单位视觉组件上，单位死亡后由同一通用 `Corpse` 实例显示。正式死亡图的完整 Alpha AABB 已以整数像素对齐画布中心，尸体 Sprite 使用零局部偏移，不再通过 Tight bounds 修补美术画布。尸体不继承生前方向镜像，但继承主 Renderer 的材质、颜色、Sorting Layer 与当前 Sorting Order，使羊魔职责换色和既有战场遮挡规则继续生效；缺少来源 Renderer 的旧单位保留尸体 Prefab 原始排序。
- 当前运行时尸体图覆盖赤柴猎人、死灵法师、凯利蓝㹴法师与六种羊魔职责。骷髅、骷髅法师和火魔属于召唤物，仍按战斗规则直接移除且不生成尸体；蝙蝠尚无运行时 Prefab，不提前导入。
- 现有死亡图的等比体量复核使用 Hunter `1.00×` 为倒地投影基准，并生成 Mage `1.04×`、Necromancer `1.16×`、Goat `1.05×` 候选。第二轮只做整数像素平移，形成并正式接入 Hunter `v04`、Mage `v04`、Necromancer `v05`、Goat `v03`；不缩放、不旋转、不改姿态。批准依据为多 Tile 的 `death_state_volume_review_v03.png` 左列精确 AABB 居中；右列 `+8px` 只保留诊断意义。旧 `v02` 单 Tile 版曾把 Sprite 中心错误放在 Tile 中心上方 `25px`，仅保留为历史，不得作为落地判断依据。

## 单帧动作姿态视觉语言

- 标准地面胶囊角色的 `Hit` 以已批准赤柴受击图作为统一漫画夸张程度参考：核心主体整体向来击反侧后仰，耳朵后折或压低，可见眼睛放大眼白并缩小瞳孔。正面两眼都可见时，默认从双眼画出两条短而清晰的蓝白泪线；泪线是面部反应符号，不是 VFX，不扩展为水花、粒子或命中闪光。
- `up-left` Hit 必须把已批准 DR 的同一冻结受击时刻原生转到背向三分之四视角，保留主体倾斜、耳朵惯性、装备偏转和可见泪线方向；不得从直立 UL idle 只重画一张受伤表情，也不得为展示双眼而变成正面。
- 法杖类 `Cast` 姿态必须由“主体向目标前倾 + 法杖同向前指”共同建立施法轴。持杖手以杆尾至晶体底座之间的直杆中点为默认握点，手掌两侧可见杆身约为 `45:55–55:45`；晶体是指向目标的最前端，杆身不得遮挡眼睛或口鼻。短匕首等非杆状装备只迁移指向语义，不套用中点握杖规则。
- 跨角色参考图只迁移动作骨架、夸张程度或握点，不迁移犬种、配色、核心体量、脸部解剖或装备清单。角色原有装备必须按姿态规则保留：法师继续持有唯一晶体法杖，死灵继续持有唯一匕首且 `Cast / Hit` 完全移除 Idle 蓝色鬼火。

## 运行时极简 Tween 表现

- 标准地面胶囊单位共用 `StandardUnitTweenProfile`。Idle、移动纸片摆动、近战突进、远程后坐、施法发光和受击回弹只作用于名为 `Sprite` 的隔离视觉 Transform；动作期间可由 `UnitActionPoseProfile` 配置化切换同一主 Renderer 的单帧 Sprite。逻辑 Root、Shadow、血条、飘字和 Tile 高亮不得参与 Tween 或姿态切换。
- 凯利蓝㹴法师和墨西哥无毛犬死灵法师分别使用 `MageActionPoseProfile` 与 `NecromancerActionPoseProfile`，只映射 Default 状态的 `Cast / Hit`。正式源位于 `Tools/artworks/doge/calibrated`，运行时副本位于 `Textures/Actions/Mage` 与 `Textures/Actions/Necromancer`；后续换图保持 Profile 与姿态族不变，只替换批准源、运行时纹理和引用。
- `UnitTweenVisual` 以轻量生命周期 `Alive → Dying → Removed` 约束前景表现。Alive 内保留受击、攻击/施法、移动、Idle 的优先级；Dying 是终止性抢占，拒绝后续动作且不恢复 Idle。致死攻击按 `0.07s` recoil、`0.05s` shake、`0.08s` collapse 播放：末段回到 Tile 基准与基础旋转，并从底部 Pivot 压到约 `1.02× / 0.58×`，在 `0.20s` 幂等交接独立尸体。取消和销毁也必须执行同一交接。玩法尸体在死亡当帧已经占格和可交互，只延迟 Renderer 显示；交接后继续播放 `0.28s` 落地。普通打断使用 `Kill(false)` 并恢复 Prefab 原始局部姿态，不用 `Kill(true)` 强制完成旧回调。
- 赤柴 Hit 的 `Default / Unarmed` 共用同一对无矛受击 Sprite；受击姿态只隐藏图内长矛，不修改 `AmazonBattleState.IsSpearHeld`，并在恢复段开始按权威视觉状态回到持矛或空手 Idle。
- Cast 蓄力使用 `SkillVfxRecipe` 的非阻塞 `CastCharge` 径向光环，以施法者可见 Sprite 中心为锚点并排在人物与阴影后方。无专属 Recipe 的 Cast 回退到低饱和蓝色光环，火球和骨矛可由技能族 Recipe 覆写颜色；禁止复制、染色或实心覆盖整张人物 Sprite。施法期间允许由姿态 Profile 切换主 Sprite，但不得改变 `Material`、`Color`、Sorting 或烘焙 VFX、投射物和阴影。
- 远程/施法动作在 release 标记启动 SkillGraph；`ProjectileLaunch` 抵达后才继续 `OnHit` 和玩法效果。场景卸载或取消必须先把等待任务标记为取消，再 Kill 临时 Tween 并销毁 Renderer，避免 `OnKill` 抢先报告成功。
- 飞行蝙蝠不使用这套地面胶囊动画；其独立悬浮与飞行动画留待专用 Profile。
- 编辑器入口 `Tactics/Pure Run/Tween Preview` 在隔离的 `PreviewRenderUtility` 舞台中复用运行时单位 Sequence 和投射物视觉构建，支持单项/组合动作、四方向、2–6 格距离、循环、倍速和时间拖动。`CorpseLanding` 继续单独检查落地；`Lethal Hit → Corpse` 使用 Target 作为攻击来源并复用运行时生命周期，标记 Recoil `0s`、Shake `0.07s`、Collapse `0.12s`、Handoff `0.20s`、Impact `0.33s`、Impact End `0.40s` 与 Settled `0.48s`。Stop、Restart、Loop、Actor 切换和窗口关闭必须恢复活体 Sprite/Shadow、销毁临时 Corpse 并清除 Tween。Play Mode 选中 `UnitTweenVisual` 时，Inspector 只读显示 Lifecycle、Priority、活动 Tween、generation 与 Handoff；不得提供强制状态按钮。Profile 始终通过隐藏沙盒编辑，只有明确点击 Apply 才借助 Undo 写回资产。
- Tween Preview 只显示角色动作、Release、ProjectileImpact 及 Sprite/SoftDisc 弹道、脉冲和尾迹；技能光环、命中特效与 Recipe 分层继续由独立的 `Tactics/Pure Run/Skill VFX Preview` 检查，两套工具不互相复制职责。

## 标准流水线

1. 先确认唯一母图、角色方向、保留区域和尺寸契约；不同时生成多个角色。
2. 使用纯绿色或洋红色幕生成/编辑单个高分辨率角色，外部武器和特效完整留在画布内。
3. 去除色幕并检查 alpha 边缘；不要以带绿边的截图作为 Sprite。
4. 在 `256×256` 画布内等比缩放并定位到标准脚底基线，再生成 `128×128` 预览。
5. 在 128 预览和临时 `64×32` Tile 线框中确认脸部识别、武器轮廓、脚掌接触和遮挡关系。
6. 按 `concepts / calibrated / candidates / rejected / tmp` 的语义归档，给每个版本保留可追溯的角色名和尺寸状态。
7. 运行只读校验脚本及正反案例清单检查，再运行 OKF 影响报告和 bundle/unit 检查；提交时按路径暂存，排除临时目录。

## 目录语义与当前状态

| 目录 | 语义 | 是否可作为运行时 Sprite |
| --- | --- | --- |
| `Tools/artworks/amazon` | 已废弃的人形/像素亚马逊探索；仅历史 provenance 与审计 | 否，不得作为身份、姿态、尺寸、Tilemap 或运行时参考 |
| `Tools/artworks/doge/concepts` | 角色设计锚点和未发布变体 | 否 |
| `Tools/artworks/doge/calibrated` | 已按统一尺寸契约校准的 Doge 发布集 | 是设计层面的可用 Sprite |
| `Tools/artworks/doge/rejected/superseded` | 已被正式版本替代的 Doge 历史失败稿 | 否，只供复盘 |
| `Tools/artworks/pure_run/enemies/approved` | 已通过人工 Review 的非 Doge 正式敌人集 | 是设计层面的可用 Sprite |
| `Tools/artworks/pure_run/enemies/candidates` | 外轮廓或尺寸仍待 Review 的怪物候选 | 否 |
| `Tools/artworks/pure_run/enemies/rejected` | 已明确否决的失败资产 | 否 |
| `Tools/artworks/pure_run/enemies/rejected/superseded` | 已由 approved 版本替代的历史敌人稿 | 否，只供复盘 |
| `Tools/artworks/pure_run/tiles` | Tile 占用与配色 Review 参考 | 否 |
| `Tools/artworks/pure_run/shadows/approved` | 已确认的单格等距脚底阴影设计源 | 是，需原字节复制到运行时路径 |
| `tmp` | ImageGen、去幕和临时比较文件 | 否，永不提交 |

`calibrated` 和 `approved` 只保留当前正式锚点；旧版本移动到 `rejected/superseded`，保留原文件名与版本号，但禁止再次作为生成母图。`candidates` 只保存仍需人工决定的资产，不能充当历史归档目录。旧横胖蛤蟆继续位于 `rejected`，仅供失败复盘。

## 正式资产锚点

| 单位 | Down-right | Up-left |
| --- | --- | --- |
| 赤柴猎犬 | `calibrated v01` | `ul v02` |
| 墨西哥无毛犬死灵法师 | `calibrated v04` | `ul v07` |
| 凯利蓝㹴法师 | `calibrated v03` | `ul v05` |
| 犬骷髅战士 | `calibrated v02` | `ul v01` |
| 犬骷髅法师 | `calibrated v02` | `ul v01` |
| 火魔 | `calibrated v03` | `ul v01` |
| 裂颚羊魔 | `v05` | `ul v01` |
| 墓穴大嘴蝠 | `v06` | `ul v01` |

机器可读的完整路径位于 [Pure Run Artwork Pipeline 案例清单](../skills/pure-run-artwork-pipeline/examples/cases.json)。山羊和蝙蝠已从 `candidates` 升级到 `approved`；其旧版本进入 `rejected/superseded`。

## 正反案例生命周期

- 最小案例库只保留核心胶囊体、远近手/装备层级、飞行球核三类代表问题，入口见 [正反案例](../skills/pure-run-artwork-pipeline/references/review-casebook.md)。
- Skill 内的 `128×128` 图片是快速 Review 快照；实际生成必须使用清单中 `approved_source` 或 `approved_assets` 指向的 `256×256` 原图。
- 反例必须来自真实失败资产，并位于 `rejected`；不能把正确候选误标为反例，也不能为了补案例人为制造失败稿。
- 反例只用于识别禁止项。即使局部正确，也不得继续编辑、改色或作为方向图母图。
- 完整历史保留在 `rejected/superseded`，但只将能说明独立错误、且在 128 尺寸仍可辨认的版本加入案例库。

## 边界与关联

默认的生成与校准流程不修改 Godot Resource、AI、遭遇配置或运行时代码。只有用户明确授权运行时美术接入时，才按“两个原生图 + 水平镜像”的固定映射更新纹理和 Unit Resource；该接入不得改变玩法朝向、AI 或遭遇语义。可复用提示词文档仍由 `artworks-prompt-library` skill 负责；本项目的 [Pure Run Artwork Pipeline skill](../skills/pure-run-artwork-pipeline/SKILL.md) 负责执行、验收、案例归档和提交准备。详细提示词继续保存在 `Tools/artworks/amazon` 等实际资源目录，不在 OKF 页面重复。
