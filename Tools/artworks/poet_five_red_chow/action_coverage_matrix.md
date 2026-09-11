# 五红土松诗人动作覆盖矩阵

状态：Idle DR/UL 已批准；Melee DR 已进行两套尝试但均未通过，其他动作仍未批准。独立出鞘装备已完成下述批准与离线晋升，不代表角色动作获批。

## 范围与盘点

初次盘点只建立离线美术覆盖清单。2026-09-10 两点清理通过 recontract 新建技术 Job 并保留原 Invocation/Delivery，经受控 CLI 批准并离线晋升独立 v02。随后 Melee DR 的直接双参考策略完成三轮视觉尝试后以 `job-a6df55a1b4f8ac96-a006` exhausted 收口；改用魔剑士动作参考的三参考策略在 `job-50532e1e9fe7c307-a001` technical_failed 收口。两套策略均无 promoted 角色动作，不接入运行时。

### 独立出鞘装备：已批准，不计入角色动作覆盖

最新技术收尾：用户明确授权仅清除两点、检测通过后自动机械验收/晋升，并允许绑定新 SHA 技术批准；不声称新视觉会话。`reviewed-recontract-e6a91dc49320f564` 将源 `job-c91127f8d0e61731-a002` 绑定到 `contract-a6b9130f239467e7`，新 Attempt `job-a254d0afc9b7b286-a001` 已 promoted，Approval `approval-8dbcb2755d3c06b2`，style verdict `equipment-style-verdict-c6f6105d9c6260b2`，AD verdict `art-direction-verdict-debb48d7057ac13e`。三项新 acceptance 通过，固定装备/AD review 为 v03。

当前独立正式输出为 [unsheathed_v02](equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02.png) 及 [128](equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02_128.png)。母版 SHA `375409b3b1fa37a4e74b81500d5abea63c57d311fed5eabcc85759d49aa193b1`；预览 SHA `d1db586755bb515036ef7626f315dab88aea775ff651668afa5af64cda3e1f49`。实测仅 `(117,166)` 的 `[0,255,0,3]`、`(169,236)` 的 `[0,255,0,2]` 清零；256×256 未 resize，visible 90×96、baseline 236，report passed，palette complexity advisory 保留。旧 v01 与下列原批准历史完整保留。strict 历史 16 项精确色幕污染不豁免；4 张 processing QA 已逐文件按本任务授权登记为 project-owned supporting-derived，非新视觉批准、不改变许可证。

以下为清理前历史：

- cty41 先接受 v02 比例，再明确接受展示的 raw_v02 与 128 预览整体外形及画风；本次仅机械落成该既有接受，没有声称用户新看过 QA 对照板，也不批准未来图片。
- Attempt `job-c91127f8d0e61731-a002`，原合同 `contract-bfb85273c3228d32`；Approval `approval-40e551d85e6899c1`，装备风格 Verdict `equipment-style-verdict-1f126a937643f493`，Art Direction Verdict `art-direction-verdict-67590b39415c014d`。三个 Brief Acceptance Case 已绑定真实现有证据；调色板复杂度仍为 advisory。
- 正式离线装备：[出鞘唐刀母版](equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v01.png) 与 [128 预览](equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v01_128.png)。正式文件沿用原合同 `_v01` 输出名，内容是已接受的候选 v02；母版 SHA-256 `704cd888947a648a9e1ee0db154804288ba230eca422fcc4c63229c145b9e82b`，与候选逐字节一致。
- 实际 v02 使用既有两张 approved 来源：入鞘 DR v02 负责环/柄/护手/画风，旧唐剑 v01 仅负责金属配色和细节密度。`feedback-addendum-6589876558d05ba6` 澄清旧 feedback 中 one input / <=220 words / new contract 属于生成前已取消的主代理准备要求；旧记录和 v01 prompt 改写说明均保留。
- 附加逐像素 QA 发现母版保留两个极低 alpha 的精确绿像素（alpha 3/2），128 预览没有；状态机 strict 与既有装备 report 未检出。`feedback-addendum-6086f09a42ad87ca` 如实记录此技术门禁缺口；候选及晋升字节不改，不声称所有 QA 全绿。
- Melee DR 已生成失败候选但未获批准或晋升；Melee UL、Hit、Cast、Thrown、Death 仍未生成正式候选。装备批准及失败尝试均不等于这些动作的构图、来源、装备状态或运行时接入授权。

按 pipeline promoted Attempt 盘点：当前装备版完整角色只有 Idle DR v03、Idle UL v07；另有无装备身份底稿 Idle DR v03（`job-43b34a4685f71ca2-a010` / `approval-1acd77a15a84f096`）。该底稿不是新的动作覆盖，也不应取代更直接的装备版来源。未发现下表非 Idle 动作的诗人完整 promoted Sprite。独立装备、组件、研究图与历史候选不计入动作覆盖。

