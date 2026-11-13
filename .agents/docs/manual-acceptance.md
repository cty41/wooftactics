# 人工验收账本

当前跨项目人工验收状态。稳定 ID 是权威身份，列表编号只是临时顺序。

## Pending

### MQA-GODOT-EXPLORATION-WALK-ATLAS-LAYOUT — 探索走位手感、连线观感与页面排版

- Status: `pending`
- Source: 探索页交互与布局修复计划
- Action: 用隔离存档在探索页走一段远路，看战后地图的出口连线，再打开背包并缩放窗口。
- Expected: 走路速度和朝向看着自然，不发飘；单出口连线看着是直的，多出口扇形不别扭；页头和背包三列不挤、不溢出。点击是否生效、出口是否对称、控件是否越界已由测试断言，不用再看。
- Observe: 探索页、战后页、背包页。
- Preserve on failure: 行走前后截图或短视频、窗口大小、Run seed。
- Save boundary: 移动会写当前 Run；使用隔离存档。
- Automated evidence: `ResidentExplorationPages`、`PlayableRunUi`、`AdventureMapTileMap` 已断言走位提交、点击忽略、出口对称、连线不相交和控件边界。不再要人看这些事实。
- User verdict: none.

### MQA-GODOT-MAW-BAT-SLICE — 大嘴蝠悬浮、浅水观感与 N2 难度

- Status: `pending`
- Source: 大嘴蝠纵切与 N2 浅水布局
- Action: 用隔离存档打至少三局不同 seed 的 N2，看大嘴蝠待机、移动、咬击、受击、死亡，并悬停浅水。
- Expected: 悬浮稳定且压在地面单位上方；咬击和吸血看着连得上；死亡先停悬浮再落尸。浅水是蓝绿色波纹，悬停能认出“浅水”。三局不卡死，并记下难度相对以前是否明显变了。移动力和目标选择规则已由测试断言。
- Observe: N2 棋盘、单位身体和影子、悬停提示。
- Preserve on failure: 短视频或连续截图、Run seed、双方格子。
- Save boundary: 战斗会改当前 Run；使用隔离存档。
- Automated evidence: Core 与 Application 已断言移动消耗、飞行穿越、吸血顺序和可击杀排序。不再要人看这些规则。
- User verdict: none.

### MQA-GODOT-MAW-BAT-POSES-DEATH — 蝙蝠姿态与死亡落点观感

- Status: `pending`
- Source: 蝙蝠死亡位置修复
- Action: 在战斗或单位图库里看蝙蝠存活、咬击、受击、死亡，并在东西朝向各看一次；击杀悬浮中的蝙蝠看落点。
- Expected: 各姿态不跳、不错位；东西翻转后仍一致；尸体落在格子接地点，不偏低、不悬空、不穿出格子。
- Observe: 单位身体、影子与格子的对齐。
- Preserve on failure: 朝向、击杀时机、短视频或连续截图。
- Save boundary: 战斗会改当前 Run；使用隔离存档。
- Automated evidence: `UnitBatchGodotTests` 已断言死亡偏移和下落终点。目视是否自然仍要人看。
- User verdict: none.

### MQA-GODOT-UNIT-VISUAL — 单位图库与出生夹具的画面密度

- Status: `pending`
- Source: 大嘴蝠加入后的 13 单位图库布局
- Reopen reason: 图库从 4×3 改为 5×3，出生夹具增加第 13 个位置。
- Action: 打开单位图库和出生夹具，切换四方向和死亡模式，重点看大嘴蝠和最外圈。
- Expected: 13 个单位和标签在 1600×900 里不重叠、不裁切、比例不跳；旧单位的染色和死亡样子保持原基线。格子唯一性和贴图是否装上已由测试断言。
- Observe: 单位图库、出生夹具、画布边缘。
- Preserve on failure: 夹具截图、方向和死亡模式。
- Save boundary: 只读预览，不改 Run 或正式资源。
- Automated evidence: 13 个出生格、资源类型、贴图和边界已自动检查。视觉密度和标签好不好认仍要人看。
- User verdict: 原第 4 阶段基线曾通过；这次因布局变化重新待验收。

### MQA-GODOT-AGENT-FIRST-EDITOR — 编辑器与 Godot AI 面板的真实启动

- Status: `pending`
- Source: 多工作区 Godot 开发启动器
- Action: 在一个新工作区用 `Tools/godot/Open-GodotDev.ps1` 打开编辑器；若提示 `CODEX_RESTART_REQUIRED`，重启一次 Codex 再开。另一个工作区的编辑器开着时再重复一次。
- Expected: 不用手敲 `dotnet build` 就打开正确项目；Godot AI 面板看得见，而且只连到这个工作区；另一个工作区的编辑器还在、没有被换掉。程序集身份和同工作区互斥已由测试断言。
- Observe: 编辑器标题、插件列表、Godot AI 面板。
- Preserve on failure: 启动输出、两个工作区路径、面板截图、第一条 Godot 错误。
- Save boundary: 使用工作区隔离的用户数据。
- Automated evidence: 启动器策略、程序集身份、发布排除和同工作区互斥已自动检查。面板是否看得见、两个窗口是否各走各的，仍要人看。
- User verdict: none.

### MQA-GODOT-DEMONBOUND-ACTION-ART — 魔剑士动作是否像正式素材

- Status: `pending`
- Source: 魔剑士待机、近战、施法、受击和死亡素材接入
- Action: 在一场战斗里让魔剑士转向、近战、施法、受击，然后击倒它。两个朝向都看。
- Expected: 用的是魔剑士自己的图，不是猎人占位；动作结束后回到正确待机；施法和受击时剑的前后关系还在；死亡居中，没有突然镜像或比例跳变。贴图是否绑上已由测试断言。
- Observe: 身体轮廓、剑和手的遮挡、脚、尸体位置。
- Preserve on failure: 截图、朝向、动作，以及第一张不对的图。
- Save boundary: 使用隔离 Run。
- Automated evidence: `PureRunDemonbound.tres` 已绑定九张魔剑士贴图，且与批准来源的哈希一致。动作过渡好不好看仍要人看。
- User verdict: 接入后的运行时画面尚未人工确认。

### MQA-GODOT-DEMONBOUND-HUD — 当前行动者卡片、腐化条和悬停是否好读

- Status: `pending`
- Source: 魔剑士当前行动者状态卡
- Action: 用隔离战斗看一回合我方、一回合敌方、腐化在低中高三段以及附身时的卡片；把鼠标移到屏幕四边的单位、空格和技能目标上。
- Expected: 卡片只跟着当前行动者，字和头像看得清；腐化条三种风险色分得开，附身脉冲突出但不晃眼；悬停说明跟着指针、不出画面、不挡住点棋盘。跟谁、数值、钳制在画面内已由测试断言。
- Observe: 左上战斗单位面板、腐化条、指针旁说明。
- Preserve on failure: 截图或短视频、分辨率、腐化值、悬停位置。
- Save boundary: 使用隔离 Run；测附身前先留战斗检查点。
- Automated evidence: 界面测试已断言当前行动者、数值绑定、腐化分段、附身状态、提示不超出画面且不挡输入。层次、裁切和颜色好不好认仍要人看。
- User verdict: none.

