# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Create one Poet Hit DR frame using the approved Hunter Hit DR only for the standard medium-chibi recoil peak and facial reaction.
First read: five-red-chow-poet, native-down-right, screen-right-hit-recoil, widened-eyes-two-tears, fully-sheathed-dao

## Frozen invariants
- Image 1 is sole Poet identity, volume, palette and style authority
- compact furry capsule with broad lion head, short muzzle, curled tail and exactly four attached paws
- no arms, elbows, leg connectors or human hands
- one compact fully sheathed ring-pommel Tang dao moves with body
- feet remain on y236 baseline

## Forbidden
- Hunter identity
- shield
- spear
- arms
- legs
- human hands
- floating paws
- missing paw
- extra paw
- bare blade
- dropped weapon
- second weapon
- staff
- blood
- impact flash
- stars
- damage numbers
- particles
- VFX
- text
- watermark
- body stretch

## Reference responsibilities
- image-1-approved-poet-idle-dr-identity-volume: Sole Poet identity, volume, anatomy, palette, tail, paws and style; not pose. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3]
- image-2-approved-hunter-hit-dr-action-only: Only recoil peak, ear inertia, widened eyes, two tear streaks and tense mouth; no Shiba identity, shield or proportions. [godot/assets/units/actions/doge_hunter_hit_dr.png @ a921486bec40be843d02efdce8617e2a1b764c33e6a9e8e4278cf76c92fa9e01]
- image-3-approved-sheathed-dao-form: Sole equipment form, length, palette and details; guide owns placement. [Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_sheathed_dr_v02.png @ eb4acedf50fe56d2c216e932a7364686de603a4df40c9bc5f1fe10c22ab55999]
- image-4-deterministic-poet-hit-guide: Sole Poet recoil geometry, paw contacts, worn-dao axis and baseline. [Tools/artworks/poet_five_red_chow/guides/poet_hit_dr_hunter_recoil_pose_guide_v1.png @ 60e3e2fb3a90d7a2d66ba497637b0eeb14e98fa9abc0e8b7723eb0335e4569cb]

