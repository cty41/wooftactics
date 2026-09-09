# Pure Run 正反案例

本页只保留最能阻止重复错误的三个案例。图片是 `128×128` Review 快照；生成或编辑时必须回到清单中的 `approved_source` 原图，不能把快照、`rejected` 或 `tmp` 文件当作母图。

机器可读的正式资产清单、原图路径和验收条件位于 [`../examples/cases.json`](../examples/cases.json)。

## 核心胶囊体体量

| 正确 | 反例 |
| --- | --- |
| ![法师正确核心体量](../examples/core-body-volume/correct_128.png) | ![法师错误核心体量](../examples/core-body-volume/incorrect_128.png) |

- **看什么：** 忽略折耳、口鼻、手脚和法杖，只比较中央胶囊体。
- **为什么错：** 反例按完整外轮廓或错误候选继续校准，主体宽高分布发生漂移。
- **如何避免：** 以同角色已确认的 `down-right` 为唯一体量锚点，检查上、中、下三个截面；核心蒙版只做测量和 QA。

## 远近手与装备层级

| 正确 | 反例 |
| --- | --- |
| ![死灵法师正确手部层级](../examples/equipment-layering/correct_128.png) | ![死灵法师错误手部层级](../examples/equipment-layering/incorrect_128.png) |

- **看什么：** 手掌是否贴身，以及目标方向下哪只手在前、哪只手应由身体部分遮挡。
- **为什么错：** 反例为了完整展示装备，把远侧手完整放在身体外，破坏了三维遮挡。
- **如何避免：** 生成前写明每只手和装备的绘制顺序；被遮挡的手仍保留可识别外弧和装备，但不画浮空手或细线手臂。

> 裂颚羊魔动作图不能从 Idle 层级或 DR 直接推断 UL。当前人工确认的 UL Melee/Thrown 契约要求双手与整把长柄武器位于身体后层，身体遮挡杆身中段和手掌内侧；手掌外弧仍须与主体多像素接触。旧的“UL 长柄斧固定前层”结论已失效。

## 飞行球核与翼展

| 正确 | 反例 |
| --- | --- |
| ![蝙蝠正确球核体量](../examples/flying-core-scale/correct_128.png) | ![蝙蝠错误整体体量](../examples/flying-core-scale/incorrect_128.png) |

- **看什么：** 先忽略两翼，只比较球核尺寸、中心和悬浮位置。
- **为什么错：** 反例按完整翼展处理整体缩放，使飞行单位相对地面角色过大。
- **如何避免：** 球核决定体量和中心；翅膀只检查横向安全边距，虚拟落点仍对准 Tile 中心。

## 使用边界

- `approved_source` 可以承担已登记的身份、比例或方向职责，但不会因此自动成为项目级风格锚点；风格锚点还必须具有明确职责的 `approved-anchor` verdict。
- `rejected_source`、`superseded`、历史 retry 和 negative regression descriptor 只能用于识别错误；即使某个局部正确，也禁止继续编辑或进入 prompt/Assembly 输入。
- 所有混合正反例的 Review 必须把每一栏明确标为 `POSITIVE ANCHOR`、`APPROVED COMPONENT`、`PENDING CANDIDATE` 或 `NEGATIVE / REJECTED — DO NOT USE AS INPUT`，不能只靠文件名或排列位置暗示职责。
- `correct_128.png` 和 `incorrect_128.png` 只用于快速 Review，不是正式 Sprite。
- 完整历史保存在 `rejected/superseded`，但不会逐张扩写进本页。
