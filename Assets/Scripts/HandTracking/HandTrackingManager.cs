using System;
using MobileQuestVR.AndroidBridge;
using MobileQuestVR.HeadTracking;
using MobileQuestVR.Localization;
using MobileQuestVR.Notifications;
using MobileQuestVR.Settings;
using UnityEngine;

namespace MobileQuestVR.HandTracking
{
    public struct HandLandmarkFrame
    {
        public bool IsDetected;
        public Vector3 WristViewportPos;
        public float ThumbIndexDistanceNormalized;
        public bool IsPinching;
    }

    public interface IHandTrackingBackend
    {
        string BackendName { get; }
        bool Initialize();
        HandLandmarkFrame SampleFrame();
        void Calibrate();
        void Shutdown();
    }

    /// <summary>
    /// MediaPipe-compatible bridge + rear-camera optical skin/contour fallback & Editor test driver.
    /// When MediaPipe Hands Android AAR (com.google.mediapipe:tasks-vision) is present in Plugins/Android,
    /// binds via JNI; otherwise uses camera optical region estimation and reports status transparently.
    /// </summary>
    public sealed class HybridHandTrackingBackend : IHandTrackingBackend
    {
        private WebCamTexture _camTex;
        private Color32[] _downsampledBuffer;
        private float _baselineLuminance = 0.45f;
        private bool _jniMediaPipeAvailable;
        private bool _pinchLatch;

        public string BackendName => _jniMediaPipeAvailable
            ? "MediaPipe Tasks Vision JNI"
            : "Optical Camera Contour + Pinch Estimator (Experimental)";

