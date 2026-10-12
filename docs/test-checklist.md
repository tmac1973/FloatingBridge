# FloatingBridge test checklist

## Automated (in game, through ModTestBridge)

From anywhere in the world. If there's no water within 50 m, the run teleports to the nearest shore (found from
world-generator heights, up to 2 km) and back again at the end:
```
mtb cmd devcommands          # only if not already on
mtb run "fb_test all" "[TEST] done" 300
```

| Test | Checks |
|---|---|
| `prefab` | Registered, on the hammer, recipe and icon, wear flags off, has colliders, floats have none |
| `placement_height` | Deck surface lands at sea level + *Deck height above water*; water accepted, dry land refused |
| `support_chain` | 40 m up in the air: pontoon keeps max support and holds two floors in a row; a lone floor breaks |
| `no_water_wear` | Submerged, one minute of wear: a control wood floor takes damage, the pontoon none |
| `ghost_on_water` | Standing on a pontoon with a hammer, the real placement ghost: at deck height and valid over open water; aimed just past the edge, it snaps flush (2 m over) at the same height |

`fb_dump [prefab]` logs a prefab's hierarchy, LOD levels and renderer modes, which is useful after a game update changes
`wood_floor`. `fb_shot [count] [frames apart]` saves screenshots to `BepInEx/FloatingBridge/shots/` for diagnosing visual
problems; delete them afterwards.

## By hand (needs eyes)

| Row | Do | Expect |
|---|---|---|
| H-1 Ghost | Select Pontoon, aim at water near and far, then at the shore | Ghost rides the surface at deck height; red over land and shallows |
| H-2 Snap | Place one, aim at its edge | Next one snaps flush, same height |
| H-3 Build off it | Wood floor beside a pontoon; railing on top; check the support colours with the hammer | Pontoon blue; the floor next to it holds |
| H-4 Walk | Walk the bridge in calm weather and in a storm | Deck is solid underfoot; storm waves only wash over when the deck is low |
| H-5 Look | Look at it from water level and from above | Logs sit under the deck; no gaps or z-fighting; icon looks like a pontoon |
| H-6 Persist | Log out and in; restart the game | Pontoons still there, still at full health |

## Multiplayer

Run `scripts/run-dedicated-server.sh` and join `127.0.0.1:2456` (password `floatingbridge`).

| Row | Setup | Expect |
|---|---|---|
| MP-1 Build | Server with mod; place a bridge; relog | Bridge persists; the server log is clean |
| MP-2 Sync | Server config *Deck height above water* = 1.0 | Client places at 1.0 and the client config shows it read-only (non-admin) |
| MP-3 No mod client | Client without the mod joins | Jotunn refuses the join with a clear message |
| MP-4 Second player | Friend joins and walks your bridge | They see the same deck height; no rubber-banding |
