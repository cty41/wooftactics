# Tactics Godot 工程

这是仓库唯一的 Godot 工程，入口为 `godot/project.godot`。不得为测试或单个功能另建 `project.godot`。

## 打开 Editor

人工开发可以直接从 Godot Project Manager 打开本目录的 `project.godot`。`Tactics Tooling` 的 GDScript Main Screen Shell 不依赖 C# 编译：已有 Adapter assembly 时直接进入 Workbench；首次未构建时显示 `BUILD C# AND RELOAD`，由用户确认后异步构建并优先在当前 Editor 中完成 reload。失败页保留首条错误、完整日志、复制命令与 Retry，不需要禁用 addon。

Agent、自动化、godot-ai/MCP 或需要 worktree 隔离的人工流程仍从仓库根目录运行 prepared 入口：

```powershell
Tools/godot/Open-GodotDev.ps1
```

该入口会串行构建 production Adapter、验证程序集身份、隔离当前 worktree 的 `user://`、同步项目级 godot-ai 配置并记录 Editor session。Project Manager 直接打开不产生这些 Agent/MCP 权限证据。

## 验证

统一验证入口：

```powershell
Tools/godot/Verify-GodotProject.ps1
```

插件位于 `addons/tactics_tooling`，Godot 资产位于 `content`。除非计划或用户明确要求，不创建第二个 Godot worktree、第二个项目或独立能力 Spike；新的验证加入当前工程的 `tests` 或 `Tools/godot`。
