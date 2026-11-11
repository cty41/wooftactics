本文件为 P4 产物（prompt-audit 初筛 + 主循环裁决：初筛只标记不修改；裁决与修订落地见文末「裁决」节，依据 `docs/compose/spec/prompt-audit.md` S2.2/S2.3）。

## 初筛候选清单

### 扫描覆盖清单（24 个文件，全量）

口径：只审 frontmatter description 与 body 规则性语句（禁令、必须、流程指令、合同要求）；教程、示例代码、参考表、路由表不审（下表注明）。事故溯源检索范围：`.agents/incidents/godot/`（15 条事故 + schema/index）与规则内文的事故引用。

**AGENTS.md（1）**

| 文件 | 覆盖 | 非规则内容处理 |
|---|---|---|
| `AGENTS.md` | 全文 66 行 | Quick Reference 表按"摘要禁令"审（与正文禁令重复时归类别 2）；规则索引、共享技能说明为导航/约定描述，不产候选 |

**`.agents/rules/`（8）**

| 文件 | 覆盖 | 非规则内容处理 |
|---|---|---|
| `.agents/rules/agent-environment.md` | 全文 10 行 | 无候选：机器（mibook）Modern Standby 运维程序与环境事实，非模型行为规则；:10 为环境操作边界 |
| `.agents/rules/agent-worktree.md` | 全文 15 行 | 无跳过 |
| `.agents/rules/code-documentation.md` | 规则句 :3-20、:81-124 | 代码示例 :26-79、:126-178 为教程/示例，不审；Quick Reference 表 :5-11 为格式参考表，不审 |
| `.agents/rules/foreground-interaction.md` | 规则句 :5、:17-55、:66-72 | 代表性场景表 :57-64 为场景参考，不审；全文件属前台安全政策（spec S2.1 安全边界），跨文件重复见 C2-8/C2-9，无独立修改候选 |
| `.agents/rules/godot-agent-workflow.md` | 全文 57 行 | 无跳过 |
| `.agents/rules/godot-migration.md` | 全文 14 行 | 全文属 Godot 迁移审计边界 → 全部只列观察，见 C2-18 |
| `.agents/rules/knowledge-maintenance.md` | 规则句 :16-20、:43-59、:61-88、:104-112 | OKF frontmatter YAML 示例 :26-41、Lint 命令块 :99-102 为格式/操作参考；权威顺序表 :9-14 为参考表，不审 |
| `.agents/rules/subagent-handback.md` | 全文 73 行 | 示例回执 :44-49 为示例不审；派发配方 :51-73 属 OKF/门禁操作细节，只列观察 |

**`.agents/skills/*/SKILL.md`（15）**

| 文件 | 覆盖 | 非规则内容处理 |
|---|---|---|
| `.agents/skills/artworks-prompt-library/SKILL.md` | 边界句 :8、:160、:162-169；Anti-patterns :178-190 | 正文 Steps 1-8 大部为提示词库生产教程与产出格式参考（Step 3 关键词清单、Step 5 帧字段清单等），不审；**无候选** |
| `.agents/skills/gameplay-design-constraints/SKILL.md` | 全文 71 行 | 合同 YAML 示例 :44-52、合同文档清单 :28-34 为参考表/示例，不审 |
| `.agents/skills/gameplay-test-framework/SKILL.md` | 全文 144 行 | CLI 命令块 :47-58、:64-70、:82-93、:115-118 为操作参考不审；Anti-patterns 表审（禁令列） |
| `.agents/skills/godot-ai-workflow/SKILL.md` | 全文 53 行 | 配置/端口/Profile 事实为工具操作细节，只列观察（C2-12/C2-14/C2-15） |
| `.agents/skills/godot-content-migration/SKILL.md` | 全文 46 行 | 全文属迁移审计 → 只列观察（C1-16/C2 系列） |
| `.agents/skills/godot-csharp-development/SKILL.md` | 全文 44 行 | 无跳过 |
| `.agents/skills/godot-editor-lifecycle/SKILL.md` | 全文 68 行 | PowerShell 命令块 :35-39、:45-49 为操作参考；流程步骤属安全程序，只列观察（C1-13） |
| `.agents/skills/godot-editor-tooling/SKILL.md` | 全文 45 行 | 无跳过 |
| `.agents/skills/godot-testing-diagnostics/SKILL.md` | 全文 44 行 | 无跳过 |
| `.agents/skills/godot-workflow/SKILL.md` | 全文 59 行 | 路由表 :10-18 为参考表不审；Examples :38-42 不审 |
| `.agents/skills/knowledge-maintenance/SKILL.md` | 全文 95 行 | 命令块 :46-48、:66-68、:72-75 为 OKF 操作参考；Anti-patterns 表审 |
| `.agents/skills/manual-qa-handoff/SKILL.md` | 全文 47 行 | 无跳过 |
| `.agents/skills/plan-plain/SKILL.md` | 全文 204 行 | 计划骨架模板 :65-91、人话版模板 :125-142、优先级表 :50-55 为格式参考不审；各段合同表 :148-154 的数值限审 |
| `.agents/skills/pure-run-artwork-pipeline/SKILL.md` | 规则句 :59-131（Workflow/Guardrails） | Quick Reference 表 :10-48 为命令路由参考不审；美术尺寸/锚点数值（256/128、y=236 等）为领域合同不审 |
| `.agents/skills/tactics-git-workspace/SKILL.md` | 全文 95 行 | git 命令块 :37-40、:50-53、:61-64 为操作教程不审 |

