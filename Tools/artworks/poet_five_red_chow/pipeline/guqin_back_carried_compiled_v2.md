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
- 保持窄琴尾右上、宽琴首左下，但将屏幕轴调整得更横向，目标端点水平差约90到105px、垂直差约70到85px

## Base prompt
# 五红土松诗人｜背负朝向古琴单图 v1

制作单独一张专用于 down-right 等距角色背部的紧凑旅行古琴组件。保持已批准紧凑古琴的七弦、深色漆面、暖色边框、内收腰线、宽琴首、窄琴尾和小琴足身份，但必须按真实三维背负姿态重新投影，而不是旋转或粘贴原正投影图。

- 固定摄像机与已批准诗人 Idle DR 相同：从上方观察角色右前侧。
- 古琴的背面贴近角色背部平面，长轴从画面左下通向右上；琴尾位于右上且更远，宽琴首位于左下且更近。
- 因透视与等距投影，琴面应明显变窄、具有侧缘厚度；七弦沿斜轴压缩但仍可辨识。
- 组件应看起来已经靠在背上：不是平放桌面、不是竖直正视、不是简单二维旋转。
- 不绘制角色、手、背带、琴袋、剑、酒器、桌面、地面或阴影。
- Pure Run 粗轮廓、2–3 个硬边平涂色块；无柔光、渐变、纹理噪点和拟真材质。
- 单一完整物体居中，纯均匀 #00ff00 绿幕；正式校准目标可见高度约 94px。

