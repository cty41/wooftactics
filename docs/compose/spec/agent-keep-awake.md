---
feature: agent-keep-awake
status: in-progress
updated: 2026-11-09
source: feat/agent-keep-awake
---

# Agent Keep Awake（关屏照常、禁止空闲休眠）

## Report

## [S1] 问题

mibook 是 Modern Standby（S0ix）机器：电源计划已设"睡眠：接通电源 = 从不"，但该设置不拦 Modern Standby 进入。关屏计时器（AC 15 分钟）到点后，无键鼠输入即触发 **Idle Timeout → Modern Standby**（系统日志 Kernel-Power 506），后台 agent 的 CPU 活动不重置空闲计时。2026-11-07 曾因此让会话停摆近两天。

用户需求明确：**不改关屏设定**（屏幕照常熄灭），只禁止睡眠。改关屏计时器（`monitor-timeout-ac 0`）是钝器，不采用。

## [S2] 设计

### S2.1 机制

用 Windows 电源请求 `SetThreadExecutionState(ES_SYSTEM_REQUIRED | ES_CONTINUOUS)`（kernel32）阻止系统空闲休眠转换：

- 只拦睡眠，不拦关屏（不使用 `ES_DISPLAY_REQUIRED`）。
- 不修改任何电源计划设置（`powercfg /query` 全程无 diff）。
- 请求随进程生命周期存在：进程退出或崩溃即自动释放，无残留（故障安全）。
- 持有进程周期性（约 30s）重申请求，防御系统状态重置。

### S2.2 交付物

- `Tools/power/Start-AgentKeepAwake.ps1` — 启动后台持有进程（隐藏窗口 PowerShell 子进程），`-Minutes` 自动到期（默认 120，0=不自动到期）；状态文件（PID/到期时间）写入 `Tools/power/.state/`（gitignore）；启动后打印 `powercfg /requests` 中的核验条目。重复启动幂等：已有存活持有进程则提示并退出 0。
- `Tools/power/Stop-AgentKeepAwake.ps1` — 停止持有进程、清理状态文件；无进程时幂等退出 0。
- `Tools/power/README.md` — 陷阱成因（Modern Standby / Idle Timeout / 输入计时 vs CPU 忙碌）、用法、核验方法、与"关屏=从不"方案的区别。
- `.agents/rules/agent-environment.md` — 长任务（评测、批量构建、长测试、整仓验证）前启用 keep-awake、结束后停止；AGENTS.md 规则索引补一行。

### S2.3 行为契约

- Enable/Disable 全程不改电源计划设置。
- 期间屏幕按原 VIDEOIDLE 设置正常关闭。
- 持有进程异常退出 = 请求自动释放；Stop 可清理任意残留状态（PID 已死则直接清状态）。
- Start 幂等：状态文件存在且 PID 存活 → 不重复启动。
- DC（电池）策略不干预：脚本仅在接通电源场景使用（规则中注明）。

### S2.4 验证边界

- 机制证据：启用后 `powercfg /requests` 出现该进程条目、Stop 后消失（该命令需管理员权限，无权限时跳过）。
- 电源计划不变：`powercfg /query SCHEME_CURRENT` 启用前后 diff 为空（测试期临时改关屏计时器除外，测毕恢复）。
- **实证判据（权威）：进程心跳连续性**——开启 keep-awake 后以 10 秒周期写心跳文件，静置跨过关屏阈值，心跳无 >15 秒断档。2026-11-09 实测：处理组（请求开启）在 standby 状态内 28 连拍零断档；对照组（纯心跳、无请求）同样零断档，证明周期性活动本身就阻止 DRIPS，请求为双保险。
- 判据修订记录：原"无新增 506 事件"判据**错误**——实测 506（Idle Timeout）在进程正常运行期间照常触发，它只表示进入可休眠状态（关屏转换），不代表冻结。11-07 冻结两天的真实条件是**完全无活动的 CPU 空闲**（回合间/命令中止后）。
- 风险声明保留：个别 OEM 可能无视电源请求；本机实测"请求 + 30 秒周期活动"组合有效。

## [S3] Out of Scope

- 永久修改电源计划（含 monitor-timeout）。
- 注册表禁用 Modern Standby（`PlatformAoAcOverride`）。
- 常驻服务/计划任务/全自动守护。
- 电池（DC）供电场景策略。

## Tasks

- [ ] T1: `Start-AgentKeepAwake.ps1` / `Stop-AgentKeepAwake.ps1` — acceptance: 启动后 `powercfg /requests` 可见条目、电源计划无 diff、`-Minutes` 到期自动释放、重复启动/停止幂等。(covers: S2.1, S2.2, S2.3)
- [ ] T2: `Tools/power/README.md` 与 `.agents/rules/agent-environment.md`（含 AGENTS.md 索引行） — acceptance: 文档说清成因/用法/核验；规则含"长任务前启用、结束停止、仅接通电源场景"。(covers: S2.2)
- [ ] T3: 实证验证（事件日志） — acceptance: keep-awake 期间跨过空闲阈值无 506 Idle Timeout 新事件；Stop 后 `/requests` 条目消失；测后电源计划恢复原值。(covers: S2.4)
- [ ] T4: 独立 review 与文档收口 — acceptance: review 无关键缺口；Report 记录验证命令与结果。(depends: T1, T2, T3)

## Journey log

- PowerShell 5.1 的十六进制字面量 `0x80000000` 按 int32 解析为负数，`[uint32]` 转换直接抛异常——ES 标志必须用十进制 `2147483648` 构造。持有进程曾因此启动即崩，冒烟时"idempotent 已在运行"恰好撞上崩溃前的编译窗口，是假阳性；Start 脚本由此加了启动后 2 秒存活检查。
- 506（Idle Timeout）不是冻结信号：它在进程正常运行期间照常触发，只表示进入可休眠状态。判据必须用进程心跳连续性；DRIPS 冻结只发生在真实 CPU 空闲（进程"存在但不动"也算空闲）。
- dry-run 测试用过期 deadline 只能验证脚本装载，进不了循环体——验证必须覆盖真实调用路径与返回值。