### 类别 1：过度具体（模型可稳定自判的微观步骤）

**C1-1** `.agents/skills/plan-plain/SKILL.md:48`
类别 1｜理由：把澄清提问数量写死为"总题数 5–12、每组 2–3 个"，属模型可稳定自判的微观参数。
建议 diff：`总题数 5–12，每组 2–3 个高价值问题即可；该组已足够可跳过后续组` → `按 P0→P3 收束关键缺口；每组抓高价值问题，信息已足的轴可跳过，题数以收束需要为限`。
事故溯源初判：否。

**C1-2** `.agents/skills/plan-plain/SKILL.md:150`
类别 1｜理由："一句话 ≤40 字优先"是字数级微观参数，模型可自判"尽量短"。
建议 diff：`计划要改变什么；≤40 字优先` → `计划要改变什么；尽量一句话讲清`。
事故溯源初判：否。

**C1-3** `.agents/skills/plan-plain/SKILL.md:163`
类别 1｜理由："约 15–35 行"是行数级微观参数；后半句（每步写明完成时能看到什么）才是意图。
建议 diff：`长度一屏优先（约 15–35 行）` → `长度一屏优先`（保留后半句不动）。
事故溯源初判：否。

**C1-4** `.agents/rules/subagent-handback.md:27`
类别 1｜理由："不超过 10 行"是行数微指令；"结论式陈述"+回执紧凑默认（:25）已表达意图。
建议 diff：`结论式陈述，不超过 10 行。` → `结论式陈述，保持紧凑。`。
事故溯源初判：否。

**C1-5** `.agents/skills/gameplay-test-framework/SKILL.md:107`
类别 1｜理由："首次通过后立即连续运行第二轮"是固定复跑微步骤；意图是暴露残留状态与顺序依赖，何时复跑模型可自判。
建议 diff：`相关 fixture 首次通过后立即连续运行第二轮，检查残留状态与顺序依赖` → `涉及输入、缓存 UI、异步或 reload 的 fixture 要暴露残留状态与顺序依赖，必要时复跑确认`。
事故溯源初判：不确定（`typed-resource-reload`、`gdunit-gameplay-journey-native-host-hang` 等残留/挂起坑同域，但规则与事故未互引）。

**C1-6** `.agents/skills/gameplay-test-framework/SKILL.md:110`
类别 1｜理由："超过 60 秒报告进度、90 秒无变化检查"是秒级微指令。
建议 diff：建议不动——疑似事故溯源；若裁决简化：`长任务持续报告可验证进展；长时间无进展时检查进程、runner 与日志，确认卡死才停止并恢复当前门禁`。
事故溯源初判：不确定（疑似对应 `gdunit-gameplay-journey-native-host-hang` 的 testhost 满载无输出挂起；双侧未显式互引，保守按疑似事故处理）。

**C1-7** `.agents/skills/godot-workflow/SKILL.md:28`
类别 1｜理由："Confirm the worktree is `migration/godot`" 把 worktree 分支名写死为检查步骤，属过度具体且疑似迁移期残留（与 `agent-worktree.md`"当前 workspace"口径不一致）。
建议 diff：`Confirm the worktree is migration/godot and the project is godot/project.godot.` → `Confirm the current workspace and the canonical project godot/project.godot (worktree policy: agent-worktree / tactics-git-workspace).`。
事故溯源初判：否（项目路径检查部分另见 C2-6/wrong-project-root，本条只指 worktree 名硬编码）。

**C1-8** `.agents/skills/godot-csharp-development/SKILL.md:22`
类别 1｜理由："新增名字/using 前先搜索"是"先 X 再 Y"微步骤，模型可稳定自判复用优先。
建议 diff：`Search existing project types and APIs before adding names or using directives.` → `Prefer existing project types and APIs over new names or usings.`。
事故溯源初判：否。

**C1-9** `AGENTS.md:54`
类别 1｜理由：提交前"检查精确 staged scope、`git diff --cached --check`、相关测试和 OKF"是四连机械检查项。
建议 diff：建议不动（spec S2.1：门禁操作细节不算补丁，只列观察）。
事故溯源初判：否。

