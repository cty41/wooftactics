---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot agent workflow
description: Godot 4.7 C# 主线的项目、分层、Editor 生命周期、验证和发布边界。
tags: [godot, agent, workflow, testing]
timestamp: "2026-09-17T00:07:57+08:00"
status: active
catalog_scope: godot-agent-workflow
repo_paths:
  - AGENTS.md
  - .agents/rules/godot-agent-workflow.md
  - .agents/skills/godot-workflow
  - .agents/skills/godot-editor-lifecycle
  - .agents/incidents/godot
  - Tactics.Godot.slnx
  - Tools/godot/Verify-GodotProject.ps1
  - Tools/godot/Build-GodotWindows.ps1
  - Tools/migration/manifest/godot-tooling.json
verified_revision: d092a955
source_fingerprint: sha256:d803273a4fcef4df0c2d3f13258dae6388dc17d967d451176e5e44a0c79e0eb9
---

# Current State

Godot 4.7 C# 与 `godot/project.godot` 是唯一产品和编辑权威。Core/Application 保持纯 .NET；Adapter 承载 Node、Resource、文件系统、UI、EditorPlugin 与运行时集成。godot-ai v3.1.2 的 MIT 源码作为审计过的 Editor-only 依赖固定在公开源码树中，但从游戏 PCK 与 Windows 运行时包排除。

`Tools/godot/Open-GodotDev.ps1` 是唯一支持的 Editor 启动入口：每次串行增量 Build production Adapter、验证程序集身份、按 worktree 隔离 `user://`、生成项目级 Codex Attach 配置，并记录 Editor session。首次生成配置会要求重启一次 Codex 任务。Agent 不得使用共享人工 QA 用户数据；同 worktree 的 Editor 启动与统一 verifier 由同一命名 mutex 串行化。

Godot 修改先由 `godot-workflow` 路由到最小 Specialist Skill。C#、ResourceSaver、生成器和 reload-sensitive 工作遵循 `godot-editor-lifecycle`；只正常关闭该流程确认的 canonical Editor，并只恢复由本流程关闭的会话。

验证按成本递增：先运行直接覆盖改动的最小本地测试，再运行相关本地门禁；产品代码收口时至多运行一次统一入口 `Tools/godot/Verify-GodotProject.ps1`，其串行执行 restore/build、Core/Application/FrozenOracle、Gameplay Spec、Python、Skill/Incident、ResourceSaver 升级、GdUnit、Release/Runtime/Editor headless、renderer、receipt 与 OKF。纯文档或人工账本变更只跑轻量策略/文档门禁。未经用户明确要求或已批准计划逐项授权，Agent 不主动触发或重跑 GitHub Actions、远程 RC/导出与制品构建；平台自动 required check 只读取结果，缺失的远程证据应等待授权。FrozenOracle、Golden 和 receipt 只是历史/确定性证据，不能替代视觉、手感、真实 Editor Reload 或干净 Windows 启动。

Windows 构建使用单一 `Windows Desktop` preset、锁定工具链和受审计 staging。CI 在运行统一 verifier 前显式安装 OKF 与 Pure Run Artwork skill 各自声明的 Python requirements；Debug/Release 包必须通过架构、PCK/managed runtime、顶层 allowlist、测试/缓存/本地配置排除、manifest/hash 与隔离用户目录启动验证。

# Relationships

- 当前规则：`.agents/rules/godot-agent-workflow.md`
- 迁移 provenance：[Godot migration provenance](../plans/godot-migration.md)
- 文档生命周期：[Project Documentation](project-documentation.md)
- 历史工具索引：[Archived Unity Agent Workflow](unity-agent-workflow.md)

# Verification Guidance

未知 API、生命周期、插件或引擎错误必须按 Research Guide 和本地复现取证。自动门禁只报告覆盖到的层级，人工和发布边界必须单独记录。
