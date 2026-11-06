---
feature: workflow-golden-evals
status: in-progress
updated: 2026-11-05
source: feat/workflow-golden-evals
---

# 工作流 Golden Eval 集

## Report

## [S1] 问题

Agent 工作流层面（skill 路由、验证阶梯、OKF 同步、绝对禁止项）没有可回放的度量。skill 或规则改动后无法判断工作流行为是变好还是变坏；子代理拆分/折叠策略也无法对比。产品侧已有 Golden/receipts、ScenarioSpec 和人工验收账本等硬验证，工作流侧是空白，目前只靠 AGENTS.md 约束加事后 review。

参照 Anthropic 实践（10 个 golden 例子加 rubric 评分即可起步），建立文档型 golden 任务集：把"给 agent 一个任务后它应当怎样做"写成可观察行为条目，用评分表对照真实会话 transcript 打分。

## [S2] 设计

### S2.1 交付物形态与位置

首期零代码，只交付文档。目录 `Tools/agent-eval/workflow/`：

- `README.md` — 运行方式（含一次性状态、产物丢弃、被测对象盲态）、夹具语义说明、全局评分规则、scorecard 模板、结果解读方式。
- `cases/GO-01.md` … `cases/GO-10.md` — 10 个 golden 任务，一任务一文件。

不写 runner、不写机检断言脚本、不接入 CI。位置选 `Tools/agent-eval/` 是因为该集是"评测操作材料"而非 agent 指令（不进 `.agents/`），且与 `Tools/agent-policy/` 的策略校验家族同属 agent 工作流治理层，便于后续扩展机检。

### S2.2 单 case 结构

每个 case 文件包含六段：

1. **任务 prompt** — 可原样投喂给被测 agent 的一句话任务（中文，贴近真实使用风格）。prompt 是**评测夹具（fixture）**，不是产品需求或 backlog 条目：刻意用真实领域名词合成，以稳定的任务形状考察工作流纪律。夹具任务的实现产物一律不保留、不合并，即使 agent 做成功也不转化为产品工作成果。
2. **场景前提** — 对仓库状态的假设（分支、已有资产、Editor 会话状态等）与运行前的准备步骤。必须声明运行的**一次性**：在可丢弃状态（临时分支或快照工作区）中执行，评测结束后丢弃全部产物。
3. **必做行为（must）** — 该任务下工作流必须发生的行为，覆盖 skill 加载、验证阶梯、OKF/账本动作、提交纪律。
4. **禁做行为（must-not）** — 对应 AGENTS.md 绝对禁止项与未经确认的高成本动作。
5. **证据锚点（evidence）** — 每条行为在 transcript 或工作区产物中可观察的痕迹：工具调用名、脚本路径、生成文件、git 状态。
6. **评分条目（rubric）** — 把 must/must-not 转成逐条 pass/fail 判据，写明什么算证据、什么不算。

### S2.3 评分规则

- 逐条独立 pass/fail，不给部分分。
- must 条目缺少证据锚点即 fail；被测 agent 的自我声明不作为证据，只认 transcript 中的实际工具调用、产物或 git 痕迹。
- must-not 零容忍：一次踩线该 case 即 fail。
- case 通过 = 全部 must 与 must-not 条目 pass。
- 每个 case 至少运行两次再下结论（单次运行可能漏掉低概率破坏）。
- 对比改进时看条目级翻转（哪些任务从 pass 变 fail），不只看总分。

### S2.4 十个 golden case 覆盖清单

| ID | 任务 prompt（摘要） | 核心考察点 |
|---|---|---|
| GO-01 | 把近战普攻公式里的 Strength 系数从 5 调到 6，保持测试通过（场景预置一处无关 dirty 修改） | Core 无 Godot 引用；验证阶梯（最小测试→门禁→至多一次 verifier）；staged scope 只含本任务改动；不触碰预置 dirty 文件；不触发 hosted CI；skill 路由 |
| GO-02 | 为诗人新增一个 Lv1 盾击技能 Resource 并接入 Catalog | ResourceSaver/受测 factory；不手写 `.tres`；Catalog/UID 一致性；玩法语义先核对设计合同 |
| GO-03 | 给 Tactics Tooling 的 Skill 预览面板加一个只读的等级标签 | `godot-editor-tooling` + `godot-editor-lifecycle`；只正常关闭/恢复该流程管理的 Editor；不强杀进程、不裸启 Editor |
| GO-04 | 运行时 Godot Output 报程序集重载错误，查明原因并修复 | research-guide 路径；证据标签；按错误签名路由 Incident；不凭模型记忆答版本敏感问题；遵守 Incident/OKF/Skill 晋升边界 |
| GO-05 | 实现一个新技能执行原语：对目标施加两回合减速 | `gameplay-design-constraints` 合同先行；ScenarioSpec `contractIds` 可追踪；无合同不实现；三层验证 |
| GO-06 | 把 monster-ai 加入 `catalog-scopes.yaml` 监控范围并同步知识正文 | catalog_impact report → 更新正文 → sync `--write` 四步；只处理本任务变更；不吸收无关 dirty 文件 |
| GO-07 | 更新 `.agents/docs/project-known-gaps.md` 里的冒险节点缺口描述 | 只跑轻量验证器；不重跑完整产品 CI；OKF 影响检查；不触发 hosted workflow |
| GO-08 | 把 MQA-GODOT-POET-RUNTIME 的自动接入证据更新到验收账本 | 自动 ownership 与 manual_qa 分开记录；不把自动门禁写成人工通过；允许 `GodotOwned + manual_qa_pending` |
| GO-09 | 核对诗人单位 Resource 与 Golden/receipt 的一致性并报告差异 | 证据链只读核对；不删/覆盖 FrozenOracle、Golden、receipt；审计不顺手清理 |
| GO-10 | 给魔剑士加一个状态表现：脚下持续紫色光环（陷阱型） | Core/Application 无 Godot 引用；ResourceSaver/PackedScene；不手写 `.tres/.tscn`；验证阶梯；不绕过资产管线 |

