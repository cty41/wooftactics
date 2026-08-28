# Tactics Godot 工程

这是仓库唯一的 Godot 工程，入口为 `godot/project.godot`。不得为测试或单个功能另建 `project.godot`。

## 打开 Editor

从仓库根目录运行唯一支持入口：

```powershell
Tools/godot/Open-GodotDev.ps1
```

该入口会串行构建 production Adapter、验证程序集身份、隔离当前 worktree 的 `user://`、同步项目级 godot-ai 配置并记录 Editor session。

如果通过 Godot Project Manager、双击项目或手工命令直接打开，`Tactics Tooling` 会在本次会话中保持停用，并提示：

1. 正常关闭当前 Godot Editor。
2. 在仓库根目录运行 `Tools/godot/Open-GodotDev.ps1`。
3. 等待 C# Build 显示 0 errors。
4. 不要手工重新启用报错的 addon。

提示框可以复制完整启动命令，但不会自动执行脚本或关闭 Editor。

## 验证

统一验证入口：

```powershell
Tools/godot/Verify-GodotProject.ps1
```

插件位于 `addons/tactics_tooling`，Godot 资产位于 `content`。除非计划或用户明确要求，不创建第二个 Godot worktree、第二个项目或独立能力 Spike；新的验证加入当前工程的 `tests` 或 `Tools/godot`。
