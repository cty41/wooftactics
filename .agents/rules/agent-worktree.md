# Agent Worktree Policy

Agent 默认复用用户当前指定的 workspace：在当前树 checkout 开发，不自动创建、删除或切换到新的 worktree。

Checkout、无关历史切换、对照旧文件和例外建树的步骤见 `.agents/skills/tactics-git-workspace/SKILL.md`。

只有以下情况允许创建新的 worktree：

1. 当前执行计划明确把新 worktree 作为必要的隔离步骤；
2. 用户明确要求创建新的 worktree；
3. 当前 worktree 存在无法安全保留的冲突状态，且新 worktree 是继续工作的唯一安全路径。

“代码改动较大”“方便管理”或“可能需要并行”本身不构成创建 worktree 的理由。

Canonical Godot 项目只使用当前 workspace 的 `godot/project.godot`，不得为隔离再建第二个项目。