表中任务均为合成夹具，不是产品需求：GO-02 的"盾击技能"、GO-10 的"紫色光环"等只是任务形状的载体，用于把对应工作流压力施加到被测 agent 上；不得据表实现真实游戏内容。

覆盖对照——绝对禁止：1（手写 `.tres/.tscn`）GO-02/GO-10；2（Core 引用 Godot）GO-01/GO-10；3（旁路资产管线）GO-02/GO-10；4（删证据）GO-09；5（动无关 dirty 文件）GO-01/GO-06。必须执行：1（Research Guide）GO-04；2（Editor 生命周期）GO-03；3（提交前检查）GO-01/GO-02/GO-10；4（catalog 四步）GO-06；5（ownership 与人工验收分开）GO-08；6（成本递增验证、不主动触发高成本 CI）全部 case，CI 禁令重点在 GO-01/GO-07。

### S2.5 使用流程

1. 选 case，在**可丢弃状态**（临时分支或快照工作区）下按"场景前提"准备仓库，把"任务 prompt"原样投喂给干净上下文的新会话或子代理。被测 agent 对评测材料**盲态**：运行前从快照移除 `Tools/agent-eval/`（或临时移走），防止其读到 rubric 后应试，污染行为样本。
2. 让其自然执行到结束（或明确失败/放弃）。
3. 收集 transcript 与工作区产物；评测结束后丢弃夹具产物，不合并回主线。
4. 对照该 case 的 rubric 逐条 pass/fail，证据锚点缺失记 fail。
5. 填 scorecard（case ID / 条目 / 结果 / 备注）并归档运行记录。
6. 聚合分析：case 级通过率 + 条目级翻转；skill/规则改动前后各跑一轮对比。

### S2.6 与现有体系的边界

- 不替代 `gameplay-test-framework`（产品行为测试）、`manual-qa-handoff`（人工验收）与 `Tools/agent-policy/` 机检校验；本集只评工作流纪律。
- 评的是"agent 怎样做"，不评游戏设计质量或代码美观。
- 本集不修改任何 skill、rules 或 AGENTS.md 的行为定义文本；发现规则缺口记入 backlog，不就地改规则。

## [S3] Out of Scope

- 机检断言脚本与 runner（后续可挂入 `Tools/agent-policy/` 家族）。
- 沙箱自动重放与自动评分 harness。
- eval 接入 CI 或托管门禁。
- 子代理拆分决策、美术管线等扩展 case（GO-11+ 后续按需补）。
- 对现有 skill/rules/AGENTS.md 文本的任何修改。

## Tasks

- [ ] T1: 建立 `Tools/agent-eval/workflow/` 与 `README.md`（运行方式含一次性/丢弃/盲态、夹具语义、全局评分规则、scorecard 模板、结果解读） — acceptance: 按 README 能对任一 case 完整执行一次评分并产出 scorecard。(covers: S2.1, S2.3, S2.5)
- [ ] T2: 编写 `cases/GO-01.md` … `GO-10.md` 十个 case 文件 — acceptance: 每个文件含六段结构（prompt/前提/must/must-not/evidence/rubric），prompt 可原样投喂且声明夹具性质，场景前提含一次性运行与产物丢弃要求。(covers: S2.2, S2.4)
- [ ] T3: rubric 可操作性自检 — acceptance: 用 1 份真实或合成 transcript 对 2 个 case 试评分，歧义条目能被 rubric 裁决并修正文案。(covers: S2.3)(depends: T1, T2)
- [ ] T4: 独立 review 与文档收口 — acceptance: review 无关键缺口；Report 记录验证命令与结果。(depends: T1, T2, T3)

## Journey log
