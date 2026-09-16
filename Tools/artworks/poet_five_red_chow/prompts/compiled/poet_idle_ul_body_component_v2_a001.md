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
      128,
      231
    ],
    "tiltDegrees": [
      -4.0,
      -2.0
    ],
    "top": [
      123,
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
    "core": "narrow capsule with top center 5 px screen-left of foot center",
    "direction": "upper-left three-quarter back, about 15–25 degrees away from straight rear",
    "earPlanes": "small inner plane visible on screen-left ear; screen-right ear recedes",
    "exclusions": "no paws, equipment, face or mane seam",
    "valuePlanes": "screen-left near plane slightly lighter; screen-right back plane darker, flat values only"
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
- none

## Base prompt
# Poet Idle UL Three-Quarter Body Component

Generate only the poet core, ears, and tail on pure `#00ff00`. This must read as an upper-left three-quarter back view—not a symmetric straight rear view. Shift the top of the core about 5 px screen-left relative to the bottom center. Show a small inner plane on the screen-left ear while the screen-right ear recedes. Use a slightly lighter flat screen-left plane and darker flat screen-right plane. Preserve a narrow compact capsule, approved orange chow identity, simple contour, and no face or mane seam.

No paws, feet, side bumps, arms, legs, weapon, ring, sheath, guqin, wineskin, or gourd. Bold dark outline, flat low-detail Pure Run style, 256×256.

