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
# 五红土松诗人 Idle DR 首版生成提示词

```text
Create one production-ready Pure Run 2D single-frame character sprite on a perfectly uniform pure #00ff00 green background.

Use the attached Chinese Chow Chow breed chart only for the five-red native Chow Chow identity: cinnamon-red dense fur, broad lion-like head, short wide muzzle, small upright triangular ears, dignified dark eyes, and a thick high-set curled plume tail. Ignore every word, price, red mark, white background and every other dog in the chart.

Match the approved Pure Run capsule-dog visual language shown by the project anchor: a compact upright capsule body, thick dark-brown outer contour, sparse controlled interior lines, flat limited colors, subtle hard-edged cel-shaded blocks, crisp game-sprite edges. This is a stylized game unit, not a realistic dog and not pixel art.

Pose: native down-right 45-degree isometric idle. The furry capsule body stands upright. Exactly four paws: two tiny front paws attach directly to and overlap the left and right capsule edges by a broad multi-pixel contact, with absolutely no arms, forearms, elbows, limb stubs or thin connector lines; two hind paws rest on the same feet baseline. Show enough near/far offset for isometric depth while preserving a calm symmetric idle. Keep the face readable and unobstructed. The curled tail rises behind the body and stays clearly separate from the face.

Canvas and framing: 256x256, body centered at x=128, hind-paw baseline y=236, visible full character roughly 122 px tall, generous transparent-safe margin after chroma removal. One character only.

Identity: this is the Poet inspired by Li Bai, expressed only through a calm, self-assured, slightly free-spirited expression. Base mother image has no clothing and no equipment.

Absolutely absent: sword, scabbard, guqin, zither, wine vessel, gourd, pouch, jewelry, clothes, hat, spell effect, aura, motion trail, floor, cast shadow, scenery, text, watermark, UI.

Avoid: Shiba Inu face, Tibetan Mastiff proportions, Pomeranian fluff ball, realistic quadruped anatomy, humanoid muscular body, human hands, extra paws, missing paws, pear-shaped lower body, tail resembling a second head or flame, photorealistic fur strands, painterly rendering, glossy highlights, smooth 3D lighting, soft gradients.
```

