---
feature: review-nits-to-gates
status: delivered
updated: 2026-11-16
source: feat/review-nits-to-gates
---

# 评审打回沉淀为门禁 + 可抄点盘点

## Report

**What was built** — `Tools/agent-policy/validate_compose_spec.py`（stdlib-only）：按 A1–A6 校验 `docs/compose/spec/*.md` 的 frontmatter、status/Report/勾选框一致性、占位符与 `covers:`/`depends:` 锚点，经既有 `unittest discover` 步骤进 verify 链（不改 `verify_steps.py`）；`test_validate_compose_spec.py` 含正反 fixture 与真树测试。同时交付 S2.2 打回语料七类分类（含跨分支锚点）与 S2.3 可抄点盘点（Poteto 五层 × tactics 现状 × 处置）。

**Verification** — `py -3 -m unittest discover -s Tools/agent-policy -p test_*.py` 34 tests OK（含真树 9 份 spec）；`py -3 Tools/agent-policy/validate_compose_spec.py` 退出码 0。独立 review PASS-WITH-NITS（无 critical），4 处 nits（A4 无 id 旁路、Risks 缺失、语料口径、打印风格）已修复并复验。

## [S1] Problem

人工评审打回 Agent 产物的问题里，反复出现的是 **spec/散文产物的内部一致性**（status 与 Report/勾选框不同步、Tasks 验收与设计修订后矛盾、宣称超证据），近两周至少 4 起（keep-awake、subagent-handback、workflow-map 首轮 FAIL、golden-evals）。这类问题现有门禁零覆盖：`Tools/agent-policy/` 只校验 incident / manual-qa / foreground 三类 schema，`docs/compose/spec/` 结构无人查。同时需要把「Poteto 金字塔」的可抄点对照 tactics 现状盘一遍，避免把散文规则当防线。

## [S2] Design

### S2.1 spec 结构校验器（本轮实现）

新增 `Tools/agent-policy/validate_compose_spec.py`（stdlib-only，风格对齐 `validate_godot_incidents.py`）校验 `docs/compose/spec/*.md`：

| 规则 | 判据 | 来源语料 |
|---|---|---|
| A1 | frontmatter 含非空 `feature`/`status`/`updated`/`source`；`status ∈ {designed, in-progress, delivered}` | 收口时 status 漏改 |
| A2 | `status: delivered` ⇒ `## Report` 非空且含 `What was built` 与 `Verification` 两段 | keep-awake Report 空 |
| A3 | `status: designed` 或 `in-progress` ⇒ Report 为空（模板约定：finalize 前不填） | 模板纪律 |
| A4 | delivered ⇒ 全部 Tasks 为 `[x]`（Tasks 段存在时；Report-only spec 允许无 Tasks——语料失效模式是勾选框过期，不是缺 Tasks） | handback 任务框未勾 |
| A5 | Tasks 验收文字无占位符（`TBD`/`TODO`/`完成实现`/`添加适当`） | 计划/规范纪律同源问题 |
| A6 | `covers: S…` 锚点指向文档内实际存在的 `Sn`/`Sn.m` 标题 | 引用失实家族 |

不校验语义级宣称（如"全部经独立 review"）——那是 REVIEW.md 的人工裁决域，见 S2.3 处置。

### S2.2 打回语料分类（本轮审计结论）

证据锚点取自 `address review nits` 提交、spec Journey log 与 P0 评测基线：

| # | 类别 | 证据 | 可机器化 | 处置 |
|---|---|---|---|---|
| A | spec/文档内部不一致 | `ab5b7424`（Tasks 与 S2.4 矛盾、Report 空）；`2a2ce1fd`（任务框/Journey 空）；`ddc33f41`（宣称超证据）；`b3ebe621` | 是 | **本轮 A1–A6 落地**（任务框=A4；Journey 空与"宣称超证据"属收口纪律，人工域，见 REVIEW.md） |
| B | 引用/出处失实 | `ddc33f41` workflow-map 首轮 FAIL：教训挂错出处、无据变量（全仓零命中） | 部分 | 后置：文档内路径/变量存在性检查；语义出处人工核 |
| C | 文档-代码漂移 | `ab5b7424` README 漏 `ES_CONTINUOUS` | 部分 | 后置：一次性文档-代码对照清单；不建全自动 |
| D | 环境事实未逐字核对 | workflow-map Journey（`CalculateFolderPath` vs `GetFolderPath`） | 否 | 已落 Journey 记录与 agent-environment.md，流程性 |
| E | 机械段边界表述 | `96f5fd93` scope 甄别应留主循环 | 否 | 已修进 rules，流程性 |
| F | 注册表/处理器一致性 | contract-coverage 踩坑后 `c5d30f8b` 静态断言 | 是 | **已有门禁**（PR #28） |
| G | 评测纪律（skill 路由翻转等） | P0 基线 GO-01 A3 FAIL | 是 | **已有门禁**（eval set） |
| H | QA 账本政策迭代 | feat/all-tilemap `6582206c`→`183912ba` 四连发 | 是 | **已有门禁**：`validate_manual_qa_handoff.py` 联动改（未合，见 Risks） |