**C1-10** `AGENTS.md:55-58`
类别 1｜理由：catalog_impact 的"report → 更新正文 → sync --write"三步命令序列属 OKF 操作细节（与 `knowledge-maintenance.md:80-88`、skill :42-75 同族）。
建议 diff：建议不动（spec S2.1：OKF 操作细节不算补丁，只列观察）。
事故溯源初判：否。

**C1-11** `.agents/rules/godot-agent-workflow.md:41`
类别 1｜理由："MCP 写操作前必须先调用 Session 与 Editor 状态接口，确认…"是"先 X 再 Y"式微步骤。
建议 diff：建议不动（硬约束：事故溯源）。
事故溯源初判：是（`wrong-project-root` 事故同域：项目路径错开即 Build 成功但内容缺失；单会话核对为安全写入门禁）。

**C1-12** `.agents/skills/godot-testing-diagnostics/SKILL.md:18-19`
类别 1｜理由："先捕获首个精确错误签名/命令/上下文/版本，再最窄复现"是诊断步骤序列。
建议 diff：建议不动（硬约束：事故溯源）。
事故溯源初判：是（`.agents/incidents/godot/schema.md` 强制 `signature` 证据字段；签名保留是事故记录前置）。

**C1-13** `.agents/skills/godot-editor-lifecycle/SKILL.md:28-52`
类别 1｜理由：关闭/恢复 Editor 的 7 步机械序列（含 120 秒轮询、`status=closed` 断言）形态上是"先 X 再 Y 再 Z"。
建议 diff：建议不动（安全与合规边界 + 疑似事故溯源；只列观察）。
事故溯源初判：不确定（与 `editor-dock-lifecycle`、`csharp-assembly-reload-*`、`typed-resource-reload` 同域，未显式互引；前台安全政策 `foreground-interaction.md` 为上位约束）。

**C1-14** `.agents/rules/knowledge-maintenance.md:63-88`
类别 1｜理由：Query/Ingest/Source Change Sync 的编号步骤（"1. 从 index 开始…2. 逐级打开…"）是 OKF 操作序列。
建议 diff：建议不动（spec S2.1：OKF 操作细节不算补丁，只列观察）。
事故溯源初判：否。

**C1-15** `.agents/rules/subagent-handback.md:55-62、66-73`
类别 1｜理由：OKF 同步链"report → 汇总 → sync --write → validate"与门禁日志分诊步骤是已验证派发配方的机械段（其自身定位即"机械段"）。
建议 diff：建议不动（spec S2.1：OKF/门禁操作细节不算补丁，只列观察）。
事故溯源初判：否。

**C1-16** `.agents/skills/godot-content-migration/SKILL.md:20-27`
类别 1｜理由：迁移批次 8 步流程（绑定冻结源→读 DTO→校验→staging→ResourceSaver→台账→扫描→ownership）形态为机械序列。
建议 diff：建议不动（硬约束：Godot 迁移审计）。
事故溯源初判：是（迁移审计：冻结源/receipt/幂等台账合同）。

### 类别 2：冗余重复（同一禁令跨文件多处表述；锚点列全部位置）

**C2-1** 托管 CI / 高耗时远程任务禁令（10 处）
类别 2｜锚点：`AGENTS.md:12`、`AGENTS.md:60`、`.agents/rules/godot-agent-workflow.md:14`、`.agents/skills/godot-workflow/SKILL.md:35`、`:50`、`:58`、`.agents/skills/godot-testing-diagnostics/SKILL.md:10`、`:23`、`:36`、`:43`。
理由：同一"未经明确授权不得触发/重跑 GitHub Actions、远程导出"禁令在 2 个顶层文件 + 2 个 skill 内全文级重复（后 6 处为 Quick Ref/步骤/反模式/清单骨架回声）。
建议 diff：建议不动（硬约束：验证阶梯；只列观察）。
事故溯源初判：是（验证阶梯——成本递增本地优先策略的组成部分；`knowledge/log.md:101` 记录同源政策变更）。

**C2-2** Core/Application 禁止引用 Godot/Editor API（5 处）
类别 2｜锚点：`AGENTS.md:10`、`AGENTS.md:45`（绝对禁止 2）、`.agents/rules/godot-agent-workflow.md:11`、`.agents/skills/godot-csharp-development/SKILL.md:10`、`.agents/skills/godot-workflow/SKILL.md:33`。
理由：分层禁令在绝对禁止清单、规则、2 个 skill 中四处全文重复。
建议 diff：建议不动（硬约束：绝对禁止清单）。
事故溯源初判：是（绝对禁止清单；与 `export-release-editor-dependency-graph-contamination`、`gdu4-conditional-package-release-contamination` 两条事故的泄漏形态同域）。