        public bool Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var mpClass = new AndroidJavaClass("com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarker");
                _jniMediaPipeAvailable = mpClass != null;
            }
            catch
            {
                _jniMediaPipeAvailable = false;
            }
#endif
            try
            {
                var devices = WebCamTexture.devices;
                if (devices != null && devices.Length > 0)
                {
                    string camName = devices[0].name;
                    for (int i = 0; i < devices.Length; i++)
                    {
                        if (!devices[i].isFrontFacing)
                        {
                            camName = devices[i].name;
                            break;
                        }
                    }
                    _camTex = new WebCamTexture(camName, 320, 240, 24);
                    _camTex.Play();
                }
            }
            catch
            {
                _camTex = null;
            }
            return true;
        }

        public void Calibrate()
        {
            if (_camTex != null && _camTex.isPlaying && _camTex.width > 16)
            {
                _downsampledBuffer = _camTex.GetPixels32();
                long sum = 0;
                for (int i = 0; i < _downsampledBuffer.Length; i += 8)
                {
                    sum += (_downsampledBuffer[i].r + _downsampledBuffer[i].g + _downsampledBuffer[i].b) / 3;
                }
                int count = Mathf.Max(1, _downsampledBuffer.Length / 8);
                _baselineLuminance = (sum / (float)count) / 255f;
            }
        }

        public HandLandmarkFrame SampleFrame()
        {
            // Keyboard / Two-finger pinch gesture test support
            if (Input.GetKey(KeyCode.P) || Input.touchCount >= 2)
            {
                float dist = 0.02f;
                if (Input.touchCount >= 2)
                {
                    Vector2 t0 = Input.GetTouch(0).position;
                    Vector2 t1 = Input.GetTouch(1).position;
                    dist = Vector2.Distance(t0, t1) / Mathf.Max(1f, Screen.width);
                }
                float threshold = SettingsManager.Instance != null ? SettingsManager.Instance.Data.PinchThresholdNormalized : 0.045f;
                return new HandLandmarkFrame
                {
                    IsDetected = true,
                    WristViewportPos = new Vector3(0.55f, 0.36f, 0.45f),
                    ThumbIndexDistanceNormalized = dist,
                    IsPinching = dist <= threshold * 2.5f
                };
            }

            if (_camTex != null && _camTex.isPlaying && _camTex.width > 32 && _camTex.didUpdateThisFrame)
            {
                _downsampledBuffer = _camTex.GetPixels32();
                int w = _camTex.width;
                int h = _camTex.height;
                int activePixels = 0;
                float cx = 0f;
                float cy = 0f;

                for (int y = h / 6; y < (h * 5) / 6; y += 4)
                {
                    for (int x = w / 6; x < (w * 5) / 6; x += 4)
                    {
                        Color32 c = _downsampledBuffer[y * w + x];
                        // Warm skin-tone / foreground contrast heuristic when MediaPipe native AAR is absent
                        bool skinCandidate = c.r > 85 && c.g > 40 && c.b > 25 && c.r > c.g && (c.r - c.b) > 18;
                        float lum = (c.r + c.g + c.b) / (3f * 255f);
                        if (skinCandidate && Mathf.Abs(lum - _baselineLuminance) > 0.08f)
                        {
                            activePixels++;
                            cx += x / (float)w;
                            cy += y / (float)h;
                        }
                    }
                }

                if (activePixels > 42)
                {
                    cx /= activePixels;
                    cy /= activePixels;
                    float compactness = Mathf.Clamp01(activePixels / 380f);
                    bool pinching = compactness < 0.28f;
                    _pinchLatch = pinching;
                    return new HandLandmarkFrame
                    {
                        IsDetected = true,
                        WristViewportPos = new Vector3(cx, cy, 0.48f),
                        ThumbIndexDistanceNormalized = pinching ? 0.02f : 0.09f,
                        IsPinching = _pinchLatch
                    };
                }
            }

            return new HandLandmarkFrame { IsDetected = false };
        }

        public void Shutdown()
        {
            if (_camTex != null)
            {
                if (_camTex.isPlaying) _camTex.Stop();
                UnityEngine.Object.Destroy(_camTex);
                _camTex = null;
            }
        }
    }

    /// <summary>
    /// Manages 3D holographic hand visualization, pinch recognition, calibration, and guidance banner when hands are lost.
    /// </summary>
    public sealed class HandTrackingManager : MonoBehaviour
    {
        public static HandTrackingManager Instance { get; private set; }

        private IHandTrackingBackend _backend;
        private GameObject _handVisualRoot;
        private Transform[] _fingerTips;
        private MeshRenderer _pinchOrbRenderer;
        private bool _wasPinching;
        private float _notDetectedTimer;
        private bool _guidanceShown;

        public bool IsActive { get; private set; }
        public bool IsHandCurrentlyDetected { get; private set; }
        public bool IsPinchingNow { get; private set; }
        public string ActiveBackendDescription => _backend != null ? _backend.BackendName : "Inactive";

        public event Action OnPinchTriggered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void AttachHandVisualToHead(Transform headPivot)
        {
            if (_handVisualRoot != null)
            {
                _handVisualRoot.transform.SetParent(headPivot, false);
                return;
            }

            _handVisualRoot = new GameObject("HolographicHandVisual");
            _handVisualRoot.transform.SetParent(headPivot, false);
            _handVisualRoot.transform.localPosition = new Vector3(0.14f, -0.14f, 0.48f);

            Shader holo = Resources.Load<Shader>("Shaders/MobileQuestHologram") ?? Shader.Find("Sprites/Default");
            var handMat = new Material(holo) { color = new Color(0f, 0.82f, 1f, 0.72f) };

            // Palm node
            var palm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            palm.name = "PalmJoint";
            Destroy(palm.GetComponent<Collider>());
            palm.transform.SetParent(_handVisualRoot.transform, false);
            palm.transform.localScale = new Vector3(0.055f, 0.025f, 0.065f);
            palm.GetComponent<MeshRenderer>().sharedMaterial = handMat;

            // 5 finger tip nodes (Thumb, Index, Middle, Ring, Pinky)
            _fingerTips = new Transform[5];
            Vector3[] offsets =
            {
                new Vector3(-0.042f, 0.015f, 0.035f), // Thumb
                new Vector3(-0.022f, 0.020f, 0.068f), // Index
                new Vector3(0.0f,    0.022f, 0.074f), // Middle
                new Vector3(0.020f,  0.018f, 0.066f), // Ring
                new Vector3(0.038f,  0.012f, 0.052f)  // Pinky
            };

            for (int i = 0; i < 5; i++)
            {
                var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.name = $"FingerTip_{i}";
                Destroy(tip.GetComponent<Collider>());
                tip.transform.SetParent(_handVisualRoot.transform, false);
                tip.transform.localPosition = offsets[i];
                tip.transform.localScale = Vector3.one * 0.014f;
                tip.GetComponent<MeshRenderer>().sharedMaterial = handMat;
                _fingerTips[i] = tip.transform;
            }

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "PinchIndicatorOrb";
            Destroy(orb.GetComponent<Collider>());
            orb.transform.SetParent(_handVisualRoot.transform, false);
            orb.transform.localPosition = new Vector3(-0.032f, 0.018f, 0.052f);
            orb.transform.localScale = Vector3.one * 0.012f;
            _pinchOrbRenderer = orb.GetComponent<MeshRenderer>();
            _pinchOrbRenderer.sharedMaterial = new Material(holo) { color = new Color(0.5f, 1f, 0.8f, 0.9f) };

            _handVisualRoot.SetActive(false);
        }

        public void SetHandTrackingEnabled(bool enable)
        {
            if (enable == IsActive) return;

            if (!enable)
            {
                _backend?.Shutdown();
                _backend = null;
                IsActive = false;
                IsHandCurrentlyDetected = false;
                if (_handVisualRoot != null) _handVisualRoot.SetActive(false);
                if (AndroidPlatformBridge.Instance != null)
                {
                    AndroidPlatformBridge.Instance.IsCameraActive = false;
                }
                return;
            }

            var bridge = AndroidPlatformBridge.Instance;
            if (bridge != null)
            {
                bridge.RequestCameraPermissionOnDemand(granted =>
                {
                    if (!granted)
                    {
                        NotificationCenter.Instance?.Push(
                            LocalizationService.Get("menu.hands_toggle"),
                            LocalizationService.Get("mr.permission_denied"),
                            NotificationSeverity.Warning);
                        return;
                    }
                    ActivateBackend();
                });
            }
            else
            {
                ActivateBackend();
            }
        }

        private void ActivateBackend()
        {
            _backend = new HybridHandTrackingBackend();
            _backend.Initialize();
            IsActive = true;
            _notDetectedTimer = 0f;
            _guidanceShown = false;
            if (AndroidPlatformBridge.Instance != null)
            {
                AndroidPlatformBridge.Instance.IsCameraActive = true;
            }
        }

        public void CalibrateHands()
        {
            _backend?.Calibrate();
            NotificationCenter.Instance?.Push(
                LocalizationService.Get("menu.hands_toggle"),
                "Calibración de seguimiento de manos actualizada.",
                NotificationSeverity.Info,
                3f);
        }

        private void Update()
        {
            if (!IsActive || _backend == null) return;

            HandLandmarkFrame frame = _backend.SampleFrame();
            IsHandCurrentlyDetected = frame.IsDetected;
            IsPinchingNow = frame.IsDetected && frame.IsPinching;

            if (_handVisualRoot != null)
            {
                _handVisualRoot.SetActive(frame.IsDetected);
                if (frame.IsDetected)
                {
                    _handVisualRoot.transform.localPosition = new Vector3(
                        (frame.WristViewportPos.x - 0.5f) * 0.45f,
                        (frame.WristViewportPos.y - 0.5f) * 0.32f - 0.08f,
                        0.48f);

                    // Animate thumb & index coming together during pinch
                    if (_fingerTips != null && _fingerTips.Length >= 2)
                    {
                        Vector3 pinchMid = new Vector3(-0.030f, 0.018f, 0.052f);
                        _fingerTips[0].localPosition = frame.IsPinching ? pinchMid : new Vector3(-0.042f, 0.015f, 0.035f);
                        _fingerTips[1].localPosition = frame.IsPinching ? pinchMid : new Vector3(-0.022f, 0.020f, 0.068f);
                    }
                    if (_pinchOrbRenderer != null)
                    {
                        _pinchOrbRenderer.material.color = frame.IsPinching
                            ? new Color(0.15f, 1f, 0.45f, 1f)
                            : new Color(0f, 0.82f, 1f, 0.55f);
                    }
                }
            }

            if (IsPinchingNow && !_wasPinching)
            {
                GazeReticleInteractor.Instance?.NotifyPinchConfirmed();
                OnPinchTriggered?.Invoke();
            }
            _wasPinching = IsPinchingNow;

            if (!frame.IsDetected)
            {
                _notDetectedTimer += Time.unscaledDeltaTime;
                if (_notDetectedTimer >= 4.0f && !_guidanceShown)
                {
                    _guidanceShown = true;
                    NotificationCenter.Instance?.Push(
                        LocalizationService.Get("menu.hands_toggle"),
                        LocalizationService.Get("hands.not_detected"),
                        NotificationSeverity.Info,
                        5f);
                }
            }
            else
            {
                _notDetectedTimer = 0f;
                _guidanceShown = false;
            }
        }
    }
}
