# TileMap 输入路由与开局事务落地边界整体优化计划

## 目标

- `GodotPlayableRunMain` 持有唯一键盘输入路由和持久全局 Pause Overlay；页面仅声明 `RunInputContext`，不再分散创建菜单或处理 Esc。
- 选人和起始技能是 `PureRunSessionService` 进程内草稿；最后一个必需技能前不写 Save，最终一次 CAS 写入完整 Active Run。
- 部分 setup 关闭/Replay 后回到零选择 Start Camp；替换旧 Run 的未完成草稿不覆盖旧 Run。

## 合同

- `RUN-INPUT-ROUTER-001`：Esc 优先级为 Console → Pause → Battle targeting cancel → current context pause。
- `RUN-PAUSE-SHELL-001`：Pause Overlay 与 Current Page 并列、Main 生命周期唯一；context 决定 Battle/Adventure/StartCamp 暂停副作用和菜单动作。
- `ROGUELIKE-SETUP-COMMIT-001`：`PendingRunSetup` 仅为 Application/UI 瞬时 DTO；最后技能前 SaveCount=0，最后一步恰好一次原子写入。

## 实现

1. Application 增加实例级 setup draft、只读投影和 `HasSetupDraft`；改写 Begin/Select/Choose/Cancel，最终一步 CAS Save。
2. V11 Encode 对 pending setup fail-closed；兼容 Decode 清除历史 pending 并保留 Active Run/Terminal Summary。
3. Main 建立持久 Shell/Input Router；`CreatePage` 仅替换页面，所有页面设置 `RunInputContext`。
4. Start Camp/技能页只读 service draft，不再从 SaveStore 恢复 pending。Setup 的 discard/quit 不生成 Abandoned Summary。
5. 测试覆盖 SaveCount、Replay、旧 Run 保护、CAS、V11、真实 Esc、唯一 overlay、Tween pause、生产 Main 重启边界和 gameplay spec。
6. 更新合同、人工验收和 OKF；串行执行 Core/Application/Godot/Gameplay/统一 verifier；不触碰任务外 `godot/project.godot`。

## 验收

- Start Camp、技能页、Initial/普通 Adventure、Battle 均可 Esc 打开/关闭同一 Pause Overlay。
- 部分 setup Replay 后零选择；完整 setup Replay 后进入 Initial Adventure。
- 最终提交前零 Save，最后一步一次 Save/revision。
- 旧 Active Run、V11 node recovery、Adventure movement pause/commit 和 Battle pause 无回归。
