# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color

## Reference responsibilities
- image-1-approved-equipped-idle-dr-v03: Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3
- image-2-approved-demonbound-melee-dr-topology: Tools/artworks/doge/calibrated/doge_capsule_demonbound_melee_dr_v01.png @ a0fee8512cf8578f57b8b3d3c5e2b74a1f68ac3afdcbefd15a87c82f5a85eaf5
- image-3-approved-unsheathed-dao-v02: Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02.png @ 375409b3b1fa37a4e74b81500d5abea63c57d311fed5eabcc85759d49aa193b1

## Composition
```json
{
  "bodyLayer": false,
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
      2.0,
      10.0
    ],
    "top": [
      119,
      114
    ]
  },
  "coreBbox": [
    90,
    112,
    166,
    231
  ],
  "equipmentState": {
    "blade": "drawn-single-ring-pommel-tang-dao",
    "scabbard": "present",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [
    {
      "name": "eye-and-face-weapon-exclusion",
      "rect": [
        91,
        105,
        151,
        149
      ]
    },
    {
      "name": "wrong-upper-left-blade-tip",
      "rect": [
        0,
        0,
        105,
        118
      ]
    },
    {
      "name": "detached-lower-left-weapon",
      "rect": [
        0,
        174,
        84,
        255
      ]
    },
    {
      "name": "too-horizontal-right-blade",
      "rect": [
        205,
        135,
        255,
        181
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "action": "single frozen peak of a forward steep diagonal downward slash toward screen lower-right",
    "core": "preserve Image 1 compact poet volume and complete four attached paws",
    "direction": "native down-right front three-quarter facing",
    "drawnDao": "Image 3 form; guard beside body, blade descends steeply to lower-right; do not copy Image 2's more horizontal sword axis",
    "effects": "none",
    "feet": "four paws remain coherent; rear feet retain y236 ground anchor",
    "grip": "follow Image 2 topology only: a small round paw sits on the body edge and grip with broad multi-pixel contact; no arm, thin connector, gap, or floating paw",
    "scabbard": "empty scabbard remains attached at side/back and partly behind the body"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      155,
      164,
      224,
      226
    ],
    "exitWindow": [
      138,
      143,
      163,
      171
    ],
    "guardWindow": [
      145,
      148,
      171,
      178
    ],
    "hiddenGrip": [
      146,
      157
    ],
    "maxBladeWidthPx": 18,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        151,
        158
      ],
      [
        226,
        228
      ]
    ],
    "tipMayBeOccluded": false,
    "tipRegion": [
      194,
      204,
      241,
      237
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create one complete 256×256 crisp pixel-art character sprite on a perfectly uniform pure #00FF00 background.

Use the three supplied images in exactly this order, with strict roles:
1. Image 1: identity and volume only. Preserve the five-red Chow poet, compact capsule body, DR three-quarter facing, face and colors, clothing, hat, gourd, four attached paws, foot anchor, and the empty scabbard at the side/back.
2. Image 2: topology and action readability only. Use its successful no-arm solution: small round paws sit directly on the body edge and weapon grip with broad contact, and the melee action reads clearly. Do not inherit its dog breed, colors, sword, collar, armor, equipment, or its more horizontal sword angle.
3. Image 3: weapon form only. Preserve the ring pommel, grip, guard, straight single-edged Tang dao blade, proportions, tip, colors, and pixel-detail language.

Pose the poet at the single peak of a forward Melee DR downward slash, facing down-right in the fixed isometric front three-quarter view. The grip is high and immediately beside the body. The dao must run on a clear steep diagonal from the body toward screen lower-right, with the tip low in the lower-right. Do not make the sword horizontal.

No visible arms or leg connectors. Keep two front paws and two rear paws. The gripping front paw is small and round, overlaps both the body edge and grip through a broad multi-pixel contact area, and never floats or connects by a thin line. Keep the other paws directly attached to the compact body.

The dao is drawn, so retain one empty scabbard attached at the side/back, partly occluded by the body. Keep the complete silhouette uncropped, preserve the compact body scale, and keep the ground anchor centered at x=128 with foot baseline y=236.

No second weapon, Demonbound identity or equipment, arms, motion blur, afterimage, streak, slash arc, glow, sparks, impact, aura, magic, particles, shadow, text, border, or background detail. Render exactly one full character.

