# 给远端 Agent 的上下文：让自动测试随新职业、新功能自动覆盖

> 本文自包含，供远端 Agent 读取并执行。仓库：`cty41/wooftactics`，分支基线 `feat/all-tilemap`（起点 `origin/main`）。
> 输出语言：计划、代码注释、调试说明用中文；代码标识符遵循 .NET 命名规范。

## 1. 项目背景

- 产品主线：Godot 4.7.1 C# 三职业 Pure Run。唯一 Godot 工程是 `godot/project.godot`。
- Unity 工程只作历史证据，不得编辑、生成或运行。
- 分层：`Tactics.Core` / `Tactics.Application` 不得引用 Godot；Node、Resource、文件系统、UI 在 `godot/src/Tactics.Godot.Adapter`。
- 本机是 Windows + PowerShell。Python 可能不在 PATH，可把本机 Python 安装目录（如 `%LOCALAPPDATA%\Python\pythoncore-3.14-64`）加入 PATH。

## 2. 硬性约束（违反即失败）

1. 不得手写或机械修改 `.tres/.tscn`，只能通过工厂、ResourceSaver 或受测转换器生成。
2. 不得让 Core/Application 引用 Godot API。
3. 不得覆盖、暂存、清理不属于当前任务的 dirty 文件。
4. 不得主动触发 GitHub Actions、远程导出或同类高耗时任务；也不得 `gh workflow run`。
5. 验证按成本递增：最小相关测试 -> 相关本地门禁 -> 至多一次 `Tools/godot/Verify-GodotProject.ps1`。
6. 提交前检查精确 staged 范围，并运行 `git diff --cached --check`。
7. 修改 `catalog-scopes.yaml` 监控范围后：先 `python Tools/okf/catalog_impact.py report --worktree`，更新受影响概念正文，再 `sync --worktree --scope <scope> --write`。
8. 自动测试不能写成人工验收通过；人工项只记 `manual_qa_pending`。
9. 不得删除 FrozenOracle、Golden、迁移 receipt 或许可证证据。

## 3. 现状（已用代码核对）

自动测试体系：

- Spec 工具链：`Tools/gameplay-test-spec`（TypeScript）。入口 `node Tools/gameplay-test-spec/dist/src/cli.js`。
  - 命令：`validate-spec`、`compile-spec`、`batch-validate`、`batch-compile`、`validate-contracts`、`contract-coverage`、`extract-contracts`、`generate-drafts` 等。
  - 合同文件：`.agents/docs/*.md` 中的 ```` ```gameplay-contract ```` fenced block 才是权威。
- Spec 源文件：`Tests/gameplay-specs/**/*.gameplay-test.md`，共约 147 个。
  - 统一验证只批量编译其中 `Tests/gameplay-specs/godot/` 子目录。
- Godot 运行器：`godot/tests/GameplaySpec/GodotGameplayRuntimeRunner.cs` 及 `GodotGameplayRuntimeRunnerTests.cs`。
  - 加载正式 `Main.tscn`，用 `Viewport.PushInput` 走生产输入链。
  - 每个场景只写 `user://qa-runner/<scenario>/<attempt>/`，并证明生产存档前后不变。
- 统一验证：`Tools/godot/Verify-GodotProject.ps1`。

## 4. 已确认的缺口

按严重程度：

1. 高：没有"新内容必须有合同和测试"的门禁。
   - `contract-coverage` 存在于 `Tools/gameplay-test-spec/src/cli.ts` 和 `contracts.ts`，但 `Verify-GodotProject.ps1` 没有调用。
   - 结果：新增职业、技能、机制即使没有任何合同和 Spec，统一验证也是绿的。
2. 中：Godot 端到端旅程是硬编码清单。
   - `Verify-GodotProject.ps1` 约第 394–431 行：`$expectedGodotGameplayScenarios` 为 20 个场景，并要求 `total -eq 20`、`passed -eq 20`。
   - 新增旅程必须手改清单和数量。这是有意的 fail-closed，不能直接删掉保护。
3. 中：Spec 被目录发现，但 `.plan.json` 必须手动编译并提交。
   - 验证会重新编译并比较 SHA256，不一致报 `Godot gameplay plan is stale or missing`。
