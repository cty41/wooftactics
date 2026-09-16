# Tactics Godot 项目 - Agent 指南

本仓库的当前产品主线是 Godot 4.7 C# 三职业 Pure Run。Unity 工程只保留到最终退役确认，不能再作为编辑、生成或运行权威；历史行为由 FrozenOracle、Golden、receipt 和最终 Unity Tag 提供证据。

## Quick Reference

| 规则 | 说明 |
|---|---|
| 唯一 Godot 项目 | 只使用 `godot/project.godot`，不得创建第二个项目或切换 worktree |
| C# 分层 | Core/Application 不引用 Godot；Adapter 承载 Node、Resource、文件系统与 UI |
| 资源写入 | `.tres/.tscn` 只能通过 ResourceSaver、Editor API 或受测转换器生成 |
| 主线验证 | 默认先跑最小本地门禁；产品代码收口时至多运行一次 `Tools/godot/Verify-GodotProject.ps1`。未经用户明确要求，不主动触发 GitHub Actions、远程导出或同类高耗时任务 |
| Editor 启动 | 只用 `Tools/godot/Open-GodotDev.ps1`；它串行 Build、隔离 worktree 用户数据并校验插件/配置 |
| Godot 修改验证 | Core/Application/Godot `.cs` 使用主线或迁移期隔离门禁，不调用 Unity compile |
| Editor 生命周期 | reload-sensitive 修改使用 `godot-editor-lifecycle` 正常关闭并恢复，不强杀进程 |
| 前台交互 | 未经明确授权不得抢占焦点或注入真实输入；自动 QA 使用后台测试链 |
| 人工验收 | review 与自动门禁完成后，用 `manual-qa-handoff` 更新累计账本；自动测试不能代替人工通过 |
| 知识维护 | 先读 `.agents/knowledge/index.md`，变更后运行 OKF impact、更新正文并 sync |

## 当前规则索引

- `.agents/rules/godot-agent-workflow.md`：Godot 项目、C#、Resource、EditorPlugin 和测试边界。
- `.agents/rules/foreground-interaction.md`：窗口、真实输入和人工验证边界。
- `.agents/rules/agent-worktree.md`：复用当前 worktree，不自动创建、删除或切换。
- `.agents/rules/code-documentation.md`：代码注释与系统规则说明。
- `.agents/rules/knowledge-maintenance.md`：OKF 查询、写回、替代和校验。
- `.agents/rules/godot-migration.md`：退役完成前的历史迁移与来源审计边界。

Unity-only rules、skills、MCP 和工具的退役证据保存在 `Tools/migration/manifest/retirement/unity-governance-retirement-v1.json`；它们不得再指导新实现。公开根不包含 Unity 工程；固定的 MIT godot-ai EditorPlugin 源码随仓库审计，但不进入游戏发布包。

## 共享 Agent 技能

通用技能（`grill-me`、`grilling`、`brainstorming`、`eli5`、`make-dev-plan`、`plan-mode-plan-writer`、`project-doc-organization`、`skill-writing`）来自公共仓 `cty41/skills`（[github.com/cty41/skills](https://github.com/cty41/skills)，MIT），经其 `scripts/install-user.ps1` 全局安装到 `~/.agents/skills`（Windows junction / macOS-Linux symlink）。本机已安装；更新 = `git -C <skills-checkout> pull` + 重跑安装脚本。任何读取用户级技能根的工具（DSH、Codex、OpenCode、Claude）在所有项目中都能看到这些技能。

- **优先级**：项目本地 `.agents/skills/<技能名>` 覆盖用户级全局安装（DSH 发现顺序为 project-agents 先于 user-agents）。
- **本地技能边界**：仅项目专属技能（`godot-*`、`gameplay-*`、`artworks-prompt-library`、`pure-run-artwork-pipeline`）与两个有意特化 —— `knowledge-maintenance`（完整 `Tools/okf` 工具链，替代全局 OKF-lite 版）、`manual-qa-handoff`（含 `agents/openai.yaml`，被 `Tools/agent-policy/validate_manual_qa_handoff.py` 硬引用）。
- **约定映射**：`.agents/plans/`、`.agents/docs/`、`.agents/knowledge/` 与共享仓默认一致；OKF 命令以本地 `Tools/okf/` 为准（见 `.agents/rules/knowledge-maintenance.md`），不使用全局 `tools/okf-lite` 的默认路径。
- **禁止**：不得把通用技能复制回 `.agents/skills/`（避免双源漂移）；删除本地技能前先确认没有 `Tools/*` 门禁或文档硬引用其路径。

## 绝对禁止

1. 直接手写或机械修改 Godot `.tres/.tscn`。
2. 让 Core/Application 引用 Godot API、Node、Resource 或文件系统。
3. 通过编辑器脚本或文件系统旁路绕过项目代码、ResourceSaver 与资产管线。
4. 未经确认删除 FrozenOracle、Golden、迁移 receipt 或许可证证据。
5. 覆盖、暂存或清理不属于当前任务的 dirty worktree 文件。

## 必须执行

1. 未知 Godot API、生命周期、插件或引擎错误按 `godot-workflow` Research Guide 调查并本地验证。
2. Godot C#、ResourceSaver、生成器或 reload-sensitive 修改前后遵循 Editor 生命周期规则。
3. 提交前检查精确 staged scope、`git diff --cached --check`、相关测试和 OKF。
4. 修改 `catalog-scopes.yaml` 监控范围后运行：
   - `python Tools/okf/catalog_impact.py report --worktree`
   - 更新受影响权威概念正文
   - `python Tools/okf/catalog_impact.py sync --worktree --scope <scope> --write`
5. 内容 ownership 与人工验收分开记录：允许 `GodotOwned + manual_qa_pending`，不得把自动门禁写成人工通过。
6. 验证采用成本递增顺序：最小相关测试 → 相关本地门禁 → 必要时一次统一本地 verifier。除非用户明确要求、已批准计划逐项写明，或托管平台自行触发 required check，否则 Agent 不得主动调用 `gh workflow run`、重新运行 GitHub Actions、远程 RC/导出/制品构建或其他同类高耗时任务；若合并政策依赖未自动触发的远程门禁，应报告并等待授权，不得自行补跑。纯文档或人工账本提交不得因此重跑完整产品 CI。

## Agent 约束

- Plan、code 和 debug 输出使用中文；代码标识符遵循 .NET 命名规范。
- 如果事实不在仓库、最终 Tag 或已验证的外部权威中，对 Agent 来说就不存在。
- OKF 是导航与当前状态综合层；实际实现必须继续核对其 `repo_paths` 指向的代码、Resource 和测试。
