using System;
using System.Collections.Generic;
using MobileQuestVR.Settings;
using MobileQuestVR.VRRendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobileQuestVR.HeadTracking
{
    /// <summary>
    /// 3DoF Head Orientation Tracker using hardware Gyroscope + Accelerometer fallback + touch/mouse pan.
    /// Explicitly reports 3DoF only (never claims 6DoF positional tracking on 3DoF mobile hardware).
    /// </summary>
    public sealed class HeadTracker3DoF : MonoBehaviour
    {
        public static HeadTracker3DoF Instance { get; private set; }

        public bool IsHardwareGyroActive { get; private set; }
        public string TrackingModeLabel => IsHardwareGyroActive ? "3DoF Gyroscope" : "3DoF Touch / Sensor Fallback";

        private Quaternion _calibrationYawInverse = Quaternion.identity;
        private bool _hasInitialCalibration;
        private float _fallbackYaw;
        private float _fallbackPitch;
        private Vector2 _lastPointerPos;
        private bool _draggingLook;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true;
                Input.gyro.updateInterval = 1f / 90f;
                IsHardwareGyroActive = true;
            }
        }

        private void Update()
        {
            var rig = StereoVRRig.Instance;
            if (rig == null || rig.HeadPivot == null) return;

            var prefs = SettingsManager.Instance != null ? SettingsManager.Instance.Data : null;
            bool reducedMotion = prefs != null && prefs.ReducedMotion;
            float smoothing = reducedMotion ? 8f : 22f;

            HandleDragLookInput();

            Quaternion targetRot;
            if (IsHardwareGyroActive && !Application.isEditor)
            {
                Quaternion rawGyro = ReadUnityGyroAttitude();
                if (!_hasInitialCalibration && rawGyro != Quaternion.identity)
                {
                    CalibrateFromRaw(rawGyro);
                    _hasInitialCalibration = true;
                }
                Quaternion gyroRelative = _calibrationYawInverse * rawGyro;
                targetRot = Quaternion.Euler(_fallbackPitch, _fallbackYaw, 0f) * gyroRelative;
            }
            else
            {
                targetRot = Quaternion.Euler(_fallbackPitch, _fallbackYaw, 0f);
            }

            rig.HeadPivot.localRotation = Quaternion.Slerp(
                rig.HeadPivot.localRotation,
                targetRot,
                Mathf.Clamp01(Time.unscaledDeltaTime * smoothing));
        }

        private void HandleDragLookInput()
        {
            // Allow touch drag (single finger drag) or right-mouse drag in Editor/Mono mode
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Moved)
                {
                    _fallbackYaw += t.deltaPosition.x * 0.14f;
                    _fallbackPitch = Mathf.Clamp(_fallbackPitch - t.deltaPosition.y * 0.12f, -82f, 82f);
                }
            }
            else if (Input.GetMouseButton(1))
            {
                if (!_draggingLook)
                {
                    _draggingLook = true;
                    _lastPointerPos = Input.mousePosition;
                }
                else
                {
                    Vector2 delta = (Vector2)Input.mousePosition - _lastPointerPos;
                    _lastPointerPos = Input.mousePosition;
                    _fallbackYaw += delta.x * 0.22f;
                    _fallbackPitch = Mathf.Clamp(_fallbackPitch - delta.y * 0.20f, -82f, 82f);
                }
            }
            else
            {
                _draggingLook = false;
            }
        }

        private static Quaternion ReadUnityGyroAttitude()
        {
            Quaternion q = Input.gyro.attitude;
            // Convert right-handed Android sensor frame to left-handed Unity landscape camera frame
            Quaternion converted = new Quaternion(q.x, q.y, -q.z, -q.w);
            return Quaternion.Euler(90f, 0f, 0f) * converted;
        }

        private void CalibrateFromRaw(Quaternion raw)
        {
            Vector3 euler = raw.eulerAngles;
            _calibrationYawInverse = Quaternion.Inverse(Quaternion.Euler(0f, euler.y, 0f));
        }

        public void RecenterView()
        {
            _fallbackYaw = 0f;
            _fallbackPitch = 0f;
            if (IsHardwareGyroActive)
            {
                CalibrateFromRaw(ReadUnityGyroAttitude());
            }

            if (StereoVRRig.Instance != null && StereoVRRig.Instance.HeadPivot != null)
            {
                StereoVRRig.Instance.HeadPivot.localRotation = Quaternion.identity;
            }
        }
    }

    /// <summary>
    /// Central gaze selection dot with dwell, tap, and pinch-gesture trigger support for 3D UI & spatial windows.
    /// </summary>
    public sealed class GazeReticleInteractor : MonoBehaviour
    {
        public static GazeReticleInteractor Instance { get; private set; }

        public float MaxRayDistance = 12f;
        public float DwellActivationSeconds = 1.35f;

        private Transform _reticleTransform;
        private MeshRenderer _reticleRenderer;
        private Material _reticleMat;

        private Selectable _currentHoveredSelectable;
        private float _dwellTimer;
        private bool _dwellFiredForCurrent;
        private bool _externalPinchRequested;

        public Vector3 CurrentHitWorldPoint { get; private set; }
        public bool IsHoveringInteractive => _currentHoveredSelectable != null;
        public float DwellNormalized => _currentHoveredSelectable != null ? Mathf.Clamp01(_dwellTimer / DwellActivationSeconds) : 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureEventSystem();
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("SpatialEventSystem");
                DontDestroyOnLoad(esObj);
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }
        }

        public void AttachReticleToHead(Transform headPivot)
        {
            if (_reticleTransform != null)
            {
                _reticleTransform.SetParent(headPivot, false);
                return;
            }

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "CenterSelectionReticle";
            Destroy(sphere.GetComponent<Collider>());
            _reticleTransform = sphere.transform;
            _reticleTransform.SetParent(headPivot, false);
            _reticleTransform.localPosition = new Vector3(0f, 0f, 1.8f);
            _reticleTransform.localScale = Vector3.one * 0.018f;

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _reticleMat = new Material(shader) { color = new Color(0f, 0.9f, 1f, 0.92f) };
            _reticleRenderer = sphere.GetComponent<MeshRenderer>();
            _reticleRenderer.sharedMaterial = _reticleMat;
            _reticleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _reticleRenderer.receiveShadows = false;
        }

        public void NotifyPinchConfirmed()
        {
            _externalPinchRequested = true;
        }

        private void Update()
        {
            var rig = StereoVRRig.Instance;
            if (rig == null || rig.HeadPivot == null) return;

            Ray gazeRay = new Ray(rig.HeadPivot.position, rig.HeadPivot.forward);
            CurrentHitWorldPoint = gazeRay.GetPoint(2.0f);

            Selectable foundSelectable = RaycastWorldCanvases(gazeRay, out float hitDist, out Vector3 hitPos);
            if (foundSelectable != null)
            {
                CurrentHitWorldPoint = hitPos;
                UpdateReticleVisual(Mathf.Clamp(hitDist - 0.02f, 0.45f, 6.0f), true);
            }
            else if (Physics.Raycast(gazeRay, out RaycastHit physHit, MaxRayDistance))
            {
                CurrentHitWorldPoint = physHit.point;
                foundSelectable = physHit.collider.GetComponentInParent<Selectable>();
                UpdateReticleVisual(Mathf.Clamp(physHit.distance - 0.02f, 0.45f, 6.0f), foundSelectable != null);
            }
            else
            {
                UpdateReticleVisual(1.9f, false);
            }

            if (foundSelectable != _currentHoveredSelectable)
            {
                _currentHoveredSelectable = foundSelectable;
                _dwellTimer = 0f;
                _dwellFiredForCurrent = false;
            }

            bool tapOrClick = Input.GetMouseButtonDown(0) ||
                              (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) ||
                              _externalPinchRequested;
            _externalPinchRequested = false;

            if (_currentHoveredSelectable != null && _currentHoveredSelectable.IsInteractable())
            {
                _dwellTimer += Time.unscaledDeltaTime;
                if (tapOrClick || (!_dwellFiredForCurrent && _dwellTimer >= DwellActivationSeconds))
                {
                    _dwellFiredForCurrent = true;
                    ExecuteSelectableAction(_currentHoveredSelectable);
                }
            }
        }

        private Selectable RaycastWorldCanvases(Ray gazeRay, out float bestDist, out Vector3 bestWorldPos)
        {
            bestDist = MaxRayDistance;
            bestWorldPos = gazeRay.GetPoint(2.0f);
            Selectable bestSelectable = null;

            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int c = 0; c < canvases.Length; c++)
            {
                Canvas canvas = canvases[c];
                if (!canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.WorldSpace) continue;

                Plane plane = new Plane(canvas.transform.forward, canvas.transform.position);
                if (!plane.Raycast(gazeRay, out float enter) || enter <= 0.1f || enter > bestDist) continue;

                Vector3 worldHit = gazeRay.GetPoint(enter);
                var buttons = canvas.GetComponentsInChildren<Selectable>(false);
                for (int b = 0; b < buttons.Length; b++)
                {
                    var rt = buttons[b].transform as RectTransform;
                    if (rt == null) continue;
                    Vector3 localPt = rt.InverseTransformPoint(worldHit);
                    if (rt.rect.Contains(new Vector2(localPt.x, localPt.y)))
                    {
                        bestDist = enter;
                        bestWorldPos = worldHit;
                        bestSelectable = buttons[b];
                    }
                }
            }

            return bestSelectable;
        }

        private static void ExecuteSelectableAction(Selectable selectable)
        {
            if (selectable is Button btn)
            {
                btn.onClick?.Invoke();
            }
            else if (selectable is Toggle toggle)
            {
                toggle.isOn = !toggle.isOn;
            }
            else if (selectable is Slider slider)
            {
                float step = (slider.maxValue - slider.minValue) * 0.125f;
                float next = slider.value + step;
                if (next > slider.maxValue + 0.001f) next = slider.minValue;
                slider.value = next;
            }
        }

        private void UpdateReticleVisual(float distance, bool hovering)
        {
            if (_reticleTransform == null) return;
            _reticleTransform.localPosition = new Vector3(0f, 0f, distance);
            float scale = distance * (hovering ? 0.015f + DwellNormalized * 0.008f : 0.0095f);
            _reticleTransform.localScale = Vector3.one * scale;
            if (_reticleMat != null)
            {
                var accent = SettingsManager.Instance != null ? SettingsManager.Instance.GetThemeAccentColor() : new Color(0f, 0.88f, 1f, 1f);
                _reticleMat.color = hovering ? Color.Lerp(accent, Color.white, DwellNormalized * 0.6f) : new Color(accent.r, accent.g, accent.b, 0.8f);
            }
        }
    }
}
