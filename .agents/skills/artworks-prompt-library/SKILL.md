---
name: artworks-prompt-library
description: "Use when creating reusable GPT Image prompt libraries for Godot 2D character sprites — analyzes a reference image plus a short character brief and writes structured prompt docs under Tools/artworks/, one folder per character. Prompt-doc authoring only; actual generation/chroma/review runs through pure-run-artwork-pipeline"
---

# Artworks Prompt Library

为 Godot 2D 角色 sprite 生成可复用的提示词库真相源。这个 skill 只负责**产出 prompt 文档**，不负责实际生图、sprite sheet 拼接或运行时导入。

## Quick Reference

| 操作 | 输出 |
|------|------|
| 分析参考图 | `reference_notes.md` |
| 生成母提示词 | `base_style_prompt.md` |
| 生成角色提示词 | `character_prompt.md` |
| 生成动作提示词 | `idle_prompts.md` / `walk_prompts.md` / `attack_prompts.md` |
| 稳定性测试 | 单张等分 sequence sheet + 机械切分规则 |
| 生成输出规格 | `output_spec.md` |

## When to use

- 用户希望基于参考图，为某个 Godot 2D 角色建立提示词库
- 需要把 sprite prompt 从聊天内容沉淀为 `Tools/artworks/<角色名>/` 下的稳定文档
- 需要复用统一的 isometric pixel-art sprite 约束到多个角色
- 需要后续继续扩展 idle / walk / attack 等动作帧
- 需要为 `60fps` 游戏中的低帧 sprite 先定义稳定输出规格

## Workflow

### Step 1: 收集最小输入

调用前至少确认三项输入：

- `角色名`
- `参考图路径`
- `一句角色描述`

如果用户没有给更多细节，第一版默认只产出：

- `idle` 6 帧
- `walk` 8 帧
- `attack` 6 帧
- `4` 个等距方向：`down-right / down-left / up-left / up-right`

并默认假设：

- 游戏运行在 `60fps`
- 角色动画采用 `低帧原画 + 60fps 节奏控制`
- 角色是 `小体量 isometric battle unit`

### Step 2: 分析参考图

先抽取应该保留的视觉信号，再明确排除项。至少覆盖：

- 比例
- 脸部简化方式
- 像素密度与块感
- 轮廓可读性
- 应忽略的背景、阴影、倾斜、展示感

输出到 `reference_notes.md` 时，必须拆成：

- `Keep`
- `Ignore`
- `Avoid Drift`

### Step 3: 生成母提示词

`base_style_prompt.md` 必须把“这是 production-ready 低分辨率 sprite，不是精致像素插画”写死。至少包含：

- strict low-resolution pixel art
- coarse pixel density
- no anti-aliasing
- no painterly rendering
- no glossy highlights
- no smooth lighting
- limited palette
- production-ready sprite readability

### Step 4: 生成角色提示词

`character_prompt.md` 只写角色身份与一致性约束，不混入动作帧细节。至少覆盖：

- 身份与职业
- 发型 / 服装 / 配色
- 主武器 / 副手装备
- 装备唯一性限制
- 脸部极简规则

### Step 5: 生成动作文档

必须固定产出：

- `idle_prompts.md`
- `walk_prompts.md`
- `attack_prompts.md`

每份文档都包含：

1. 一个动作层模板
2. 一个可直接复用的完整组合模板
3. 逐帧骨架

逐帧骨架中的每一帧必须写出：

- `frame X of N`
- head state
- torso state
- left arm state
- right arm state
- left leg state
- right leg state
- weapon state
- consistency constraints

当用户需要验证同一角色的低幅度 idle 连续性时，`idle_prompts.md` 还必须提供一个可选的 sequence sheet 测试模式：

- 用一张图承载整套帧，降低独立生图的身份漂移
- 根据输出画幅选择等分网格；正方形输出优先使用 `3x3`，前 `8` 格为动画帧、第 `9` 格完全透明
- 明确读取顺序、每帧的闭环轨迹、固定脚底基线和固定装备锚点
- 切分后禁止逐帧 trim、重新居中、独立缩放或独立背景处理
- 该模式是 prompt 资产测试流程，不改变本 skill 不直接生图的边界