### MQA-GODOT-DEMONBOUND-BANE — 祸源紫月刃的形状和时机

- Status: `pending`
- Source: 魔剑士主动技能祸源重做
- Action: 用隔离战斗朝四个方向放一次祸源，其中一次穿墙、一次只有远格有敌人、一次两格都有敌人；再在二倍或四倍速以及附身时各看一次。
- Expected: 紫色月刃看着正好飞过两格，先打近格再打远格，结束后没有残留刀光；变速和附身时轨迹仍然看得清。能否穿墙、伤害和两段结算已由测试断言。
- Observe: 棋盘上的月刃、命中数字、状态层。
- Preserve on failure: 短视频、方向、墙的位置、速度、是否附身。
- Save boundary: 使用隔离战斗；测附身前保留战前检查点。
- Automated evidence: Core 与 Godot 测试已断言两格合法性、穿墙、由近到远的独立结算和特效队列会清掉。月刃形状和时机手感仍要人看。
- User verdict: none.

### MQA-GODOT-DEMONBOUND-RUN — 带魔剑士打完整局的平衡手感

- Status: `pending`
- Source: 魔剑士连续循环与固定种子检查点
- Action: 用三局隔离 Run，魔剑士分别搭配法师加亚马逊、法师加死灵、亚马逊加死灵，尽量打完，记下腐化峰值、冥想次数、第一次附身和有没有过强或没用的技能。
- Expected: 四人里选三人和起始技能让人明白；魅力成长和技能链都能用；腐化是故意的风险而不是误操作；冥想选项看得懂；附身不会让人以为换了阵营，也不会把结算卡住。合法性已由测试断言。
- Observe: 开局选人、成长卡、战斗里的腐化、结算和终局摘要。
- Preserve on failure: Run seed、队伍、成长选择、腐化过程、短视频。
- Save boundary: 每局都会写完一个 Run；使用隔离存档。
- Automated evidence: Core、Application 和玩法规格已断言确定性规则和状态转换。固定种子套件只是平衡信号，整局手感仍要人看。
- User verdict: none.

### MQA-GODOT-DEMONBOUND-POSSESSION — 附身能不能一眼认出来

- Status: `pending`
- Source: 魔剑士附身形态与永久死亡表现
- Execution checklist: [`demonbound-possession-manual-checklist.md`](demonbound-possession-manual-checklist.md)
- Action: 把魔剑士推到 10 点腐化，看附身形态和它选谁打；若方便，再看到一次普通倒地和一次永久死亡后的下一场队伍。
- Expected: 附身立刻能认出来：变色、`POSSESSED` 脉冲突出、血蓝和技能等级变高。目标选择让人看得出不是只打队友。永久死亡在结算里写清楚，下一场名册也看得懂谁没了。数值、目标池和墓碑规则已由测试断言。
- Observe: 单位染色、腐化标签、状态卡、结算和终局摘要。
- Preserve on failure: 种子、格子和血量、事件顺序、截图或短视频。
- Save boundary: 永久死亡会改当前 Run 名册；使用隔离 Run。
- Automated evidence: 附身加成、技能投影、统一目标池、永久死亡只判一次、死者不进下一场但留在摘要，均已由测试断言。看起来像不像、名册清不清楚仍要人看。
- User verdict: none.

### MQA-GODOT-DEMONBOUND-WORKBENCH — 编辑器里改腐化消耗的真实重载

- Status: `pending`
- Source: 魔剑士技能工作台
- Action: 复制一个魔剑士技能到临时资源，改 `executionProfile.corruptionCost` 和 `executionProfile.damageScaling`，在编辑器里验证、应用、重载 C#，再撤销、重做并删掉这份副本。
- Expected: 在真正的编辑器里，重载后两个值还在；撤销和重做手感正常；失败或取消不会留下半份资源。编译和字节级往返已由测试断言。
- Observe: 技能页字段、诊断、Godot 输出。
- Preserve on failure: 源和目标内容 ID、操作、资源副本、输出。
- Save boundary: 不要改正式魔剑士资源；只用临时副本。
- Automated evidence: 编写编译器、资源保存、幂等和语义往返已通过。真正的编辑器重载操作仍要人做一次。
- User verdict: none.

### MQA-GODOT-OWNERSHIP-CONTENT — 三级技能、宝箱和地图的跨页可读性

- Status: `pending`
- Source: 内容所有权收口
- Action: 用隔离 Run 升到一次玩家三级技能，结算一个宝箱，重载后继续在正式地图上走。
- Expected: 三级技能卡和宝箱结果在各页上都看得懂，重载后地图还能对上当前节点。升级合同、宝箱不重复发奖和存档往返已由测试断言。
- Observe: 成长卡、宝箱结果、地图节点、背包。
- Preserve on failure: Run seed、所选技能、宝箱结果、存档副本、截图。
- Save boundary: 会改成长、奖励和当前存档；先留副本。
- Automated evidence: 九个玩家三级技能、骷髅战士三级、宝箱幂等和地图往返已由测试断言。手感和跨页好不好读仍要人看。
- User verdict: none.

### MQA-GODOT-CONTENT-WORKBENCH — 编辑器里拖节点和检查器的手感

- Status: `pending`
- Source: 统一内容工作台
- Reopen reason: 事件和宝箱的导航、图交互在上次部分通过之后又改过。
- Action: 打开 Tactics 工具，只看页签是不是地图、事件、技能与表现。在事件页打开一个事件和一个宝箱，拖节点，把检定在“无”和一个属性之间切换，再改一行宝箱权重。用临时草稿走一遍全部验证、全部应用、撤销、重做，然后预览一次表现并做一次 C# 重载。
- Expected: 图好读，拖动和检查器顺手；自动成功只是藏起失败数据，不是删掉。重载后没有残留预览节点。三个页签、节点种类和依赖环已由测试断言。
- Observe: 顶部页签、图编辑器、右侧检查器、Godot 输出。
- Preserve on failure: 页签名、所选内容 ID、操作、截图、是否重载过。
- Save boundary: 只对临时副本做验证和预览，不覆盖正式内容。
- Automated evidence: 统一验证器曾覆盖编辑器生命周期和编写往返。图好不好读、拖动和检查器手感、真实重载仍要人看。
- User verdict: 部分通过：在真实编辑器里重新编译后，重复的 `AuthoringWorkspaceCoordinator` 输出错误已消失。页面操作和观感仍待验收。

### MQA-GODOT-FORMAL-UI — 整局界面是否是同一套

