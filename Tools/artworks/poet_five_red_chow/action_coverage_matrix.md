# 五红土松诗人动作覆盖矩阵

状态：Idle DR/UL 已批准并以逐字节一致副本接入 Godot；Melee、Cast、Hit 的 DR/UL 与一张通用 Death 已由 cty41 批准并离线晋升，但均未接入运行时。2026-09-12 的 v3 实验首次绑定 Release→Impact 时间合同与真正关键姿态 Guide：a001 动作张力明显改善但丢失胶囊拓扑；用户授权的 a002 恢复直立胶囊和空鞘，却退回近似 Idle 展示姿态，且唐刀仍漂成宽双刃剑。两张均 technical_failed。随后 body-first v4 只生成无装备身体：直立胶囊和无长肢方向更接近，但前爪偏大、发力仍弱，平滑渐变和额头高光仍未受控；初始 a001 technical_failed；用户随后明确接受该身体用于较宽松的近战可读性标准，技术子 Attempt a008 在保持已展示 256 图 SHA `e6feabd6…0404` 下通过。两张 review-only 唐刀合成草图继续隐藏剑鞘：v1 低劈被判定有脱手感，v2 抬高 18° 后仍因两只前爪离主体过远而不自然。随后 v6/v7 局部编辑均因握持、主体重画及唐刀漂移失败。最终回到 accepted body，以正式唐刀作确定性高举剑合成；cty41 先确认 85% 唐刀、剑尖位于头部右上方的 v5 high-guard；再对照既有地面 Melee 判定无需夸张主体前倾，最终确认总校正 10°、脚底 y=236 的 DR v11 upright review-only 构图方向可以。对应 UL 保留原生身份，将 DR 双前爪按水平对应关系投影到同一左侧握点，并按用户选择向攻击方向前倾 5°；脚底 y=236 的 UL v11 同样获判定方向可以。UL 最终选择 v15，并于 2026-09-13 通过 reviewed-import `job-7bafde3439fd4e9f-a001` 获 cty41 批准、离线晋升为 `calibrated/poet_five_red_chow_melee_ul_high_guard_v01.png`；随后 DR v11 通过 reviewed-import `job-72d7ec1c23adfbdc-a001` 晋升为 `calibrated/poet_five_red_chow_melee_dr_high_guard_v01.png`。Melee DR/UL 离线正式覆盖已闭合，尚未接入运行时。缺失动作运行时仅回退到方向 Idle，Death 暂以 Idle DR 占位；`MQA-GODOT-POET-RUNTIME` 仍为 pending。

## 范围与盘点

