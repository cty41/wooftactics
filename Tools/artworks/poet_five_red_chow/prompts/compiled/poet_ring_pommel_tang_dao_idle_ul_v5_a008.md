# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color

## Reference responsibilities
- approved_ul_body_direction: Tools/artworks/pipeline/artifacts/job-9a531767f319b957/job-9a531767f319b957-a001/calibrated.png @ 9c37f61930aefd4aac08ad6f82a2e1ff6282faf90ca5f0ed42dbfb3e4d29a387
- equipped_dr_authority: Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3
- pose_guide: Tools/artworks/poet_five_red_chow/guides/poet_ring_pommel_tang_dao_idle_ul_pose_guide_v7.png @ 35d0ed9fbf7f5c0532bd7c39859595dc67ad7e2f779b6f13605edb0fd02ce7c9

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
    "direction": "tail-right face-left native upper-left rear three-quarter",
    "faceCue": "one tiny eye and minimal nose edge only on screen-left",
    "feet": "two compact rear paws attached at baseline",
    "freePaw": "tiny screen-left near paw arc behind core without equipment contact",
    "holdingPaw": "tiny screen-right far paw arc behind core",
    "ringPommel": "old approved small ring at screen-right shoulder",
    "scabbard": "connected fully sheathed axis from right shoulder toward screen upper-left behind body",
    "tail": "tail mass on screen-right"
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
- correct the whole weapon endpoint axis without changing the character
- restore exactly four paws; move and shrink weapon behind core
- human decision between best whole candidate and deterministic component assembly
- move tail to the visual foreground without changing shape or position
- remove body contour over tail root and draw tail root in front
- tail-root foreground overlap only

## Base prompt
# Confirmed-source Equipped Poet Idle UL

Primary source is the formally approved equipped Idle DR v03: preserve its exact poet identity, compact directly attached four-paw topology, original Tang dao grip/ring design, and relaxed carried relationship. Reproject only the direction.

UL direction source is the approved body component: tail on screen-right, head turns toward screen-left, one tiny left eye and minimal nose edge, asymmetric ears, 68×110 narrow capsule.

In the final UL sprite, exactly one old-design fully sheathed Tang dao sits behind the core. Its small ring/grip is at the screen-right shoulder and its connected sheath axis extends toward screen upper-left. No regenerated grip ornament, spiral pattern, second weapon, exposed blade, disconnected fragment, guqin, wineskin, or gourd.

