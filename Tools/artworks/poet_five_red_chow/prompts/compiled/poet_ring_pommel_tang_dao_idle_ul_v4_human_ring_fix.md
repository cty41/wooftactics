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
        101,
        122,
        157,
        172
      ]
    },
    {
      "name": "front-layer-left-free-paw",
      "rect": [
        79,
        151,
        108,
        207
      ]
    },
    {
      "name": "front-layer-right-holding-paw",
      "rect": [
        148,
        151,
        180,
        207
      ]
    },
    {
      "name": "wrong-left-shoulder-ring",
      "rect": [
        65,
        120,
        105,
        180
      ]
    },
    {
      "name": "vertical-or-lower-scabbard",
      "rect": [
        151,
        176,
        187,
        232
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [
    "near_hand",
    "far_hand",
    "equipment"
  ],
  "screenSpaceIntent": {
    "core": "must fit the cyan narrow capsule guide, approximately 66 px wide by 110 px high after calibration",
    "direction": "native upper-left back view",
    "feet": "two compact rear paws attached at baseline",
    "freePaw": "tiny screen-left near paw arc behind core without equipment contact",
    "holdingPaw": "tiny screen-right far paw arc behind core",
    "ringPommel": "small edge-on ring fixed at screen-right shoulder",
    "scabbard": "connected axis leaves the right shoulder and points diagonally toward screen upper-left behind the body"
  },
  "visibleAreaCaps": {
    "equipment": 0.22,
    "far_hand": 0.18,
    "near_hand": 0.18
  },
  "weapon": {
    "bladeCenterline": [
      96,
      115,
      171,
      167
    ],
    "exitWindow": [
      151,
      137,
      180,
      174
    ],
    "guardWindow": [
      153,
      145,
      177,
      173
    ],
    "hiddenGrip": [
      168,
      160
    ],
    "maxBladeWidthPx": 12,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        169,
        160
      ],
      [
        105,
        125
      ]
    ],
    "tipRegion": [
      88,
      107,
      126,
      145
    ]
  }
}
```

## Unresolved fixes
- deterministic report and Reviewer
- final round deterministic report and Reviewer
- Initial semantic mask mislabeled the central lower core as tail/feet, causing false core disconnection and vertical offset.
- cty41 visual decision

## Base prompt
# Five-Red Chow Poet — Narrow-Guide Idle UL

Create one native upper-left back-view sprite on pure `#00ff00`. The cyan rounded rectangle in the bound pose guide is the exact target for the core body after calibration: narrow, compact, about 66 wide by 110 high. Keep the body inside that proportion. Do not turn it into a broad egg or column.

Preserve the approved five-red chow identity, orange palette, two ears, modest fluffy tail, simple continuous back contour, and no face or mane seam. Show exactly four compact directly attached paws. Both front paws are tiny circular arcs behind the core, smaller than the rear paws and never vertically elongated. No arms or legs.

The pose-guide white diagonal is mandatory: its right/lower endpoint is the small edge-on ring and short grip at the screen-right shoulder; the connected fully sheathed dao extends behind the body toward the left/upper endpoint. Most of the sheath may be hidden. No left-shoulder ring, vertical weapon, lower-left axis, exposed blade, second weapon, or disconnected fragment.

Bold outline, flat low-detail Pure Run style, centered 256×256, feet near y=236. No guqin, wineskin, gourd, face on back, limb connectors, text, floor, shadow, gradients, or effects.

