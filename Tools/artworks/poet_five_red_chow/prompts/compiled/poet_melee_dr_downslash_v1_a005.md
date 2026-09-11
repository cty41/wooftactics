# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color

## Reference responsibilities
- image-1-approved-equipped-idle-dr: Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_ring_pommel_dao_idle_dr_v03.png @ e2246e0e6d3a4cea92b85cf7add5ea442366389ac5173490bf0bff2fbf25dfb3
- image-2-approved-unsheathed-dao-v02: Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_unsheathed_v02.png @ 375409b3b1fa37a4e74b81500d5abea63c57d311fed5eabcc85759d49aa193b1

## Composition
```json
{
  "bodyLayer": false,
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      128,
      231
    ],
    "tiltDegrees": [
      2.0,
      10.0
    ],
    "top": [
      119,
      114
    ]
  },
  "coreBbox": [
    90,
    112,
    166,
    231
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
      "name": "eye-and-face-weapon-exclusion",
      "rect": [
        91,
        105,
        151,
        151
      ]
    },
    {
      "name": "wrong-upper-left-blade-tip",
      "rect": [
        0,
        0,
        96,
        112
      ]
    },
    {
      "name": "detached-lower-left-weapon",
      "rect": [
        0,
        175,
        82,
        255
      ]
    }
  ],
  "renderForbiddenRegions": false,
  "requiredHiddenLabels": [],
  "screenSpaceIntent": {
    "action": "single frozen peak of a forward diagonal downward slash",
    "core": "preserve the approved equipped Idle DR v03 compact capsule volume and complete four-paw silhouette; body top remains slightly left of the y236 foot center during the forward strike",
    "direction": "native down-right front three-quarter facing",
    "drawnDao": "starts at the high body-adjacent implicit front-paw grip and sweeps clearly toward screen lower-right; guard near body and blade tip inside lower-right target region",
    "effects": "none: no motion blur afterimage slash trail blade glow sparks or impact effect",
    "feet": "complete feet remain anchored to the same y236 Idle ground point",
    "grip": "no visible arm; implied front paw overlaps the grip with a broad multi-pixel body-contact surface, never floating and never connected by a thin line",
    "scabbard": "empty scabbard remains tied at the body side/back and is partly hidden by the body"
  },
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      155,
      169,
      222,
      222
    ],
    "exitWindow": [
      137,
      145,
      164,
      174
    ],
    "guardWindow": [
      143,
      149,
      174,
      181
    ],
    "hiddenGrip": [
      145,
      159
    ],
    "maxBladeWidthPx": 18,
    "maxGemAreaPx": 0,
    "screenAxis": [
      [
        150,
        162
      ],
      [
        224,
        222
      ]
    ],
    "tipMayBeOccluded": false,
    "tipRegion": [
      190,
      199,
      240,
      235
    ]
  }
}
```

## Unresolved fixes
- ImageGen chroma background contains near-green variation; exact tolerance 0 left opaque canvas and failed size/corner/fringe validation.
- Prepared raw requires semantic core mask and deterministic core calibration to 256x256 before validation.
- Semantic mask was binary rather than required semantic label colors; calibration could not identify the red core region.
- Candidate drops the approved hat, clothing, and gourd; shows only two grounded paws plus an oversized circular front paw; the blade reads as a symmetric double-edged sword rather than the approved single-edged dao; the pose reads as static presentation more than a forceful downslash peak.

## Base prompt
Generate exactly one complete 256×256 pixel-art character sprite on a square, perfectly uniform pure #00FF00 background.

Use exactly the two supplied images, in their listed order and with strictly separated responsibilities:
Image 1 is the sole authority for the five-red Chow poet’s identity, compact body volume, native down-right three-quarter facing, anatomy, facial markings, colors, line weight, clothing, hat, gourd, four-paw topology, Idle foot placement, and the attached carried relationship of the now-empty scabbard at the body side/back. Preserve these faithfully.
Image 2 is the sole authority for the drawn weapon: preserve its thick empty ring pommel, grip, guard, straight single-edged Tang dao blade, blade proportions, point, colors, and detail budget. Do not import any character identity, scale, direction, or pose from Image 2.

Pose: a single frozen Melee DR action frame at the clear peak of a forward diagonal downward slash. Keep the character facing down-right in the fixed isometric front three-quarter camera. In screen space, the grip is high and immediately adjacent to the character’s near-side middle; the guard remains near the body; the blade exits from that high body-adjacent point and runs diagonally downward toward screen lower-right; the blade tip is clearly in the lower-right. The weapon must read as being swung out from near the body, not posed horizontally, vertically, behind the face, or toward any other quadrant.

No visible arms and no leg connectors. Preserve two front paws and two rear paws. The implied near-side front paw must overlap the grip and overlap the body edge through a broad, stable, multi-pixel contact area. The grip and paw must not float, must not be separated by transparent pixels, and must not connect by a one-pixel point or thin line. Keep the other paws coherently attached to the compact body.

The scabbard is empty because the dao is drawn. Keep that empty scabbard physically attached at the character’s side/back, with the body naturally occluding part of it. Do not delete it, move it into the hand, expose it as a second blade, or place it in front of the striking blade. Preserve the hat, clothing, and gourd exactly as identity equipment.

Keep the complete character and complete sword silhouette inside the canvas with comfortable edge clearance. Keep both feet anchored to the same Idle ground point, centered at screen x=128 with foot baseline y=236. Preserve the approved compact core scale and avoid enlarging or elongating the body to make room for the sword.

Hard exclusions: no arms; no motion blur; no afterimage; no speed streak; no sword trail; no slash arc; no blade light; no blade glow; no sparks; no impact mark; no aura; no magic; no particles; no FX or VFX of any kind. No second weapon, no second drawn blade, no missing scabbard, no double-edged sword, no curved katana, no redesigned ring pommel or guard, no cropped tip, no floating paw, no detached grip, no extra gourd, no guqin, no text, no border, no shadow, and no background detail.

Render one full character only, crisp readable pixel art, no anti-aliased painterly blur, on pure #00FF00.
