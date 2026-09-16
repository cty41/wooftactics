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
- identity_reference: Tools/artworks/poet_five_red_chow/concepts/poet_five_red_chow_idle_dr_raw_v05_capsule.png @ 7dc3b5cd7affedd40e6c29c4b4ecf72e81c83f334c4a71156e94d21609459fca

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
      228
    ],
    "tiltDegrees": [
      -2.0,
      2.0
    ],
    "top": [
      128,
      116
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
      116,
      188,
      140,
      216
    ],
    "exitWindow": [
      116,
      188,
      140,
      216
    ],
    "guardWindow": [
      116,
      188,
      140,
      216
    ],
    "hiddenGrip": [
      128,
      198
    ],
    "maxBladeWidthPx": 0,
    "maxGemAreaPx": 0,
    "tipRegion": [
      116,
      188,
      140,
      216
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# 五红土松诗人 Idle DR 正式合同生成

用户已确认 `poet_five_red_chow_idle_dr_raw_v05_capsule.png` 的视觉方向。生成一个新的原生完整 Sprite，严格保持该候选的窄直胶囊核心、平行中段、不外扩下段、四爪位置、五红土松头脸、红棕色、小卷尾和简化平涂。

同时以批准 Demonbound Idle DR 为几何锚点：身体核心高度、最大宽度、中心、前爪贴合方式、后爪间距和 down-right 等距朝向必须匹配。只允许修正色幕边缘、轮廓连续性和目标构图，不增加新设计。

纯 `#00ff00` 均匀背景；无剑、古琴、酒器、服装、阴影、地面、场景、文字、UI 或特效。完整角色不裁切。

