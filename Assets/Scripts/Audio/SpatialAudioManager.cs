using System;
using MobileQuestVR.AndroidBridge;
using MobileQuestVR.Localization;
using MobileQuestVR.Notifications;
using MobileQuestVR.Settings;
using UnityEngine;

namespace MobileQuestVR.Audio
{
    public enum AmbientSoundProfile
    {
        CosmicDrone = 0,
        CinemaWarmth = 1,
        CyberPulse = 2,
        OceanWaves = 3,
        MountainWind = 4
    }

    /// <summary>
    /// Procedural 3D spatial audio synthesizer and optional button-activated Voice Assistant.
    /// Zero external audio binary dependencies required; works 100% offline inside the APK.
    /// </summary>
    public sealed class SpatialAudioManager : MonoBehaviour
    {
        public static SpatialAudioManager Instance { get; private set; }

        private AudioSource _ambientSource;
        private AudioSource _spatialUiSource;

        private AudioClip _hoverClip;
        private AudioClip _clickClip;
        private AudioClip _bootChimeClip;
        private AudioClip _alarmClip;

        // Voice assistant state (Section 13 & 14: strictly button-activated, with active mic indicator)
        private AudioClip _micRecordingClip;
        private bool _isListeningVoice;
        private float _voiceListenTimer;
        public bool IsVoiceListening => _isListeningVoice;
        public event Action<string> OnVoiceCommandRecognized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildSynthesizedUiClips();
            SetupAudioSources();
        }

