using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace MobileQuestVR.AndroidBridge
{
    [Serializable]
    public sealed class DeviceCompatibilityReport
    {
        public string DeviceModel;
        public string DeviceName;
        public string OperatingSystem;
        public int AndroidSdkInt;
        public string CpuArchitecture;
        public string GpuName;
        public string GraphicsApi;
        public int SystemMemoryMB;
        public int GraphicsMemoryMB;
        public int ScreenWidth;
        public int ScreenHeight;
        public float ScreenRefreshHz;
        public bool HasGyroscope;
        public bool HasAccelerometer;
        public bool HasMagnetometer;
        public bool HasRearCamera;
        public bool IsPriorityXiaomiMi11Lite5G;
        public int RecommendedTargetFps;
        public string SummaryNotes;
    }

    /// <summary>
    /// Native Android bridge for permissions, battery/thermal telemetry, immersive mode,
    /// hardware compatibility checks (priority: Xiaomi Mi 11 Lite 5G / Snapdragon 780G / Adreno 642),
    /// and URI opening.
    /// </summary>
    public sealed class AndroidPlatformBridge : MonoBehaviour
    {
        public static AndroidPlatformBridge Instance { get; private set; }

        public event Action<bool> OnCameraActiveChanged;
        public event Action<bool> OnMicrophoneActiveChanged;

        private bool _cameraActive;
        private bool _microphoneActive;

        public bool IsCameraActive
        {
            get => _cameraActive;
            set
            {
                if (_cameraActive == value) return;
                _cameraActive = value;
                OnCameraActiveChanged?.Invoke(_cameraActive);
            }
        }

        public bool IsMicrophoneActive
        {
            get => _microphoneActive;
            set
            {
                if (_microphoneActive == value) return;
                _microphoneActive = value;
                OnMicrophoneActiveChanged?.Invoke(_microphoneActive);
            }
        }

        public DeviceCompatibilityReport HardwareReport { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ApplyLandscapeImmersiveMode();
            HardwareReport = RunCompatibilityAudit();
        }

        public void ApplyLandscapeImmersiveMode()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.fullScreen = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity?.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using var window = activity.Call<AndroidJavaObject>("getWindow");
                        using var decorView = window.Call<AndroidJavaObject>("getDecorView");
                        // IMMERSIVE_STICKY | FULLSCREEN | HIDE_NAVIGATION | LAYOUT_FULLSCREEN
                        const int flags = 0x00001000 | 0x00000004 | 0x00000002 | 0x00000400 | 0x00000200 | 0x00000100;
                        decorView.Call("setSystemUiVisibility", flags);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AndroidBridge] Immersive flags warning: {ex.Message}");
                    }
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AndroidBridge] ApplyLandscapeImmersiveMode skipped: {e.Message}");
            }
