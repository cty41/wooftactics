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
# 紧凑旅行古琴｜独立道具候选 v3

生成一床能斜背在五红土松诗人背上的紧凑七弦古琴。

## 保留

- 七弦、清楚收腰、宽圆琴首、收窄琴尾。
- 深栗黑漆、暖棕边缘、少量金色徽位。
- 可读的岳山、龙龈、弦端与两只小琴足。
- 三分之四俯视对角展示：琴首左下、琴尾右上。

## 尺寸与比例修正

- 相比上一版进一步原生缩短琴体，不做横向或纵向机械压缩。
- 采用更紧凑、略宽厚的旅行琴比例；仍然是古琴，不能变成琵琶或木板。
- 正式候选目标 AABB 约 `102×76px`，完整对角约 `127px`，斜背后不明显超过诗人角色身高。
- 七弦在缩小后仍以清楚的平行分组可读，不制造高频噪点。

## 输出

- Pure Run 粗轮廓、平涂、有限色阶。
- 单件完整居中，纯均匀 #00ff00 绿幕。
- 无角色、手、软套、背带、桌架、雁柱、地面、阴影、文字或 UI。

