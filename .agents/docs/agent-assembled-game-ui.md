---
title: Agent 继续开发游戏 UI 的上下文
status: agent-handoff-2026-11
---

# Agent 继续开发游戏 UI 的上下文

这是给本仓库下一个 Agent 的交接，记录到 2026-11-12。它不是第二份 UI 权威。方向以 [pure-run-ui-design-guide.md](pure-run-ui-design-guide.md) 为准，外部资料的取舍和十条自查以 [ui-skill-survey.md](ui-skill-survey.md) 为准。代码、资源和测试与这两份文档冲突时，先核对代码，再改文档。

工作区里还有与本任务无关的未提交改动（画师管线和诗人校准图）。不要暂存、还原或顺手整理它们。

## 先读什么

1. 本文。
2. [pure-run-ui-design-guide.md](pure-run-ui-design-guide.md) 全文。表皮尚未原型验证，不得据此替换运行时 UI。四角分区是原型起点，不授权立刻重排现有 HUD。正式字体、图标、边框宽度、圆角、纹理强度、动效时长仍是延期项。当前继续用测试占位字体，不新增字体资产。
3. [ui-skill-survey.md](ui-skill-survey.md) 全文。下面的规则是它的执行摘要。
4. 改 C# 或生成资源前读 `.agents/skills/godot-csharp-development/SKILL.md` 和 `.agents/skills/godot-editor-lifecycle/SKILL.md`。编辑器会话不为 0 时，按生命周期技能关闭并在结束时恢复。禁止手写或手改 `.tres` / `.tscn`。
5. 人工验收账本 `.agents/docs/manual-acceptance.md` 里的 `MQA-GODOT-UI-RULE-PILOT`。自动测试不能把它改成 passed。

## 已经决定的做法

用户要的是 KISS：先做简单、干净、不扣分的界面，不先追求风格化或整套设计系统。

讨论过、并且已经定下的顺序是：

1. 规则先落进 Theme，不先把所有界面迁到场景。
2. 样式应该数据化。运行时试点期间仍用 `GodotTacticsTheme.Create()`，生成的 Theme 资源只给编辑器预览。
3. 布局先只改一个面板，并同时给出代码版和场景版，等用户看过再决定新界面默认用哪一种。用户还没有选。
4. 硬规则进自动测试。好不好看靠截图和人工验收。无窗口模式截不到真实画面；截图若要抢前台，就停在 `manual_visual_qa_pending`，不要用 Computer Use。
5. 九宫格贴图、换皮、新 skill、把规则推广到其他界面，都还没获准。

用户明确否决过的理解：不是要把现有 C# 界面全部改成手写 `.tscn`。场景只能由 `ResourceSaver` 或编辑器 API 产出。

## 代码和资源

| 路径 | 职责 |
|---|---|
| `godot/src/Tactics.Godot.Adapter/Runtime/GodotTacticsTheme.cs` | 运行时主题。新增档位和变体；原有按钮、面板、卡片数值没有改 |
| `godot/src/Tactics.Godot.Adapter/Runtime/GodotBattleActiveUnitPanel.cs` | 左上当前行动者面板。运行时用 main 的固定头像和中线布局；场景里已有节点时只绑定 |
| `godot/src/Tactics.Godot.Adapter/Editor/TacticsUiAssetFactory.cs` | 用 `ResourceSaver` 写 Theme 和场景 |
| `godot/src/Tactics.Godot.Adapter/Editor/TacticsUiAssetBuilder.cs` | `godot --headless --script` 入口，调用上面的 `Build()` |
| `godot/content/ui/TacticsTheme.tres` | `Create()` 的落盘副本。运行时没有改成加载它 |
| `godot/content/ui/BattleActiveUnitPanel.tscn` | 场景版面板。未接入 `GodotPlayableRunMain`。文件已存在时生成器不再覆盖 |
| `godot/tests/UiDesignRuleChecker.cs` | 只检查试点面板要用的四条规则 |
| `godot/tests/PlayableRunUiGodotTests.cs` | 主题、场景绑定、规则正例和反例 |

`GodotPlayableRunMain` 仍 `new GodotBattleActiveUnitPanel`，位置 `new Vector2(26, 26)`，大小 `new Vector2(414, 174)`。根节点的位置由 HUD 外壳决定，规则检查不查根节点。同文件里的 `GodotBattleHoverTooltip` 没有改，仍有字号覆盖 15。

窗口合同在 `godot/project.godot`：1600×900，`canvas_items`，`aspect=keep`。不要改。

### Theme 里现在有什么

间距：`SpaceXs` 4、`SpaceSm` 8、`SpaceMd` 16、`SpaceLg` 24、`SpaceXl` 32，数组 `SpacingScale`。

字号：`FontSmall` 14、`FontBody` 18、`FontHeading` 22、`FontTitle` 28，数组 `FontSizeScale`。正文沿用原来的 18，没有改成调研初稿里的 16。

变体：`TacticsHeadingLabel`、`TacticsSmallLabel` 继承 `Label`；`TacticsResourceBar` 继承 `ProgressBar`。资源条用变体，是为了不改变其他界面里基础 `ProgressBar` 的外观。`ResourceBarFill(Color)` 按状态色生成填充，边框是填充色提亮 0.18。

