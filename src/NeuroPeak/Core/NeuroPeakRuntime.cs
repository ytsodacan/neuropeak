using NeuroPeak.Actions;
using NeuroPeak.Perception;
using NeuroPeak.Voice;
using UnityEngine;

namespace NeuroPeak.Core
{
    public static class NeuroPeakRuntime
    {
        private const string RuntimeObjectName = "NeuroPeakRuntime";

        private static GameObject? _runtime;

        public static GameObject? Runtime => _runtime;

        public static void Create()
        {
            if (_runtime != null) return;

            _runtime = new GameObject(RuntimeObjectName) { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(_runtime);

            _runtime.AddComponent<MainThreadCommandQueue>();
            _runtime.AddComponent<PeakStateTracker>();
            _runtime.AddComponent<PeakIntentDriver>();
            _runtime.AddComponent<NeuroPeakActionRegistry>();
            _runtime.AddComponent<EnvironmentReporter>();
            _runtime.AddComponent<WeatherReporter>();

            InstallVoiceBridge();
            InstallVoiceTransmitSink();
        }

        public static void Destroy()
        {
            if (_runtime == null) return;

            Object.Destroy(_runtime);
            _runtime = null;
        }

        private static void InstallVoiceBridge()
        {
#if NEUROSDK_VOICE
            _runtime!.AddComponent<PeakVoiceBridge>();
#else
            NeuroPeakPlugin.Log?.LogInfo(
                "The Neuro SDK build in use has no voice side-channel, so PEAK's voice chat is not bridged. See README.md, \"Voice chat\".");
#endif
        }

        private static void InstallVoiceTransmitSink()
        {
#if PEAK_PHOTON_VOICE
            PhotonVoiceTransmitSink.TryInstall();
#else
            NeuroPeakPlugin.Log?.LogInfo(
                "Built without the Photon Voice references, so Neuro's voice is buffered instead of transmitted. See README.md, \"Voice chat\".");
#endif
        }
    }
}
