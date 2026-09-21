---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec
title: Gameplay Test Framework
description: 将受控 gameplay spec 编译为 Godot runtime runner 可执行的确定性计划。
tags: [testing, gameplay, automation, godot]
timestamp: "2026-09-12T03:22:50+08:00"
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
source_fingerprint: sha256:9a0d4e4943fbcb07a5ef83075469800e9a61801ad5789dc9319b6df1f9e012d7
---

# Current State

维护对象是 `.gameplay-test.md`/`ScenarioSpec`；TypeScript validator/compiler 生成声明 Godot runtime、capability、adapter、checkpoint、隔离存档和 watchdog 的 plan。目标 runtime 不支持的步骤、错误 adapter 与被篡改的 capability/checkpoint 均 fail-closed，生成 plan 不手改。Runtime Runner的状态哈希包含Pause可见性与Root/Options视图，Esc等待开/关双向转换；presentation/movement活跃只提供有上限的宽限，不能永久掩盖no-progress，Main重建复用场景FixedSeed。

设计文档中的明确 `gameplay-contract` block 可注册稳定 Contract ID，ScenarioDraft/ScenarioSpec/plan 保留 `contractIds`，并由批量覆盖报告检查缺失 spec 或 DSL 不支持。LLM provider 层默认从仓库外用户配置调用 OpenCode Go `deepseek-v4-flash`，每次进程执行模型发现且失败不回退；Ollama 仅为显式本地选项。模型输出不具权威性，仍由 schema、逐字证据与 capability/compiler 确定性拒绝漂移。

作者编译器支持 `EnemySliceDraft`，可把一个受约束敌人纵切确定性投影为 Unit、Skill、AI、Layout 与 Encounter
Authoring V2 batch，并以 Catalog revision fence 区分 create/update。模型只能填充严格 Draft；素材路径必须命中
显式批准清单，Resource 写入仍交给受测 Godot 作者服务与 ResourceSaver。本轮大嘴蝠的三份在线输出仅作为
provider/schema/compiler smoke 候选，因使用占位 checkpoint 未晋升为正式 gameplay spec。

OpenCode Go Key 与普通 provider 配置分离，secrets ACL 只允许当前用户、SYSTEM 或 Administrators。doctor 只发送固定 JSON 探针；项目文档仅由显式 extract/generate 命令发送，确定性 validate/compile 命令不联网。审计输出不包含 Key、Authorization、prompt 或原始响应。

`GodotGameplayRuntimeRunner` 加载正式 `Main.tscn`，并通过 `Viewport.PushInput` 驱动生产 GUI/Input 链；Adventure pointer target 必须返回实际接收 Hover/GuiInput 的 Control，而不是只承载语义的 sibling View。Start Camp action 失败会附带最后一次结构化输入诊断，以区分未到输入面、坐标换算、命中、模式阻断、持久化拒绝和 handler 异常。Setup 断言读取 Main 暴露的瞬时 draft probe，而不是把 SaveStore 中的 `PendingRunSetup` 当作可恢复状态；最终技能后的 Active Run 仍从隔离 Save 断言。每个场景使用隔离 `user://qa-runner/<scenario>/<attempt>/`，执行前后验证生产主档与 backup 未变化，并在退出时释放 Main、临时节点和隔离目录。

框架覆盖 Battle、Map、UI、Skill、Adventure 和作者 spec。Adventure 合同使用 `exitCommitted`、`immediateSuccessorNodeIdsEqual` 和权威 Adventure revision/state hash 验证节点内即时出口，不再表达全局 RouteNode 预提交。自动化证明规则、事务、生产输入、重载和清理；视觉、动画、可读性、Editor Assembly Reload 与操作手感仍由人工验收。

# Relationships

- Battle 场景验证 [Battle System](battle.md)与[Monster AI](monster-ai.md)。
- Skill 场景验证 [SkillGraph](skill-graph.md)。
- Map/Adventure 场景验证 [Roguelike Run](roguelike-run.md)。

# Verification Guidance

修改 schema、compiler、adapter 或 runner 后，运行 TypeScript 测试、源 spec validate/compile、Godot runtime batch 和清理断言。玩家输入场景的状态变化必须来自生产输入链，不能以直接调用业务服务代替。
