---
title: 生图状态机复杂性审计
status: draft
verified_revision: c9ff3a710fd9
---

# 生图状态机复杂性审计

本文回答哪些复杂度必须保留、哪些只属于 Pure Run、哪些来自当前实现和历史兼容。它不设计 vNext schema。目标原则见[游戏美术生产第一性原则](game-art-production-first-principles.md)，事实全景见[当前策略地图](generative-art-current-strategy-map.md)。

## 1. 四类复杂度

### 1.1 本质复杂度

任何可靠游戏美术生产系统都需要处理：

- 非确定性生产与确定性验证的边界；
- 来源、版本、rights 与人工决定；
- 技术可用、视觉成立、状态集一致、运行时体验和发布许可的不同结论；
- 角色身份连续性与动作变化的张力；
- 表现通道分工和时间语义；
- 失败候选、负面证据和策略切换；
- 可重放处理、不可变 receipt 与可查询投影。

这些复杂度不能通过删除状态或改成单一 `approved` 消失。

### 1.2 Pure Run 项目复杂度

- 可爱暗黑、英雄卡通和战棋可读性；
- 胶囊/球核、无臂腿四爪等项目拓扑；
- DR/UL 原生视图和镜像取舍；
- `256×256`、`y=236`、128 预览和当前 `64×32` 离线 panel；
- 当前 Godot `96×48` 棋盘、`.34` Actor 缩放和项目动作表现；
- 项目角色、装备、材质、Acceptance Case、rights 与作者身份。

这些应留在 Tactics 项目数据和薄适配层，不进入通用独立仓默认值。`64×32` 仍由现行管线执行，但其当前 Godot 有效性待验证，不能在决策前直接归为纯历史。

### 1.3 偶然实现复杂度

- 单个约 6,450 行脚本同时承担领域状态、Pillow、Review、Reviewer、事务、许可、发布和 Runtime Copy；
- 81 个实际 CLI 子命令平铺在同一入口；
- Job/Packet、规则文字、Review role 和 supporting artifact 的重复物化；
- 大量项目角色特例和路径硬编码；
- 多套状态轴缺少清晰聚合；
- 设计意图、已执行门禁和 Skill 政策混在一起。

这类复杂度应拆分、投影或删除，不能照搬进独立仓。

### 1.4 历史兼容复杂度

- schema v1/v2/v3/v4；
- legacy、migration、reviewed import、recontract 和旧 provenance；
- 退役 death recipe、旧 prompt 与旧 Reviewer 数据；
- Unity PPU、Prefab、SpriteRenderer、`.meta`、`FourDirectionSpriteVisual`、`UnitPoseFamily`、`ReleaseTime`、`PoseRestoreTime`。

独立仓需要明确 read/migrate 边界，但不能让历史术语继续指导新生产。

## 2. 当前核心复杂性问题

### 2.1 一等对象仍偏向文件，而不是游戏信息需求

Brief 有 purpose、firstRead 和 targetContexts，但 Contract/Job 的核心仍是 `assetId + kind + direction + pose + outputs`。系统擅长追踪 PNG 如何移动，却没有一等表达：

- 玩家问题；
- actor × gameplay state 覆盖；
- Sprite/Tween/VFX/UI/音效职责；
- 显示、Release、Impact、Recovery 与中断时点；
- 真实运行时 Context Fixture；
- 全生命周期成本；
- 从源 SHA 到 Resource/Cue/Manual QA/build 的 Release Binding。

### 2.2 状态轴很多，但不能组成一条真实生命周期

- Attempt 有十三个枚举值；
- Job 当前全部是 `ready`；
- Feedback 有 selected/backup/retry/technical_failed/exhausted；
- Series pose 另有 pending/active/review_pending/approved/promoted/exhausted/provisional；
- Approval、Art Direction Verdict、Equipment Verdict 和 Public Provenance 又是独立记录；
- Runtime authorization 当前没有独立 receipt。

一个候选可能视觉 selected、技术 failed、rights 可分发、运行时未接入。单一 state 无法表达。当前没有 `state=rejected` 的 Attempt；文档中 `approved/rejected → promoted` 的线性箭头也错误，因为 rejected 应是终点。

### 2.3 持久化投影与真实结论脱节