**C2-3** 禁止手写/机械修改 `.tres/.tscn`、必须走 ResourceSaver（6 处）
类别 2｜锚点：`AGENTS.md:11`、`AGENTS.md:44`（绝对禁止 1）、`AGENTS.md:46`（绝对禁止 3）、`.agents/rules/godot-agent-workflow.md:20`、`.agents/skills/godot-content-migration/SKILL.md:36`、`.agents/skills/godot-ai-workflow/SKILL.md:44`（deny-list 同源）。
理由：资源写入禁令在绝对禁止清单 3 条、规则、2 个 skill 中重复。
建议 diff：建议不动（硬约束：绝对禁止清单）。
事故溯源初判：是（绝对禁止清单；资产管线审计：`headless-resource-uid-cache` 等迁移审计合同）。

**C2-4** 验证阶梯（最小测试→相关门禁→至多一次统一 verifier）（7 处）
类别 2｜锚点：`AGENTS.md:12`、`AGENTS.md:60`、`.agents/rules/godot-agent-workflow.md:13`、`.agents/skills/godot-workflow/SKILL.md:34`、`.agents/skills/godot-testing-diagnostics/SKILL.md:10`、`:22`、`:42`、`.agents/skills/gameplay-test-framework/SKILL.md:106`（部分）。
理由：验证阶梯句式跨 AGENTS.md、规则、3 个 skill 重复展开。
建议 diff：建议不动（硬约束：验证阶梯；只列观察）。
事故溯源初判：是（验证阶梯）。

**C2-5** worktree 三条例外清单复述（2 处主锚点 + 摘要）
类别 2｜锚点：`.agents/rules/agent-worktree.md:7-13`（canonical）、`.agents/skills/tactics-git-workspace/SKILL.md:70-76`（逐条复述）、`:20`（Quick Ref 行复述）；摘要另见 `AGENTS.md:9`、`AGENTS.md:24`、`.agents/rules/godot-agent-workflow.md:6`、`.agents/skills/godot-workflow/SKILL.md:20`（worktree 半句）。
理由：skill :10 自己声明"规则只保留默认句和例外"，却在 :70-76 把三条例外原样再写一遍，同一例外清单两份全文。
建议 diff：`tactics-git-workspace:70-76` 三条例外 → `只有 agent-worktree.md 三条例外成立时才可 git worktree add，创建前记录理由/分支/路径/生命周期`（例外清单只留规则一份）；摘要句保留不动。
事故溯源初判：否（worktree 政策部分；canonical 项目部分另见 C2-6）。

**C2-6** 唯一 canonical `godot/project.godot`（6 处）
类别 2｜锚点：`AGENTS.md:9`、`.agents/rules/godot-agent-workflow.md:5`、`.agents/skills/godot-workflow/SKILL.md:20`、`:49`、`.agents/skills/tactics-git-workspace/SKILL.md:22`、`:86`。
理由：单项目禁令（不得再建 project.godot）跨文件 6 处重复。
建议 diff：建议不动（硬约束：事故溯源）。
事故溯源初判：是（`wrong-project-root` 事故：Build 成功但 tooling/内容缺失，根因是 Editor 打开了别的项目根）。

**C2-7** 自动证据不得充当人工验收/视觉通过（9 处）
类别 2｜锚点：`AGENTS.md:17`、`AGENTS.md:59`、`.agents/skills/manual-qa-handoff/SKILL.md:27`、`:36`、`.agents/skills/gameplay-design-constraints/SKILL.md:64`、`.agents/skills/godot-editor-lifecycle/SKILL.md:59`、`.agents/skills/godot-content-migration/SKILL.md:38`、`.agents/skills/pure-run-artwork-pipeline/SKILL.md:106`、`:158`。
理由：同一"自动≠人工"禁令在 AGENTS.md 两处、5 个 skill 中重复 7 种措辞。
建议 diff：建议不动（spec S2.1：安全与合规边界——人工验收账本诚实性；只列观察）。若主循环裁决收敛：canonical 留 `AGENTS.md:59` 与 `manual-qa-handoff:27`，其余改短指针。
事故溯源初判：否（其中 `godot-content-migration:38` 属迁移审计验收口径=是；其余为合规边界，无事故记录）。

**C2-8** 前台交互 default-deny / 不得抢占焦点（7 处）
类别 2｜锚点：`AGENTS.md:16`、`.agents/rules/foreground-interaction.md:5`（canonical）、`.agents/skills/godot-editor-lifecycle/SKILL.md:8`、`:56`、`.agents/skills/pure-run-artwork-pipeline/SKILL.md:100`、`:153-154`、`.agents/skills/manual-qa-handoff/SKILL.md:31`。
理由：同一 default-deny 禁令在 AGENTS.md、政策文件、3 个 skill 中以摘要句重复。
建议 diff：建议不动（spec S2.1：安全与合规边界；只列观察）。
事故溯源初判：否（安全政策，无事故记录）。

