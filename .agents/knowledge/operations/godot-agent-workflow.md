---
type: Game System
resource: https://github.com/cty41/tactics
title: Godot agent workflow
description: Godot 4.7 C# 主线的项目、分层、Editor 生命周期、验证和发布边界。
tags: [godot, agent, workflow, testing]
timestamp: "2026-10-27T10:23:30+08:00"
status: active
catalog_scope: godot-agent-workflow
repo_paths:
  - AGENTS.md
  - .agents/rules/godot-agent-workflow.md
  - .agents/skills/godot-workflow
  - .agents/skills/godot-editor-lifecycle
  - .agents/skills/tactics-git-workspace
  - .agents/incidents/godot
  - Tactics.Godot.slnx
  - godot/addons/tactics_tooling
  - Tools/godot/Open-GodotDev.ps1
  - Tools/godot/Verify-GodotProject.ps1
  - Tools/godot/Build-GodotWindows.ps1
  - Tools/migration/manifest/godot-tooling.json
verified_revision: d092a955
source_fingerprint: sha256:7d2c1cf26e153c373612993671f4be6ff03fbaaed41bef10a770f9bc09db35ff
---

# Current State

本机4.7.1已验证ResourceLoader的UID查询可能保留旧正/负缓存；生成器以只读磁盘头部验证ResourceSaver写出的UID，再执行Editor扫描与独立Runtime验证，见`.agents/incidents/godot/headless-resource-uid-cache.md`。不可把内存AddId或一次API返回值当作持久化证据。Act准备阶段现与主目录共用RequiredResourceLoader的CacheMode.Ignore及精确类型诊断；缓存替身测试验证不采信旧缓存对象，但不能据此宣称已修复所有原生runner异常。

Godot 4.7 C# 与 `godot/project.godot` 是唯一产品和编辑权威。Core/Application 保持纯 .NET；Adapter 承载 Node、Resource、文件系统、UI、EditorPlugin 与运行时集成。godot-ai v4.3.1 与 `addons/tactics_tooling` 作为 Editor-only 工具保留在源码树中，但均由 export preset 从游戏 PCK 与 Windows 运行时包排除。godot-ai v4 只允许经过 capability 认证的 `godot-ai attach` stdio bridge，项目级启动固定使用隔离的 public-PyPI uvx 解析策略。godot-ai manifest 是 tag、commit、vendor tree 与 license digest 的单一真相源；显式 refresh 只准备待 review 的升级 diff，日常 Godot 门禁验证 vendor 完整性，依赖清单与第三方声明由独立 public-release 门禁验证。

Editor 采用 Human Direct / Prepared Agent 双入口。人工可以从 Project Manager 直接打开 canonical 项目：GDScript Bootstrap 始终注册 Tactics Main Screen；有 production Adapter assembly 时直接挂载 C# Workbench，无 assembly 时显示显式 `BUILD C# AND RELOAD` 状态页。用户确认后由固定 build-only helper 异步串行 Build、验证 assembly identity并写token化结果；成功优先在同一Editor PID完成reload，失败保留首条错误、日志、复制命令和Retry。prepared启动参数不再作为Controller/Main Screen准入条件，也不能单独授权bridge；controller还必须核对dev-session中的当前PID、canonical路径、派生worktree key、mode及与进程启动时间关联的startedAt，才创建可写authoring named-pipe bridge。Direct Human保留Workbench UI而不暴露Agent bridge。

开发在当前 workspace checkout，不另开 worktree；切分支、无关历史切换和对照旧文件见 `tactics-git-workspace`。Agent、自动化、godot-ai/MCP和严格worktree隔离仍只使用`Tools/godot/Open-GodotDev.ps1`。入口复用`Build-GodotDevAssembly.ps1`，并继续校验Godot/.NET、隔离`user://`、生成项目级Codex Attach配置和记录Editor session。直接人工入口不产生这些Agent/MCP readiness证据。首次生成配置会要求重启一次Codex任务；Agent不得使用共享人工QA用户数据，同worktree的prepared启动与统一verifier继续由同一命名mutex串行化。

Godot 修改先由 `godot-workflow` 路由到最小 Specialist Skill。C#、ResourceSaver、生成器和 reload-sensitive 工作遵循 `godot-editor-lifecycle`；只正常关闭该流程确认的 canonical Editor，并只恢复由本流程关闭的会话。恢复动作本身也必须委托 `Tools/godot/Open-GodotDev.ps1`，不得裸启动 Godot；因此恢复仍会完成 production Adapter 串行 Build、worktree 用户数据、bootstrap 参数、互斥锁和 session 记录。Python 策略测试固定检查 lifecycle 脚本没有重新引入 raw `Start-Process godot --editor --path`。

统一入口 `Tools/godot/Verify-GodotProject.ps1` 串行执行 restore/build、Core/Application/FrozenOracle、Gameplay Spec、Python、Skill/Incident、GdUnit、Release/Runtime/Editor headless、renderer、receipt 与 OKF。FrozenOracle、Golden 和 receipt 只是历史/确定性证据，不能替代视觉、手感、真实 Editor Reload 或干净 Windows 启动。

Windows 构建使用单一 `Windows Desktop` preset、锁定工具链和受审计 staging。Debug/Release 包必须通过架构、PCK/managed runtime、顶层 allowlist、测试/缓存/本地配置排除、manifest/hash 与隔离用户目录启动验证。

# Relationships

- 当前规则：`.agents/rules/godot-agent-workflow.md`
- 迁移 provenance：[Godot migration provenance](../plans/godot-migration.md)
- 文档生命周期：[Project Documentation](project-documentation.md)
- 历史工具索引：[Archived Unity Agent Workflow](unity-agent-workflow.md)

# Verification Guidance

未知 API、生命周期、插件或引擎错误必须按 Research Guide 和本地复现取证。自动门禁只报告覆盖到的层级，人工和发布边界必须单独记录。
