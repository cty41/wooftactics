---
name: tactics-git-workspace
description: "Use when starting a new branch, checking out, creating a worktree, isolating parallel work, or worrying about LFS/disk — keep development on the current workspace checkout instead of a second tree."
---

# Tactics Git Workspace

Tactics 是带 LFS、美术和 Godot 导入缓存的游戏仓库。默认在用户当前 workspace 切分支开发，不自动再建一份检出。

Always-on 指针：`.agents/rules/agent-worktree.md`。本 Skill 写 checkout 步骤；规则只保留默认句和例外。

## Quick Reference

| 需要 | 做法 |
|---|---|
| 新功能 / 对 `main` 移植 | 当前树 `git checkout -b <name> <start>` |
| 对照旧分支文件 | `git show <ref>:<path>`，不要复制整树 |
| Godot 打开时要切分支 | 先走 `godot-editor-lifecycle` 关闭，切完再按需恢复 |
| 保留对照源 | 不要删本地或 `origin/<old-branch>` 引用 |
| 第二份 worktree | 仅计划写明、用户明确要求、或当前树无法安全保留时 |

Canonical 项目：当前 workspace 的 `godot/project.godot`。不要为隔离再建第二个项目。

## When to use

- 要从 `main` 或其他起点拉新分支。
- 用户说就地切、不要新 worktree、或担心磁盘 / LFS。
- 有人想为「改动大」「方便对照」「可能并行」再开一份检出。
- 无关历史或公开根切换，工作区会整树替换。

## Workflow

### Step 1: 留在当前 workspace

确认用户指定的 worktree。不要 `git worktree add`，不要为同一仓库再建路径。

```powershell
git status -sb
git rev-parse --show-toplevel
```

工作区必须干净。任务外 dirty 文件不得覆盖、暂存或清理。

### Step 2: 关闭 Godot

切分支、尤其是无关历史之前，按 `godot-editor-lifecycle` 正常关闭 Editor。不要在 Editor 开着时替换整树。

### Step 3: 从目标起点切分支

```powershell
git fetch origin
git checkout -b feat/example origin/main
```

保留旧分支引用。之后 cherry-pick、`git show` 和 `git diff <old> -- <path>` 都靠它们。

无关历史切换会整树替换。`.godot`、mono 和导入缓存会过期；第一次打开要重新导入。这比复制整份 LFS / 美术 / 导入缓存便宜。

### Step 4: 对照用对象库

```powershell
git show origin/feat/example:src/Tactics.Core/Runs/PureRunModels.cs
git diff origin/feat/example -- path/to/file
```

不要为并排看文件再开 worktree 或复制检出。

### Step 5: 例外才建新 worktree

只有这些情况可以 `git worktree add`：

1. 当前执行计划把新 worktree 写成必要隔离步骤。
2. 用户明确要求新 worktree。
3. 当前树有无法安全保留的冲突，且新 worktree 是唯一安全路径。

创建前记录理由、源/目标分支、绝对路径、生命周期和必须保留的 dirty 文件。创建后说明哪棵树是主工作路径，避免 Editor 打开错误项目。

## Anti-patterns

| 错误 | 正确 | 原因 |
|---|---|---|
| 为方便或并行自动 `git worktree add` | 当前 workspace `checkout -b` | 第二份树再占 LFS、美术、`.godot`、mono |
| Editor 开着切无关历史 | 先 lifecycle 关闭 | 导入和 Adapter 程序集会对错树 |
| 删旧分支好「干净」 | 保留对照引用 | cherry-pick 和 `git show` 会断 |
| 为 diff 复制整份检出 | `git show <ref>:<path>` | 游戏资源体积不值得 |
| 再建一个 `project.godot` | 只用当前树的 canonical 项目 | 产品只承认一份 Godot 项目 |

## Checklist

- [ ] 仍在用户指定的 workspace
- [ ] 工作区干净，或已说明不能动的 dirty 文件
- [ ] 切分支前 Editor 已按生命周期关闭
- [ ] 新分支从正确起点拉出，旧引用仍在
- [ ] 对照走对象库，没有第二份树或第二个项目
- [ ] 若创建了 worktree，三条例外之一成立且已记录
