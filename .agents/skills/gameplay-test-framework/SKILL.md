---
name: gameplay-test-framework
description: "Use when generating, validating, compiling, or running gameplay automation tests from natural language, design docs, or batch templates — guides agents through Tools/gameplay-test-spec and the Godot runtime runner"
---

# Gameplay Test Framework

使用 Agent-first Spec 工具链创建和运行 Gameplay 自动化测试。能力与命令以 `Tools/gameplay-test-spec` 和 Godot runtime runner 为准，系统概览见 `../../docs/gameplay-test-framework.md`。

## Quick Reference

| 阶段 | 入口 |
|---|---|
| 生成/编写 | `*.gameplay-test.md` 或 `ScenarioDraft` |
| 校验 | `validate-spec -s <spec>` |
| 编译 | `compile-spec -s <spec> -o <plan>` |
| 批处理 | `batch-validate -d <dir>` / `batch-compile -d <dir> -o <dir>` |
| 执行 | Godot `GodotGameplayRuntimeRunner` |
| 合同 | `validate-contracts -d <doc-or-dir>` / `contract-coverage --docs <dir> --specs <dir>` |

## When to use

- 将需求或设计文档转成可执行 Gameplay 测试。
- 添加或修改 Skill、Battle、Map、UI adapter 场景。
- 排查 Spec 校验、编译或 Godot 运行失败。
- 用真实资产回归技能、战斗或 Roguelike 行为。

## Workflow

### 1. 先核对支持面

action/assertion 支持面以 `Tools/gameplay-test-spec/src` 当前 schema/adapter 与 runner 为准。

当前 adapter 包括 Skill、Battle、Map、UI；它们按场景需要组合，不存在必须先完成某一 adapter 才能使用其他 adapter 的阶段限制。

### 2. 创建源 Spec

- Agent 将自然语言收束成受控 `ScenarioSpec`/`ScenarioDraft`。
- 优先维护 `*.gameplay-test.md`，不要手写 `.plan.json`。
- 需要真实 ScriptableObject 语义时使用项目已有真实资产 setup；框架自身单元测试才使用最小测试世界。
- 一个场景证明一个清晰行为，别用日志字符串代替状态断言。

### 3. 校验与编译

在仓库根目录执行：

```powershell
npm --prefix Tools/gameplay-test-spec test
node Tools/gameplay-test-spec/dist/src/cli.js validate-spec -s <scenario.gameplay-test.md>
node Tools/gameplay-test-spec/dist/src/cli.js compile-spec -s <scenario.gameplay-test.md> -o <scenario.plan.json>
```

批处理：

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js batch-validate -d <spec-directory>
node Tools/gameplay-test-spec/dist/src/cli.js batch-compile -d <spec-directory> -o <output-directory>
```

CLI 默认 runtime 为 Godot；`--runtime unity` 只保留给冻结兼容夹具，不用于新场景。

### 3a. 设计合同与可选 Ollama

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js validate-contracts -d .agents/docs
node Tools/gameplay-test-spec/dist/src/cli.js contract-coverage --docs .agents/docs --specs Tests/gameplay-specs
node Tools/gameplay-test-spec/dist/src/cli.js provider-doctor
node Tools/gameplay-test-spec/dist/src/cli.js extract-contracts -d <design.md> -o <candidates.json>
node Tools/gameplay-test-spec/dist/src/cli.js generate-drafts -d <design.md> -c <CONTRACT-ID> -o <draft.json>
```

- 只有 ``gameplay-contract`` fenced block 是权威合同；模型候选不是。
- 默认 provider 从 `%LOCALAPPDATA%\Tactics\gameplay-test-spec\providers.json` 读取；当前正式目标为 OpenCode Go `deepseek-v4-flash`。
- Key 只存在同目录的 `secrets.json`，该文件 ACL 只能授权当前用户、SYSTEM 或 Administrators；不得把 Key 放入仓库、命令参数、日志或对话。
- 每次在线进程调用 `/models` 发现 exact model；缺模型、鉴权/配额、超时、截断或协议错误一律 fail-closed，不自动回退。
- 本地调试可显式传 `--provider ollama --host http://127.0.0.1:11434 --model qwen3.5:2b`。
- 模型输出一律经 `validate-draft` / `compile-draft` 验证后才进入编译。
- 新角色、技能、状态和机制设计先使用 `gameplay-design-constraints` Skill 确认相关 Contract ID。

