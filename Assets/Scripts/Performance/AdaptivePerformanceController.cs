using MobileQuestVR.AndroidBridge;
using MobileQuestVR.Localization;
using MobileQuestVR.Notifications;
using MobileQuestVR.Settings;
using UnityEngine;

namespace MobileQuestVR.Performance
{
    /// <summary>
    /// Adaptive FPS, graphics quality, battery alert, rest-reminder, and thermal throttling manager
    /// tuned for Xiaomi Mi 11 Lite 5G (Snapdragon 780G / Adreno 642, 90Hz AMOLED).
    /// </summary>
    public sealed class AdaptivePerformanceController : MonoBehaviour
    {
        public static AdaptivePerformanceController Instance { get; private set; }

        public float CurrentFps { get; private set; } = 60f;
        public float CurrentTemperatureC { get; private set; } = 34.5f;
        public bool IsThermallyThrottled { get; private set; }
        public int ActiveTargetFps { get; private set; } = 90;

        private int _frameAccumulator;
        private float _fpsTimer;
        private float _telemetryPollTimer;
        private float _sessionElapsedSeconds;
        private float _nextRestReminderSeconds = 1800f;
        private bool _lowBatteryWarned;
        private bool _criticalBatteryWarned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ApplyConfiguredPerformanceProfile();
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged += _ => ApplyConfiguredPerformanceProfile();
            }
        }

        public void ApplyConfiguredPerformanceProfile()
        {
            var settings = SettingsManager.Instance != null ? SettingsManager.Instance.Data : null;
            var bridge = AndroidPlatformBridge.Instance;

            QualitySettings.vSyncCount = 0;
            int baseFps = 60;
            if (bridge != null && bridge.HardwareReport != null)
            {
                baseFps = bridge.HardwareReport.RecommendedTargetFps;
            }

            if (settings != null)
            {
                int maxQuality = Mathf.Max(0, QualitySettings.names.Length - 1);
                int desiredQuality = Mathf.Clamp(settings.GraphicsQualityLevel, 0, maxQuality);
                QualitySettings.SetQualityLevel(desiredQuality, false);

                if (!settings.AdaptiveFps)
                {
                    baseFps = 60;
                }
                if (settings.RestReminderIntervalMinutes > 0)
                {
                    _nextRestReminderSeconds = _sessionElapsedSeconds + settings.RestReminderIntervalMinutes * 60f;
                }
            }

            ActiveTargetFps = baseFps;
            Application.targetFrameRate = ActiveTargetFps;
        }

        private void Update()
        {
            _frameAccumulator++;
            _fpsTimer += Time.unscaledDeltaTime;
            _telemetryPollTimer += Time.unscaledDeltaTime;
            _sessionElapsedSeconds += Time.unscaledDeltaTime;

            if (_fpsTimer >= 1.0f)
            {
                CurrentFps = _frameAccumulator / _fpsTimer;
                _frameAccumulator = 0;
                _fpsTimer = 0f;
            }

            if (_telemetryPollTimer >= 5.0f)
            {
                _telemetryPollTimer = 0f;
                EvaluateThermalBatteryAndAdaptiveScaling();
            }

            EvaluateComfortRestReminder();
        }

        private void EvaluateThermalBatteryAndAdaptiveScaling()
        {
            var bridge = AndroidPlatformBridge.Instance;
            var settings = SettingsManager.Instance != null ? SettingsManager.Instance.Data : null;
            if (bridge == null || settings == null) return;

            CurrentTemperatureC = bridge.GetDeviceTemperatureCelsius();
            float battery = bridge.GetBatteryLevelNormalized();

            // Battery checks (Section 8)
            if (battery <= 0.10f && !_criticalBatteryWarned)
            {
                _criticalBatteryWarned = true;
                NotificationCenter.Instance?.Push(
                    "MobileQuestVR OS",
                    LocalizationService.Get("alert.battery_critical"),
                    NotificationSeverity.Critical,
                    6f);
                SettingsManager.Instance?.SaveAndNotify();
                ActiveTargetFps = 45;
                Application.targetFrameRate = 45;
            }
            else if (battery <= 0.20f && !_lowBatteryWarned)
            {
                _lowBatteryWarned = true;
                NotificationCenter.Instance?.Push(
                    "MobileQuestVR OS",
                    LocalizationService.Get("alert.battery_low"),
                    NotificationSeverity.Warning,
                    5f);
            }
            else if (battery > 0.25f)
            {
                _lowBatteryWarned = false;
                _criticalBatteryWarned = false;
            }

            // Thermal throttling check (Sections 8 & 10)
            if (settings.ThermalThrottlingProtection && CurrentTemperatureC >= 42.0f)
            {
                if (!IsThermallyThrottled)
                {
                    IsThermallyThrottled = true;
                    QualitySettings.SetQualityLevel(0, false);
                    ActiveTargetFps = 45;
                    Application.targetFrameRate = 45;
                    NotificationCenter.Instance?.Push(
                        "Thermal Guard",
                        LocalizationService.Get("alert.thermal"),
                        NotificationSeverity.Warning,
                        5f);
                }
                return;
            }
            else if (IsThermallyThrottled && CurrentTemperatureC <= 39.0f)
            {
                IsThermallyThrottled = false;
                ApplyConfiguredPerformanceProfile();
            }

            // Adaptive FPS / Quality scaling when frame rate dips
            if (settings.AdaptiveQuality && !IsThermallyThrottled)
            {
                int currentQ = QualitySettings.GetQualityLevel();
                if (CurrentFps < ActiveTargetFps * 0.72f && currentQ > 0)
                {
                    QualitySettings.SetQualityLevel(currentQ - 1, false);
                }
            }
        }

        private void EvaluateComfortRestReminder()
        {
            var settings = SettingsManager.Instance != null ? SettingsManager.Instance.Data : null;
            if (settings == null || settings.RestReminderIntervalMinutes <= 0) return;

            if (_sessionElapsedSeconds >= _nextRestReminderSeconds)
            {
                _nextRestReminderSeconds = _sessionElapsedSeconds + settings.RestReminderIntervalMinutes * 60f;
                NotificationCenter.Instance?.Push(
                    LocalizationService.Get("settings.comfort"),
                    LocalizationService.Get("alert.rest_reminder"),
                    NotificationSeverity.Comfort,
                    6f);
            }
        }
    }
}
