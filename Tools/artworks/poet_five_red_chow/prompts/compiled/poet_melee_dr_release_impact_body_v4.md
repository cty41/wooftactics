# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Create one equipment-free body component for the Poet Melee DR Release-to-Impact peak. Prove that dynamic commitment, the upright capsule topology, directly attached paws and approved flat value structure can coexist before any weapon assembly.
First read: five-red-chow-poet, upright-leaning-capsule, committed-down-right-body-action, four-paws-overlapping-capsule, equipment-free-body-component

## Frozen invariants
- Image 1 is the sole identity, volume, palette, outline and value-structure authority
- cinnamon-red five-red native Chow Chow with broad lion-like head, short wide muzzle, small upright triangular ears, two small dark eyes, restrained mouth and thick curled tail
- one upright compact furry capsule whose height remains greater than width even while the whole mass leans forward-right
- exactly four small round paws overlap the capsule boundary at the four Image 2 contact zones; no limbs, arms, legs, shoulders, hips, elbows, knees, ankles, human hands, gaps or thin connectors
- two front paws remain small body-edge paw pads clustered at the future grip corridor; two hind paws remain small body-bottom paw pads
- body value grouping matches Image 1: few flat discrete fur values, restrained simple shadow shapes and bold dark outlines
- no smooth body gradient, airbrush shadow, volumetric lighting, glossy highlight patch, extra forehead highlight or chest/belly modeling
- the body component contains no sword, handle, guard, blade, scabbard, clothing, hat, gourd, wineskin, guqin, jewelry or VFX
- no scabbard anywhere

## Forbidden
- horizontal quadruped torso
- realistic animal back
- shoulder
- hip
- arm
- forearm
- elbow
- leg
- thigh
- shin
- knee
- ankle
- human hand
- floating paw
- separated paw
- thin connector
- extra paw
- missing paw
- oversized fist
- upright Idle read
- smooth gradient
- airbrush shading
- volumetric light
- specular body highlight
- extra forehead highlight
- chest highlight
- belly modeling
- sword
- handle
- guard
- blade
- scabbard
- clothing
- hat
- gourd
- wineskin
- guqin
- jewelry
- VFX
- text
- watermark
- guide marks
- cropped silhouette

## Reference responsibilities
- image-1-approved-poet-identity-flat-values: Sole identity, native DR face, compact volume, cinnamon-red palette, curled tail, bold outline, sparse flat shading and direct-attached paw style. Ignore its Idle pose and all visible equipment. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3]
- image-2-deterministic-capsule-paw-contact-guide: Action-body geometry only: complete upright leaning capsule, line of action, compression, attached counterbalance and exactly four paw/body overlap polygons. Never render guide colors, lines, boxes or circles. [Tools/artworks/poet_five_red_chow/guides/poet_melee_dr_release_impact_body_pose_guide_v4.png @ 0440642e2b76e638b214a942d20dc7e74a127ded56c800d5dd5154ce5feaeda8]

## Composition
```json
{
  "actionDesign": {
    "centerOfMass": [
      139,
      177
    ],
    "compressionLine": [
      [
        104,
        201
      ],
      [
        139,
        177
      ],
      [
        164,
        160
      ]
    ],
    "counterbalanceLine": [
      [
        118,
        171
      ],
      [
        91,
        148
      ],
      [
        72,
        130
      ]
    ],
    "lineOfAction": [
      [
        108,
        229
      ],
      [
        153,
        108
      ]
    ],
    "pawContactZones": {
      "farFoot": [
        [
          137,
          216
        ],
        [
          161,
          218
        ],
        [
          162,
          237
        ],
        [
          138,
          237
        ]
      ],
      "farHand": [
        [
          142,
          163
        ],
        [
          164,
          163
        ],
        [
          165,
          183
        ],
        [
          143,
          181
        ]
      ],
      "nearFoot": [
        [
          91,
          216
        ],
        [
          116,
          216
        ],
        [
          118,
          237
        ],
        [
          92,
          237
        ]
      ],
      "nearHand": [
        [
          153,
          151
        ],
        [
          178,
          156
        ],
        [
          176,
          177
        ],
        [
          151,
          172
        ]
      ]
    },
    "silhouette": [
      [
        91,
        218
      ],
      [
        91,
        157
      ],
      [
        106,
        116
      ],
      [
        128,
        96
      ],
      [
        153,
        103
      ],
      [
        173,
        128
      ],
      [
        181,
        169
      ],
      [
        174,
        211
      ],
      [
        153,
        231
      ],
      [
        111,
        232
      ]
    ]
  },
  "bodyLayer": true,
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      113,
      228
    ],
    "tiltDegrees": [
      12.0,
      23.0
    ],
    "top": [
      153,
      107
    ]
  },
  "coreBbox": [
    90,
    96,
    181,
    232
  ],
  "equipmentState": {
    "blade": "absent",
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
    "bodyDynamics": "shift the whole capsule mass forward-right and compress its lower-left side; do not describe or draw support legs, drive legs, thighs, shins, shoulders or hips",
    "capsule": "one upright compact capsule whose height remains greater than width; the whole capsule leans forward-right without becoming a horizontal quadruped torso",
    "counterbalance": "curled tail/rear fur opens upper-left while remaining attached to the capsule",
    "direction": "native down-right front three-quarter facing",
    "equipment": "none; body component contains no sword, handle, guard, blade, scabbard, clothing, vessel, guqin, jewelry or VFX",
    "frontPaws": "two front paws cluster at the right-front grip corridor but no handle, blade or scabbard is drawn in this body component",
    "paws": "exactly four small round paw pads overlap the capsule boundary inside the four colored contact polygons; no space or limb may exist between any paw and the capsule",
    "valueStructure": "match Image 1 flat controlled value grouping; no smooth body gradient, airbrush shadow, volumetric lighting, glossy highlight patch or extra forehead/body highlight",
    "visualMoment": "body-only Release-to-Impact peak"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "exitWindow": [
      150,
      150,
      178,
      181
    ],
    "hiddenGrip": [
      162,
      165
    ],
    "tipMayBeOccluded": true,
    "tipRegion": [
      196,
      207,
      232,
      242
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create one complete equipment-free Pure Run character BODY component on a perfectly flat exact #00ff00 background.

Image 1 alone owns the cinnamon-red five-red Chow identity, DR face, compact volume, curled tail, bold dark outline, and sparse flat shading. Image 2 owns only the action silhouette and four colored paw/body overlap zones; never draw its guide marks.

Keep one upright furry capsule, clearly taller than wide, leaning forward-right as a single mass. Compress the capsule’s lower-left side and reach its head/upper core toward screen lower-right. Keep the curled tail attached and opening upper-left as counterbalance. It must read as committed Release-to-Impact movement, not Idle.

Exactly four SMALL ROUND PAW PADS overlap the capsule edge inside Image 2’s four zones: two at the right-front future grip corridor and two at the body bottom. Every paw directly overlaps the body. No arms, legs, shoulders, hips, elbows, knees, ankles, human hands, gaps, thin connectors, long back, or quadruped torso.

Match Image 1’s few discrete fur colors and simple flat shadow shapes. No smooth gradients, airbrush shading, volumetric light, glossy body highlights, extra forehead/chest/belly highlights, or realistic fur modeling.

Draw absolutely no sword, handle, guard, blade, scabbard, clothing, hat, vessel, guqin, jewelry, VFX, text, watermark, crop, or guide marks. Leave the complete silhouette centered with safe margin.