4. 低：`Tests/gameplay-specs` 根目录的旧 Spec 不在统一验证内。
5. 部分自动、部分写死：
   - GdUnit 套件按 `[TestSuite]` 自动发现（`Verify-GodotProject.ps1` 约第 360–372 行），新测试文件无需登记。
   - 技能资源加载类测试按 Catalog 的 `entry` 遍历，新条目自动覆盖。
   - 职业专属断言（如法师、死灵法师偏移）按职业写死，新职业不会自动获得。

## 5. 目标

让"新增职业或功能"尽量被自动检测和覆盖，同时保留 fail-closed 保护。不追求全自动。

| 层级 | 是否可自动 | 做法 |
|---|---|---|
| 资源契约检查（偏移、贴图、Catalog 一致性） | 可以 | 改为遍历 Catalog 全部单位 |
| "有没有测试"缺口检测 | 可以 | 把 `contract-coverage` 接入验证 |
| 通用冒烟（进战斗、放技能、死亡） | 基本可以 | 按职业参数化，由 Catalog 驱动 |
| 职业专属规则 | 不可以 | 仍需合同加 Spec（可用 LLM 起草，必须人工确认） |
| 视觉、手感 | 不可以 | 人工验收 |

## 6. 建议任务（按顺序）

先做 1 和 2，成本低、收益大。

1. 接入 `contract-coverage` 门禁。
   - 先读 `Tools/gameplay-test-spec/src/contracts.ts` 与 `cli.ts` 中该命令的输入、输出和退出码。
   - 在 `Verify-GodotProject.ps1` 的 gameplay spec 编译步骤附近调用：
     `node Tools/gameplay-test-spec/dist/src/cli.js contract-coverage --docs .agents/docs --specs Tests/gameplay-specs`
   - 若当前基线有大量未覆盖合同，先生成基线（白名单文件），只对"新增未覆盖"失败，避免一次性变红。白名单必须提交并可审计。
   - 在 `Tools/gameplay-test-spec` 的测试中补用例：缺 Spec 时门禁失败，白名单项不失败。
2. Catalog 驱动的资源契约测试。
   - 找出现有按职业写死的断言（例如 `godot/tests/UnitBatchGodotTests.cs` 中 mage、necromancer 的偏移断言），保留原断言不变。
   - 新增一个遍历 Catalog 全部 `UnitDefinitionResource` 的测试，检查：必需贴图非空、`ValidateVisualContract` 通过、各姿态偏移与贴图尺寸一致、影子字段合法。
   - 新增职业进入 Catalog 后即被覆盖，无需改测试。
3. 参数化通用冒烟。
   - 在 `godot/tests/GameplaySpec` 中新增一个由 Catalog 驱动的"进战斗、放技能、死亡"旅程。
   - 旅程数量变化时，让预期清单由 Spec 与 checkpoint 生成，同时保留"数量变化必须显式确认"的保护。不要直接删掉 `total -eq` 检查。
4. 文档与流程。
   - 更新 `.agents/docs/gameplay-test-framework.md` 与 `.agents/skills/gameplay-test-framework/SKILL.md`，写明新增职业的最小步骤：合同、Spec、编译并提交计划、Catalog 条目。
   - 跑 OKF impact 与 sync。

## 7. 验收标准

- 新增一个没有任何合同和 Spec 的假职业时，验证会失败并指出缺口。
- 新增一个满足合同的职业时，资源契约测试无需改代码就被执行。
- 现有 20 个旅程全部保持通过，隔离与存档不变性检查不被削弱。
- 已知与本任务无关的基线失败：`.agents/skills/pure-run-artwork-pipeline` 的 MaLiang artwork 单测因缺 RC 源 manifest 而失败（3 个 import 错误）。不要修它，也不要把它算作本任务回归。
- 提交信息用中文，范围精确，`git diff --cached --check` 通过。

## 8. 给执行者的提醒

- 先只读核对 `Tools/gameplay-test-spec/src` 和 `godot/tests/GameplaySpec`，不要凭本文猜命令参数。
- 每步先跑最小测试，并确认测试数非零；`total=0` 是无效运行。
- 同一 Editor、runner、verifier 同时只能有一个修改者，串行执行。
- 若需要重新生成资源或做会触发程序集重载的改动，先让用户处理 Editor 的正常关闭与恢复，不要强杀进程。
- 遇到需要用户决策的点（例如基线白名单策略），停下并提问，不要自行扩大范围。
