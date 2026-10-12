using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace FloatingBridge.Game
{
    /// <summary>
    /// The pontoon: a clone of the 2x2 wood floor with logs underneath, which water holds up and never rots.
    /// </summary>
    internal static class PontoonPiece
    {
        // Saved in every placed pontoon's ZDO: never rename.
        internal const string PrefabName = "FloatingBridge_Pontoon";
        private const string DeckPrefab = "wood_floor";
        private const string FloatPrefab = "wood_pole_log";
        internal const string FloatsName = "pontoon_floats";

        /// <summary>Height of the deck surface above the prefab root, measured from the deck mesh.</summary>
        internal static float DeckTopLocalY { get; private set; }

        internal static void Register()
        {
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", new Dictionary<string, string>
            {
                ["piece_fb_pontoon"] = "Pontoon",
                ["piece_fb_pontoon_desc"] = "A floating deck. Place it on water; floors and railings can be built off it.",
            });
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;

            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(PrefabName, DeckPrefab);
            if (prefab == null)
            {
                Plugin.Log.LogError($"Vanilla prefab {DeckPrefab} not found; the pontoon is unavailable");
                return;
            }

            var wnt = prefab.GetComponent<WearNTear>();
            // Rain and being under water both go through m_noRoofWear in UpdateWear.
            wnt.m_noRoofWear = false;
            // Skips UpdateSupport, so the pontoon keeps the max support it starts with: it behaves like a piece on the
            // ground, and floors built off it get their support from it.
            wnt.m_noSupportWear = false;

            Transform deck = wnt.m_new != null ? wnt.m_new.transform : prefab.transform;
            Bounds deckBounds = PrefabGeometry.MeshBounds(prefab.transform, deck);
            DeckTopLocalY = deckBounds.max.y;

            AddFloats(prefab, deckBounds);

            var config = new PieceConfig
            {
                Name = "$piece_fb_pontoon",
                Description = "$piece_fb_pontoon_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Building,
                CraftingStation = CraftingStations.Workbench,
            };
            config.AddRequirement(new RequirementConfig("Wood", 4, 0, true));
            config.AddRequirement(new RequirementConfig("Resin", 2, 0, true));
            PieceManager.Instance.AddPiece(new CustomPiece(prefab, false, config));

            // If the render fails, the piece keeps the wood floor's icon.
            Sprite? icon = RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);
            if (icon != null)
                prefab.GetComponent<Piece>().m_icon = icon;
            else
                Plugin.Log.LogWarning("Pontoon icon render failed; using the wood floor icon");

            Plugin.Log.LogInfo($"Pontoon registered: deck {deckBounds.size.x:0.00}x{deckBounds.size.z:0.00}, " +
                               $"top {DeckTopLocalY:0.00} above root");
        }

        /// <summary>
        /// Two logs lying under the deck, for looks only: no colliders, so they change nothing about how it plays.
        /// </summary>
        private static void AddFloats(GameObject prefab, Bounds deckBounds)
        {
            GameObject? log = PrefabManager.Instance.GetPrefab(FloatPrefab);
            WearNTear? logWnt = log != null ? log.GetComponent<WearNTear>() : null;
            if (log == null)
            {
                Plugin.Log.LogWarning($"Vanilla prefab {FloatPrefab} not found; pontoon has no floats");
                return;
            }
            Transform logVisual = logWnt != null && logWnt.m_new != null ? logWnt.m_new.transform : log.transform;

            var floats = new GameObject(FloatsName);
            floats.layer = prefab.layer;
            floats.transform.SetParent(prefab.transform, false);

            foreach (float side in new[] { -0.25f, 0.25f })
            {
                GameObject one = PrefabGeometry.CopyMeshes(log.transform, logVisual, floats.transform);
                one.name = "float";
                PrefabGeometry.LayAlongX(one.transform, deckBounds.size.x);

                // Tuck the log under the deck, a little overlapped so there's no visible gap.
                Bounds b = PrefabGeometry.MeshBounds(prefab.transform, one.transform);
                var target = new Vector3(deckBounds.center.x, deckBounds.min.y - b.extents.y + 0.05f,
                    deckBounds.center.z + side * deckBounds.size.z);
                one.transform.localPosition += target - b.center;
            }

            // Draw and cull the logs with the deck; outside its LODGroup they'd still show after the deck is culled.
            var lodGroup = prefab.GetComponent<LODGroup>();
            if (lodGroup != null)
            {
                Renderer[] logs = floats.GetComponentsInChildren<Renderer>(true);
                LOD[] levels = lodGroup.GetLODs();
                for (int i = 0; i < levels.Length; i++)
                    levels[i].renderers = levels[i].renderers.Concat(logs).ToArray();
                lodGroup.SetLODs(levels);
            }
        }
    }
}
