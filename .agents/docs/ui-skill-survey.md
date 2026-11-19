---
title: UI Skill Survey (KISS)
status: survey-pending-decision
---

# UI Skill 调研（KISS 版）

目标只有一个：先做出**简单、干净、不扣分**的九宫格 UI。本文只摘要外部 skill 的可执行约束，不复制原文；方向与功能宪法仍以 [pure-run-ui-design-guide.md](pure-run-ui-design-guide.md) 为准，本文只补“具体数值与自查”这一层。

## 来源

参考仓库浅克隆在仓库外 `D:\codes\_refs\ui-skills\`，不进入本仓库。

| 仓库 | commit | 许可证 | 看了什么 |
|---|---|---|---|
| gamedev-skills/awesome-gamedev-agent-skills | 19d93f9 | Apache-2.0 | `disciplines/game-ui-ux`、`godot/godot-ui-control` |
| sebastian-software/effective-ui-design-skill | bf4d641 | MIT | `SKILL.md`、`references/04-layout-spacing`、`07-buttons` |
| anthropics/skills | 2c5958e | 见 `skills/frontend-design/LICENSE.txt` | `frontend-design` |
| plugin87/ux-ui-agent-skills | def84a7 | MIT | `taste/design-taste.md`（Windows 长路径导致完整检出失败，只恢复了 `taste/`、`reference/`） |
| Donchitos/Claude-Code-Game-Studios | 05ad7c8 | MIT | `docs/templates/guidance/hud-design-guide.md` |

结论：没有一个仓库讲九宫格（Godot `StyleBoxTexture`）本身；布局和焦点看 gamedev-skills，数值看 effective-ui-design，评审和反面例子看 plugin87 与 anthropics。

## 逐项提炼

### 布局

| 约束 | 来源 | 结论 |
|---|---|---|
| 只用锚点预设 + Container，不写绝对坐标；Container 子节点不设锚点 | gamedev-skills | 采纳 |
| Stretch：`canvas_items` + 固定参考分辨率 | gamedev-skills | 已满足：项目为 1600x900、`canvas_items`、`keep`；是否改 `expand` 另议 |
| 安全区：PC 全屏约 3% 边距，最低检查 1280x720 不拥挤 | Game Studios | 精简后采纳（只做 PC） |
| 外层间距 > 面板内边距 > 元素间距，不能反过来 | plugin87 | 采纳 |
| 相关元素靠近、无关元素拉开；能用留白分组就不再套框 | effective-ui | 采纳 |
| 不嵌套面板（框里套框） | effective-ui | 采纳，对九宫格尤其重要 |
| 12 栏网格、容器查询、subgrid | effective-ui | 舍弃（网页专属） |
| Variance Mandate（每段布局都要变化） | plugin87 | 舍弃（营销页思路，游戏 UI 要的是位置稳定） |

### 视觉 token（建议放进一个基础 Theme）

| 类别 | 建议值 | 来源 |
|---|---|---|
| 间距档位 | 4 / 8 / 16 / 24 / 32（只用这几档） | effective-ui 的 8 点网格，加 4 给图标与文字间距 |
| 字号档位 | 小字 14，正文 18，小标题 22，标题 28（900p 参考；已落在 `GodotTacticsTheme.FontSizeScale`） | gamedev-skills 引 Xbox 无障碍指南：PC 1080p 下限 18px，按 900/1080 折算约 15；正文沿用 Theme 现有的 18 |
| 字重 | 最多 2 种 | effective-ui |
| 颜色角色 | 背景、面板、边框、正文、次要文字、强调（暖金）+ 语义色 | effective-ui 调色板结构；语义色沿用现有指南 |
| 对比度 | 文字至少 4.5:1，边框与图标至少 3:1 | effective-ui |
| 纯黑纯白 | 不用 #000/#FFF，改用略偏暖的近黑与近白 | effective-ui、plugin87 |
| 强调色数量 | 一个主强调色（暖金），不加第二个 | plugin87 |
| 圆角/阴影 | 全局一种圆角语言；阴影克制，大多数元素平面 | plugin87、anthropics |

### 九宫格（外部无覆盖，按 Godot 概念自定）

1. 所有面板和按钮用 `StyleBoxTexture`，`texture_margin_*` 等于源图边框的真实像素宽度，四边一致优先。
2. 中间区域默认拉伸（`axis_stretch` = Stretch）；只有带纹样的边才用 Tile 或 TileFit，且纹样要能整除。
3. `content_margin_*` = 边框宽度 + 一个间距档位（例如边框 6 + 8 = 14），文字永不压边框。
4. 源图按整数倍缩放，开启像素对齐，避免边框在不同尺寸下出现半像素糊边。
5. 同一层级只用一套九宫格源图；状态切换（悬停/按下/禁用/聚焦）只改颜色调制或换同尺寸变体，边距不变，切换时内容不跳动。
6. 面板最小尺寸至少是两倍边框宽度加内容，避免角块重叠。
7. 装饰只放在边框与角块上，中心区保持安静纯色（对应现有指南“装饰只占 Chrome”）。

### 控件状态

| 约束 | 来源 | 结论 |
|---|---|---|
| Normal / Hover / Pressed / Disabled / Focus 五态都要有，且同一类控件长得一样 | plugin87、effective-ui | 采纳，具体表现沿用现有指南 |
| 每个界面打开时默认聚焦一个控件；`focus_mode` 设为 All | gamedev-skills | 采纳 |
| 一屏只有一个主按钮；按钮分主/次/弱三档 | effective-ui | 采纳 |
| 按钮文案用“动词+对象” | effective-ui、anthropics | 采纳 |
| 点击区域至少 40x40（900p 参考，由 48pt 折算） | effective-ui | 采纳 |
| 不用禁用按钮、改为提交时校验 | effective-ui | 舍弃（游戏里禁用技能很常见，改为“禁用要悬停说明原因”） |
| 纯装饰的全屏 Control 设 `mouse_filter = Ignore` | godot-ui-control | 采纳 |

### 反馈与动效

| 约束 | 来源 | 结论 |
|---|---|---|
| 只定两档：快 0.1 秒（悬停/按下），常规 0.2 秒（开合）；统一一种缓出曲线 | plugin87 动效思路，精简 | 采纳 |
| 动效只回应玩家操作，不加持续呼吸、飘光 | anthropics、现有指南 | 采纳 |
| HUD 由信号驱动刷新，不在 `_Process` 里轮询 | gamedev-skills | 采纳（也符合 Core/Adapter 分层） |

### 文案

- 不用 emoji 当图标；不加“SECTION 01”之类的假标签；全大写标签少用（anthropics、plugin87）。
- 同一个动作全流程同一个名字（按钮写“装备”，提示也写“已装备”）（anthropics）。

## “不扣分”自查表（10 条）

改或新建一个界面后逐条过一遍，截图对照：

1. 窗口拉到 1280x720、1600x900 和 1920x1080 都不重叠、不裁切，没有绝对坐标。
2. 所有间距都来自 4/8/16/24/32，外层间距大于内层。
3. 没有框里套框；分组优先用留白。
4. 九宫格边框不糊、不拉伸变形，文字不压边框。
5. 字号只用 14/18/22/28 四档，正文 18，字重不超过 2 种。
6. 文字对比度至少 4.5:1，边框与图标至少 3:1，没有纯黑纯白。
7. 一屏只有一个主按钮，主次按钮一眼可分。
8. 每个可交互控件都有五种状态，切换时内容不跳动。
9. 打开界面就有默认焦点，键盘能走完全部控件。
10. 状态不只靠颜色表达；动效只有两档时长且只回应操作。

## 试点结果：当前单位面板

试点对象是战斗左上角的当前行动者状态卡（`GodotBattleActiveUnitPanel`）。2026-11-18 合入 `origin/main` 之后，以代码为准，下面这节替换先前“已改成容器、没有绝对坐标、检查全绿”的说法。

已落地：

- `GodotTacticsTheme` 增加间距档位 4/8/16/24/32、字号档位 14/18/22/28、标题与小字 Label 变体、资源条变体。已有的按钮和面板条目没有改动（按钮仍有 20 和 15 两种不在档位内的字号，面板内容边距 20、卡片 14）。
- 游戏内这张卡保持 `main` 的 `LayoutBlock`：内容区 414×174，头像固定 96×96，名字左对齐并使用字号 22，资源行高 34，行距 8。名字与资源列和头像共用一条垂直中线；特殊资源行隐藏时头像尺寸不变。名字挂 `TacticsHeadingLabel`，资源条挂 `TacticsResourceBar`，填充仍按状态色在代码里覆盖。
- `TacticsUiAssetFactory` 生成 `godot/content/ui/TacticsTheme.tres`（只供编辑器预览，运行时仍用 `Create()`）。`godot/content/ui/BattleActiveUnitPanel.tscn` 仍是更早的容器版，运行时不加载；文件已存在时生成器不覆盖。
- `UiDesignRuleChecker` 仍查四项。运行时这张卡的测试现在要求：字号落在档位内，同时允许报出绝对坐标，并且头像在腐化行显示和隐藏后保持同一个正方形。场景版测试仍要求检查结果为空，对比度仍拿 `PanelBackground` 去比。

试点暴露的问题：

- 对比度检查对着面板底色，数值文字实际压在资源条填充上。正文 `f4f4f2` 对生命填充 `b95f66` 约 3.90:1，对魔力填充 `527cc7` 约 3.76:1，低于 4.5:1。腐化三档 `65576f` / `8444a3` / `c33772` 约 6.06 / 5.71 / 4.64，达标。填充色没有改。
- 控件不在场景树里时 Theme 不会向下传递，检查器会读到引擎默认字号 16。检查前必须先把界面挂进场景树。
- 没有渲染截图。视觉整齐度仍待人看。人工账本当前只有 `MQA-GODOT-SINGLE-RUN`，这次审查没有另开一条，也没有写成通过。

## 2026-11-18 玩家界面审查

只读代码，没有开编辑器，没有截图，没有改运行时。范围是一局里玩家会看到的界面：营地和选人、战斗 HUD、地图、结算、背包、暂停。调试夹具和作弊控制台不在内。尺子是上面的自查表。好不好看不能用这把尺子判完；没有渲染图的句子都标成待看。

已经落在档位里、这次不记成问题的例子：大量正文 18，单位卡名字、营地预览徽章、背包标题、裁决者标题用 22，背包列间距 8，页眉行间距 24，加点行间距 16。主按钮最小 300×56，结束回合 242×72，紧凑按钮 118×44，背包条目高 44，都达到约 40×40 的点击下限。主题按钮有 Normal / Hover / Pressed / Focus / Disabled 五套样式。战斗里不可用的技能带有失败原因提示。

### 尺寸和间距

| 现在的数值 | 文件 | 违反 | 建议 |
|---|---|---|---|
| 标题 42、34、36、30、26、24、20；小字 17、16、15、13 | `GodotPlayableRunMain.cs`：主菜单 `TACTICS` 42，页眉 34，暂停 `PAUSED` 36，终局摘要 30，结算奖励和休息治疗 26，升级标题与选项结果 24，加点步骤和属性 20，背包说明 17，副标题和状态 16，格点调试、主属性标记、技能卡标题、队伍条 15，页眉 Run 信息和技能卡说明 13 | 字号只用 14/18/22/28 | 以后可改。先收 13/15/16/17 到 14 或 18，标题收到 22 或 28。这一轮不动 |
| 先攻回合 16，省略号 24；槽位 34，头像 28，间距 10 和 6 | `GodotInitiativeStrip.cs` | 字号档位；间距只用 4/8/16/24/32 | 以后可改。槽位 34 同时也小于约 40×40 的可悬停目标 |
| 飘字 24，默认伤害用纯白 | `GodotDamageNumberLayer.cs` | 字号档位；不用纯白 | 以后可改到 22，并换成近白。压在战场上的实际对比度待看 |
| 单位卡行高 34；头顶血蓝条 44×18，条高 8，字号 7，纯白 | `GodotBattleActiveUnitPanel.cs`；`GodotCompactUnitMeter.cs` 与 `UnitMeterSize` | 行高不在间距档；血条字号和纯白 | 单位卡行高以后可改到 32。头顶条是玩家能看见的诊断条，字号 7 压在亮绿色填充上，按规则会低于 4.5:1，精确比值待看。这一轮不改色 |
| 战后总览链接 240×34 | `GodotPlayableRunMain.PostBattle.cs` 的 `PostBattleOverviewButtonRect` | 点击高度低于约 40 | 以后可改。字号 18 已在档位内 |
| 背包卡片内边距 12；技能卡文字间距 2，标题 15、说明 13 | `InventoryColumn`、`SkillCard` | 间距和字号档位 | 以后可改。技能卡 340×112 的点击面本身够大 |
| 主题：主按钮字号 20，紧凑和行动按钮 15，圆角 8/6，面板内容边距 20，卡片 14，按钮内容边距 12 | `GodotTacticsTheme.cs` | 不在新档位内 | 先不动。这是先前明确留下的旧条目 |
| 战斗 HUD 单位卡在 (26, 26)，结束回合底边距约 27 | `GodotPlayableRunMain.cs` 约 1006 与 1020 行 | PC 安全区约 3%（1600×900 上约 48） | 先不动。现有四角位置不在这次重排范围内 |

### 布局、主按钮和焦点

| 现在的做法 | 文件 | 违反 | 建议 |
|---|---|---|---|
| 单位卡用绝对坐标把名字、资源列和头像对齐到同一条中线 | `GodotBattleActiveUnitPanel.LayoutBlock` | 自查第 1 条要求容器或全矩形锚点 | 先不动。这是 `main` 的布局合同，用来保证头像不随腐化行变高变矮。规则和这条合同冲突，检查器报绝对坐标是预期结果 |
| 营地、地图、结算、背包、暂停大多用 `PanelAt` / `LabelAt` 写死位置 | `GodotPlayableRunMain.cs` | 同上 | 先不动。整页都是这样搭的，一次改会带动现有测试里的位置 |
| 主菜单 New Run / Continue / Options / Quit，暂停 CONTINUE / OPTIONS / ABANDON RUN / SAVE AND QUIT，地图 Complete Progression / Inventory / Return，都用 `Button()`，也就是 `TacticsPrimaryButton` | `ShowHome`、暂停菜单、`ShowRunMap` | 一屏只有一个主按钮 | 以后可改：每页留一个主动作，其余降成次按钮。放弃本局应走危险态，而不是和继续长得一样 |
| 战斗里只有结束回合是主按钮（242×72）。移动和技能是 `TacticsActionButton`（168×54），描边仍是同一强调色；这些按钮 `FocusMode = None` | `GodotPlayableRunMain.cs` 约 1020、1085–1099 行；`ActionButton` | 主次不易分开；键盘走不到战斗动作 | 以后可改。禁用原因已经写在 Tooltip 上，这点符合“禁用要说明原因” |
| 加点技能卡放在已经有框的 `ProgressionFlowPanel` 里；背包每一列自己也是卡片 | `ShowProgression`、`InventoryColumn` | 框里套框 | 以后可改。分组可以改成留白，这一轮不拆框 |

### 对比度

生命条和魔力条的比值见上一节，建议先不动，等确认要不要改填充色。检查器若继续拿面板底色比较，场景版测试会保持通过，这是误报。战斗飘字和头顶血条的实机对比度待看。

### 观感缺口（规则量不出“好看”）

这些会让界面显得素，规则里没有一条改完就会变好看：

- 面板和按钮是 `StyleBoxFlat` 色块。九宫格七条还没有源图，也没有接到运行时。
- 强调色仍是 `f9874b`。设计指南里的暖金细边还没原型验证，不能拿来换皮。
- 字体仍是测试占位字体。不新增字体文件。
- 先攻头像被关掉（`GodotInitiativeStrip.ShowPortraits = false`），玩家看到的是 34px 色点。
- 主菜单四个按钮同一尺寸、同一变体，标题 42 又超出档位。尺寸上的“笨重”主要来自这里和结算、地图上同样的 300–360px 宽主按钮，不是来自单位卡的 34px 行高这一处。

没有截图，所以上面的观感只说明当时的代码长什么样。这张表是过期快照。随后的调整按已有原则收了字号、一屏一个主按钮，并让路线图的字跟着缩放；没有换色、没有加字体、没有重排四角。整齐度仍要人看。

## 路线图原则候选

这些从路线图的画法里看出来，进游戏核对之前不是定论，也不写进 skill。

- 会缩放的图，文字和节点要一起缩放。字号固定、节点间距跟着缩放，缩小时字会叠在一起。
- 节点种类和状态要能靠形状或文字认出来。只换颜色，或多种类共用一个符号，玩家分不清。
- 地图的层距和车道距是玩法几何，不是界面间距档位。

## 待决定

- 新界面默认用 C# 搭建还是场景文件：需要用户在编辑器里打开场景版、在游戏里看 C# 版之后选定。
- 已有原则写在 `.agents/skills/godot-ui-design-constraints/`。路线图那几条还要进游戏看过，才能决定收不收进 skill。
- 对比度检查是否改成按文字实际所在的底色计算，以及是否调整生命和魔力条的填充色。
- 是否把规则检查推广到其他界面，并规整 Theme 里已有的按钮字号和内边距。
- 点击区域 40x40 是按 1600x900 折算的，需在真实界面上目测确认。
