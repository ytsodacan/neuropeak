using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Voice;
using NeuroSdk.Websocket;
using UnityEngine;
using UnityEngine.Events;

namespace NeuroPeak.Voice
{
    public sealed class PeakVoiceBridge : MonoBehaviour
    {
        private const float RosterRefreshInterval = 1f;

        private readonly Dictionary<Character, PeakVoiceCapture> _captures = new Dictionary<Character, PeakVoiceCapture>();
        private readonly List<Character> _departed = new List<Character>();

        private NeuroVoiceChat? _voice;
        private bool _unavailable;
        private bool _listenersInstalled;
        private float _nextRosterRefresh;
        private int _outputSampleRate = 48000;

        public bool Ready => _voice != null && _voice.IsReady;

        private void Awake()
        {
            _outputSampleRate = AudioSettings.outputSampleRate;
        }

        private void Update()
        {
            NeuroPeakConfig? settings = NeuroPeakPlugin.Settings;
            if (settings != null && !settings.VoiceChatEnabled.Value) return;
            if (_unavailable) return;
            if (WebsocketConnection.Instance == null) return;

            Character local = Character.localCharacter;
            if (local == null)
            {
                if (_voice != null) TearDown();
                return;
            }

            EnsureConnected(local);

            if (Time.unscaledTime >= _nextRosterRefresh)
            {
                _nextRosterRefresh = Time.unscaledTime + RosterRefreshInterval;
                RefreshSpeakerRoster(local, settings);
            }

            PumpCapturedAudio();
        }

        private void OnDestroy() => TearDown();

        private void EnsureConnected(Character local)
        {
            if (_voice != null) return;

            _voice = NeuroVoiceChat.Connect();
            InstallListeners();
            PeakVoiceTransmitRegistry.Current.Attach(local);
        }

        private void InstallListeners()
        {
            if (_voice == null || _listenersInstalled) return;

            _voice.onReady ??= new UnityEvent();
            _voice.onUnavailable ??= new UnityEvent<string>();
            _voice.onSpeakingChanged ??= new UnityEvent<bool>();
            _voice.onCancelled ??= new UnityEvent();
            _voice.onAudioReceived ??= new UnityEvent<float[]>();

            _voice.onReady.AddListener(OnVoiceReady);
            _voice.onUnavailable.AddListener(OnVoiceUnavailable);
            _voice.onSpeakingChanged.AddListener(OnSpeakingChanged);
            _voice.onCancelled.AddListener(OnCancelled);
            _voice.onAudioReceived.AddListener(OnNeuroAudio);

            _listenersInstalled = true;
        }

        private void OnVoiceReady()
        {
            NeuroPeakPlugin.Log?.LogInfo("Neuro voice chat side-channel is live");
        }

        private void OnVoiceUnavailable(string reason)
        {
            _unavailable = true;
            NeuroPeakPlugin.Log?.LogWarning($"Neuro voice chat unavailable, continuing without it: {reason}");
        }

        private void OnSpeakingChanged(bool speaking)
        {
            PeakVoiceTransmitRegistry.Current.SetTransmitting(speaking);
        }

        private void OnCancelled()
        {
            PeakVoiceTransmitRegistry.Current.Flush();
            PeakVoiceTransmitRegistry.Current.SetTransmitting(false);
        }

        private void OnNeuroAudio(float[] pcm)
        {
            PeakVoiceTransmitRegistry.Current.SubmitNeuroAudio(pcm);
        }

        private void RefreshSpeakerRoster(Character local, NeuroPeakConfig? settings)
        {
            if (_voice == null) return;

            List<Character> all = Character.AllCharacters;
            if (all == null) return;

            float threshold = settings?.VoiceActivityThreshold.Value ?? 0.0025f;

            for (int i = 0; i < all.Count; i++)
            {
                Character other = all[i];
                if (other == null || other == local) continue;
                if (_captures.ContainsKey(other)) continue;

                AudioSource? voiceSource = FindVoiceSource(other);
                if (voiceSource == null) continue;

                int speakerId = _voice.RegisterSpeaker(other.characterName);
                PeakVoiceCapture capture = voiceSource.gameObject.AddComponent<PeakVoiceCapture>();
                capture.Configure(speakerId, other.characterName, _outputSampleRate, threshold);
                _captures[other] = capture;

                NeuroPeakPlugin.Log?.LogInfo($"Forwarding {other.characterName}'s voice to Neuro as speaker {speakerId}");
            }

            _departed.Clear();
            foreach (KeyValuePair<Character, PeakVoiceCapture> entry in _captures)
            {
                if (!StillInLobby(entry.Key, all)) _departed.Add(entry.Key);
            }

            foreach (Character gone in _departed) RemoveSpeaker(gone);
        }

        private void PumpCapturedAudio()
        {
            if (_voice == null || !_voice.IsReady) return;

            foreach (KeyValuePair<Character, PeakVoiceCapture> entry in _captures)
            {
                PeakVoiceCapture capture = entry.Value;
                if (capture == null) continue;

                while (capture.TryDequeue(out CapturedVoiceChunk chunk))
                {
                    _voice.SendSpeakerAudio(capture.SpeakerId, chunk.Samples, capture.SampleRate, chunk.Channels);
                }
            }
        }

        private void RemoveSpeaker(Character character)
        {
            if (!_captures.TryGetValue(character, out PeakVoiceCapture capture)) return;

            _captures.Remove(character);
            if (capture != null) Destroy(capture);
            if (_voice != null && capture != null) _voice.UnregisterSpeaker(capture.SpeakerId);
        }

        private void TearDown()
        {
            foreach (KeyValuePair<Character, PeakVoiceCapture> entry in _captures)
            {
                if (entry.Value != null) Destroy(entry.Value);
            }

            _captures.Clear();
            PeakVoiceTransmitRegistry.Current.Detach();

            if (_voice != null)
            {
                _voice.Disconnect();
                _voice = null;
            }

            _listenersInstalled = false;
        }

        private static bool StillInLobby(Character speaker, List<Character> all) => speaker != null && all.Contains(speaker);

        private static AudioSource? FindVoiceSource(Character character)
        {
            CharacterVoiceHandler handler = character.GetComponentInChildren<CharacterVoiceHandler>(true);
            if (handler == null) return null;

            AudioSource source = handler.GetComponent<AudioSource>();
            return source == null ? null : source;
        }
    }
}
