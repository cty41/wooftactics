# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Create one unified Cast DR sprite for five Poet skill families. The Poet braces one short completely sheathed ring-pommel Tang dao vertically and stacks both front paws immediately below the guard on the sheath throat. Preserve sword/body ratio and keep the sheathed tip above ground.
First read: five-red-chow-poet, native-down-right-facing, firm-vertical-cast-brace, two-paws-stacked-below-guard, short-fully-sheathed-dao

## Frozen invariants
- Image 1 is the sole identity, volume, palette and native DR-facing authority
- cinnamon-red five-red Chow Chow with broad lion-like head, short wide muzzle, small upright triangular ears, dark eyes and restrained confident mouth
- compact furry capsule body with thick high-set curled plume tail
- exactly four small round paws attached directly to the body with broad multi-pixel contact; no arms, forearms, elbows, leg connectors, human hands or humanoid torso
- no hat, clothing, gourd, wineskin, guqin, jewelry or spell effect
- one completely sheathed ring-pommel Tang dao at approved sword/body ratio; no bare blade and no separate scabbard
- feet remain centered at x128 with baseline y236

## Forbidden
- staff-length weapon
- ground-contacting sheath tip
- mid-sheath grip
- grip above guard
- generic forehead highlights
- hat
- clothing
- gourd
- wineskin
- guqin
- jewelry
- arm
- forearm
- elbow
- leg connector
- human hand
- floating paw
- thin connector
- missing paw
- extra paw
- bare blade
- drawn sword
- empty side scabbard
- second weapon
- crystal
- motion blur
- glow
- runes
- VFX
- text
- watermark
- cropped silhouette
- guide marks

