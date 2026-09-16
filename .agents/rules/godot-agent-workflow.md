# Godot Agent 工作规约

## 项目与工作区

- 唯一 Godot 项目是 `godot/project.godot`。不得为测试、插件或单个迁移批次再创建 `project.godot`。
- 远程 `main` 是 Godot 产品与治理权威；复用用户指定的现有 worktree，除非计划或用户明确要求，不创建、删除或切换 worktree。主线切换期间现有 `migration/godot` worktree 可以继续承载同一 tracked tree，但分支名不再定义产品权威。
- `unity-final-2026-08-08` 与 Frozen Oracle 是只读历史证据。当前树不包含 Unity 工程；需要取证时从最终 Tag 或临时归档分支建立隔离的只读检出，不在 Godot 主线恢复 Unity 运行时。

## C# 分层与验证

- `Tactics.Core` 和 `Tactics.Application` 禁止引用 Unity、Godot、Editor API 或迁移 DTO。
- Godot Node、Resource、UID 与 Editor 类型只能存在于 Godot Adapter/Editor 层。
- 修改 Godot/Core/Application C# 后必须 Build，并先运行直接覆盖改动的最小 NUnit、GdUnit、headless 或项目工具测试；产品代码达到收口点时，至多运行一次 `Tools/godot/Verify-GodotProject.ps1` 串行收口。纯文档、人工账本或 PR 描述变更只运行对应轻量策略/文档门禁，不重跑完整产品 verifier。
- 验证默认仅在本地执行。未经用户明确要求或已批准计划逐项授权，不得主动触发、重跑或等待 GitHub Actions、`workflow_dispatch`、远程 RC/Windows 导出、制品打包或其他同类高耗时托管任务。仓库平台自行触发的 required check 可以读取结果；若合并政策需要但未自动触发，则报告缺少的远程证据并等待授权。
- 禁止并行执行共享 Core 输出的 Godot/Core 构建或测试；已验证会争抢 `obj` 文件。
- GdUnit、godot-ai、EditorPlugin、测试和迁移 DTO 不得成为 Release 运行时依赖。

## Godot 资产

- 禁止人工或机械直接修改 `.tres`/`.tscn` 文本。必须使用 ResourceSaver、PackedScene、Editor API 或受测转换器。
- Catalog entry 必须包含严格小写 `ContentId`、`ResourceTypeId`、`uid://`、诊断路径和 `SchemaVersion`。
- Unity GUID/LocalFileId 只定位源资产；Godot UID 只定位目标资源；两者都不替代业务 `ContentId`。
- 迁移 DTO 只服务导出、校验和报告，不能成为 Godot Runtime 输入。

## EditorPlugin 生命周期

- EditorPlugin 使用 `[Tool]` 与 `#if TOOLS`；在 `_EnterTree` 注册，在 `_ExitTree` 对称清理。
- 退出、异常和 assembly reload 路径必须断开信号、取消预览、移除 Dock、释放 SubViewport/临时对象并清空引用。
- Headless 初始化不等于 GraphEdit、Undo/Redo、SubViewport 或视觉验收；这些按计划保留人工门禁。

## godot-ai 项目级 MCP

- Godot Editor 只通过 `Tools/godot/Open-GodotDev.ps1` 启动。入口始终串行增量 Build production Adapter，校验程序集身份，并以 worktree 路径派生互斥锁、用户数据目录和会话记录；禁止把直接双击 Editor 作为 Agent 支持流程。
- godot-ai v3.1.2 源码固定在 `godot/addons/godot_ai`，允许进入公开源码根，但必须从 PCK/Windows 运行时包排除。Dock 自更新不受支持；升级只能通过固定 Tag/commit/hash 的显式 vendor refresh、许可证审计和完整门禁。
- godot-ai 只允许由包含 canonical `godot/project.godot` 的当前项目 worktree 中、被 Git 忽略的 `.codex/config.toml` 加载；用户级 `~/.codex/config.toml` 不得长期保留 godot-ai 表，也不得影响历史 Unity 检出或其他项目。
- 固定使用 `godot-ai==3.1.2` 的 Windows Attach 启动方式：绝对路径 `pythonw.exe`、无窗口 bootstrap、HTTP 8000、WebSocket 9500。配置以 `Tools/migration/manifest/godot-tooling.json` 为策略真相源。
- 首次启动由统一入口运行 `Sync-GodotAiCodexConfig.ps1 -Bootstrap` 生成项目配置，并输出 `CODEX_RESTART_REQUIRED`；重启一次当前 worktree 根目录的 Codex 任务。后续用 `-Check` 验证，用 `-Profile <name>` 切换累积白名单。
- Agent 只允许 `Worktree` 用户数据；固定 `SharedManualQA` 数据只允许 `-Mode Human` 显式选择。一个 worktree 同时只允许一个 Editor/Agent 写会话；多个 worktree 可共享兼容的 8000/9500 Attach backend，但每次写入仍必须核对 session 的项目路径。
- 更换 godot-ai 版本、端口、启动方式或工具 Profile 后，必须重新生成/同步配置并重启根目录为该 canonical Godot worktree 的 Codex 任务。
- 已授权 Godot 修改任务需要 session 为 `0` 时，必须使用 `godot-editor-lifecycle` 对唯一 canonical PID 做正常关闭，并且只在 Editor 原本打开时恢复；超时不强杀、不继续写入。
- MCP 写操作前必须先调用 Session 与 Editor 状态接口，确认仅有一个会话、Godot 版本为 4.7.1，且项目指向当前 worktree 的 `godot/project.godot`；不满足时停止写入。
- Profile 按 `phase3-observe → content-authoring → ui-input → presentation` 累积扩展。未进入对应迁移阶段，不得提前扩大工具面。
- 始终禁用 `script_create`、`script_attach`、`script_patch`、`filesystem_manage`、`client_manage` 和 `autoload_manage`。生产 C# 使用 `apply_patch` 与 Build；Godot 资产使用 ResourceSaver、Editor API 或受测转换器。
- MCP 只承担 Editor/Scene/Resource 观察、运行、日志、截图和已批准的重复编辑；不得成为 Runtime、Catalog、ContentId、迁移台账、typed ChangeSet 或测试真相源。

## 研究与证据

- 未知 Godot API、生命周期、C#/GDScript 差异、版本差异、插件与引擎错误必须使用 `godot-workflow` 的 Research Guide。
- 单个社区帖子、旧版本回答、未合并 PR/proposal 不能写成项目事实。
- 社区 workaround 采用前必须由官方资料、源码或本地精确版本复现至少交叉验证一项。
- 研究结论使用 `verified_local`、`official_docs`、`upstream_source`、`upstream_open`、`community_lead`、`inference` 标记。

## Incident 与知识晋升

- 新引擎/工具链踩坑先写 `.agents/incidents/godot/`，状态从 `observed` 到 `reproduced`、`verified` 或 `superseded`。
- 修复并有证据后，摘要才能进入 OKF；同一流程被重复使用后，才能修改 Skill。
- 普通语法错误、一次误操作或未验证猜测不进入 OKF，也不得升级为永久 Rule。