已有条目必须保持不动，除非用户另开范围：`PrimaryButton` 字号 20，`CompactButton` 和 `ActionButton` 字号 15，面板内容边距 20，卡片内容边距 14。这些值不在新档位里。`Label` 基础字号是 18。主题没有给基础 `ProgressBar` 设置 `fill`。

颜色仍是改动前的值：背景 `090d10`，强调色 `f9874b`，正文 `f4f4f2`，次要文字 `b6b8b9`。这和设计指南里的暖金还不是同一套已验证表皮。不要在这次延续里把强调色改成暖金。

### 面板结构

合入 `origin/main` 之后，游戏里这张卡沿用 main 的 `LayoutBlock`：内容区 414×174，头像固定 96×96，名字左对齐，资源行高 34，行距 8。名字和资源列与头像共用一条垂直中线；特殊资源行隐藏时头像尺寸不变。名字挂 `TacticsHeadingLabel` 并显式使用字号 22，资源条挂 `TacticsResourceBar`，填充仍由代码按状态色覆盖。

已经生成的 `BattleActiveUnitPanel.tscn` 仍是更早的容器版（边距 8、头像最小宽度 108、行高 32）。运行时不加载它。`_Ready` 发现场景里已有 `ActiveUnitPortrait` 时只按名字绑定，不再创建第二套节点，也不对容器里的节点调用 `LayoutBlock`。

`BuildLayout()` 现在写出的是运行时那套节点。生成器只在场景文件不存在时调用它，所以现有场景文件不会被覆盖。

### 生成器

`TacticsUiAssetFactory.Build()` 每次重写 `res://content/ui/TacticsTheme.tres`，并固定 `StyleBox` 的 `ResourceSceneUniqueId` 为 `{类型}_{条目}`，所以 Theme 可以重复生成且逐字节相同。场景只在 `res://content/ui/BattleActiveUnitPanel.tscn` 不存在时写入。场景里的占位文字和资源条数值只为了编辑器预览，运行时 `Bind` 会覆盖。

Headless 入口：

```powershell
& D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe --headless --path godot --rendering-method gl_compatibility --script res://src/Tactics.Godot.Adapter/Editor/TacticsUiAssetBuilder.cs
```

生成前要先编过 `godot/Tactics.Godot.Adapter.csproj`。不要在编辑器开着并锁住程序集时编。

### 规则检查

`UiDesignRuleChecker.Check(Control root, Color background)` 遍历后代，不查根节点：

- `Label` 和 `Button` 的 `font_size` 必须在 `FontSizeScale`。
- `BoxContainer.separation`、网格的 `h_separation` / `v_separation`、`MarginContainer` 四边边距，非 0 时必须在 `SpacingScale`。
- 父节点不是 `Container` 时，控件自己必须是四边锚点 0/0/1/1 且偏移全 0。
- `Label` 的 `font_color` 对传入的 `background` 至少 4.5:1。

控件不在场景树里时，Godot 不把 Theme 传下去，字号会读到默认 16。测试必须先 `AddChild` 到 `((SceneTree)Engine.GetMainLoop()).Root`。

反例测试造一个字号 17、绝对坐标的 `Label`，期望两条违规；再把字色改成 `45484a`，期望第三条对比度违规。

这条检查目前只被试点面板的测试调用。不要把它接到所有界面上，除非用户扩大范围。那样会先打红现有 HUD。

### 已通过和未通过的验证

2026-11-10，`PlayableRunUiGodotTests` 里主题、面板绑定、场景版、规则正例和反例共 10 项定向测试通过。过滤条件是套件名再加 `ActiveUnitPanel|TacticsTheme|CorruptionProgress|UiDesignRule`。运行前设置 `GODOT_BIN` 为上面的 Godot 控制台可执行文件，用 `godot/tests/Tactics.Godot.TestHost.csproj` 和仓库根的 `Tactics.Godot.runsettings`。测试宿主和 Godot 工程不要并行 `dotnet build`。

`Tools/godot/Verify-GodotProject.ps1` 在那次收口时被中断，没有结果。不要写成整仓已通过。产品代码准备收口时可以跑一次，文档和账本改动不要为此重跑。不要自己触发 GitHub Actions。

没有前后对比截图。编辑器在那次任务开始时是关的，结束时按生命周期规则没有新开。

## 必须遵守的设计约束

摘自设计指南，方便执行；全文仍以该文档为准。

目标是克制、服务功能和少量可爱暗黑手绘。UI 帮助玩家看状态、做决定、回到游戏。

功能宪法：HUD 让位给画面；一个界面一件事；高频功能位置稳定；细节渐进披露；状态不只靠颜色；装饰只在边框、标题、角标和空白；全屏不同时多个强发光；动效解释状态，禁止无功能呼吸边框；系统页是否留世界由任务决定；纹理不得穿过文字和图标。

四角：左上是当前 Actor、HP/MP、特殊资源和状态；右上是回合、先攻、目标和关卡目标；左下是详情和低频操作；右下是当前动作、确认和结束回合。屏幕中心保持干净。这次试点只动了左上这一张卡，不要借机重排另外三角。

