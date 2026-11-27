using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MobileQuestVR.Localization;
using UnityEngine;

namespace MobileQuestVR.Settings
{
    public enum RoomId
    {
        FuturisticBedroom = 0,
        CinemaRoom = 1,
        GamingRoom = 2,
        SpaceStation = 3,
        ModernBedroom = 4,
        TropicalBeach = 5,
        Mountain = 6
    }

    public enum OSThemePreset
    {
        QuestCosmicCyan = 0,
        NebulaViolet = 1,
        SolarAmber = 2,
        EmeraldHorizon = 3
    }

    public enum HeadsetViewerProfile
    {
        XiaomiMi11Lite5G_Visor = 0,
        GenericCardboardV2 = 1,
        WideFOVMobileVR = 2,
        MinimalDistortion = 3
    }

    [Serializable]
    public sealed class OSPreferencesData
    {
        // Section 4: Profile
        public string UserDisplayName = "Explorador VR";
        public bool FirstBootCompleted = false;
        public bool ShowOnboardingTips = true;

        // Theme & Localization
        public OSThemePreset Theme = OSThemePreset.QuestCosmicCyan;
        public AppLanguage Language = AppLanguage.Spanish;

        // Audio
        public float MasterVolume = 0.85f;
        public bool UiSoundEffects = true;
        public bool AmbientSoundEnabled = true;
        public bool SpatialAudioEnabled = true;

        // Rooms & Favorites
        public RoomId ActiveRoom = RoomId.FuturisticBedroom;
        public List<string> FavoriteRooms = new List<string> { "FuturisticBedroom", "SpaceStation" };
        public List<string> FavoriteApps = new List<string> { "YouTube", "Chrome", "Spotify" };

        // Tracking & Camera
        public bool HandTrackingEnabled = false;
        public float PinchThresholdNormalized = 0.045f;
        public float HeadYawCalibrationOffset = 0f;
        public float HeadPitchCalibrationOffset = 0f;
        public int CameraTargetFps = 30;

        // VR Headset & Spatial Windows (Sections 4, 8, 11)
        public bool StereoscopicVRMode = true;
        public HeadsetViewerProfile ViewerProfile = HeadsetViewerProfile.XiaomiMi11Lite5G_Visor;
        public float InterpupillaryDistanceMm = 63.0f;
        public float LensDistortionK1 = 0.18f;
        public float LensDistortionK2 = 0.06f;
        public float Brightness = 1.0f;
        public float UiScale = 1.0f;
        public float WindowDistanceMeters = 2.15f;
        public float WindowScale = 1.0f;

        // Comfort & Performance (Sections 8, 10)
        public int GraphicsQualityLevel = 2; // Balanced-High for Xiaomi Mi 11 Lite 5G
        public bool AdaptiveQuality = true;
        public bool AdaptiveFps = true;
        public bool ThermalThrottlingProtection = true;
        public bool ReducedMotion = false;
        public bool ReduceEffects = false;
        public bool ReduceAnimations = false;
        public int RestReminderIntervalMinutes = 30;

        // Storage
        public List<string> CompatibleMediaFolders = new List<string>();

        // Security & Privacy (Section 14: Never lock app launch; optional PIN ONLY for sensitive settings)
        public bool ProtectSensitiveSettingsWithPin = false;
        public string SensitiveSettingsPinSha256 = string.Empty;
    }

