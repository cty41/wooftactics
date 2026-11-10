# P2 拆分试点：OKF 同步链与门禁日志分诊

## Summary

把 Claude 多代理实践清单里的 P2 两个首批试点各做一次真实子代理拆分：OKF 同步链（report→正文→sync→校验）与统一门禁日志分诊。交付形态是「真实试跑 + 回执契约验收 + 派发配方沉淀进 rules」，不新建 skill 或 Tools 工具。

成功标准（可裁决）：每个试点至少一次真实子代理执行；回执按 `.agents/rules/subagent-handback.md` 四段契约交回并通过主循环独立核对；派发配方写进 rules 后复核无冲突。满足后才考虑冻结合同施工、美术批审、引擎调研三个后续试点。

## 人话版

### 一句话
把「OKF 同步链」和「门禁日志分诊」两件重活真拆给子代理各跑一次，用回执契约验收，把派发配方写进规则。

### 为什么
拆子代理最怕传话丢细节、规则各写各的。不先用真实任务试跑并固化配方，后面三个更贵的试点不敢开。

### 步骤白话
先把 OKF 同步的派发配方写进规则——这次改动本身就是要同步的知识变更，正好当载体；主循环写概念正文，子代理跑报表、汇总、同步、校验这四步机械活，按四段契约交回执。再抓一份真实测试日志，让子代理只回「哪几个 case 挂、签名指向哪」，主循环抽查对得上。最后把日志分诊的配方也写进规则，复核两份规则没打架，spec 记录结论。

### 怎样算完成
两次子代理试跑都交了合格回执且抽查与原数据对得上；两份派发配方进了 rules；OKF `validate_bundle.py` 通过；spec 收口且无占位符。

### 易踩坑
1. OKF 正文落笔不能丢给子代理——它需要完整会话上下文，规则里明确归「不该拆」；拆出去的只是 report/sync/validate 机械段。
2. 日志必须来自真实测试命令输出，不许拿合成文本充数。
3. 子代理自报「测试全过」不算证据；只有附命令输出锚点的计数才算，否则归「待核验项」。

## Background

**当前问题**：`subagent-handback.md` 已定义拆分判据与四段回执契约，但没有任何一次真实拆分用它跑通过。文章结论是「上下文重、结果紧凑」的任务该拆，本清单最重的两个候选（OKF 同步链、门禁日志分诊）从未验证过这套契约是否可操作。

**目标**：用两个真实任务把「怎么派发、回执长什么样、主循环怎么核对」跑成可复用配方，并写回 rules。

**预期收益**：拆分的代价从「丢上下文」降到「多一次核对」；后续三个试点（冻结合同施工、美术批审、引擎调研）有先例可抄。

## Scope

### In Scope
- OKF 同步链派发配方 + 一次真实子代理试跑（载体 = 本次 rules 修改触发的 `godot-agent-workflow` scope 同步）
- 门禁日志分诊派发配方 + 一次真实子代理试跑（载体 = 目标测试批次的真实输出日志）
- 回执四段契约的实战验收与主循环核对记录
- 配方沉淀进 `.agents/rules/subagent-handback.md`，必要时在 `knowledge-maintenance.md` 补一行边界说明
- spec `docs/compose/spec/p2-split-pilots.md` 收口

### Out of Scope
- 后续三个试点：冻结合同施工、美术批审、引擎调研（本计划跑通后另立）
- 新建 skill、Tools/ 下派发或分诊脚本
- skill 级小 eval（GO-11+）、hillclimb、事件时间戳（P5）
- 跑完整 `Tools/godot/Verify-GodotProject.ps1`（本计划日志源用目标测试批次；完整门禁不在授权内）
- 环境坑修复（ProgramFiles/python PATH、`GODOT_BIN`、submodule，backlog 另立）
- 评测运行（`Tools/agent-eval/`）——遵循其自身 README，不在回执契约约束内

## File Structure

- `.agents/rules/subagent-handback.md` — 追加「已验证派发配方」两节（OKF 同步链、日志分诊各一），含任务书模板四要素
- `.agents/rules/knowledge-maintenance.md` — 如需，补一行「机械段可拆、正文落笔不拆」边界说明
- `.agents/knowledge/operations/godot-agent-workflow.md` — OKF 试跑中由主循环落笔更新的概念正文（预计命中）
- `.agents/knowledge/log.md` — OKF 状态变化记录
- `docs/compose/spec/p2-split-pilots.md` — 新增 spec（Report / Journey log）
- 临时日志文件 — 测试批次输出捕获，不入库

