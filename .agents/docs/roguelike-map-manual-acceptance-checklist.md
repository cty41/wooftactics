# Roguelike Map 真实 TileMap 人工验收 Checklist

本文件是 `.agents/plans/roguelike-map-real-tilemap-atlas.md` 的分阶段人工验收操作指南，方便按 PR 连续执行；它不保存 verdict。实际 `pending / passed / failed / deferred / blocked` 状态、稳定 MQA ID 和用户反馈只写入 [人工验收账本](manual-acceptance.md)。只有对应 PR 完成 review 且所需自动门禁通过后，才执行该阶段；未实现阶段不能按人工失败记录。

## 本轮验证状态（2026-08-24）

### 已通过的自动验证

- [x] `dotnet build Tactics.Godot.slnx --no-restore`：成功，0 warning、0 error。
- [x] Application Start/Save 回归：60/60，包括增量选人、首位顺序、重复/满员拒绝、setup 放弃和 V11 编解码。
- [x] Core Adventure Map 合同测试：3/3。
- [x] 隔离 Godot TileMap/Start Camp 测试：7/7，包括 100 Tile 投影、模板、真实 Actor、唯一 Active、Start→N1 锚点连线、统一地图尺寸和地图集方向。
- [x] OKF 工具测试：16/16；knowledge bundle 校验通过。
- [x] `git diff --check`：通过；未暂存、提交或清理用户现有改动。

### 当前工具阻断（不等于本切片测试失败）

- [ ] 完整 `Verify-GodotProject.ps1` 在运行测试前因仓库仍存在退役 Unity 根目录 `Assets` 而停止；未授权删除该目录。
- [ ] gameplay contract CLI 因本地缺少 Node 包 `commander` 无法启动；未擅自安装依赖或修改 lockfile。

### 仍需人工验证

- [ ] 首屏自动路由、视觉布局、输入手感、误触、真实重启/Reload、损坏存档隔离文件和 Godot Output。
- [ ] 以下 Start 四项均保持 `pending`；自动测试不得代替玩家 verdict。

## 环境准备

- [ ] 在 `D:\codes\tactics-worktrees\feat-gd1` 通过 `Tools/godot/Open-GodotDev.ps1` 打开唯一 `godot/project.godot`；已有正确 Editor 时不重复启动。
- [ ] 使用隔离、可丢弃 Run；涉及节点迁移、奖励、战斗或 Continue 前先保留 save/backup。
- [ ] 以 1600×900 为首轮基线，需要布局检查时再覆盖 16:9、16:10 和 21:9。
- [ ] 保持 Godot Output 可见；失败时先保存截图/短视频、Run seed、节点/格位、存档和第一条异常，不立即重试覆盖现场。

## 本轮重点：Start 优先切片

1. [ ] **自动启动与恢复（`MQA-GODOT-START-FLOW`）**
   - **操作**：备份存档后，依次验证空存档、营地已选 1 人、技能选择中、普通节点和 PendingBattle 的关闭再启动；另用可丢弃损坏副本启动一次。
   - **预期**：没有旧 Home；空/不可恢复存档直接进入新营地，其余直接恢复对应页面；PendingBattle 保持 Encounter/Seed 并从入口 checkpoint 重开；损坏文件被隔离。
   - **观察**：首个可交互页面、Run seed/revision、`user://pure-run` 文件和 Godot Output。
   - **失败保留**：save/backup/corrupt 文件、首屏截图、页面标题、seed/revision 和第一条 Output 异常。
   - **存档边界**：只使用备份或隔离存档，损坏测试不得针对唯一生产存档。

2. [ ] **营地组队与领队（`MQA-GODOT-TILE-START-CAMP`）**
   - **操作**：确认选 3 人提示、计数、三槽、首位领队和出口原因；先选 1 人并移动到多个可走 Tile，再选满 3 人；重复点击已选角色并在满员后点击第 4 人；关闭再启动。
   - **预期**：首人是唯一可移动领队，后续成员留在模板槽；选择不可撤销/重排且不超过 3 人；重启保留顺序但 Actor 回模板格；出口只在 3/3 解锁。
   - **观察**：StartCampView、Party 面板、角色 Body/Shadow、Tile、出口和 Godot Output。
   - **失败保留**：点击位置、选择顺序、移动前后格位、截图/短视频、save/backup 和 Output。
   - **存档边界**：每次选人都会修改 PendingRunSetup；失败后先复制存档再继续。

