using BepInEx.Configuration;
using Jotunn.Configs;

namespace FloatingBridge
{
    internal static class BridgeConfig
    {
        private static ConfigEntry<float> _deckHeight = null!;
        private static ConfigEntry<float> _minWaterDepth = null!;

        /// <summary>Metres from calm sea level up to the deck surface of a newly placed pontoon.</summary>
        internal static float DeckHeight => _deckHeight.Value;
        internal static float MinWaterDepth => _minWaterDepth.Value;

        internal static void Bind(ConfigFile cfg)
        {
            // Admin-only entries are synced from the server by Jotunn, so every builder places decks at one height.
            _deckHeight = cfg.Bind("1 - Placement", "Deck height above water", 0.5f,
                new ConfigDescription(
                    "Metres from calm sea level up to the deck surface when a pontoon is placed. Waves ride on top of " +
                    "sea level, so raise this for clearance in storms. Only affects new placements.",
                    new AcceptableValueRange<float>(0f, 3f), AdminOnly()));
            _minWaterDepth = cfg.Bind("1 - Placement", "Minimum water depth", 0.3f,
                new ConfigDescription(
                    "Pontoons can only be placed where the sea bed is at least this many metres below sea level.",
                    new AcceptableValueRange<float>(0f, 10f), AdminOnly()));
        }

        private static ConfigurationManagerAttributes AdminOnly() => new() { IsAdminOnly = true };
    }
}
