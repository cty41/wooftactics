# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Create the universal static Death sprite for the five-red Chow Poet using the approved Hunter capsule death geometry and one detached fully sheathed dao.
First read: dead-five-red-chow-poet, supine-short-thick-capsule, head-upper-right, four-attached-paws, detached-sheathed-dao

## Frozen invariants
- Image 1 alone owns Poet identity, core volume, palette and style
- broad ruff, short muzzle, small ears and curled tail
- body ears and exactly four attached paws form one continuous generated soft-body silhouette
- no arms, legs, hands or connectors
- one completely sheathed ring-pommel Tang dao detached below corpse
- no wine vessel, guqin, bare blade or FX

## Forbidden
- standing
- sleeping expression
- side curl
- arched body
- segmented rotation
- arms
- legs
- human hands
- floating paw
- missing paw
- extra paw
- worn or held weapon
- bare blade
- second weapon
- shield
- blood
- wound
- tears
- impact flash
- stars
- numbers
- particles
- VFX
- text
- watermark

## Reference responsibilities
- image-1-approved-poet-identity-volume: Sole Poet identity, breed, compact core volume, palette, ruff, tail, four-paw topology and style; not pose or worn weapon location. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3]
- image-2-approved-hunter-death-pose-only: Only supine Death axis, head direction, short-thick flattening, X-eye and paw-splay language; no Shiba identity or shield. [Tools/artworks/doge/calibrated/doge_capsule_hunter_death_color_v04.png @ f5997651edd71cc78b926d5824ef4ac178f28604f5d7c9846f5a81ee0a2b3d19]
- image-3-approved-sheathed-dao-form: Only one complete fully sheathed ring-pommel Tang dao form; it is detached and never held. [Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_sheathed_dr_v02.png @ eb4acedf50fe56d2c216e932a7364686de603a4df40c9bc5f1fe10c22ab55999]
- image-4-deterministic-poet-death-guide: Sole screen geometry, corpse axis, four paw zones, detached dao axis and overall AABB planning. [Tools/artworks/poet_five_red_chow/guides/poet_death_hunter_horizontal_pose_guide_v1.png @ 5ede27b5a7b8635848dcff27da80e91aa97e07bb718372ce4379352e6efb6a29]