### S2.3 可抄点盘点（Poteto 五层倒金字塔 × tactics 现状）

| 层 | Poteto 主张 | tactics 现状 | 结论 |
|---|---|---|---|
| Codebase | 正确写法是默认路径；目录即上下文 | 分层规则（Core/Application 无 Godot）靠"不引包=编译不过"默认成立，无显式禁令检查 | **可抄（后置）**：src csproj 禁引 Godot 显式检查，便宜且一次成型 |
| Static Analysis | 规则写进 Lint 报错信息而非散文 | `TreatWarningsAsErrors`；agent-policy 三校验器；OKF `validate_bundle`；contract-coverage；verify 注册表断言；gd1 分支已把 QA 账本政策编码进校验器 | **本轮补一块**：spec 结构校验器（S2.1）；gd1 做法作为已有实践引用 |
| Verification | Feature Map + CLI 自检 + 形式化验证 | 单测/golden/FrozenOracle/gameplay spec/verify 编排齐；gd1 新政策下截图证据可选 | **部分可抄**：Feature Map 思路与 gameplay spec 重叠，不另造；TLA+/Lean/Bombadil 对单机游戏成本不匹配，**不抄** |
| Prose | 只留需人工判断的轻量规则；禁辩护型注释 | P4 已清规则文本；REVIEW.md 试行 | **可抄（后置）**：代码"辩护型注释"先一次性审计再定禁令；**不抄** 99% 砍注释（前置条件：命名/类型/目录清晰度不满足） |
| Human Review | 收缩为只看验证产物 | REVIEW.md 七项收口 + 三轮上限；"测试已断言的不看截图"已进 AGENTS.md（gd1 未合） | **已有实践**，无需新抄 |

外循环工厂（沙盒 Fuzz→自动修复 PR）**不抄**：单机 Godot 项目无持续发版节奏，属"不要自动化坏循环"里的外圈。

### S2.4 接线方式

校验器经既有 `python -m unittest discover -s Tools/agent-policy -p 'test_*.py'` 步骤进入 verify 编排；另含一条真树测试：对仓库现存 `docs/compose/spec/*.md` 跑校验器（修掉现存违例，使基线为绿）。不改 `verify_steps.py`（避免与 gd1 分支同面摩擦）。

## [S3] Out of Scope

- 类别 B/C 的检查器实现（只在 S2.2 记处置建议）
- csproj 禁引 Godot 检查（S2.3 标为后置，另立小任务）
- 代码辩护型注释审计（S2.3 标为后置）
- TLA+/Lean/Bombadil、外循环工厂
- 收编 feat/all-tilemap 的 16 个提交；处置 local-review-contract 会话遗留（未删计划 + 未提交 log.md 重写）
- OKF sync 收尾（工作区含他人未提交知识改动，见 Risks）

## Risks

- **OKF sync 延后**：主工作区含另一会话未提交的知识改动（`local-review-contract` 遗留：未删计划 + `log.md` 重写）。新文件 `Tools/agent-policy/validate_compose_spec.py` 落在 `Tools/agent-policy/` 受监控根内但暂无 scope 映射；待工作区干净后补 `catalog-scopes.yaml` 映射并跑 report→正文→sync，本次未执行。
- **与 feat/all-tilemap 的合并摩擦**：该分支（未合）改了 `Tools/agent-policy/validate_manual_qa_handoff.py` 与 `AGENTS.md` 相邻行；建议其先收编，再合本分支。
- **真树测试的耦合**：`test_real_tree_specs_pass` 将 unittest 绿度与现存 spec 内容绑定，后续新 spec 违例会直接打红全套测试——这是预期门禁行为，不是回归。

## Tasks

- [x] T1: `Tools/agent-policy/validate_compose_spec.py` — acceptance: 规则 A1–A6 全部可执行；正反 fixture 覆盖每条规则；stdlib-only，风格对齐 `validate_godot_incidents.py`。(covers: S2.1)
- [x] T2: `Tools/agent-policy/test_validate_compose_spec.py` — acceptance: unittest discover 全绿；含真树测试（现存 spec 校验通过或列出已修违例）；入口可 `python -m` 直接跑真实目录。(covers: S2.1, S2.4)(depends: T1)
- [x] T3: 独立 review 与收口 — acceptance: review 无关键缺口；Report 记录验证命令与结果。(depends: T1, T2)

## Journey log

- 门禁规则按「语料失效模式」校准而非按模板完备性校准：打回语料的病是勾选过期与状态不同步，不是缺 Tasks 段，故 Report-only delivered spec（如 `p2-split-pilots.md`）判合法。
- 评审暴露 A4 旁路：任务行不带 `T\d:` id 时勾选框漏审。已改为「有 id 查 id、无 id 查框」双覆盖；结论「模板任务必须带 id」自此过时。
- S2.2 语料锚点含跨分支引用（`feat/all-tilemap` 的 `6582206c`→`183912ba`），评审抽验全部真实；该表可作类别 B/C 后置检查器的索引。
