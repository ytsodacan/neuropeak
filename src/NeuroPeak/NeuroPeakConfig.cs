using BepInEx.Configuration;

namespace NeuroPeak
{
    public sealed class NeuroPeakConfig
    {
        public ConfigEntry<bool> MovementEnabled { get; }
        public ConfigEntry<bool> PerceptionEnabled { get; }
        public ConfigEntry<bool> VoiceChatEnabled { get; }
        public ConfigEntry<float> PerceptionScanInterval { get; }
        public ConfigEntry<float> AmbientContextMinInterval { get; }
        public ConfigEntry<float> UrgentContextMinInterval { get; }
        public ConfigEntry<float> NearbyScanRadius { get; }
        public ConfigEntry<float> DangerousDropMeters { get; }
        public ConfigEntry<float> LowStaminaFraction { get; }
        public ConfigEntry<float> VoiceActivityThreshold { get; }

        public NeuroPeakConfig(ConfigFile file)
        {
            MovementEnabled = file.Bind("Control", "MovementEnabled", true,
                "Allow Neuro to drive the local player's movement and climbing.");
            PerceptionEnabled = file.Bind("Perception", "PerceptionEnabled", true,
                "Send natural language descriptions of the surroundings to Neuro.");
            VoiceChatEnabled = file.Bind("Voice", "VoiceChatEnabled", true,
                "Bridge PEAK's voice chat to the Neuro voice side-channel.");
            PerceptionScanInterval = file.Bind("Perception", "ScanInterval", 0.4f,
                "Seconds between environment scans. Scans are cheap; sending is gated by change detection.");
            AmbientContextMinInterval = file.Bind("Perception", "AmbientMinInterval", 6f,
                "Minimum seconds between two ambient (silent) description messages.");
            UrgentContextMinInterval = file.Bind("Perception", "UrgentMinInterval", 5f,
                "Minimum seconds between two urgent (non silent) messages of the same kind.");
            NearbyScanRadius = file.Bind("Perception", "NearbyScanRadius", 7f,
                "Radius in metres used when looking for climbable surfaces around the player.");
            DangerousDropMeters = file.Bind("Perception", "DangerousDropMeters", 12f,
                "Drop height in metres below which the ground is considered a dangerous fall.");
            LowStaminaFraction = file.Bind("Perception", "LowStaminaFraction", 0.2f,
                "Fraction of maximum stamina under which Neuro is warned while climbing.");
            VoiceActivityThreshold = file.Bind("Voice", "VoiceActivityThreshold", 0.0025f,
                "Mean absolute sample level a teammate's voice must exceed before it is forwarded.");
        }
    }
}
