---
feature: agent-workflow-map
status: in-progress
updated: 2026-11-11
source: docs/agent-workflow-map
---

# Agent 工作流基础设施地图（双读者薄档案）

## Report

## [S1] 问题

围绕 Anthropic"何时把子代理折叠进主循环"一文做的 P0–P4 优化线已全部落地并分散留档（6 份 spec 的 Report/Journey log、评测基线、skill/prompt 两份审计报告、规则与工具本身）。缺的是一张**地图**：为什么有这一串、五件事如何咬合、未来什么场景用哪个入口、哪些教训横跨全部工作。新开发者（或未来 AI 会话）翻到散落产物能看懂局部，但看不到全局；细节已有归宿，不应重写。

## [S2] 设计

### S2.1 文档形态（双读者薄地图）

单文件，约 150 行，两段式：

1. **上半·人话回顾**（给开发者）：缘起（文章与"先度量再改"方法论）；五件事各一句话（P0 评测尺子 / keep-awake / P1 派活契约 / P2 派发配方 / P3 体检 / P4 清补丁）及其咬合顺序（尺子→规矩→实战→体检→清补丁，keep-awake 横切）；跨切面教训 8–9 条（从各 Journey log 聚合：先度量再动架构、盲态泄漏、自我声明不算证据、自含性≠Frankenprompt、事故溯源≠旧补丁、506≠冻结、机械段按命令固有行为划界、契约过两道筛、局部修改用编辑）。
2. **下半·AI 使用索引**（给 agent）：场景→入口表（改玩法数值 / 派活拆分 / 门禁日志分诊 / OKF 同步 / 改规则后验证 / 长任务防睡 / 多会话并行），每行指针到具体文件路径。

**硬规则：零复述**——细节一律以路径指针指向既有 spec/报告/规则/工具；地图不创造任何新约束（约束只住在 `.agents/rules/`）；不复述任何 spec 的 Report 内容。

### S2.2 落点与入口

`.agents/docs/agent-workflow-infrastructure.md`（无日期领域文档惯例）。可发现性三处一行指针：`AGENTS.md` 当前规则索引区（标注"参考"）、`CONTRIBUTING.md`（开发者入口）。产品 README（中/英）不动——受众是产品/许可证，不混 agent 工具链内容。

### S2.2b 本机环境坑归拢

评测与试点跑出的环境坑（`ProgramFiles` 被置空致 dotnet/NuGet 崩、`python` 不在 PATH 需 `py -3`、GdUnit 需 `GODOT_BIN` 与 `--import` 前置、`Tools/vendor/maliang` 子模块未初始化致 verifier 误报）目前散落在各 Journey log 与运行记录。归拢进 `.agents/rules/agent-environment.md` 新增小节（与 Modern Standby 同族的环境事实），各条注明一行成因与修法；不新增行为约束。

### S2.3 工作区覆盖声明

隔离 worktree `.worktrees/agent-workflow-map`（共享检出停留在其他会话分支且带脏文件），基于 `origin/main`（全部 PR 已合并，指针目标均在树内）。

## [S3] Out of Scope

- 修改任何既有产物内容（specs/报告/规则/工具）。
- 把地图写成 OKF 概念页（frontmatter/index/scope 治理成本不值）。
- P5（扩 case、hillclimb、时间戳）内容；英文版；时间线叙事（被 S2.1 排除）。

## Tasks

- [ ] T1: 写地图文档 — acceptance: 五件事齐且咬合逻辑清楚；教训 8–9 条均可溯源到 Journey log；场景索引每行有有效文件路径；全文零复述（无细节拷贝、无新约束）。(covers: S2.1, S2.2)
- [ ] T2: 入口指针 — acceptance: AGENTS.md 索引区与 CONTRIBUTING.md 各新增一行指向地图，格式与邻居一致。(covers: S2.2)(depends: T1)
- [ ] T3: 本机环境坑归拢进 agent-environment.md — acceptance: 四类坑齐（成因+修法各一行），来源注明散落记录；不新增行为约束。(covers: S2.2b)
- [ ] T4: 独立 review 与收口 — acceptance: review 重点核验零复述/指针有效/教训准确/环境坑成因修法属实，无关键缺口；Report 记录验证结果。(covers: S2.3)(depends: T1, T2, T3)

## Journey log
