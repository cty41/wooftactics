# Pure Run Equipment ImageGen Task Packet

## Category
- consumable

## Frozen base style
- clean readable silhouette at 128px
- crisp controlled outline matching approved Pure Run class weapons
- restrained flat material shading with only a few deliberate value regions
- single isolated object on chroma green with no cast shadow

## Category rules
- simple centered inventory silhouette
- use restrained liquid highlights and no realistic glass caustics

## Reference responsibilities
- character-style-anchor: Tools/artworks/doge/calibrated/doge_capsule_hunter_color_calibrated_v01.png @ 68a35da9d5d646d497ae7bf99eebc5871bcc5be26a893441d9fcece57877ac07
- consumable-category-anchor: Tools/artworks/pure_run/items/approved/pure_run_item_life_potion_v01.png @ b4430957f839c381088a50536e2b32a27eac027126a1c1862a64fa737f74a121

## Negative constraints
- no photorealistic rendering
- no glossy cinematic lighting or excessive specular highlights
- no airbrushed gradients, bloom, depth of field, text, border, or watermark
- do not copy layout, UI frame, or background from a third-party reference

## Feedback delta
- none

## Base prompt
# 旅行古琴｜独立道具候选 v2

生成一床适合诗人旅行携带、可斜背在背上的紧凑七弦古琴。保持古琴身份，不画成古筝、琵琶或普通木板。

## 冻结特征

- 七弦、深栗黑漆、暖棕边缘、少量金色徽位。
- 三分之四俯视对角展示，琴首左下、琴尾右上。
- 无桌架、无雁柱、无大量琴码。

## 本版修正

- 完整琴体长度约等于诗人角色身高，适合装入软套后斜背旅行；不得再生成超长演奏台比例。
- 强化古琴轮廓：首部较宽且圆润，尾部明显收窄，中段有克制但清楚的内收腰线。
- 岳山、龙龈、七弦端点和两只小琴足必须可读；琴面有轻微弧度。
- 绝不能是等宽直边木板。
- 不生成角色、背包、软套或背带；便携感仅由紧凑尺寸与比例表达。

## 输出

- Pure Run 粗轮廓、平涂、有限色阶；128px 预览可识别。
- 完整对角长度约 135px，垂直 AABB 约 95px。
- 单件居中，纯均匀 #00ff00 绿幕，无阴影、地面、文字、UI 或其他物品。

