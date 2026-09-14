# Deterministic ImageGen Task Packet

## Brief intent
Purpose: Create one unified Cast DR sprite for five Poet skill families. At the release hold, both front paws gather at chest height around the midpoint of one completely sheathed ring-pommel Tang dao, using the whole sheathed weapon as a short ritual focus. This is not a skill-specific drinking, music, sword-rain VFX, attack, Hit or Idle frame.
First read: five-red-chow-poet, native-down-right-facing, unified-cast-release, two-paw-centered-focus-grip, fully-sheathed-dao-lower-left-to-upper-right

## Frozen invariants
- Image 1 is the sole identity, volume, palette, native DR-facing and carried-character style authority
- cinnamon-red five-red native Chow Chow with broad lion-like head, short wide muzzle, small upright triangular ears, two small dark eyes and restrained confident mouth
- compact furry capsule body with thick high-set curled plume tail
- exactly four small round paws attached directly to the body with broad multi-pixel contact; no arms, forearms, elbows, leg connectors, human hands or humanoid torso
- poet profession is expressed through dignified temperament, not clothing or extra props
- no hat, clothing, gourd, wineskin, guqin, jewelry or spell effect
- one and only one completely sheathed ring-pommel Tang dao; no bare blade and no separate empty scabbard
- all feet remain within the 256 canvas and preserve the offline y236 ground-anchor envelope

## Forbidden
- upright Idle display
- generic gray-white forehead blaze
- heterochromic ear
- half-body alternate coat color
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
- thin grip connector
- missing paw
- extra paw
- bare blade
- drawn sword
- empty side scabbard
- second weapon
- broad double-edged fantasy sword
- curved katana
- staff crystal
- motion blur
- spell glow
- runes
- VFX
- text
- watermark
- cropped silhouette
- pose-guide lines or labels

## Reference responsibilities
- image-1-approved-poet-identity-volume-dr: Sole character identity, compact volume, cinnamon-red palette, native DR facing, exactly four attached paws, curled tail and facial restraint. Do not copy the Idle paw placement or weapon placement as the target action. [Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3]
- image-2-approved-fully-sheathed-dao-form: Sole equipment-form authority: completely sheathed straight ring-pommel Tang dao, palette, proportions and detail budget. It does not prescribe the mounted grip; Image 3 owns the new two-paw midpoint grip and screen axis. [Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_sheathed_dr_v02.png @ eb4acedf50fe56d2c216e932a7364686de603a4df40c9bc5f1fe10c22ab55999]
- image-3-deterministic-unified-cast-guide: Action geometry only: option B two-paw chest-height gathering, approximately five-degree body lean, lower-left ring pommel, upper-right sheathed tip, midpoint grip and grounded hind paws. Do not render guide colors, boxes, arrows or labels. [Tools/artworks/poet_five_red_chow/guides/poet_cast_dr_sheathed_focus_pose_guide_v1.png @ 47ed30369fb33f6afb1607184ceac72cf7bb657d730efb8ecbc52948d3c92640]