- Status: `pending`
- Source: Godot 根界面收口
- Action: 用一局隔离 Run 走主页、选项、地图、成长、背包、战斗抬头、结算、暂停和终局摘要。
- Expected: 近黑底、半透明面板、橙色重点和白灰字看着是一套；地图详情、成长卡和背包三列不挤；装饰面板不挡按钮和棋盘。控件边界和鼠标过滤已由测试断言。
- Observe: 各页面的焦点、悬停、按下和禁用状态，以及暂停遮罩。
- Preserve on failure: 带页面名和分辨率的截图，以及被挡住的控件。
- Save boundary: 使用隔离 Run。
- Automated evidence: 主题状态、面板语义、控件边界和鼠标过滤已断言。疏密和手感仍要人看。
- User verdict: 正式主题通过后尚未再给结论。

### MQA-GODOT-DAMAGE-NUMBERS — 治疗、回蓝和落空飘字的观感

- Status: `pending`
- Source: 相机、菜单、伤害和成长事务
- Reopen reason: 毒伤现在有单独的命中提示，不再拖到帧结束。
- Action: 用舒服的速度各看一次落空、治疗和回蓝。普通伤害、毒伤和暴击不用再看。
- Expected: 治疗是绿色 `+N`，回蓝是蓝色 `+N MP`，落空是灰色；都出现在单位头顶，停顿和播放一致，消失后不留残影。颜色节点和清理已由测试断言。
- Observe: 单位头顶、动画时机、界面速度。
- Preserve on failure: 截图或短视频、事件日志、速度和暂停状态。
- Save boundary: 使用隔离 Run。
- Automated evidence: 玩法规格已通过正式主场景断言落空和回蓝的事件、颜色、暂停和清理。好不好认、动得自然不自然仍要人看。
- User verdict: 部分通过：普通伤害、毒伤和金色暴击已看过；落空、治疗和回蓝还没确认。

### MQA-GODOT-RELOAD-OUTPUT — 编辑器里真正重载一次

- Status: `pending`
- Source: 导出依赖图隔离修复
- Action: 在打开的 Godot 编辑器里做一次真正的 C# 程序集重载，继续当前 Run，再做一次战斗操作，看输出。
- Expected: 重载后页面和单位还在，没有卡住的动画、点不了、存档损坏，或输出里的乱码。进程内重启主场景已由测试断言。
- Observe: Godot 输出、当前页面和单位。
- Preserve on failure: 完整输出、页面、存档副本、复现顺序。
- Save boundary: 重载可能从已提交的战斗检查点重开；失败时留下存档。
- Automated evidence: 玩法规格会重启主场景、继续未完成战斗，并确认没有残留表现节点、生产存档不变。真正的编辑器重载仍要人做一次。
- User verdict: 最近一次影响生命周期的改动之后尚未再给结论。

### MQA-GODOT-DEFEAT-FLOW — 团灭过场顺不顺

- Status: `pending`
- Source: 召唤物 AI 与失败流程修复
- Reopen reason: 以前精英战在可见队伍死后会停住；友方召唤物和终局提交已走同一条自动控制。
- Action: 用一场隔离战斗让最后一名我方单位死掉，看失败摘要和回主页。
- Expected: 最后一下死亡看着自然，摘要只出现一次，回主页不卡、不叠两页。是否提交失败、召唤物回合和回主页清理已由测试断言。
- Observe: 战斗阶段、行动顺序、终局摘要、主页。
- Preserve on failure: 保持战斗不关；记下存活单位、当前行动者、暂停状态和输出。
- Save boundary: 当前精英检查点有诊断价值；覆盖前先复制存档。
- Automated evidence: 玩法规格和应用测试已断言有无召唤物、自动回合、一次结算、失败摘要和回主页清理。过场手感仍要人看。
- User verdict: 修复前失败过：第一场精英战可见队伍死后，战斗停住，没有进失败摘要。

### MQA-GODOT-INVENTORY — 背包数值好不好懂

- Status: `pending`
- Source: 背包投影与只从地图进入
- Reopen reason: 背包现在用应用层的基础、加成、合计和派生值，并且只能从地图打开。
- Action: 从地图打开背包，看一件买来的装备，穿上，判断基础值、加成和合计能不能看懂。
- Expected: 不用猜就知道选中的是哪件、影响哪个槽、加减了多少。穿上后进战斗数值是否变了已由测试断言。
- Observe: 背包物品详情、装备槽、角色数值。
- Preserve on failure: 截图、物品实例 ID、穿上前后的数值。
- Save boundary: 购买和穿戴会写存档；先留商店前的副本。
- Automated evidence: 玩法规格已通过正式界面穿戴、进战斗，并对照基础、加成、合计。好不好读、点起来顺不顺仍要人看。
- User verdict: 部分通过：背包操作和装备详情界面可以；战斗里的数值提升当时还没看。

### MQA-GODOT-WINDOWS-RC — 没有编辑器的机器上启动安装包

- Status: `pending`
- Source: `eba98f9b`，GitHub Actions 运行 `31889338418`
- Action: 下载 `tactics-godot-windows-eba98f9b-19`，在没装 Godot 或 Unity 编辑器的 Windows 上启动。
- Expected: 主页面能开，新游戏能进，退出时没有启动或资源错误。包里有没有测试程序集已由持续集成审计。
- Observe: 游戏窗口和进程输出。
- Preserve on failure: 工作流地址或本地构建日志、启动输出。
- Save boundary: 用干净的用户数据目录，不要指向当前生产存档。
- Automated evidence: 运行 `31889338418` 已通过导出、199 个文件审计和可执行文件启动。制品 `9248204605`。没装编辑器的机器上真正点一次仍要人做。
- User verdict: 还差一次干净机器上的下载、启动、新游戏和退出。持续集成不能把这条写成通过。

### MQA-GODOT-AVAILABILITY-TURNS-LOS — 等距画面上的视线好不好认

- Status: `pending`
- Source: 阴影锥视线合同修复
- Reopen reason: 视线从对角线超覆盖改成开放内部的阴影锥，贴角不再算挡住。
- Action: 按当时的截图摆一次法师冰箭，目标是友军贴角后方的敌人；再摆一次有活着的单位真正挡在射线中间。
- Expected: 贴角那次看着能放，真正挡住的那次悬停能指出是谁挡住。合法与否已由测试断言。
- Observe: 目标高亮、悬停说明、放出的冰箭。
- Preserve on failure: 截图、施法者、目标和阻挡者的格子、悬停文字。
- Save boundary: 使用隔离 Run。
- Automated evidence: Core 与 Application 已断言贴角合法、内部穿越非法，以及冰箭可以提交贴角目标。等距画面上认不认得出仍要人看。
- User verdict: 阴影锥合同改完后尚未再给结论。

### MQA-GODOT-TILE-READABILITY — 冒险格子不用调试文字也能分清

