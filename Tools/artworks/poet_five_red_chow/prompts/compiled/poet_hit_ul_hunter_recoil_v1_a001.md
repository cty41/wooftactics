# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Create the native up-left/back three-quarter counterpart of the approved Poet Hit DR recoil, with only anatomically visible facial reaction and the worn sheathed dao behind the core.
First read: five-red-chow-poet-back-view, native-up-left, screen-left-hit-recoil, one-visible-eye-one-short-tear, rear-layer-sheathed-dao

## Frozen invariants
- Image 1 is sole Poet UL identity, volume, palette and style authority
- compact rear-view furry capsule with curled tail and exactly four attached paws
- no arms, elbows, leg connectors or human hands
- one compact fully sheathed ring-pommel Tang dao behind core
- feet on y236 baseline

## Forbidden
- full front face
- two visible eyes
- two tears
- front-layer weapon
- active grip
- arms
- legs
- human hands
- floating paw
- missing paw
- extra paw
- bare blade
- second weapon
- blood
- impact flash
- stars
- numbers
- particles
- VFX
- text
- watermark
- body stretch

## Reference responsibilities
- image-1-approved-poet-idle-ul-identity-volume: Sole UL identity, rear anatomy, compact volume, palette, paws, tail and style; not pose. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_ul_v07.png @ 519e288ff7f4ea2cb35de9393b4163a13c1fbfdca3b8fb5f83eff81733eec512]
- image-2-approved-poet-hit-dr-moment-only: Only frozen Hit intensity and equipment state; do not copy front face, tears, volume, grip or style defects. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_hit_dr_hunter_recoil_v01.png @ 4d8b87f7407295f9f6c166086acde62d7976393de172a7061fbff77909edb92d]
- image-3-approved-hunter-hit-ul-direction-only: Only canonical screen-left UL recoil and anatomical visibility; no Shiba identity or shield. [godot/assets/units/actions/doge_hunter_hit_ul.png @ 0eb88380ebee80e99d1208b0adf5dca6b91ed368c94039ae298b9c4bb0c297af]
- image-4-deterministic-poet-hit-ul-guide: Sole Poet screen geometry, paw contacts, rear-layer equipment and baseline. [Tools/artworks/poet_five_red_chow/guides/poet_hit_ul_hunter_recoil_pose_guide_v1.png @ f54eab6d7fbae45e88ef484147cb8af6bc9f37dc1118188d637782099afee549]

