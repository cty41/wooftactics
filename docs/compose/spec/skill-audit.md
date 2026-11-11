---
feature: skill-audit
status: in-progress
updated: 2026-11-11
source: feat/skill-audit
---

# P3 Skill 体检（用量审计 · 触发去重 · log 归档）

## Report

## [S1] 问题

15 个项目 skill 从未做过体检：没有任何用量数据（哪些真被加载、哪些是死重无从判断）；`godot-workflow` 路由与各 specialist、项目 skill 与用户级共享 skill 的触发描述存在语义重叠，加载靠临场猜测；`knowledge/log.md` 已 566 行 append 流且仍在增长，拖慢知识检索。文章的 skill-doctor / skill-creator 审计正是解法，P3 落地为一次带真实数据的体检。

## [S2] 设计

### S2.1 用量审计（数据口径）

以**工具调用痕迹**为准（轨迹库中的 `skill` 工具调用与 SKILL.md 读取记录），不采信自述。对 15 个项目 skill 逐一统计：加载次数、最近一次加载时间、典型加载场景。数据表写入体检报告；口径与查询方式（history / 轨迹 SQLite）随表注明，保证可复算。

### S2.2 触发边界去重

两两比对 15 个 skill 的 description/触发段，以及与 `.agents/rules/`、`AGENTS.md` 的文本重叠。产出"重叠对清单 + 建议措辞"。低风险项直接落地：触发词消歧、指向性收窄、补"何时不用本 skill"边界。结构性重组（合并 skill、拆分 skill）只列建议不执行。

### S2.3 log.md 归档

旧段按月移入 `.agents/knowledge/log-archive-YYYY-MM.md`，`log.md` 保留近期段与指向归档的独立 `## 归档` 段。前置核对：读 `Tools/okf/catalog_impact.py` 的 `update_log` 实现，确认归档切口不破坏其同日同 scope 去重替换行为（归档段此后只读）。配套修正（实施中发现）：`validate_bundle` 对 `log-archive-` 前缀文件按保留名豁免，否则归档文件会被当概念页误报。验收含"再触发一次 sync 不破坏归档结构"的实测。

### S2.4 处置边界

- 直接落地：description 触发去重、log.md 归档。
- 只列建议：删除 skill、合并/重组 skill、用户级 `~/.agents/skills` 的任何修改。

### S2.5 交付物与工作区

- 体检报告 `.agents/docs/skill-audit-2026-11.md`（用量数据表、重叠清单、建议清单、归档说明）。
- 修剪与归档的 diff 本身。
- **工作区覆盖声明**：本特性在隔离 worktree `.worktrees/skill-audit` 开发（`agent-worktree.md` 例外条款 1：并发会话正占用主检出做未提交修改，隔离是必要步骤），不触碰主检出。

## [S3] Out of Scope

- 删除/合并任何 skill（只出建议）。
- 用户级共享 skill（`~/.agents/skills`）的修改。
- P4 prompt audit 的全量执行（本次仅覆盖 skill 触发文本）。
- 新建 skill、skill 机检校验脚本。

## Tasks

- [ ] T1: 用量审计数据表 — acceptance: 15 个 skill 每个有加载次数与最近使用时间，查询口径可复算。(covers: S2.1)
- [ ] T2: 触发边界比对与低风险修订 — acceptance: 重叠对清单 + description 修订 diff；消歧后触发语义无丢失、无新增重叠。(covers: S2.2, S2.4)(depends: T1)
- [ ] T3: log.md 归档 — acceptance: log.md 降至 <200 行且保留近期段；归档文件完整；实测再触发一次 sync 不破坏归档结构。(covers: S2.3)
- [ ] T4: 体检报告与收口 — acceptance: 报告含数据表/重叠清单/建议清单；独立 review 无关键缺口。(covers: S2.5)(depends: T1, T2, T3)

## Journey log

- 实施发现的 pivot：`validate_bundle` 对 bundle 内非保留名 `.md` 一律按概念页校验（frontmatter + index 收录），log 月度归档文件会被误报 6 错。解法取「扩保留名」（`log-archive-` 前缀豁免 + 回归测试）而非伪装概念页或移出 bundle——归档属 log 家族，语义上就该和 `log.md` 同待遇。
- 对既有文件做局部修改要用编辑/追加，勿用整文件写入：T1 的用量数据表曾被覆盖，靠回执与抽查记录复原。