## Current State

- `.agents/rules/subagent-handback.md` 已在 main（PR #26）：拆分判据（该拆 4 条 / 不该拆 4 条，默认不拆）、四段回执契约、派发侧义务（任务书四要素、不传会话历史）、主循环核验义务（自我声明不算证据、至少一次便宜独立核对）。
- `.agents/rules/knowledge-maintenance.md` 定义 OKF profile 与同步流程；AGENTS.md 固化命令链：`catalog_impact.py report --worktree` → 更新受影响概念正文 → `catalog_impact.py sync --worktree --scope <scope> --write`。
- `.agents/knowledge/catalog-scopes.yaml` 中 `godot-agent-workflow` scope 的 paths 含 `.agents/rules`——修改 `subagent-handback.md` 即触发真实 scope 影响，可作 OKF 试跑载体。
- `Tools/okf/` 有 `catalog_impact.py`、`validate_bundle.py` 及测试；依赖 `PyYAML>=6.0,<7.0`（`Tools/okf/requirements.txt`）。本机托管 Python 未装 PyYAML，执行前需准备依赖环境。
- 仓库内当前无现成门禁/测试日志文件（`.log`/`.trx` 为空），日志分诊试跑需现抓。
- P0 golden eval 基线已归档（`Tools/agent-eval/workflow/runs/baseline-2026-11-09.md`），与本计划无耦合。

## Relevant Context

- 唯一 Godot 项目 `godot/project.godot`；本计划纯规则/文档/知识同步，不动 Core/Application/Adapter 代码。
- `.agents/rules/subagent-handback.md` 明确：「OKF 正文落笔」属不该拆第 4 条（需要完整会话上下文的写入决策）；「OKF 同步链」「日志分诊」属该拆第 2 条（上下文重、结果紧凑）。试点即验证这条边界可操作。
- 主循环核对手段（契约规定）：`git diff`、状态查询、抽查命令重跑；核对不符以工作区事实为准并记录差异。
- 测试计数只有附带命令输出锚点时才算证据，自报计数归入待核验项。
- 提交前检查精确 staged scope、`git diff --cached --check`；OKF 受监控范围变更后须走影响检测与同步（AGENTS.md 必须执行 #4）。

## Tasks

### Task 1: OKF 同步链派发配方落地与真实试跑

- **目标**：把 OKF 同步链的拆分边界与派发任务书写进 `subagent-handback.md`，并以该变更本身为载体完成一次真实子代理同步链试跑。
- **输入**：`subagent-handback.md` 现行文本；`Tools/okf/catalog_impact.py`、`validate_bundle.py`（需 PyYAML 环境）；`catalog-scopes.yaml`。
- **输出**：rules 更新；受影响概念正文更新（预计 `operations/godot-agent-workflow.md`，由主循环落笔）；sync 后 metadata 与 `log.md` 记录；子代理四段回执一份。
- **涉及文件**：`.agents/rules/subagent-handback.md`、`.agents/knowledge/operations/godot-agent-workflow.md`、`.agents/knowledge/log.md`。
- **验收标准**：
  1. `& $MIMO_PYTHON Tools/okf/catalog_impact.py report --worktree` 对本次 rules 修改报出 `godot-agent-workflow` scope（若还报出其他 scope，按同一机械段处理并记入偏差点）。
  2. 任务书含四要素（工作区路径、任务描述、验收判据、所需验证命令），派发不含会话历史。
  3. 子代理只执行机械段（report→影响汇总→sync→validate），回执四段齐备；正文落笔由主循环完成。
  4. 主循环对回执证据至少 1 个锚点独立核对（建议：重跑 `validate_bundle.py` 或 `git diff` 比对 sync 写入字段），结果一致。
  5. `& $MIMO_PYTHON Tools/okf/validate_bundle.py` 通过。
  6. rules 中留下可复用派发任务书模板与机械段清单。

### Task 2: 门禁日志分诊派发配方落地与真实试跑

