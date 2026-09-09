# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color

## Reference responsibilities
- equipment_reference: Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_sheathed_ul_v01.png @ 28a2de2782aace589855fd7aafb86e90ff11204bb085a12c5a4bf0ee2613eed4
- mother: Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_idle_dr_v03.png @ ee4bc7fe07aa2e2d2cd96e9d1d9c62779d07b191e1e1dd2efe0e05c3f096e753

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
      -2.0,
      2.0
    ],
    "top": [
      128,
      121
    ]
  },
  "equipmentState": {
    "scabbard": "present",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [
    {
      "name": "face-on-back",
      "rect": [
        98,
        116,
        160,
        176
      ]
    },
    {
      "name": "front-layer-left-free-paw",
      "rect": [
        78,
        149,
        111,
        211
      ]
    },
    {
      "name": "front-layer-right-holding-paw",
      "rect": [
        145,
        149,
        181,
        211
      ]
    },
    {
      "name": "vertical-or-lower-scabbard-fragment",
      "rect": [
        150,
        176,
        184,
        232
      ]
    }
  ],
  "requiredHiddenLabels": [
    "near_hand",
    "far_hand",
    "equipment"
  ],
  "screenSpaceIntent": {
    "continuity": "ring grip and any visible throat or sheath cue form one connected object; most sheath may remain hidden",
    "direction": "native upper-left back view",
    "feet": "two compact directly attached rear paws at baseline",
    "freePaw": "tiny screen-left near paw arc behind core and not touching equipment",
    "holdingPaw": "tiny screen-right far paw arc behind core",
    "ringPommel": "small edge-on ring at the screen-right shoulder",
    "scabbard": "from the right-shoulder ring and grip, the fully sheathed connected axis points diagonally toward screen upper-left behind the body"
  },
  "visibleAreaCaps": {
    "equipment": 0.22,
    "far_hand": 0.22,
    "near_hand": 0.22
  },
  "weapon": {
    "bladeCenterline": [
      96,
      116,
      169,
      168
    ],
    "exitWindow": [
      146,
      136,
      177,
      173
    ],
    "guardWindow": [
      151,
      145,
      174,
      174
    ],
    "hiddenGrip": [
      166,
      160
    ],
    "maxBladeWidthPx": 13,
    "maxGemAreaPx": 0,
    "tipRegion": [
      88,
      108,
      126,
      145
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# Five-Red Chow Poet — Human-Authorized Idle UL Attempt

Create one native upper-left back-view sprite on pure `#00ff00`. Preserve the approved poet identity, but make the core visibly narrow and compact like the approved DR anchor—not the exhausted broad-column candidates. Keep a simple continuous head/body contour with two ears and no mane seam or face.

Exactly four compact paws attach directly to the core. Both front paws are tiny round arcs behind the body, no taller than the rear paws. No visible arms or legs.

Carry one fully sheathed ring-pommel Tang dao behind the body. The small edge-on ring and short grip sit at the screen-right shoulder. From there, the connected sheathed weapon axis runs diagonally toward screen upper-left, as specified by the bound UL composition guide. Most of the sheath may be hidden. Do not make the weapon vertical or point it toward lower-left. Do not create disconnected pieces.

Bold outline, flat low-detail Pure Run cartoon style, centered 256×256, feet near y=236. No guqin, wineskin, gourd, exposed blade, second weapon, face on back, limb connectors, text, floor, shadow, gradients, or effects.