首次配置：复制 `Tools/gameplay-test-spec/examples/providers.example.json` 和 `secrets.example.json` 到上述用户目录，写入已重新生成且未暴露的 Key，然后用 PowerShell 为 secrets 撤销继承并仅授予当前用户 FullControl：

```powershell
$secretPath = Join-Path $env:LOCALAPPDATA 'Tactics\gameplay-test-spec\secrets.json'
$acl = Get-Acl -LiteralPath $secretPath
$acl.SetAccessRuleProtection($true, $false)
$rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
  [System.Security.Principal.WindowsIdentity]::GetCurrent().Name,
  [System.Security.AccessControl.FileSystemRights]::FullControl,
  [System.Security.AccessControl.AccessControlType]::Allow)
$acl.SetAccessRule($rule)
Set-Acl -LiteralPath $secretPath -AclObject $acl
node Tools/gameplay-test-spec/dist/src/cli.js provider-doctor
```

修改 TypeScript 后先按 package scripts 构建。校验失败时修复 Spec 或 schema，由 compiler 重新生成计划。

### 4. Godot 执行

- Godot v2 plan 必须声明 `runtime: Godot`、能力、adapter、隔离存档、watchdog 和可选 validated checkpoint。
- Runner 加载正式 `Main.tscn`，玩家动作通过 `Viewport.PushInput` 进入生产输入链，不直接调用业务服务证明输入成功。
- 每个场景只写 `user://qa-runner/<scenario>/<attempt>/`；执行前后必须证明生产主档和 backup 未变化。
- 批量结果写入 `artifacts/gameplay-specs/godot/godot-gameplay-spec-result-v1.json`，并由统一迁移门禁校验。

### 5. 测试升级与 Job 所有权

- 先运行能精确命中目标行为的最小测试，并确认测试数非零；再升级到相关 fixture、相关门禁和统一 verifier。`total=0` 是无效运行，不是通过。
- 涉及输入、场景、缓存 UI、异步状态或 reload 时，相关 fixture 首次通过后立即连续运行第二轮，检查残留状态与顺序依赖。
- 下游首次出现新失败时停止扩围，先取得该失败的最小复现和 GREEN，再从最早受影响的后续门禁继续；不要盲目重跑已证明不受影响的前置门禁。
- 同一 canonical Godot Editor、runner 和 verifier 同时只能有一个 mutating job owner；build、Editor、GdUnit 和统一 verifier 按项目 mutex 串行，独立只读分析可以并行。
- 超过 60 秒的任务报告阶段、进度和最近更新时间。自最后可验证进展起 90 秒无变化时检查进程、runner 与日志；只有确认卡死后才停止当前 job，并只恢复当前门禁。
- 当前任务引入的失败必须修复。疑似基线失败需有基线复现或权威记录，并按用户许可或仓库政策决定是否继续。

## Agent-first SkillGraph 辅助命令

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js generate-skill-graph-spec -t "<技能描述>" -o <skill-spec.json>
node Tools/gameplay-test-spec/dist/src/cli.js generate-test-from-spec -s <skill-spec.json> -o <scenario.gameplay-test.md>
```

`generate-spec` 仅辅助产出草案；仍需 Agent 收束与人工确认。

## Anti-patterns

| 错误 | 正确 |
|---|---|
| 手写 `.plan.json` | 维护 Spec 并由 compiler 生成 |
| 依赖旧“Phase”支持矩阵 | 读取当前 schema、adapter 和测试 |
| 用轻量测试替身证明真实资产行为 | 显式加载真实资产 |
| 跳过 validate | 先校验再编译 |
| 只看最终日志 | 使用专用状态断言 |
| `total=0` 当作通过 | 修正发现或筛选并取得非零测试数 |
| 多个任务竞争同一 Editor/runner | 指定唯一 mutating job owner 并串行执行 |
| focused 门禁失败后继续扩围 | 先取得最小复现和 GREEN，再恢复升级 |
| 没有进度证据就反复等待 | 90 秒检查进程、runner 和日志，确认卡死后恢复当前门禁 |

## Checklist

- [ ] 已核对当前 schema 与 adapter 支持面。
- [ ] 源 Spec 清晰且进入版本控制。
- [ ] TS 测试、validate 和 compile 通过。
- [ ] Godot 使用需要的真实 Resource 与 adapter 执行。
- [ ] 失败信息能定位到具体步骤/断言。
- [ ] 测试数非零，并按最小测试、fixture、相关门禁、统一 verifier 的顺序升级。
- [ ] 同一 Editor、runner 和 verifier 只有一个 mutating job owner；长任务有可验证进度记录。