初次盘点只建立离线美术覆盖清单。2026-09-10 两点清理通过 recontract 新建技术 Job 并保留原 Invocation/Delivery，经受控 CLI 批准并离线晋升独立 v02。随后 Melee DR 的直接双参考任务完成三轮视觉尝试；`job-a6df55a1b4f8ac96-a006` 的最后一条 `pipeline-agent` Feedback disposition 为 `exhausted`，但该 Job `series=null` 且没有人类策略终止 receipt。增加魔剑士动作参考的三参考任务只有 `job-50532e1e9fe7c307-a001`，该 Attempt 为 `technical_failed`，无 Feedback；随后因美术暂停未继续，不能据此判定视觉策略 exhausted。两套历史任务均无 promoted 角色动作。2026-09-12 经用户授权的新 v3 实验改为“正式 Idle DR 身份 + 正式出鞘唐刀 + 确定性关键姿态 Guide”，明确禁止 Demonbound 与旧合同污染，先生成 a001；其 Agent Feedback `feedback-c3e9ac296b6f748d` 为 `technical_failed`。用户查看后明确授权按反馈 retry，产生 a002；其 Feedback `feedback-77cbac471a36f39e` 同为 `technical_failed`。两张均有正式 Invocation/Delivery、技术报告与 Review，未接入运行时。之后按用户确认切换 body-first：`contract-36d6d10fc7636f02` / `job-0a890f2b18e97967` 只绑定正式 Idle DR 与 `pose-guide-7dbf206b592058b0`，禁止全部装备并直接绘制四个脚爪/胶囊重叠区。初始 a001 因平滑体积光、额头高光、偏大前爪、弱动作以及 core/mask 门禁为 `technical_failed`；Feedback `feedback-679bcd5803c7f454`，cty41 先以 `feedback-addendum-fdc65c0380399530` 判定方向更接近但需修正，随后放宽为“只需读成近战攻击”并以 `feedback-addendum-d1d4da36683c55ca` 明确接受该身体、要求正式唐刀且隐藏剑鞘。语义蒙版技术 remediation 最终由 a008 在候选 SHA `e6feabd6…0404` 下通过，Feedback `feedback-b9910b15738df26c` selected。review-only v1 使用正式唐刀 65% 等比合成并由双前爪覆盖握点，被判定刀像要脱手且劈得太低；v2 以握点为中心抬高 18°，仍暴露根因是两只生成前爪离主体过远。两版均未成为正式 Assembly、未批准/晋升/接入。用户随后授权一次 v6 局部编辑：`contract-d8a94b4327db2072` / `job-6a95f820a37e8586-a001` 绑定 selected body、正式唐刀和 `pose-guide-98eff3199fa852c9`，只允许双前爪/握柄变化。候选 SHA `03f988b3…e6b9` 的双爪更靠近同一刀柄、无剑鞘且斩向抬高，但模型仍重画主体并把唐刀漂成宽双刃剑；技术门禁失败，Feedback `feedback-da1e6e7295771c76`，cty41 addendum `feedback-addendum-31df2cd707d8bca2` 明确判定握持不对且爪子收得不够。未批准/晋升/接入。用户随后授权 v7 深嵌握持 `contract-a3ae892a4eb513b0` / `job-75ce23e75e7159f7-a001`：候选 SHA `dd22b18f…4cf8` 隐藏了环首与大部分刀柄、护手贴近主体且斩向抬高，却只留下一个可辨前爪，同时继续重画主体并生成宽双刃剑；Feedback `feedback-12ca8cca66a45b45` 为 technical_failed。cty41 以 `feedback-addendum-7cd7b33df185bd64` 明确回到此前接受的 body，并把下一视觉时刻改为“举剑准备下劈、剑尖接近最高点”，不再表现下劈低点。v7 未批准/晋升/接入。随后不再生成身体：review-only high-guard v3 将正式唐刀旋转到右上，cty41 确认方向正确并要求继续调整刀尺寸/位置；修正全画布旋转平移问题后，v5 将正式唐刀放大到 85%、环首握点内收至 `[141,205]`，刀尖位于头部右上且不遮脸，输出 `reviews/poet_melee_dr_selected_body_sword_mockup_v5_high_guard.png`（SHA `c53dcf02…f887`）。cty41 明确判定“这版整体可以”。该结论只冻结 review-only 构图。后续 Melee UL v2 使用正式 UL 身份与后层唐刀，用户指出其与 DR 的前倾不对应；将 UL 再前倾 7° 后又确认策略走得过远。对照正式 Demonbound Melee DR 及 Hunter/Splitjaw 地面 Melee 候选，主体均主要保持直立、动作由武器/手爪承担，因此回到 DR 消减前倾：总校正 5° 的 v7 仍需更直，总校正 8° 的 v9 仍需完全直立，最终总校正 10° 的 `reviews/poet_melee_dr_high_guard_mockup_v11_upright_calibrated.png`（SHA `113cad67…d4bf`）恢复脚底 y=236，并由 cty41 明确判定方向可以。随后 UL 回到直立原生身份。首个直立 UL 仍沿用 Idle 双手分置，cty41 指出身体角度与双手位置都应和 DR 对应；最终 v9 清除 Idle 装备/双手语义区，保留原生 UL 头身尾脚，并把 accepted DR 的两只前爪水平投影到左侧同一高举握点。用户选择在此基础上向攻击方向前倾 5°，校准后的 `reviews/poet_melee_ul_high_guard_mockup_v11_five_degree_calibrated.png`（SHA `1528d299…a474`，脚底 y=236）获 cty41 判定方向可以。随后 cty41 指出 UL v11 的双爪错误地绘在主体前层；v13 保持 5° 姿态、正式唐刀与 y=236 不变，将两爪和握柄都放回主体后层：正式近爪只露身体左缘小弧，第二爪位于下方且几乎完全被主体遮挡。v13 获 cty41 判定深度正确，但灰底复核暴露主体右后方仍有未清理涂层。v14 在绑定的 pre-transform `[155,120,190,185]` 区域内以 approved clean UL body alpha 为轮廓白名单，清除了 644 个轮廓外像素（原 alpha 1–255），并通过灰底人工复核。v14 的残留清理虽获判定干净，但随后放大检查发现两处正式身体黑边被误删。v15 从清理前源精确恢复 v14 实体轮廓 2px 邻域内 64 个原始 RGBA 像素，仍有轻微断裂；v16 扩到 4px/136 像素后仍未完全修复且重新出现残留。cty41 明确停止继续修补，并指定现有 `reviews/poet_melee_ul_high_guard_mockup_v15_outline_restore.png`（SHA `595bc79b…6dfa`）“先这样，也能用”。因此 v15 被指定为 provisional usable，v16 为负面证据。用户随后明确要求晋升；受控 reviewed-import 先清除 v15 在 `(165,171)` 的唯一 `#00ff00/alpha=2` 精确色幕残留，不改变可见几何，生成 Candidate SHA `7fde40f9…d5b7` 与四栏尺寸证据 `size-comparison-f40bbe9251d4e92f`。`contract-730c6d668387c680` / `job-7bafde3439fd4e9f-a001` 经 Approval `approval-e913d6270f923eb7` 晋升为 `calibrated/poet_five_red_chow_melee_ul_high_guard_v01.png`。随后用户选择晋升 Melee DR v11。受控收编先清除母版 23 个 `#00ff00` 精确低 alpha 边缘像素（最大 alpha 23）及缩略图重采样后出现的 4 个同类像素，bbox 与姿态不变；`contract-5b8c01856dcd9e10` / `job-72d7ec1c23adfbdc-a001` 绑定 `size-comparison-32071169d9857a00`，经 Approval `approval-c0b74a1d8e6e944a` 晋升为 `calibrated/poet_five_red_chow_melee_dr_high_guard_v01.png`（SHA `22d198c9…f591`）。至此 Melee DR/UL 均正式离线晋升，但未接入运行时。