## Composition
```json
{
  "actionDesign": {
    "centerOfMass": [
      134,
      178
    ],
    "compressionLine": [
      [
        106,
        196
      ],
      [
        134,
        178
      ],
      [
        166,
        165
      ]
    ],
    "counterbalanceLine": [
      [
        119,
        182
      ],
      [
        92,
        173
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
        140,
        104
      ]
    ],
    "pawContactZones": {
      "farFoot": [
        [
          96,
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
          96,
          241
        ]
      ],
      "farHand": [
        [
          124,
          155
        ],
        [
          149,
          155
        ],
        [
          149,
          178
        ],
        [
          124,
          178
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
          148,
          160
        ],
        [
          176,
          160
        ],
        [
          176,
          185
        ],
        [
          148,
          185
        ]
      ]
    },
    "silhouette": [
      [
        90,
        204
      ],
      [
        91,
        151
      ],
      [
        108,
        113
      ],
      [
        137,
        96
      ],
      [
        169,
        106
      ],
      [
        188,
        139
      ],
      [
        188,
        207
      ],
      [
        174,
        231
      ],
      [
        143,
        236
      ],
      [
        108,
        230
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
      3.0,
      8.0
    ],
    "top": [
      140,
      104
    ]
  },
  "coreBbox": [
    85,
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
        108,
        91,
        178,
        143
      ]
    },
    {
      "name": "wrong-lower-right-scabbard-tip",
      "rect": [
        184,
        184,
        255,
        255
      ]
    },
    {
      "name": "wrong-upper-left-scabbard-tip",
      "rect": [
        0,
        0,
        100,
        118
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "bodyDynamics": "compact near-upright capsule leans about five degrees toward the target while both hind paws stay grounded at y236",
    "direction": "native down-right front three-quarter facing",
    "effects": "none",
    "grip": "two small round front paws gather at chest height on opposite sides of the weapon midpoint, directly overlapping the body edge with broad multi-pixel contact; no arms, elbows, thin connectors or human hands",
    "heldDao": "one completely sheathed straight ring-pommel Tang dao crosses from lower-left to upper-right; scabbard tip is the upper-right focus endpoint and ring pommel remains lower-left",
    "identity": "Image 1 only; five-red chow, cinnamon-red coat, broad lion head, short muzzle, compact furry capsule, curled tail, exactly four attached paws; no clothing, hat, gourd, wineskin, guqin or jewelry",
    "playerRead": "poet gathers both front paws at chest height around the middle of one fully sheathed Tang dao and uses the whole sheathed weapon as a short ritual focus",
    "visualMoment": "unified casting release hold; not Idle, attack, Hit or recover"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      96,
      132,
      209,
      210
    ],
    "exitWindow": [
      126,
      151,
      174,
      188
    ],
    "guardWindow": [
      116,
      174,
      151,
      205
    ],
    "hiddenGrip": [
      149,
      169
    ],
    "maxBladeWidthPx": 20,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        96,
        210
      ],
      [
        209,
        132
      ]
    ],
    "tipMayBeOccluded": false,
    "tipRegion": [
      190,
      118,
      222,
      148
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create one complete 256×256 RGBA character sprite on a perfectly flat pure green #00FF00 background.

Image 1 is the approved equipped Poet Idle DR v03. It is the sole authority for the cinnamon-red five-red Chow Chow identity, compact furry capsule volume, down-right front three-quarter anatomy, broad lion-like head, short muzzle, small upright ears, dark eyes, restrained expression, curled plume tail, exactly four small round paws directly attached to the body, palette, outlines and detail budget. Preserve that identity while redrawing the whole character into the Cast action. Do not copy its Idle paw placement or Idle weapon placement.

Image 2 is the approved isolated completely sheathed ring-pommel Tang dao. It is the sole authority for the one-piece equipment form, straight compact proportions, hollow ring pommel, scabbard tip, palette, outline and detail budget. It does not prescribe the new grip or canvas placement.

Image 3 is a deterministic geometry Guide only. Follow its selected B-pose: both front paws gather at chest height around the exact midpoint of the completely sheathed dao. The weapon crosses from lower-left ring pommel to upper-right sheathed tip, like a short ritual focus. The compact body remains near upright with about five degrees of forward casting lean; both hind paws remain grounded. Do not render any Guide boxes, colors, arrows, labels or construction marks.

This is one unified Cast DR release-hold frame shared by five skill families. It must read as dignified gathering and channeling, not Idle, drinking, music playing, melee windup, strike, Hit, or recovery. The completely sheathed dao is used like a short staff but remains unmistakably a sheathed ring-pommel Tang dao. There is no bare blade and no separate empty scabbard.

This is a no-arm, no-leg capsule character. Both small round front paws must visibly clasp opposite sides of the weapon midpoint while each paw directly overlaps the body edge through a broad multi-pixel contact. Never draw arms, forearms, elbows, wrists, thin connectors, human hands, floating paws or detached paws. Keep exactly two front paws and two hind paws.

Keep the face unobstructed. Keep the full character, tail, four paws, ring pommel and complete sheathed tip inside the canvas. Preserve the ground anchor centered at x=128 with baseline y=236. Preserve bold dark outlines, flat controlled values, compact readable masses and restrained sprite detail.

No hat, clothing, gourd, wineskin, guqin, jewelry, crystal, staff ornament, bare blade, drawn sword, second weapon, motion blur, glow, runes, particles, spell effect, text or watermark. Do not copy Mage, Necromancer or Demonbound identity. Redraw one coherent complete action sprite.

