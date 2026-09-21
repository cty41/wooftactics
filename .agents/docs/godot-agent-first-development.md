# Godot Agent-first 开发入口

Agent、自动化与 godot-ai/MCP 开发统一从仓库根运行 prepared 入口：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/godot/Open-GodotDev.ps1
```

默认等价于 `-Mode Agent -UserDataProfile Worktree -GodotAiProfile phase3-observe`。入口会校验锁定的 Godot/.NET/godot-ai，生成被忽略的 `godot/override.cfg` 和 `.codex/config.toml`，始终串行增量 Build `Tactics.Godot.Adapter.csproj`，验证 production DLL 身份，再以 `--tactics-open-godot-dev=v1` user argument 启动当前 worktree 的 Editor。第一次生成 Codex 配置时会输出 `CODEX_RESTART_REQUIRED`；Editor 可以继续使用，但必须重启一次以当前 worktree 为根的 Codex 任务，Godot AI 工具才会进入该 Agent 会话。

人工可以从 Project Manager 直接打开 canonical 项目。Tactics Tooling 的 GDScript Bootstrap 始终注册Main Screen：有production Adapter assembly时立即加载C# Workbench；无assembly时显示`BUILD C# AND RELOAD`，仅在用户确认后异步调用固定helper并优先在同一Editor PID reload。Direct Human会保留Workbench UI作者能力，但不会启动可写authoring MCP bridge，也不会生成prepared dev-session、Codex或worktree readiness证据；Agent不得把direct会话当作恢复或写入入口。

人工复用既有 QA 存档时显式运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/godot/Open-GodotDev.ps1 -Mode Human -UserDataProfile SharedManualQA
```

`SharedManualQA` 不允许 Agent 使用。不同 worktree 拥有不同的 `user://` 与 Editor session；兼容的 godot-ai Attach backend 可以共享 8000/9500，但写操作前仍须检查 session 指向当前 worktree。统一 verifier、GdUnit/TestHost 和同-worktree Editor 不得重叠；入口与 verifier 会通过同一命名 mutex 拒绝冲突。

常用操作：

- 仅准备依赖、配置和 DLL：追加 `-NoLaunch`。
- 扩大工具白名单：使用 `-GodotAiProfile content-authoring|ui-input|presentation`，然后重启 Codex 任务。
- 完整验证：关闭该 worktree 的 Editor，再运行 `Tools/godot/Verify-GodotProject.ps1`。
- 更新 Godot AI：只能审阅固定 Tag/commit/license 后运行 vendor refresh；禁止使用 Dock 自更新。插件源码进入公开源码，但 `godot/export_presets.cfg` 与 Windows包验证继续排除它。