### 独立出鞘装备：已批准，不计入角色动作覆盖

最新技术收尾：用户明确授权仅清除两点、检测通过后自动机械验收/晋升，并允许绑定新 SHA 技术批准；不声称新视觉会话。`reviewed-recontract-e6a91dc49320f564` 将源 `job-c91127f8d0e61731-a002` 绑定到 `contract-a6b9130f239467e7`，新 Attempt `job-a254d0afc9b7b286-a001` 已 promoted，Approval `approval-8dbcb2755d3c06b2`，style verdict `equipment-style-verdict-c6f6105d9c6260b2`，AD verdict `art-direction-verdict-debb48d7057ac13e`。三项新 acceptance 通过，固定装备/AD review 为 v03。

当前独立正式输出为 [unsheathed_v02](equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02.png) 及 [128](equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02_128.png)。母版 SHA `375409b3b1fa37a4e74b81500d5abea63c57d311fed5eabcc85759d49aa193b1`；预览 SHA `d1db586755bb515036ef7626f315dab88aea775ff651668afa5af64cda3e1f49`。实测仅 `(117,166)` 的 `[0,255,0,3]`、`(169,236)` 的 `[0,255,0,2]` 清零；256×256 未 resize，visible 90×96、baseline 236，report passed，palette complexity advisory 保留。旧 v01 与下列原批准历史完整保留。strict 历史 16 项精确色幕污染不豁免；4 张 processing QA 已逐文件按本任务授权登记为 project-owned supporting-derived，非新视觉批准、不改变许可证。

以下为清理前历史：

- cty41 先接受 v02 比例，再明确接受展示的 raw_v02 与 128 预览整体外形及画风；本次仅机械落成该既有接受，没有声称用户新看过 QA 对照板，也不批准未来图片。
- Attempt `job-c91127f8d0e61731-a002`，原合同 `contract-bfb85273c3228d32`；Approval `approval-40e551d85e6899c1`，装备风格 Verdict `equipment-style-verdict-1f126a937643f493`，Art Direction Verdict `art-direction-verdict-67590b39415c014d`。三个 Brief Acceptance Case 已绑定真实现有证据；调色板复杂度仍为 advisory。
- 正式离线装备：[出鞘唐刀母版](equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v01.png) 与 [128 预览](equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v01_128.png)。正式文件沿用原合同 `_v01` 输出名，内容是已接受的候选 v02；母版 SHA-256 `704cd888947a648a9e1ee0db154804288ba230eca422fcc4c63229c145b9e82b`，与候选逐字节一致。
- 实际 v02 使用既有两张 approved 来源：入鞘 DR v02 负责环/柄/护手/画风，旧唐剑 v01 仅负责金属配色和细节密度。`feedback-addendum-6589876558d05ba6` 澄清旧 feedback 中 one input / <=220 words / new contract 属于生成前已取消的主代理准备要求；旧记录和 v01 prompt 改写说明均保留。
- 附加逐像素 QA 发现母版保留两个极低 alpha 的精确绿像素（alpha 3/2），128 预览没有；状态机 strict 与既有装备 report 未检出。`feedback-addendum-6086f09a42ad87ca` 如实记录此技术门禁缺口；候选及晋升字节不改，不声称所有 QA 全绿。
- Melee DR/UL 已于 2026-09-13 经 reviewed-import 正式离线晋升；Cast DR/UL、Hit DR/UL 与通用 Death 随后于 2026-09-14 经 cty41 明确选择并离线晋升。技能合同与表现映射复核确认诗人没有 `Ranged` execution kind，因此 Thrown/Ranged 不是诗人必做动作。动作晋升不等于运行时接入授权；当前诗人动作字段仍为空并安全回退到方向 Idle，Death 单独以 Idle DR 占位。

