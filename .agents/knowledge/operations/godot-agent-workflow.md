---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot agent workflow
description: Godot 4.7 C# 主线的项目、分层、Editor 生命周期、验证和发布边界。
tags: [godot, agent, workflow, testing]
timestamp: "2026-08-28T12:01:18+08:00"
status: active
catalog_scope: godot-agent-workflow
repo_paths:
  - AGENTS.md
  - .agents/rules/godot-agent-workflow.md
  - .agents/skills/godot-workflow
  - .agents/skills/godot-editor-lifecycle
  - .agents/incidents/godot
  - Tactics.Godot.slnx
  - godot/addons/tactics_tooling
  - Tools/godot/Open-GodotDev.ps1
  - Tools/godot/Test-TacticsToolingBootstrap.ps1
  - Tools/godot/Verify-GodotProject.ps1
  - Tools/godot/Build-GodotWindows.ps1
  - Tools/migration/manifest/godot-tooling.json
verified_revision: d092a955
source_fingerprint: sha256:271dd95c722c2a779339c673397ec1cbec13fd50d4a70907f88984a00362982e
---

# Current State

Godot 4.7 C# 与 `godot/project.godot` 是唯一产品和编辑权威。Core/Application 保持纯 .NET；Adapter 承载 Node、Resource、文件系统、UI、EditorPlugin 与运行时集成。godot-ai v3.1.2 与 `addons/tactics_tooling` 作为 Editor-only 工具保留在源码树中，但均由 export preset 从游戏 PCK 与 Windows 运行时包排除。

`Tools/godot/Open-GodotDev.ps1` 是唯一支持的 Editor 启动入口：每次串行增量 Build production Adapter、验证程序集身份、按 worktree 隔离 `user://`、生成项目级 Codex Attach 配置，并记录 Editor session。入口在 Godot 的 `--` 后传递项目启动契约；不经入口直接打开时，依赖无关的 GDScript Bootstrap 会保持 `Tactics Tooling` 停用，并在 GUI 中提示关闭 Editor、运行入口和等待 Build，而不是让缺失或过期的 C# 程序集触发含糊 addon 错误。Bootstrap 的 Main Screen 注册只依赖构造时已取得的启动参数，因为 Godot 可能在 `_enter_tree()` 前查询 `_has_main_screen()`；Controller 就绪后再重放可见性。复制出的绝对启动命令可在任意 shell 目录执行；入口会先切到仓库根解析 `global.json` 和执行 Adapter Build，避免误用调用目录中的其他 .NET SDK。首次生成配置会要求重启一次 Codex 任务。Agent 不得使用共享人工 QA 用户数据；同 worktree 的 Editor 启动与统一 verifier 由同一命名 mutex 串行化。

Godot 修改先由 `godot-workflow` 路由到最小 Specialist Skill。C#、ResourceSaver、生成器和 reload-sensitive 工作遵循 `godot-editor-lifecycle`；只正常关闭该流程确认的 canonical Editor，并只恢复由本流程关闭的会话。

统一入口 `Tools/godot/Verify-GodotProject.ps1` 串行执行 restore/build、Core/Application/FrozenOracle、Gameplay Spec、Python、Skill/Incident、GdUnit、Release/Runtime/Editor headless、renderer、receipt 与 OKF。FrozenOracle、Golden 和 receipt 只是历史/确定性证据，不能替代视觉、手感、真实 Editor Reload 或干净 Windows 启动。

Windows 构建使用单一 `Windows Desktop` preset、锁定工具链和受审计 staging。Debug/Release 包必须通过架构、PCK/managed runtime、顶层 allowlist、测试/缓存/本地配置排除、manifest/hash 与隔离用户目录启动验证。

# Relationships

- 当前规则：`.agents/rules/godot-agent-workflow.md`
- 迁移 provenance：[Godot migration provenance](../plans/godot-migration.md)
- 文档生命周期：[Project Documentation](project-documentation.md)
- 历史工具索引：[Archived Unity Agent Workflow](unity-agent-workflow.md)

# Verification Guidance

未知 API、生命周期、插件或引擎错误必须按 Research Guide 和本地复现取证。自动门禁只报告覆盖到的层级，人工和发布边界必须单独记录。
