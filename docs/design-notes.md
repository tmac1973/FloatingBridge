# Design notes

Verified against `assembly_valheim` decompiled with ilspycmd (game build installed 2026-10-11). Re-check the names
below after a major Valheim update.

## Version 1: how it works

### Support: `WearNTear.m_noSupportWear = false`, no patch
- `WearNTear.UpdateWear` only calls `UpdateSupport()` when `m_noSupportWear` is true.
- `m_support` starts at `GetMaxSupport()` in `Awake`, and `GetSupport()` reads `m_support` (owner) or the ZDO's
  `s_support` with `GetMaxSupport()` as the default (others).
- So with the flag off, a pontoon reports max support forever and never breaks for lack of it. Neighbours run their
  own `UpdateSupport`, find the pontoon's collider, and take support from `GetSupport()` as they would from any piece.
- Material stays `Wood` (max 100, min 10, horizontal loss 0.2/m, vertical loss 0.125/m), so a floor attached beside
  it gets about 58. That's the same as beside a wood floor resting on the ground. A floor beside a *stone* foundation
  gets 100, because it is capped at its own wood max. Switching `m_materialType` to `Stone` would give that, but it
  also changes hit effects, damage modifiers and `m_notOnWood` checks, so it's not done.
- This is unconditional, not "when near water". Placement is the only thing that keeps pontoons on water. Something
  that spawns a pontoon elsewhere, like another building mod or `spawn`, gets a floating deck.

### No water wear: `WearNTear.m_noRoofWear = false`, no patch
`UpdateWear` adds rain damage (5% every 60 s at above 50% health) inside `if (m_noRoofWear && !shielded && ...)`, and
`IsWet()` is `m_rainWet || IsUnderWater()`. Turning the flag off removes both rain and submerged wear, and the
`m_wet` visual.

### Placement: two `Player` patches
- `Player.PieceRayTest(..., bool water)`: a prefix forces `water = true` for the pontoon ghost. The ray then uses
  `m_placeWaterRayMask` (which includes the `Water` layer) and stops at the surface, not the sea bed.
- Setting `Piece.m_waterPiece` would have done that too. But then `UpdatePlacementGhost` marks the placement Invalid
  whenever the ray hits something other than water, such as the pontoon you're snapping to, and it lifts the ghost 3 m.
- `Player.UpdatePlacementGhost`: a postfix keeps vanilla's X/Z (including snap-point snapping) and sets
  `y = ZoneSystem.m_waterLevel (30) + deck height - deck top above root`. If `ZoneSystem.GetGroundHeight` is above
  `water - min depth`, it sets `m_placementStatus = Invalid`. Then it calls `SetPlacementGhostValid` again.
- Vanilla snaps to neighbouring snap points *before* the postfix runs. At that point the ghost sits at the water-hit
  height, about 0.3 m below a neighbouring deck, so the 3D snap distance usually misses the 0.5 m radius. The postfix
  therefore repeats vanilla's snap (`FindClosestSnapPoints`, `IsOverlappingOtherPiece`, skipped while alt-place is
  held) at deck height. An A/B run of `ghost_on_water` confirmed it: 1.66 m (no snap) without it, 2.00 m with it.
- The ground probe uses `ZoneSystem.GetGroundHeight(p, out h)`. The single-argument version silently returns `p.y`
  when no terrain is loaded under `p`.
- `Player.PlacePiece` uses the ghost's transform, so the adjusted height is what gets built.

### Floats
- Two copies of `wood_pole_log`'s meshes (`log (1)` and `low (1)`: both in its only LOD level, so both are drawn)
  are laid along X under the deck. They have no colliders.
- They are added to every level of the pontoon's `LODGroup`, so they cull with the deck at a distance.

### Visual bob: tried and removed (2026-10-12)
V1 briefly had a visual-only bob. Colliders were moved into a static child, and only the meshes moved up and down
with the waves. In game, the planks flickered darker and lighter, each independently, but only while the deck moved.
- Ruled out: jitter in the bob (a per-frame trace was smooth), self-shadow shimmer (*Cast shadows* off changed
  nothing), and SSAO or temporal upscaling motion vectors (SSAO off, other upscaling, and per-object motion vectors
  changed nothing).
- Screenshot diffs show each plank's own shading changing in smooth blotches as the deck moves, with no second
  surface showing through. Most likely Valheim's building shader shades by world position (the game binds a 3D
  wear texture for buildings), so the shading slides across a moving mesh. This was not confirmed in the shader.
- **Consequence for V2:** any visibly moving deck built from vanilla building materials will shimmer. Real bobbing
  needs its own material, e.g. the ship materials, or a custom one.

## Version 2 notes: real per-segment bobbing

What the decompile says about computing waves locally:
- `WaterVolume.GetWaterSurface(point)` = `transform.y + CalcWave(point, depth, s_wrappedDayTimeSeconds, ...)`.
- `s_wrappedDayTimeSeconds` comes from `ZNet.GetWrappedDayTimeSeconds()`, which is server-synced time.
- `depth` comes from the heightmap's ocean depth, which is world-generated and deterministic.
- Wind comes from `EnvMan.GetWindData`. **Still to verify:** that EnvMan's wind is purely a function of synced time and
  the world seed on every client. Weather transitions are seeded, but check for any `Random` without a fixed seed.
- `WaterVolume.s_waterTime` is client-smoothed (`Lerp` towards day time), but it only drives the shader. Gameplay
  wave height uses `s_wrappedDayTimeSeconds`.
- So each client can probably compute the same wave height with no network traffic, provided wind checks out.

Risks still standing for V2:
- Building pieces have no transform sync, and the support cache keys on collider positions (see above). Moving
  colliders means patching `UpdateSupport`'s cache check, or keeping colliders static and moving only players.
- Players on a kinematic moving collider: Valheim's ship handling (`Character` and `Ship`) is the model for this, and
  ValheimRAFT (zolantris fork) shows the patches.
- Long bridges crossing zone boundaries, partly loaded: neighbouring segments may be owned by different clients.
