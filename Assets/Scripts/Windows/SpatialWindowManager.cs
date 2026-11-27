using System;
using System.Collections.Generic;
using MobileQuestVR.Audio;
using MobileQuestVR.HeadTracking;
using MobileQuestVR.Localization;
using MobileQuestVR.Settings;
using MobileQuestVR.VRRendering;
using UnityEngine;
using UnityEngine.UI;

namespace MobileQuestVR.Windows
{
    /// <summary>
    /// Reusable helper for constructing crisp 3D spatial UI controls with depth and Quest-inspired styling.
    /// </summary>
    public static class SpatialUIBuilder
    {
        public static Button CreateSpatialButton(
            Transform parent,
            string label,
            Vector2 anchoredPos,
            Vector2 size,
            Color bgColor,
            Action onClick,
            int fontSize = 18)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.8f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.55f, 0.85f, 1f, 1f);
            btn.colors = colors;

            btn.onClick.AddListener(() =>
            {
                SpatialAudioManager.Instance?.PlaySpatialClick(go.transform.position);
                onClick?.Invoke();
            });

            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(go.transform, false);
            var txt = txtObj.AddComponent<Text>();
            txt.font = LocalizationService.GetDefaultFont();
            txt.text = label;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.94f, 0.98f, 1f, 1f);

            var trt = txtObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6f, 2f);
            trt.offsetMax = new Vector2(-6f, -2f);

            return btn;
        }

        public static Text CreateLabel(
            Transform parent,
            string content,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize = 20,
            TextAnchor align = TextAnchor.MiddleCenter,
            Color? color = null,
            bool bold = false)
        {
            var go = new GameObject("TextLabel");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.font = LocalizationService.GetDefaultFont();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            txt.alignment = align;
            txt.color = color ?? new Color(0.90f, 0.95f, 1f, 1f);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return txt;
        }
    }

    /// <summary>
    /// A single 3D floating spatial window with holographic 3D slab depth, title bar,
    /// Move (gaze follow), Minimize, Maximize, and Close controls.
    /// </summary>
    public sealed class SpatialWindow : MonoBehaviour
    {
        public string WindowId { get; private set; }
        public string Title { get; private set; }
        public RectTransform ContentRoot { get; private set; }
        public bool IsMinimized { get; private set; }
        public bool IsMaximized { get; private set; }
        public bool IsFollowingGazeMove { get; private set; }

        private Vector2 _normalSize = new Vector2(680f, 440f);
        private RectTransform _canvasRect;
        private Transform _slab3DBacking;
        private Text _titleLabel;
        private SpatialWindowManager _owner;
        private float _yawAngleDeg;
        private float _elevationMeters;

        public float YawAngleDeg => _yawAngleDeg;

        public void Initialize(SpatialWindowManager owner, string id, string title, Vector2 pixelSize, float yawDeg)
        {
            _owner = owner;
            WindowId = id;
            Title = title;
            _normalSize = pixelSize;
            _yawAngleDeg = yawDeg;

            Build3DWindowStructure();
            ApplySpatialPose();
        }

        private void Build3DWindowStructure()
        {
            // 3D Holographic Depth Slab behind the canvas (avoids flat 2D web appearance per Section 16)
            var slabObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slabObj.name = "HolographicDepthSlab3D";
            Destroy(slabObj.GetComponent<Collider>());
            _slab3DBacking = slabObj.transform;
            _slab3DBacking.SetParent(transform, false);
            _slab3DBacking.localPosition = new Vector3(0f, 0f, 0.018f);

            Shader holoShader = Resources.Load<Shader>("Shaders/MobileQuestHologram") ?? Shader.Find("Sprites/Default");
            var slabMat = new Material(holoShader);
            Color accent = SettingsManager.Instance != null ? SettingsManager.Instance.GetThemeAccentColor() : new Color(0f, 0.84f, 1f, 1f);
            if (slabMat.HasProperty("_RimColor")) slabMat.SetColor("_RimColor", accent);
            slabObj.GetComponent<MeshRenderer>().sharedMaterial = slabMat;

            // WorldSpace Canvas
            var canvasObj = new GameObject("WindowCanvas");
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            _canvasRect = canvasObj.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = _normalSize;
            canvasObj.transform.localScale = Vector3.one * 0.00145f;

            var bgObj = new GameObject("WindowSurfaceBG");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = SettingsManager.Instance != null ? SettingsManager.Instance.GetThemePanelColor() : new Color(0.06f, 0.10f, 0.20f, 0.92f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Header bar
            var headerObj = new GameObject("HeaderBar");
            headerObj.transform.SetParent(bgObj.transform, false);
            var headerImg = headerObj.AddComponent<Image>();
            headerImg.color = new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.35f, 0.95f);
            var hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = Vector2.zero;
            hRect.sizeDelta = new Vector2(0f, 48f);

            _titleLabel = SpatialUIBuilder.CreateLabel(
                headerObj.transform,
                Title,
                new Vector2(-110f, 0f),
                new Vector2(360f, 40f),
                20,
                TextAnchor.MiddleLeft,
                Color.white,
                true);

            // Window Chrome Controls: Move, Minimize, Maximize, Close
            float rightX = _normalSize.x * 0.5f - 32f;
            SpatialUIBuilder.CreateSpatialButton(headerObj.transform, "✕", new Vector2(rightX, 0f), new Vector2(44f, 34f), new Color(0.68f, 0.16f, 0.22f, 0.95f), () => _owner.CloseWindow(WindowId), 17);
            SpatialUIBuilder.CreateSpatialButton(headerObj.transform, "▢", new Vector2(rightX - 50f, 0f), new Vector2(44f, 34f), new Color(0.18f, 0.32f, 0.52f, 0.95f), ToggleMaximize, 17);
            SpatialUIBuilder.CreateSpatialButton(headerObj.transform, "—", new Vector2(rightX - 100f, 0f), new Vector2(44f, 34f), new Color(0.18f, 0.32f, 0.52f, 0.95f), () => _owner.MinimizeWindow(WindowId), 17);
            SpatialUIBuilder.CreateSpatialButton(headerObj.transform, "✥", new Vector2(rightX - 150f, 0f), new Vector2(44f, 34f), new Color(0.12f, 0.45f, 0.58f, 0.95f), ToggleMoveMode, 17);

            // Content body container
            var contentObj = new GameObject("ContentArea");
            contentObj.transform.SetParent(bgObj.transform, false);
            ContentRoot = contentObj.AddComponent<RectTransform>();
            ContentRoot.anchorMin = Vector2.zero;
            ContentRoot.anchorMax = Vector2.one;
            ContentRoot.offsetMin = new Vector2(14f, 14f);
            ContentRoot.offsetMax = new Vector2(-14f, -56f);

            SyncSlabDimensions();
        }

        private void SyncSlabDimensions()
        {
            if (_slab3DBacking == null || _canvasRect == null) return;
            Vector2 sz = _canvasRect.sizeDelta * 0.00145f;
            _slab3DBacking.localScale = new Vector3(sz.x + 0.03f, sz.y + 0.03f, 0.022f);
        }

        public void SetYawAndElevation(float yawDeg, float elevationM = 0f)
        {
            _yawAngleDeg = yawDeg;
            _elevationMeters = elevationM;
            ApplySpatialPose();
        }

        public void ResizeWindow(Vector2 newPixelSize)
        {
            _normalSize = newPixelSize;
            if (_canvasRect != null && !IsMaximized)
            {
                _canvasRect.sizeDelta = _normalSize;
                SyncSlabDimensions();
            }
        }

        public void ToggleMaximize()
        {
            IsMaximized = !IsMaximized;
            if (_canvasRect != null)
            {
                _canvasRect.sizeDelta = IsMaximized ? _normalSize * 1.32f : _normalSize;
                SyncSlabDimensions();
            }
            ApplySpatialPose();
        }

        public void ToggleMoveMode()
        {
            IsFollowingGazeMove = !IsFollowingGazeMove;
            if (_titleLabel != null)
            {
                _titleLabel.text = IsFollowingGazeMove ? $"{Title} [MOVIENDO...]" : Title;
            }
        }

        public void SetMinimizedState(bool minimized)
        {
            IsMinimized = minimized;
            gameObject.SetActive(!minimized);
        }

        public void ApplySpatialPose()
        {
            var prefs = SettingsManager.Instance != null ? SettingsManager.Instance.Data : null;
            float dist = prefs != null ? Mathf.Clamp(prefs.WindowDistanceMeters, 1.25f, 3.85f) : 2.15f;
            float scale = prefs != null ? Mathf.Clamp(prefs.WindowScale * prefs.UiScale, 0.65f, 1.65f) : 1.0f;

            float rad = _yawAngleDeg * Mathf.Deg2Rad;
            Vector3 eyeCenter = new Vector3(0f, 1.62f, 0f);
            Vector3 offset = new Vector3(Mathf.Sin(rad) * dist, _elevationMeters, Mathf.Cos(rad) * dist);
            transform.position = eyeCenter + offset;
            transform.rotation = Quaternion.LookRotation(transform.position - eyeCenter, Vector3.up);
            transform.localScale = Vector3.one * scale;
        }

        private void Update()
        {
            if (!IsFollowingGazeMove) return;
            var rig = StereoVRRig.Instance;
            if (rig != null && rig.HeadPivot != null)
            {
                Vector3 fwd = rig.HeadPivot.forward;
                _yawAngleDeg = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                _elevationMeters = Mathf.Clamp(fwd.y * 1.6f, -0.55f, 0.75f);
                ApplySpatialPose();
            }
        }
    }

    /// <summary>
    /// Enforces up to 3 simultaneous visible floating spatial windows, auto-arc layout,
    /// minimize/restore, and live window distance/size scaling (Sections 2 & 5).
    /// </summary>
    public sealed class SpatialWindowManager : MonoBehaviour
    {
        public static SpatialWindowManager Instance { get; private set; }
        public const int MaxSimultaneousVisibleWindows = 3;

        private readonly List<SpatialWindow> _windows = new List<SpatialWindow>();
        public IReadOnlyList<SpatialWindow> AllWindows => _windows;

        public event Action OnWindowListChanged;
        public event Action OnWindowLimitReached;

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
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged += _ => RefreshAllWindowPoses();
            }
        }

        public int VisibleWindowCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < _windows.Count; i++)
                {
                    if (_windows[i] != null && !_windows[i].IsMinimized) c++;
                }
                return c;
            }
        }

        public SpatialWindow FindWindow(string windowId)
        {
            for (int i = 0; i < _windows.Count; i++)
            {
                if (_windows[i] != null && _windows[i].WindowId == windowId)
                {
                    return _windows[i];
                }
            }
            return null;
        }

        public SpatialWindow OpenOrFocusWindow(string windowId, string title, Vector2 size, Action<SpatialWindow> populateBuilder)
        {
            var existing = FindWindow(windowId);
            if (existing != null)
            {
                if (existing.IsMinimized)
                {
                    if (VisibleWindowCount >= MaxSimultaneousVisibleWindows)
                    {
                        OnWindowLimitReached?.Invoke();
                        return null;
                    }
                    existing.SetMinimizedState(false);
                    AutoArrangeArc();
                    OnWindowListChanged?.Invoke();
                }
                return existing;
            }

            if (VisibleWindowCount >= MaxSimultaneousVisibleWindows)
            {
                OnWindowLimitReached?.Invoke();
                return null;
            }

            float defaultYaw = VisibleWindowCount switch
            {
                0 => 0f,
                1 => -30f,
                _ => 30f
            };

            var winGo = new GameObject($"SpatialWindow_{windowId}");
            winGo.transform.SetParent(transform, false);
            var win = winGo.AddComponent<SpatialWindow>();
            win.Initialize(this, windowId, title, size, defaultYaw);
            _windows.Add(win);

            populateBuilder?.Invoke(win);
            AutoArrangeArc();
            OnWindowListChanged?.Invoke();
            return win;
        }

        public void MinimizeWindow(string windowId)
        {
            var w = FindWindow(windowId);
            if (w == null) return;
            w.SetMinimizedState(true);
            AutoArrangeArc();
            OnWindowListChanged?.Invoke();
        }

        public void CloseWindow(string windowId)
        {
            var w = FindWindow(windowId);
            if (w == null) return;
            _windows.Remove(w);
            Destroy(w.gameObject);
            AutoArrangeArc();
            OnWindowListChanged?.Invoke();
        }

        public void CloseAllWindows()
        {
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                if (_windows[i] != null) Destroy(_windows[i].gameObject);
            }
            _windows.Clear();
            OnWindowListChanged?.Invoke();
        }

        public void AutoArrangeArc()
        {
            var visible = new List<SpatialWindow>();
            for (int i = 0; i < _windows.Count; i++)
            {
                if (_windows[i] != null && !_windows[i].IsMinimized)
                {
                    visible.Add(_windows[i]);
                }
            }

            if (visible.Count == 1)
            {
                visible[0].SetYawAndElevation(0f, 0.05f);
            }
            else if (visible.Count == 2)
            {
                visible[0].SetYawAndElevation(-22f, 0.05f);
                visible[1].SetYawAndElevation(22f, 0.05f);
            }
            else if (visible.Count >= 3)
            {
                visible[0].SetYawAndElevation(-34f, 0.05f);
                visible[1].SetYawAndElevation(0f, 0.05f);
                visible[2].SetYawAndElevation(34f, 0.05f);
            }
        }

        public void RefreshAllWindowPoses()
        {
            for (int i = 0; i < _windows.Count; i++)
            {
                if (_windows[i] != null) _windows[i].ApplySpatialPose();
            }
        }
    }
}
