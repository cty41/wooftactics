# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities


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
      118
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
  "pose": {
    "allFourPawsRequired": true,
    "camera": "fixed-isometric-45-degrees",
    "frontPawsAttachedToCore": true,
    "hindPawsOnBaseline": true,
    "stance": "upright-capsule-dog-idle",
    "tail": "high-set-curled-clear-of-face",
    "worldFacing": "down-right"
  },
  "weapon": {
    "exitWindow": [
      124,
      182,
      132,
      190
    ],
    "hiddenGrip": [
      128,
      186
    ],
    "maxBladeWidthPx": 0,
    "maxGemAreaPx": 0,
    "tipRegion": [
      124,
      182,
      132,
      190
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# 五红土松诗人 Idle DR 方案 B：项目简洁画风

保留方案 A 的五红土松身份、宽头短吻、红棕毛色、卷尾、胶囊体、四爪位置和 down-right Idle。显著减少毛发碎片、眼睛高光、睫毛感、脸颊细线和身体内部明暗层次；将鬃毛概括为少量大色块，外轮廓更粗、更稳定。向批准的 Demonbound 母图靠拢：小而简单的黑眼、克制表情、稀疏内部线、扁平有限色板。不得增加装备、服装、背景或阴影。纯 `#00ff00` 色幕，256×256 构图目标。

