# Skill 体检报告 · 2026-11

P3 体检产物。数据口径见各节；处置边界见 `docs/compose/spec/skill-audit.md`（S2.4：低风险直接修，删除/重组只列建议）。

## 用量数据表

以轨迹库 `mimocode.db` 的 `history_fts` 表为准（主口径：`tool_name='skill'` 且 body 含 `{"name":"<skill>"}`；`time_created` 为 epoch 毫秒，换算 `time_created/1000 + 28800` 呈现 UTC+8）。辅助列（SKILL.md 读取次数）为参考口径，含 body 共现的近似归因，不作判定依据。

| 技能名 | 加载次数 | 最近一次加载时间（UTC+8） | SKILL.md 读取次数（参考） |
|---|---:|---|---:|
| artworks-prompt-library | 0 | — | 5 |
| gameplay-design-constraints | 5 | 2026-11-09 11:57:23 | 1 |
| gameplay-test-framework | 12 | 2026-08-07 14:20:11 | 160 |
| godot-ai-workflow | 0 | — | 1 |
| godot-content-migration | 1 | 2026-09-24 16:39:02 | 1 |
| godot-csharp-development | 3 | 2026-11-09 12:04:18 | 1 |
| godot-editor-lifecycle | 0 | — | 2 |
| godot-editor-tooling | 0 | — | 1 |
| godot-testing-diagnostics | 2 | 2026-11-09 11:10:52 | 1 |
| godot-workflow | 5 | 2026-11-09 11:11:18 | 1 |
| knowledge-maintenance | 22 | 2026-08-07 19:10:56 | 5 |
| manual-qa-handoff | 1 | 2026-11-09 11:25:58 | 3 |
| plan-plain | 1 | 2026-11-10 17:59:14 | 9 |
| pure-run-artwork-pipeline | 3 | 2026-08-07 10:40:02 | 10 |
| tactics-git-workspace | 0 | — | 1 |

全库 `tool_name='skill'` 记录 459 条 / 59 个技能名；上述 15 个合计 55 条。非 15 技能 top5（重叠分析参考）：review-agent 69、unity-git-commit 36、pdf-official 26、project-coding-reference 25、unity-auto-compile-guard 24。

### 查询口径

```sql
-- 主口径：每技能加载次数与最近时间
SELECT COUNT(*), MAX(time_created) FROM history_fts
WHERE tool_name='skill' AND body LIKE '%"name":"<skill>"%';
-- 时间呈现：datetime(time_created/1000 + 28800, 'unixepoch')（固定 UTC+8）
-- 辅助口径（参考）：tool_name='read' AND body LIKE '%SKILL.md%' AND body LIKE '%<skill>%'
-- top5：从 tool_name='skill' 的 body 用 instr/substr 提取 {"name":"..."} 段后分组计数
```

主口径已由主循环独立抽查复算（knowledge-maintenance 22、godot-ai-workflow 0、godot-workflow 5、plan-plain 1 全部一致）。

## 触发边界重叠清单与修订

比对范围：15 个项目 skill 的 `description` 触发段两两比对，并对照 `.agents/rules/` 与 `AGENTS.md` 的职责表述。以下 4 对为真重叠，已按低风险方式直接修订 description（仅措辞消歧，未删语义）：

| 重叠对 | 冲突点 | 修订 |
|---|---|---|
| godot-workflow ↔ 各 specialist | 路由器 description 写成"changes/diagnoses/tests/migrates 全覆盖"，与自身 body 的"仅当边界不明确时"矛盾，直接抢 specialist 的触发 | description 对齐 body：定位为"边界/证据层级不明确时的路由入口"，domain 明确时直达 specialist |
| godot-content-migration ↔ godot-csharp-development | "Resource" 双义：迁移批量资产 vs C# 类代码 | content-migration 限定 "migration-batch"，csharp 标注 "Resource 指 C# 类"，互相指向 |
| godot-testing-diagnostics ↔ gameplay-test-framework | "testing" 撞车：测试运行 vs 测试编写 | testing-diagnostics 明示"gameplay 自动化测试编写归 gameplay-test-framework" |
| artworks-prompt-library ↔ pure-run-artwork-pipeline | 美术双雄：写 prompt 文档 vs 生产管线 | prompt-library 标注 "prompt-doc authoring only"，生成/色键/审查指向 pipeline |

**未修订的可接受重叠**：godot-editor-lifecycle ↔ godot-editor-tooling（"Editor" 词面重叠但语义分工清晰：会话生命周期 vs 插件代码改动，且官方示例本就要求两者同载）；plan-plain ↔ compose-next（用户级/显式调用边界，项目 skill 无法单侧修复）。

## 建议清单（只列不动手）

- **零加载 skill（5 个）**：godot-ai-workflow、godot-editor-lifecycle、godot-editor-tooling、tactics-git-workspace、artworks-prompt-library。均为情境性触发（Editor 会话、godot-ai MCP、worktree 操作、prompt 文档创作），**不建议删除**；但 godot-ai-workflow 与 tactics-git-workspace 的触发面可能过窄（近 30 天有 worktree 与 godot-ai 活动却零加载），建议下轮体检前观察其是否"该触发而未触发"。
- **删除候选（需人工批准才动手）**：无。5 个零加载 skill 都有明确场景价值，无死重。
- **结构性重组建议**：无紧迫项。godot-workflow 路由与 specialist 的边界经本次消歧后可维持现结构。

## log.md 归档

- 归档前 628 行 → 现 107 行（`splitlines()` 总行口径，含 `## 归档` 指针段与归档后 sync 增段）。
- 切口：保留 `## 2026-09-16` 及更新的段；`## 2026-09-15` 及更早按月移入 `log-archive-2026-09/08/07.md`（57/318/157 行），归档段此后只读；两文件接缝各少一条空白分隔行（标题/条目零丢失，拼接还原时补回）。
- 工具兼容实测：归档后触发真实 sync（2 个 scope，按 P2 配方派发机械段）——`update_log` 新日期段正常插入顶部、同日同 scope 去重替换正常、归档文件零触碰。
- **发现与修正（pivot）**：`validate_bundle` 将非保留名 `.md` 一律按概念页校验，归档文件触发 6 错（缺 frontmatter、未收录 index）。裁决采用「扩保留名」方案：`validate_bundle.py` 对 `log-archive-` 前缀豁免（归档本就是 log 家族），补回归测试 `test_log_archive_files_are_reserved`；OKF 单测 17/17，真 bundle `OKF_CHECK_OK concepts=17`。
- 归档指针独立成 `## 归档` 段置于文件尾，避免被 `update_log` 的顶部插段挤进日期段内。
