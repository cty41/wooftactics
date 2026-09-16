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
# 五红土松诗人 Idle DR 方案 D：B 画风 + C 犬种脸

用户已明确选择融合方向：冻结方案 B 的简化项目画风、扁平色块、稀疏内部线和小尺寸可读性；冻结方案 C 的宽阔方头、短宽口吻、厚鬃和庄重五红土松神情。

生成一个新的原生整体 Sprite，不做左右图块拼接。保持 down-right 直立胶囊犬 Idle、两只贴身前爪、两只落地后爪、红棕毛色和高位卷尾。眼睛采用 B 的小而简洁黑眼，不要睫毛或宠物卖萌高光；头脸比例采用 C，但把毛发内部层次压缩为少量大色块。纯 `#00ff00` 色幕；无剑、古琴、酒器、服装、场景、阴影、文字或特效。