## Composition
```json
{
  "actionDesign": {
    "centerOfMass": [
      117,
      179
    ],
    "compressionLine": [
      [
        91,
        188
      ],
      [
        117,
        178
      ],
      [
        161,
        160
      ]
    ],
    "counterbalanceLine": [
      [
        130,
        183
      ],
      [
        167,
        180
      ],
      [
        192,
        165
      ]
    ],
    "driveFoot": [
      105,
      235
    ],
    "lineOfAction": [
      [
        128,
        229
      ],
      [
        108,
        104
      ]
    ],
    "pawContactZones": {
      "farFoot": [
        [
          141,
          220
        ],
        [
          171,
          220
        ],
        [
          173,
          241
        ],
        [
          143,
          242
        ]
      ],
      "farHand": [
        [
          164,
          155
        ],
        [
          189,
          160
        ],
        [
          188,
          181
        ],
        [
          163,
          176
        ]
      ],
      "nearFoot": [
        [
          94,
          220
        ],
        [
          123,
          219
        ],
        [
          125,
          241
        ],
        [
          96,
          242
        ]
      ],
      "nearHand": [
        [
          78,
          155
        ],
        [
          103,
          151
        ],
        [
          107,
          176
        ],
        [
          82,
          181
        ]
      ]
    },
    "silhouette": [
      [
        82,
        201
      ],
      [
        83,
        149
      ],
      [
        92,
        116
      ],
      [
        108,
        99
      ],
      [
        144,
        101
      ],
      [
        178,
        124
      ],
      [
        190,
        167
      ],
      [
        188,
        206
      ],
      [
        171,
        232
      ],
      [
        143,
        238
      ],
      [
        104,
        231
      ]
    ],
    "supportFoot": [
      151,
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
      -13.0,
      -7.0
    ],
    "top": [
      108,
      104
    ]
  },
  "coreBbox": [
    82,
    98,
    190,
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
      "name": "full-front-face",
      "rect": [
        125,
        103,
        190,
        166
      ]
    },
    {
      "name": "impact-vfx",
      "rect": [
        0,
        55,
        65,
        185
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "bodyDynamics": "compact capsule leans screen-left without stretching; both hind paws remain attached and on y236; front paws flinch at body edges without arms",
    "direction": "native up-left back three-quarter facing",
    "effects": "one short tear streak from the one visible eye only; no impact flash, stars, numbers, blood, particles or VFX",
    "grip": "no active grip; both front paws remain attached to body; no arms or connectors",
    "heldDao": "one compact fully sheathed ring-pommel Tang dao remains worn on the far/front side of the torso and therefore behind the core; only ring/outer sheath fragments may appear at the screen-right edge",
    "identity": "approved Idle UL only; mostly back of head and body, native rear depth, tail and compact volume; never turn into full front face",
    "playerRead": "Poet is struck and recoils as one body from the rear three-quarter view",
    "visualMoment": "same frozen Hit peak as approved Poet Hit DR and canonical Hunter Hit UL: whole compact body recoils toward screen-left, ears trail with inertia, only the anatomically visible near eye widens with a tiny pupil and produces one short blue-white tear streak, with a tiny tense mouth sliver"
  },
  "visibleAreaCaps": {
    "equipment": 0.25
  },
  "weapon": {
    "bladeCenterline": [
      151,
      126,
      180,
      224
    ],
    "exitWindow": [
      151,
      126,
      190,
      204
    ],
    "guardWindow": [
      158,
      125,
      186,
      151
    ],
    "hiddenGrip": [
      150,
      183
    ],
    "maxBladeWidthPx": 18,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        176,
        136
      ],
      [
        161,
        216
      ]
    ],
    "tipMayBeOccluded": true,
    "tipRegion": [
      151,
      196,
      180,
      224
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create exactly one complete 256×256 RGBA game character sprite on a perfectly flat pure green #00FF00 background.

Image 1 is the sole identity, volume and style authority. Preserve this exact approved native up-left/back three-quarter cinnamon-red five-red Chow Chow Poet: compact furry capsule, back of broad lion-like head and ruff, two small ears, high-set curled plume tail, exactly four small round paws directly attached to body, bold dark outlines, flat controlled values and restrained pixel detail. Redraw a Hit pose, not Idle.

Image 2 contributes ONLY the already accepted Poet Hit DR impact timing and intensity: one rigid-body recoil, ear inertia and tense startled reaction. Do not copy its front face, two visible eyes, two tears, body proportions, active-looking sheath grip, oversized paws, smooth style or other known deviations.

Image 3 contributes ONLY canonical native up-left Hit projection: the body recoils toward screen-left and only anatomically visible facial features react. Do not copy Shiba breed, white belly, shield, proportions, colors or equipment.

Image 4 is the sole screen geometry and depth authority. Follow the screen-left whole-body axis, paw contact zones, baseline y236 and behind-core sheathed-dao placement. Do not render guide colors, boxes, lines, arrows or labels.

Show mostly the back of the Poet. Only ONE anatomically visible near eye may appear at the left profile edge: widened white with a tiny pupil. Draw exactly ONE short thin blue-white tear streak from that eye and only a tiny tense mouth sliver. No full front face, second eye or second tear.

The one compact fully sheathed ring-pommel Tang dao remains worn on the far/front side of the torso, so from the rear camera it is BEHIND the opaque capsule core. The body occludes nearly all of it; at most show a small hollow ring and limited sheath fragments at the screen-right edge. No active paw grip, bare blade, drop, duplicate, staff length or weapon crossing the body front.

NO ARMS OR LEGS. Exactly two front paws and two hind paws. Front paws flinch at body edges but remain broadly attached. Both hind paws stay attached and on y236. No forearms, elbows, wrists, thin connectors, human hands, floating paws, missing paws or extra paws. Keep the core compact and continuous without stretching.

No hat, clothing, gourd, wineskin, guqin, jewelry, blood, wound, impact flash, stars, damage number, motion trail, particles, spell VFX, text or watermark. The single short tear is the only reaction mark. Keep the full silhouette inside canvas.
