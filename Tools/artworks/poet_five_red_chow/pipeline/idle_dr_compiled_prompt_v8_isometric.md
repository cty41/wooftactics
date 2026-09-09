# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- core_anchor: Tools/artworks/doge/calibrated/doge_capsule_demonbound_idle_dr_v01.png @ 21559ca7aedd3e146feb2b93c5a6633affc6ce544122628c960972a982b0b5e7
- identity_reference: Tools/artworks/poet_five_red_chow/concepts/poet_five_red_chow_idle_dr_raw_v06_formal.png @ ee964f8e2251c2b128e3f8ba6405ab13a1b2144e9f5590d4cb6b0ef0cc54d49a

## Composition
```json
{
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      128,
      231
    ],
    "tiltDegrees": [
      -2.0,
      2.0
    ],
    "top": [
      128,
      121
    ]
  },
  "equipmentState": {
    "guqin": "absent",
    "scabbard": "absent",
    "staticEffects": "absent",
    "sword": "absent",
    "wineVessel": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [],
  "weapon": {
    "bladeCenterline": [
      124,
      194,
      132,
      202
    ],
    "exitWindow": [
      124,
      194,
      132,
      202
    ],
    "guardWindow": [
      124,
      194,
      132,
      202
    ],
    "hiddenGrip": [
      128,
      198
    ],
    "maxBladeWidthPx": 1,
    "maxGemAreaPx": 1,
    "tipRegion": [
      124,
      194,
      132,
      202
    ]
  }
}
```

## Unresolved fixes
- 头部和口吻朝画面右下偏转；双眼、双耳、前爪和后爪体现近远侧差；核心仍竖直且脚底中心不变

## Base prompt
# 五红土松诗人 Idle DR 正式合同生成

用户已确认 `poet_five_red_chow_idle_dr_raw_v05_capsule.png` 的视觉方向。生成一个新的原生完整 Sprite，严格保持该候选的窄直胶囊核心、平行中段、不外扩下段、四爪位置、五红土松头脸、红棕色、小卷尾和简化平涂。

同时以批准 Demonbound Idle DR 为几何锚点：身体核心高度、最大宽度、中心、前爪贴合方式、后爪间距和 down-right 等距朝向必须匹配。只允许修正色幕边缘、轮廓连续性和目标构图，不增加新设计。

纯 `#00ff00` 均匀背景；无剑、古琴、酒器、服装、阴影、地面、场景、文字、UI 或特效。完整角色不裁切。