## Reference responsibilities
- image-1-approved-poet-identity-volume-dr: Sole Poet identity, compact volume, palette, native DR anatomy, four attached paws, curled tail and style. Do not copy Idle pose or weapon placement. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3]
- image-2-approved-fully-sheathed-dao-form: Sole equipment form, ring, grip, guard, sheath, proportions, palette and detail authority; Image 3 owns vertical placement and grip. [Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_sheathed_dr_v02.png @ eb4acedf50fe56d2c216e932a7364686de603a4df40c9bc5f1fe10c22ab55999]
- image-3-deterministic-vertical-guard-grip-guide: Accepted v07 geometry only: vertical short sheathed dao, ring up, tip above ground, two paws stacked on sheath throat immediately below guard, compact grounded body. [Tools/artworks/poet_five_red_chow/guides/poet_cast_dr_vertical_guard_grip_pose_guide_v2.png @ ae4f96af30ace1e6654f65c039f3ad3f0b8e2fca5cfb2faf175437d6f44cd5aa]

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
        105,
        194
      ],
      [
        132,
        178
      ],
      [
        167,
        157
      ]
    ],
    "counterbalanceLine": [
      [
        118,
        182
      ],
      [
        92,
        174
      ],
      [
        76,
        158
      ]
    ],
    "driveFoot": [
      108,
      233
    ],
    "lineOfAction": [
      [
        128,
        229
      ],
      [
        136,
        101
      ]
    ],
    "pawContactZones": {
      "farFoot": [
        [
          95,
          220
        ],
        [
          124,
          220
        ],
        [
          124,
          241
        ],
        [
          95,
          241
        ]
      ],
      "farHand": [
        [
          148,
          132
        ],
        [
          176,
          132
        ],
        [
          179,
          154
        ],
        [
          150,
          157
        ]
      ],
      "nearFoot": [
        [
          142,
          221
        ],
        [
          174,
          221
        ],
        [
          174,
          242
        ],
        [
          142,
          242
        ]
      ],
      "nearHand": [
        [
          151,
          153
        ],
        [
          180,
          151
        ],
        [
          183,
          176
        ],
        [
          152,
          178
        ]
      ]
    },
    "silhouette": [
      [
        86,
        204
      ],
      [
        89,
        151
      ],
      [
        106,
        112
      ],
      [
        135,
        96
      ],
      [
        168,
        105
      ],
      [
        190,
        140
      ],
      [
        188,
        204
      ],
      [
        173,
        231
      ],
      [
        143,
        236
      ],
      [
        105,
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
      2.0,
      6.0
    ],
    "top": [
      136,
      101
    ]
  },
  "coreBbox": [
    84,
    96,
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
      "name": "face-equipment-exclusion",
      "rect": [
        105,
        96,
        159,
        143
      ]
    },
    {
      "name": "staff-length-upper",
      "rect": [
        160,
        0,
        190,
        72
      ]
    },
    {
      "name": "grounded-sheath-tip",
      "rect": [
        158,
        229,
        194,
        255
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "bodyDynamics": "compact near-upright capsule with a slight forward brace; both hind paws grounded at y236",
    "direction": "native down-right front three-quarter facing",
    "effects": "none",
    "grip": "two small round front paws stack directly below the guard around the sheath mouth/throat, each broadly overlapping the body edge; no arms, elbows, wrists, thin connectors or human hands",
    "heldDao": "approved original sword/body ratio; hollow ring pommel above the guard, sheath descends vertically, complete sheathed tip stops visibly above the ground and never becomes staff length",
    "identity": "approved Image 1 only; five-red chow, cinnamon-red coat, broad lion head, short muzzle, compact furry capsule, curled tail and exactly four attached paws",
    "playerRead": "poet braces one short fully sheathed Tang dao vertically in front-right of the chest and stacks both paws immediately below the guard on the sheath throat",
    "visualMoment": "unified Cast release hold inspired only by the firm vertical blocking silhouette of the supplied Gandalf bridge screenshot; not a likeness or costume reference"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      166,
      86,
      182,
      220
    ],
    "exitWindow": [
      151,
      126,
      190,
      177
    ],
    "guardWindow": [
      158,
      116,
      190,
      137
    ],
    "hiddenGrip": [
      174,
      151
    ],
    "maxBladeWidthPx": 18,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        174,
        88
      ],
      [
        174,
        218
      ]
    ],
    "tipMayBeOccluded": false,
    "tipRegion": [
      164,
      204,
      186,
      226
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create one complete 256×256 RGBA game character sprite on a perfectly flat pure green #00FF00 background.

Image 1 is the sole character authority. Preserve the exact approved cinnamon-red five-red Chow Chow Poet identity: broad lion-like head, short wide muzzle, small upright triangular ears, two small dark eyes, restrained confident face, compact furry capsule body, thick high-set curled plume tail, flat controlled values, bold dark outlines, and exactly four small round paws directly attached to the body. Native down-right front three-quarter view. Do not copy the Idle pose or Idle weapon placement.

Image 2 is the sole equipment-form authority. Preserve one completely sheathed straight compact ring-pommel Tang dao: original sword-to-body length, hollow ring pommel, short grip, guard, sheath mouth, scabbard body, complete sheath tip, palette, outline and restrained detail. It is a sword in its sheath, not a staff.

Image 3 is geometry only. Follow the accepted vertical guard-grip pose: the short fully sheathed dao is almost vertical in front-right of the chest. Ring pommel and grip are above the guard. Both front paws stack immediately below the guard and clasp the sheath mouth/throat—the exact region just beyond the guard—not the handle, not the weapon midpoint, and not the lower sheath. The complete sheathed tip stops visibly above the ground. Preserve the approved sword/body ratio; never lengthen it to staff scale. Do not render guide boxes, colors, arrows or labels.

The body makes a slight firm forward brace inspired only by the vertical blocking silhouette of a classic bridge wizard scene. Do not copy human anatomy, costume, beard, staff or likeness. This is one dignified unified Cast release-hold frame, not Idle, drinking, music, melee attack, Hit or recovery.

NO ARMS OR LEGS. Each small round front paw must broadly overlap the body edge and the sheath throat with stable multi-pixel contact. Exactly two front paws and two grounded hind paws. No forearms, elbows, wrists, thin connectors, human hands, floating paws, missing paws or extra paws. Keep the face unobstructed.

Keep the full silhouette, tail, all paws, ring pommel and sheath tip inside the canvas. Feet centered at x=128, baseline y=236. No hat, clothing, gourd, wineskin, guqin, jewelry, crystal, bare blade, drawn sword, empty second scabbard, second weapon, motion blur, glow, runes, particles, spell VFX, text or watermark. No smooth generic AI gradient, no forehead highlight ovals, and no pear-shaped humanoid torso. Redraw one coherent complete sprite.
