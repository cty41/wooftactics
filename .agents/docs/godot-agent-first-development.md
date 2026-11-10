# Godot Agent-first 开发入口

跨平台工具链分层与迁移顺序（讨论稿，非实现合同）见 [Tools/godot 跨平台工具链迁移原则](godot-tooling-cross-platform-migration.md)。

Agent 与人工开发统一从仓库根运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/godot/Open-GodotDev.ps1
```

默认等价于 `-Mode Agent -UserDataProfile Worktree -GodotAiProfile phase3-observe`。入口会校验锁定的 Godot/.NET/godot-ai，生成被忽略的 `godot/override.cfg` 和 `.codex/config.toml`，始终串行增量 Build `Tactics.Godot.Adapter.csproj`，验证 production DLL 身份，再启动当前 worktree 的 Editor。第一次生成 Codex 配置时会输出 `CODEX_RESTART_REQUIRED`；Editor 可以继续使用，但必须重启一次以当前 worktree 为根的 Codex 任务，Godot AI 工具才会进入该 Agent 会话。

人工复用既有 QA 存档时显式运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/godot/Open-GodotDev.ps1 -Mode Human -UserDataProfile SharedManualQA
```

`SharedManualQA` 不允许 Agent 使用。不同 worktree 拥有不同的 `user://` 与 Editor session；兼容的 godot-ai Attach backend 可以共享 8000/9500，但写操作前仍须检查 session 指向当前 worktree。统一 verifier、GdUnit/TestHost 和同-worktree Editor 不得重叠；入口与 verifier 会通过同一命名 mutex 拒绝冲突。

Linux／云端 Agent 在无法运行 `Open-GodotDev.ps1` 时，用 Python 入口做与 `-NoLaunch` 等价的准备（Build、`override.cfg`、`.codex/config.toml`、会话 JSON）。**不**会替你启动完整 Editor；godot-ai Attach 仍按 `Tools/migration/manifest/godot-tooling.json` 分平台说明（Windows Dock 生成的 consoleless `pythonw.exe` 启动链与 Linux 环境并不逐字相同）。

```bash
export PATH="$HOME/.dotnet:$PATH"
export GODOT_EXECUTABLE=/path/to/Godot_v4.7.1-stable_mono_linux.x86_64/Godot_v4.7.1-stable_mono_linux.x86_64
PYTHONPATH=. python3 Tools/godot/open_godot_dev.py --no-launch
```

成功时会输出 `GODOT_DEV_READY …`；`godot/.godot/tactics-dev-session.json` 会写入 `noLaunch=true`（无 `editorPid`）。非 Windows 上会 `SKIP (Windows-only): Godot AI Codex configuration …`（Attach 依赖 `pythonw.exe`，与 Windows Dock 生成链不一致）。Windows 日常仍用 `Open-GodotDev.ps1 -NoLaunch`；该 ps1 路径在 `-NoLaunch` 时不写 session 文件，仅做 DLL／配置准备。

常用操作：

- 仅准备依赖、配置和 DLL：追加 `-NoLaunch`。
- 扩大工具白名单：使用 `-GodotAiProfile content-authoring|ui-input|presentation`，然后重启 Codex 任务。
- 完整验证：关闭该 worktree 的 Editor，再运行 `Tools/godot/Verify-GodotProject.ps1`（Windows 薄包装，内部转发到 `Tools/godot/verify_godot_project.py --profile windows`）。Linux／云端 Agent 子集仍用 `python3 Tools/godot/verify_godot_project.py --profile agent`；**未**把 AGENTS.md Quick Reference 默认入口改为 Python，需维护者单独批准。
- 更新 Godot AI：只能审阅固定 Tag/commit/license 后运行 vendor refresh；禁止使用 Dock 自更新。插件源码进入公开源码，但 `godot/export_presets.cfg` 与 Windows包验证继续排除它。
