# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- mother: Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_idle_dr_v03.png @ ee4bc7fe07aa2e2d2cd96e9d1d9c62779d07b191e1e1dd2efe0e05c3f096e753

## Composition
```json
{
  "bodyLayer": true,
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      127,
      231
    ],
    "tiltDegrees": [
      -2.0,
      2.0
    ],
    "top": [
      127,
      121
    ]
  },
  "coreBbox": [
    94,
    121,
    160,
    231
  ],
  "equipmentState": {
    "scabbard": "absent",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "core": "body-only core fits cyan guide",
    "direction": "native upper-left back view",
    "exclusions": "no paws and no equipment"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "exitWindow": [
      168,
      160,
      168,
      160
    ],
    "hiddenGrip": [
      168,
      160
    ],
    "maxBladeWidthPx": 0,
    "maxGemAreaPx": 0,
    "tipRegion": [
      168,
      160,
      168,
      160
    ]
  }
}
```

## Unresolved fixes
- Mask output directory was absent, so the first validation ran before mask attachment.
- body component deterministic pass and human review

## Base prompt
# Poet Idle UL Body Component

Generate only the five-red chow poet's native upper-left back-view core, two ears, and one modest fluffy tail on pure `#00ff00`. Fit the central capsule exactly to the cyan narrow guide. Preserve approved identity and palette. Do not draw any paws, arms, legs, equipment, guqin, wineskin, gourd, face, mane seam, floor, shadow, text, gradients, or effects. Leave clean empty space around both side contact zones and beneath the body for separately assembled paws. Bold dark outline, flat low-detail Pure Run style, 256×256.