- Status: `pending`
- Source: 玩法规格驱动的格子冒险
- Action: 从起始营地走过每种已实现的冒险节点，在支持的窗口大小下看 10×10 等距棋盘。
- Expected: 能走的格、挡住的格、当前位置、角色、互动物件、出口和节点状态变化，不靠调试文字也能分开。坐标和对象状态已由测试断言。
- Observe: 冒险棋盘、格子高亮、角色和物件轮廓。
- Preserve on failure: 带窗口大小的截图、节点 ID、领队格。不要接着进下一节点。
- Save boundary: 只看和悬停不改 Run；进入或结算节点会改。
- Automated evidence: 正式主场景已断言投影往返、棋盘就绪、格子坐标和对象状态。分不分辨得出仍要人看。
- User verdict: 尚未人工确认。

### MQA-GODOT-TILE-INPUT-FEEL — 点格子和换领队的手感

- Status: `pending`
- Source: 玩法规格驱动的格子冒险
- Action: 点格子边缘和有人的格子，让领队走短路和长路，换头像后再用每个领队走一次。
- Expected: 想点的格和人能稳定点中；选中的领队变化明显；站着不动的同伴看起来不会接受移动。路径只提交一次已由测试断言。
- Observe: 指针高亮、路线预览、领队头像、走路。
- Preserve on failure: 短视频或连续截图、点击位置、期望格和实际格。
- Save boundary: 选领队是临时的；提交移动可能改检查点。
- Automated evidence: 正式输入已断言格子和角色目标、换领队检查点以及最终坐标。手感仍要人看。
- User verdict: 尚未人工确认。

### MQA-GODOT-TILE-SCENE-CHANGE — 交互前后现场是否一眼能看出来

- Status: `pending`
- Source: 玩法规格驱动的格子冒险
- Action: 做一次普通宝箱、诅咒宝箱和守卫祭坛，包括进战斗再回到原来的场景。
- Expected: 每个物件交互前后不一样，看得明白；胜利回到同一节点时，箱子或祭坛明显已经解决。只结算一次、战斗上下文正确已由测试断言。
- Observe: 互动物件、场景遮罩、战后棋盘。
- Preserve on failure: 前后截图、节点和物件 ID、存档副本。不要重试同一次交互。
- Save boundary: 交互和战斗结算会改 Run；先留事件前检查点。
- Automated evidence: 交互结算、事件战就绪、遮罩更新和幂等状态已断言。前后好不好认仍要人看。
- User verdict: 尚未人工确认。

### MQA-GODOT-TILE-ROUTE-MISCLICK — 出口会不会一点就误离开

- Status: `pending`
- Source: 当前节点里的直接后继出口
- Reopen reason: 以前的路线总览和两段提交已去掉，现在在当前场景里点出口。
- Action: 在每个有多个出口的节点，点出口边缘、两个出口之间，以及无关的棋盘或背景，再故意进入一个出口。
- Expected: 差点点到或点背景不会离开；看出口标签就能知道要去哪。只显示直接后继、点中后只离开一次已由测试断言。
- Observe: 出口物件和标签、指针高亮、到达的节点。
- Preserve on failure: 截图或短视频、指针位置、源节点和实际到达的节点。不要再点下一次离开。
- Save boundary: 看和误点不应改 Run；点中出口会立刻进入下一节点，先留副本。
- Automated evidence: 正式主场景已断言每个出口只指向直接后继，锁定节点点不了，离开只提交一次。标签好不好认、会不会误点仍要人看。
- User verdict: 尚未人工确认。

### MQA-GODOT-START-FLOW — 真正重启进程后的第一屏

- Status: `pending`
- Source: 恢复后的先营地流程
- Action: 分别用空存档、半截开局、普通节点、未完成战斗，以及一份可丢弃的损坏副本，真正关掉再打开程序。
- Expected: 第一屏就是该去的地方，没有旧主页；损坏档被隔离开，不把正常档一起弄坏。序列化、恢复和固定种子已由测试断言。
- Observe: 第一屏、Run 种子、`user://pure-run` 文件、Godot 输出。
- Preserve on failure: 存档和损坏副本、第一屏截图、第一条输出错误。
- Save boundary: 用隔离副本；不要拿唯一的生产存档做损坏测试。
- Automated evidence: V11 序列化、规范化和会话恢复已由纯 .NET 测试断言。真正的第一帧和进程重启仍要人看。
- User verdict: none.

### MQA-GODOT-MAP-CAMERA — 开局总览好不好读、镜头顺不顺

- Status: `pending`
- Source: 恢复后的地图集稳定
- Action: 在当前模式点候选身体并移动领队；按 `M` 进总览，用右键拖动和方向键浏览，点一次预览，滚轮试一次，再按 `M` 回来，用 `F` 或 Home 对焦。
- Expected: 当前模式能看全起始棋盘；总览仍然看得清，并且能浏览到最远的节点；提示不互相压住。点谁、滚轮无效、`M` 和 `F` 会切换已由测试断言。
- Observe: 起始营地、节点标题、队伍设置、提示、Esc 遮罩。
- Preserve on failure: 短视频、窗口大小、输入顺序、点击位置。
- Save boundary: 镜头和预览是只读的；保留失败现场时不要从出口离开。
- Automated evidence: Godot 测试已断言身体点选、总览限制、滚轮不变、`M`/`F` 状态和标题层级。好不好读、镜头手感仍要人看。
- User verdict: 恢复之后尚未再给结论。

### MQA-GODOT-START-ESC — 营地菜单好不好读，以及真正退出再开

- Status: `pending`
- Source: 恢复后的先营地流程
- Action: 看 0/3 到 3/3 的出口，完成起始技能，打开 Esc。再用隔离存档分别试放弃本局，以及战斗中保存并退出后真正重启。
- Expected: 菜单字看得清，列出的是本局控制而不是旧主页。三人未满不能走、满了只走一次，以及放弃和保存后的恢复，已由测试断言；真正关掉进程再打开仍要人看。
- Observe: 起始出口、技能页、Esc 遮罩、终局摘要、恢复后的战斗入口。
- Preserve on failure: 菜单截图、完整输入顺序、Run 种子、检查点副本。
- Save boundary: 放弃、选技能和退出会改隔离 Run；先留副本。
- Automated evidence: 开局转移、持久化、未完成战斗检查点和退出已有纯 .NET 覆盖。菜单好不好读、真正重启仍要人看。
- User verdict: none.

### MQA-GODOT-TILE-ESCORT-DIFFICULTY — 迷路村民护送清不清晰、难不难

- Status: `pending`
- Source: 玩法规格驱动的格子冒险
- Action: 接受迷路村民护送，走过相关节点，战斗里至少给敌人一次威胁村民的机会。
- Expected: 谁是护送对象、要去哪，都看得清；村民的行动让人明白；威胁明显但还有机会救；死亡和存活的结果在画面上分得开。状态、优先目标和奖励规则已由测试断言。
- Observe: 护送遮罩、村民位置和血量、敌人打谁、奖励。
- Preserve on failure: Run seed、回合顺序、村民血量和格子、敌人目标。
- Save boundary: 接受、战斗和结算会改 Run；用战前检查点重放。
- Automated evidence: 护送状态、受保护村民的 AI、敌人优先目标、胜负和奖励已断言。清不清晰、难不难仍要人看。
- User verdict: 尚未人工确认。

