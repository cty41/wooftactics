# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Create one complete Melee DR sprite for the Release-to-Impact peak: the poet has committed body weight into a steep down-right Tang-dao strike. This is not an Idle, windup, recover, or VFX frame.
First read: five-red-chow-poet, native-down-right-facing, committed-body-weight, release-to-impact-downslash, drawn-ring-pommel-tang-dao

## Frozen invariants
- Image 1 is the sole identity, volume, palette and native DR-facing authority
- cinnamon-red five-red native Chow Chow with broad lion-like head, short wide muzzle, small upright triangular ears, two small dark eyes and restrained confident mouth
- compact furry capsule body with thick high-set curled plume tail
- exactly four small round paws attached directly to the body with broad multi-pixel contact; no arms, forearms, elbows, leg connectors, human hands or humanoid torso
- poet profession is expressed through dignified temperament, not clothing or extra equipment
- no hat, clothing, gourd, wineskin, guqin, jewelry or spell effect
- one empty carried side/back scabbard remains mostly behind the body
- all feet remain within the 256 canvas and preserve the offline y236 ground-anchor envelope

## Forbidden
- upright Idle weapon display
- generic gray-white forehead blaze
- heterochromic ear
- half-body alternate coat color
- hat
- clothing
- gourd
- wineskin
- guqin
- jewelry
- Demonbound identity
- arm
- forearm
- elbow
- leg connector
- human hand
- floating paw
- thin grip connector
- missing paw
- extra paw
- horizontal sword axis
- broad double-edged fantasy sword
- curved katana
- second blade
- missing empty scabbard
- motion blur
- slash trail
- glow
- VFX
- text
- watermark
- cropped silhouette
- pose-guide lines or boxes

## Reference responsibilities
- image-1-approved-poet-identity-volume-dr: Sole character identity, compact volume, cinnamon-red palette, native DR facing, four attached paws, curled tail, facial restraint and carried empty-scabbard relationship. Do not copy the upright Idle pose or sheathed-blade state. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3]
- image-2-approved-unsheathed-tang-dao-form: Sole drawn weapon authority: ring pommel, grip, guard, straight single-edged Tang-dao blade, proportions, tip, colors and detail budget. Do not copy isolated canvas placement as body pose. [Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02.png @ 375409b3b1fa37a4e74b81500d5abea63c57d311fed5eabcc85759d49aa193b1]
- image-3-deterministic-release-impact-action-guide: Action design only: down-right committed line of action, forward center of mass, right/down support paw, rear-left drive paw, compressed body middle, upper-left counterbalance and steep blade endpoints. Do not render guide colors, boxes or abstract marks. [Tools/artworks/poet_five_red_chow/guides/poet_melee_dr_release_impact_pose_guide_v3.png @ 8b88f44ee38e01728e9471fef2c5424bf2b34086499f99bd89f3608435d5d421]

## Composition
```json
{
  "actionDesign": {
    "centerOfMass": [
      140,
      178
    ],
    "compressionLine": [
      [
        104,
        197
      ],
      [
        140,
        178
      ],
      [
        160,
        159
      ]
    ],
    "counterbalanceLine": [
      [
        119,
        171
      ],
      [
        91,
        148
      ],
      [
        73,
        129
      ]
    ],
    "driveFoot": [
      108,
      229
    ],
    "lineOfAction": [
      [
        107,
        229
      ],
      [
        153,
        108
      ]
    ],
    "silhouette": [
      [
        91,
        204
      ],
      [
        91,
        153
      ],
      [
        108,
        113
      ],
      [
        137,
        92
      ],
      [
        165,
        103
      ],
      [
        181,
        135
      ],
      [
        179,
        178
      ],
      [
        162,
        216
      ],
      [
        143,
        231
      ],
      [
        111,
        229
      ]
    ],
    "supportFoot": [
      149,
      233
    ]
  },
  "bodyLayer": false,
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      112,
      228
    ],
    "tiltDegrees": [
      14.0,
      24.0
    ],
    "top": [
      153,
      108
    ]
  },
  "coreBbox": [
    88,
    92,
    182,
    232
  ],
  "equipmentState": {
    "blade": "drawn-single-ring-pommel-tang-dao",
    "scabbard": "present",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [
    {
      "name": "face-weapon-exclusion",
      "rect": [
        102,
        99,
        157,
        143
      ]
    },
    {
      "name": "wrong-upper-left-blade-tip",
      "rect": [
        0,
        0,
        104,
        112
      ]
    },
    {
      "name": "detached-lower-left-weapon",
      "rect": [
        0,
        178,
        78,
        255
      ]
    },
    {
      "name": "too-horizontal-right-blade",
      "rect": [
        194,
        135,
        255,
        184
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "bodyDynamics": "center of mass shifts down-right over the support paw; rear drive paw trails; core compresses then reaches through head-shoulder and grip",
    "counterbalance": "tail and rear silhouette open to upper-left against the down-right blade axis",
    "direction": "native down-right front three-quarter facing",
    "drawnDao": "Image 2 form only; one straight single-edged ring-pommel Tang dao, steep down-right axis, complete tip",
    "effects": "none",
    "feet": "right/down support hind paw at y236 area, left/rear drive paw trails; all four paws remain coherent",
    "grip": "two small round front paws attach directly to the body edge around the grip with broad multi-pixel contact; no arms, elbows, thin connectors or human hands",
    "identity": "Image 1 only; five-red chow, cinnamon-red coat, broad lion head, short muzzle, compact furry capsule, curled tail, exactly four attached paws; no clothing, hat, gourd, wineskin, guqin or jewelry",
    "playerRead": "poet has committed body weight into one steep down-right Tang-dao strike",
    "scabbard": "one empty side/back scabbard remains mostly behind the body; no second blade",
    "visualMoment": "release-to-impact peak; not windup and not recover"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      163,
      164,
      224,
      235
    ],
    "exitWindow": [
      146,
      146,
      176,
      177
    ],
    "guardWindow": [
      151,
      151,
      181,
      181
    ],
    "hiddenGrip": [
      159,
      160
    ],
    "maxBladeWidthPx": 18,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        160,
        158
      ],
      [
        222,
        232
      ]
    ],
    "tipMayBeOccluded": false,
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
Create one complete 256×256 Pure Run character sprite on a perfectly flat exact #00ff00 background.

Image 1 owns the poet’s identity, volume, palette, native down-right facing, four-paw anatomy, curled tail, restrained expression, and carried empty scabbard. Image 2 owns only the drawn ring-pommel Tang dao’s exact form and colors. Image 3 owns only action geometry; do not draw its colored lines, boxes, circles, or abstract guide marks.

Show the Release-to-Impact peak of one committed steep diagonal downslash toward screen lower-right. Shift the compact body’s weight forward-right over the support hind paw; trail the rear-left drive paw; compress the body middle and reach head/shoulder and grip into the strike. Open the tail/rear silhouette slightly upper-left as counterbalance. This must read as an attack, not an upright weapon display.

Keep exactly four small round paws directly attached to the furry capsule body. Two front paws clamp the grip at the body edge with broad contact. No arms, elbows, connectors, human hands, or floating paws.

Draw one complete straight single-edged Tang dao descending steeply lower-right and one mostly occluded empty side/back scabbard. No hat, clothing, gourd, wineskin, guqin, jewelry, second blade, fantasy sword, katana, VFX, trail, glow, text, watermark, crop, or guide marks. Preserve bold dark outlines, flat controlled values, and compact game-scale readability.