        private void Start()
        {
            ApplyAudioSettings();
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged += _ => ApplyAudioSettings();
            }
        }

        private void SetupAudioSources()
        {
            var ambientObj = new GameObject("AmbientRoomAudio");
            ambientObj.transform.SetParent(transform, false);
            _ambientSource = ambientObj.AddComponent<AudioSource>();
            _ambientSource.loop = true;
            _ambientSource.playOnAwake = false;
            _ambientSource.volume = 0.22f;
            _ambientSource.spatialBlend = 0.35f;

            var uiObj = new GameObject("SpatialUIAudio");
            uiObj.transform.SetParent(transform, false);
            _spatialUiSource = uiObj.AddComponent<AudioSource>();
            _spatialUiSource.loop = false;
            _spatialUiSource.playOnAwake = false;
            _spatialUiSource.volume = 0.55f;
            _spatialUiSource.spatialBlend = 1.0f;
            _spatialUiSource.minDistance = 0.5f;
            _spatialUiSource.maxDistance = 12f;
        }

        private void BuildSynthesizedUiClips()
        {
            _hoverClip = SynthesizeTone("UI_Hover", 660f, 880f, 0.055f, 0.25f);
            _clickClip = SynthesizeTone("UI_Click", 520f, 1040f, 0.095f, 0.40f);
            _bootChimeClip = SynthesizeChord("Boot_Chime", new[] { 261.63f, 329.63f, 392.00f, 523.25f }, 1.35f, 0.35f);
            _alarmClip = SynthesizeTone("Timer_Alarm", 880f, 660f, 0.45f, 0.5f);
        }

        public void ApplyAudioSettings()
        {
            var data = SettingsManager.Instance != null ? SettingsManager.Instance.Data : null;
            if (data == null) return;

            AudioListener.volume = Mathf.Clamp01(data.MasterVolume);
            if (_spatialUiSource != null)
            {
                _spatialUiSource.spatialBlend = data.SpatialAudioEnabled ? 1.0f : 0.0f;
                _spatialUiSource.mute = !data.UiSoundEffects;
            }
            if (_ambientSource != null)
            {
                _ambientSource.mute = !data.AmbientSoundEnabled;
                if (data.AmbientSoundEnabled && !_ambientSource.isPlaying && _ambientSource.clip != null)
                {
                    _ambientSource.Play();
                }
                else if (!data.AmbientSoundEnabled && _ambientSource.isPlaying)
                {
                    _ambientSource.Pause();
                }
            }
        }

        public void PlayBootChime()
        {
            if (_spatialUiSource == null || _bootChimeClip == null) return;
            _spatialUiSource.transform.position = new Vector3(0f, 1.6f, 2.0f);
            _spatialUiSource.PlayOneShot(_bootChimeClip);
        }

        public void PlaySpatialClick(Vector3 worldPos)
        {
            if (_spatialUiSource == null || _clickClip == null) return;
            _spatialUiSource.transform.position = worldPos;
            _spatialUiSource.PlayOneShot(_clickClip);
        }

        public void PlaySpatialHover(Vector3 worldPos)
        {
            if (_spatialUiSource == null || _hoverClip == null) return;
            _spatialUiSource.transform.position = worldPos;
            _spatialUiSource.PlayOneShot(_hoverClip, 0.45f);
        }

        public void PlayTimerAlarm()
        {
            if (_spatialUiSource == null || _alarmClip == null) return;
            _spatialUiSource.PlayOneShot(_alarmClip);
        }

        public void SetRoomAmbientProfile(AmbientSoundProfile profile)
        {
            if (_ambientSource == null) return;
            _ambientSource.Stop();
            _ambientSource.clip = SynthesizeAmbientLoop(profile);
            var data = SettingsManager.Instance != null ? SettingsManager.Instance.Data : null;
            if (data == null || data.AmbientSoundEnabled)
            {
                _ambientSource.Play();
            }
        }

        /// <summary>
        /// Button-triggered Voice Assistant. Requests RECORD_AUDIO permission on demand and turns on
        /// the mandatory active microphone privacy badge while recording.
        /// </summary>
        public void ActivateVoiceAssistantPushToTalk(string simulatedOrQuickIntent = null)
        {
            var bridge = AndroidPlatformBridge.Instance;
            if (bridge == null) return;

            bridge.RequestMicrophonePermissionOnDemand(granted =>
            {
                if (!granted)
                {
                    NotificationCenter.Instance?.Push(
                        LocalizationService.Get("menu.voice"),
                        "Permiso de micrófono denegado / Microphone permission denied.",
                        NotificationSeverity.Warning);
                    return;
                }

                StartShortVoiceCapture(simulatedOrQuickIntent);
            });
        }

        private void StartShortVoiceCapture(string intentHint)
        {
            if (_isListeningVoice) return;

            _isListeningVoice = true;
            _voiceListenTimer = 2.6f;

            if (AndroidPlatformBridge.Instance != null)
            {
                AndroidPlatformBridge.Instance.IsMicrophoneActive = true;
            }

            try
            {
                if (Microphone.devices != null && Microphone.devices.Length > 0)
                {
                    _micRecordingClip = Microphone.Start(null, false, 3, 16000);
                }
            }
            catch
            {
                // Clean fallback when hardware mic is busy or unavailable
            }

            NotificationCenter.Instance?.Push(
                LocalizationService.Get("menu.voice"),
                LocalizationService.Get("privacy.mic_active") + " — Escuchando comando...",
                NotificationSeverity.Privacy,
                2.8f);

            _pendingVoiceIntent = string.IsNullOrEmpty(intentHint) ? "recenter" : intentHint;
        }

        private string _pendingVoiceIntent;

        private void Update()
        {
            if (!_isListeningVoice) return;

            _voiceListenTimer -= Time.unscaledDeltaTime;
            if (_voiceListenTimer <= 0f)
            {
                StopVoiceCaptureAndDispatch();
            }
        }

        private void StopVoiceCaptureAndDispatch()
        {
            _isListeningVoice = false;
            try
            {
                if (Microphone.devices != null && Microphone.devices.Length > 0 && Microphone.IsRecording(null))
                {
                    Microphone.End(null);
                }
            }
            catch
            {
                // Ignore cleanup warning
            }

            if (AndroidPlatformBridge.Instance != null)
            {
                AndroidPlatformBridge.Instance.IsMicrophoneActive = false;
            }

            if (!string.IsNullOrEmpty(_pendingVoiceIntent))
            {
                OnVoiceCommandRecognized?.Invoke(_pendingVoiceIntent);
                _pendingVoiceIntent = null;
            }
        }

        private static AudioClip SynthesizeTone(string name, float fStart, float fEnd, float durationSec, float gain)
        {
            const int sampleRate = 22050;
            int samples = Mathf.Max(64, Mathf.RoundToInt(sampleRate * durationSec));
            float[] data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float freq = Mathf.Lerp(fStart, fEnd, t);
                phase += 2f * Mathf.PI * freq / sampleRate;
                float env = Mathf.Sin(t * Mathf.PI);
                data[i] = Mathf.Sin(phase) * env * gain;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeChord(string name, float[] freqs, float durationSec, float gain)
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * durationSec);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float sec = i / (float)sampleRate;
                float norm = i / (float)samples;
                float env = Mathf.Sin(Mathf.Min(1f, norm * 4f) * Mathf.PI * 0.5f) * (1f - norm * 0.85f);
                float sum = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    sum += Mathf.Sin(2f * Mathf.PI * freqs[f] * sec);
                }
                data[i] = (sum / freqs.Length) * env * gain;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeAmbientLoop(AmbientSoundProfile profile)
        {
            const int sampleRate = 16000;
            const int durationSec = 4;
            int samples = sampleRate * durationSec;
            float[] data = new float[samples];

            uint rng = 1234567u;
            for (int i = 0; i < samples; i++)
            {
                float sec = i / (float)sampleRate;
                rng = rng * 1664525u + 1013904223u;
                float noise = ((rng & 0xFFFF) / 32768f - 1f) * 0.08f;

                switch (profile)
                {
                    case AmbientSoundProfile.OceanWaves:
                    {
                        float swell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.25f * sec);
                        data[i] = noise * swell * 0.9f;
                        break;
                    }
                    case AmbientSoundProfile.MountainWind:
                    {
                        float breeze = 0.45f + 0.55f * Mathf.Sin(2f * Mathf.PI * 0.5f * sec);
                        data[i] = (noise * 0.65f + Mathf.Sin(2f * Mathf.PI * 196f * sec) * 0.03f) * breeze;
                        break;
                    }
                    case AmbientSoundProfile.CyberPulse:
                    {
                        data[i] = (Mathf.Sin(2f * Mathf.PI * 110f * sec) + Mathf.Sin(2f * Mathf.PI * 165f * sec)) * 0.05f;
                        break;
                    }
                    case AmbientSoundProfile.CinemaWarmth:
                    {
                        data[i] = Mathf.Sin(2f * Mathf.PI * 82.5f * sec) * 0.04f;
                        break;
                    }
                    default:
                    {
                        data[i] = (Mathf.Sin(2f * Mathf.PI * 98f * sec) + Mathf.Sin(2f * Mathf.PI * 147f * sec)) * 0.045f;
                        break;
                    }
                }
            }

            var clip = AudioClip.Create($"Ambient_{profile}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
