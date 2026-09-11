---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec
title: Gameplay Test Framework
description: 将受控 gameplay spec 编译为 Godot runtime runner 可执行的确定性计划。
tags: [testing, gameplay, automation, godot]
timestamp: "2026-08-28T13:05:39+08:00"
status: active
catalog_scope: gameplay-test-framework
repo_paths:
  - .agents/docs/gameplay-test-framework.md
  - .agents/skills/gameplay-test-framework/SKILL.md
  - Tools/gameplay-test-spec
  - godot/tests/GameplaySpec
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunTestContext.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/GodotPlayableRunMain.cs
  - Tests/gameplay-specs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:718bbb7740bb514482f834fcc906dea03032a16b1d8e4e5ca10b12ed3b734742
---

# Current State

维护对象是 `.gameplay-test.md`/`ScenarioSpec`；TypeScript validator/compiler 生成声明 Godot runtime、capability、adapter、checkpoint、隔离存档和 watchdog 的 plan。目标 runtime 不支持的步骤、错误 adapter 与被篡改的 capability/checkpoint 均 fail-closed，生成 plan 不手改。

设计文档中的明确 `gameplay-contract` block 可注册稳定 Contract ID，ScenarioDraft/ScenarioSpec/plan 保留 `contractIds`，并由批量覆盖报告检查缺失 spec 或 DSL 不支持。LLM provider 层默认从仓库外用户配置调用 OpenCode Go `deepseek-v4-flash`，每次进程执行模型发现且失败不回退；Ollama 仅为显式本地选项。模型输出不具权威性，仍由 schema、逐字证据与 capability/compiler 确定性拒绝漂移。

作者编译器支持 `EnemySliceDraft`，可把一个受约束敌人纵切确定性投影为 Unit、Skill、AI、Layout 与 Encounter
Authoring V2 batch，并以 Catalog revision fence 区分 create/update。模型只能填充严格 Draft；素材路径必须命中
显式批准清单，Resource 写入仍交给受测 Godot 作者服务与 ResourceSaver。本轮大嘴蝠的三份在线输出仅作为
provider/schema/compiler smoke 候选，因使用占位 checkpoint 未晋升为正式 gameplay spec。

OpenCode Go Key 与普通 provider 配置分离，secrets ACL 只允许当前用户、SYSTEM 或 Administrators。doctor 只发送固定 JSON 探针；项目文档仅由显式 extract/generate 命令发送，确定性 validate/compile 命令不联网。审计输出不包含 Key、Authorization、prompt 或原始响应。

`GodotGameplayRuntimeRunner` 加载正式 `Main.tscn`，并通过 `Viewport.PushInput` 驱动生产 GUI/Input 链。每个场景使用隔离 `user://qa-runner/<scenario>/<attempt>/`，执行前后验证生产主档与 backup 未变化，并在退出时释放 Main、临时节点和隔离目录。

依赖命中/闪避结果的 presentation 场景绑定显式 checkpoint RNG state；属性、行动顺序或 committed battle snapshot 变化后必须重新确认目标分支，并同步 checkpoint semantic hash 与由源 spec 编译的 plan。生产输入 readiness 同时观察表现播放锁和 initiative Tween 输入锁，避免自动旅程在 Core 已提交但 HUD 动画尚未解锁时注入下一次点击。

测试按非零最小测试、相关 fixture、相关门禁和统一 verifier 逐级升级；输入、场景、缓存 UI、异步状态或 reload fixture 首次通过后连续复跑一轮。同一 canonical Editor、runner 和 verifier 只有一个 mutating job owner。长任务以实际进度判断 active/stalled，90 秒无可验证进展时检查进程、runner 和日志，确认卡死后只恢复当前门禁。当前任务失败必须修复；基线失败需要可核验证据和用户或仓库政策授权。

框架覆盖 Battle、Map、UI、Skill、Adventure 和作者 spec。Adventure 合同使用 `exitCommitted`、`immediateSuccessorNodeIdsEqual` 和权威 Adventure revision/state hash 验证节点内即时出口，不再表达全局 RouteNode 预提交。自动化证明规则、事务、生产输入、重载和清理；视觉、动画、可读性、Editor Assembly Reload 与操作手感仍由人工验收。

# Relationships

- Battle 场景验证 [Battle System](battle.md)与[Monster AI](monster-ai.md)。
- Skill 场景验证 [SkillGraph](skill-graph.md)。
- Map/Adventure 场景验证 [Roguelike Run](roguelike-run.md)。

# Verification Guidance

修改 schema、compiler、adapter 或 runner 后，按非零最小测试、相关 fixture、相关门禁和统一 verifier 的顺序运行 TypeScript 测试、源 spec validate/compile、Godot runtime batch 和清理断言。玩家输入场景的状态变化必须来自生产输入链，不能以直接调用业务服务代替。
