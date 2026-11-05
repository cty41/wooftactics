---
title: Tools/godot 跨平台工具链迁移原则
status: approved-via-migration-plan
authority: principle-approved
owner_decision: Frank（2026-11）
related_pr: https://github.com/cty41/wooftactics/pull/13
implementation_plan: .agents/plans/godot-tooling-cross-platform-migration.md
---

# Tools/godot 跨平台工具链迁移原则

## 文档定位

| 项 | 说明 |
|---|---|
| **状态** | Frank 已批准的方向性原则；**实现按** [跨平台工具链迁移开发计划](../plans/godot-tooling-cross-platform-migration.md) 分刀落地。本文仍**不是**逐行实现合同、不是 OKF 系统概念；脚本与测试行为以仓库代码为准。 |
| **读者** | 负责 Linux／云端 Agent、Verify／Open-GodotDev 演进与 `Tools/gameplay-test-spec` 边界的维护者。 |
| **事实源** | 当前仍以实现为准：`Tools/godot/*.ps1`、`GodotDevSession.psm1`、`Tools/gameplay-test-spec`、`.agents/rules/godot-agent-workflow.md` 与根 `AGENTS.md`。本文描述**目标分层与迁移顺序**；落地须单独 PR、测试与（必要时）计划条目。 |
| **本任务边界** | 记录已批准原则与迁移顺序；具体编排改动见开发计划各刀 PR。**不**替代 TypeScript 规格链；**不**把本文当作 Verify 逐步清单。 |

关联入口：[Godot Agent-first 开发](godot-agent-first-development.md)、[Gameplay Test Framework](gameplay-test-framework.md)、[Godot Agent 工作规约](../rules/godot-agent-workflow.md)。

---

## 1. 分层：Python 编排，TypeScript 规格

Frank 拍板：**编排用 Python，规格留 TypeScript**。不把「第三种脚本语言」或「把 Spec 编译迁到 Python」当作选项。

### Python 负责（跨平台编排层）

- **门禁编排**：按成本递增串行调用 build、测试、迁移脚本、策略检查；统一退出码与步骤标题（Verify 外壳的长期归宿）。
- **会话与进程**：worktree 互斥、Editor 是否占用 canonical 项目、会话 JSON、用户数据目录派生（可与现有 `GodotDevSession.psm1` 并存或逐步迁入，见第 5 节）。
- **PATH 与子进程**：解析 `GODOT_EXECUTABLE`、`.NET` SDK、`node`、仓库根；在 Linux／云端用同一套入口发现工具，而不是假设 Windows 默认盘符路径。
- **薄包装调用**：对已有 Python 门禁（`Tools/migration/*.py`、`Tools/okf/`、`Tools/agent-policy/` 等）用 `subprocess` 显式调用；不复制其业务逻辑到 ps1。

典型归属：`Verify-GodotProject` 的跨平台**外壳**、`Open-GodotDev` 的跨平台**外壳**（Build 仍调用 `dotnet`；Editor 仍由 Godot 二进制启动）。

### TypeScript 继续负责（规格与确定性编译）

- **`Tools/gameplay-test-spec`**：schema、Zod 校验、ScenarioSpec／AuthoringSpec、`validate-*` / `compile-*` / `contract-coverage`、runtime plan 生成与 contract 报告。
- **合同与 Spec 文件**：`*.gameplay-test.md`、authoring JSON、capability 与 fail-closed 语义；见 [Gameplay Test Framework](gameplay-test-framework.md)。
- **调用约定**：Python 编排层**只**通过固定 CLI 调用，例如：

```text
node Tools/gameplay-test-spec/dist/src/cli.js validate-scenario-spec …
node Tools/gameplay-test-spec/dist/src/cli.js compile-scenario-spec --runtime godot …
node Tools/gameplay-test-spec/dist/src/cli.js contract-coverage …
```

不得把 spec 编译、contract 解析或 plan 生成重写为 Python；不得让 Agent 手改 `.plan.json` 绕过 compiler（与现有规约一致）。

### 边界示意

```text
                    ┌─────────────────────────────────────┐
  Agent / CI        │  Python：步骤顺序、进程/PATH、子进程  │
                    └──────────────┬──────────────────────┘
                                   │ subprocess
         ┌─────────────────────────┼─────────────────────────┐
         ▼                         ▼                         ▼
    dotnet build/test      node …/cli.js (TS)          python Tools/… (既有)
         │                         │
         ▼                         ▼
   Core / App / Godot C#     规格 / plan / coverage
```

PowerShell（`.ps1` / `.psm1`）在迁移期仍是 **Windows 主路径上的实现载体**；目标状态是 ps1 **可变薄**为 Windows 友好包装（见第 2、3 节），逻辑与 Linux 共用 Python 编排核心（或共享模块），而不是两套永久分叉的业务规则。

---

## 2. 语言与重写范围

