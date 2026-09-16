# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Create the native up-left/back three-quarter counterpart of the human-approved unified Poet Cast DR moment. The short fully sheathed ring-pommel Tang dao and both paws are in front of the chest in world space and therefore behind the capsule body from the rear camera.
First read: five-red-chow-poet-back-view, native-up-left, firm-vertical-cast-brace, rear-layer-paws-and-sheathed-dao

## Frozen invariants
- Image 1 is the sole native UL identity, volume, anatomy, palette and style authority
- compact cinnamon-red furry capsule; back of broad lion head and two small upright ears; high-set curled plume tail
- exactly four small round paws directly attached to body; no arms, elbows, leg connectors or human hands
- one completely sheathed compact ring-pommel Tang dao at approved sword/body ratio
- feet centered at x128 on y236 baseline
- no face turn toward camera, props, clothing, bare blade or VFX

## Forbidden
- front-layer paws
- front-layer equipment
- arms
- forearms
- elbows
- human hands
- floating paws
- missing paw
- extra paw
- staff-length weapon
- grounded sheath tip
- bare blade
- second weapon
- face toward camera
- forehead highlights
- pear torso
- hat
- clothing
- gourd
- wineskin
- guqin
- glow
- runes
- VFX
- text
- watermark
- guide marks

## Reference responsibilities
- image-1-approved-idle-ul-identity-volume: Sole UL identity, volume, rear anatomy, palette, tail, four paws and style; not pose or equipment placement. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_ul_v07.png @ 519e288ff7f4ea2cb35de9393b4163a13c1fbfdca3b8fb5f83eff81733eec512]
- image-2-approved-cast-dr-action-moment-only: Only the firm vertical Cast hold moment and fully sheathed equipment state. Do not copy identity, arm, long weapon, grounded tip or grip geometry. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_cast_dr_vertical_guard_grip_v01.png @ 7cd072d554222d24c1cc4b357f3220260c01016c47be51d21c34e045d4139883]
- image-3-approved-sheathed-dao-form: Sole ring-pommel Tang dao form, original length, palette and details; geometry and depth belong to Image 4. [Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_sheathed_dr_v02.png @ eb4acedf50fe56d2c216e932a7364686de603a4df40c9bc5f1fe10c22ab55999]
- image-4-deterministic-cast-ul-guide: Sole native UL placement, vertical axis, guard-throat grip, y236 baseline and rear-layer occlusion geometry. [Tools/artworks/poet_five_red_chow/guides/poet_cast_ul_vertical_guard_grip_pose_guide_v1.png @ c633fd8742ecf2b3db7d7d046ded8d64185009d3f0633cc77c76a1e08b58f8c1]

