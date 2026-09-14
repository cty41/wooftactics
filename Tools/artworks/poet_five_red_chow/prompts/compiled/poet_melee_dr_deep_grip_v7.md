# Deterministic ImageGen Task Packet

## Brief intent
Purpose: One final tightly scoped grip retry: embed both front paws mostly inside the selected body's torso silhouette, overlap the paws, hide the ring pommel and most handle, keep the guard against the body edge, show one raised approved Tang dao, and hide the scabbard.
First read: five-red-chow-poet, deep-overlapping-two-paw-grip, raised-down-right-tang-dao, no-scabbard

## Frozen invariants
- Image 1 owns all body appearance and placement outside a tiny front-paw/grip patch
- preserve head, face, torso, tail, hind paws, shading, scale and framing
- two small front paws sit mostly inside and broadly overlap the torso silhouette; only outer paw arcs remain visible
- the paws overlap each other instead of forming two complete circles beside the body
- ring pommel and most handle remain hidden under torso/paws; handle passes between both paws; guard touches torso edge
- Image 2 solely owns the narrow single-edged Tang dao form
- Image 3 solely owns deep paw zones and raised blade placement
- one sword, no scabbard
- no scabbard anywhere

## Forbidden
- two full circular paws outside torso
- paw chain
- loose grip
- visible ring pommel outside paws
- long exposed handle
- change to head
- change to torso
- change to tail
- change to hind paws
- body rescale
- body reposition
- arm
- leg
- human hand
- floating paw
- broad double-edged sword
- scabbard
- second weapon
- clothing
- VFX
- text
- watermark
- crop
- guide marks

## Reference responsibilities
- image-1-selected-body-preserve: Exact body appearance and all pixels outside the smallest front-paw/grip patch. [Tools/artworks/pipeline/artifacts/job-0a890f2b18e97967/job-0a890f2b18e97967-a008/calibrated.png @ e6feabd6ca60bf75701528c30056103e7781cb6fe5234de353c14caf41ef0404]
- image-2-approved-tang-dao: Sole weapon form and color authority; no scabbard. [Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02.png @ 375409b3b1fa37a4e74b81500d5abea63c57d311fed5eabcc85759d49aa193b1]
- image-3-deep-grip-guide: Deep overlapping paw zones, hidden grip, body-edge guard and raised blade axis only; never render guide marks. [Tools/artworks/poet_five_red_chow/guides/poet_melee_dr_deep_grip_pose_guide_v7.png @ 01b1bb580d031bdc87727b8742de1d7e1658d4f54c3134ceef12ffc4c3f3ad91]

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
        144,
        193
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
          128,
          181
        ],
        [
          142,
          182
        ],
        [
          146,
          195
        ],
        [
          131,
          197
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
          132,
          189
        ],
        [
          147,
          188
        ],
        [
          151,
          202
        ],
        [
          135,
          205
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
  "forbiddenRegions": [],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "direction": "native down-right front three-quarter facing",
    "editScope": "preserve Image 1 outside a tiny front-paw/grip patch",
    "equipment": "no scabbard or second weapon",
    "frontPaws": "two small paws overlap each other and sit mostly inside the torso silhouette; only their outer arcs are visible",
    "grip": "ring pommel and most handle are hidden inside the torso/paws; handle passes between overlapping paws; guard touches torso edge",
    "visualMoment": "readable compact melee strike",
    "weapon": "one approved narrow Tang dao follows the raised down-right axis"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      151,
      197,
      214,
      239
    ],
    "bladeLengthRangePx": [
      60,
      80
    ],
    "exitWindow": [
      146,
      193,
      158,
      207
    ],
    "guardWindow": [
      145,
      191,
      159,
      207
    ],
    "hiddenGrip": [
      137,
      193
    ],
    "maxBladeWidthPx": 12,
    "minBladeWidthPx": 4,
    "screenAxis": [
      [
        136,
        192
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
Make exactly one tightly scoped edit on exact #00ff00 green.

Image 1 owns the complete selected body. Preserve its head, face, torso, tail, hind paws, shading, size, position and silhouette. Change only the smallest front-paw/grip patch.

Image 2 solely owns one narrow straight single-edged ring-pommel Tang dao. No scabbard.

Image 3 owns placement. Pull both front paws MUCH FARTHER INTO the torso than the previous result. Each paw must be mostly hidden inside the body silhouette; show only a small outer crescent of each paw. The paws overlap each other and must never appear as two complete circles beside the body.

Hide the ring pommel and most of the handle inside the torso and overlapping paws. Route the handle between both paws. The far paw is mostly behind handle/body; the near paw crosses over the handle. Put the guard directly against the body edge. Outside the torso, primarily show guard and blade—not a chain of paws and handle.

Raise the complete blade moderately down-right along Image 3’s white axis. Preserve safe tip margin.

No body redraw, new shading, arm, leg, wrist, human hand, floating paw, full circular external paws, loose grip, broad double-edged sword, second weapon, scabbard, clothing, VFX, text, watermark, crop, or guide marks.