### MQA-GODOT-POET-RUNTIME — 诗人动作、分身和职业手感

- Status: `pending`
- Source: 第五职业诗人与临时分身；接入近战、施法、受击和死亡后重开
- Reopen reason: 战斗表现从待机和死亡占位换成正式动作，以前的暂缓不能覆盖新画面。
- Action: 用三局隔离 Run 选诗人，分别从剑仙、诗仙、酒仙起步。战斗里看南、北、东、西的近战、施法、受击和死亡，并看后退后留下的分身。
- Expected: 五个候选人站位不挤；动作恢复后回到正确待机，脚底不跳、没有多余蓝色；死亡图居中且不镜像；分身是半透明青色，一眼能和本体分开，不跟着播死亡。技能公式和分身优先攻击已由测试断言。
- Observe: 起始营地候选人、棋盘单位、血量和法力、事件日志。
- Preserve on failure: Run seed、起始分支、朝向、错误姿态、短视频。
- Save boundary: 只使用隔离存档。
- Automated evidence: 诗人资源重复生成、四方向动作、死亡偏移、分身字段清理和表现分类已通过。动作好不好看、分身认不认得出、职业手感仍要人看。
- User verdict: 这次画面改动后尚未再给结论。2026-09-17 的暂缓只针对当时的待机占位。

### MQA-GODOT-STALE-SAVE-RESET — 真正关掉进程再打开

- Status: `pending`
- Source: 开发期矛盾存档清档
- Action: 用当前构建在前三层探索、第 4 层商店或事件、第 5 到 6 层各保存并退出一次，真正关掉再打开后点继续。再用一份结算完成但棋盘未标记完成的旧隔离存档启动。
- Expected: 当前构建的存档回到原节点，页面正常；旧半截档回到主页，状态栏是“存档来自旧版本，已清除，请开始新游戏”，继续变灰，新游戏能进。冷启动逻辑已由 `StaleSaveColdResumeGodotTests` 断言。
- Observe: 主页状态栏、继续按钮、恢复后的页面。
- Preserve on failure: 点继续之前先复制存档和备份、节点 ID、Run seed。
- Save boundary: 清档会覆盖当前存档；不要用唯一的生产存档。
- Automated evidence: `StaleSaveColdResumeGodotTests` 已断言当前存档冷启动仍在、旧半截档被清、状态栏提示和继续变灰。真正关闭进程再打开，以及页面观感，仍要人看。
- User verdict: none.

### MQA-GODOT-TILE-RESIDENT-EXPLORATION — 探索页地图和角色看着像战斗里那一套

- Status: `pending`
- Source: 探索页复用驻留地图
- Action: 用隔离 Run 看战后、休息、商店、宝箱和一个事件节点。每页走一走、点物件，再从出口离开。休息页打开背包再返回。普通、精英和首领战斗节点会自动开战，不用在这里看探索页。
- Expected: 每页的地图和三名队员看着就是战斗里那一套，不是文字块；背包按钮不觉得挡出口。正式地图、精灵、站位和按钮不重叠已由测试断言。
- Observe: 探索棋盘、角色精灵、右侧出口、背包按钮。
- Preserve on failure: 含窗口大小的截图、节点 ID、领队格。
- Save boundary: 使用隔离 Run；移动和离开会写存档。
- Automated evidence: `ResidentExplorationPagesGodotTests` 和玩法旅程已断言正式地图、精灵、移动持久化、点击规则和背包不挡出口标记。观感和点击手感仍要人看。
- User verdict: none.

### MQA-GODOT-SETTLEMENT-BATTLE-HUD-POLISH — 加点页、技能卡、战后镜头和战斗抬头好不好看

- Status: `pending`
- Source: 2026-11-05 截图复查
- Action: 用隔离 Run 打完第一场：看加点页和技能卡，进战后地图看默认镜头，点总览再按 `M` 切回；下一场看行动条和左上角色卡，等敌人行动时再看一次。
- Expected: 主属性金色标记不别扭，继续按钮不显得太长；两列技能卡看起来一样大、字不出框；右上每位角色一行、不超框；战后镜头不是突然拉成俯瞰；行动条圆点能分清敌我；左上头像和名字、血条看着对齐。位置、宽度、四张卡同尺寸和镜头缩放已由测试断言。
- Observe: 加点页、技能页、右上面板、战后镜头、行动条、左上角色卡。
- Preserve on failure: 含窗口大小的截图、所在页面、当前回合。
- Save boundary: 使用隔离 Run；加点和选技能会写存档。
- Automated evidence: `PlayableRunUiGodotTests`、`ResidentExplorationPagesGodotTests` 和 `InitiativeStripGodotTests` 已断言主属性位置、按钮宽度、技能卡尺寸、镜头缩放和行动条顺序。观感和齐不齐仍要人看。
- User verdict: none.

## Passed

### MQA-GODOT-PROGRESSION-DOWNED-EXCLUDED — 战斗结束时倒地的角色不升级

- Status: `passed`
- Source: 倒地角色不升级计划
- Action: 用隔离存档让等级最低的角色在战斗结束前倒地并取胜，再打一场无人倒地作对照；第 4 或第 6 层的战斗和事件战也各看一次升级页从哪进来。
- Expected: 升级页显示仍站着的人里等级最低的一名；倒地者被救起但等级不变。事件战先看到结果，再从继续或出口进入升级页。
- Observe: 升级页、队伍等级和血量。
- Preserve on failure: Run seed、战斗结束时的血量和等级、升级页截图。
- Save boundary: 使用隔离存档。
- Automated evidence: 结算测试和升级页测试已断言倒地者不入选、无人倒地选队首、第 6 层同一路径，以及事件战的继续和出口会打开升级页。未计入：没有在实战里打倒角色；第 4 层只证明能开战。
- User verdict: 2026-11-13 按收窄后的规则，用自动证据收口。依据是测试断言的页面文字、存档字段和导航，不是用户看过截图。

### MQA-ARTWORK-DEMONBOUND-DEATH — 魔剑士圆角死亡图的身份和尺寸

- Status: `passed`
- Source: 魔剑士死亡第四轮画面复查
- Action: 把魔剑士待机、上一版死亡、已批准的猎人死亡和第四轮圆角死亡放在同一 128 像素比例下比较。
- Expected: 第四轮仍是魔剑士，有叉形眼、脱手的剑和完整的圆形尸体；居中的 56×48 预览相对已批准参考没有明显比例问题。
- Observe: `Tools/artworks/doge/reviews/doge_capsule_demonbound_death_round_size_compare_v05.png`。
- Preserve on failure: 对比图、所选候选，以及看到的尺寸或身份差异。
- Save boundary: 只审图，不改 Godot 运行时资源或存档。
- Automated evidence: 严格清单 385 项无问题，发布精灵 129 个文件无失败。自动检查覆盖通道、色边、居中包围盒和成对预览，不代替看图。
- User verdict: 用户在 2026-08-20 看过四面尺寸对比后明确说没问题。