按 pipeline promoted Attempt 盘点：装备版完整角色已有 Idle DR v03、Idle UL v07，以及 Melee high-guard、Cast vertical-guard 与 Hit hunter-recoil 的 DR/UL，另有通用 Death v02。Idle 两图已分别登记为 `godot/assets/units/doge_poet.png` 与 `doge_poet_ul.png` 的逐字节一致运行时副本；Melee、Cast、Hit 与 Death 均仅离线晋升，尚未接入。另有无装备身份底稿 Idle DR v03（`job-43b34a4685f71ca2-a010` / `approval-1acd77a15a84f096`），它不是新的动作覆盖，也不应取代更直接的装备版来源。独立装备、组件、研究图与历史候选不计入动作覆盖。

覆盖状态含义：**已批准**=有 promoted Attempt 与 cty41 Approval；**尝试失败**=存在可追溯候选但没有 promoted 输出；**待制作**=建议补齐但尚未生成正式候选；**待确认**=需求/表现/变体尚未批准；**历史模板**=不得直接用于新生成。

## 来源及职责

所有路径以仓库根为基准。批准成图不自动具有任意风格锚点权限；生成前还须核验适用的窄职责 AnchorVerdict、合同和来源确认。

| 来源 | 路径 | Attempt / Approval | 允许职责 | 拟进入 ImageGen |
| --- | --- | --- | --- | --- |
| S-DR 装备版 Idle DR | `Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png` | `job-34f6747e5a1f30d0-a001` / `approval-3affe3151e3652b6`；v3 AnchorVerdict `anchor-verdict-0baad2ab67c2076a` | 五红身份、DR 解剖、唯一核心体量基准、配色、四爪、卷尾与空鞘关系；不提供攻击姿态或出鞘刀状态 | v3 Image 1，已输入 a001 |
| S-UL 装备版 Idle UL | `Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_ul_v07.png` | `job-4affd0d3e702d0d0-a001` / `approval-a71f2eecb8e0ec5e` | 原生 UL 身份/后脑/耳型/方向；体量仍向 S-DR 校验；Idle 遮挡不自动延伸为动作合同 | UL 动作候选输入，待来源确认；本轮不输入 |
| A-DR 对应已批准 DR 动作 | Melee、Cast、Hit DR 均已有 promoted Attempt；详见下方矩阵对应行 | 按动作分别绑定 | 仅向相应 UL 提供同一动作峰值、装备状态和动作轴，不迁移正脸或 DR 遮挡 | 相应 UL 已使用窄职责合同并离线晋升 |
| W-DAO 正式出鞘唐刀 | `Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02.png` | `job-a254d0afc9b7b286-a001` / `approval-8dbcb2755d3c06b2`；v3 AnchorVerdict `anchor-verdict-7d63dc0c53bc6c4a` | 仅负责环首、柄、护手、直身单刃刀身、比例、刀尖、颜色和细节预算 | v3 Image 2，已输入 a001 |
| G-V3 确定性 Release→Impact Guide | `Tools/artworks/poet_five_red_chow/guides/poet_melee_dr_release_impact_pose_guide_v3.png` | `pose-guide-e156d4cb46ff0e9c` | 仅负责力线、重心、支撑/驱动脚、压缩、反向平衡、握点和刀端；不能成为 Sprite | v3 Image 3，已输入 a001 |
| X-ACTION 跨角色动作参考 | Demonbound 明确排除 | 用户确认不使用 | 只作为旧策略的负面历史，不提供任何正向身份/动作/装备信号 | v3 不输入 |
| QA 语义蒙版、Tile/深度板 | 按具体 Attempt 绑定 | v3 a001 已生成 | 仅测量接触、体量、遮挡与落点，不当作成品外观 | Review-only，不作为 ImageGen 输入 |

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
| Melee DR | 已批准并离线晋升 | 高举唐刀准备下劈；主体近直立，脚底 y=236 | 一把正式出鞘唐刀；剑鞘缺席 | accepted body-first DR + W-DAO；`job-72d7ec1c23adfbdc-a001` | 双爪覆盖握点，无夸张整体前倾；母版已清理精确低-alpha 色幕 | `calibrated/poet_five_red_chow_melee_dr_high_guard_v01.png`；未接入运行时 |
| Melee UL | 已批准并离线晋升 | 与 DR 同一 high-guard 时刻；向攻击方向前倾 5° | 正式唐刀位于主体/爪后层；剑鞘缺席 | 原生 S-UL 身份 + DR 对应握持；`job-7bafde3439fd4e9f-a001` | 双爪大部由主体遮挡；接受 v15 的轻微黑边不连续，禁止回用 v16 残留 | `calibrated/poet_five_red_chow_melee_ul_high_guard_v01.png`；未接入运行时 |
| Hit DR | 已批准并离线晋升 | Hunter 标准中度漫画后仰峰值、耳朵惯性与双泪线 | 一把完全入鞘环首唐刀随身体偏转；无命中 FX | S-DR、正式 Hunter Hit DR、正式入鞘唐刀与 Hit Guide；reviewed-import `job-b68aa40bd7cd26c5-a001` | cty41 明确接受；身份漂移、梨形主体、泪线偏大、握鞘感和前爪偏大如实保留 | `calibrated/poet_five_red_chow_hit_dr_hunter_recoil_v01.png`；未接入运行时 |
| Hit UL | 已批准并离线晋升 | 与 Hit DR 同一受击峰值的原生背向版本；一眼一泪 | 完全入鞘环首唐刀位于身体后层；无命中 FX | S-UL、已晋升 Hit DR、正式 Hunter Hit UL 与 Guide；reviewed-import `job-211e9534c3ce587f-a001` | cty41 明确接受；第二前爪外弧不可读、身份/体量漂移、后仰偏弱和爪比例偏差如实保留 | `calibrated/poet_five_red_chow_hit_ul_hunter_recoil_v01.png`；未接入运行时 |
| Cast DR | 已批准并离线晋升 | 代表剑雨、将进酒、行路难、月下独酌、山中与幽人对酌五个技能族的统一垂直阻挡式 Cast 峰值 | 一把完全入鞘环首唐刀作为垂直施法焦点；无额外 FX | 用户选择 v07 语义，最终 reviewed-import `job-7d997c18b9657421-a001` | cty41 明确接受并晋升展示候选；已知长前臂、长杖化触地、握位偏差和身份漂移如实保留，不视为技术通过 | `calibrated/poet_five_red_chow_cast_dr_vertical_guard_grip_v01.png`；未接入运行时 |
| Cast UL | 已批准并离线晋升 | 与获批 Cast DR 同一统一施法峰值的原生背向投影 | 一把完全入鞘环首唐刀位于身体后层；无额外 FX | S-UL、已晋升 Cast DR、正式入鞘唐刀与 UL Guide；reviewed-import `job-45467bc1b4100e53-a001` | cty41 明确接受；已知侧脸偏置、主体过高过宽、后爪过小、鞘尖间隙弱和画风漂移如实保留 | `calibrated/poet_five_red_chow_cast_ul_vertical_guard_grip_v01.png`；未接入运行时 |
| Thrown / Ranged DR | 不适用 | 诗人六个 execution kind 中没有 Ranged/Thrown；普通攻击与侠客行走 Melee | 不存在诗人投掷对象 | `BattlePresentationFrame.ResolveAction` + 15 个 Poet Skill Resource | 不得因通用动作清单误造投掷唐刀、酒具或投射物 | 不制作 |
| Thrown / Ranged UL | 不适用 | 同 DR；当前 Godot 也只有 Ranged 槽，没有独立 Thrown 字段 | 不适用 | 同 DR | 不建立无玩法消费者的方向图 | 不制作 |
| Death 独立静态图 | 已批准并离线晋升一张通用图 | Hunter 胶囊死亡姿态：仰面倒地、头朝右上、X 眼 | 一把完全入鞘环首唐刀脱手并按魔剑士式紧贴尸体下缘；无 FX | S-DR 身份/体量、Hunter Death、正式入鞘唐刀、Demonbound 仅负责贴身间距与 v2 Guide；reviewed-import `job-b428606f17132f21-a001` | cty41 明确接受；身份简化、轻微豆形核心与鞘尖邻近爪轮廓如实保留 | `calibrated/poet_five_red_chow_death_hunter_close_dao_v02.png`；未接入运行时 |

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
- 直接双参考任务 `job-a6df55a1b4f8ac96` 的三轮视觉生成被 Feedback 判为未满足无臂四爪拓扑、唐刀形制或下劈动作张力；最后生成轮对应 a006 的 `pipeline-agent` Feedback disposition 为 `exhausted`。但 v1/v2 Melee Brief、Prompt 与 Feedback 中“帽、衣、葫芦必须保留”的要求和正式诗人身份/装备版 Idle 明确冲突，只能作为历史合同漂移，不再视为候选缺陷或未来 retry 输入；Agent disposition 也不是人类策略终止 receipt。
- 引入魔剑士 Melee 姿势职责后的三参考任务 `job-50532e1e9fe7c307` 只有 a001；它没有 Feedback，Report 只因 `annotations_missing`、`core_row_disconnected` 为 `technical_failed`。该次 Attempt 已终止，但现有证据不足以判定三参考视觉策略 exhausted。
- 2026-09-12 v3 单图实验完成前述恢复条件：`poet_melee_dr_release_impact_visual_moment_v3.md` 定义 Release→Impact 时间合同；`composition-632266b9dd38fea2` / `pose-guide-e156d4cb46ff0e9c` 首次表达力线、重心、支撑/驱动脚、压缩和反向平衡；`contract-5277695f41ebb229` / `job-0a6c57acd0d21d59` 只绑定正式诗人 Idle、正式出鞘唐刀和 Guide。a001 确实改善动作张力并避免帽衣葫芦污染，但因横向四足躯干、宽双刃剑、刀尖边界和空鞘歧义而 `technical_failed`。该候选不得晋升、接入或成为正向来源；是否 retry 必须等待 cty41 对本次单图的人工结论。