219 个 Job 全部仍为 ready，包括其 Attempt 已 promoted 或 technical_failed 的 Job。诗人双参考最后的 `exhausted` 是 Agent Feedback，Job `series=null`，Attempt a006 仍为 prepared，也没有人类策略终止 receipt。

因此“现在能否继续生产”必须同时读取 Job、Attempt、Feedback、Series、人工文档和用户决定。

### 2.4 约束复制放大合同污染

同一要求可能重复出现在 Profile、Brief、Contract、Composition、Job role、prompt、compiled prompt、Feedback、Acceptance Case 和 Review Rule。SHA 可以防止静默修改，却不能证明重复文字忠于上游事实。

诗人 Melee 将源图中不存在的帽、衣、葫芦写进 Brief/Prompt，再由 Feedback 要求“恢复”，形成审计完整但事实错误的闭环。未来必须区分批准来源事实、Gameplay State 的授权变化、任务职责、生产能力假设和事后结果证据。

### 2.5 引用职责是自然语言，不是能力隔离

Job 可以固定输入 SHA、顺序和自由命名 role，但不能证明模型只从某图学习姿态而不迁移身份、装备或材质。多参考隔离必须被视为可证伪的 Production Strategy 假设，而不是已保证能力。

### 2.6 Pose Guide 越权承担动作设计

当前诗人 guide 主要表达胶囊框、脚底点、握点、刀轴和端点区域；Composition 又要求保留紧凑体量和 Idle 足点。它没有充分表达力线、重心、压缩/伸展、支撑脚、负形和反向惯性。

武器窗口属于 Technical Art；它不能替代 Animation/Action Design。动作设计不足时，增加检测框只会更精确地验证一个静态摆拍。

### 2.7 技术 QA 与游戏美术验收顺序不清

技术报告可以可靠发现 annotations、core continuity、色幕、尺寸和基线问题，这是必要能力。但存在两种风险：

- 对明显技术失败仍生产昂贵完整 Review；
- 技术通过后误以为已接近游戏美术完成。

技术 QA 只回答“文件是否可被可靠审查和使用”，不回答“玩家是否读懂”“角色状态集是否一致”或“运行时是否成立”。

### 2.8 离线几何与当前 Game View 脱节

管线仍以 256、`y=236`、128 和 `64×32` panel 组织新证据；当前 Godot 却使用 `96×48` 投影和 `.34` Actor 缩放。`UnitDefinitionResource.ValidateVisualContract` 不验证纹理尺寸、AABB 或离线 baseline。

Scene Brief 当前只有模板，没有正式实例。因此没有机器链证明离线通过的 Sprite 在实际摄像机、棋盘、缩放和同屏关系下保持同一构图。问题不是只替换一个常量，而是缺少共享 Runtime Context Fixture。

### 2.9 资产语义与时间语义脱节

诗人 Melee Brief 要求“下劈峰值”；当前 Player 在 windup 前切入 Melee Texture，使其贯穿 windup、lunge 和 impact hold。即使生成完美峰值图，也会在当前 choreography 中过早展示。

另一个例子是历史动作设计要求免费拾矛不播放姿态；当前 `PickupSpear` 会产生 `SkillUsedEvent`，而未显式列出的 execution kind 在 `BattlePresentationFrame.ResolveAction` 默认映射为 Cast。当前代码是行为权威，但没有一份经验证的现行 gameplay-action→visual-cue 合同。

### 2.10 Reviewer 已成长为第二套平台

Reviewer 包含 Policy、Rule、Case、Compiled Policy、Packet、Invocation、Result、Application、Audit、Qualification、Supersession、Suspension 和 Experience。它解决了模型不能自我批准的真实问题，但也带来 Reviewer 运维与普通生产共享入口、缺陷分类重复、结果反向推动 Attempt，以及模型能力生命周期与资产生产生命周期耦合。

Reviewer 权限边界应保留，但不应成为游戏美术领域模型中心。`artwork_review.py` 已是较纯的确定性核心，可作为未来分离证据。

### 2.11 发布、许可、Runtime 与 QA 没有端到端绑定

应分别记录：美术批准、正式晋升、分发 rights、Runtime 授权、Resource/Cue 绑定、自动测试、Manual QA、build/package 包含版本。

