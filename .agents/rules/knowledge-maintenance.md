# OKF 知识维护规范

## 目标

`.agents/knowledge/` 是 Tactics 的独立 Open Knowledge Format v0.1 bundle。它负责跨系统导航、当前状态综合和关系表达，不复制或替代设计、计划、代码、Godot Resource 和测试。

## 权威顺序

| 问题类型 | 首要真相源 | OKF 的作用 |
|----------|------------|------------|
| 设计意图 | `.agents/docs/` 的主题权威文档 | 汇总结论并连接相关系统 |
| 活跃执行计划 | `.agents/plans/` | 展示状态、依赖和替代关系 |
| 当前实现 | 代码、Godot Resource、测试 | 提供入口和最后验证 revision |
| Agent 工作流 | `AGENTS.md`、rules、skills | 提供渐进导航，不自动改写规则 |

发生冲突时，OKF 页面必须回到相应真相源复核并更新，不能用综合页覆盖源事实。

`.agents/docs/brainstorm.md` 是未经验证的临时灵感收集箱，不属于设计真相源，也不自动进入 OKF 系统概念或已知缺口。

`.agents/plans/` 只保存仍需执行的计划。实施完成后，长期结论迁移到权威 docs，未实施项进入统一缺口或经批准的新计划，completed plan 删除并由 Git 保留历史。OKF 自身需要保留的历史概念使用 `archived` 或 `superseded`。

## Tactics OKF Profile 0.1 与自动同步扩展

每个非保留概念文档必须包含：

```yaml
---
type: Game System
resource: https://github.com/cty41/tactics
title: Example System
description: 一句话说明该概念负责什么。
tags: [gameplay]
timestamp: 2026-07-14T00:00:00+08:00
status: active
catalog_scope: example-system
repo_paths:
  - godot/src/Tactics.Godot.Adapter/Example
verified_revision: d5f1730d3527
source_fingerprint: sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
---
```

规则：

- OKF 核心必填字段是 `type`；Tactics Profile 额外要求 `title`、`description`、`timestamp`。
- `status` 使用 `draft | active | superseded | archived`。
- 同一 `catalog_scope` 最多存在一个 `active` 概念。
- 声明当前实现状态的概念必须包含有效 `repo_paths` 和 `verified_revision`。
- 经自动同步的概念额外包含 `source_fingerprint`；它记录当前工作区来源状态，为后续提交前或 CI 新鲜度校验预留接口。
- `superseded` 概念必须提供 `superseded_by`，并在正文中链接替代概念。
- 内部关系使用 Markdown 链接表达。OKF 支持 bundle 根相对和普通相对链接；Tactics Profile 使用普通相对链接，以兼容当前 OKF reference viewer。
- 外部事实在正文末尾使用 `# Citations`；不复制许可不明的完整外部文章。
- 消费者必须保留未知 frontmatter 字段。

## 保留文件

- `index.md`：渐进披露目录。根 index 可以用 frontmatter 声明 `okf_version` 和 `tactics_profile`；子 index 不使用 frontmatter。
- `log.md`：按 `## YYYY-MM-DD` 倒序记录知识变化。
- 普通只读查询不写 log；只有 Creation、Update、Deprecation 或 Lint 导致知识状态变化时记录。

## 工作流

### Query

1. 从 `.agents/knowledge/index.md` 开始。
2. 逐级打开相关概念，不一次加载整个 bundle。
3. 若问题涉及当前实现，核对 `repo_paths` 指向的代码、资产和测试。
4. 默认只读，不因普通问答修改知识库。

### Ingest

1. 搜索已有 `catalog_scope`。
2. 阅读原始来源和受影响的真相源。
3. 更新已有概念，避免按会话创建重复页面。
4. 更新关系链接、目录 index 和根 log。
5. 运行 `python Tools/okf/validate_bundle.py`。

### Source Change Sync

Agent 修改代码、Godot Resource、设计、计划、规则或工具后：

1. 运行 `python Tools/okf/catalog_impact.py report --worktree`。
2. 只处理本任务实际引入的变更；工作区中已有的无关修改不得吸收到本次同步。
3. 阅读受影响概念及对应真实 diff，更新 Current State、Relationships、Verification Guidance 或 Citations 中已经变化的结论。
4. 对每个实际影响的 scope 运行 `python Tools/okf/catalog_impact.py sync --worktree --scope <scope> --write`。
5. 运行 bundle 校验和 OKF 单元测试。

`sync` 只自动更新时间、来源指纹和根日志，不替代 Agent 对正文语义的核对。未映射的受监控路径必须加入已有 scope、建立新概念，或经核对后加入明确的忽略规则。第 1、4、5 步（report → sync --write → validate）的机械段可按 `.agents/rules/subagent-handback.md` 已验证派发配方拆给子代理；第 2、3 步的变更甄别与正文落笔仍由主循环完成。

### Supersede

1. 保留旧概念文件。
2. 将旧概念标记为 `superseded` 并填写 `superseded_by`。
3. 在新旧正文中建立双向说明。
4. 更新 index 和 log。

### Lint

```powershell
python Tools/okf/validate_bundle.py
python -m unittest discover Tools/okf -p "test_*.py"
```

OKF v0.1 允许断链，但 Tactics Profile 将 bundle 内断链视为错误。

## 禁止事项

- 不把 `.agents/` 整体当作 OKF bundle。
- 不把聊天记录逐条写成概念。
- 不在 OKF 页面复制完整代码或大段原始设计。
- 不仅凭 OKF 综合页判断当前实现。
- 不由普通 ingest 自动修改 `AGENTS.md`、rules 或 skills。
