# Agent Environment

## Modern Standby 防休眠

本机（mibook）是 Modern Standby（S0ix）机器：电源计划"睡眠：从不"**不阻止**空闲休眠。关屏计时器（AC 15 分钟）到点且无键鼠输入即进入 Modern Standby（Kernel-Power 506，`Reason: Idle Timeout`），后台 agent 的 CPU 活动不重置空闲计时。

- 长任务（评测、批量构建、长测试、整仓验证）开始前运行 `Tools/power/Start-AgentKeepAwake.ps1 -Minutes <预计分钟数>`，结束后运行 `Tools/power/Stop-AgentKeepAwake.ps1`。
- 该工具只阻止睡眠，不改电源计划、不阻止关屏；进程退出即自动释放。细节见 `Tools/power/README.md`。
- 仅在接通电源场景使用；电池供电时允许系统休眠。
- 不得用修改电源计划（如 `monitor-timeout-ac 0`）或注册表禁用 Modern Standby（`PlatformAoAcOverride`）的方式替代。

## 本机工具链环境坑

2026-11 评测与试点实测归拢（来源散见 `Tools/agent-eval/workflow/runs/` 与 `docs/compose/spec/` 各 Journey log）：

- **`ProgramFiles`/`ProgramFiles(x86)` 被 shell 置空** → `dotnet`/NuGet 在 `NuGetEnvironment.GetFolderPath` 抛 `path1` 空引用。修法：同一 bash 调用内先置 `$env:ProgramFiles='C:\Program Files'; ${env:ProgramFiles(x86)}='C:\Program Files (x86)'` 再跑 dotnet（该 shell 环境变量不跨调用保留）。
- **`python` 不在 PATH**（仅 `py -3`），且 `MIMO_PYTHON` 不含 PyYAML → OKF/Python 门禁用 `py -3`；脚本写文件执行，勿内联 `python -c`（PowerShell 引号易碎）。
- **GdUnit 测试前置**：`GODOT_BIN` 指向 Godot console.exe；新 worktree 首跑先 `--headless --editor --path godot --import` 生成 `.uid`，import 后重建 TestHost 再 `dotnet test`。
- **`Tools/vendor/maliang` 子模块默认未初始化** → 统一验证器的 artwork 用例误报失败。修法：`git submodule update --init Tools/vendor/maliang`。
