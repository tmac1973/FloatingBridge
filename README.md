# FloatingBridge

> [!NOTE]
> **This is a small, low-effort mod, not a polished one.** It does one thing, has only been tested in single player so far, and reuses vanilla art: the pontoon is the wood floor with two log poles stuck under it. Expect rough edges.

Build bridges across lakes and bays. FloatingBridge adds a **Pontoon** to the hammer: a 2×2 wooden deck on two logs that sits on the water. Water holds it up the way the ground holds up a foundation, so you can build floors, railings and walls off it.

## Requirements

- [Jotunn](https://valheim.hexium.gg/mods/ValheimModding/Jotunn)

**Install on the server and on every client.** The pontoon is a new build piece, so a player without the mod can't see it or load it, and Jotunn won't let them join.

## Usage

1. Equip the hammer and stand near a workbench.
2. Open the build menu: **Building → Pontoon** (4 Wood, 2 Resin).
3. Aim at the water. The ghost sits on the surface at the configured deck height, wherever you aim. Aim at an existing pontoon to snap the next one on.
4. Place it. It can't be placed on dry land or in water shallower than the minimum depth.

What a pontoon does:

- **Holds itself up.** It always has full support, so it never collapses. Pieces built off it get their support from it, exactly as they would from a wood floor resting on the ground.
- **Never rots.** It takes no wear from rain or from being under water. Other damage (weapons, monsters, fire) still applies, and it can be repaired as usual.
- **Doesn't move.** It's a normal static building piece that stays put whatever the waves do.

## Configuration

`BepInEx/config/Spronglehump.FloatingBridge.cfg`, or in-game with a configuration manager (F1).

| Setting | Default | Server-synced | Description |
|---|---|---|---|
| Deck height above water | `0.5` | yes | Metres from calm sea level up to the deck surface when placing. Waves ride on top of sea level, so raise this if storms wash over your bridge. Only affects new placements. |
| Minimum water depth | `0.3` | yes | How far below sea level the sea bed must be for a pontoon to be placed there. |

## Notes and limitations

- **Removing the mod breaks your bridges.** Without the mod, placed pontoons don't load (they stay in the save and come back if you reinstall), and anything they were holding up collapses, for good, the next time that area loads. Take bridges down before you uninstall.
- Boats can't pass under a pontoon bridge. Leave a gap, or build a raised section on land.
- In heavy storms, high waves can wash over a low deck and you'll swim. To avoid that, raise *Deck height above water*. The height is fixed when a pontoon is placed, so changing the setting doesn't move pontoons you've already built: to raise an existing bridge, take its pontoons down and place them again.
- All Valheim water is at the same sea level, so this works the same in lakes, rivers and the ocean.

## Source

<https://github.com/tmac1973/FloatingBridge>
