using BepInEx;
using BepInEx.Logging;
using FloatingBridge.Game;
using FloatingBridge.Testing;
using HarmonyLib;
using Jotunn.Utils;

namespace FloatingBridge
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // The pontoon is a new prefab: a client without the mod can't load or see it.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "Spronglehump.FloatingBridge";
        public const string Name = "FloatingBridge";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log = null!;

        private void Awake()
        {
            Log = Logger;
            BridgeConfig.Bind(Config);
            PontoonPiece.Register();
            new Harmony(Guid).PatchAll(typeof(PlacementPatches));
            BridgeTests.Register(this);
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}