## Composition
```json
{
  "actionDesign": {
    "centerOfMass": [
      139,
      179
    ],
    "compressionLine": [
      [
        101,
        189
      ],
      [
        137,
        177
      ],
      [
        177,
        159
      ]
    ],
    "counterbalanceLine": [
      [
        125,
        184
      ],
      [
        94,
        181
      ],
      [
        75,
        164
      ]
    ],
    "driveFoot": [
      155,
      235
    ],
    "lineOfAction": [
      [
        128,
        229
      ],
      [
        148,
        104
      ]
    ],
    "pawContactZones": {
      "farFoot": [
        [
          96,
          221
        ],
        [
          124,
          219
        ],
        [
          126,
          240
        ],
        [
          98,
          242
        ]
      ],
      "farHand": [
        [
          91,
          157
        ],
        [
          116,
          153
        ],
        [
          121,
          176
        ],
        [
          96,
          181
        ]
      ],
      "nearFoot": [
        [
          141,
          220
        ],
        [
          171,
          220
        ],
        [
          174,
          240
        ],
        [
          143,
          242
        ]
      ],
      "nearHand": [
        [
          169,
          154
        ],
        [
          192,
          160
        ],
        [
          190,
          184
        ],
        [
          166,
          178
        ]
      ]
    },
    "silhouette": [
      [
        87,
        204
      ],
      [
        91,
        150
      ],
      [
        114,
        111
      ],
      [
        147,
        98
      ],
      [
        177,
        111
      ],
      [
        192,
        150
      ],
      [
        189,
        202
      ],
      [
        171,
        230
      ],
      [
        141,
        237
      ],
      [
        104,
        230
      ]
    ],
    "supportFoot": [
      111,
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
      7.0,
      13.0
    ],
    "top": [
      148,
      104
    ]
  },
  "coreBbox": [
    84,
    98,
    192,
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
      "name": "bare-blade",
      "rect": [
        25,
        80,
        80,
        220
      ]
    },
    {
      "name": "impact-vfx",
      "rect": [
        190,
        70,
        255,
        190
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "bodyDynamics": "compact capsule tilts screen-right without stretching; both hind paws remain attached and contact y236 baseline; front paws flinch outward but stay broadly attached to body edge",
    "direction": "native down-right front three-quarter facing",
    "effects": "only two short facial tear streaks inherited from project Hit language; no impact flash, stars, damage numbers, blood, particles or weapon FX",
    "grip": "no active weapon grip; exactly two small front paws attached directly to body with no arms or connectors",
    "heldDao": "one original compact fully sheathed ring-pommel Tang dao remains worn diagonally across the body and tilts rigidly with the whole character; no draw, drop, duplicate or staff conversion",
    "identity": "approved Poet Idle DR only; preserve five-red Chow face, lion ruff, short muzzle, compact volume, curled tail and exact palette",
    "playerRead": "Poet has just been struck and recoils as one compact body; not casting, attacking or guarding",
    "visualMoment": "same medium chibi impact peak as approved Hunter Hit DR: rigid whole-body recoil toward screen-right, ears folded by inertia, widened eyes with tiny pupils, exactly two short blue-white tear streaks and a tense small wavy mouth"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      88,
      158,
      169,
      238
    ],
    "exitWindow": [
      88,
      158,
      163,
      232
    ],
    "guardWindow": [
      92,
      160,
      116,
      184
    ],
    "hiddenGrip": [
      125,
      199
    ],
    "maxBladeWidthPx": 18,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        92,
        164
      ],
      [
        159,
        231
      ]
    ],
    "tipMayBeOccluded": false,
    "tipRegion": [
      145,
      218,
      168,
      238
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create exactly one complete 256×256 RGBA game character sprite on a perfectly flat pure green #00FF00 background.

Image 1 is the sole identity, volume and style authority. Preserve this exact approved cinnamon-red five-red Chow Chow Poet: broad lion-like head and ruff, short wide muzzle, small upright ears, dark eyes, compact furry capsule body, high-set curled plume tail, exactly four small round paws directly attached to body, bold dark outlines, flat controlled values and restrained pixel detail. Native down-right front three-quarter view. Do not copy Idle pose.

Image 2 contributes ONLY the project-standard Hit action: medium chibi impact peak, whole rigid capsule recoils toward screen-right, ears fold with inertia, both eyes widen with tiny pupils, exactly two short blue-white tear streaks, and a tense small wavy mouth. Do not copy Shiba breed, white belly, shield, proportions, colors or equipment.

Image 3 is the sole equipment-form authority. Keep one compact completely sheathed ring-pommel Tang dao: hollow ring, short grip, guard, straight sheath and complete tip at original proportions. It remains worn diagonally across the Poet and tilts rigidly with the whole body. It is not held, drawn, dropped, duplicated or lengthened.

Image 4 is geometry only. Follow the screen-right whole-body recoil axis, four paw contact zones, diagonal worn-dao axis and feet baseline y236. Do not render guide colors, boxes, lines, arrows or labels.

NO ARMS OR LEGS. Exactly two front paws and two hind paws. Front paws flinch outward but remain broadly attached to body edge. Both hind paws remain attached and contact the same baseline. No forearms, elbows, wrists, thin connectors, human hands, floating paws, missing paws or extra paws. Do not stretch, elongate or turn the core into a pear-shaped humanoid torso.

No hat, clothing, gourd, wineskin, guqin, jewelry, bare blade, second weapon, blood, wound, impact flash, stars, damage number, motion trail, particles or spell VFX. The two short facial tear streaks are the only reaction marks. Keep full silhouette, tears, tail, paws, ring pommel and sheath tip inside canvas.
