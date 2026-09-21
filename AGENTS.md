# Tactics Godot 项目 - Agent 指南

本仓库的当前产品主线是 Godot 4.7 C# 三职业 Pure Run。Unity 工程只保留到最终退役确认，不能再作为编辑、生成或运行权威；历史行为由 FrozenOracle、Golden、receipt 和最终 Unity Tag 提供证据。

## Quick Reference

| 规则 | 说明 |
|---|---|
| 唯一 Godot 项目 | 只使用 `godot/project.godot`，不得创建第二个项目或切换 worktree |
| C# 分层 | Core/Application 不引用 Godot；Adapter 承载 Node、Resource、文件系统与 UI |
| 资源写入 | `.tres/.tscn` 只能通过 ResourceSaver、Editor API 或受测转换器生成 |
| 主线验证 | 使用 `Tools/godot/Verify-GodotProject.ps1`；它要求 Unity 根目录不存在 |
| Editor 启动 | 人工可从 Project Manager 直接打开并由 GDScript Shell 首次构建；Agent、自动化和 godot-ai/MCP 只用 `Tools/godot/Open-GodotDev.ps1` |
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

## Agent 约束

- Plan、code 和 debug 输出使用中文；代码标识符遵循 .NET 命名规范。
- 如果事实不在仓库、最终 Tag 或已验证的外部权威中，对 Agent 来说就不存在。
- OKF 是导航与当前状态综合层；实际实现必须继续核对其 `repo_paths` 指向的代码、Resource 和测试。