**C2-9** Editor session 0 → 正常关闭并按原状态恢复、不强杀（8 处）
类别 2｜锚点：`AGENTS.md:15`、`AGENTS.md:53`、`.agents/rules/godot-agent-workflow.md:40`、`.agents/rules/foreground-interaction.md:14`、`:28-36`、`.agents/skills/godot-editor-lifecycle/SKILL.md:8`、`:18`、`:41`、`:56`、`.agents/skills/godot-ai-workflow/SKILL.md:27`、`.agents/skills/godot-workflow/SKILL.md:31`、`.agents/skills/tactics-git-workspace/SKILL.md:18`、`:46`。
理由：生命周期关闭/恢复禁令在 2 规则 + 4 skill 中反复展开（skill 骨架回声未计入独立表述）。
建议 diff：建议不动（安全与合规边界 + 疑似事故溯源；只列观察）。
事故溯源初判：不确定（`editor-dock-lifecycle`、`csharp-assembly-reload-*`、`typed-resource-reload` 同域未互引；`foreground-interaction.md` 为上位安全政策）。

**C2-10** Incident 晋升边界（verified 才进 OKF、重复流程才进 Skill）（4 处）
类别 2｜锚点：`.agents/rules/godot-agent-workflow.md:55-57`、`.agents/skills/godot-workflow/SKILL.md:36`、`.agents/skills/godot-testing-diagnostics/SKILL.md:24`、`.agents/skills/godot-editor-tooling/SKILL.md:27`。
理由：同一晋升边界句在规则与 3 个 skill 中重复。
建议 diff：建议不动（硬约束：事故溯源）。
事故溯源初判：是（`.agents/incidents/godot/schema.md` 晋升顺序原文即该边界；事故记录体系自体规则）。

**C2-11** 禁止并行构建/测试（obj 争抢）（4 处）
类别 2｜锚点：`.agents/rules/godot-agent-workflow.md:15`、`.agents/skills/godot-testing-diagnostics/SKILL.md:10`、`:33`、`.agents/skills/gameplay-test-framework/SKILL.md:109`（扩展为单一 mutating owner）。
理由：同一串行要求在规则与 2 个 skill 中重复（后者扩大到 Editor/runner/verifier 互斥）。
建议 diff：建议不动（硬约束：事故溯源）。
事故溯源初判：是（`parallel-build-obj-contention`，且 `godot-agent-workflow.md:15` 内文显式引用"已验证会争抢 obj 文件"）。

**C2-12** OKF 不是实现真相源，须核对 `repo_paths`（4 处）
类别 2｜锚点：`AGENTS.md:66`、`.agents/rules/knowledge-maintenance.md:67`、`:111`、`.agents/skills/knowledge-maintenance/SKILL.md:82`。
理由：同一"回代码/Resource/测试核对"原则 4 处重复（skill 反模式行与规则禁止事项措辞几乎同义）。
建议 diff：canonical 留 `.agents/rules/knowledge-maintenance.md:111`（与 :67 择一）；`SKILL.md:82` 反模式行 → `把 OKF 当实现真相源 | 见 rules/knowledge-maintenance.md 禁止事项`；`AGENTS.md:66` 保留（Agent 约束区单行原则）。
事故溯源初判：否。

**C2-13** 不吸收/不清理任务外无关修改（dirty 文件保护）（4 处）
类别 2｜锚点：`AGENTS.md:48`（绝对禁止 5）、`.agents/rules/knowledge-maintenance.md:83`、`.agents/skills/knowledge-maintenance/SKILL.md:50`、`.agents/skills/tactics-git-workspace/SKILL.md:42`。
理由：同一"任务外 dirty 文件不得覆盖/暂存/清理/吸收"禁令在绝对禁止、规则、2 skill 重复。
建议 diff：建议不动（硬约束：绝对禁止清单）。
事故溯源初判：是（绝对禁止清单）。

**C2-14** Editor 只经 `Tools/godot/Open-GodotDev.ps1` 启动（4 处）
类别 2｜锚点：`AGENTS.md:13`、`.agents/rules/godot-agent-workflow.md:33`、`.agents/skills/godot-ai-workflow/SKILL.md:14`、`:43`。
理由：同一入口禁令 4 处重复。
建议 diff：建议不动（疑似事故溯源 + MCP/Editor 操作细节；只列观察）。
事故溯源初判：不确定（`wrong-project-root`、`parallel-build-obj-contention` 同域——入口串行 Build 与项目根校验正是事故对策，但句身未显式引用）。

