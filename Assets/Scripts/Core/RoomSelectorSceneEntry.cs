using MobileQuestVR.Environment;
using MobileQuestVR.HeadTracking;
using MobileQuestVR.Localization;
using MobileQuestVR.RoomCustomization;
using MobileQuestVR.Settings;
using MobileQuestVR.Windows;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobileQuestVR.Core
{
    /// <summary>
    /// Scene entry for 01_RoomSelector.unity:
    /// Runs the First-Boot Interactive Onboarding Wizard (Section 15) on initial launch,
    /// followed by the 360° Panoramic 7-Room Selector (Section 1 & 3).
    /// </summary>
    public sealed class RoomSelectorSceneEntry : MonoBehaviour
    {
        private GameObject _onboardingPanel;
        private GameObject _selectorPanel;
        private int _onboardingStepIndex;

        private void Start()
        {
            var kernel = OSKernel.EnsureInitialized();
            kernel.InitializeSceneRuntimeStack(spawnMainShell: false);

            RoomId initialRoom = SettingsManager.Instance != null ? SettingsManager.Instance.Data.ActiveRoom : RoomId.FuturisticBedroom;
            RoomManager.Instance?.LoadRoom(initialRoom);
            RoomCustomizationManager.Instance?.ApplySavedCustomizationToRoom(initialRoom);

            if (SettingsManager.Instance != null && !SettingsManager.Instance.Data.FirstBootCompleted)
            {
                BuildOnboardingStepUI(0);
            }
            else
            {
                BuildPanoramicRoomSelectorUI();
            }
        }

        private void BuildOnboardingStepUI(int step)
        {
            _onboardingStepIndex = step;
            if (_onboardingPanel != null) Destroy(_onboardingPanel);

            _onboardingPanel = SpatialShellController.CreateCurvedSpatialPanel(
                "FirstBootOnboarding3D",
                new Vector3(0f, 1.62f, 1.95f),
                Quaternion.identity,
                new Vector2(760f, 440f),
                0.00135f);

            Transform root = _onboardingPanel.transform.Find("PanelCanvas/Surface");
            var prefs = SettingsManager.Instance.Data;

            string titleKey = step switch
            {
                0 => "onboarding.welcome.title",
                1 => "onboarding.headset.title",
                2 => "onboarding.head.title",
                _ => "onboarding.pinch.title"
            };
            string bodyKey = step switch
            {
                0 => "onboarding.welcome.body",
                1 => "onboarding.headset.body",
                2 => "onboarding.head.body",
                _ => "onboarding.pinch.body"
            };

            SpatialUIBuilder.CreateLabel(root, $"PASO {step + 1}/4 — {LocalizationService.Get(titleKey)}", new Vector2(0f, 145f), new Vector2(680f, 44f), 22, TextAnchor.MiddleCenter, new Color(0f, 0.88f, 1f), true);
            SpatialUIBuilder.CreateLabel(root, LocalizationService.Get(bodyKey), new Vector2(0f, 52f), new Vector2(660f, 110f), 18, TextAnchor.MiddleCenter, Color.white);

            Color actionCol = new Color(0.12f, 0.44f, 0.64f, 0.95f);
            if (step == 0)
            {
                SpatialUIBuilder.CreateSpatialButton(root, "Español / English", new Vector2(-150f, -45f), new Vector2(240f, 46f), actionCol, () =>
                {
                    prefs.Language = prefs.Language == AppLanguage.Spanish ? AppLanguage.English : AppLanguage.Spanish;
                    SettingsManager.Instance.SaveAndNotify();
                    BuildOnboardingStepUI(0);
                }, 16);
                SpatialUIBuilder.CreateSpatialButton(root, LocalizationService.Get("onboarding.tips") + $": {(prefs.ShowOnboardingTips ? "ON" : "OFF")}", new Vector2(150f, -45f), new Vector2(280f, 46f), actionCol, () =>
                {
                    prefs.ShowOnboardingTips = !prefs.ShowOnboardingTips;
                    SettingsManager.Instance.SaveAndNotify();
                    BuildOnboardingStepUI(0);
                }, 15);
            }
            else if (step == 1)
            {
                SpatialUIBuilder.CreateSpatialButton(root, $"IPD: {prefs.InterpupillaryDistanceMm:F0} mm (+1)", new Vector2(-150f, -45f), new Vector2(250f, 46f), actionCol, () =>
                {
                    prefs.InterpupillaryDistanceMm = prefs.InterpupillaryDistanceMm >= 70f ? 58f : prefs.InterpupillaryDistanceMm + 1f;
                    SettingsManager.Instance.SaveAndNotify();
                    BuildOnboardingStepUI(1);
                }, 16);
                SpatialUIBuilder.CreateSpatialButton(root, $"Visor: {prefs.ViewerProfile}", new Vector2(150f, -45f), new Vector2(270f, 46f), actionCol, () =>
                {
                    var next = (HeadsetViewerProfile)(((int)prefs.ViewerProfile + 1) % 4);
                    SettingsManager.Instance.ApplyHeadsetProfilePreset(next);
                    BuildOnboardingStepUI(1);
                }, 15);
            }
            else if (step == 2)
            {
                SpatialUIBuilder.CreateSpatialButton(root, "⌖ Calibrar Orientación 3DoF Ahora", new Vector2(0f, -45f), new Vector2(380f, 48f), actionCol, () =>
                {
                    HeadTracker3DoF.Instance?.RecenterView();
                }, 17);
            }
            else
            {
                Text testFeedback = SpatialUIBuilder.CreateLabel(root, "Apunta aquí y selecciona para validar control espacial.", new Vector2(0f, -15f), new Vector2(560f, 32f), 16, TextAnchor.MiddleCenter, new Color(0.55f, 1f, 0.78f));
                SpatialUIBuilder.CreateSpatialButton(root, "✓ Probar Selección / Pinza", new Vector2(0f, -62f), new Vector2(320f, 46f), actionCol, () =>
                {
                    testFeedback.text = "¡Gesto/selección verificado con éxito!";
                }, 16);
            }

            SpatialUIBuilder.CreateSpatialButton(
                root,
                step < 3 ? LocalizationService.Get("onboarding.next") + " →" : LocalizationService.Get("onboarding.room.title") + " →",
                new Vector2(0f, -148f),
                new Vector2(360f, 50f),
                new Color(0.08f, 0.58f, 0.48f, 0.96f),
                () =>
                {
                    if (step < 3)
                    {
                        BuildOnboardingStepUI(step + 1);
                    }
                    else
                    {
                        prefs.FirstBootCompleted = true;
                        SettingsManager.Instance.SaveAndNotify();
                        Destroy(_onboardingPanel);
                        BuildPanoramicRoomSelectorUI();
                    }
                },
                18);
        }

        private void BuildPanoramicRoomSelectorUI()
        {
            if (_selectorPanel != null) Destroy(_selectorPanel);

            _selectorPanel = SpatialShellController.CreateCurvedSpatialPanel(
                "PanoramicRoomSelector3D",
                new Vector3(0f, 1.56f, 2.05f),
                Quaternion.identity,
                new Vector2(860f, 500f),
                0.00135f);

            Transform root = _selectorPanel.transform.Find("PanelCanvas/Surface");

            SpatialUIBuilder.CreateLabel(
                root,
                LocalizationService.Get("room.selector.title").ToUpperInvariant(),
                new Vector2(0f, 204f),
                new Vector2(780f, 38f),
                23,
                TextAnchor.MiddleCenter,
                new Color(0f, 0.88f, 1f),
                true);

            SpatialUIBuilder.CreateLabel(
                root,
                "Mira a tu alrededor en 360° para previsualizar cada entorno antes de iniciar el escritorio espacial.",
                new Vector2(0f, 164f),
                new Vector2(780f, 28f),
                15,
                TextAnchor.MiddleCenter,
                new Color(0.76f, 0.90f, 1f));

            for (int i = 0; i < 7; i++)
            {
                RoomId rid = (RoomId)i;
                int col = i % 4;
                int row = i / 4;
                float x = -285f + col * 190f;
                float y = 78f - row * 104f;

                bool isSelected = SettingsManager.Instance.Data.ActiveRoom == rid;
                bool isFav = SettingsManager.Instance.IsFavoriteRoom(rid);
                Color cardCol = isSelected
                    ? new Color(0.06f, 0.54f, 0.72f, 0.96f)
                    : new Color(0.12f, 0.24f, 0.40f, 0.94f);

                string label = (isFav ? "★ " : "") + LocalizationService.Get(RoomManager.GetRoomLocalizationKey(rid));
                SpatialUIBuilder.CreateSpatialButton(
                    root,
                    label,
                    new Vector2(x, y),
                    new Vector2(178f, 86f),
                    cardCol,
                    () =>
                    {
                        RoomManager.Instance?.LoadRoom(rid);
                        RoomCustomizationManager.Instance?.ApplySavedCustomizationToRoom(rid);
                        BuildPanoramicRoomSelectorUI();
                    },
                    15);
            }

            SpatialUIBuilder.CreateSpatialButton(
                root,
                "★ Marcar Favorita",
                new Vector2(-190f, -182f),
                new Vector2(240f, 52f),
                new Color(0.22f, 0.32f, 0.50f, 0.95f),
                () =>
                {
                    SettingsManager.Instance.ToggleFavoriteRoom(SettingsManager.Instance.Data.ActiveRoom);
                    BuildPanoramicRoomSelectorUI();
                },
                16);

            SpatialUIBuilder.CreateSpatialButton(
                root,
                LocalizationService.Get("room.enter") + " ▶",
                new Vector2(140f, -182f),
                new Vector2(360f, 54f),
                new Color(0.06f, 0.62f, 0.48f, 0.97f),
                () => SceneManager.LoadScene("02_MainOS"),
                19);
        }
    }
}
