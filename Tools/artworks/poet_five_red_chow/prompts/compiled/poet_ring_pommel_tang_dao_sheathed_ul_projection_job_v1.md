# Pure Run Equipment ImageGen Task Packet

## Category
- weapon

## Frozen base style
- clean readable silhouette at 128px
- crisp controlled outline matching approved Pure Run class weapons
- restrained flat material shading with only a few deliberate value regions
- single isolated object on chroma green with no cast shadow

## Category rules
- use the approved diagonal inventory orientation
- keep edges bold and mechanically readable

## Reference responsibilities
- character-style-anchor: Tools/artworks/doge/calibrated/doge_capsule_hunter_color_calibrated_v01.png @ 68a35da9d5d646d497ae7bf99eebc5871bcc5be26a893441d9fcece57877ac07
- weapon-category-anchor: Tools/artworks/pure_run/equipment/approved/pure_run_equipment_necromancer_dagger_v01.png @ 7b5854a12e2515cabab1f8e263e444e2407d2e77fcfd7c808fb4314b1e837123

## Negative constraints
- no photorealistic rendering
- no glossy cinematic lighting or excessive specular highlights
- no airbrushed gradients, bloom, depth of field, text, border, or watermark
- do not copy layout, UI frame, or background from a third-party reference

## Feedback delta
- none

## Base prompt
# Isolated Ring-Pommel Tang Dao — Native UL 3D Projection

Create exactly one isolated equipment sprite: the same approved, completely sheathed ring-pommel Tang dao shown in the bound identity reference, redrawn after a real 180-degree character turn in 3D under the fixed Pure Run isometric camera.

## Preserve exactly

- Thick empty circular ring pommel.
- Short dark-red wrapped grip.
- Compact guard and distinct scabbard throat.
- Straight, complete dark red-brown scabbard and restrained brass fittings.
- Bold dark outline, flat color blocks, low interior detail, cute-dark cartoon rendering.
- Blade remains completely inside the scabbard.

## New UL spatial projection

- This is a native opposite-view redraw, not transformed source pixels.
- Imagine the approved weapon physically held in the same low relaxed 3D carry while the wielder turns from down-right to up-left.
- Reproject the physical weapon under the same orthographic isometric camera.
- Screen reading for isolated review: ring/grip endpoint on the upper-right; scabbard extends toward the lower-left.
- The apparent full axis must be shorter and shallower than the approved DR screen axis because the ground-direction component reverses while the physical downward pitch remains downward.
- Show believable foreshortening in the grip, guard, throat, ring ellipse, fittings spacing, scabbard width and end cap. Do not merely mirror, rotate, skew, or non-uniformly squash the DR bitmap.
- Keep every part unobstructed in this isolated study. Character-body occlusion will be designed only after this equipment projection is approved.

## Composition

- One weapon only, centered on a 256×256 canvas with generous transparent-safe margins.
- Pure bright green `#00ff00` background for deterministic chroma removal.
- No cast shadow, floor, hand, paw, character, clothing, guqin, wineskin, exposed blade, text, border, second weapon or decorative effects.

## Reject

- A horizontal mirror of the source.
- The original DR upper-left-to-lower-right axis.
- A flat 2D rotation with unchanged ring/guard/fittings perspective.
- Curved or saber-like blade/scabbard.
- Realistic metal, gradients, engraving, texture noise, extra tassels or ornamentation.

