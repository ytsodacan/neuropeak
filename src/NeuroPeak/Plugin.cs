using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using NeuroPeak.Core;
using NeuroPeak.Patches;
using NeuroSdk;

namespace NeuroPeak
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class NeuroPeakPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.sillyprootsoda.neuropeak";
        public const string PluginName = "Neuro PEAK";
        public const string PluginVersion = "0.1.0";
        public const string NeuroGameName = "PEAK";

        public static NeuroPeakPlugin? Instance { get; private set; }
        public static ManualLogSource? Log { get; private set; }
        public static NeuroPeakConfig? Settings { get; private set; }

        private Harmony? _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Settings = new NeuroPeakConfig(Config);

            NeuroSdkSetup.Initialize(NeuroGameName);

            _harmony = new Harmony(PluginGuid);
            PeakPatchInstaller.ApplyAll(_harmony);

            NeuroPeakRuntime.Create();

            Logger.LogInfo($"{PluginName} {PluginVersion} ready, reporting to Neuro as \"{NeuroGameName}\"");
        }

        private void OnDestroy()
        {
            NeuroPeakRuntime.Destroy();
            _harmony?.UnpatchSelf();
            _harmony = null;
            Instance = null;
        }
    }
}
