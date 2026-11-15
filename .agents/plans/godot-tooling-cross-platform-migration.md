# Tools/godot 跨平台工具链迁移 — 开发计划

> 本文自包含，供远端 Agent 读取并执行。仓库：`cty41/wooftactics`，分支基线 `feat/all-tilemap`。
> 输出语言：计划正文用中文；路径、命令、标识符保持仓库原样。
> **权威原则**（Frank 已确认）：见 [Tools/godot 跨平台工具链迁移原则](../docs/godot-tooling-cross-platform-migration.md)。若该文件尚未合入当前基线，以 [PR #14](https://github.com/cty41/wooftactics/pull/14) 分支 `cursor/godot-tooling-cross-platform-migration-892a` 上的全文为准；本计划是对原则 §6 迁移顺序的可执行展开，**不**另发明方向。

## 人话版

**一句话**：把 `Tools/godot` 里 Verify／Open-GodotDev 从「只能在 Windows + pwsh 上完整跑」逐步改成「Linux／云端也能跑与改代码相关的门禁子集」，编排归 Python、规格仍归 TypeScript，Windows 日常开发路径不断裂。

**为什么**：云端 Agent 与 Linux CI 已在 PR [#13](https://github.com/cty41/wooftactics/pull/13) 上越过 CIM 阻塞，但 Verify 仍因硬编码 `python`、NamedPipe／taskkill 等卡在下一步；需要按 Frank 拍板的分层与刀序小步落地，避免一次性重写 ~1250 行 Verify 或把 Spec 链迁到 Python。

**步骤白话**：先合原则文档与进程探测 PR → 让 Linux 能解析到 Python 解释器 → 在 Linux 跑通 Verify 的可移植步骤并显式 SKIP Windows-only → 可选把进程探测迁 Python → 让 `-NoLaunch` 跨平台可用 → 最后把 Verify 步骤搬进 Python 模块、ps1 变薄。Windows RC／包 smoke、reload NamedPipe 替身不在本计划里做。

**怎样算完成**：各刀独立小 PR、可 revert；Linux 云端能重复执行「与任务相关的」Verify 子集；Windows 行为与互斥语义不变；原则文档 status 与实现状态一致；本计划文件存在且与原则 §6 顺序一致。

**易踩坑**：不要全局加 `python`→`python3` shim 除非 Frank 单独决策；不要为跑通 Linux 放宽 canonical 项目 Editor 互斥；不要把 contract-coverage（[#12](https://github.com/cty41/wooftactics/pull/12)）与跨平台编排混在一个 PR；纯文档 PR 不要借机重跑完整 Verify／GitHub Actions。

---

## Summary

| 项 | 内容 |
|---|---|
| **目标** | 按原则实现「Python 编排 + TS 规格」的跨平台 `Tools/godot` 入口，Linux／云端可跑 Verify 关键子集与 Open-GodotDev `-NoLaunch` 准备。 |
| **非目标** | byte-for-byte 等同 Windows 完整 Verify；Spec／`Tools/gameplay-test-spec` 迁 Python；Windows RC／包 smoke 重构；NamedPipe reload 的 Linux 替身；本计划 PR 本身不改任何 `.ps1` 行为。 |
| **依赖 PR** | [#14 原则文档](https://github.com/cty41/wooftactics/pull/14)（discussion-draft → 合入后升「已批准，实现按本计划」）；[#13 进程探测](https://github.com/cty41/wooftactics/pull/13)（OPEN，刀 1 前须合入或 cherry-pick）；[#12 contract-coverage](https://github.com/cty41/wooftactics/pull/12)（正交，Verify 步骤可能后续并列，不阻塞刀 1–2）。 |

---

## 1. 项目背景

- 产品主线：Godot 4.7 C# 三职业 Pure Run；唯一工程 `godot/project.godot`。
- Agent 默认门禁：`Tools/godot/Verify-GodotProject.ps1`（约 1250 行）、开发入口 `Tools/godot/Open-GodotDev.ps1` + `GodotDevSession.psm1`。
- 规格链：`Tools/gameplay-test-spec`（TypeScript），Verify 通过 `node Tools/gameplay-test-spec/dist/src/cli.js …` 调用；Python 只编排子进程，不复制 compiler 逻辑。
- Frank 拍板分层与迁移顺序见原则文档 §1、§6；本计划只描述**如何拆 PR 落地**。

关联入口：[Godot Agent-first 开发](../docs/godot-agent-first-development.md)、[Gameplay Test Framework](../docs/gameplay-test-framework.md)、技能 `.agents/skills/gameplay-test-framework/SKILL.md`、`.agents/skills/godot-workflow/SKILL.md`。

---

## 2. 硬性约束（违反即失败）

与根 `AGENTS.md`、原则 §7 一致；跨平台便利**不得**削弱：

1. 禁止手写或机械直接改 `.tres` / `.tscn`；生成走 ResourceSaver、Editor API 或受测转换器。
2. `Tactics.Core` / `Tactics.Application` 不得引用 Godot API。
3. 不得覆盖、暂存、清理不属于当前任务的 dirty worktree 文件。
4. 不得主动 `gh workflow run`、远程导出或同类高耗时 CI；验证成本递增：最小相关测试 → 相关本地门禁 → 产品收口时**至多一次**完整 Verify。
5. 自动测试绿灯 ≠ 人工验收；Reload、UI、Dock 等仍走 `manual-qa-handoff`。
6. canonical 项目过滤与 Verifier／Open-GodotDev Editor 互斥语义不变（与 [#13](https://github.com/cty41/wooftactics/pull/13) 一致）。
7. 提交前精确 staged scope、`git diff --cached --check`；改 `catalog-scopes.yaml` 监控范围时走 OKF impact／sync。

---

## 3. 现状（已用代码核对）

### 3.1 `Tools/godot` 脚本与模块

| 路径 | 角色 |
|---|---|
| `Verify-GodotProject.ps1` | 统一验证：Editor 互斥、`dotnet test/build`、大量 `python …` 与 `powershell …` 子进程、`node …/cli.js` gameplay spec、GdUnit 隔离 host、迁移 generation 链等。 |
| `Open-GodotDev.ps1` | Build Adapter、`-Bootstrap`、`-NoLaunch`、会话 JSON、`override.cfg`、启动 Editor；依赖 `GodotDevSession.psm1`。 |
| `GodotDevSession.psm1` | worktree 互斥锁、`Get-TacticsGodotEditorProcess`（**基线**仍 `Get-CimInstance Win32_Process`；[#13](https://github.com/cty41/wooftactics/pull/13) 提供 `/proc` 小刀）。 |
| `Test-TacticsToolingAssemblyReload.ps1` | NamedPipe、`taskkill`、默认 `D:\Godot\…` — Windows-only reload smoke。 |
| `Build-GodotWindows.ps1` / `Test-GodotWindowsPackage.ps1` | Windows RC／包验证，独立 workflow。 |
| `Sync-GodotAiCodexConfig.ps1` | `-Check` 经 Verify 调用；内部 `python`。 |

Verify 入口行为要点（`feat/all-tilemap` 核对）：

- 加载 `GodotDevSession.psm1` 后 `Get-TacticsGodotEditorProcess`；有 Editor 则拒绝验证。
- 默认 `$GodotExecutable` 为 Windows 盘符路径；Linux 须 `-GodotExecutable` 显式传入。
- 子进程大量硬编码 `python`（如 `Tools/migration/godot_ai_vendor.py` 为首步之一）与 `powershell -File …`（如 `validate-skills.ps1`、Codex sync）。
- Gameplay spec：`node Tools/gameplay-test-spec/dist/src/cli.js`（需事先 `npm run build` 或 CI 等价物）。
- 末尾调用 `python -m unittest discover -s Tools/godot/tests`（`test_godot_dev_launcher.py` 等为**策略只读**测试，读 ps1/psm1 文本）。

### 3.2 `Tools/gameplay-test-spec`

- TypeScript 全链：validate／compile／`contract-coverage` 等；Python **不得**重写。
- [#12](https://github.com/cty41/wooftactics/pull/12) 拟将 `contract-coverage` 接入 Verify — 与跨平台编排正交，但 Verify 步骤列表会变长；刀 2 设计 SKIP／步骤表时留扩展位。

### 3.3 已知 Windows-only 运行时（非 Verify 主路径，记录备查）

- `.agents/skills/godot-editor-lifecycle/scripts/Invoke-GodotEditorLifecycle.ps1`：`Get-CimInstance Win32_Process`。
- `godot/addons/godot_ai/utils/port_resolver.gd`：Attach 路径内嵌 PowerShell CIM。

---

## 4. 已确认缺口（按严重度）

| 严重度 | 缺口 | 证据／影响 |
|---|---|---|
| **阻塞** | Linux PATH 无 `python`，Verify 在 godot-ai vendor 等步失败 | [#13](https://github.com/cty41/wooftactics/pull/13) 云端报告；Verify 全文 `python` 调用（50+ 处） |
| **阻塞** | 基线 `GodotDevSession.psm1` 仍 CIM-only | 未合 #13 前 Linux 脚本加载即失败 |
| **高** | `powershell` 硬编码步骤在 Linux 无等价或需 pwsh + 仍 Windows 向 | Verify Codex sync、`validate-skills.ps1` 等 |
| **高** | reload smoke：NamedPipe / `taskkill` / 默认 `D:\Godot` | `Test-TacticsToolingAssemblyReload.ps1` |
| **中** | 无 Linux 默认的 Python 编排入口；云端只能读 ps1 | 原则 §3 渐进双入口 |
| **中** | Editor lifecycle 技能、godot_ai `port_resolver.gd` CIM | 不影响 Verify 刀 1–2，但影响 Attach／lifecycle 文档 |
| **低** | `test_godot_dev_launcher.py` 仅断言 ps1 文本，未覆盖跨平台探测实现 | 刀 3／5 须扩展 |

---

## 5. 目标与非目标

### 5.1 目标

1. Linux／云端 Agent 能通过**共享语义**的编排层执行与改动相关的 Verify **子集**（dotnet 定向测、TS cli、已有 Python 门禁、可跑 GdUnit 子集）。
2. Windows 继续以 `Verify-GodotProject.ps1` / `Open-GodotDev.ps1` 为主路径；迁移期允许双入口，**mutex／过滤／失败即停**一致。
3. Python 编排层解析 `GODOT_EXECUTABLE`、dotnet、node、**Python 解释器**；TS 仍只经 `node …/cli.js`。
4. Windows-only 步骤日志显式 `SKIP (Windows-only): <reason>`，不 silent fail、不伪造通过。

### 5.2 非目标

- Linux 上完整 Verify 与 Windows **逐字等价**（含 MaLiang 可选链、全量 migration generation、reload smoke、Windows RC）。
- 将 `Tools/gameplay-test-spec` 或 contract 解析迁到 Python。
- 在本迁移计划内重构 `godot-windows-build.yml` 或 RC 发布链（原则 §6 ⑤ 保持独立）。
- 为 Linux 实现 NamedPipe／taskkill 替身；Editor 仍遵循 `godot-editor-lifecycle` **正常关闭、不强杀**。
- **全局** `python`→`python3` 系统 shim（见刀 1 决策点）。

---

## 6. 建议任务（按顺序，可回滚的独立小 PR）

原则 §6 顺序：**① 进程／PATH → ② Verify Linux 子集 → ③ Open-GodotDev 会话 → ④ 更厚编排 → ⑤（可选）Windows 发布链**。下列「刀」与之一一对应；**禁止颠倒顺序**（除非 Frank 书面调整原则）。

---

### 刀 0 / 前置：文档与依赖 PR

**目的**：原则 status 与实现挂钩；执行刀 1 前代码依赖就绪。

**改哪些路径**

- 合入或 rebase：[PR #14](https://github.com/cty41/wooftactics/pull/14)（`.agents/docs/godot-tooling-cross-platform-migration.md`）。
- 合入或 cherry-pick：[PR #13](https://github.com/cty41/wooftactics/pull/13)（`GodotDevSession.psm1`、`Test-TacticsToolingAssemblyReload.ps1` 探测部分）。
- PR #14 合入后：原则 front matter `status` 从 `discussion-draft` 改为说明「Frank 已批准；实现按 `.agents/plans/godot-tooling-cross-platform-migration.md`」；原则 §变更记录增加一行指向本计划（**仅文档措辞**）。
- 本计划已在 `.agents/plans/`；无需放入 `.agents/knowledge/plans/`。

**怎么测**

- 纯文档／合并 PR：`git diff --cached --check`。
- 确认 #13 合入后 Linux 上 `Import-Module GodotDevSession; Get-TacticsGodotEditorProcess -ProjectRoot (Resolve-Path godot)` 不抛 CIM 错误。

**完成标志**

- 原则文档在 `feat/all-tilemap`（或执行分支）可读；#13 行为在目标环境可用。
- 执行者明确：**未合 #13 不得开刀 1 的 Verify 实跑验证**（刀 1 可先做 python 解析，但端到端仍依赖 #13）。

**Windows-only SKIP**

- 无。

**禁止事项**

- 在本刀合并原则时改 Verify／Open-GodotDev 行为（除 #13 自身 diff）。
- 假定 #12 已合入；contract-coverage 接入单独跟进。

---

### 刀 1：PATH / `python` 解释器解析小刀

**对应原则**：§6 ①（PATH 探测的一部分；与 #13 进程探测并列，可同 PR 或紧接小 PR）。

**目的**：Verify（及同类调用点）在 Linux 上能找到 Python，**越过「python 未在 PATH」**进入后续步骤；Windows 行为不变。

**改哪些路径（建议，执行前只读核对调用点）**

- 新增小型 **Python 编排辅助模块**（例如 `Tools/godot/lib/resolve_python.py` 或等价，命名以仓库惯例为准），职责：
  - 解析顺序建议：`PYTHON` 环境变量 → `python3` → `python`（与 manylinux／Ubuntu 云镜像一致）；
  - 提供单一函数返回可执行 argv 列表，供后续模块与测试使用。
- **最小侵入**：先在 Verify 调用链**最靠前**的 python 步（如 `godot_ai_vendor.py`）改为通过解析结果调用；或引入薄 wrapper 函数于 ps1 顶部 `$python = …`（若仍走 ps1 主路径）— **二选一由 implementer 在 PR 描述中说明，默认优先编排层解析，不修改系统 PATH**。
- 同步扫：`Sync-GodotAiCodexConfig.ps1` 内 `& python`、Open-GodotDev 若存在 python 调用（只读核对）。

**怎么测**

- Linux：`pwsh Tools/godot/Verify-GodotProject.ps1 -GodotExecutable <linux godot>` — 完成标志为**越过** `godot_ai_vendor.py` 步（允许在更后步骤失败）。
- Windows：同命令或默认 Godot 路径，确认 vendor 步仍成功、无回归。
- 单元测试：对 `resolve_python` 做 table-driven 测试（mock PATH 或使用 `PYTHON=` 覆盖）。

**完成标志**

- 云端 Verify 不再因 **`python: command not found`** 在 vendor 步立即失败。
- Windows 未强制要求 `python3` 别名。

**Windows-only SKIP**

- 无。

**禁止事项**

- **未经 Frank 决策**不得：仓库外全局 shim、Docker 镜像层 `update-alternatives`、或 CI 秘密改 PATH 作为唯一方案（可作为环境文档建议，但与代码解析方案分离决策）。
- 不把 Verify 全文件 50+ 处 `python` 一次性机械替换在本刀做完（本刀只保证「能进入后续步骤」的最小刀；剩余调用点列入刀 2／5）。

**决策点（须记录）**

- 若团队选择在 CI 镜像统一提供 `python`→`python3` symlink **且** Frank 批准，刀 1 可收窄为「文档 + 检测」；默认实现仍为**编排层解析**。

---

### 刀 2：Verify 关键可移植步骤在 Linux 跑通子集

**对应原则**：§6 ②。

**目的**：编排层串行跑 Core/Application 定向 `dotnet test`、已 build 的 `node …/cli.js`、已有 Python 门禁、`Invoke-IsolatedGdUnitSuite` 可跑子集；Windows-only 显式 SKIP。

**改哪些路径**

- 步骤清单（维护为数据或 Python 模块，避免复制业务规则）：
  - **可移植**：solution build、Core/App 过滤测试、gameplay-test-spec cli（validate／compile 子集）、`Tools/agent-policy`／`Tools/migration` 中已在 Linux 跑过的 unittest、GdUnit 隔离 host（需 `GODOT_BIN`）。
  - **SKIP 标注**：`Test-TacticsToolingAssemblyReload.ps1` 整段；`Invoke-CursorMcpGuard.ps1`；Windows RC scripts；MaLiang 未初始化时已有 warning SKIP；`validate-skills.ps1` 若仅 Windows pwsh 策略 — `SKIP (Windows-only): …`；Codex `-Check` 若 Linux 无 local config 保持现有 skip 语义。
- 扩展 Verify 或新增 **Linux 入口**（例如 `Tools/godot/verify_godot_project.py` 调用共享步骤表），ps1 在 Windows 仍可为主入口；语义与 mutex 与现 ps1 一致。
- 硬编码 `$GodotExecutable` 默认值：Linux 入口不得 require `D:\Godot\…`；必须 env／参数（与原则 §4 一致）。

**怎么测**

- Linux：跑「子集模式」或带 `-SkipWindowsOnly` 类开关（命名 implementer 定，须文档化）至第一个预期 SKIP 之后仍 green 的步骤链。
- `python -m unittest discover -s Tools/godot/tests` green；新增步骤表／SKIP 日志的单元测试。
- Windows：完整 ps1 路径不因 SKIP 逻辑而跳过本应在 Windows 跑的步骤。

**完成标志**

- 云端可**重复**执行 documented 子集（CI／Agent 文档写清命令与环境变量）。
- 日志中出现可 grep 的 `SKIP (Windows-only)`，无 silent pass。
- **不要求** Linux 完整 Verify 全绿。

**Windows-only SKIP 约定**

- 统一前缀：`SKIP (Windows-only): <step name> — <one-line reason>`（Write-Warning 或编排层等价）。
- reload smoke、NamedPipe、taskkill、Windows package smoke、默认盘符 Godot 路径依赖 — 一律 SKIP。

**禁止事项**

- 为子集 green 而删除 migration generation 或 gameplay 旅程 fail-closed 检查（Windows 完整路径仍保留）。
- 在本刀接入 [#12](https://github.com/cty41/wooftactics/pull/12) 的 contract-coverage **除非** #12 已合且单独 commit；避免与 SKIP 表混杂难以 revert。

---

### 刀 3：进程探测中期选项（原则 §5）

**对应原则**：§6 ① 深化 + 原则 §5 中期路径。

**目的**：可选将 `Get-TacticsProcessRecords` 同等逻辑迁入 Python；pwsh 变薄 wrapper；**单测**覆盖 canonical 项目过滤与「无 Editor → 0」。

**改哪些路径**

- `Tools/godot/` 下 Python 模块（如 `godot_process_probe.py`）+ `Tools/godot/tests/test_godot_process_probe.py`。
- 可选：`GodotDevSession.psm1` 改为 subprocess 调用 Python 或 duplicate 最小逻辑（implementer 选原则 §5 路径 2，PR 须说明）。
- 更新 `test_godot_dev_launcher.py`：对过滤语义添加跨平台测试，而非仅读 ps1 字符串。

**怎么测**

- 无 Editor：`Get-TacticsGodotEditorProcess` 等价 API 返回空。
- 启动 `--editor --path <canonical godot>`：返回 1 个 PID；命令行含 project 根（`/` `\` 归一化、大小写不敏感）。
- **负向**：另一 worktree 或假路径 Editor **不得**被计入（回归 #13 语义）。

**完成标志**

- Python 单测 green；Linux／Windows 手工 smoke 各一次（文档记录）。
- 互斥行为不变：Verify 仍在 Editor 打开时 fail-closed。

**Windows-only SKIP**

- Windows 仍可用 CIM；Python 实现可在 Win 用 `psutil` 或 CIM — 不强制 Linux 库在 Windows 失败。

**禁止事项**

- **禁止**放宽「仅当前 canonical `godot/project.godot` 项目根」过滤。
- **禁止**允许 Verifier 与 production Editor 并行。

---

### 刀 4：Open-GodotDev `-NoLaunch` 跨平台准备

**对应原则**：§6 ③。

**目的**：Agent 在 Linux 可 `-NoLaunch` 完成 Build、写 `override.cfg`、会话 JSON、互斥；完整 Editor + godot-ai Attach 仍按 `Tools/migration/manifest/godot-tooling.json` 分平台说明。

**改哪些路径**

- Python 编排入口（或扩展刀 2 模块）：镜像 `Open-GodotDev.ps1` 中 `-NoLaunch` 分支（Build Adapter、`tactics-dev-session.json`、Bootstrap 策略）。
- 文档：`.agents/docs/godot-agent-first-development.md` 增加 Linux／云端 `-NoLaunch` 示例与环境变量。
- 仍调用 `dotnet build … -m:1`；不手改 `.tres/.tscn`。

**怎么测**

- Linux：`Open-GodotDev` Python 等价 `-NoLaunch -GodotExecutable …` → Adapter DLL 存在、session 文件写入、无 Editor 进程（除非用户另启）。
- 互斥：并行两次启动应仍 `AbandonedMutexException` 或等价失败。
- Windows：原 ps1 `-NoLaunch` 回归。

**完成标志**

- 文档化命令在云端 Agent 环境跑通一次（记录于 PR）。
- Attach／`pythonw.exe` 无窗启动 **不**宣称 Linux 与 Windows 逐字相同。

**Windows-only SKIP**

- Codex `-Bootstrap` 若依赖仅 Windows 路径，须 SKIP 或条件分支，日志明示。

**禁止事项**

- Agent 使用 `SharedManualQA` profile（现有策略测试已 forbid）。
- 强杀 Editor 完成 `-NoLaunch` 准备。

---

### 刀 5：更厚编排 — Verify 步骤迁入 Python、ps1 变薄

**对应原则**：§6 ④。

**目的**：Verify 步骤定义集中在 Python 模块；`Verify-GodotProject.ps1` 主要为 Windows 转发与参数绑定；`Tools/godot/tests/test_godot_dev_launcher.py` 与 verifier 集成测试扩展。

**改哪些路径**

- `Tools/godot/verify/` 或等价包结构（步骤注册、SKIP 表、Invoke-Checked 语义）。
- ps1 行数显著下降（目标 implementer 在 PR 写 before/after 行数）。
- `AGENTS.md` Quick Reference **仅在** Frank／维护者批准切换默认入口后更新（可与刀 5 同 PR 或 follow-up）。

**怎么测**

- Windows：ps1 转发后完整 Verify 与刀 5 前 baseline 对比（关键步骤 exit code）。
- Linux：子集 + 全 SKIP 日志审计。
- `python -m unittest discover -s Tools/godot/tests` + 新集成测试。

**完成标志**

- 步骤单源：Python 模块为语义权威；ps1 无重复业务 if-tree。
- 刀 2 的子集成为 Python 模块的 `--profile agent` 或等价开关。

**Windows-only SKIP**

- 与刀 2 同一 SKIP 表，不重复定义。

**禁止事项**

- 一次性删除 ps1 导致 Windows 文档／IDE 任务断裂（须保留薄包装）。
- 将 TS spec 编译迁入 Python。

---

### 刀 5+ / 明确排除（原则 §6 ⑤）

| 项 | 处理 |
|---|---|
| Windows RC／双 EXE smoke | 保持 `Build-GodotWindows.ps1`、`Test-GodotWindowsPackage.ps1` + Windows workflow；**不**并入 Linux Verify 子集 |
| `Test-TacticsToolingAssemblyReload.ps1` Linux 版 | **不做**；可选独立 Windows 门禁 |
| `godot-editor-lifecycle` CIM | 单独 PR／技能维护；不在刀 2 阻塞 |
| `port_resolver.gd` PowerShell CIM | Attach 运行时；文档标注 Windows 向，非本计划 Verify 刀序 |
| 主动 `gh workflow run` | 禁止 |

---

## 7. 验收标准（整项迁移 programa）

- [ ] 原则文档合入且指向本计划；status 反映「已批准、按 plan 实现」。
- [ ] Linux 云端：刀 1 后越过 python PATH 阻塞；刀 2 后 documented Verify 子集可重复执行；SKIP 可审计。
- [ ] Windows：Verify／Open-GodotDev 主路径仍可用；互斥与 canonical 过滤与 #13 一致。
- [ ] TS 规格链仍仅 `node Tools/gameplay-test-spec/dist/src/cli.js`；无 Python spec compiler。
- [ ] 每刀独立 PR、中文 commit、`git diff --cached --check`；失败可 revert 单刀。
- [ ] 硬约束 §2 无削弱；自动测试未写入 manual passed。

---

## 8. 给执行者的提醒

1. **先只读核对**：`Tools/godot/Verify-GodotProject.ps1` 全文步骤、`GodotDevSession.psm1`、原则 PR #14 分支、`#13` diff；不要凭本计划猜参数名。
2. **最小测试**：每刀先 `python -m unittest` 相关目录，再 dotnet 定向 filter，再子集 Verify；确认 `total=0` 的 dotnet／GdUnit 运行无效。
3. **串行**：同一 canonical Editor、GdUnit host、Verifier 同时只有一个修改者；遵循 `godot-editor-lifecycle`。
4. **遇决策停下**：全局 python shim、是否刀 3 迁 Python 探测、Quick Reference 默认入口切换 — 需要 Frank 或用户确认，不要自行扩大范围。
5. **纯文档 PR**（含本计划）：不跑完整 Verify，不触发 GitHub Actions。
6. **合入顺序建议**：#14（原则）→ #13（探测）→ 本计划（已合入则跳过）→ 刀 1 → 刀 2 → …；#12 与刀 2 步骤表协调即可。

---

## 9. 相关链接

| 资源 | 链接 |
|---|---|
| 原则文档 | `.agents/docs/godot-tooling-cross-platform-migration.md`（[PR #14](https://github.com/cty41/wooftactics/pull/14)） |
| 进程探测 | [PR #13](https://github.com/cty41/wooftactics/pull/13) |
| contract-coverage | [PR #12](https://github.com/cty41/wooftactics/pull/12) |
| 自动测试覆盖计划（正交） | `.agents/plans/grok-context-auto-test-coverage.md` |
| Agent 开发入口 | `.agents/docs/godot-agent-first-development.md` |

---

## 变更记录

| 日期 | 说明 |
|---|---|
| 2026-11-05 | 初稿：落实 Frank 拍板原则 §6 的可执行开发计划；文档-only，无脚本行为变更。 |
| 2026-11-05 | 刀 5 落地：Verify 步骤 Python 单源 + ps1 薄转发（见 PR）。 |