### Step 6: 生成输出规格文档

必须额外产出 `output_spec.md`，用于锁定这套角色资源的输出目标。至少覆盖：

- 母版画布尺寸
- 透明背景要求
- 固定脚底基线
- 安全区
- 动作默认帧数
- 4 向方向集
- 命名规范
- 推荐播放节奏

如果目标是 `60fps` 游戏，默认推荐：

- `idle`：6 帧原画，按 `6fps` 播放；若使用 sequence sheet 稳定性测试，可改为 `8` 帧、`8fps`、`1s` 闭环
- `walk`：8 帧原画，按 `12fps` 播放
- `attack`：6 帧原画，按 `12fps` 播放

### Step 7: 固定输出位置

把结果写到：

`Tools/artworks/<角色名>/`

固定文件集合：

- `base_style_prompt.md`
- `character_prompt.md`
- `reference_notes.md`
- `output_spec.md`
- `idle_prompts.md`
- `walk_prompts.md`
- `attack_prompts.md`

如果目标目录已存在，按“提示词真相源”思路覆盖已有 markdown，而不是保留聊天草稿副本。

### Step 8: 保持边界清楚

这个 skill 第一版**不做**：

- 不调用 GPT Image 或其他生图工具
- 不保存 PNG、sprite sheet 或中间图片
- 不处理 Godot import、pivot 或运行时资源设置
- 不自动推断完整世界观或剧情设定

```markdown
# Example invocation

请基于参考图 `C:/path/to/ref.png`，为 `amazon` 生成提示词库。
角色描述：手持标枪和圆盾的亚马逊女战士。
```

## Anti-patterns

| ❌ 错误 | ✅ 正确 | 原因 |
|---------|---------|------|
| 直接产出一段长 prompt | 拆成 base / character / action 文档 | 后续难复用、难维护 |
| 因为游戏是 60fps 就要求 60 张原画帧 | 保持低帧原画，靠播放节奏适配 60fps | 小体量 tilemap 单位不需要超高原画帧数 |
| 把参考图所有内容都当成要保留 | 明确区分 Keep / Ignore | 背景阴影和展示姿态会把模型带偏 |
| 动作文档只有“frame X of N” | 每帧写 body-part 级骨架 | 否则帧间一致性差 |
| 为 idle 独立生成多张成品图 | 先用单张等分 sequence sheet 验证连续性 | 独立生图会重画角色轮廓、装备和构图 |
| 切分后逐帧自动裁切 | 保留统一 tile canvas，只允许整张 sheet 的背景清理 | 独立裁切会制造基线和体量抖动 |
| 用镜像补齐所有等距方向 | 对不对称装备角色默认做 4 向原生绘制 | 否则长矛手和盾手会反掉 |
| 在 skill 里顺带规定 Godot 导入 | 把边界限定在 prompt 库 | 第一版目标是稳定提示词真相源 |
| 为了好看放宽到像素插画 | 优先 production-ready sprite 约束 | 用户目标是游戏资源，不是展示插画 |

## Checklist

- [ ] 已收集角色名、参考图路径、一句角色描述
- [ ] 已输出 `reference_notes.md` 且包含 Keep / Ignore / Avoid Drift
- [ ] 已输出 `output_spec.md`
- [ ] 已输出 `base_style_prompt.md`
- [ ] 已输出 `character_prompt.md`
- [ ] 已输出 `idle_prompts.md` / `walk_prompts.md` / `attack_prompts.md`
- [ ] 每帧都包含 body-part 级骨架字段
- [ ] 如需 idle 连续性测试，已写明 sheet 网格、读取顺序、空白格和禁止逐帧裁切规则
- [ ] 已写明 60fps 游戏下的推荐播放节奏
- [ ] 文档没有混入生图执行或 Godot 导入步骤
