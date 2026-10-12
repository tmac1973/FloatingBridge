using FloatingBridge.Core;
using HarmonyLib;
using UnityEngine;

namespace FloatingBridge.Game
{
    /// <summary>Puts the pontoon's placement ghost on the water surface, at the configured deck height.</summary>
    [HarmonyPatch]
    internal static class PlacementPatches
    {
        /// <summary>
        /// Aim at the water surface rather than through it to the sea bed. Vanilla only does this for m_waterPiece,
        /// which would also refuse placement when aiming at a pontoon to snap to it.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.PieceRayTest))]
        [HarmonyPrefix]
        private static void AimAtWater(Player __instance, ref bool water)
        {
            if (IsPontoon(__instance.m_placementGhost))
                water = true;
        }

        /// <summary>After vanilla has placed and snapped the ghost, keep its X/Z and set its height.</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        [HarmonyPostfix]
        private static void SnapToWater(Player __instance)
        {
            GameObject ghost = __instance.m_placementGhost;
            if (ghost == null || !ghost.activeSelf || !IsPontoon(ghost))
                return;

            float rootY = DeckRootY();
            SetY(ghost, rootY);
            // Vanilla snapped while the ghost was still at the water-hit height, about 0.3 m off a neighbouring deck, so
            // nearby snap points were often out of reach. Snap again at deck height, as vanilla does.
            if (!AltPlaceHeld(__instance))
            {
                __instance.m_tempPieces.Clear();
                if (__instance.FindClosestSnapPoints(ghost.transform, 0.5f, out Transform a, out Transform b, __instance.m_tempPieces))
                {
                    Vector3 snapped = b.position - (a.position - ghost.transform.position);
                    if (!__instance.IsOverlappingOtherPiece(snapped, ghost.transform.rotation, ghost.name,
                            __instance.m_tempPieces, ghost.GetComponent<Piece>().m_allowRotatedOverlap))
                    {
                        ghost.transform.position = snapped;
                        SetY(ghost, rootY);
                    }
                }
            }

            if (!OverWater(ghost.transform.position) && __instance.m_placementStatus == Player.PlacementStatus.Valid)
                __instance.m_placementStatus = Player.PlacementStatus.Invalid;
            __instance.SetPlacementGhostValid(__instance.m_placementStatus == Player.PlacementStatus.Valid);
        }

        private static void SetY(GameObject ghost, float y)
        {
            Vector3 p = ghost.transform.position;
            p.y = y;
            ghost.transform.position = p;
        }

        /// <summary>Same test as the start of Player.UpdatePlacementGhost: alt-place turns snapping off.</summary>
        private static bool AltPlaceHeld(Player player) =>
            ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive()
                ? player.m_altPlace
                : ZInput.GetButton("AltPlace") || (ZInput.GetButton("JoyAltPlace") && !ZInput.GetButton("JoyRotate"));

        internal static float DeckRootY() =>
            PontoonMath.DeckRootY(ZoneSystem.instance.m_waterLevel, BridgeConfig.DeckHeight, PontoonPiece.DeckTopLocalY);

        /// <summary>False where there's no terrain under <paramref name="pos"/> to measure, e.g. not loaded yet.</summary>
        internal static bool OverWater(Vector3 pos) =>
            ZoneSystem.instance.GetGroundHeight(pos, out float ground)
            && PontoonMath.HasEnoughWater(ground, ZoneSystem.instance.m_waterLevel, BridgeConfig.MinWaterDepth);

        // The ghost is an instance of the prefab, named "<prefab>(Clone)"; vanilla compares ghost names the same way.
        internal static bool IsPontoon(GameObject? go) => go != null && go.name.StartsWith(PontoonPiece.PrefabName);
    }
}