覆盖状态含义：**已批准**=有 promoted Attempt 与 cty41 Approval；**尝试失败**=存在可追溯候选但没有 promoted 输出；**待制作**=建议补齐但尚未生成正式候选；**待确认**=需求/表现/变体尚未批准；**历史模板**=不得直接用于新生成。

## 来源及职责

所有路径以仓库根为基准。批准成图不自动具有任意风格锚点权限；生成前还须核验适用的窄职责 AnchorVerdict、合同和来源确认。

| 来源 | 路径 | Attempt / Approval | 允许职责 | 拟进入 ImageGen |
| --- | --- | --- | --- | --- |
| S-DR 装备版 Idle DR | `Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png` | `job-34f6747e5a1f30d0-a001` / `approval-3affe3151e3652b6` | 五红身份、DR 解剖、唯一核心体量基准、配色、入鞘唐刀形制与现有装备清单；不提供攻击时刻 | DR 动作候选输入，待来源确认；本轮不输入 |
| S-UL 装备版 Idle UL | `Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_ul_v07.png` | `job-4affd0d3e702d0d0-a001` / `approval-a71f2eecb8e0ec5e` | 原生 UL 身份/后脑/耳型/方向；体量仍向 S-DR 校验；Idle 遮挡不自动延伸为动作合同 | UL 动作候选输入，待来源确认；本轮不输入 |
| A-DR 对应已批准 DR 动作 | 尚未制作，无路径、SHA 或 Approval | 不存在 | 将来只提供同一动作峰值、装备状态和动作轴，不向 UL 迁移正脸 | 前置 DR 人工通过后才可选择 |
| X-ACTION 跨角色动作参考 | 尚未选定具体资产 | 待核验 | 只提供姿势/夸张程度；不能迁移犬种、核心体量或装备 | 当前不输入；需展示候选再确认 |
| QA 语义蒙版、Tile/深度板 | 按未来具体 Attempt 绑定 | 待制作 | 仅测量接触、体量、遮挡与落点，不当作成品外观 | Review-only，不默认输入 |

已复核源图 SHA-256：

- S-DR：`e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3`
- S-UL：`519e288ff7f4ea2cb35de9393b4163a13c1fbfdca3b8fb5f83eff81733eec512`
- S-DR `_128.png`：`0535842582f41fb8afb578a570a03ea5cde819e2f2318c96522014be8222adae`
- S-UL `_128.png`：`b06382fd59b3d4e16b2c26fde0ee138d4b883d963b3a7820db3d652fa233eaeb`

## 动作与方向矩阵

P1/P2/P3 是建议制作先后，不是缺陷严重度或已经批准的生产队列。动作语义均为提案，不能从动作名称推导新装备授权。

| 动作 / 方向 | 覆盖状态 | 冻结时刻 / 语义提案 | 装备状态 | 身份 / 动作来源 | 核心风险与验收点 | 依赖 / 优先级 / 待决事项 |
| --- | --- | --- | --- | --- | --- | --- |
| Idle DR | 已批准 | 静止低位放松握持 | 一把完全入鞘环首唐刀 | S-DR | 保持原图；不重做 | 已完成 |
| Idle UL | 已批准 | 原生背向静止 | 同一入鞘唐刀，柄环可见、鞘体大部遮挡 | S-UL；S-DR 体量 | 尾巴前层，画面右侧手爪/底脚在身体后；仅是 Idle 已接受关系 | 已完成 |
| Melee DR | 尝试失败；无 promoted 输出 | 已锁定向前斜下劈峰值；不含手臂、拖尾或额外 FX | 出鞘唐刀；空鞘留在侧后方并部分遮挡 | S-DR；独立出鞘刀；后续策略增加魔剑士 Melee 姿势职责 | 直接策略反复丢失诗人身份/四爪并把刀画成宽剑；三参考策略仍丢帽衣葫芦且校验失败 | `job-a6df55a1b4f8ac96` exhausted；`job-50532e1e9fe7c307` technical_failed；美术暂停 |
| Melee UL | 待制作；依赖获批 DR | 与未来获批 Melee DR 同一时刻的原生背向投影 | 与 DR 一致，不自动改变装备侧 | S-UL + 将来的 A-DR；S-DR 测量 | 不镜像 DR；近/远手及刀鞘深度逐项声明，不机械复制 Idle 遮挡 | 当前无获批 DR，不进入队列 |
| Hit DR | 待制作；需求待确认 | 建议中度漫画后仰峰值、耳朵受惯性压低 | 唐刀是否保留与偏转方向待确认，不新增道具 | S-DR；X-ACTION 待选 | 主体整体后仰不长出腿；面部反应参考项目 Hit 规范，不附加命中特效 | P2；建议在 Melee 对完成后讨论 |
| Hit UL | 待制作；依赖 DR | 同一受击峰值的背向版本 | 与获批 Hit DR 一致 | S-UL + 将来的 Hit DR | 仅绘制真实可见眼睛/泪线，不把背面变成完整正脸 | P2；DR 人工通过 |
| Cast DR | 待确认 | 尚未确定吟诵、引导或释放峰值；不直接套用法杖轴 | 不默认古琴、酒具、裸刃或施法 FX | S-DR；具体动作参考待选 | 角色动作与技能效果分开；脸、握爪与唯一装备不冲突 | P3；先明确 Cast 代表哪些动作需求 |
| Cast UL | 待确认 | 与将来获批 Cast DR 对应 | 与 DR 一致 | S-UL + 将来的 Cast DR | 原生 UL；明确主体轴、手爪接触与装备出口 | P3；需求及 DR 批准后再进入队列 |
| Thrown DR | 待确认，非默认必做 | 是否存在投掷需求尚未确认，投掷对象/出手峰值未知 | 不默认投掷唐刀，不默认酒具或新增投射物 | S-DR；动作参考待选 | 出手前后装备数量、脱手对象与角色层分离；投射物需独立合同 | 暂不排产；先确认是否需要此槽位 |
| Thrown UL | 待确认，非默认必做 | 与 DR 同一投掷阶段的背向投影 | 取决于经批准的 DR 装备状态 | S-UL + 将来的 Thrown DR | 不通过镜像猜测投掷端点和前后层 | 依赖 Thrown 需求及 DR 批准 |
| Death 独立静态图 | 待制作；数量与道具待确认 | 建议按胶囊地面规范仰面平躺、头朝画面右上 | 默认无职业特效；若保留刀，须批准且完整入鞘脱手，最多一件识别道具 | S-DR 身份/体量；死亡姿态参考待核验 | 完整身体/耳/附着四爪连续生成；禁拆分压扁，尸体 AABB 中心落 Tile | P2；先讨论一张通用死亡图是否足够，不预设 DR/UL 两张 |

