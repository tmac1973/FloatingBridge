namespace FloatingBridge.Core
{
    /// <summary>Placement maths, kept free of Unity types so it can be unit tested.</summary>
    internal static class PontoonMath
    {
        /// <summary>
        /// Y for the pontoon's root so that its deck surface (at <paramref name="deckTopLocalY"/> above the root) sits
        /// <paramref name="deckHeight"/> above the calm sea level.
        /// </summary>
        public static float DeckRootY(float waterLevel, float deckHeight, float deckTopLocalY) =>
            waterLevel + deckHeight - deckTopLocalY;

        /// <summary>True when the sea bed is at least <paramref name="minDepth"/> below the calm sea level.</summary>
        public static bool HasEnoughWater(float groundHeight, float waterLevel, float minDepth) =>
            groundHeight <= waterLevel - minDepth;
    }
}
