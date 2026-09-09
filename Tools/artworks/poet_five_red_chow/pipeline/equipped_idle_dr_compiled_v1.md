# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- body_anchor: Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_idle_dr_v03.png @ ee4bc7fe07aa2e2d2cd96e9d1d9c62779d07b191e1e1dd2efe0e05c3f096e753
- guqin: Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_guqin_compact_v03.png @ 1abc09cbedfee504d9f3ad33fc71f44f5996cf5d0ba1eab07091b1c7b0cf7cb7
- tang_sword: Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_tang_sword_v01.png @ d4a4198fb47a3d84a0c5f6656d1cef0e66af369c3f85a0ae6692d5ae299164bd
- wineskin: Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_wine_wineskin_v01.png @ b677fa5977ea35454bf24238bbf8a9f10833bb37c95454abe7d0b46fac1cd926

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
    "guqin": "back-layer-diagonal",
    "scabbard": "absent",
    "staticEffects": "absent",
    "sword": "near-hand-front",
    "wineVessel": "far-left-waist-wineskin"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [
    {
      "label": "face-eyes-muzzle",
      "rect": [
        112,
        138,
        146,
        158
      ]
    }
  ],
  "weapon": {
    "bladeCenterline": [
      101,
      126,
      154,
      186
    ],
    "exitWindow": [
      146,
      178,
      164,
      198
    ],
    "guardWindow": [
      142,
      174,
      162,
      194
    ],
    "hiddenGrip": [
      155,
      190
    ],
    "maxBladeWidthPx": 9,
    "maxGemAreaPx": 1,
    "tipRegion": [
      92,
      112,
      114,
      140
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# 五红土松诗人｜装备版 Idle DR v1

以已批准的裸装五红土松诗人 Idle DR 为唯一身体、脸、体量、视角和脚位母图，制作一张完整连贯的装备版角色 Sprite。

## 冻结角色

- 保持同一 down-right 三分之四等距视角、宽度、高度、鬃毛、短宽口吻、小眼、四爪、小卷尾和红棕配色。
- 核心胶囊不得变宽、变窄、拉长或梨形外扩；脚底中心与基线不变。
- 无手臂：物品直接由小前爪稳定接触，禁止细连接臂。

## 装备构图

- 近侧右爪握已批准唐剑。剑柄在右爪内，剑身从右腰前方向左上横跨，剑尖落在角色左上安全区；不能遮挡双眼和口鼻。
- 已批准紧凑旅行古琴斜背在身体后层。只允许琴尾从右肩后上方、琴首从左腰后下方适量露出；琴体中央大部分被身体遮挡，不能像手持琴或漂浮木板。
- 已批准皮革酒囊系在远侧左腰，位于身体侧后层，只露出小型水滴皮囊与系耳；不得放大成主装备，不得替换成葫芦。
- 不生成剑鞘、长背带、衣服、帽子、特效、地面或阴影。

## 风格与输出

- 严格 Pure Run 平涂：粗而受控的深棕轮廓、少量硬边色块，无柔光、无拟真材质和 AI 渐变。
- 单角色完整居中，纯均匀 #00ff00 绿幕；正式校准后 256×256，脚底 y=236。