#endif
        }

        public DeviceCompatibilityReport RunCompatibilityAudit()
        {
            string model = SystemInfo.deviceModel ?? "Unknown";
            string gpu = SystemInfo.graphicsDeviceName ?? "Unknown";
            int sdkInt = GetAndroidSdkInt();

            bool hasRearCam = false;
            try
            {
                var cams = WebCamTexture.devices;
                if (cams != null)
                {
                    for (int i = 0; i < cams.Length; i++)
                    {
                        if (!cams[i].isFrontFacing)
                        {
                            hasRearCam = true;
                            break;
                        }
                    }
                    if (!hasRearCam && cams.Length > 0) hasRearCam = true;
                }
            }
            catch
            {
                hasRearCam = false;
            }

            // Detect Xiaomi Mi 11 Lite 5G (Model M2101K9AG / M2101K9C / renoir / Adreno 642)
            bool isMi11Lite5G =
                model.IndexOf("M2101K9", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("Mi 11 Lite 5G", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("renoir", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpu.IndexOf("Adreno (TM) 642", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpu.IndexOf("Adreno 642", StringComparison.OrdinalIgnoreCase) >= 0;

            float refreshHz = (float)Screen.currentResolution.refreshRateRatio.value;
            if (refreshHz <= 0f || float.IsNaN(refreshHz)) refreshHz = 60f;

            int recommendedFps = isMi11Lite5G ? 90 : (refreshHz >= 88f ? 90 : 60);

            var report = new DeviceCompatibilityReport
            {
                DeviceModel = model,
                DeviceName = SystemInfo.deviceName ?? "Android Device",
                OperatingSystem = SystemInfo.operatingSystem ?? "Android",
                AndroidSdkInt = sdkInt,
                CpuArchitecture = IntPtr.Size == 8 ? "ARM64 (64-bit)" : "ARMv7 (32-bit)",
                GpuName = gpu,
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                SystemMemoryMB = SystemInfo.systemMemorySize,
                GraphicsMemoryMB = SystemInfo.graphicsMemorySize,
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
                ScreenRefreshHz = refreshHz,
                HasGyroscope = SystemInfo.supportsGyroscope,
                HasAccelerometer = SystemInfo.supportsAccelerometer,
                HasMagnetometer = true,
                HasRearCamera = hasRearCam,
                IsPriorityXiaomiMi11Lite5G = isMi11Lite5G,
                RecommendedTargetFps = recommendedFps,
                SummaryNotes = isMi11Lite5G
                    ? "Perfil prioritario activo: Xiaomi Mi 11 Lite 5G (Snapdragon 780G / Adreno 642, 90Hz AMOLED, 3DoF Gyro)."
                    : $"Perfil adaptativo móvil ({gpu}, {(SystemInfo.supportsGyroscope ? "3DoF Gyro disponible" : "Sin giroscopio físico - usando respaldo táctil/acelerómetro")})."
            };

            return report;
        }

        public int GetAndroidSdkInt()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                return version.GetStatic<int>("SDK_INT");
            }
            catch
            {
                return 30;
            }
#else
            return 33;
#endif
        }

        public float GetBatteryLevelNormalized()
        {
            float lvl = SystemInfo.batteryLevel;
            return lvl < 0f ? 0.85f : Mathf.Clamp01(lvl);
        }

        public bool IsWifiConnected()
        {
            return Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork;
        }

        public bool HasInternetConnection()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        /// <summary>
        /// Reads real battery/chassis temperature in Celsius from Android BatteryManager Intent.
        /// Returns ~34.5C fallback in Editor.
        /// </summary>
        public float GetDeviceTemperatureCelsius()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED");
                using var batteryIntent = activity.Call<AndroidJavaObject>("registerReceiver", null, intentFilter);
                if (batteryIntent != null)
                {
                    int tenthsC = batteryIntent.Call<int>("getIntExtra", "temperature", 340);
                    return tenthsC / 10f;
                }
            }
            catch
            {
                // Ignore if restricted by OEM
            }
#endif
            return 34.5f;
        }

        public bool HasCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
            return true;
#endif
        }

        public bool HasMicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
            return true;
#endif
        }

        /// <summary>
        /// Requests camera permission strictly on demand (never at OS launch).
        /// </summary>
        public void RequestCameraPermissionOnDemand(Action<bool> callback)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                callback?.Invoke(true);
                return;
            }
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => callback?.Invoke(true);
            callbacks.PermissionDenied += _ => callback?.Invoke(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => callback?.Invoke(false);
            Permission.RequestUserPermission(Permission.Camera, callbacks);
#else
            callback?.Invoke(true);
#endif
        }

        /// <summary>
        /// Requests microphone permission strictly on demand when Voice Assistant is triggered.
        /// </summary>
        public void RequestMicrophonePermissionOnDemand(Action<bool> callback)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                callback?.Invoke(true);
                return;
            }
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => callback?.Invoke(true);
            callbacks.PermissionDenied += _ => callback?.Invoke(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => callback?.Invoke(false);
            Permission.RequestUserPermission(Permission.Microphone, callbacks);
#else
            callback?.Invoke(true);
#endif
        }
    }
}
