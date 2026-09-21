# Godot Editor 双入口与 GDScript 自恢复 Shell 优化计划

## Summary

人工从 Project Manager 直接打开 `gd1` 是正式支持流程；`TacticsToolingBootstrap.gd` 始终注册 Main Screen，C# assembly 缟失/不可实例化时显示正常的首次构建页，点击 `BUILD C# AND RELOAD` 后异步构建并优先在当前 Editor PID 内加载现有 C# Workbench。`Open-GodotDev.ps1` 继续作为 Agent、自动化、godot-ai/MCP 和严格 worktree 隔离入口。

## Contracts

- Human Direct：不要求 `--tactics-open-godot-dev=v1`，已有 assembly 直接 READY，缺失时进入 BUILD_REQUIRED。
- Prepared Agent：launcher 预构建、配置 worktree user dir/Codex、写 session 并传 prepared 标记；Agent/lifecycle 禁止裸启动。
- GDScript 是唯一 EditorPlugin/Main Screen owner；C# controller 仍为 Editor-only Node，authoring 规则不迁到 GDScript。
- 本轮只处理 assembly 缺失或不可实例化，不新增源码/DLL 过期检测。

## Implementation

1. 新增 `Tools/godot/Build-GodotDevAssembly.ps1`，统一 SDK 校验、operation lock、串行 Adapter Debug Build、assembly identity、token/result/log合同。
2. `Open-GodotDev.ps1` 复用 helper，保留 Godot版本、override/Codex、启动和 session职责。
3. Bootstrap 实现 CHECKING_CONTROLLER、BUILD_REQUIRED、BUILDING、WAITING_FOR_ASSEMBLY_RELOAD、READY、BUILD_FAILED、RESTART_REQUIRED；Main Screen 恒注册；异步 PowerShell、token化结果、cache-replace controller重试和清理。
4. Bootstrap probe 覆盖 direct+assembly、direct缺assembly、同一direct Editor外部Build后READY、prepared READY；reload smoke覆盖direct/prepared。
5. 更新Python政策、Agent/Human入口规则、README、生命周期Skill、权威合同、manual acceptance和OKF。
6. 串行运行工具测试、Adapter/TestHost、Bootstrap/Reload probes、统一verifier、diff/OKF；保持任务外 `godot/project.godot` 原样。

## Acceptance

- Direct open从不显示“关闭Editor并运行Open-GodotDev”阻断弹窗。
- 首次Build不冻结、不启动第二Editor；成功同PID进入Workbench；失败显示firstError、logPath、复制命令和Retry。
- Agent prepared入口安全边界不放宽。
- 完成后将长期结论并入docs、同步OKF并删除本active plan，由Git保留历史。