### MQA-ARTWORK-DEMONBOUND-HIT-UL — 魔剑士左上受击的后仰、层次和右手剑

- Status: `passed`
- Source: 魔剑士左上受击第三版画面复查
- Action: 比较已批准的左上近战剑、无剑受击第二版，以及右手持剑的受击第三版。
- Expected: 后侧四分之三的身体向画面左侧后仰，双耳甩向画面右侧；身体盖住双脚上沿；只剩一只眼睛和一道泪；画面左侧的解剖学右手握住那把短剑，身体姿势不变。
- Observe: `Tools/artworks/doge/reviews/doge_capsule_demonbound_hit_ul_sword_compare_v03.png`。
- Preserve on failure: 对比图，以及第一处不对的耳、脚、手、剑或身体边缘。
- Save boundary: 只审图，不改运行时资源或存档。
- Automated evidence: 调用、交付和不可变的人工反馈已记录，严格来源校验通过。抠图、校准和运行时接入不在这次画面结论里。
- User verdict: 2026-08-20 明确通过；只批准左上受击第三版的画面安排。

### MQA-ARTWORK-DEMONBOUND-HIT-DR — 魔剑士右下受击的身体和表情

- Status: `passed`
- Source: 魔剑士右下受击第二版画面复查
- Action: 比较已批准的右下待机、猎人受击语言、失败的第一版和无剑的第二版。
- Expected: 保持灰黑身份和红项圈，硬核胶囊向画面右侧倾；折耳、睁大的眼、正好两道泪和绷紧的嘴读得出是受击顶点；错误生成的剑不在，以便另装正式的剑。
- Observe: `Tools/artworks/doge/reviews/doge_capsule_demonbound_hit_dr_identity_pose_review_v02.png`。
- Preserve on failure: 对比图，以及第一处身份、姿势或表情差异。
- Save boundary: 只审图，不改运行时资源或存档。
- Automated evidence: 调用、交付和反馈收据已记录，严格来源校验通过。去色边、抠图、装剑和运行时接入不在这次结论里。
- User verdict: 2026-08-20 明确通过；只批准无剑的右下受击第二版身体。

### MQA-ARTWORK-DEMONBOUND-CAST-UL — 魔剑士左上施法的层次和轮廓

- Status: `passed`
- Source: 胶囊精灵组件第八版画面复查
- Action: 把已批准的右下施法姿势和左上施法第八版放在一起，看脚、藏起的爪、复用的剑轮廓和身体遮挡。
- Expected: 两只爪完全藏在背对镜头的身体后面；剑保持原宽度，在身体后面，只在双耳之间露出短尖；双脚仍保留原来的部分遮挡。
- Observe: `Tools/artworks/doge/reviews/doge_capsule_demonbound_cast_dr_ul_sword_depth_compare_wip_v08.png`。
- Preserve on failure: 对比图、组件变换，以及第一处不该露出来的边。
- Save boundary: 只审图，不改运行时资源或存档。
- Automated evidence: 严格来源校验通过。层次、轮廓和立体读法仍是当时的人工结论。
- User verdict: 2026-08-20 明确通过；只批准左上施法第八版的画面，不授权接入运行时。

### MQA-GODOT-FULL-RUN — 完整一局的终局外壳

- Status: `passed`
- Source: 首领结算事务修订修复
- Reopen reason: 第二次实机重放证明终局检测和表现队列已经走完；真正的失败是终局转移复用了修订号 148。转移和结算协调已修好并复查。
- Action: 从保留的首领检查点继续，打倒最后的敌人，看终局结算，再回主页。
- Expected: 最后的动画播完，结算从提交走到已保存和导航完成，首领胜利摘要只出现一次，回主页后本局结束。
- Observe: 战斗页、作弊控制台里的结算诊断、首领胜利摘要、主页的继续状态。
- Preserve on failure: 页面先别关；复制控制台日志，留下生产存档和备份，记下最后的结算阶段。
- Save boundary: 这次重放会提交终局摘要并结束本局。
- Automated evidence: 终局修订经完整转移递增；活跃 Run 变空，摘要只写一次，失败回读和重复回调有保护。
- User verdict: 最近一次报告中通过。

### MQA-GODOT-CHEAT-CONSOLE-COPY — 只读战斗日志复制

- Status: `passed`
- Source: 首领结算诊断和控制台复制
- Action: 打开作弊控制台，拖选几行，用 Ctrl+C 和右键菜单复制；再在筛选下试“复制可见”和“全部复制”。
- Expected: 选中文字能正常复制；复制可见只有筛出来的行，全部复制含保留的所有行；控制台操作不会发出战斗指令。
- Observe: 控制台的选择、右键菜单、状态文字，以及用来粘贴核对的外部文本编辑器。
- Preserve on failure: 截图、筛选、复制到的文字、Godot 输出错误。
- Save boundary: 复制不改 Run 或存档。
- Automated evidence: 选择模式、筛选与全部渲染、注入剪贴板，以及不触发玩法输入，均已断言。
- User verdict: 最近一次报告中通过。

### MQA-GODOT-MYSTERY-ADJUDICATION — 固定选项的事件判定

- Status: `passed`
- Source: 神秘事件判定重做
- Action: 进入一个神秘节点，记下被指定的队员和全部固定选项，选之前重载，再继续并结算一个选项。
- Expected: 指定的人、选项、成功率和最终结果在重载前后一致。
- Observe: 神秘事件页、结果页、继续。
- Preserve on failure: Run seed、事件 ID、指定角色、选项和存档副本。
- Save boundary: 结算选项会改 Run。
- Automated evidence: 确定性分配、选项属性、掷点和第五版存档往返已断言。
- User verdict: 最近一次报告中通过。

### MQA-GODOT-SUMMON-CONTROL — 召唤物由谁控制

- Status: `passed`
- Source: 冻结的召唤物 AI 与控制器修复
- Reopen reason: 骷髅战士、骷髅法师和火魔按单位内容 ID 解析内部 AI 和装备；诱饵明确不行动。
- Action: 召唤骷髅战士、骷髅法师和火魔，再把回合推到每个召唤物。
- Expected: 有 AI 的召唤物自动行动；诱饵不攻击。
- Observe: 行动顺序、输入锁、作弊控制台里的 AI 日志。
- Preserve on failure: 召唤类型和等级、当前行动者、AI 日志。
- Save boundary: 普通战斗会改状态。
- Automated evidence: 内部资源、装备、自动回合、拒绝输入和跳过诱饵已断言。
- User verdict: 最近一次报告中通过，没有异常。