    /// <summary>
    /// Persistent local settings manager. Automatically saves changes locally to JSON
    /// inside Application.persistentDataPath without any cloud sync.
    /// </summary>
    public sealed class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        public OSPreferencesData Data { get; private set; } = new OSPreferencesData();
        public event Action<OSPreferencesData> OnSettingsChanged;

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, "os_preferences.json");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadFromLocalDisk();
        }

        public void LoadFromLocalDisk()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath, Encoding.UTF8);
                    var loaded = JsonUtility.FromJson<OSPreferencesData>(json);
                    if (loaded != null)
                    {
                        Data = loaded;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SettingsManager] Failed loading preferences, using defaults: {e.Message}");
                Data = new OSPreferencesData();
            }

            EnsureDefaultMediaFolders();
            LocalizationService.SetLanguage(Data.Language);
            AudioListener.volume = Mathf.Clamp01(Data.MasterVolume);
        }

        private void EnsureDefaultMediaFolders()
        {
            if (Data.CompatibleMediaFolders == null)
            {
                Data.CompatibleMediaFolders = new List<string>();
            }
            string localMediaDir = Path.Combine(Application.persistentDataPath, "Media");
            try
            {
                if (!Directory.Exists(localMediaDir)) Directory.CreateDirectory(localMediaDir);
                if (!Data.CompatibleMediaFolders.Contains(localMediaDir))
                {
                    Data.CompatibleMediaFolders.Add(localMediaDir);
                }
            }
            catch
            {
                // Ignore directory errors on restricted sandboxes
            }
        }

        public void SaveAndNotify()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(SaveFilePath, json, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SettingsManager] Local save warning: {e.Message}");
            }

            LocalizationService.SetLanguage(Data.Language);
            AudioListener.volume = Mathf.Clamp01(Data.MasterVolume);
            OnSettingsChanged?.Invoke(Data);
        }

        public Color GetThemeAccentColor()
        {
            return Data.Theme switch
            {
                OSThemePreset.NebulaViolet => new Color(0.62f, 0.42f, 1.0f, 1f),
                OSThemePreset.SolarAmber => new Color(1.0f, 0.72f, 0.22f, 1f),
                OSThemePreset.EmeraldHorizon => new Color(0.18f, 0.92f, 0.65f, 1f),
                _ => new Color(0.0f, 0.84f, 1.0f, 1f)
            };
        }

        public Color GetThemePanelColor()
        {
            return Data.Theme switch
            {
                OSThemePreset.NebulaViolet => new Color(0.10f, 0.08f, 0.22f, 0.90f),
                OSThemePreset.SolarAmber => new Color(0.16f, 0.12f, 0.08f, 0.90f),
                OSThemePreset.EmeraldHorizon => new Color(0.06f, 0.16f, 0.14f, 0.90f),
                _ => new Color(0.06f, 0.10f, 0.20f, 0.90f)
            };
        }

        public void ApplyHeadsetProfilePreset(HeadsetViewerProfile profile)
        {
            Data.ViewerProfile = profile;
            switch (profile)
            {
                case HeadsetViewerProfile.XiaomiMi11Lite5G_Visor:
                    Data.InterpupillaryDistanceMm = 63.0f;
                    Data.LensDistortionK1 = 0.18f;
                    Data.LensDistortionK2 = 0.06f;
                    break;
                case HeadsetViewerProfile.GenericCardboardV2:
                    Data.InterpupillaryDistanceMm = 64.0f;
                    Data.LensDistortionK1 = 0.25f;
                    Data.LensDistortionK2 = 0.10f;
                    break;
                case HeadsetViewerProfile.WideFOVMobileVR:
                    Data.InterpupillaryDistanceMm = 65.0f;
                    Data.LensDistortionK1 = 0.30f;
                    Data.LensDistortionK2 = 0.12f;
                    break;
                case HeadsetViewerProfile.MinimalDistortion:
                    Data.InterpupillaryDistanceMm = 62.0f;
                    Data.LensDistortionK1 = 0.05f;
                    Data.LensDistortionK2 = 0.01f;
                    break;
            }
            SaveAndNotify();
        }

        public void SetOptionalSettingsPin(string plainPin)
        {
            if (string.IsNullOrWhiteSpace(plainPin))
            {
                Data.ProtectSensitiveSettingsWithPin = false;
                Data.SensitiveSettingsPinSha256 = string.Empty;
            }
            else
            {
                Data.ProtectSensitiveSettingsWithPin = true;
                Data.SensitiveSettingsPinSha256 = ComputeSha256(plainPin.Trim());
            }
            SaveAndNotify();
        }

        public bool VerifySensitiveSettingsPin(string candidatePin)
        {
            if (!Data.ProtectSensitiveSettingsWithPin || string.IsNullOrEmpty(Data.SensitiveSettingsPinSha256))
            {
                return true;
            }
            if (string.IsNullOrEmpty(candidatePin)) return false;
            return string.Equals(ComputeSha256(candidatePin.Trim()), Data.SensitiveSettingsPinSha256, StringComparison.OrdinalIgnoreCase);
        }

        public void ToggleFavoriteRoom(RoomId room)
        {
            string key = room.ToString();
            if (Data.FavoriteRooms.Contains(key)) Data.FavoriteRooms.Remove(key);
            else Data.FavoriteRooms.Add(key);
            SaveAndNotify();
        }

        public bool IsFavoriteRoom(RoomId room) => Data.FavoriteRooms != null && Data.FavoriteRooms.Contains(room.ToString());

        public void ToggleFavoriteApp(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return;
            if (Data.FavoriteApps.Contains(appId)) Data.FavoriteApps.Remove(appId);
            else Data.FavoriteApps.Add(appId);
            SaveAndNotify();
        }

        public bool IsFavoriteApp(string appId) => Data.FavoriteApps != null && Data.FavoriteApps.Contains(appId);

        private static string ComputeSha256(string input)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