## 方向与验收共识

- 本矩阵规划 DR/UL 两个原生视图；UL 不由 DR 翻转生成。DL/UR 不计作额外已制作图，也不在本轮修改或确认运行时镜像/换手映射。
- 每次只制作一个动作、一个方向、一张单帧候选。DR 人工通过后才可作为 UL 动作参考；Reviewer 只能 retry/转人工，不能批准。
- 五红土松身份、紧凑胶囊主体、两前爪两后爪直接附着、无臂腿是共同不变量。允许真实遮挡，不要求四爪全部外露；不能用遮挡掩盖断裂或丢爪。
- 不新增古琴、酒囊/葫芦、裸刃、第二武器、断裂装备片或未经批准的 FX。Idle 对装备的批准不是攻击/投掷/死亡状态的新授权。
- 未来逐图 Brief/Composition 明确世界朝向、固定等距摄像机、主体顶部相对脚底的 x、握点、武器端点、眼区禁入和前后层。
- 256 RGBA 母版、128 预览、核心体量/三截面、爪接触、Alpha/色幕、画布边界及 Tile Review 均需验证。站立动作使用脚底锚点；Death 使用尸体 AABB 中心，不套站立基线。
- 刀尖真实不可见时必须由该动作 Composition 明确允许，不能伪造 annotation；Idle UL 的遮挡批准不可直接代替动作批准。

## Melee DR 尝试归档

- 已确认语义为向前斜下劈峰值；空鞘留在侧后方并部分遮挡；禁止手臂、拖尾、斩击 FX 与额外效果。
- 直接双参考策略 `job-a6df55a1b4f8ac96` 的三轮视觉生成均未保留诗人身份与无臂四爪拓扑，并反复生成宽而对称的剑形；最终 Attempt a006 为 `exhausted`。
- 引入魔剑士 Melee 动作姿势职责后的三参考策略 `job-50532e1e9fe7c307` 仍移除帽、衣与葫芦，刀形仍偏剑；Attempt a001 同时因 `annotations_missing`、`core_row_disconnected` 为 `technical_failed`。
- 两套策略的候选、Review 与反馈仅作失败历史，不得晋升、接入运行时或作为正向生成来源。当前美术工作暂停；恢复前须由用户另行授权新策略。

## 历史入口与依据

- [旧 Attack 模板](attack_prompts.md)：六帧、foreleg/hindleg、Tang sword/guqin/wine vessel 等措辞已经不适用于当前单帧无臂腿诗人；保留历史正文，不直接编译生成。
- [单帧动作规范](../../../.agents/skills/pure-run-artwork-pipeline/references/single-frame-action-poses.md)
- [死亡图规范](../../../.agents/skills/pure-run-artwork-pipeline/references/death-state-sprites.md)
- [方向与逐图迭代](../../../.agents/skills/pure-run-artwork-pipeline/references/imagegen-iteration.md)
- [Reviewer 设计](../../../.agents/docs/2026-09-04-pure-run-model-reviewer-design.md)
