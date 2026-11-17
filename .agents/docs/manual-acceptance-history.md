# 人工验收历史

已通过的结论从活账本挪到这里。状态和 ID 保持原样。当前要看的内容在 [manual-acceptance.md](manual-acceptance.md)。

## Pending

当前要看的条目在活账本，不在这里。

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

暂缓条目留在活账本。
