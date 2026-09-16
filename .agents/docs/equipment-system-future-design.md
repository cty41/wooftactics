# 未来装备系统设计（本轮不实现）

状态：`deferred_design_only`

## 本轮边界

诗人版本不新增装备 schema、装备 Resource、掉落、商店条目、背包槽位、职业限制或迁移。侠客行等技能不要求剑，将进酒等技能不要求琴。现有装备代码与十二件迁移装备保持原样；本文不得被解释为已实现合同。

## 建议领域模型

### EquipmentFamily

稳定 family 用于规则与内容演进，不用显示名做判断：

- `weapon.sword`、`weapon.spear`、`weapon.bow`、`weapon.staff`、`instrument.qin`
- `armor.light`、`armor.medium`、`armor.heavy`
- `accessory.charm`、`accessory.ring`

每件装备具有稳定 `ContentId`、实例 ID、family、slot、rarity、price、基础属性修正、可选玩法 tag。family 与 slot 分离，例如琴可以占武器位，也可在产品决策后占副手位。

### 装备资格

建议按显式 tag 组合，而不是硬编码职业 switch：

- 单位模板声明 `AllowedEquipmentFamilies` 与 `DeniedEquipmentFamilies`。
- 装备可声明 `RequiredRoleTags`、`RequiredAttribute`、`MinimumAttribute`。
- 技能默认不检查装备；只有明确标注 `RequiredEquipmentFamilies` 的未来技能才在 Availability 和执行入口共用同一校验。
- 永远以 Core/Application engine-neutral DTO 表达，Godot Resource 只负责序列化。

### 背包与占位

- 角色装备槽和共享背包继续保存装备实例，不保存 Resource 副本。
- 装备/卸下/替换是单事务；目标槽已有装备时先验证卸下物能进入背包，再一次性提交。
- 背包满时禁止会产生额外背包物品的装备替换，不得先卸下再失败。
- 双手武器、副手和成套占位在引入前必须增加显式占位模型，不能通过两个 slot 写同一实例实现。

## 获取与商店

- 战斗掉落、宝箱、事件、商店均先生成确定性的 `EquipmentOffer`，确认后才生成实例。
- 商店 offer 池按层级、rarity、family 权重和队伍可用性构建；保底只保证存在可购买装备，不保证适合特定职业。
- 刷新、购买和出售均使用 transaction key 防重；价格由内容基价与 run 修正计算并冻结在 offer 中。
- 死亡角色卸下物进入共享背包；背包不足时进入明确的 overflow resolution，不允许静默销毁。永久死亡与普通倒地采用同一卸载事务，但记录不同原因。

## Full-backpack 决策表

| 操作 | 背包空间 | 结果 |
|---|---:|---|
| 空槽装备背包物品 | 任意 | 成功，背包减少一件 |
| 替换已装备物 | 至少可容纳旧物 | 原子替换 |
| 替换已装备物 | 无空间 | 拒绝且状态不变 |
| 卸下 | 有空间 | 成功 |
| 卸下 | 无空间 | 拒绝且状态不变 |
| 角色永久死亡自动卸载 | 足够 | 全部转入背包 |
| 角色永久死亡自动卸载 | 不足 | 进入 overflow resolution，结算前必须解决 |

## 存档与迁移建议

1. 先新增版本化 DTO：family/tag/限制字段默认映射现有装备，旧存档实例 ID 与 ContentId 不变。
2. 加载时只做纯迁移，不访问 Godot ResourceLoader；缺失内容产生可诊断错误而不是丢弃实例。
3. 存档版本升级必须覆盖 active run、checkpoint、背包、角色装备和 pending store offer。
4. 增加 encode/decode round-trip、旧版本升级、重复 transaction、死亡卸载及背包满测试。
5. 新版本上线若无法无损解释旧 active run，应像既有重大候选池迁移一样显式标记 `RequiresNewRun`，不能暗中重置。

## 尚未决策的产品问题

- 琴是主手、双手还是独立乐器槽。
- 诗人是否允许所有剑，其他职业是否允许琴。
- 是否支持耐久、词缀、套装、强化、绑定与出售折损。
- overflow resolution 是临时溢出箱、强制丢弃还是结算选择页。
- 商店是否按当前三人队伍过滤，还是允许为候选/未来招募购买。
- 永久死亡装备是否全部回收、部分损坏或形成墓碑掉落。

这些选择必须另行 grilling 和计划，不得在实现时自行推断。
