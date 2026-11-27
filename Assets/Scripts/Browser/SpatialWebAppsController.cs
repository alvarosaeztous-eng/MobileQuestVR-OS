using System.Collections;
using MobileQuestVR.AndroidBridge;
using MobileQuestVR.Localization;
using MobileQuestVR.Notifications;
using MobileQuestVR.Settings;
using MobileQuestVR.Windows;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MobileQuestVR.Browser
{
    /// <summary>
    /// Spatial Web & Multitasking Apps Controller (Section 5):
    /// - YouTube: Normal floating window
    /// - Chrome & Google: Floating spatial browser with URL navigation & search
    /// - Spotify: Toggleable Mini-Player & Full Window modes
    /// Respects provider DRM/embedding policies; never claims arbitrary external APK execution in 3D windows.
    /// </summary>
    public sealed class SpatialWebAppsController : MonoBehaviour
    {
        public static SpatialWebAppsController Instance { get; private set; }

        private bool _spotifyMiniMode = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void OpenYouTubeWindow()
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "App_YouTube",
                LocalizationService.Get("app.youtube"),
                new Vector2(720f, 460f),
                win => BuildWebServiceSurface(
                    win,
                    "YouTube",
                    "https://m.youtube.com",
                    new Color(0.75f, 0.12f, 0.16f, 0.95f),
                    new[]
                    {
                        ("Explorar Vídeos 360° VR", "https://m.youtube.com/results?search_query=360+vr+space"),
                        ("Música Ambiental Lo-Fi", "https://m.youtube.com/results?search_query=ambient+synth+relax"),
                        ("Inicio YouTube", "https://m.youtube.com")
                    }));
        }

        public void OpenChromeBrowserWindow(string initialUrl = "https://www.google.com")
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "App_ChromeBrowser",
                LocalizationService.Get("app.chrome"),
                new Vector2(740f, 470f),
                win => BuildWebServiceSurface(
                    win,
                    "Chrome",
                    initialUrl,
                    new Color(0.14f, 0.44f, 0.78f, 0.95f),
                    new[]
                    {
                        ("Google", "https://www.google.com"),
                        ("Wikipedia ES", "https://es.m.wikipedia.org"),
                        ("Noticias Tech", "https://news.google.com")
                    }));
        }

        public void OpenGoogleSearchWindow()
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "App_Google",
                LocalizationService.Get("app.google"),
                new Vector2(680f, 440f),
                win => BuildWebServiceSurface(
                    win,
                    "Google",
                    "https://www.google.com",
                    new Color(0.18f, 0.48f, 0.85f, 0.95f),
                    new[]
                    {
                        ("Búsqueda Web", "https://www.google.com"),
                        ("Google Imágenes", "https://images.google.com"),
                        ("Google Maps Web", "https://maps.google.com")
                    }));
        }

        public void OpenSpotifyWindow(bool startAsMiniPlayer = false)
        {
            _spotifyMiniMode = startAsMiniPlayer;
            var existing = SpatialWindowManager.Instance?.FindWindow("App_Spotify");
            if (existing != null)
            {
                SpatialWindowManager.Instance.CloseWindow("App_Spotify");
            }

            Vector2 targetSize = _spotifyMiniMode ? new Vector2(440f, 230f) : new Vector2(700f, 450f);
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "App_Spotify",
                _spotifyMiniMode ? "Spotify — " + LocalizationService.Get("app.spotify.mini") : "Spotify — " + LocalizationService.Get("app.spotify.full"),
                targetSize,
                BuildSpotifyWindowContent);
        }

        private void BuildSpotifyWindowContent(SpatialWindow win)
        {
            RectTransform root = win.ContentRoot;
            Color spotifyGreen = new Color(0.11f, 0.72f, 0.33f, 0.95f);

            if (_spotifyMiniMode)
            {
                SpatialUIBuilder.CreateLabel(root, "♫ Spotify Web Mini-Player", new Vector2(0f, 42f), new Vector2(390f, 32f), 18, TextAnchor.MiddleCenter, Color.white, true);
                SpatialUIBuilder.CreateSpatialButton(root, "⏮", new Vector2(-120f, -15f), new Vector2(64f, 40f), spotifyGreen, () => Application.OpenURL("https://open.spotify.com"), 16);
                SpatialUIBuilder.CreateSpatialButton(root, "⏯ Web", new Vector2(-20f, -15f), new Vector2(110f, 40f), spotifyGreen, () => LaunchUrlRespectingPolicies("https://open.spotify.com"), 16);
                SpatialUIBuilder.CreateSpatialButton(root, LocalizationService.Get("app.spotify.full"), new Vector2(115f, -15f), new Vector2(140f, 40f), new Color(0.16f, 0.30f, 0.44f, 0.95f), () => OpenSpotifyWindow(false), 14);
                return;
            }

            SpatialUIBuilder.CreateLabel(
                root,
                "Spotify Web Player — Multitarea Espacial",
                new Vector2(-90f, 150f),
                new Vector2(440f, 34f),
                20,
                TextAnchor.MiddleLeft,
                spotifyGreen,
                true);

            SpatialUIBuilder.CreateSpatialButton(
                root,
                LocalizationService.Get("app.spotify.mini"),
                new Vector2(215f, 150f),
                new Vector2(180f, 38f),
                new Color(0.14f, 0.34f, 0.48f, 0.95f),
                () => OpenSpotifyWindow(true),
                15);

            SpatialUIBuilder.CreateSpatialButton(
                root,
                "★ Favorito",
                new Vector2(215f, 102f),
                new Vector2(180f, 36f),
                new Color(0.24f, 0.28f, 0.42f, 0.95f),
                () => SettingsManager.Instance?.ToggleFavoriteApp("Spotify"),
                15);

            SpatialUIBuilder.CreateSpatialButton(root, "Abrir Spotify Web (DRM Compatible)", new Vector2(-90f, 55f), new Vector2(410f, 46f), spotifyGreen, () => LaunchUrlRespectingPolicies("https://open.spotify.com"), 17);
            SpatialUIBuilder.CreateSpatialButton(root, "Lista: Deep Focus Spatial", new Vector2(-90f, -2f), new Vector2(410f, 42f), new Color(0.12f, 0.28f, 0.22f, 0.95f), () => LaunchUrlRespectingPolicies("https://open.spotify.com/genre/focus-page"), 16);
            SpatialUIBuilder.CreateSpatialButton(root, "Lista: Synthwave & Sci-Fi", new Vector2(-90f, -54f), new Vector2(410f, 42f), new Color(0.12f, 0.28f, 0.22f, 0.95f), () => LaunchUrlRespectingPolicies("https://open.spotify.com/search/synthwave"), 16);

            SpatialUIBuilder.CreateLabel(
                root,
                LocalizationService.Get("web.restriction_note"),
                new Vector2(0f, -135f),
                new Vector2(630f, 48f),
                14,
                TextAnchor.MiddleCenter,
                new Color(0.68f, 0.78f, 0.90f));
        }

        private void BuildWebServiceSurface(
            SpatialWindow win,
            string appKey,
            string defaultUrl,
            Color brandColor,
            (string title, string url)[] presets)
        {
            RectTransform root = win.ContentRoot;
            bool online = AndroidPlatformBridge.Instance == null || AndroidPlatformBridge.Instance.HasInternetConnection();

            Text addressBar = SpatialUIBuilder.CreateLabel(
                root,
                $"URL: {defaultUrl}  [{(online ? "ONLINE" : "SIN CONEXIÓN")}]",
                new Vector2(-75f, 152f),
                new Vector2(480f, 34f),
                16,
                TextAnchor.MiddleLeft,
                new Color(0.82f, 0.94f, 1f),
                true);

            SpatialUIBuilder.CreateSpatialButton(
                root,
                "★ Fav",
                new Vector2(245f, 152f),
                new Vector2(110f, 36f),
                new Color(0.22f, 0.30f, 0.48f, 0.95f),
                () => SettingsManager.Instance?.ToggleFavoriteApp(appKey),
                15);

            Text statusBody = SpatialUIBuilder.CreateLabel(
                root,
                online
                    ? $"Conectado. Selecciona un acceso rápido o abre {defaultUrl} respetando políticas CORS/DRM del servicio web."
                    : "Sin conexión a internet. Las aplicaciones web requieren Wi-Fi/datos; tus habitaciones y galería local siguen activas.",
                new Vector2(0f, 65f),
                new Vector2(640f, 95f),
                16,
                TextAnchor.MiddleCenter,
                new Color(0.88f, 0.93f, 1f));

            string currentUrl = defaultUrl;
            for (int i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                float x = (i - 1) * 215f;
                SpatialUIBuilder.CreateSpatialButton(
                    root,
                    preset.title,
                    new Vector2(x, -25f),
                    new Vector2(200f, 44f),
                    brandColor,
                    () =>
                    {
                        currentUrl = preset.url;
                        addressBar.text = $"URL: {currentUrl}";
                        StartCoroutine(FetchWebPagePreviewSummary(currentUrl, statusBody));
                    },
                    15);
            }

            SpatialUIBuilder.CreateSpatialButton(
                root,
                "Abrir Vista Web Completa en Android",
                new Vector2(0f, -88f),
                new Vector2(380f, 44f),
                new Color(0.10f, 0.46f, 0.62f, 0.95f),
                () => LaunchUrlRespectingPolicies(currentUrl),
                16);

            SpatialUIBuilder.CreateLabel(
                root,
                LocalizationService.Get("web.restriction_note"),
                new Vector2(0f, -148f),
                new Vector2(650f, 44f),
                13,
                TextAnchor.MiddleCenter,
                new Color(0.65f, 0.76f, 0.88f));
        }

        private IEnumerator FetchWebPagePreviewSummary(string url, Text targetLabel)
        {
            if (targetLabel != null) targetLabel.text = $"Consultando {url} ...";
            using UnityWebRequest req = UnityWebRequest.Get(url);
            req.timeout = 6;
            yield return req.SendWebRequest();

            if (targetLabel == null) yield break;
            if (req.result == UnityWebRequest.Result.Success)
            {
                string raw = req.downloadHandler.text ?? string.Empty;
                string pageTitle = ExtractHtmlTitle(raw);
                targetLabel.text = $"Respuesta HTTP {req.responseCode} recibida.\nTítulo detectado: {pageTitle}\nPulsa 'Abrir Vista Web Completa' para reproducción multimedia interactiva.";
            }
            else
            {
                targetLabel.text = $"No se pudo previsualizar en segundo plano ({req.error}). Puedes abrir la versión web oficial directamente.";
            }
        }

        private static string ExtractHtmlTitle(string html)
        {
            int i0 = html.IndexOf("<title", System.StringComparison.OrdinalIgnoreCase);
            if (i0 < 0) return "Web Service";
            int gt = html.IndexOf('>', i0);
            int i1 = html.IndexOf("</title>", gt + 1, System.StringComparison.OrdinalIgnoreCase);
            if (gt < 0 || i1 < 0 || i1 <= gt) return "Web Service";
            string t = html.Substring(gt + 1, Mathf.Min(80, i1 - gt - 1)).Trim();
            return string.IsNullOrEmpty(t) ? "Web Service" : t;
        }

        private static void LaunchUrlRespectingPolicies(string url)
        {
            if (AndroidPlatformBridge.Instance != null && !AndroidPlatformBridge.Instance.HasInternetConnection())
            {
                NotificationCenter.Instance?.Push(
                    "Red no disponible",
                    "Conéctate a Wi-Fi para acceder a servicios web online.",
                    NotificationSeverity.Warning);
                return;
            }
            Application.OpenURL(url);
        }
    }
}