**C2-15** MCP 永久 deny-list（`script_*`、`filesystem_manage` 等）（2 处）
类别 2｜锚点：`.agents/rules/godot-agent-workflow.md:43`、`.agents/skills/godot-ai-workflow/SKILL.md:44-45`。
理由：同一永久禁用工具清单在规则与 skill 重复（skill 附 Cursor hook 边界说明）。
建议 diff：建议不动（硬约束：绝对禁止清单——manifest `alwaysForbiddenTools` 硬拦截）。
事故溯源初判：是（绝对禁止清单）。

**C2-16** `knowledge-maintenance` skill 对规则 Profile 要点的复述
类别 2｜锚点：`.agents/skills/knowledge-maintenance/SKILL.md:54-60`（"遵循 .agents/rules/knowledge-maintenance.md：" 后又逐条复述 active 唯一/frontmatter/链接/superseded_by 等 5 条），对照 `.agents/rules/knowledge-maintenance.md:43-53`。
理由：先指针后全文复述，指针失效。
建议 diff：`:54-60` 五条要点 → `遵循 .agents/rules/knowledge-maintenance.md 的 Profile 规则与禁止事项。`。
事故溯源初判：否（备注：属 OKF Profile 操作细节，主循环可裁决只列观察）。

**C2-17** 未知/版本敏感行为 → Research Guide 路由（4 处全文表述）
类别 2｜锚点：`AGENTS.md:52`、`.agents/rules/godot-agent-workflow.md:48`、`.agents/skills/godot-workflow/SKILL.md:32`、`:46`；指针式引用（不计重复）：`godot-csharp-development:23`、`godot-testing-diagnostics:20`、`godot-editor-tooling:27`。
理由：同一"未知 API/引擎错误走 Research Guide"指令 4 处全文展开；指针式引用不算。
建议 diff：canonical 留 `.agents/rules/godot-agent-workflow.md:48`；`godot-workflow:46` 防御措辞并入 :32 后删除（措辞问题见 C3-1）；`AGENTS.md:52` 保留单行。
事故溯源初判：不确定（Research Guide 为制度化实践；`script-mainloop-entry` 等版本敏感坑同源）。

**C2-18** 迁移审计边界重复（删除冻结证据须单独确认、自动≠真实验收）
类别 2｜锚点：`.agents/rules/godot-migration.md:14` ↔ `AGENTS.md:47`（绝对禁止 4）；`.agents/rules/godot-migration.md:13` ↔ `AGENTS.md:17`/`AGENTS.md:59`（C2-7 家族）。
理由：同一"未经确认不得删除 FrozenOracle/Golden/receipt/许可证证据"禁令两处全文。
建议 diff：建议不动（硬约束：Godot 迁移审计 + 绝对禁止清单）。
事故溯源初判：是（迁移审计：retirement manifest / receipt 证据链）。

### 类别 3：时代补丁（旧模型失败模式的通用防御句，无事故溯源）

**C3-1** `.agents/skills/godot-workflow/SKILL.md:46`
类别 3｜理由："Do not answer version-sensitive Godot questions from model memory" 是防训练数据臆答的通用防御句（"不要凭记忆"族），且与 :32 的 Research Guide 路由重复。
建议 diff：`Do not answer version-sensitive Godot questions from model memory.` → 删除（语义已由 :32 "follow references/research-guide.md before changing code" 承担）；若保留则改为意图式 `Version-sensitive behavior is settled through the Research Guide with evidence labels.`。
事故溯源初判：不确定（Research Guide 制度化实践；无事故直接引用）。

**C3-2** `.agents/skills/plan-plain/SKILL.md:44`
类别 3｜理由："（仍须核对计划正文，不得虚构）"是"不要编造"类防御括注，与 :162 "与正文事实一致：不引入计划里没有的承诺"重复。
建议 diff：`（仍须核对计划正文，不得虚构）` → `（人话版与计划正文一致）`。
事故溯源初判：否。

**C3-3** `.agents/skills/gameplay-test-framework/SKILL.md:32`
类别 3｜理由："不要根据旧阶段文档猜 action/assertion 名称"是"不要猜/不要编造"类防御句；支持面真相源前半句已表达。
建议 diff：`检查 Tools/gameplay-test-spec/src 的 schema/validator/compiler 和 Godot runtime runner 的 adapter。不要根据旧阶段文档猜 action/assertion 名称。` → `action/assertion 支持面以 Tools/gameplay-test-spec/src 当前 schema/adapter 与 runner 为准。`。
事故溯源初判：否。

**C3-4** `.agents/skills/gameplay-test-framework/SKILL.md:95`
类别 3｜理由："不跳过 validator 直接改生成计划"是"不要跳步"类防御句；`.plan.json` 由 compiler 产出的边界已在 :39/:126 声明。
建议 diff：`校验失败必须修复 Spec 或 schema，不跳过 validator 直接改生成计划。` → `校验失败时修复 Spec 或 schema，由 compiler 重新生成计划。`。
事故溯源初判：否。

