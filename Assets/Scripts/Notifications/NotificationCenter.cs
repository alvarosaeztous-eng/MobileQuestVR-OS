using System;
using System.Collections.Generic;
using MobileQuestVR.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace MobileQuestVR.Notifications
{
    public enum NotificationSeverity
    {
        Info = 0,
        Comfort = 1,
        Warning = 2,
        Critical = 3,
        Privacy = 4
    }

    [Serializable]
    public sealed class NotificationEntry
    {
        public string Title;
        public string Message;
        public NotificationSeverity Severity;
        public string Timestamp;
    }

    /// <summary>
    /// Spatial 3D floating notification banner and history log (Sections 8, 13, 14).
    /// </summary>
    public sealed class NotificationCenter : MonoBehaviour
    {
        public static NotificationCenter Instance { get; private set; }

        private readonly List<NotificationEntry> _history = new List<NotificationEntry>();
        public IReadOnlyList<NotificationEntry> History => _history;
        public event Action<NotificationEntry> OnNotificationPushed;

        private GameObject _toastCanvasObj;
        private Text _toastTitleText;
        private Text _toastBodyText;
        private Image _toastBackground;
        private float _hideTimer;

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

        public void AttachFloatingToastToRig(Transform headAnchor)
        {
            if (_toastCanvasObj != null)
            {
                _toastCanvasObj.transform.SetParent(headAnchor, false);
                return;
            }

            _toastCanvasObj = new GameObject("SpatialNotificationToast");
            _toastCanvasObj.transform.SetParent(headAnchor, false);
            _toastCanvasObj.transform.localPosition = new Vector3(0f, 0.34f, 1.85f);
            _toastCanvasObj.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            _toastCanvasObj.transform.localScale = Vector3.one * 0.0014f;

            var canvas = _toastCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = _toastCanvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(560f, 110f);

            var bgObj = new GameObject("ToastBG");
            bgObj.transform.SetParent(_toastCanvasObj.transform, false);
            _toastBackground = bgObj.AddComponent<Image>();
            _toastBackground.color = new Color(0.05f, 0.11f, 0.22f, 0.93f);
            var bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Font font = LocalizationService.GetDefaultFont();

            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(bgObj.transform, false);
            _toastTitleText = titleObj.AddComponent<Text>();
            _toastTitleText.font = font;
            _toastTitleText.fontSize = 22;
            _toastTitleText.fontStyle = FontStyle.Bold;
            _toastTitleText.color = new Color(0f, 0.88f, 1f, 1f);
            _toastTitleText.alignment = TextAnchor.MiddleLeft;
            var tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.04f, 0.55f);
            tRect.anchorMax = new Vector2(0.96f, 0.92f);
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(bgObj.transform, false);
            _toastBodyText = bodyObj.AddComponent<Text>();
            _toastBodyText.font = font;
            _toastBodyText.fontSize = 18;
            _toastBodyText.color = new Color(0.90f, 0.95f, 1f, 1f);
            _toastBodyText.alignment = TextAnchor.UpperLeft;
            var bRect = bodyObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0.04f, 0.08f);
            bRect.anchorMax = new Vector2(0.96f, 0.55f);
            bRect.offsetMin = Vector2.zero;
            bRect.offsetMax = Vector2.zero;

            _toastCanvasObj.SetActive(false);
        }

        public void Push(string title, string message, NotificationSeverity severity = NotificationSeverity.Info, float durationSeconds = 4.2f)
        {
            var entry = new NotificationEntry
            {
                Title = title,
                Message = message,
                Severity = severity,
                Timestamp = DateTime.Now.ToString("HH:mm:ss")
            };
            _history.Insert(0, entry);
            if (_history.Count > 30) _history.RemoveAt(_history.Count - 1);

            if (_toastCanvasObj != null)
            {
                _toastTitleText.text = $"[{entry.Timestamp}] {title}";
                _toastBodyText.text = message;
                _toastBackground.color = severity switch
                {
                    NotificationSeverity.Critical => new Color(0.38f, 0.06f, 0.09f, 0.95f),
                    NotificationSeverity.Warning => new Color(0.32f, 0.20f, 0.05f, 0.94f),
                    NotificationSeverity.Privacy => new Color(0.07f, 0.25f, 0.16f, 0.94f),
                    _ => new Color(0.05f, 0.11f, 0.22f, 0.93f)
                };
                _toastCanvasObj.SetActive(true);
                _hideTimer = Mathf.Max(1.5f, durationSeconds);
            }

            OnNotificationPushed?.Invoke(entry);
        }

        private void Update()
        {
            if (_hideTimer > 0f)
            {
                _hideTimer -= Time.unscaledDeltaTime;
                if (_hideTimer <= 0f && _toastCanvasObj != null)
                {
                    _toastCanvasObj.SetActive(false);
                }
            }
        }
    }
}
