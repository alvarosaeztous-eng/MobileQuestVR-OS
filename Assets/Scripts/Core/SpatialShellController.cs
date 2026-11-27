using System;
using MobileQuestVR.AndroidBridge;
using MobileQuestVR.Audio;
using MobileQuestVR.Browser;
using MobileQuestVR.Environment;
using MobileQuestVR.Gallery;
using MobileQuestVR.HandTracking;
using MobileQuestVR.HeadTracking;
using MobileQuestVR.Localization;
using MobileQuestVR.Media;
using MobileQuestVR.Notifications;
using MobileQuestVR.Performance;
using MobileQuestVR.RoomCustomization;
using MobileQuestVR.Settings;
using MobileQuestVR.VRRendering;
using MobileQuestVR.Windows;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobileQuestVR.Core
{
    /// <summary>
    /// Quest-inspired 3D Spatial OS Shell:
    /// - Floating 3D Quick Dock + Status Bar (Time, Battery, Wi-Fi, Camera/Mic Privacy Badges)
    /// - Virtual Home Button & 3D Spatial Main Menu
    /// - Interactive Spatial Settings, Room Customization, Clock/Stopwatch/Timer, and Pause/Exit Confirmation
    /// </summary>
    public sealed class SpatialShellController : MonoBehaviour
    {
        private GameObject _dockObject;
        private GameObject _mainMenuObject;
        private GameObject _pauseModalObject;
        private GameObject _exitConfirmObject;

        private Text _statusBarClockText;
        private Text _statusBarTelemetryText;
        private Text _privacyBadgeText;
        private float _statusUpdateTimer;

        // Clock / Stopwatch / Timer state (Section 13)
        private bool _stopwatchRunning;
        private float _stopwatchSeconds;
        private bool _countdownRunning;
        private float _countdownRemainingSeconds = 300f;
        private Text _liveClockUtilityLabel;

        public void BuildSpatialShell()
        {
            BuildFloatingQuickDockAndStatusBar();
            BuildSpatialMainMenu3D();
            BuildPauseAndExitConfirmationModals();

            LocalizationService.OnLanguageChanged += _ => RebuildMenusOnLocaleChange();
        }

        private void RebuildMenusOnLocaleChange()
        {
            if (_dockObject != null) Destroy(_dockObject);
            if (_mainMenuObject != null) Destroy(_mainMenuObject);
            if (_pauseModalObject != null) Destroy(_pauseModalObject);
            BuildFloatingQuickDockAndStatusBar();
            BuildSpatialMainMenu3D();
            BuildPauseAndExitConfirmationModals();
        }

        private void BuildFloatingQuickDockAndStatusBar()
        {
            _dockObject = CreateCurvedSpatialPanel(
                "SpatialQuickDock3D",
                new Vector3(0f, 1.06f, 1.62f),
                Quaternion.Euler(22f, 0f, 0f),
                new Vector2(880f, 136f),
                0.00125f);

            Transform canvasRoot = _dockObject.transform.Find("PanelCanvas/Surface");

            // Status Bar Row (Time, Battery, Wi-Fi, Active Camera/Mic Privacy Indicators)
            _statusBarClockText = SpatialUIBuilder.CreateLabel(
                canvasRoot,
                DateTime.Now.ToString("HH:mm"),
                new Vector2(-365f, 42f),
                new Vector2(130f, 30f),
                20,
                TextAnchor.MiddleLeft,
                Color.white,
                true);

            _statusBarTelemetryText = SpatialUIBuilder.CreateLabel(
                canvasRoot,
                "BAT 85%  |  Wi-Fi  |  90 FPS",
                new Vector2(-95f, 42f),
                new Vector2(380f, 30f),
                15,
                TextAnchor.MiddleLeft,
                new Color(0.74f, 0.88f, 1f));

            _privacyBadgeText = SpatialUIBuilder.CreateLabel(
                canvasRoot,
                string.Empty,
                new Vector2(265f, 42f),
                new Vector2(330f, 30f),
                15,
                TextAnchor.MiddleRight,
                new Color(0.25f, 1f, 0.55f),
                true);

            // Customizable Quick Bar Buttons (Sections 2 & 13)
            Color homeColor = SettingsManager.Instance != null ? SettingsManager.Instance.GetThemeAccentColor() * 0.72f : new Color(0f, 0.62f, 0.85f, 0.96f);
            homeColor.a = 0.96f;
            Color quickCol = new Color(0.12f, 0.22f, 0.36f, 0.95f);

            (string label, Action act, Color col)[] quickButtons =
            {
                ("◉ " + LocalizationService.Get("menu.home"), ToggleMainMenu, homeColor),
                ("♫ Música", () => SpatialWebAppsController.Instance?.OpenSpotifyWindow(true), quickCol),
                ("⌂ Habitación", () => OSKernel.Instance?.CycleNextRoom(), quickCol),
                ("📷 MR", () => OSKernel.Instance?.ToggleMixedReality(), quickCol),
                ("⌖ Recentrar", () => HeadTracker3DoF.Instance?.RecenterView(), quickCol),
                ("🥽 VR/2D", () => StereoVRRig.Instance?.ToggleStereoVRMode(), quickCol),
                ("☀ Brillo", CycleBrightnessQuick, quickCol),
                ("🔊 Vol", CycleVolumeQuick, quickCol),
                ("🎤 Voz", () => SpatialAudioManager.Instance?.ActivateVoiceAssistantPushToTalk("recenter"), quickCol)
            };

            float startX = -384f;
            float spacing = 96f;
            for (int i = 0; i < quickButtons.Length; i++)
            {
                var item = quickButtons[i];
                SpatialUIBuilder.CreateSpatialButton(
                    canvasRoot,
                    item.label,
                    new Vector2(startX + i * spacing, -18f),
                    new Vector2(90f, 48f),
                    item.col,
                    item.act,
                    13);
            }
        }

        private void BuildSpatialMainMenu3D()
        {
            _mainMenuObject = CreateCurvedSpatialPanel(
                "SpatialMainMenu3D",
                new Vector3(0f, 1.62f, 1.92f),
                Quaternion.identity,
                new Vector2(820f, 520f),
                0.00135f);

            Transform root = _mainMenuObject.transform.Find("PanelCanvas/Surface");

            SpatialUIBuilder.CreateLabel(
                root,
                "MOBILEQUESTVR OS — " + LocalizationService.Get("menu.home").ToUpperInvariant(),
                new Vector2(0f, 215f),
                new Vector2(740f, 38f),
                23,
                TextAnchor.MiddleCenter,
                new Color(0f, 0.88f, 1f),
                true);

            (string title, Color tint, Action callback)[] launchers =
            {
                (LocalizationService.Get("app.youtube"),    new Color(0.68f, 0.14f, 0.18f, 0.95f), () => { SpatialWebAppsController.Instance?.OpenYouTubeWindow(); HideMainMenu(); }),
                (LocalizationService.Get("app.chrome"),     new Color(0.14f, 0.42f, 0.74f, 0.95f), () => { SpatialWebAppsController.Instance?.OpenChromeBrowserWindow(); HideMainMenu(); }),
                (LocalizationService.Get("app.google"),     new Color(0.18f, 0.48f, 0.82f, 0.95f), () => { SpatialWebAppsController.Instance?.OpenGoogleSearchWindow(); HideMainMenu(); }),
                (LocalizationService.Get("app.spotify"),    new Color(0.11f, 0.62f, 0.32f, 0.95f), () => { SpatialWebAppsController.Instance?.OpenSpotifyWindow(false); HideMainMenu(); }),

                (LocalizationService.Get("menu.media"),     new Color(0.32f, 0.20f, 0.58f, 0.95f), () => { MediaCenterController.Instance?.OpenMediaHubWindow(); HideMainMenu(); }),
                (LocalizationService.Get("menu.gallery"),   new Color(0.16f, 0.44f, 0.52f, 0.95f), () => { GalleryManager.Instance?.OpenGalleryWindow(); HideMainMenu(); }),
                (LocalizationService.Get("menu.rooms"),     new Color(0.14f, 0.38f, 0.56f, 0.95f), () => { OpenRoomSwitcherWindow(); HideMainMenu(); }),
                (LocalizationService.Get("menu.customize"), new Color(0.46f, 0.28f, 0.54f, 0.95f), () => { OpenRoomCustomizationWindow(); HideMainMenu(); }),

                (LocalizationService.Get("menu.clock"),     new Color(0.18f, 0.36f, 0.46f, 0.95f), () => { OpenClockStopwatchTimerWindow(); HideMainMenu(); }),
                (LocalizationService.Get("menu.hands_toggle"), new Color(0.14f, 0.46f, 0.44f, 0.95f), ToggleHandsFromMenu),
                (LocalizationService.Get("menu.arrange_windows"), new Color(0.18f, 0.34f, 0.54f, 0.95f), () => { SpatialWindowManager.Instance?.AutoArrangeArc(); HideMainMenu(); }),
                (LocalizationService.Get("menu.settings"),  new Color(0.22f, 0.30f, 0.46f, 0.95f), () => { OpenSettingsSpatialWindow(); HideMainMenu(); })
            };

            for (int i = 0; i < launchers.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                float x = -278f + col * 185f;
                float y = 115f - row * 102f;
                var entry = launchers[i];
                SpatialUIBuilder.CreateSpatialButton(root, entry.title, new Vector2(x, y), new Vector2(172f, 84f), entry.tint, entry.callback, 16);
            }

            // Bottom row: Panoramic Selector Scene & Pause/Exit
            SpatialUIBuilder.CreateSpatialButton(
                root,
                "Selector Panorámico 3D",
                new Vector2(-165f, -205f),
                new Vector2(280f, 44f),
                new Color(0.12f, 0.34f, 0.52f, 0.95f),
                () => SceneManager.LoadScene("01_RoomSelector"),
                16);

            SpatialUIBuilder.CreateSpatialButton(
                root,
                LocalizationService.Get("menu.pause") + " / Salir",
                new Vector2(165f, -205f),
                new Vector2(280f, 44f),
                new Color(0.55f, 0.16f, 0.22f, 0.95f),
                () =>
                {
                    HideMainMenu();
                    TogglePauseModal();
                },
                16);
        }

        private void BuildPauseAndExitConfirmationModals()
        {
            _pauseModalObject = CreateCurvedSpatialPanel(
                "PauseMenuModal3D",
                new Vector3(0f, 1.62f, 1.72f),
                Quaternion.identity,
                new Vector2(520f, 360f),
                0.00135f);

            Transform pRoot = _pauseModalObject.transform.Find("PanelCanvas/Surface");
            SpatialUIBuilder.CreateLabel(pRoot, LocalizationService.Get("pause.title"), new Vector2(0f, 125f), new Vector2(460f, 38f), 24, TextAnchor.MiddleCenter, Color.white, true);

            SpatialUIBuilder.CreateSpatialButton(pRoot, LocalizationService.Get("pause.continue"), new Vector2(0f, 52f), new Vector2(320f, 46f), new Color(0.10f, 0.48f, 0.64f, 0.95f), () => _pauseModalObject.SetActive(false), 18);
            SpatialUIBuilder.CreateSpatialButton(pRoot, LocalizationService.Get("pause.home"), new Vector2(0f, -6f), new Vector2(320f, 46f), new Color(0.16f, 0.34f, 0.52f, 0.95f), () => SceneManager.LoadScene("01_RoomSelector"), 18);
            SpatialUIBuilder.CreateSpatialButton(pRoot, LocalizationService.Get("pause.settings"), new Vector2(0f, -64f), new Vector2(320f, 46f), new Color(0.16f, 0.34f, 0.52f, 0.95f), () =>
            {
                _pauseModalObject.SetActive(false);
                OpenSettingsSpatialWindow();
            }, 18);
            SpatialUIBuilder.CreateSpatialButton(pRoot, LocalizationService.Get("pause.exit"), new Vector2(0f, -124f), new Vector2(320f, 46f), new Color(0.64f, 0.16f, 0.22f, 0.95f), () =>
            {
                _pauseModalObject.SetActive(false);
                _exitConfirmObject.SetActive(true);
            }, 18);
            _pauseModalObject.SetActive(false);

            // Mandatory Exit Confirmation Dialog (Sections 9 & 14)
            _exitConfirmObject = CreateCurvedSpatialPanel(
                "ExitConfirmationModal3D",
                new Vector3(0f, 1.62f, 1.68f),
                Quaternion.identity,
                new Vector2(540f, 260f),
                0.00135f);

            Transform eRoot = _exitConfirmObject.transform.Find("PanelCanvas/Surface");
            SpatialUIBuilder.CreateLabel(eRoot, LocalizationService.Get("exit.confirm_title"), new Vector2(0f, 55f), new Vector2(480f, 64f), 22, TextAnchor.MiddleCenter, Color.white, true);
            SpatialUIBuilder.CreateSpatialButton(eRoot, LocalizationService.Get("exit.confirm_no"), new Vector2(-120f, -52f), new Vector2(200f, 48f), new Color(0.18f, 0.38f, 0.56f, 0.95f), () => _exitConfirmObject.SetActive(false), 18);
            SpatialUIBuilder.CreateSpatialButton(eRoot, LocalizationService.Get("exit.confirm_yes"), new Vector2(120f, -52f), new Vector2(200f, 48f), new Color(0.72f, 0.14f, 0.20f, 0.95f), () =>
            {
                SettingsManager.Instance?.SaveAndNotify();
                RoomCustomizationManager.Instance?.SaveLocalState();
                Application.Quit();
            }, 18);
            _exitConfirmObject.SetActive(false);
        }

        public void ToggleMainMenu()
        {
            if (_mainMenuObject == null) return;
            _mainMenuObject.SetActive(!_mainMenuObject.activeSelf);
        }

        private void HideMainMenu()
        {
            if (_mainMenuObject != null) _mainMenuObject.SetActive(false);
        }

        public void TogglePauseModal()
        {
            if (_pauseModalObject == null) return;
            _exitConfirmObject.SetActive(false);
            _pauseModalObject.SetActive(!_pauseModalObject.activeSelf);
        }

        private void ToggleHandsFromMenu()
        {
            if (HandTrackingManager.Instance == null || SettingsManager.Instance == null) return;
            bool next = !HandTrackingManager.Instance.IsActive;
            SettingsManager.Instance.Data.HandTrackingEnabled = next;
            SettingsManager.Instance.SaveAndNotify();
            HandTrackingManager.Instance.SetHandTrackingEnabled(next);
        }

        public void OpenRoomSwitcherWindow()
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "RoomSwitcher3D",
                LocalizationService.Get("room.selector.title"),
                new Vector2(720f, 460f),
                win =>
                {
                    SpatialUIBuilder.CreateLabel(win.ContentRoot, LocalizationService.Get("room.static_notice"), new Vector2(0f, 158f), new Vector2(660f, 28f), 15, TextAnchor.MiddleCenter, new Color(0.7f, 0.9f, 1f));
                    for (int i = 0; i < 7; i++)
                    {
                        RoomId rid = (RoomId)i;
                        int col = i % 2;
                        int row = i / 2;
                        float x = col == 0 ? -165f : 165f;
                        float y = 104f - row * 64f;
                        bool fav = SettingsManager.Instance != null && SettingsManager.Instance.IsFavoriteRoom(rid);
                        string label = (fav ? "★ " : "") + LocalizationService.Get(RoomManager.GetRoomLocalizationKey(rid));
                        SpatialUIBuilder.CreateSpatialButton(
                            win.ContentRoot,
                            label,
                            new Vector2(x, y),
                            new Vector2(300f, 50f),
                            new Color(0.12f, 0.34f, 0.54f, 0.95f),
                            () =>
                            {
                                RoomManager.Instance?.LoadRoom(rid);
                                RoomCustomizationManager.Instance?.ApplySavedCustomizationToRoom(rid);
                            },
                            16);
                    }
                });
        }

        public void OpenRoomCustomizationWindow()
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "RoomCustomizer3D",
                LocalizationService.Get("custom.title"),
                new Vector2(680f, 450f),
                win =>
                {
                    RectTransform r = win.ContentRoot;
                    Color bc = new Color(0.16f, 0.34f, 0.54f, 0.95f);

                    SpatialUIBuilder.CreateSpatialButton(r, LocalizationService.Get("custom.furniture"), new Vector2(-155f, 115f), new Vector2(280f, 48f), bc, () =>
                    {
                        RoomCustomizationManager.Instance?.CycleFurniture(SettingsManager.Instance.Data.ActiveRoom);
                    }, 16);

                    SpatialUIBuilder.CreateSpatialButton(r, LocalizationService.Get("custom.decor"), new Vector2(155f, 115f), new Vector2(280f, 48f), bc, () =>
                    {
                        RoomCustomizationManager.Instance?.ToggleDecorations(SettingsManager.Instance.Data.ActiveRoom);
                    }, 16);

                    SpatialUIBuilder.CreateSpatialButton(r, LocalizationService.Get("custom.lighting"), new Vector2(-155f, 50f), new Vector2(280f, 48f), bc, () =>
                    {
                        RoomCustomizationManager.Instance?.CycleLightingColor(SettingsManager.Instance.Data.ActiveRoom);
                    }, 16);

                    SpatialUIBuilder.CreateSpatialButton(r, "★ " + LocalizationService.Get("room.favorite"), new Vector2(155f, 50f), new Vector2(280f, 48f), bc, () =>
                    {
                        SettingsManager.Instance?.ToggleFavoriteRoom(SettingsManager.Instance.Data.ActiveRoom);
                    }, 16);

                    SpatialUIBuilder.CreateSpatialButton(r, LocalizationService.Get("custom.day"), new Vector2(-205f, -18f), new Vector2(190f, 46f), new Color(0.18f, 0.48f, 0.72f, 0.95f), () =>
                    {
                        RoomCustomizationManager.Instance?.SetTimeOfDay(SettingsManager.Instance.Data.ActiveRoom, RoomTimeOfDay.Day);
                    }, 15);

                    SpatialUIBuilder.CreateSpatialButton(r, LocalizationService.Get("custom.sunset"), new Vector2(0f, -18f), new Vector2(190f, 46f), new Color(0.68f, 0.34f, 0.18f, 0.95f), () =>
                    {
                        RoomCustomizationManager.Instance?.SetTimeOfDay(SettingsManager.Instance.Data.ActiveRoom, RoomTimeOfDay.Sunset);
                    }, 15);

                    SpatialUIBuilder.CreateSpatialButton(r, LocalizationService.Get("custom.night"), new Vector2(205f, -18f), new Vector2(190f, 46f), new Color(0.14f, 0.20f, 0.44f, 0.95f), () =>
                    {
                        RoomCustomizationManager.Instance?.SetTimeOfDay(SettingsManager.Instance.Data.ActiveRoom, RoomTimeOfDay.Night);
                    }, 15);

                    SpatialUIBuilder.CreateSpatialButton(r, LocalizationService.Get("custom.ambient_audio"), new Vector2(-155f, -86f), new Vector2(280f, 46f), bc, () =>
                    {
                        SettingsManager.Instance.Data.AmbientSoundEnabled = !SettingsManager.Instance.Data.AmbientSoundEnabled;
                        SettingsManager.Instance.SaveAndNotify();
                    }, 15);

                    SpatialUIBuilder.CreateSpatialButton(r, LocalizationService.Get("custom.photo"), new Vector2(155f, -86f), new Vector2(280f, 46f), bc, () =>
                    {
                        GalleryManager.Instance?.OpenGalleryWindow();
                    }, 15);

                    SpatialUIBuilder.CreateSpatialButton(r, "💾 " + LocalizationService.Get("custom.save"), new Vector2(0f, -150f), new Vector2(360f, 48f), new Color(0.10f, 0.56f, 0.44f, 0.95f), () =>
                    {
                        RoomCustomizationManager.Instance?.SaveLocalState();
                    }, 17);
                });
        }

        public void OpenClockStopwatchTimerWindow()
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "ClockTimerUtility",
                LocalizationService.Get("menu.clock"),
                new Vector2(620f, 410f),
                win =>
                {
                    _liveClockUtilityLabel = SpatialUIBuilder.CreateLabel(
                        win.ContentRoot,
                        FormatClockUtilityReadout(),
                        new Vector2(0f, 75f),
                        new Vector2(560f, 130f),
                        21,
                        TextAnchor.MiddleCenter,
                        new Color(0.85f, 0.96f, 1f),
                        true);

                    Color c = new Color(0.14f, 0.38f, 0.58f, 0.95f);
                    SpatialUIBuilder.CreateSpatialButton(win.ContentRoot, "Cronómetro Start/Stop", new Vector2(-145f, -35f), new Vector2(250f, 46f), c, () => _stopwatchRunning = !_stopwatchRunning, 15);
                    SpatialUIBuilder.CreateSpatialButton(win.ContentRoot, "Reiniciar Cronómetro", new Vector2(145f, -35f), new Vector2(250f, 46f), c, () => { _stopwatchRunning = false; _stopwatchSeconds = 0f; }, 15);
                    SpatialUIBuilder.CreateSpatialButton(win.ContentRoot, "Temporizador 5m Start/Stop", new Vector2(-145f, -95f), new Vector2(250f, 46f), new Color(0.22f, 0.44f, 0.42f, 0.95f), () => _countdownRunning = !_countdownRunning, 15);
                    SpatialUIBuilder.CreateSpatialButton(win.ContentRoot, "Temporizador +1 Min", new Vector2(145f, -95f), new Vector2(250f, 46f), new Color(0.22f, 0.44f, 0.42f, 0.95f), () => _countdownRemainingSeconds += 60f, 15);
                });
        }

        public void OpenSettingsSpatialWindow()
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "App_Settings",
                LocalizationService.Get("app.settings"),
                new Vector2(760f, 490f),
                PopulateSettingsWindow);
        }

        private void PopulateSettingsWindow(SpatialWindow win)
        {
            RectTransform root = win.ContentRoot;
            var prefs = SettingsManager.Instance.Data;
            var hw = AndroidPlatformBridge.Instance != null ? AndroidPlatformBridge.Instance.HardwareReport : null;

            Text summary = SpatialUIBuilder.CreateLabel(
                root,
                BuildSettingsSummaryString(prefs, hw),
                new Vector2(0f, 128f),
                new Vector2(700f, 108f),
                15,
                TextAnchor.UpperLeft,
                new Color(0.84f, 0.95f, 1f));

            void RefreshSummary() => summary.text = BuildSettingsSummaryString(prefs, hw);

            Color c = new Color(0.14f, 0.34f, 0.54f, 0.95f);

            // Grid of all required OS, VR, Comfort & Privacy settings (Sections 4, 8, 11, 14)
            (string lbl, Action act)[] controls =
            {
                ("Tema / Colores", () => { prefs.Theme = (OSThemePreset)(((int)prefs.Theme + 1) % 4); SettingsManager.Instance.SaveAndNotify(); RefreshSummary(); }),
                ("Idioma ES / EN", () => { prefs.Language = prefs.Language == AppLanguage.Spanish ? AppLanguage.English : AppLanguage.Spanish; SettingsManager.Instance.SaveAndNotify(); RefreshSummary(); }),
                ("IPD Lentes (+1mm)", () => { prefs.InterpupillaryDistanceMm = prefs.InterpupillaryDistanceMm >= 70f ? 58f : prefs.InterpupillaryDistanceMm + 1f; SettingsManager.Instance.SaveAndNotify(); RefreshSummary(); }),
                ("Perfil Visor VR", () => { var next = (HeadsetViewerProfile)(((int)prefs.ViewerProfile + 1) % 4); SettingsManager.Instance.ApplyHeadsetProfilePreset(next); RefreshSummary(); }),

                ("Distorsión Lentes", () => { prefs.LensDistortionK1 = prefs.LensDistortionK1 >= 0.30f ? 0.0f : prefs.LensDistortionK1 + 0.06f; SettingsManager.Instance.SaveAndNotify(); RefreshSummary(); }),
                ("Distancia Ventanas", () => { prefs.WindowDistanceMeters = prefs.WindowDistanceMeters >= 3.2f ? 1.45f : prefs.WindowDistanceMeters + 0.35f; SettingsManager.Instance.SaveAndNotify(); RefreshSummary(); }),
                ("Tamaño Ventanas/UI", () => { prefs.WindowScale = prefs.WindowScale >= 1.3f ? 0.8f : prefs.WindowScale + 0.1f; SettingsManager.Instance.SaveAndNotify(); RefreshSummary(); }),
                ("Calidad Gráfica", () => { prefs.GraphicsQualityLevel = (prefs.GraphicsQualityLevel + 1) % 4; SettingsManager.Instance.SaveAndNotify(); RefreshSummary(); }),

                ("Mov. Reducido", () => { prefs.ReducedMotion = !prefs.ReducedMotion; prefs.ReduceEffects = prefs.ReducedMotion; SettingsManager.Instance.SaveAndNotify(); RefreshSummary(); }),
                ("Aviso Descanso", () => { prefs.RestReminderIntervalMinutes = prefs.RestReminderIntervalMinutes >= 45 ? 0 : prefs.RestReminderIntervalMinutes + 15; SettingsManager.Instance.SaveAndNotify(); RefreshSummary(); }),
                ("Calibrar Manos", () => { HandTrackingManager.Instance?.CalibrateHands(); RefreshSummary(); }),
                ("PIN Ajustes Sensibles", () =>
                {
                    if (prefs.ProtectSensitiveSettingsWithPin) SettingsManager.Instance.SetOptionalSettingsPin(string.Empty);
                    else SettingsManager.Instance.SetOptionalSettingsPin("1234");
                    RefreshSummary();
                })
            };

            for (int i = 0; i < controls.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                float x = -262f + col * 175f;
                float y = 22f - row * 62f;
                var item = controls[i];
                SpatialUIBuilder.CreateSpatialButton(root, item.lbl, new Vector2(x, y), new Vector2(166f, 50f), c, item.act, 14);
            }
        }

        private static string BuildSettingsSummaryString(OSPreferencesData p, DeviceCompatibilityReport hw)
        {
            string dev = hw != null ? $"{hw.DeviceModel} | {hw.GpuName} | {hw.CpuArchitecture} | SDK {hw.AndroidSdkInt}" : SystemInfo.deviceModel;
            string pinInfo = p.ProtectSensitiveSettingsWithPin ? "PIN Ajustes: ACTIVO (Sin PIN al arrancar)" : "PIN Ajustes: Desactivado (Sin PIN al arrancar)";
            return $"Perfil: {p.UserDisplayName}  |  Tema: {p.Theme}  |  Idioma: {p.Language}  |  {pinInfo}\n" +
                   $"Visor: {p.ViewerProfile} | IPD: {p.InterpupillaryDistanceMm:F0}mm | Distorsión K1: {p.LensDistortionK1:F2} | Ventanas: {p.WindowDistanceMeters:F2}m ({p.WindowScale:F1}x)\n" +
                   $"Calidad: Nivel {p.GraphicsQualityLevel} | Mov. Reducido: {(p.ReducedMotion ? "Sí" : "No")} | Descanso: {(p.RestReminderIntervalMinutes > 0 ? p.RestReminderIntervalMinutes + "m" : "Off")}\n" +
                   $"Sistema: {dev}";
        }

        private void CycleBrightnessQuick()
        {
            var prefs = SettingsManager.Instance.Data;
            prefs.Brightness = prefs.Brightness >= 1.1f ? 0.55f : prefs.Brightness + 0.15f;
            SettingsManager.Instance.SaveAndNotify();
        }

        private void CycleVolumeQuick()
        {
            var prefs = SettingsManager.Instance.Data;
            prefs.MasterVolume = prefs.MasterVolume >= 0.95f ? 0.25f : Mathf.Clamp01(prefs.MasterVolume + 0.25f);
            SettingsManager.Instance.SaveAndNotify();
        }

        private string FormatClockUtilityReadout()
        {
            TimeSpan sw = TimeSpan.FromSeconds(_stopwatchSeconds);
            TimeSpan cd = TimeSpan.FromSeconds(Mathf.Max(0f, _countdownRemainingSeconds));
            return $"Hora Local: {DateTime.Now:HH:mm:ss}   ({DateTime.Now:dd/MM/yyyy})\n" +
                   $"Cronómetro: {sw.Minutes:D2}:{sw.Seconds:D2}.{sw.Milliseconds / 100}\n" +
                   $"Temporizador: {cd.Minutes:D2}:{cd.Seconds:D2}";
        }

        private void Update()
        {
            if (_stopwatchRunning) _stopwatchSeconds += Time.unscaledDeltaTime;
            if (_countdownRunning)
            {
                _countdownRemainingSeconds -= Time.unscaledDeltaTime;
                if (_countdownRemainingSeconds <= 0f)
                {
                    _countdownRemainingSeconds = 0f;
                    _countdownRunning = false;
                    SpatialAudioManager.Instance?.PlayTimerAlarm();
                    NotificationCenter.Instance?.Push("Temporizador", "¡Tiempo completado!", NotificationSeverity.Info, 5f);
                }
            }

            _statusUpdateTimer += Time.unscaledDeltaTime;
            if (_statusUpdateTimer >= 0.5f)
            {
                _statusUpdateTimer = 0f;
                RefreshStatusBar();
                if (_liveClockUtilityLabel != null)
                {
                    _liveClockUtilityLabel.text = FormatClockUtilityReadout();
                }
            }
        }

        private void RefreshStatusBar()
        {
            if (_statusBarClockText != null)
            {
                _statusBarClockText.text = DateTime.Now.ToString("HH:mm");
            }

            var bridge = AndroidPlatformBridge.Instance;
            var perf = AdaptivePerformanceController.Instance;
            if (_statusBarTelemetryText != null && bridge != null)
            {
                int batPct = Mathf.RoundToInt(bridge.GetBatteryLevelNormalized() * 100f);
                string wifi = bridge.IsWifiConnected() ? "Wi-Fi ON" : (bridge.HasInternetConnection() ? "Datos" : "Offline");
                int fps = perf != null ? Mathf.RoundToInt(perf.CurrentFps) : 60;
                float temp = perf != null ? perf.CurrentTemperatureC : 34f;
                _statusBarTelemetryText.text = $"🔋 {batPct}%   |   📶 {wifi}   |   {fps} FPS · {temp:F0}°C";
            }

            if (_privacyBadgeText != null && bridge != null)
            {
                string badge = string.Empty;
                if (bridge.IsCameraActive) badge += LocalizationService.Get("privacy.camera_active") + "  ";
                if (bridge.IsMicrophoneActive) badge += LocalizationService.Get("privacy.mic_active");
                _privacyBadgeText.text = badge;
            }
        }

        public static GameObject CreateCurvedSpatialPanel(string name, Vector3 worldPos, Quaternion worldRot, Vector2 sizePx, float scale)
        {
            var root = new GameObject(name);
            root.transform.position = worldPos;
            root.transform.rotation = worldRot;

            // 3D Holographic glass backing
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "HoloSlab3D";
            Destroy(slab.GetComponent<Collider>());
            slab.transform.SetParent(root.transform, false);
            slab.transform.localPosition = new Vector3(0f, 0f, 0.015f);
            slab.transform.localScale = new Vector3(sizePx.x * scale + 0.025f, sizePx.y * scale + 0.025f, 0.02f);

            Shader holo = Resources.Load<Shader>("Shaders/MobileQuestHologram") ?? Shader.Find("Sprites/Default");
            slab.GetComponent<MeshRenderer>().sharedMaterial = new Material(holo);

            var canvasGo = new GameObject("PanelCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            canvasGo.transform.localScale = Vector3.one * scale;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.GetComponent<RectTransform>().sizeDelta = sizePx;

            var surf = new GameObject("Surface");
            surf.transform.SetParent(canvasGo.transform, false);
            var img = surf.AddComponent<Image>();
            img.color = SettingsManager.Instance != null ? SettingsManager.Instance.GetThemePanelColor() : new Color(0.06f, 0.10f, 0.20f, 0.91f);
            var srt = surf.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            return root;
        }
    }
}
