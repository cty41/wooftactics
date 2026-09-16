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
# 五红土松诗人 Idle DR 方案 C：强化犬种

保留方案 A 的胶囊体、四爪位置、卷尾、down-right Idle 和项目平涂轮廓。进一步强化中国五红土松：更宽更方的松狮头、更短更宽的口吻、更小且间距更宽的直立三角耳、更厚的环颈鬃毛、更深沉庄重而非宠物卖萌的眼神；红棕/肉桂被毛保持统一。避免柴犬尖脸、博美圆球脸、藏獒过大头、写实四足体和夸张笑脸。不得增加装备、服装、背景或阴影。纯 `#00ff00` 色幕，256×256 构图目标。