## Composition
```json
{
  "actionDesign": {
    "centerOfMass": [
      128,
      158
    ],
    "compressionLine": [
      [
        83,
        169
      ],
      [
        128,
        156
      ],
      [
        174,
        137
      ]
    ],
    "counterbalanceLine": [
      [
        101,
        194
      ],
      [
        137,
        176
      ],
      [
        176,
        153
      ]
    ],
    "driveFoot": [
      151,
      125
    ],
    "lineOfAction": [
      [
        89,
        188
      ],
      [
        165,
        117
      ]
    ],
    "pawContactZones": {
      "farFoot": [
        [
          94,
          191
        ],
        [
          116,
          187
        ],
        [
          125,
          203
        ],
        [
          103,
          207
        ]
      ],
      "farHand": [
        [
          115,
          129
        ],
        [
          135,
          118
        ],
        [
          148,
          133
        ],
        [
          128,
          147
        ]
      ],
      "nearFoot": [
        [
          76,
          169
        ],
        [
          98,
          172
        ],
        [
          101,
          192
        ],
        [
          79,
          193
        ]
      ],
      "nearHand": [
        [
          141,
          151
        ],
        [
          164,
          139
        ],
        [
          177,
          155
        ],
        [
          156,
          170
        ]
      ]
    },
    "silhouette": [
      [
        70,
        180
      ],
      [
        75,
        145
      ],
      [
        97,
        124
      ],
      [
        127,
        108
      ],
      [
        160,
        112
      ],
      [
        183,
        135
      ],
      [
        184,
        166
      ],
      [
        167,
        190
      ],
      [
        133,
        202
      ],
      [
        96,
        201
      ]
    ],
    "supportFoot": [
      91,
      185
    ]
  },
  "bodyLayer": false,
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      89,
      188
    ],
    "tiltDegrees": [
      43.0,
      49.0
    ],
    "top": [
      165,
      117
    ]
  },
  "coreBbox": [
    73,
    105,
    182,
    202
  ],
  "equipmentState": {
    "blade": "fully-sheathed",
    "scabbard": "present",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    165
  ],
  "forbiddenRegions": [
    {
      "name": "standing-zone",
      "rect": [
        108,
        40,
        151,
        91
      ]
    },
    {
      "name": "impact-vfx",
      "rect": [
        14,
        69,
        60,
        193
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "bodyDynamics": "one continuous generated soft-body outline containing body, ears and exactly four broadly attached paws; no standing baseline, no segmented rotation, no curled or arched torso",
    "direction": "universal capsule death, chest and face upward, head toward screen upper-right",
    "effects": "no tears, blood, wound, impact mark, stars, particles, aura or death VFX",
    "grip": "no paw touches or grips the dao; there are no arms, legs or connectors",
    "heldDao": "one compact completely sheathed ring-pommel Tang dao is fully detached and lies below the corpse; hollow ring at screen-left and complete sheath tip at screen-right",
    "identity": "approved five-red Poet appearance and compact volume only; preserve broad ruff, short muzzle, cinnamon-red coat, small ears and curled tail while rebuilding a native death silhouette",
    "playerRead": "the five-red Chow Poet is unmistakably dead and fully grounded, not sleeping, attacking or merely hit",
    "visualMoment": "final still death pose: straight short thick capsule lies on its back along the lower-left to upper-right axis; eyes use simple X marks, mouth slack and small"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      80,
      206,
      178,
      221
    ],
    "exitWindow": [
      70,
      198,
      185,
      230
    ],
    "guardWindow": [
      91,
      198,
      121,
      223
    ],
    "hiddenGrip": [
      104,
      211
    ],
    "maxBladeWidthPx": 17,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        80,
        206
      ],
      [
        178,
        221
      ]
    ],
    "tipMayBeOccluded": false,
    "tipRegion": [
      162,
      204,
      187,
      229
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create exactly one complete 256×256 RGBA game character Death sprite on a perfectly flat pure green #00FF00 background.

Image 1 is the sole identity, core-volume, palette and style authority. Preserve this exact cinnamon-red five-red Chow Chow Poet: broad lion-like ruff, short wide muzzle, small ears, compact furry capsule, high-set curled plume tail, exactly four small round paws, bold dark outlines, flat controlled values and restrained pixel detail. Redraw a native death pose; do not rotate or squash the standing sprite and do not keep its worn weapon placement.

Image 2 contributes ONLY the approved capsule Death language: a straight short-thick body lying supine, chest and face upward, head toward screen upper-right, long axis lower-left to upper-right, simple X eyes, small slack mouth, ears and four paws collapsed naturally but attached. Do not copy Shiba identity, white belly, shield, colors or exact proportions.

Image 3 is the sole equipment-form authority. Draw exactly one compact complete fully sheathed ring-pommel Tang dao with hollow ring, short grip, guard, straight sheath and complete tip. It is fully detached from every paw and lies below the corpse: ring pommel at screen-left, sheath tip at screen-right. No bare blade and no duplicate.

Image 4 is the sole screen geometry authority. Follow its corpse axis, short-thick silhouette, four attached paw zones and detached dao axis. Do not render guide colors, boxes, lines, dots, arrows or labels.

The body, ears and all four paws form ONE continuous naturally generated soft-body silhouette. Exactly two front paws and two hind paws remain broadly attached to the body. NO ARMS OR LEGS, elbows, wrists, thin connectors, human hands, floating paws, missing paws or extra paws. Keep the corpse flat, straight, short and thick—never standing, sleeping curled on its side, arched, segmented or elongated.

Make the body unmistakably dead with two simple X eyes and a small slack mouth. No tears. No gourd, wineskin, guqin, hat, clothing, jewelry, shield, second weapon, blood, wound, impact flash, stars, damage numbers, aura, particles, smoke, spell or death VFX, text or watermark. Keep the entire corpse, tail, paws, ring pommel and sheath tip safely inside canvas.
