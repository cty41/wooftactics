# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color

## Reference responsibilities
- equipment_reference: Tools/artworks/poet_five_red_chow/equipment/calibrated/poet_ring_pommel_tang_dao_sheathed_ul_v01.png @ 28a2de2782aace589855fd7aafb86e90ff11204bb085a12c5a4bf0ee2613eed4
- mother: Tools/artworks/poet_five_red_chow/calibrated/poet_five_red_chow_idle_dr_v03.png @ ee4bc7fe07aa2e2d2cd96e9d1d9c62779d07b191e1e1dd2efe0e05c3f096e753

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
      -2.0,
      2.0
    ],
    "top": [
      128,
      121
    ]
  },
  "equipmentState": {
    "scabbard": "present",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [
    {
      "name": "face-on-back",
      "rect": [
        100,
        120,
        158,
        175
      ]
    },
    {
      "name": "front-layer-left-free-paw",
      "rect": [
        80,
        150,
        110,
        210
      ]
    }
  ],
  "requiredHiddenLabels": [
    "near_hand",
    "far_hand",
    "equipment"
  ],
  "screenSpaceIntent": {
    "direction": "upper-left back view",
    "feet": "two rear paws visible at baseline",
    "freePaw": "screen-left near paw behind core; no equipment contact",
    "holdingPaw": "screen-right far paw behind core; only outer arc may remain visible",
    "ringPommel": "upper-right outside or touching the shoulder silhouette",
    "scabbard": "steep behind-body axis descending slightly toward lower-left"
  },
  "visibleAreaCaps": {
    "equipment": 0.45,
    "far_hand": 0.35,
    "near_hand": 0.35
  },
  "weapon": {
    "bladeCenterline": [
      149,
      154,
      161,
      226
    ],
    "exitWindow": [
      151,
      143,
      180,
      180
    ],
    "guardWindow": [
      149,
      158,
      172,
      185
    ],
    "hiddenGrip": [
      161,
      164
    ],
    "maxBladeWidthPx": 16,
    "maxGemAreaPx": 0,
    "tipRegion": [
      141,
      207,
      164,
      235
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# Five-Red Chow Poet — Idle UL with Sheathed Ring-Pommel Tang Dao

Create exactly one native upper-left-facing back-view sprite of the approved five-red chow poet. Use the bound poet image only for identity, palette, ears, fluffy tail, rounded capsule volume and four-paw anatomy. Do not mirror the face or place facial features on the back of the head.

Use the bound approved UL Tang dao as the only weapon projection. Preserve its steep carried-space axis, oblique partially visible ring face, short wrapped grip, straight completely enclosed scabbard, palette and cartoon detail. Do not use or reconstruct the old DR dao projection.

## Required back-view topology

- Native upper-left back view under the fixed orthographic isometric camera.
- Rounded capsule torso, not pear-shaped; body volume comparable to approved poet DR.
- Two triangular chow ears and a readable full fluffy tail.
- No eyes, nose, muzzle or mouth visible on the back of the head.
- Two rear foot paws visible at the baseline.
- No thin arms or legs.

## Required depth and carry

Draw in this depth order:

1. Approved UL Tang dao behind the body.
2. Both front paws behind the body.
3. Main head-and-body core over their inner portions.
4. Rear foot paws at the bottom.

- Anatomical left holding paw becomes the screen-right far paw in UL and remains behind the core. Only a small outer arc may remain visible around the grip; the body hides its inner area.
- Screen-left near/free paw is also behind the core and does not touch the dao. Only a small relaxed outer arc may remain visible.
- Ring pommel sits at or just outside the upper-right shoulder silhouette.
- The approved steep scabbard descends behind the screen-right side and slightly toward lower-left. Its middle is hidden by the torso. Only physically plausible ring/grip and limited lower scabbard/end-cap portions may remain visible.
- Keep the pose relaxed and low, not raised, attacking or alert.

## Composition

- One complete character, centered on 256×256 with foot baseline near y=236.
- Pure bright green `#00ff00` background for deterministic chroma removal.
- Bold dark outline, flat values, low interior detail, cute-dark Pure Run cartoon rendering.
- Preserve safe margins around both ears, tail, feet and ring pommel.

## Exclude

No guqin, wineskin, exposed blade, second weapon, face on the back of the head, front-layer front paw, fully exposed holding paw, full dao pasted across the back, mirrored DR sprite, thin limb connector, text, floor, shadow, border, realistic material, gradients, engraving or effects.