1. **不引入第三种脚本语言**（例如 Bash 版 Verify、Go 版 launcher）作为并列权威；已有 C#、Python、TypeScript、PowerShell 各司其职。
2. **不一次性重写** `Tools/godot/Verify-GodotProject.ps1`（约 1250 行）。按**可审计步骤**从外围向内核抽取：
   - 先抽：进程探测、PATH 解析、步骤调度与日志标题；
   - 后抽：与 Windows 无关的 build/test 子进程序列；
   - 保留在 ps1 或 Windows 脚本内：仅当确实依赖 Win32 API 的步骤（见第 4 节）。
3. **`Open-GodotDev.ps1`** 同理：会话语义与 Build 顺序不变，跨平台部分逐步委托 Python；Windows 上现有参数（`-Mode`、`-UserDataProfile`、`-GodotAiProfile` 等）保持可用。

每步抽取应附带：`Tools/godot/tests/` 或等价策略测试、可回滚 commit、文档更新（必要时 OKF `project-documentation` scope）。

---

## 3. Windows 主开发路径不断裂

| 场景 | 原则 |
|---|---|
| **Windows 日常开发** | 继续可用 `Tools/godot/Open-GodotDev.ps1` 与 `Tools/godot/Verify-GodotProject.ps1`；README／CONTRIBUTING 中的 pwsh 示例在过渡期**仍然有效**。 |
| **Linux／云端 Agent** | 使用**同一套** Python 编排入口（例如未来的 `python -m …` 或 `Tools/godot/verify_godot_project.py`），与 Windows 共享步骤定义；避免「云端只能读 ps1、无法执行」的长期状态。 |
| **渐进双入口** | 允许短期内 Windows 仍默认 ps1、Linux 默认 Python，但**语义必须一致**（同一 mutex 规则、同一 Editor 路径过滤、同一失败即停）。双入口是迁移手段，不是永久产品分叉。 |
| **文档与 Agent** | `AGENTS.md` Quick Reference 在完全切换入口前仍以 ps1 为准；切换时一次性更新 Quick Reference 与 [godot-agent-first-development.md](godot-agent-first-development.md)，并保留 ps1 薄包装说明。 |

---

## 4. Windows-only 能力（显式标注，不假装跨平台）

下列能力**当前或 foreseeable 仍绑定 Windows**；跨平台编排层应跳过、委派 Windows 专用脚本，或在文档／日志中明确 `SKIP (Windows-only)`，而不是 silent fail 或伪造通过。