语义色：生命和危险用红，魔力用蓝或青，恢复用绿，焦点和确认用暖金。运行时强调色目前仍是 `f9874b`，还不是这条暖金。交互状态要有 Normal、Hover、Focus、Selected、Pressed、Disabled、Dangerous、New/Changed。Disabled 要能说明原因。

参考职责只取《轩辕剑 3》的小确认框、当前值到新值、深色半透明、少量暖金，以及《暗黑 2 重制版》的位置稳定、Chrome 与内容分离、清楚网格。不取卷轴书法、暗到难读、只靠颜色选中、写实石材和厚框。仓库里不要复制那些参考截图。

## 外部资料和取舍

浅克隆在仓库外 `D:\codes\_refs\ui-skills\`，不进本仓库。版本是 2026-11 的浅读：

- `gamedev-skills/awesome-gamedev-agent-skills` `19d93f9`，Apache-2.0。用了 `skills/disciplines/game-ui-ux` 和 `skills/godot/godot-ui-control`。留下锚点加容器、参考分辨率、安全区、默认焦点、界面栈、事件刷新 HUD。
- `sebastian-software/effective-ui-design-skill` `bf4d641`，MIT。留下 8 点间距、4.5:1 和 3:1、一个主按钮、三档按钮、最多两种字重。丢掉网页网格，以及「不要禁用按钮」。
- `anthropics/skills` 的 `skills/frontend-design` `2c5958e`，Apache-2.0。留下少假标签、动效只回应操作、一个动作全程同一个名字。
- `plugin87/ux-ui-agent-skills` `def84a7`，MIT。Windows 长路径导致完整检出失败，只恢复了 `taste/` 和 `reference/`。留下外层间距大于内层、一个强调色、阴影克制、五态齐全。丢掉「相邻区块必须换布局」。
- `Donchitos/Claude-Code-Game-Studios` `05ad7c8`，MIT。看了 `.claude/docs/templates/guidance/hud-design-guide.md`。留下分区和 PC 约 3% 安全边。丢掉整套多人流程。

这五个仓库都没有九宫格。下面 7 条是按 Godot `StyleBoxTexture` 补的，还没有资源，也还没有测试：

1. `texture_margin_*` 等于源图边框像素，四边一致优先。
2. 中间默认拉伸；只有能整除的纹样边才平铺。
3. `content_margin_*` = 边框宽度 + 一档间距，文字不压边。
4. 整数倍缩放并对齐像素。
5. 同一层级一套源图；换状态不改边距。
6. 最小尺寸至少两倍边框加内容。
7. 装饰只在边和角，中间安静。

十条自查以调研笔记为准。点击区域 40×40 是 48pt 按 900p 折算的，还没在真实界面上目测。

## 已知的坑

生命条填充 `b95f66` 上的正文对比度约 3.90:1，魔力条 `527cc7` 约 3.76:1，都低于 4.5:1。腐化三档 `65576f`、`8444a3`、`c33772` 分别约 6.06、5.71、4.64，达标。检查器当时对着 `PanelBackground` 算，所以测试是绿的。下一个改动如果要修对比度，改的是填充色或字色，并让检查器对着文字真正压住的底算。这是配色决定，没有用户确认不要改。

魔剑士附身时特殊资源行会在 `_Process` 里做脉冲。这是已有玩法反馈，不是这次要删的「无功能呼吸边框」。不要顺手删掉。

## 还不能擅自决定

- 新界面默认用 C# 还是场景。场景版要用户在编辑器里打开 `res://content/ui/BattleActiveUnitPanel.tscn` 之后再选。
- 是否把本文和调研收成 `.agents/skills/godot-ui-design-constraints/`。
- 是否把 `UiDesignRuleChecker` 用到其他界面，以及是否把按钮字号 20/15 和面板边距 20/14 收进档位。
- 是否把运行时改成加载 `TacticsTheme.tres`。
- 是否做九宫格贴图、改强调色、改生命和魔力条颜色。
- 是否重排 HUD 四角或修改 `GodotBattleHoverTooltip`。

继续开发时，下一项默认只做用户新点名的界面，并复用 Theme 档位。游戏内这张卡保持 main 的固定布局，不要再改回容器排版。扩大范围前先问。

## 人工验收

视觉验收还没做。main 已经把人工账本收成一条 `MQA-GODOT-SINGLE-RUN`，这次没有把旧的 `MQA-GODOT-UI-RULE-PILOT` 再插回去，也没有把它写成通过。用户要在 disposable battle 里看左上面板，包括玩家回合、敌方回合和魔剑士腐化，并在 1280×720 与 1920×1080 各看一次；再打开场景和 Theme 资源，试一次拖动或改间距后撤销。不要把定向测试写成视觉通过。

相关但不是这次改出来的旧项 `MQA-GODOT-DEMONBOUND-HUD` 仍 pending。改了左上卡的布局之后，那一项的「可读、不挡棋盘」需要用户重新看，但不要把它改成 passed，也不要在没有新的用户结论时改它的历史记录。