当前 `register-runtime-copy` 只验证已存在字节并更新 target rights/provenance；不复制、不记录授权、不绑定 Resource。也没有一条聚合记录连接源 SHA、Approval、Runtime Copy、Resource、Cue、测试、Manual QA 和 build。

## 3. 设计文档与实际门禁差异

当前高价值差异包括：

- active Manifest 已存在，但未传 Family/Brief 仍可走 v2/v3，v4 前向绑定可绕开；
- 设计声明 Project→Material→Family→Theme→Brief→Feedback 优先级；本次窄修复后 v4 `compile_prompt` 已从哈希绑定 Brief 编译身份、不准项和逐输入职责并拒绝绑定漂移，但 Project/Material/Family/Theme 规则仍未进入 prompt，旧 v2/v3 兼容分支仍保留历史角色硬编码；
- ThemeProfile、`render-anchor-board`、ArtDirectionException 尚未实现；现有 `approve-exception` 只是 core-size 技术例外；
- `acceptedWarnings` 未与当前 warning 集合对账；Approval 未强制复用 ArtDirectionReview 冻结的同一 Acceptance result 集；
- v4 equipment 因 schema 分支顺序未必强制 equipment style verdict；
- rejected/superseded 来源隔离主要依赖 Skill；`create_job` 只对 role 名含 `anchor|mother` 的输入做有限锚点检查；
- Promotion 跨正式图、preview、public provenance、Skill casebook、Attempt 和 Series，但不在同一 transaction，resolver 只覆盖 prepare；
- 141 组 Job/Packet 字节完全相同；多个 Review role 可绑定同一 SHA，role 完整不等于独立证据完整；
- `job-ed5f76ac4fb68184-a004` 引用不存在的 `feedback-17ec2819444713c6`，当前 strict 未发现该悬空关系；
- strict 当前仍有 16 条已 promoted 装备精确色幕残留。

这些不是本轮立即修复项。独立仓抽取必须区分“已执行协议”“设计意图”“Skill 政策”和“兼容历史”。

## 4. 诗人 Melee 暴露的完整问题链

1. **上游合同污染：** 正式身份无衣帽酒具；Melee 合同却创造这些身份事实。
2. **动作设计不足：** Pose Guide 将武器端点几何当作关键姿态，缺少身体动力学和负形。
3. **运行时时点不匹配：** 离线 peak Sprite 在当前 Runtime 覆盖整个动作窗口。
4. **策略结论权限不清：** Agent Feedback 可记录问题，但不能决定继续投资；`exhausted` 必须区分预算事实、Agent 建议和人工决定。
5. **技术失败被扩展为策略失败：** 三参考只有一个 technical_failed Attempt、无 Feedback，不足以证伪视觉策略。

## 5. 下一代领域设计前必须回答

### 游戏美术目标

1. 一等对象如何表达 Visual Moment 与 Character Visual State Set？
2. 每个状态帮助玩家回答什么，允许多少识别时间？
3. Sprite、Tween、VFX、UI、音效分别承担什么？
4. 哪些身份项必须稳定，哪些变化是动作所需？

### 生产策略

5. 如何表达策略假设、适用范围、成本、预算和停止信号？
6. 何时 retry、技术 remediation、换策略或回到动作设计？
7. AI、组件、人工清理如何作为可替换能力？

### 验收与发布

8. 技术、单 moment、状态集、Game View、Runtime QA 分别由什么证据证明？
9. 哪些事实可自动拒绝，哪些必须人工判断？
10. fallback/placeholder 如何成为显式产品状态？
11. 如何连接 Approval、rights、Runtime binding、测试、Manual QA 和 build 而不混为一态？

### 仓库边界

12. 通用仓拥有哪些协议和引擎，Pure Run adapter 拥有哪些视觉规则与数据？
13. 哪些历史 schema 只读，哪些可迁移，哪些应停止传播？
14. Alfred 如何只发现/路由，而不复制实现？

## 6. 本轮边界

本审计不新增 schema、状态枚举、AI 组合、独立仓或 Alfred 集成；不生成、重试、审核或晋升图片。下一步只能在已确认第一性原则之上设计领域对象和状态关系。