3. [ ] **Start 地图集与相机（`MQA-GODOT-MAP-CAMERA`）**
   - **操作**：在 Current 状态分别点击各候选角色身体并移动首位领队；按 `M` 进入 Overview，用右键拖动和 WASD/方向键浏览到最远节点并点击 Preview；滚动滚轮确认无变化，再按 `M` 返回，另用 `F/Home` 返回领队。
   - **预期**：Current 完整显示 10×10 Start，角色身体可选且重叠处选择前景角色。Overview 保持地图与文字可读，不要求一屏显示全路线；只允许浏览和 Preview，不允许选人、移动或出口。滚轮在两种状态均无作用；`M` 不受焦点影响并可反复稳定切换。每张 TileMap 的“标题·类型·层数”固定在自身上方中央，随地图平移而不漂移。Current 与 Overview 显示不同的固定操作提示；Party Setup、状态和 Esc 菜单始终位于地图内容上方。
   - **观察**：StartCampView、10×10 Tile 边界、Start→N1 端点、主路线/分支方向、各节点视觉尺度、页头、Party Setup、Planning/Party 状态、操作提示、Esc overlay 和 Godot Output。
   - **失败保留**：窗口尺寸、输入序列、节点 ID、缩放/位置、短视频和 Output。
   - **存档边界**：Preview 与相机只读；不要点击出口覆盖故障现场。

4. [ ] **出口、技能与 Esc（`MQA-GODOT-START-ESC`）**
   - **操作**：在 0/3、1/3、2/3 点击出口，再在 3/3 点击一次并完成起始技能；检查 Esc 菜单；分别验证营地 Abandon Run 与战斗 Save and Quit。
   - **预期**：未满员不推进，3/3 仅提交一次；Esc 只有 Continue、Options、Abandon Run、Save and Quit；Abandon 经确认进入 Abandoned 摘要并可进新营地；Save and Quit 后恢复同一 Encounter/Seed 入口 checkpoint。
   - **观察**：出口状态、技能页、Esc overlay、Terminal Summary、重启后的战斗入口和 Godot Output。
   - **失败保留**：完整操作序列、菜单截图、Run seed/revision、checkpoint 存档和 Output。
   - **存档边界**：Abandon 与技能选择会永久推进隔离 Run；测试前保留备份。

## PR 2 — 战斗棋盘 TileMap 化（实现后执行）

- [ ] 普通、精英和 Boss 战斗均显示真实共享 TileMap；进入战斗节点后直接开战，没有战前探索或占位 Encounter Label。
- [ ] 玩家/敌人位置、Facing、遮挡排序、Hover、目标选择、移动、技能 VFX、伤害数字、尸体和覆盖层锚点与迁移前体验一致。
- [ ] 三类战斗胜利后显示对应 resolved 残局并开放正确出口；失败只进入一次 Defeated 终局。
- [ ] PendingBattle 退出并 Continue 后，以相同 Encounter、Seed 和入口重新开始，不恢复逐行动状态。

## PR 3 — 地图集剩余遮罩与情报（Start 切片通过后执行）

- [ ] 地图集包含全部节点 Preview，只有当前节点为 Active；远端 Preview 不出现真实单位、交互或动画行为。
- [ ] 拖动、拉远、全屏聚焦、返回当前地图均顺畅；关闭地图集后当前节点输入恢复，战斗/阻塞事件期间地图集只读。
- [ ] Hover/点击远端节点只更新详情，不直接迁移；低性能降级仍保留节点类型、拓扑、出口方向和已知情报。
- [ ] Planning 只显示类型/拓扑/入口出口且内部保持黑色；TacticalPreview 不泄露敌人数、出生格或奖励；Current/Completed 状态清楚可辨。
- [ ] 四种连线状态、徽标和出口端点易读；提交出口后的“拉远→沿线→聚焦”转场自然，中断后 Reload 直接聚焦已保存目标。
- [ ] 在最大地图集观察帧 pacing、闪烁、排序和拖动延迟，记录窗口尺寸与硬件环境。