**C3-5** `.agents/skills/gameplay-test-framework/SKILL.md:77`
类别 3｜理由："不得因 JSON 合法就视为 DSL 或玩法合法"是"不要轻信"类防御后半句；前半句"必须继续执行 validate-draft / compile-draft"已构成流程约束。
建议 diff：`模型输出必须继续执行 validate-draft / compile-draft，不得因 JSON 合法就视为 DSL 或玩法合法。` → `模型输出一律经 validate-draft / compile-draft 验证后才进入编译。`。
事故溯源初判：否。

**C3-6** `.agents/skills/gameplay-test-framework/SKILL.md:111`
类别 3｜理由：尾句"不得自行忽略"是"不要偷懒"类通用补丁，前半句"必须修复"已表达。
建议 diff：`…不得自行忽略。` → 删除尾句（保留"当前任务引入的失败必须修复。疑似基线失败需有基线复现或权威记录，并按用户许可或仓库政策决定是否继续。"）。
事故溯源初判：否。

**C3-7** `.agents/rules/code-documentation.md:19`、`:119-124`
类别 3｜理由：括注"(no commenting obvious code)"与 What NOT to Comment 清单是对旧模型过度注释失败的通用防御（"不要写废话注释"族），且 :126-178 示例段已正反示范。
建议 diff：`:19` 括注删除，保留 `Comments explain why and rules, not what.`；`:119-124` 清单压缩为一条 `Do not comment obvious code or restate the code.`
事故溯源初判：否。

**C3-8** `.agents/skills/pure-run-artwork-pipeline/SKILL.md:112`
类别 3｜理由："不批量生成多个角色来「碰运气」"是"不要偷懒/不要赌"类口语化防御句；series 预算与 record-feedback 状态机（:67）已有强制门禁。
建议 diff：`不批量生成多个角色来"碰运气"，不复制完整聊天记录或完整提示词到 OKF；` → `单角色逐张推进；不复制完整聊天记录或完整提示词到 OKF；`（后半句 OKF 治理保留）。
事故溯源初判：否。

**C3-9** `.agents/skills/gameplay-test-framework/SKILL.md:120`
类别 3｜理由："不是绕过 Agent 收束和人工确认的理由"是"不要走捷径"类防御句式。
建议 diff：`自然语言 generate-spec 是辅助入口，不是绕过 Agent 收束和人工确认的理由。` → `generate-spec 仅辅助产出草案；仍需 Agent 收束与人工确认。`。
事故溯源初判：否。

**C3-10** `.agents/skills/godot-testing-diagnostics/SKILL.md:19`、`:34`
类别 3｜理由："do not hide it with a full clean / 不得先删 .godot 再保留签名"是"不要掩盖现场"类防御句，形态为时代补丁但保护事故证据链。
建议 diff：建议不动（硬约束：事故溯源）。
事故溯源初判：是（`.agents/incidents/godot/schema.md` 强制首错误 `signature`；签名保留是事故立案前置）。

### 只列观察（无候选或硬约束，不进修订面）

- `AGENTS.md:3、:31、:47`：Unity 退役与冻结证据边界——迁移审计硬约束，不产候选。
- `AGENTS.md:42-48`（绝对禁止清单全 5 条）：硬约束清单本体，不产候选（其跨文件重复见 C2-2/C2-3/C2-13/C2-18）。
- `.agents/rules/agent-environment.md`：机器电源运维程序（Modern Standby keep-awake 脚本对），非模型行为规则，无候选。
- `.agents/rules/foreground-interaction.md` 全文：前台安全政策（policy-id），spec S2.1 安全边界；无修改候选。
- `.agents/rules/godot-migration.md` 全文：迁移审计硬约束；重复项见 C2-18。
- `.agents/skills/artworks-prompt-library/SKILL.md`：正文为提示词库生产教程/格式参考（不审）；:162-169 边界声明（第一版不生图/不碰 Godot import）为作用域合同，无候选。
- `.agents/skills/godot-content-migration/SKILL.md:29-46`（Examples/Anti-patterns/Checklist）：迁移审计口径，硬约束。
- `.agents/skills/godot-editor-tooling/SKILL.md:26`（Ctrl+Z/Ctrl+Y 验证项）：验收动作枚举，形态微具体但与 `csharp-assembly-reload-duplicate-type` 事故的用户验证面一致——事故溯源=是，建议不动。
- `.agents/skills/godot-ai-workflow/SKILL.md` 配置事实（版本 4.3.1、8000/9500、Profile 相序）：工具操作细节（spec S2.1），只列观察；deny-list 重复见 C2-15。
- `.agents/rules/knowledge-maintenance.md:26-41、:99-102` 与 skill 命令块：OKF frontmatter/Lint 操作细节，不审不改。
- `.agents/rules/subagent-handback.md:44-49` 示例回执、`:25-30` 四段契约本体：契约/示例，无候选（行数微限见 C1-4；"自我声明不算证据":32 为五字原则，无补丁形态，不动）。
- `.agents/skills/manual-qa-handoff/SKILL.md:23-29`：人工验收账本操作协议（references/output-contract.md 特化），合规边界。
- `.agents/skills/gameplay-design-constraints/SKILL.md:62`（"从旧 Unity 类结构反推当前规则"）：迁移审计同域反模式，建议不动。
- `.agents/skills/tactics-git-workspace/SKILL.md:33-66` Step 1-4：git 操作教程不审；例外清单重复见 C2-5。
- `AGENTS.md:65`、`.agents/rules/subagent-handback.md:32`：短原则（证据认识论 / 自我声明不算证据），无补丁形态，不动。