### MQA-GODOT-PROGRESSION-ATOMIC — 成长不能跳过、只生效一次

- Status: `passed`
- Source: 相机、菜单、伤害和成长事务
- Action: 分配一点属性，在选技能前关掉，再继续，然后把这次成长做完。
- Expected: 继续会回到分配属性；最终确认只生效一次，并且只解锁下一个节点一次。
- Observe: 成长页、地图节点、主页的继续。
- Preserve on failure: 存档副本、截图、输出。
- Save boundary: 待完成的成长会保存；没做完的界面草稿不写入。
- Automated evidence: 修订、第五版规范化、一次事务和解锁语义已断言。
- User verdict: 最近一次报告中通过，没有异常。

### MQA-GODOT-TURN-PACING — 敌人先攻节奏

- Status: `passed`
- Source: 可玩的敌人速度配置
- Action: 看一场普通遭遇的前两回合。
- Expected: 不同敌人仍有相对快慢，但不会整队敌人总是排在所有我方前面。
- Observe: 行动顺序和作弊控制台。
- Preserve on failure: 遭遇 ID、行动顺序和行动者序列。
- Save boundary: 普通战斗会改状态。
- Automated evidence: 速度配置生成、玩家速度保留和派生属性重算已断言。
- User verdict: 最近一次报告中通过。

### MQA-GODOT-L4-RECOVERY — 第 4 层旧存档恢复

- Status: `passed`
- Source: 第 4 层地图不变量修复
- Reopen reason: 当时的第五版存档已到等待第 4 层选择，三场战斗已完成，但没有地图状态，所以第 4 层路线全锁着。
- Action: 继续保留的当前 Run，进入第 4 层。
- Expected: 第 4 层路线可以走，所选路线能进第一场精英战。
- Observe: 地图、所选路线、继续、Godot 输出。
- Preserve on failure: 存档和备份、Run 种子和修订、地图截图、输出。
- Save boundary: 使用当前持久 Run。
- Automated evidence: 旧第五版修复、确定性投影和不变量检查仍有覆盖。
- User verdict: 已通过；该局已进入第一场精英遭遇。

### MQA-GODOT-FIRE-DEMON-COMBAT — 火魔的专用攻击

- Status: `passed`
- Source: 冻结的火魔攻击迁移
- Action: 召唤火魔并使用它的专用攻击。
- Expected: 目标正确，并遵守冻结的伤害、点燃、射程和每回合次数。
- Observe: 火魔行动、目标血量和状态、作弊控制台、输出。
- Preserve on failure: 格子、行动前后血量、日志、当前回合。
- Save boundary: 普通战斗会改状态。
- Automated evidence: 冻结合同、资源身份、伤害、状态、不暴击和使用次数仍有覆盖。
- User verdict: 最近一次报告中通过。

### MQA-GODOT-HUD-SKILL-PRESENTATION — 抬头和已提交技能的画面

- Status: `passed`
- Source: 紧凑行动条和已提交表现的高亮修复
- Reopen reason: 行动按钮改为显示名和紧凑的第二行消耗；动作播放期间不再高亮当前格。
- Action: 看紧凑行动条、纵向闪电、重叠处按不透明像素点选、毒矛的持有、丢下和捡回、抬头控件，以及反引号打开的作弊控制台。
- Expected: 普攻没有法力行；技能 ID 不带 `skill.` 前缀；正的法力消耗在第二行；动作动画期间行动者脚下没有选中格。其余技能和抬头按已提交状态显示。
- Observe: 行动按钮、角色脚下的格子、战斗单位、抬头、悬停详情、作弊控制台。
- Preserve on failure: 截图或短视频、选中单位和格子、按钮文字、技能事件日志、当前遭遇。
- Save boundary: 技能会改战斗；使用可重放的遭遇检查点。
- Automated evidence: 标签格式、行动中取消高亮、由事件得到的目标、不透明点击顺序、毒矛生命周期和控制台保护已断言。尺寸和画面一致当时仍记为人工项，本条结论已经通过。
- User verdict: 最近一次用户报告中通过。

### MQA-GODOT-BOARD-FIT — 等距棋盘的取景和点击

- Status: `passed`
- Source: 相机、菜单、伤害和成长事务
- Action: 打开一场战斗，在 16:9、16:10 和 21:9 之间缩放，再悬停和点击边缘以及重叠的单位。
- Expected: 完整的 10×10 棋盘居中，不再留出旧的右侧空档；抬头不挡棋盘，指针点选仍然准。
- Observe: 战斗棋盘、抬头边界、悬停详情、选中的格子或单位。
- Preserve on failure: 带分辨率的截图、选中坐标、当前遭遇。
- Save boundary: 使用当前 Run；缩放和点选不改存档。
- Automated evidence: 棋盘包围盒适配和屏幕与格子的往返已断言。取景和指针手感当时仍记为人工项，本条结论已经通过。
- User verdict: 最近一次用户报告中通过。

### MQA-GODOT-PROGRESSION — 成长优先级和三张技能卡

- Status: `passed`
- Source: 选定起始分支和职业属性修复
- Action: 当时对成长优先级、需求、卡片详情和已有技能的验收。
- Expected: 三张不重复的候选，分支优先级和职业属性需求正确。
- Observe: 成长的属性和技能页。
- Preserve on failure: 截图、角色、起始技能、属性和全部卡片。
- Save boundary: 草稿在最终确认前是临时的。
- Automated evidence: 排序、唯一性、元数据、分支和需求仍有覆盖。
- User verdict: 最近一次用户报告中通过。

### MQA-GODOT-UNIT-MOTION-CONTACT — 单位动作、受击、死亡和接地

- Status: `passed`
- Source: 用户截图之后的第 8 阶段表现对齐
- Reopen reason: 十四张已批准的法师、死灵和亚马逊施法、受击、近战、投掷贴图已迁入，并和原有的程序动作合在一起。
- Action: 在一场战斗里至少走两格；法师和死灵施法；亚马逊刺击和毒矛；受一次非致命伤；击倒一个单位；中途暂停，再以 0.5 倍和 4 倍速继续。
- Expected: 我方身体在规定窗口换成对应方向的动作，释放或恢复时回到待机；敌人和召唤物安全地用程序回退。移动摇晃、受击后仰、致命倒下和尸体落地仍是串行的；影子和覆盖层不跟着身体变形；死者不显示状态图标。
- Observe: 移动、受击和死亡时的身体、脚、影子、状态层、血量和法力锚点。
- Preserve on failure: 短视频或连续截图、单位、速度、提示类型、Godot 输出。
- Save boundary: 使用当前战斗检查点；表现速度和暂停不改玩法状态。
- Automated evidence: 十四张贴图的哈希转换、资源引用、方向姿势和回退、只变形身体、中断清理、串行提示、死亡清状态，以及两种渲染器已断言。动作手感和接地间距当时仍记为人工项，本条结论已经通过。
- User verdict: 最近一次用户报告中通过。