## PR 4 — 非战斗节点、出口事务与 Continue（实现后执行）

- [ ] 休息、商店、宝箱和普通事件使用可辨认的正式模板与对象；三名队员出现但只有领队可移动，固定队友不显示可移动反馈。
- [ ] 切换领队后从该角色当前内存格移动；对象只能在领队邻接后交互，解决状态明确且奖励不能重复领取。
- [ ] 每个出口常驻显示目标类型、方向、威胁和已知情报；锁定原因清楚。
- [ ] 点击背景、出口边缘和相邻出口间隙不会迁移；邻接后确认才提交，取消不改 revision 或存档，双击只成功一次。
- [ ] Continue 保留领队、节点结果、奖励、库存、路线和发现，但不保留临时格位、相机、动画、Tooltip 或未确认弹窗；队伍从模板槽重建。
- [ ] PendingBattle、Defeated 和 Completed 分别恢复到规定入口；已完成节点不能通过 Atlas、反向出口或 Reload 重进。

## PR 5 — 事件原地图战斗（实现后执行）

- [ ] 分别触发诅咒宝箱、祭坛守卫和护送伏击；确认后探索、对象、出口和地图集被锁定，队伍直接进入战斗槽且无战前走位。
- [ ] 战斗始终保留同一 TileMap 和相机，不创建或闪切到第二棋盘；敌人、对象与护送 NPC 身份清楚。
- [ ] 战中 Reload 从同一事件遭遇入口重新开始；胜利只结算一次奖励/结果，正确切换宝箱、祭坛、NPC、尸体和出口状态。
- [ ] 任一事件战失败立即进入终局，不能恢复到触发前探索。

## PR 6 — 完整 Run、性能与 Editor 生命周期（实现后执行）

- [ ] 从 Start Camp 完成至少一局完整 Run，覆盖普通、精英、Boss、休息、商店、宝箱和事件节点。
- [ ] 全程检查地图尺度、角色/对象辨识、Atlas 拖动/聚焦、遮罩/连线、出口确认、事件同图战斗、转场和帧 pacing。
- [ ] 执行一次真实 C# Assembly Reload 后 Continue；当前节点、领队、奖励、路线和遭遇正确，无重复 Actor/信号、临时节点、输入锁或存档异常。
- [ ] 检查 Godot Output 与 CheatConsole；任何新资源、程序集、孤儿节点、Unicode/NUL 或 Save 错误均保留完整上下文。

## 自动覆盖，不重复作为人工步骤

- Tile 数量、逐格投影、边缘拾取、模板槽容量/锚点/连通性/状态层。
- ResourceSaver 生成幂等、固定 UID、Catalog、Save hash/revision、事务幂等与 Preview/Active 结构计数。
- 自动结果只能证明对应结构和逻辑；视觉辨识、误触、操作手感、转场、帧 pacing 与真实 Editor Reload 仍由人工判定。

## 环境与收尾

- [ ] 使用 `Tools/godot/Open-GodotDev.ps1` 打开 canonical Editor；不要再输入 `cd cd ...`。
- [ ] 本轮 Editor 已恢复到 `D:\codes\tactics-worktrees\feat-gd1\godot`；如果当前已经打开，无需重复启动。
- [ ] 完成 1–4 后检查 Godot Output；失败现场先保存证据，再恢复原生产 save/backup。

## 反馈格式

执行账本当前发出的编号后，按以下格式反馈；编号由账本 `Last Emitted Order` 映射到稳定 MQA ID：

```text
1–4 OK
3 failed：实际现象……
4 deferred
Output：无异常 / 第一条异常及上下文
```