### 初筛统计

- 候选合计 44 条：类别 1 共 16 条（建议不动 9、可简化 7）；类别 2 共 18 条（建议不动 14、可简化 4）；类别 3 共 10 条（建议不动 1、可简化 9）。
- 事故溯源初判：是 14 条（全部"建议不动"，含绝对禁止/验证阶梯/迁移审计/Incident 体系）；不确定 7 条；否 23 条。
- 24 个文件全量覆盖，无未覆盖文件；非规则内容跳过均已在覆盖清单注明。

## 裁决（主循环，S2.2/S2.3）

裁决口径合并任务书与 spec S2.1：事故溯源=是/不确定 → 保护不动；硬约束域（绝对禁止/验证阶梯/迁移审计/OKF 门禁操作/安全合规）→ 不动；跨文件"重复"按独立加载自含性处理 → 不动；仅"溯源=否 + 真过度具体/时代补丁 + 语义零丢失"→ 修。

### 直接修订（13 条）

| 编号 | 锚点 | 修订摘要 |
|---|---|---|
| C1-1 | plan-plain:48 | 题数微参数 → "题数以收束需要为限" |
| C1-2 | plan-plain:150 | "≤40 字" → "尽量一句话讲清" |
| C1-3 | plan-plain:163 | 删"约 15–35 行"括注 |
| C1-7 | godot-workflow:28 | `migration/godot` 硬编码（迁移期残留）→ 当前 workspace 口径，项目根检查保留 |
| C1-8 | godot-csharp-development:22 | "先搜索再命名"微步骤 → "优先复用"意图式 |
| C3-2 | plan-plain:44 | "不得虚构"括注 → "与计划正文一致" |
| C3-3 | gameplay-test-framework:32 | "不要猜 action 名称" → 真相源正面表述 |
| C3-4 | gameplay-test-framework:95 | "不跳过 validator 直接改" → "由 compiler 重新生成"（产物所有权语义保留） |
| C3-5 | gameplay-test-framework:77 | "不得因 JSON 合法…" → 正面流程表述 |
| C3-6 | gameplay-test-framework:111 | 删尾句"不得自行忽略" |
| C3-7 | code-documentation:19 | 删"(no commenting obvious code)"冗余括注（119-124 清单保留） |
| C3-8 | pure-run-artwork-pipeline:112 | "不批量碰运气" → "单角色逐张推进" |
| C3-9 | gameplay-test-framework:120 | "不是绕过…的理由" → 正面表述 |

### 不修订（31 条）及理由

- **事故溯源=是（14 条）**：C1-11、C1-12、C1-16、C2-1、C2-2、C2-3、C2-4、C2-6、C2-10、C2-11、C2-13、C2-15、C2-18、C3-10。
- **事故溯源=不确定 → 保守保护（6 条）**：C1-5、C1-6、C1-13、C2-9、C2-14、C2-17。
- **硬约束域只列观察（6 条）**：C1-9（提交检查=门禁操作细节）、C1-10、C1-14、C1-15（OKF/派发配方操作细节）、C2-7（自动≠人工=合规边界）、C2-8（前台 default-deny=安全政策）。
- **独立加载自含性（3 条）**：C2-5（worktree 例外清单供 skill 独立加载）、C2-12、C2-16。跨文件"重复"在 agent 指令文件中多为自含性设计而非 Frankenprompt——此为类别 2 的通用裁决原则。
- **可裁决锚点（2 条）**：C1-4（"≤10 行"是新契约回执的可裁决参数，非旧模型补丁）；C3-1（"不凭记忆答版本敏感问题"兼为 GO-04 golden eval 的判据锚点，且溯源不确定——删除会使 eval 失锚）。

注：初筛统计"不确定 7 条"含 C3-1；裁决时 C3-1 移入本组（可裁决锚点）并仍不修改，故保护组为 6 条，非漏裁决。

修订落地于 6 个文件（plan-plain、godot-workflow、godot-csharp-development、code-documentation、gameplay-test-framework、pure-run-artwork-pipeline），diff 见 git；被审文件的语义边界经主循环逐条比对，零丢失。
