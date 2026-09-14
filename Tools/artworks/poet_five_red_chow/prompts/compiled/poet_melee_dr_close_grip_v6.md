# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Perform one tightly scoped edit of the cty41-selected body: replace only the two front paws, place the approved unsheathed Tang dao through them, keep the paws close to the body, raise the slash, and hide the scabbard. The project requires a clear melee read rather than strict action-game timing.
First read: five-red-chow-poet, close-two-paw-grip, raised-down-right-tang-dao, readable-melee-attack, no-scabbard

## Frozen invariants
- Image 1 is the exact selected body and owns every pixel outside the two-front-paw and grip edit region
- preserve Image 1 head, face, ears, torso, tail, hind paws, shading, scale, placement and silhouette
- replace both front paws with two small round paws attached at the body's right edge; they must not form a chain extending away from the torso
- Image 2 solely owns the narrow straight single-edged ring-pommel Tang dao form
- the handle passes between both paws; far paw is partly behind the handle and near paw clearly overlaps the handle
- Image 3 owns the close-grip positions and moderately raised down-right blade axis
- exactly one sword and no scabbard or other equipment
- no scabbard anywhere

## Forbidden
- change to head
- change to face
- change to torso
- change to tail
- change to hind paws
- body rescale
- body reposition
- new lighting
- front paws chained away from body
- floating paw
- one-paw grip
- human hand
- arm
- leg
- broad double-edged sword
- scabbard
- second weapon
- clothing
- vessel
- guqin
- VFX
- text
- watermark
- crop
- guide marks

## Reference responsibilities
- image-1-cty41-selected-body-exact-preservation: Exact selected body appearance, placement and all pixels outside the local two-front-paw/grip region. [Tools/artworks/pipeline/artifacts/job-0a890f2b18e97967/job-0a890f2b18e97967-a008/calibrated.png @ e6feabd6ca60bf75701528c30056103e7781cb6fe5234de353c14caf41ef0404]
- image-2-approved-unsheathed-tang-dao-form: Sole weapon-form, ring-pommel, guard, blade proportion and color authority; no scabbard. [Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02.png @ 375409b3b1fa37a4e74b81500d5abea63c57d311fed5eabcc85759d49aa193b1]
- image-3-close-grip-raised-blade-guide: Only the two close paw zones, hidden grip, guard/exit windows and moderately raised down-right blade axis. Never render guide marks. [Tools/artworks/poet_five_red_chow/guides/poet_melee_dr_close_grip_pose_guide_v6.png @ e5ba41d603f184422cda41b3d29858ccc983e56620b85bfac3cea212e536770a]

## Composition
```json
{
  "actionDesign": {
    "centerOfMass": [
      132,
      178
    ],
    "compressionLine": [
      [
        103,
        207
      ],
      [
        132,
        178
      ],
      [
        149,
        194
      ]
    ],
    "counterbalanceLine": [
      [
        112,
        172
      ],
      [
        88,
        154
      ],
      [
        72,
        142
      ]
    ],
    "lineOfAction": [
      [
        111,
        229
      ],
      [
        150,
        122
      ]
    ],
    "pawContactZones": {
      "farFoot": [
        [
          126,
          221
        ],
        [
          138,
          221
        ],
        [
          138,
          237
        ],
        [
          127,
          237
        ]
      ],
      "farHand": [
        [
          136,
          183
        ],
        [
          150,
          184
        ],
        [
          152,
          198
        ],
        [
          138,
          198
        ]
      ],
      "nearFoot": [
        [
          89,
          213
        ],
        [
          108,
          213
        ],
        [
          109,
          235
        ],
        [
          91,
          235
        ]
      ],
      "nearHand": [
        [
          140,
          190
        ],
        [
          155,
          190
        ],
        [
          159,
          206
        ],
        [
          143,
          208
        ]
      ]
    },
    "silhouette": [
      [
        69,
        175
      ],
      [
        86,
        130
      ],
      [
        126,
        116
      ],
      [
        160,
        127
      ],
      [
        175,
        166
      ],
      [
        164,
        215
      ],
      [
        139,
        239
      ],
      [
        91,
        231
      ]
    ]
  },
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      128,
      236
    ],
    "tiltDegrees": [
      6.0,
      16.0
    ],
    "top": [
      147,
      120
    ]
  },
  "coreBbox": [
    69,
    117,
    175,
    240
  ],
  "equipmentState": {
    "blade": "visible",
    "scabbard": "absent",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [
    [
      0,
      0,
      256,
      112
    ],
    [
      0,
      241,
      256,
      256
    ]
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "direction": "native down-right front three-quarter facing",
    "editScope": "preserve Image 1 head, face, torso, tail, hind paws, shading, scale and framing; only replace both front paws and introduce Image 2 sword",
    "equipment": "one approved Tang dao only; no scabbard or other equipment",
    "frontPaws": "both paws sit against the body's right edge, overlap each other around the hidden handle near [145,195], and never form a chain extending away from the body",
    "grip": "the narrow handle passes between both paws; far paw partly behind handle, near paw over handle; guard immediately outside near paw",
    "visualMoment": "readable compact melee strike, not strict action-game impact timing",
    "weapon": "raise the blade to a moderate down-right axis ending around [212,236], less steep than prior review mockups"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      154,
      199,
      214,
      239
    ],
    "bladeLengthRangePx": [
      58,
      78
    ],
    "exitWindow": [
      150,
      196,
      164,
      211
    ],
    "guardWindow": [
      148,
      194,
      163,
      209
    ],
    "hiddenGrip": [
      145,
      195
    ],
    "maxBladeWidthPx": 12,
    "minBladeWidthPx": 4,
    "screenAxis": [
      [
        143,
        194
      ],
      [
        212,
        236
      ]
    ],
    "tipMayBeOccluded": false,
    "tipRegion": [
      198,
      220,
      222,
      243
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Edit one complete 256×256 Pure Run sprite on exact flat #00ff00 green.

Image 1 is the selected body. Preserve its head, face, ears, torso, tail, hind paws, shading, scale, position and silhouette exactly. Modify ONLY both front paws and the new sword/grip region.

Image 2 solely owns the narrow straight single-edged ring-pommel Tang dao form, proportions and colors. Add exactly one complete sword. No scabbard.

Image 3 owns only placement. Redraw two SMALL ROUND front paws tightly against Image 1’s right body edge inside the two green zones. They must overlap the torso, not form a chain extending away from it. Pass the narrow handle BETWEEN both paws: far paw partly behind the handle, near paw visibly over the handle, guard immediately outside the near paw. Both paws must visibly grip the same handle with broad contact; the sword must not look loose or about to leave the paws.

Aim the blade moderately down-right along Image 3’s white axis, ending around its orange tip box—clearly higher than the prior low slash. Preserve exactly two hind paws.

No arm, leg, human hand, floating paw, broad double-edged sword, second weapon, scabbard, clothing, vessel, guqin, VFX, new highlight, new body shading, text, watermark, crop, or guide marks.
