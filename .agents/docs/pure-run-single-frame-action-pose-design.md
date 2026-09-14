---
title: Pure Run 单帧动作姿态设计（退役 Unity）
status: historical
verified_revision: c9ff3a710fd9
---

# Pure Run 单帧动作姿态设计

> 状态：`historical / FrozenOracle-only`。本文记录退役 Unity 的 `UnitPoseFamily`、ReleaseTime、PoseRestoreTime、Prefab 与 SpriteRenderer 行为，不再是当前 Godot 权威。当前实现由 `GodotBattlePresentationPlayer`、`GodotUnitActor`、`StandardUnitPresentationResource` 与 `PresentationCueKind` 定义；缺图回退方向 Idle。本文只用于历史意图追溯，新美术合同必须重新核对当前 Godot 行为。

## 目标

在现有 Tween 位移、缩放、旋转和 Release 时序之上，允许单位在动作期间切换一张静态 Sprite。该能力只增强表现，不参与命中、伤害、投射物、VFX、AI 或死亡结算。

首批使用四个可复用姿态族：`MeleeAttack`、`ThrownAttack`、`Cast`、`Hit`。需要独立美术的角色姿态只制作 `down-right` 与 `up-left` 两张原生图，其余方向由 `FourDirectionSpriteVisual` 只镜像主 Sprite Renderer 补齐；同一角色的不同姿态族允许显式复用同一方向 Sprite 对。

## 运行时模型

### UnitPoseFamily

`UnitPoseFamily` 是可复用的姿态语义资产，包含稳定标识和退出策略：

- `ThrownAttack` 在 Release 当帧退出。
- `MeleeAttack`、`Cast`、`Hit` 在恢复段开始退出。

未来可以新增 `ShotAttack`，不需要把通用远程 Ability 永久绑定为投掷姿态。

### UnitActionPoseProfile

角色 Profile 负责把语义映射为角色 Sprite：

- 保存 `Default` 与 `Unarmed` 两种视觉状态的 idle 方向对。
- 保存各姿态族、视觉状态对应的方向 Sprite 对。
- 保存每种 `UnitVisualAction` 的角色默认姿态族，以及默认 Hit 姿态族。
- 六类羊魔共用一个 Profile；Prefab 上已有材质、Tint、排序、阴影和死亡图保持不变。

### 解析与回退

Ability 显式指定姿态族时，解析顺序固定为：

1. 精确姿态族与当前视觉状态。
2. 同一姿态族的 `Default` 状态。
3. 当前视觉状态的 idle。
4. 原有基础 idle。

显式但缺图的未来姿态族不得改用角色的另一默认族。Ability 未指定姿态族时，才由角色 Profile 按 `UnitVisualAction` 选择默认族。

### Tween 标记

共享动作时间计划同时暴露 `ReleaseTime` 和 `PoseRestoreTime`。运行时与 Tween Preview 必须消费同一计划：

- Release 回调至多执行一次。
- 投掷姿态在 Release 清除。
- 近战、施法和受击姿态在恢复段开始清除。
- 移动、受击抢占、取消、销毁和显式停止都会恢复按当前视觉状态解析出的 idle。

## 赤柴持矛状态

`AmazonBattleState.IsSpearHeld` 是权威玩法状态，`FourDirectionSpriteVisual` 的 `Default / Unarmed` 只是视觉投影。

毒矛 Release 顺序固定为：

1. 切换到临时 `Unarmed` 视觉状态。
2. 清除 `ThrownAttack` 姿态。
3. 启动投射物与技能图执行。

技能成功、失败或取消后都按 `AmazonBattleState.IsSpearHeld` 对账。远程召回与免费拾取成功后恢复 `Default` 持矛视觉；免费拾取不播放动作姿态。战斗清理和死亡不能遗留错误视觉状态。

赤柴 `Cast` 的 `Default` 与 `Unarmed` 配置显式引用同一对无矛施法 Sprite。长矛只在 Cast 姿态显示期间视觉缺席，不修改 `AmazonBattleState.IsSpearHeld`，也不在 Release 回调切换全局视觉状态；恢复段清除姿态后，解析器按权威状态自动回到持矛或空手 idle。

## 美术契约

扩展后的首批规划为 22 张动作图：

- 赤柴 3 对：近战、无矛施法、无矛受击；`ThrownAttack` 显式复用近战方向对，`Cast` 与 `Hit` 的 `Default / Unarmed` 分别显式复用各自的一对无矛方向图，各自继续保留原有退出语义。
- 羊魔 4 对：近战、投掷、施法、受击。
- 法师 2 对：施法、受击。全部已发布法师 Ability 使用 `UnitVisualAction.Cast`，因此不提前制作未使用的近战或远程姿态。
- 死灵法师 2 对：施法、受击。动作图保留唯一匕首，但按美术决定移除只属于 Idle 的右手蓝色鬼火，不用其他 VFX 或法术物件替代。

另复审并接入已有两张赤柴空手 idle。动作图只画身体与随身武器，不烘焙 VFX、投射物、阴影、文字或地面。生产规格为 `256×256 RGBA`、`128 PPU`、底部 Pivot `(0.5, 0.078125)`、脚底基线 `y=236`；`_128` 仅用于 Review。

每次只推进一个角色的一个方向。必须先批准 `down-right`，再制作对应 `up-left`；未经明确批准，候选图不得进入 `godot/assets`、Unit Resource 或运行时 Profile。

### 真实战斗试玩闸门

赤柴基础试玩切片已在真实战斗确认切图时机、投掷复用与跨技能施法姿态成立。已批准的无矛受击方向对通过 `HitFamily` 在恢复段开始退出，`Default / Unarmed` 共用同一对 Sprite，姿态期间不改变 `AmazonBattleState.IsSpearHeld`。法师与死灵法师现已分别接入 Default 状态的 `Cast / Hit` 两对正式图；两者不配置 Melee、Ranged 或 Idle 覆盖，未命中姿态时继续回退各自既有 Idle。后续获批图片只替换正式源、运行时纹理和 Profile 引用，不改变动作时序或方向接口。羊魔动作图仍保持候选门禁；夜间任务只把图片加工到可审核状态，不自动批准或接入运行时。

## 边界

- 缺少姿态图时必须安全回退 idle，不能影响技能结算。
- 不引入逐帧动画、移动姿态、免费拾取姿态或弓箭 `ShotAttack` 美术。
- 不修改投射物、技能 VFX、伤害时序、AI、死亡逻辑或玩法规则。
- Sprite 切换不得修改 Material、Color、Sorting、Transform、Shadow 或其他 Renderer。
- 尸体继续使用独立死亡 Sprite，不继承当前动作姿态或镜像状态。
