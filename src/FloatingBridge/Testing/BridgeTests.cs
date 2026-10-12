using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FloatingBridge.Game;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FloatingBridge.Testing
{
    /// <summary>
    /// In-game tests, run through ModTestBridge: <c>mtb run "fb_test all" "[TEST] done" 300</c>.
    ///   fb_test list | &lt;name&gt; | all     tests; one "[TEST] result" line each, then "[TEST] done passed=N failed=M"
    ///   fb_dump [prefab]                  log a prefab's hierarchy (default: the pontoon)
    /// Tests build near the local player and remove what they built.
    /// </summary>
    internal static class BridgeTests
    {
        private delegate IEnumerator Test(Checks checks);

        private static readonly Dictionary<string, Test> Tests = new()
        {
            ["prefab"] = Prefab,
            ["placement_height"] = PlacementHeight,
            ["support_chain"] = SupportChain,
            ["no_water_wear"] = NoWaterWear,
            ["ghost_on_water"] = GhostOnWater,
        };

        private static MonoBehaviour _host = null!;
        private static bool _running;

        internal static void Register(MonoBehaviour host)
        {
            _host = host;
            new Terminal.ConsoleCommand("fb_test", "[name|all|list] run FloatingBridge tests", args =>
            {
                string which = args.Length > 1 ? args[1] : "list";
                if (which == "list")
                {
                    args.Context.AddString("tests: " + string.Join(", ", Tests.Keys));
                    return;
                }
                if (_running)
                {
                    args.Context.AddString("tests are already running");
                    return;
                }
                List<string> names = which == "all" ? Tests.Keys.ToList() : new List<string> { which };
                if (names.Any(n => !Tests.ContainsKey(n)))
                {
                    args.Context.AddString($"no test named {which}");
                    return;
                }
                args.Context.AddString($"running {string.Join(", ", names)}");
                _host.StartCoroutine(Run(names));
            });
            new Terminal.ConsoleCommand("fb_shot", "[count] [frames apart] save screenshots for diagnosis", args =>
            {
                int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 6;
                int apart = args.Length > 2 && int.TryParse(args[2], out int a) ? a : 2;
                _host.StartCoroutine(Shots(count, apart));
                args.Context.AddString($"capturing {count} shots");
            });
            new Terminal.ConsoleCommand("fb_dump", "[prefab] log a prefab's hierarchy", args =>
            {
                string name = args.Length > 1 ? args[1] : PontoonPiece.PrefabName;
                GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
                if (prefab == null)
                {
                    args.Context.AddString($"no prefab {name}");
                    return;
                }
                var sb = new StringBuilder($"[DUMP] {name}\n");
                Dump(prefab.transform, prefab.transform, 0, sb);
                Plugin.Log.LogInfo(sb.ToString());
                args.Context.AddString("dumped to log");
            });
        }

        private static readonly HashSet<string> NeedsWater = new() { "placement_height", "no_water_wear", "ghost_on_water" };

        private static IEnumerator Run(List<string> names)
        {
            _running = true;
            int passed = 0, failed = 0;
            Vector3? home = null;
            if (names.Any(NeedsWater.Contains) && !TryFindWater(out _))
            {
                if (TryFindShore(out Vector3 shore))
                {
                    home = Player.m_localPlayer.transform.position;
                    Plugin.Log.LogInfo($"[TEST] setup no water near, teleporting to shore at {shore}");
                    yield return Teleport(shore);
                }
                else
                {
                    Plugin.Log.LogWarning("[TEST] setup no shore found within 2 km");
                }
            }
            foreach (string name in names)
            {
                var checks = new Checks(name);
                // One test throwing must not stop the rest, so step the coroutine by hand.
                IEnumerator test = Tests[name](checks);
                while (true)
                {
                    object? current;
                    try
                    {
                        if (!test.MoveNext())
                            break;
                        current = test.Current;
                    }
                    catch (Exception e)
                    {
                        checks.That("no exception", false, e.Message);
                        break;
                    }
                    yield return current;
                }
                checks.CleanUp();
                bool pass = checks.Failed == 0;
                if (pass) passed++; else failed++;
                Log(pass, $"[TEST] result name={name} pass={(pass ? "true" : "false")} checks={checks.Count} failed={checks.Failed}");
            }
            if (home is Vector3 back)
            {
                Plugin.Log.LogInfo($"[TEST] setup teleporting back to {back}");
                yield return Teleport(back);
            }
            Log(failed == 0, $"[TEST] done passed={passed} failed={failed}");
            _running = false;
        }

        private static void Log(bool ok, string line)
        {
            if (ok) Plugin.Log.LogInfo(line); else Plugin.Log.LogWarning(line);
        }

        /// <summary>Each check is logged as it's made. Pieces spawned through it are removed when the test ends.</summary>
        private sealed class Checks
        {
            private readonly string _test;
            private readonly List<GameObject> _spawned = new();
            private readonly HashSet<ItemDrop> _dropsBefore = new(ItemDrop.s_instances);
            private Vector3? _site;
            public int Count, Failed;
            public Checks(string test) => _test = test;

            public void That(string what, bool ok, object? actual = null)
            {
                Count++;
                if (!ok) Failed++;
                Log(ok, $"[TEST] check name={_test} what=\"{what}\" pass={(ok ? "true" : "false")}" + (actual != null ? $" actual={actual}" : ""));
            }

            public GameObject Spawn(string prefabName, Vector3 pos, Quaternion rot)
            {
                GameObject go = Object.Instantiate(ZNetScene.instance.GetPrefab(prefabName), pos, rot);
                go.GetComponent<WearNTear>()?.OnPlaced();
                _spawned.Add(go);
                _site ??= pos;
                return go;
            }

            public void CleanUp()
            {
                foreach (GameObject go in _spawned)
                    if (go != null)
                        ZNetScene.instance.Destroy(go);
                // Pieces that broke during the test drop their materials.
                if (_site is Vector3 site)
                    foreach (ItemDrop drop in ItemDrop.s_instances.ToList())
                        if (drop != null && !_dropsBefore.Contains(drop) && Vector3.Distance(drop.transform.position, site) < 30f)
                            ZNetScene.instance.Destroy(drop.gameObject);
            }
        }

        // --- the tests ------------------------------------------------------------------------------------------------

        private static IEnumerator Prefab(Checks checks)
        {
            GameObject? prefab = ZNetScene.instance.GetPrefab(PontoonPiece.PrefabName);
            checks.That("prefab registered", prefab != null);
            if (prefab == null)
                yield break;

            var wnt = prefab.GetComponent<WearNTear>();
            checks.That("no rain/water wear", !wnt.m_noRoofWear);
            checks.That("no support recompute", !wnt.m_noSupportWear);
            checks.That("still supports others", wnt.m_supports);

            GameObject? hammer = ObjectDB.instance.GetItemPrefab("Hammer");
            PieceTable? table = hammer?.GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces;
            checks.That("on the hammer", table != null && table.m_pieces.Contains(prefab));

            var piece = prefab.GetComponent<Piece>();
            checks.That("has icon", piece.m_icon != null);
            checks.That("has a recipe", piece.m_resources.Length > 0, piece.m_resources.Length);

            checks.That("deck top measured", PontoonPiece.DeckTopLocalY > -1f && PontoonPiece.DeckTopLocalY < 1f,
                PontoonPiece.DeckTopLocalY.ToString("0.000"));

            Collider[] all = prefab.GetComponentsInChildren<Collider>(true);
            checks.That("has colliders", all.Length > 0, all.Length);
            Transform? floats = prefab.transform.Find(PontoonPiece.FloatsName);
            int floatColliders = floats != null ? floats.GetComponentsInChildren<Collider>(true).Length : -1;
            checks.That("has floats, without colliders", floatColliders == 0, floatColliders);
        }

        private static IEnumerator PlacementHeight(Checks checks)
        {
            float water = ZoneSystem.instance.m_waterLevel;
            if (!FindWater(checks, out Vector3 site))
                yield break;

            site.y = PlacementPatches.DeckRootY();
            GameObject p = checks.Spawn(PontoonPiece.PrefabName, site, Quaternion.identity);
            yield return null;
            Transform deck = p.GetComponent<WearNTear>().m_new.transform;
            float top = PrefabGeometry.MeshBounds(p.transform, deck).max.y + p.transform.position.y;
            float want = water + BridgeConfig.DeckHeight;
            checks.That("deck top at configured height", Mathf.Abs(top - want) < 0.02f, $"{top:0.000} want {want:0.000}");
            checks.That("over water allowed", PlacementPatches.OverWater(site));

            checks.That("dry land found to test", TryFindLand(out Vector3 land));
            if (land != default)
                checks.That("dry land refused", !PlacementPatches.OverWater(land), $"ground={ZoneSystem.instance.GetGroundHeight(land):0.00}");
        }

        /// <summary>
        /// High in the air, where nothing else can hold it up: the pontoon stays put and carries a row of floors, while a
        /// floor on its own breaks.
        /// </summary>
        private static IEnumerator SupportChain(Checks checks)
        {
            Player player = Player.m_localPlayer;
            Quaternion rot = Quaternion.Euler(0f, Mathf.Round(player.transform.eulerAngles.y / 90f) * 90f, 0f);
            Vector3 right = rot * Vector3.right;
            // High enough that no tree, rock or hillside nearby can hold the control floor up.
            Vector3 origin = player.transform.position + Vector3.up * 40f + rot * Vector3.forward * 4f;
            checks.That("air around the test is clear",
                !Physics.CheckBox(origin - right * 3f, new Vector3(9f, 3f, 3f), rot), "something in the way");

            WearNTear p = checks.Spawn(PontoonPiece.PrefabName, origin, rot).GetComponent<WearNTear>();
            WearNTear f1 = checks.Spawn("wood_floor", origin + right * 2f, rot).GetComponent<WearNTear>();
            WearNTear f2 = checks.Spawn("wood_floor", origin + right * 4f, rot).GetComponent<WearNTear>();
            WearNTear alone = checks.Spawn("wood_floor", origin - right * 10f, rot).GetComponent<WearNTear>();

            float waited = 0f;
            while (alone != null && waited < 30f)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            checks.That("unsupported floor broke", alone == null, $"waited={waited:0.0}s");
            // Give the chain one more round of wear updates after the control broke.
            yield return new WaitForSeconds(3f);

            checks.That("pontoon still there", p != null);
            checks.That("pontoon at max support", p != null && p.GetSupport() >= 100f, p != null ? p.GetSupport().ToString("0.0") : "gone");
            checks.That("floor next to pontoon held", f1 != null, f1 != null ? f1.GetSupport().ToString("0.0") : "gone");
            checks.That("second floor out held", f2 != null, f2 != null ? f2.GetSupport().ToString("0.0") : "gone");
        }

        /// <summary>
        /// Under water, run the wear update a minute apart on a pontoon and on a wood floor. The floor's support wear is
        /// turned off so only wetness can hurt it.
        /// </summary>
        private static IEnumerator NoWaterWear(Checks checks)
        {
            if (!FindWater(checks, out Vector3 site))
                yield break;
            // Deep enough that wave troughs never uncover it (they reach about a metre below sea level near shore).
            // The water volume extends far below the sea bed, so being inside terrain doesn't matter here.
            site.y = ZoneSystem.instance.m_waterLevel - 4f;
            // WearNTear's underwater check uses a layer mask that only Floating.Awake or GetLiquidLevel sets up; in a
            // fresh session with nothing floating yet, nothing counts as under water.
            Floating.GetLiquidLevel(site);
            WearNTear p = checks.Spawn(PontoonPiece.PrefabName, site, Quaternion.identity).GetComponent<WearNTear>();
            WearNTear floor = checks.Spawn("wood_floor", site + Vector3.right * 6f, Quaternion.identity).GetComponent<WearNTear>();
            floor.m_noSupportWear = false;
            yield return null;

            checks.That("pontoon is wet", p.IsWet());
            checks.That("floor is wet", floor.IsWet());
            float t = Time.time;
            foreach (WearNTear w in new[] { p, floor })
            {
                w.UpdateWear(t);
                w.UpdateWear(t + 61f);
            }
            yield return null;   // health reaches m_healthPercentage through RPC_HealthChanged
            checks.That("wet floor took damage (control)", floor.GetHealthPercentage() < 1f, floor.GetHealthPercentage().ToString("0.000"));
            checks.That("wet pontoon took none", p.GetHealthPercentage() >= 1f, p.GetHealthPercentage().ToString("0.000"));
        }

        /// <summary>
        /// The real placement ghost, through the patched Player code: standing on a pontoon with a hammer, aim at open
        /// water, then just past the pontoon's edge so it snaps.
        /// </summary>
        private static IEnumerator GhostOnWater(Checks checks)
        {
            Player player = Player.m_localPlayer;
            if (!FindWater(checks, out Vector3 site))
                yield break;
            // A direction along the pontoon's axes with water to aim at.
            Vector3 dir = Vector3.zero;
            foreach (Vector3 d in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                if (PlacementPatches.OverWater(site + d * 3.5f) && PlacementPatches.OverWater(site + d * 2f))
                {
                    dir = d;
                    break;
                }
            checks.That("open water beside the site", dir != Vector3.zero);
            if (dir == Vector3.zero)
                yield break;

            float rootY = PlacementPatches.DeckRootY();
            site.y = rootY;
            checks.Spawn(PontoonPiece.PrefabName, site, Quaternion.identity);
            yield return null;

            Vector3 startPos = player.transform.position;
            Inventory inv = player.GetInventory();
            ItemDrop.ItemData? oldRight = player.GetRightItem();
            ItemDrop.ItemData? hammer = inv.AddItem("Hammer", 1, 1, 0, 0L, "", false);
            bool oldNoCost = player.m_noPlacementCost;
            int oldRotation = player.m_placeRotation;
            try
            {
                player.m_placeRotation = 0;   // the character's last build rotation carries over
                MovePlayer(player, site + Vector3.up * (PontoonPiece.DeckTopLocalY + 0.1f));
                checks.That("hammer added", hammer != null);
                if (hammer == null)
                    yield break;
                player.EquipItem(hammer);
                yield return new WaitForSeconds(0.5f);

                var piece = ZNetScene.instance.GetPrefab(PontoonPiece.PrefabName).GetComponent<Piece>();
                if (!player.SetSelectedPiece(piece))
                {
                    // Not known yet on this character: free build mode lists every piece.
                    player.m_noPlacementCost = true;
                    player.UpdateAvailablePiecesList();
                    yield return null;
                }
                checks.That("pontoon selected", player.SetSelectedPiece(piece) || IsPontoonGhost(player));

                yield return AimAt(player, site + dir * 3.5f + Vector3.up * (ZoneSystem.instance.m_waterLevel - rootY));
                GameObject? ghost = player.m_placementGhost;
                checks.That("ghost is the pontoon", IsPontoonGhost(player));
                if (ghost == null)
                    yield break;
                checks.That("open water: ghost at deck height", Mathf.Abs(ghost.transform.position.y - rootY) < 0.01f,
                    $"{ghost.transform.position.y:0.000} want {rootY:0.000}");
                checks.That("open water: placement valid", player.m_placementStatus == Player.PlacementStatus.Valid,
                    player.m_placementStatus);

                yield return AimAt(player, site + dir * 1.6f + Vector3.up * (ZoneSystem.instance.m_waterLevel - rootY));
                Vector3 want = site + dir * 2f;
                Vector3 got = ghost.transform.position;
                checks.That("edge: snapped flush beside the pontoon", Vector3.Distance(Flat(got), Flat(want)) < 0.05f,
                    $"({got.x - site.x:0.00},{got.z - site.z:0.00}) want ({dir.x * 2f:0.00},{dir.z * 2f:0.00})");
                checks.That("edge: ghost at deck height", Mathf.Abs(got.y - rootY) < 0.01f, $"{got.y:0.000}");
                checks.That("edge: placement valid", player.m_placementStatus == Player.PlacementStatus.Valid,
                    player.m_placementStatus);
            }
            finally
            {
                player.m_placeRotation = oldRotation;
                if (hammer != null)
                {
                    player.UnequipItem(hammer);
                    inv.RemoveItem(hammer);
                }
                if (oldRight != null && inv.ContainsItem(oldRight))
                    player.EquipItem(oldRight);
                if (player.m_noPlacementCost != oldNoCost)
                {
                    player.m_noPlacementCost = oldNoCost;
                    player.UpdateAvailablePiecesList();
                }
                MovePlayer(player, startPos);
            }
        }

        // --- helpers --------------------------------------------------------------------------------------------------

        private static bool FindWater(Checks checks, out Vector3 site)
        {
            if (TryFindWater(out site))
                return true;
            checks.That("water within 50 m (stand by a lake or the sea)", false);
            return false;
        }

        /// <summary>The nearest spot within 50 m with water at least 1.5 m deep and no pieces in the way.</summary>
        private static bool TryFindWater(out Vector3 site)
        {
            Vector3 center = Player.m_localPlayer.transform.position;
            float water = ZoneSystem.instance.m_waterLevel;
            int pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
            for (float r = 4f; r <= 50f; r += 3f)
            {
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI * 2f / 24f;
                    Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    p.y = water;
                    if (!ZoneSystem.instance.GetGroundHeight(p, out float ground) || ground > water - 1.5f)
                        continue;
                    if (Physics.CheckBox(p, new Vector3(5f, 2f, 5f), Quaternion.identity, pieceMask))
                        continue;
                    site = p;
                    return true;
                }
            }
            site = default;
            return false;
        }

        /// <summary>
        /// Dry land right next to deep water, nearest the player along rays out to 2 km. Uses world-generator heights,
        /// so it works for zones that aren't loaded (terrain edits are ignored).
        /// </summary>
        private static bool TryFindShore(out Vector3 shore)
        {
            const int rays = 32;
            Vector3 center = Player.m_localPlayer.transform.position;
            float water = ZoneSystem.instance.m_waterLevel;
            var lastLand = new Vector3?[rays];
            for (float r = 0f; r <= 2000f; r += 4f)
            {
                for (int i = 0; i < rays; i++)
                {
                    float a = i * Mathf.PI * 2f / rays;
                    Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    float h = WorldGenerator.instance.GetHeight(p.x, p.z);
                    if (h > water + 1f)
                    {
                        lastLand[i] = new Vector3(p.x, h, p.z);
                    }
                    else if (h < water - 3f && lastLand[i] is Vector3 land && Vector3.Distance(Flat(land), Flat(p)) <= 12f)
                    {
                        shore = land + Vector3.up;
                        return true;
                    }
                }
            }
            shore = default;
            return false;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        /// <summary>A spot within 50 m whose ground is at least 1 m above sea level.</summary>
        private static bool TryFindLand(out Vector3 land)
        {
            Vector3 center = Player.m_localPlayer.transform.position;
            float water = ZoneSystem.instance.m_waterLevel;
            for (float r = 0f; r <= 50f; r += 2f)
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI * 2f / 24f;
                    Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (ZoneSystem.instance.GetGroundHeight(p, out float ground) && ground > water + 1f)
                    {
                        land = p;
                        return true;
                    }
                }
            land = default;
            return false;
        }

        private static bool IsPontoonGhost(Player player) =>
            PlacementPatches.IsPontoon(player.m_placementGhost);

        private static void MovePlayer(Player player, Vector3 pos)
        {
            player.transform.position = pos;
            player.m_body.position = pos;
            player.m_body.linearVelocity = Vector3.zero;
        }

        /// <summary>
        /// Point the camera at <paramref name="target"/>. The camera orbits with the look pitch, so settle it over a
        /// few frames.
        /// </summary>
        private static IEnumerator AimAt(Player player, Vector3 target)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 dir = (target - GameCamera.instance.transform.position).normalized;
                player.SetLookDir(new Vector3(dir.x, 0f, dir.z));
                player.m_lookPitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;
                yield return new WaitForSeconds(0.15f);
            }
        }

        private static IEnumerator Teleport(Vector3 to)
        {
            Player player = Player.m_localPlayer;
            // Refused while another teleport (including the one on login) runs, and for 2 s after one.
            float tries = 0f;
            while (!player.TeleportTo(to, player.transform.rotation, distantTeleport: true) && tries < 30f)
            {
                yield return new WaitForSeconds(0.5f);
                tries += 0.5f;
            }
            float waited = 0f;
            // Terrain colliders arrive after the zone counts as loaded; ground probes miss until then.
            while ((player.IsTeleporting() || !ZoneSystem.instance.IsZoneLoaded(to)
                    || !ZoneSystem.instance.GetGroundHeight(to, out _)) && waited < 60f)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            yield return new WaitForSeconds(3f);   // let nearby zones and their water volumes load
            float off = Vector3.Distance(Flat(player.transform.position), Flat(to));
            if (off > 5f)
                Plugin.Log.LogWarning($"[TEST] setup teleport missed: at {player.transform.position}, {off:0.0} m from target");
        }

        private static IEnumerator Shots(int count, int apart)
        {
            string dir = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "FloatingBridge", "shots",
                DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            System.IO.Directory.CreateDirectory(dir);
            for (int i = 0; i < count; i++)
            {
                for (int f = 0; f < apart; f++)
                    yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"shot{i:00}.png"));
            }
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[SHOT] done dir={dir}");
        }

        private static void Dump(Transform root, Transform t, int depth, StringBuilder sb)
        {
            Vector3 lp = root.InverseTransformPoint(t.position);
            sb.Append(' ', depth * 2).Append(t.name)
              .Append($" active={t.gameObject.activeSelf} layer={LayerMask.LayerToName(t.gameObject.layer)} pos=({lp.x:0.00},{lp.y:0.00},{lp.z:0.00}) [");
            sb.Append(string.Join(", ", t.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name)));
            sb.Append("]");
            if (t.GetComponent<MeshRenderer>() is MeshRenderer mr)
                sb.Append($" shadows={mr.shadowCastingMode} motion={mr.motionVectorGenerationMode}");
            sb.Append("\n");
            if (t.GetComponent<LODGroup>() is LODGroup lods)
            {
                LOD[] levels = lods.GetLODs();
                for (int i = 0; i < levels.Length; i++)
                    sb.Append(' ', depth * 2 + 2).Append($"LOD{i} h={levels[i].screenRelativeTransitionHeight:0.000}: ")
                      .Append(string.Join(", ", levels[i].renderers.Where(r => r != null).Select(r => $"{r.transform.parent?.name}/{r.name}")))
                      .Append('\n');
            }
            foreach (Transform child in t)
                Dump(root, child, depth + 1, sb);
        }
    }
}