## Composition
```json
{
  "actionDesign": {
    "centerOfMass": [
      128,
      179
    ],
    "compressionLine": [
      [
        151,
        194
      ],
      [
        128,
        178
      ],
      [
        98,
        147
      ]
    ],
    "counterbalanceLine": [
      [
        139,
        183
      ],
      [
        170,
        174
      ],
      [
        192,
        157
      ]
    ],
    "driveFoot": [
      153,
      235
    ],
    "lineOfAction": [
      [
        128,
        229
      ],
      [
        122,
        101
      ]
    ],
    "pawContactZones": {
      "farFoot": [
        [
          142,
          221
        ],
        [
          171,
          221
        ],
        [
          171,
          241
        ],
        [
          142,
          241
        ]
      ],
      "farHand": [
        [
          80,
          148
        ],
        [
          101,
          147
        ],
        [
          103,
          170
        ],
        [
          82,
          172
        ]
      ],
      "nearFoot": [
        [
          94,
          220
        ],
        [
          123,
          220
        ],
        [
          123,
          241
        ],
        [
          94,
          241
        ]
      ],
      "nearHand": [
        [
          78,
          128
        ],
        [
          101,
          126
        ],
        [
          103,
          151
        ],
        [
          81,
          153
        ]
      ]
    },
    "silhouette": [
      [
        85,
        205
      ],
      [
        85,
        150
      ],
      [
        101,
        111
      ],
      [
        123,
        97
      ],
      [
        159,
        101
      ],
      [
        187,
        130
      ],
      [
        189,
        201
      ],
      [
        174,
        231
      ],
      [
        144,
        236
      ],
      [
        104,
        232
      ]
    ],
    "supportFoot": [
      105,
      235
    ]
  },
  "bodyLayer": false,
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      128,
      229
    ],
    "tiltDegrees": [
      -6.0,
      -2.0
    ],
    "top": [
      122,
      101
    ]
  },
  "coreBbox": [
    83,
    97,
    189,
    232
  ],
  "equipmentState": {
    "blade": "fully-sheathed",
    "scabbard": "present",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [
    {
      "name": "staff-length-upper",
      "rect": [
        80,
        0,
        111,
        70
      ]
    },
    {
      "name": "grounded-sheath-tip",
      "rect": [
        76,
        229,
        113,
        255
      ]
    },
    {
      "name": "wrong-front-layer",
      "rect": [
        110,
        128,
        172,
        195
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "bodyDynamics": "compact near-upright back-facing capsule with slight forward brace; both hind paws grounded at y236",
    "direction": "native up-left back three-quarter facing",
    "effects": "none",
    "grip": "two front paws stack immediately below guard around sheath throat, both behind core; only small outer arcs attached to the left body edge may remain visible; no arms or connectors",
    "heldDao": "approved sword/body ratio; ring pommel above guard at screen-left shoulder edge; sheath descends behind body; only outer strips and lower tip may emerge; tip remains above ground",
    "identity": "approved Idle UL only; preserve back of head, ears, tail, compact volume, native UL depth and exactly four attached paws",
    "playerRead": "Poet holds one short fully sheathed Tang dao vertically in front of the chest in world space; from the rear camera the weapon and both front paws sit behind the capsule body",
    "visualMoment": "same firm vertical Cast hold as approved Cast DR, redrawn natively from the approved Idle UL identity"
  },
  "visibleAreaCaps": {
    "equipment": 0.35,
    "far_hand": 0.08,
    "near_hand": 0.22
  },
  "weapon": {
    "bladeCenterline": [
      87,
      84,
      104,
      219
    ],
    "exitWindow": [
      78,
      112,
      108,
      178
    ],
    "guardWindow": [
      79,
      111,
      110,
      134
    ],
    "hiddenGrip": [
      96,
      144
    ],
    "maxBladeWidthPx": 18,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        96,
        87
      ],
      [
        94,
        217
      ]
    ],
    "tipMayBeOccluded": false,
    "tipRegion": [
      82,
      202,
      106,
      224
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create one complete 256×256 RGBA game character sprite on a perfectly flat pure green #00FF00 background.

Image 1 is the sole character authority. Preserve this exact approved native up-left/back three-quarter five-red Chow Chow Poet: compact cinnamon-red furry capsule, back of broad lion-like head, two small upright ears, high-set curled plume tail, bold dark outlines, flat controlled values, restrained detail, exactly four small round paws directly attached to body, and y236 foot baseline. Redraw a Cast pose, not Idle.

Image 2 contributes ONLY the approved firm vertical Cast hold moment and the fully sheathed equipment state. Do not copy its face, front view, long arm, generic body, weapon length, grounded tip, highlights or grip error.

Image 3 is the sole equipment-form authority. Preserve one compact completely sheathed ring-pommel Tang dao at its original sword/body ratio: hollow ring, short grip, guard, sheath mouth, straight sheath and complete tip. It is not a staff.

Image 4 is the sole geometry and depth authority. Native up-left rear three-quarter view. The Poet holds the short sheathed dao vertically in front of its chest in world space; therefore from the rear camera the entire weapon and both front paws are BEHIND the opaque capsule body. The body must occlude the weapon middle and paw interiors. Only the ring and guard near the screen-left shoulder edge, tiny paw outer arcs attached to the left body edge, limited sheath edge strips and lower sheath tip may emerge. Both paws stack immediately below the guard around the sheath throat. The tip remains visibly above ground. Do not render guide colors or marks.

NO ARMS OR LEGS. Exactly two front paws and two hind paws. No forearms, elbows, wrists, thin connectors or human hands. Rear-layer paws may be mostly hidden but their small visible arcs must touch the body edge broadly. Keep the capsule core continuous; no equipment or paw shape may draw across its middle.

Firm slight forward brace, dignified unified Cast release-hold, no facial turn toward camera. No hat, clothing, gourd, wineskin, guqin, jewelry, bare blade, second weapon, staff-length extension, grounded sheath tip, glow, runes, particles, VFX, text or watermark. Keep full silhouette inside canvas and both hind paws grounded at y236.
