# Agent Environment

## Modern Standby 防休眠

本机（mibook）是 Modern Standby（S0ix）机器：电源计划"睡眠：从不"**不阻止**空闲休眠。关屏计时器（AC 15 分钟）到点且无键鼠输入即进入 Modern Standby（Kernel-Power 506，`Reason: Idle Timeout`），后台 agent 的 CPU 活动不重置空闲计时。

- 长任务（评测、批量构建、长测试、整仓验证）开始前运行 `Tools/power/Start-AgentKeepAwake.ps1 -Minutes <预计分钟数>`，结束后运行 `Tools/power/Stop-AgentKeepAwake.ps1`。
- 该工具只阻止睡眠，不改电源计划、不阻止关屏；进程退出即自动释放。细节见 `Tools/power/README.md`。
- 仅在接通电源场景使用；电池供电时允许系统休眠。
- 不得用修改电源计划（如 `monitor-timeout-ac 0`）或注册表禁用 Modern Standby（`PlatformAoAcOverride`）的方式替代。
