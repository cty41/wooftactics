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
    "direction": "upper-left rear three-quarter, 25–35 degrees from straight rear",
    "earPlanes": "screen-left ear visibly reprojected toward profile; screen-right ear smaller and receding",
    "exclusions": "no paws, equipment, full face, second eye or mane seam",
    "faceCue": "one tiny eye and minimal nose/muzzle edge may appear only on screen-left outline",
    "tail": "tail mass must be on screen-right",
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
# Poet Idle UL Corrected Body Component

Create a 25–35° upper-left rear three-quarter view. The face turns toward screen-left: show only one tiny near eye and the smallest nose/muzzle edge at the screen-left contour. Reproject the screen-left ear toward profile; keep the screen-right ear smaller and receding. Put the fluffy tail on screen-right. Preserve the smooth narrow 66×110 capsule core, approved orange chow identity, and flat directional value planes.

Body component only: no paws, feet, limbs, equipment, guqin, wineskin, or gourd. No full face, second eye, large muzzle, mane seam, text, floor, shadow, gradients, or effects.