| 能力 | 典型位置 | 跨平台原则 |
|---|---|---|
| WMI / CIM 进程枚举（历史路径） | 旧版 `Get-CimInstance Win32_Process` | 已由 [PR #13](https://github.com/cty41/wooftactics/pull/13) 方向改为 `/proc`／`ps` 或 Python 等价；Windows 仍可用 CIM |
| **NamedPipe** 与 tooling reload smoke | `Test-TacticsToolingAssemblyReload.ps1` 等 | Linux Verify **不要求**跑通；可选独立 Windows 门禁 |
| **`taskkill` / Win32 进程强杀** | reload smoke、部分 lifecycle 辅助 | Editor 生命周期仍遵循 `godot-editor-lifecycle`：**正常关闭、不强杀**；不为此在 Linux 引入 taskkill 替身 |
| **Windows RC／双 EXE 包 smoke** | `Build-GodotWindows.ps1`、`Test-GodotWindowsPackage.ps1`、`godot-windows-build.yml` | 发布与 RC 证据留在 Windows CI；Linux Verify 不替代 Windows 包验证 |
| **默认 `D:\Godot\…` 等盘符路径** | 部分测试脚本默认值 | 跨平台入口只认 env／显式参数／仓库 manifest，不硬编码盘符 |
| **godot-ai Attach 的 `pythonw.exe` 无窗启动** | `godot-tooling.json`、Open-GodotDev 引导 | 策略仍以 manifest 为准；Linux 云端 Attach 行为单独 documented，不宣称与 Windows 逐字相同 |
| **Cursor MCP guard hook** | `Invoke-CursorMcpGuard.ps1` | 云端 Agent 本就不依赖该 hook；规约已说明 |

任何新步骤加入 Verify 前须标注：**是否必须在 Windows 上才能给出完整产品证据**。

---

## 5. 与 PR #13（进程探测小刀）的衔接

[PR #13](https://github.com/cty41/wooftactics/pull/13) 已确立方向：**Editor 进程列举不必依赖 `Win32_Process`**，且 **`Get-TacticsGodotEditorProcess` 的语义保持不变**：

- 按 canonical `godot/project.godot` 所在项目根过滤（大小写不敏感、`/` 与 `\` 归一化）；
- 进程名仍满足 `Godot*` 类过滤；
- worktree 互斥、Verifier 与 Open-GodotDev 拒绝重叠 Editor／TestHost 的**产品语义**不变。

后续迁移可选路径（原则级，不互斥）：

1. **短期**：进程探测继续留在 `GodotDevSession.psm1`（pwsh 模块），由 ps1 与 Python 共同 `Import-Module` 或 subprocess 调用 pwsh 片段——仅当 Linux 已具备 PR #13 行为。
2. **中期**：将 `Get-TacticsProcessRecords` 同等逻辑迁入 Python（读 `/proc`、Windows 用 CIM 或 `psutil` 等），pwsh 模块变薄 wrapper，**单测**仍覆盖项目路径过滤与「无 Editor 时返回 0」。
3. **禁止**：为了跨平台而放宽「仅当前 worktree 的 canonical 项目」过滤，或允许 Verifier 与 production Editor 并行。

`Tools/godot/tests/test_godot_dev_launcher.py` 一类策略测试应随语义迁移而扩展，而不是删除。

---

## 6. 建议迁移顺序（原则级，可回滚）

每步独立完成 PR；失败时可 revert 单步而不回滚整条产品线。

| 顺序 | 目标 | 完成标志（原则） | 依赖 |
|---|---|---|---|
| **① 进程／PATH 探测** | 统一「Godot／dotnet／node 是否可用」与 Editor 占用检测 | Linux 上 Verify **不再**于脚本加载阶段因 CIM／`python` 未定义而**立即**退出；步骤日志可读到探测结果 | PR #13 类修复；**不在此步强推 python3 shim**（环境项单独决策） |
| **② Verify 关键步骤在 Linux 跑通** | 编排层串行跑 Core/Application 定向测试、TS spec 编译、已有 Python 门禁、可跑的 GdUnit 子集 | 云端可重复执行「与改代码相关」的子集 Verify；Windows-only 步骤显式 SKIP | ①；`node …/cli.js` 已 build |
| **③ Open-GodotDev 会话** | 跨平台 `-NoLaunch` 准备（Build、override.cfg、会话文件、互斥） | Agent 在 Linux 可 `-NoLaunch` 准备 Adapter DLL 与会话；完整 Editor＋godot-ai Attach 仍按 manifest 分平台说明 | ①② 的 PATH／mutex |
| **④ 更厚编排** | 将 Verify 剩余步骤迁入 Python 模块、ps1 只转发 | Windows ps1 行数显著下降；`test_godot_dev_launcher.py`／verifier 集成测试 green | ②③ 稳定 |
| **⑤（可选）Windows 发布链** | 保持独立；不与 ② 混为一谈 | RC／包 smoke 仍在 Windows workflow | 不变 |

**不**在 ② 之前承诺「Linux 上 byte-for-byte 等同 Windows 完整 Verify」。

---

## 7. 硬约束复述（迁移不得削弱）

以下与根 `AGENTS.md`、`.agents/rules/godot-agent-workflow.md` 一致；跨平台工具链 **不得** 通过「方便跑通」绕过：

1. **Godot 资产**：禁止手改或机械直接改 `.tres` / `.tscn`；生成仍走 ResourceSaver、Editor API 或受测转换器。
2. **C# 分层**：Core／Application 不引用 Godot API；工具链改动不引入反向依赖。
3. **远程 CI**：Agent **不主动** `gh workflow run`、不主动重跑 GitHub Actions／远程导出，除非用户或已批准计划明确授权。
4. **验证成本递增**：最小相关测试 → 相关本地门禁 → 产品收口时**至多一次**完整 Verify；纯文档 PR 不借此重跑完整产品 CI。
5. **自动测试 ≠ 人工验收**：Gameplay spec、GdUnit、Verify 绿灯不写入 `manual-acceptance.md` 的 passed；Reload、UI 手感、Dock 可见性仍走 `manual-qa-handoff`。
6. **worktree 与 Editor**：仍只用 canonical `godot/project.godot`；Open-GodotDev 互斥与 SharedManualQA  Human-only 规则不变。

---

## 8. 相关文档与索引

- **开发入口（当前）**：[godot-agent-first-development.md](godot-agent-first-development.md)
- **Spec／合同**：[gameplay-test-framework.md](gameplay-test-framework.md)、[godot-authoring-spec-workflow.md](godot-authoring-spec-workflow.md)
- **策略 manifest**：`Tools/migration/manifest/godot-tooling.json`
- **临时灵感箱**：[brainstorm.md](brainstorm.md)（工具链方向收束后从此处删除条目，历史由 Git 保留）

---

## 变更记录

| 日期 | 说明 |
|---|---|
| 2026-11-05 | 初稿：Frank 拍板「Python 编排、TS 规格」原则；文档-only，无脚本行为变更。 |
| 2026-11-05 | 原则 status 升为已批准；实现跟踪 [godot-tooling-cross-platform-migration 开发计划](../plans/godot-tooling-cross-platform-migration.md)。[#14](https://github.com/cty41/wooftactics/pull/14) 原则、[#13](https://github.com/cty41/wooftactics/pull/13) 进程探测、[#15](https://github.com/cty41/wooftactics/pull/15) 开发计划已合入 `feat/all-tilemap`。 |
