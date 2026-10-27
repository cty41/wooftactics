# godot-ai 边界

当前使用 v4.3.1 通用接口；`script_*` 在 4.2+ 可接受 `.cs`，仍保持禁止，C# 一律走项目代码与 Build 门禁。版本、端口、Profile 与精确工具集合以 `Tools/migration/manifest/godot-tooling.json` 为准，项目配置由 `Tools/godot/Sync-GodotAiCodexConfig.ps1` 生成和检查。

## 分阶段白名单

| Profile | 新增职责 | 典型用途 |
|---|---|---|
| `phase3-observe` | Session/Editor 状态、Scene/Resource 读取、运行、日志、截图、插件 reload、MCP smoke | Poison Spear 自动观察与证据收集 |
| `content-authoring` | Node/Scene 创建、属性修改、Signal、批处理与保存 | Unit/Buff/Skill 重复结构编辑 |
| `ui-input` | InputMap、UI、Theme、运行时输入 | UI/Input 批次 |
| `presentation` | Animation、Material、Particle、Audio、Camera | Presentation/VFX/Audio 批次 |

Profile 累积继承前一阶段；不得跳过当前迁移阶段提前扩权。任何写操作都先以 `session_manage` 和 `editor_state` 核对唯一 Session、Godot 4.7.1 与 canonical 项目根。

## 永久禁用

- `script_create`、`script_attach`、`script_patch`：生产代码保持 C#、`apply_patch`、Build 和代码审查链路。
- `filesystem_manage`：不得绕过 ResourceSaver、转换器和内容冲突保护。
- `client_manage`：MCP 不得修改自身或用户级 Codex 配置。
- `autoload_manage`：只有后续架构计划明确批准后才可重新评估。

Cursor 没有 Codex 的 `enabled_tools` profile 白名单，只靠项目级 `.cursor/hooks.json` 的 `beforeMCPExecution` guard（`Tools/godot/Invoke-CursorMcpGuard.ps1`）硬拦上述 6 个工具（名单来自 manifest `alwaysForbiddenTools`）。该 hook 只作用于 Agent Chat，需要工作区受信任；云端 agent 不支持。

不得通过 godot-ai 定义 Core 规则、ContentId、迁移台账、跨资产事务或最终测试真相。领域操作优先实现为受测 C# Application Service/CLI；custom tool 只有在 Revision、typed ChangeSet、原子 Undo 和失败清理稳定后再评估。
