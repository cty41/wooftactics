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
        98,
        116,
        160,
        176
      ]
    },
    {
      "name": "front-layer-left-free-paw",
      "rect": [
        78,
        149,
        111,
        211
      ]
    },
    {
      "name": "front-layer-right-holding-paw",
      "rect": [
        145,
        149,
        181,
        211
      ]
    },
    {
      "name": "visible-lower-scabbard-fragment",
      "rect": [
        128,
        182,
        172,
        232
      ]
    }
  ],
  "requiredHiddenLabels": [
    "near_hand",
    "far_hand",
    "equipment"
  ],
  "screenSpaceIntent": {
    "continuity": "ring short grip and optional throat cue form one connected shoulder-side object",
    "direction": "native upper-left back view",
    "feet": "two compact rear paws directly attached at baseline without leg segments",
    "freePaw": "screen-left near paw behind core; compact outer arc only and no equipment contact",
    "holdingPaw": "screen-right far paw behind core; only compact outer paw arc may remain visible",
    "ringPommel": "small oblique edge-on ring immediately outside or touching the screen-right shoulder silhouette",
    "scabbard": "fully sheathed and mostly or completely hidden behind the capsule; no isolated lower fragment"
  },
  "visibleAreaCaps": {
    "equipment": 0.22,
    "far_hand": 0.28,
    "near_hand": 0.28
  },
  "weapon": {
    "exitWindow": [
      148,
      139,
      172,
      177
    ],
    "guardWindow": [
      149,
      151,
      169,
      181
    ],
    "hiddenGrip": [
      159,
      158
    ],
    "maxBladeWidthPx": 13,
    "maxGemAreaPx": 0,
    "tipRegion": [
      126,
      194,
      151,
      228
    ]
  }
}
```

## Unresolved fixes
- Semantic mask authoring used noncanonical colors and mislabeled the lower-right core, causing missing-foot and core-disconnection failures.
- Mask background alpha was accidentally opaque, invalidating subject isolation and geometry checks.
- deterministic report must pass before Model Reviewer
- final automatic image generation round

## Base prompt
# Five-Red Chow Poet — Corrected Idle UL with Sheathed Ring-Pommel Tang Dao

Create exactly one native upper-left-facing back-view sprite of the approved five-red chow poet on a pure `#00ff00` background. Keep the approved poet identity: orange five-red chow palette, two triangular ears, fluffy tail, compact rounded capsule core, and four compact paws.

Use the approved isolated UL Tang dao only for weapon identity and its three-dimensional carried axis. The mounted result must show one fully sheathed Tang dao behind the body. At the screen-right shoulder, expose only a small oblique ring seen more edge-on than the isolated reference, a short connected grip, and optionally one connected scabbard-throat cue. The body may hide the entire remaining sheath. Do not force any lower scabbard or end-cap to remain visible.

## Hard topology

- Native upper-left back view; no mirrored or rotated DR body.
- Compact rounded capsule core matching the approved poet DR volume; not pear-shaped, narrow-column, or squat.
- No eyes, nose, mouth, muzzle, or frontal face on the back of the head.
- Exactly four compact paw masses: two front paws and two rear foot paws.
- Every paw attaches directly to the capsule through a broad multi-pixel overlap.
- Absolutely no arm, forearm, leg, lower-leg, thin connector, floating paw, or single-pixel joint.
- Both front paws are behind the capsule core. Only small outer paw arcs may remain visible.
- The screen-left free paw does not touch the weapon.
- Both rear paws attach directly at the baseline; their upper portions are covered by the body.

## Depth order

1. One fully sheathed Tang dao behind the body.
2. Both compact front paws behind the body.
3. Main head-and-body capsule over their inner portions.
4. Two compact rear paws at the bottom, partially covered by the capsule.

The ring, grip, and optional throat cue must read as one connected shoulder-side object. Never draw a second disconnected lower weapon fragment. Keep the small ring oblique and more edge-on, not a large frontal circle. Keep the pose relaxed and low.

## Output

- One complete centered character on 256×256.
- Foot baseline near `y=236`.
- Pure bright green `#00ff00` background.
- Bold dark outline, flat values, low interior detail, cute-dark Pure Run cartoon language.
- Safe margin around ears, tail, feet, and the small ring.

## Exclude

No guqin; no wineskin or gourd; no exposed blade; no second weapon; no disconnected weapon pieces; no required lower-scabbard visibility; no face on the back; no front-layer front paws; no visible limbs; no mirrored DR sprite; no text, floor, shadow, border, realistic material, engraving, gradients, or effects.

