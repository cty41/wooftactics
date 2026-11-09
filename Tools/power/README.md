# Agent Keep Awake

防止 Modern Standby 空闲休眠打断 agent 长任务（评测、批量构建、长测试、整仓验证）。**屏幕照常按电源计划关闭，只是系统不进入睡眠。**

## 陷阱成因（为什么"睡眠：从不"没用）

mibook 是 Modern Standby（S0ix）机器。电源计划的"睡眠：从不"只表示不进入传统 S3 睡眠，**拦不住 Modern Standby**：

- 进入路径是**关屏计时器（AC 15 分钟）+ 无键鼠输入** → 系统日志 Kernel-Power 506 记录 `Reason: Idle Timeout`。
- **后台 agent 的 CPU 忙碌不重置空闲计时**——Windows 只认键鼠输入。"机器在干活"和"系统判定空闲"可以同时成立。
- 因此改 `monitor-timeout-ac`（关屏=从不）是钝器：屏幕被迫常亮。本工具用 Windows 电源请求 `SetThreadExecutionState(ES_SYSTEM_REQUIRED)`，**只拦睡眠转换、不拦关屏、不动电源计划**，进程退出即自动释放（故障安全）。

## 用法

```powershell
# 启动（默认 120 分钟自动到期；0 = 不自动到期，必须手动停止）
Tools\power\Start-AgentKeepAwake.ps1 -Minutes 180

# 结束（到期会自动释放，手动结束用这个）
Tools\power\Stop-AgentKeepAwake.ps1
```

- 重复 Start 幂等（已有存活持有进程则提示并退出）。
- 仅建议在**接通电源**场景使用；电池供电时应允许系统休眠省电。
- 不修改任何电源计划设置；`powercfg /query` 全程无 diff。

## 核验

- **权威判据是进程心跳连续性，不是 506 事件**：Kernel-Power 506 只表示"进入可休眠状态"（关屏转换），期间进程照常运行；真正冻结进程的是其后的深度空闲（DRIPS），需要真实 CPU 空闲。
- 心跳法：开启 keep-awake 后跑一个每 10 秒写文件的进程，静置跨过关屏阈值，日志无 >15 秒断档即通过（2026-11-09 实测：standby 状态内 28 连拍零断档；纯心跳进程无请求时同样连续）。
- `powercfg /requests` 可辅助确认请求存在（部分环境需管理员权限，看不到不影响功能）。
- **要防的真实场景是"完全无活动的空闲"**（回合之间、命令中止后、夜间待机）：此时进程"存在但不动"不算活动，系统会进 DRIPS 把一切冻结。持有进程的电源请求 + 30 秒周期活动正是针对它。