- **目标**：抓取真实测试批次日志，子代理做失败分诊，配方写进 rules。
- **输入**：一次目标测试批次的真实输出（建议 `dotnet test` Core+Application 批次，或等价 Python 编排日志；行数应达数百以上）。
- **输出**：分诊回执（失败 case 清单 + 签名锚点文件:行号）；rules 中日志分诊派发任务书模板；临时日志文件（不入库）。
- **涉及文件**：`.agents/rules/subagent-handback.md`；临时日志路径在回执中登记。
- **验收标准**：
  1. 日志来源为真实测试命令输出；回执记录捕获命令行与时间；禁止合成或手写日志。
  2. 回执四段齐备；「做了什么」≤10 行且只含结论式分诊结果。
  3. 主循环对失败 case 与签名抽查 ≥2 处，可在原始日志中定位到相同锚点。
  4. rules 中含日志分诊任务书模板（同样四要素）。
  5. 若本次 rules 修改触发 scope 影响，补做一次 OKF 同步链（可再派子代理跑机械段，或主循环直接处理），并把回执/核对记录在案。

### Task 3: 规则交叉复核与 spec 收口

- **目标**：确认两份规则无表述冲突，写 spec Report，满足「规则落地后复核无冲突」。
- **输入**：Task 1/2 的回执、核对记录与 rules diff。
- **输出**：`docs/compose/spec/p2-split-pilots.md`（frontmatter `status: delivered`）。
- **涉及文件**：`docs/compose/spec/p2-split-pilots.md`、`.agents/rules/subagent-handback.md`、`.agents/rules/knowledge-maintenance.md`（如需补边界行）。
- **验收标准**：
  1. spec 含 Report（What was built / Verification）与 Journey log，无空任务框。
  2. `subagent-handback.md` 与 `knowledge-maintenance.md` 对「OKF 正文落笔不拆、机械段可拆」表述一致；必要时加一行指向。
  3. 全文无占位符（TBD/TODO/「完成实现」等）。
  4. 两份回执与核对记录作为 Verification 证据写入 spec（摘要形式，不粘贴全量输出）。

## Test Plan

- 无产品代码变更，不适用单元测试；验证以命令可观察结果为准：
  - OKF：`catalog_impact.py report --worktree` 输出范围、`validate_bundle.py` 退出码、sync 前后 `git diff` 字段级比对。
  - 分诊：主循环抽查原始日志锚点（失败 case 名、文件:行号）逐条对得上。
  - 契约：回执四段缺一即判不合格，退回重做；核对不符以工作区事实为准并记录。
- 成本递增：两个试点各自只做定向命令，不触发完整 `Verify-GodotProject.ps1`、不触发 GitHub Actions。

## Risks / Assumptions

| 项 | 说明 | 处置 |
|----|------|------|
| PyYAML 缺失 | 托管 Python 无 `yaml` 模块，OKF 命令跑不动 | 执行前按 `Tools/okf/requirements.txt` 准备依赖（一次性环境准备，不改 Tools 代码） |
| report 范围超出预期 | rules 修改可能命中多个 scope | 同一机械段内处理，超出部分记入回执「偏差点」 |
| 测试日志不够大 | 目标批次输出过短，分诊无代表性 | 换更大批次重抓；仍不做完整 verifier |
| 试点失败 | 回执不合格或核对不符 | 不迁就：退回重派或改任务书；失败经验写进 Journey log，不写成通过 |
| 正文落笔边界模糊 | 子代理可能越界写概念正文 | 任务书明确「正文由主循环落笔」；越界记入偏差点并改回 |

## Handoff Notes

- **先读**：`.agents/rules/subagent-handback.md`（回执契约全文）→ `.agents/rules/knowledge-maintenance.md` → 本计划。
- **先验证**：Python 环境能 `import yaml`；`catalog_impact.py report --worktree` 在干净树上可运行（输出为空属正常）。
- **不要做**：不要合成日志冒充分诊试跑；不要把子代理自报结论直接写进 OKF 正文；不要跑完整 verifier 或远程门禁；不要顺手修改本计划 Out of Scope 项。
- **收口约定**：实现验证后，长期结论（派发配方）已由 Task 1/2 沉淀进 rules；spec 完成后本计划文件按 `knowledge-maintenance.md` 约定删除并由 Git 保留历史。
