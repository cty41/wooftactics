---
feature: contract-coverage-gate
status: in-progress
updated: 2026-11-09
source: https://github.com/cty41/wooftactics/pull/12
---

# contract-coverage 门禁接入 Verify 编排

## Report

## [S1] Problem

PR #12 让 `contract-coverage` 对 `dsl_support: supported` 且 `missing-spec` 的合同 fail-closed，并把门禁调用插进 `Tools/godot/Verify-GodotProject.ps1`。但 base `feat/all-tilemap` 在 PR 分叉后把 Verify 编排整体重构成 Python step registry（`Tools/godot/lib/verify_steps.py` + `verify_runner.py`），ps1 只剩转发到 `verify_godot_project.py`。两个后果：

1. `Tools/godot/Verify-GodotProject.ps1` 冲突——PR 往已被删空的 ps1 正文里插步骤。
2. 若简单取 base 的 ps1，门禁就没了调用方，计划「接入 contract-coverage 门禁」这一步落空，PR 任务 1 的验收无法成立。

另外，PR 描述与 `.agents/plans/grok-context-auto-test-coverage.md` §6.1 都写着「在 `Verify-GodotProject.ps1` 的 gameplay spec 编译步骤附近调用」。落地后这句话会失真，需要一处记录更正。

## [S2] Design

### S2.1 门禁归属：Python step registry，不写回 ps1

- `Verify-GodotProject.ps1` 冲突侧取 base 的精简转发版，丢弃 PR 的 `Invoke-Checked 'Enforce gameplay contract coverage gate'` 片段。
- 门禁改为 `Tools/godot/lib/verify_steps.py` 里的一条步骤 `gameplay_contract_coverage`，标题沿用 `Enforce gameplay contract coverage gate`，`kind="run"`。
- 执行体注册在 `Tools/godot/lib/verify_runner.py`，调用：
  `node Tools/gameplay-test-spec/dist/src/cli.js contract-coverage --docs .agents/docs --specs Tests/gameplay-specs --allow-missing-spec Tests/gameplay-specs/contract-coverage-allow-missing-spec-v1.json`
  用 `ctx.node_exe` 解析，不裸写 `node`。

### S2.2 位置与 profile

- 插在 `gameplay_spec_batch_compile` 之后。该步骤已调用 `_ensure_gameplay_spec_dist`，保证 `dist/src/cli.js` 存在，门禁不重复造轮子。
- agent 与 windows 两个 profile 都跑：把 step id 加进 `windows_profile_steps` 的 `shared_run_ids`。门禁是纯 Node/TS 检查，跨平台，PR 云端 Linux 验证已覆盖。
- 不进 `Verify-GodotProject.WindowsExtended.ps1`——那是 Windows-only 扩展体，门禁不属于那里。

### S2.3 白名单语义（PR 原有行为，合并后不变）

- `--allow-missing-spec` 只豁免白名单内基线缺口；不在白名单的新增 `missing-spec` 仍然失败。
- 白名单里出现已不再是 `missing-spec` 的 ID 时抛 `ContractCoverageAllowlistStale` 并失败，便于审计清理。

### S2.4 测试契约

- `test_verify_orchestration.py::test_agent_profile_unchanged_count` 是步骤数变更探测器，新步骤会让它从 19 变 20，必须显式更新，不能绕过。
- `test_windows_profile_ends_with_tooling_tests` 断言 windows profile 尾两步是 `windows_extended_verification` 与 `python_godot_tooling_tests`，新步骤插在共享段，不改变尾部，该断言应保持通过。

## [S3] Out of Scope

- 计划里的任务 3（Catalog 驱动参数化通用冒烟）与任务 4（文档与流程更新）。
- `Verify-GodotProject.ps1` 的 Linux 兼容性——base 的 Python 编排已解决，非本 PR 事务。
- Catalog 单位视觉契约测试（`AllGlobalCatalogUnitsSatisfyVisualResourceContracts`）本身：它是 PR 干净并入的部分，只随本次合并交付，不改其设计。
- 白名单基线内容的增删：8 个 ID 原样保留。

## Tasks

- [ ] T1: merge `origin/feat/all-tilemap` 进 PR 分支，`Verify-GodotProject.ps1` 冲突取 base 精简转发版 — acceptance: ps1 无冲突标记、含 `verify_godot_project.py` 转发、不含 `Invoke-Checked` 步骤体 (covers: S2.1)
- [ ] T2: `verify_steps.py` 注册 `gameplay_contract_coverage` 并加入 `shared_run_ids` — acceptance: agent 与 windows profile 均含该步骤，位置紧跟 `gameplay_spec_batch_compile` (covers: S2.1, S2.2)
- [ ] T3: `verify_runner.py` 注册 handler 调用 contract-coverage CLI — acceptance: 命令与 PR 原命令一致（含 allowlist 参数），用 `ctx.node_exe` (covers: S2.1, S2.3)
- [ ] T4: 更新 `test_verify_orchestration.py` 步骤数断言 19→20 — acceptance: `python -m unittest Tools.godot.tests.test_verify_orchestration` 通过 (covers: S2.4)
- [ ] T5: 跑 gameplay-test-spec 单测 + 门禁命令 + Tools/godot Python 单测 — acceptance: 全绿；门禁 exit 0 且 `uncoveredMissingSpec` 为空

## Journey log