## 诗人试点可复用教训

- 同一次 ImageGen 同时要求重新设计动作、严格恢复身份、精确复现装备和满足几何窗口时，候选会在“动作更强但身份/拓扑漂移”与“身份恢复但退回 Idle”之间振荡。
- 当动作时点或剪影尚未确认，先用 `pose_proof.py` 确定性展示 `1–4` 张纯橙单色火柴人缩略草图及 128 预览；草图只决定冻结时刻、力线、重心、接触和负形，不混入犬种、配色、装备造型或技术检测框。
- Cast DR 的历史三方案选择现已回填为 `pose-proofs/poet_cast_dr_sheathed_focus_v1.json`：保留 cty41 选择 B 的完整抽象几何及 A/C 否决摘要，并绑定原 v03 Review 看板；旧 Composition/Contract/Attempt 不回写。
- 纯橙姿态经人工选择后，再由身份母图、装备母图和 Composition/Pose Guide 分别承担身份、形制与可测几何。新 `action_pose` Composition schema v3 必须绑定 Action Card 或受控豁免；已有可用身体和正式装备时，优先采用确定性 Assembly，不再让 ImageGen 重画冻结区域。
- 低成本 Draft、临时看板与未选方案完整几何不创建 Attempt/Series、不进入 Git；正式状态机从真实生成调用或需要追责的确定性处理开始。每生成一张即停止，连续两轮重复同一语义能力失败时返回姿态/来源层，不用 prompt 堆叠继续碰运气。

## 历史入口与依据

- [旧 Attack 模板](attack_prompts.md)：六帧、foreleg/hindleg、Tang sword/guqin/wine vessel 等措辞已经不适用于当前单帧无臂腿诗人；保留历史正文，不直接编译生成。
- [单帧动作规范](../../../.agents/skills/pure-run-artwork-pipeline/references/single-frame-action-poses.md)
- [死亡图规范](../../../.agents/skills/pure-run-artwork-pipeline/references/death-state-sprites.md)
- [方向与逐图迭代](../../../.agents/skills/pure-run-artwork-pipeline/references/imagegen-iteration.md)
- [Reviewer 设计](../../../.agents/docs/2026-09-04-pure-run-model-reviewer-design.md)
