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

- `powercfg /requests`：运行期间 SYSTEM 块应出现 powershell 进程条目（部分环境需管理员权限才能查看，看不到不影响功能）。
- 最硬的证据是事件日志：启用后静置超过关屏阈值，系统日志**无**新增 506（Idle Timeout）事件；停止后照常出现。
- 持有进程异常退出（崩溃/关机）= 请求自动释放，不会留下"永远不睡"的残留。
