# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Retry Poet Death with the confirmed Hunter capsule pose and Demonbound-like close-body dropped-weapon spacing while keeping a fully sheathed compact Poet dao.
First read: dead-five-red-chow-poet, short-thick-supine-capsule, head-upper-right, four-attached-paws, close-detached-sheathed-dao

## Frozen invariants
- Image 1 solely owns Poet identity, volume, palette and style
- body ears and exactly four attached paws form one continuous generated silhouette
- one complete fully sheathed ring-pommel Tang dao detached but tucked directly against/partly under lower corpse
- no arms, legs, active grip, bare blade, wine vessel, guqin or FX

## Forbidden
- standing
- side curl
- bean body
- arched core
- missing or floating paw
- arms
- legs
- human hands
- active grip
- large green gap between corpse and dao
- long thin staff-like dao
- bare blade
- second weapon
- shield
- blood
- wound
- tears
- stars
- particles
- VFX
- text
- watermark

## Reference responsibilities
- image-1-approved-poet-identity-volume: Sole Poet identity, compact volume, palette, ruff, tail, four-paw topology and style; not standing pose. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3]
- image-2-approved-hunter-death-pose-only: Only straight short-thick supine Death axis, head direction, X-eye and attached-paw language; no Shiba identity or shield. [Tools/artworks/doge/calibrated/doge_capsule_hunter_death_color_v04.png @ f5997651edd71cc78b926d5824ef4ac178f28604f5d7c9846f5a81ee0a2b3d19]
- image-3-approved-sheathed-dao-form: Only compact complete fully sheathed ring-pommel dao form. [Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_sheathed_dr_v02.png @ eb4acedf50fe56d2c216e932a7364686de603a4df40c9bc5f1fe10c22ab55999]
- image-4-approved-demonbound-death-spacing-only: Only close-body detached weapon spacing and slight overlap; never body pose, identity, colors, bare blade, sword form or proportions. [Tools/artworks/doge/calibrated/doge_capsule_demonbound_death_v01.png @ 90218c756be1a4e3a40d8221667536f53d665447c24cca597f5f01eef0b462a0]
- image-5-deterministic-poet-death-v2-guide: Sole final screen geometry, corpse axis, four paw zones and compact close-dao axis. [Tools/artworks/poet_five_red_chow/guides/poet_death_hunter_close_dao_pose_guide_v2.png @ 0936f526aa766f5636675718194f96c7b8a0c87966b11cd8839468ccf5f21e05]

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
    158
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
    "bodyDynamics": "one continuous generated body-ear-four-paw silhouette; never curled, arched, standing or segmented",
    "direction": "universal capsule death, chest and face upward, head toward screen upper-right",
    "effects": "no bare blade, blood, wounds, tears, stars, particles or death VFX",
    "grip": "no paw grips the dao; slight corpse overlap proves ground contact while the complete sheathed form remains readable",
    "heldDao": "one compact completely sheathed ring-pommel Tang dao is detached but tucked immediately against and partly beneath the lower corpse contour, following Demonbound Death placement compactness only; ring left, sheath tip right, no large green gap",
    "identity": "approved Poet only; Hunter owns body pose; Demonbound owns close dropped-weapon spacing only",
    "playerRead": "five-red Chow Poet is unmistakably dead and fully grounded",
    "visualMoment": "same final still Death as v1: straight short thick capsule lies supine along lower-left to upper-right, with X eyes and a small slack mouth"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      81,
      188,
      167,
      203
    ],
    "exitWindow": [
      72,
      181,
      177,
      211
    ],
    "guardWindow": [
      91,
      181,
      117,
      205
    ],
    "hiddenGrip": [
      102,
      192
    ],
    "maxBladeWidthPx": 19,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        81,
        188
      ],
      [
        167,
        203
      ]
    ],
    "tipMayBeOccluded": true,
    "tipRegion": [
      150,
      190,
      177,
      213
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create exactly one complete 256×256 RGBA game character Death sprite on perfectly flat pure green #00FF00.

Image 1 is the sole Poet identity, compact core volume, palette and style authority. Preserve its cinnamon-red five-red Chow breed, broad dense ruff, short muzzle, small ears, curled plume tail, bold dark outlines, flat values and exactly four small round paws. Redraw a native Death; never rotate the standing sprite.

Image 2 contributes ONLY the approved body pose: straight short-thick supine capsule, chest/face upward, head toward screen upper-right, X eyes, small slack mouth, ears and exactly four paws collapsed but broadly attached. Never copy Shiba identity, white belly or shield.

Image 3 is the sole equipment-form authority: exactly one compact complete fully sheathed ring-pommel Tang dao with hollow ring, short grip, guard, thick straight sheath and complete tip. No blade is visible.

Image 4 contributes ONLY close dropped-weapon spacing: the detached weapon is tucked immediately against and slightly beneath the corpse lower edge, eliminating the green gap and keeping a compact combined footprint. Never copy Demonbound identity, body, colors, proportions or its bare sword.

Image 5 is sole final geometry. Follow its corpse axis, paw zones and shorter close-dao axis. Do not render guide marks.

Keep body, ears and exactly two front paws plus two hind paws as ONE continuous generated silhouette. NO ARMS OR LEGS, connectors, floating or missing paws. The corpse must be flat, straight, short and thick, not standing, curled, bean-shaped, arched or elongated.

The fully detached sheathed dao touches and partly passes beneath the lower corpse contour; no paw grips it. Ring is screen-left, complete sheath tip screen-right. Make it about 30% shorter and visibly thicker than the long thin weapon in the previous failed candidate; it must read as a compact sheathed sword, never a staff.

No wine vessel, guqin, clothing, shield, bare blade, second weapon, blood, wounds, tears, stars, impact marks, numbers, particles, aura, smoke, death FX, text or watermark. Keep everything inside canvas.