## Deferred or Blocked

### MQA-GODOT-TILE-START-CAMP — 起始营地的选人、移动和技能衔接

- Status: `deferred`
- Source: 恢复后的起始营地格子流程
- Action: 用隔离空档启动，按顺序点三名候选人，试重复选择和满员后再点第四人，移动第一名领队，重启，再经起始技能进入第一个节点。
- Expected: 界面清楚显示要三人、顺序、槽位、领队和出口锁的原因；选择是逐步的，不能超出；只有第一个选中的领队会动；重启保留选择但重置模板格子；满三人后出口解锁且只提交一次。
- Observe: 起始营地、角色身体和影子、营火和出口、队伍状态、技能卡、第一个节点。
- Preserve on failure: 每个设置页的截图、点击位置、选择顺序和技能、角色格子、存档副本、第一条输出错误。
- Save boundary: 每次选择都会改待完成的开局；使用隔离存档，失败后继续之前先留存档和备份。
- Automated evidence: Application 已覆盖逐步顺序、重复和满员拒绝、开局持久化和第十一版往返；Godot 测试覆盖真实角色、10×10 格子、身体点击和出口结构。好不好读、会不会点错、移动手感仍是人工项。
- User verdict: 2026-09-17 用户明确选择暂缓这条人工门禁，并立刻合并拉取请求 4，不把它当成通过。

### MQA-GODOT-PAUSE-MENU — Esc 的本局控制

- Status: `deferred`
- Source: 拉取请求 4 合入主线
- Reopen reason: 合入后的起始营地、格子冒险和战斗流程，用本局范围的继续、选项、放弃本局、保存并退出，替换了旧的主页合同。
- Action: 在隔离 Run 里，从起始营地、格子冒险和未完成战斗按 Esc；试继续和选项返回，再分别试放弃本局，以及保存并退出后真正重启进程。
- Expected: 遮罩盖住当前页，只出现本局控制。继续关掉遮罩，选项能安全返回；放弃生成已放弃摘要并去新营地；保存并退出后，重启回到同一个已提交的遭遇和种子。不出现旧主页动作。
- Observe: Esc 遮罩、当前页标题、终局摘要、恢复后的战斗入口、`user://pure-run` 文件。
- Preserve on failure: 遮罩和摘要截图、按键顺序、Run 种子和修订、检查点副本、第一条输出错误。
- Save boundary: 继续和选项是只读的；放弃以及保存并退出会改隔离 Run。做这两个之前先留副本。
- Automated evidence: 当时的代码通过了统一验证器、29 条玩法旅程，以及重启和退出的正式输入覆盖。遮罩好不好读、手感和真正重启进程仍是人工项。
- User verdict: 2026-09-17 用户明确选择暂缓这条人工门禁，并立刻合并拉取请求 4，不把它当成通过。

### MQA-GODOT-INITIATIVE-HUD — 动态先攻头像条、悬停和输入锁

- Status: `deferred`
- Source: 动态先攻与右上圆形头像行动条
- Action: 在隔离战斗里完成普通行动、用行路难改变还没行动的单位的敏捷、召唤或替换召唤物、击杀一个单位，并让一个不能行动的单位轮到；悬停友方和敌方头像，再把队列加到十个以上看省略号。
- Expected: 右上只显示当前和还没行动的单位，当前是金框，敌我底色清楚，先攻相同则我方在前。已行动的头像消失，未行动的按新先攻平滑重排。最多九个头像加一个不能点的省略号。悬停有白框，并在棋盘单位上显示阵营色轮廓和血量、法力。约 0.22 秒的过渡里点击不会重复提交。不能行动的单位自动跳过，但只消耗一次结束回合。
- Observe: 右上先攻条、回合标签、头像框和省略号、棋盘轮廓、行动条、事件日志。
- Preserve on failure: 短视频、回合、可见和隐藏队列、各单位先攻、悬停的单位、点击时刻、Run 种子和检查点。
- Save boundary: 战斗和召唤会改隔离 Run；悬停本身只读。失败时先复制检查点。
- Automated evidence: Core 与 Application 覆盖逐轮重排、未行动队列、召唤插入、昏迷自动结束回合；Godot 测试覆盖九个头像、0.22 秒过渡和输入锁。动画手感、悬停对不对得上、视觉层次仍是人工项。
- User verdict: 2026-09-17 用户明确选择暂缓这条人工门禁，并立刻合并拉取请求 4，不把它当成通过。

### MQA-GODOT-POET-DECOY-ART — 青川犬正式分身美术

- Status: `deferred`
- Source: 诗人功能优先；当前分身继承诗人待机并用半透明青色占位
- Action: 以后的美术任务里完成青川犬身份、四方向、受击或消散和格子落点，逐图审过再接入分身资源。
- Expected: 正式分身在正常战斗缩放下能立刻认出是青川犬，不和诗人本体、敌人或其他召唤物混淆；方向、脚底锚点、受击段数和消失都清楚。
- Observe: 审图面板、单位图库、战斗棋盘、分身受击和消失。
- Preserve on failure: 母图和候选版本、方向、格子对比截图、运行时截图。
- Save boundary: 美术只能走作品管线；没批准不得替换当前运行时素材或改生产存档。
- Automated evidence: 当前半透明诗人待机占位的资源类型、装载、动作和死亡清理、分身规则已自动验证。独立的正式分身美术还没做，不能自动验收。
- User verdict: 用户暂缓。正式青川犬美术不在那一轮实现里。

### MQA-GODOT-AUDIO-ASSETS — 可再分发的音频和试听

- Status: `deferred`
- Source: Godot 音频框架检查点
- Action: 提供或批准一套允许再分发的音频包，再在音频工作台和一整局里试听音乐、音效和界面提示。
- Expected: 文件级来源和哈希有记录；主音量、音乐、音效、界面四条总线的音量、静音、同时播放和清理听着正确，而且不改变玩法时机。
- Observe: 音频工作台、总线表、战斗和页面切换、Godot 输出。
- Preserve on failure: 资源和许可身份、提示和总线、复现步骤、输出。
- Save boundary: 音频设置用自己的版本化设置文件，不改 Run 存档。
- Automated evidence: 总线创建、独立设置、提示和配置校验、同时播放和清理框架已测试。目前没有登记可再分发的音频内容，不能宣称内容迁移或试听已验收。
- User verdict: 用户为这一版暂缓。Windows 候选包故意没有声音，也不要求音频内容。

## Last Emitted Order

1. `MQA-GODOT-TILE-START-CAMP` — 起始营地选人、移动和技能衔接
2. `MQA-GODOT-PAUSE-MENU` — Esc、放弃、保存并退出和重启恢复
3. `MQA-GODOT-POET-RUNTIME` — 诗人开局、六套技能和临时分身
4. `MQA-GODOT-INITIATIVE-HUD` — 动态先攻头像条、悬停和输入锁
