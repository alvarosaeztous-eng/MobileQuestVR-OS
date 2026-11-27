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

namespace MobileQuestVR.Core
{
    /// <summary>
    /// Central OS Kernel orchestrating all modular services across scenes.
    /// Ensures zero startup PIN (Section 14), intercepts Android Back key for exit confirmation,
    /// and binds head/hand/passthrough/notification anchors.
    /// </summary>
    public sealed class OSKernel : MonoBehaviour
    {
        public static OSKernel Instance { get; private set; }

        public MixedRealityPassthroughController MixedReality { get; private set; }
        public SpatialShellController SpatialShell { get; private set; }

        public static OSKernel EnsureInitialized()
        {
            if (Instance != null) return Instance;
            var kernelObj = new GameObject("MobileQuestVR_OSKernel");
            DontDestroyOnLoad(kernelObj);
            return kernelObj.AddComponent<OSKernel>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Input.backButtonLeavesApp = false;

            EnsureSubsystem<AndroidPlatformBridge>("Sub_AndroidBridge");
            EnsureSubsystem<SettingsManager>("Sub_Settings");
            EnsureSubsystem<NotificationCenter>("Sub_Notifications");
            EnsureSubsystem<AdaptivePerformanceController>("Sub_Performance");
            var audio = EnsureSubsystem<SpatialAudioManager>("Sub_SpatialAudio");
            audio.OnVoiceCommandRecognized += HandleVoiceIntent;
        }

        public void InitializeSceneRuntimeStack(bool spawnMainShell)
        {
            var rig = FindFirstObjectByType<StereoVRRig>();
            if (rig == null)
            {
                var rigObj = new GameObject("StereoVRRig_Root");
                rig = rigObj.AddComponent<StereoVRRig>();
            }

            var headTracker = FindFirstObjectByType<HeadTracker3DoF>();
            if (headTracker == null)
            {
                headTracker = rig.gameObject.AddComponent<HeadTracker3DoF>();
            }

            var gaze = FindFirstObjectByType<GazeReticleInteractor>();
            if (gaze == null)
            {
                gaze = rig.gameObject.AddComponent<GazeReticleInteractor>();
            }
            gaze.AttachReticleToHead(rig.HeadPivot);

            var handMgr = FindFirstObjectByType<HandTrackingManager>();
            if (handMgr == null)
            {
                handMgr = rig.gameObject.AddComponent<HandTrackingManager>();
            }
            handMgr.AttachHandVisualToHead(rig.HeadPivot);

            MixedReality = rig.GetComponent<MixedRealityPassthroughController>();
            if (MixedReality == null)
            {
                MixedReality = rig.gameObject.AddComponent<MixedRealityPassthroughController>();
            }
            MixedReality.AttachToHeadTransform(rig.HeadPivot);
            MixedReality.OnPassthroughError += errKey =>
            {
                NotificationCenter.Instance?.Push(
                    LocalizationService.Get("menu.mr_toggle"),
                    LocalizationService.Get(errKey),
                    NotificationSeverity.Warning);
            };

            NotificationCenter.Instance?.AttachFloatingToastToRig(rig.HeadPivot);

            EnsureSceneLocalComponent<RoomManager>("RoomManager_3D");
            EnsureSceneLocalComponent<RoomCustomizationManager>("RoomCustomization_Manager");
            var winMgr = EnsureSceneLocalComponent<SpatialWindowManager>("SpatialWindow_Manager");
            winMgr.OnWindowLimitReached += () =>
            {
                NotificationCenter.Instance?.Push(
                    "Multitarea Espacial",
                    LocalizationService.Get("window.limit_reached"),
                    NotificationSeverity.Warning,
                    4f);
            };

            EnsureSceneLocalComponent<MediaCenterController>("MediaCenter_360");
            EnsureSceneLocalComponent<GalleryManager>("Gallery_Local");
            EnsureSceneLocalComponent<SpatialWebAppsController>("SpatialWebApps");

            if (spawnMainShell)
            {
                RoomId activeRoom = SettingsManager.Instance != null ? SettingsManager.Instance.Data.ActiveRoom : RoomId.FuturisticBedroom;
                RoomManager.Instance.LoadRoom(activeRoom);
                RoomCustomizationManager.Instance.ApplySavedCustomizationToRoom(activeRoom);

                SpatialShell = EnsureSceneLocalComponent<SpatialShellController>("SpatialShell_QuestUI");
                SpatialShell.BuildSpatialShell();

                if (SettingsManager.Instance != null && SettingsManager.Instance.Data.HandTrackingEnabled)
                {
                    HandTrackingManager.Instance?.SetHandTrackingEnabled(true);
                }
            }
        }

        private T EnsureSubsystem<T>(string childName) where T : Component
        {
            var existing = FindFirstObjectByType<T>();
            if (existing != null) return existing;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        private static T EnsureSceneLocalComponent<T>(string objName) where T : Component
        {
            var existing = FindFirstObjectByType<T>();
            if (existing != null) return existing;
            var go = new GameObject(objName);
            return go.AddComponent<T>();
        }

        private void HandleVoiceIntent(string intent)
        {
            switch (intent)
            {
                case "settings":
                    SpatialShell?.OpenSettingsSpatialWindow();
                    break;
                case "mr":
                    ToggleMixedReality();
                    break;
                case "room":
                    CycleNextRoom();
                    break;
                default:
                    HeadTracker3DoF.Instance?.RecenterView();
                    NotificationCenter.Instance?.Push(
                        LocalizationService.Get("menu.voice"),
                        "Vista recentrada correctamente.",
                        NotificationSeverity.Info,
                        2.5f);
                    break;
            }
        }

        public void ToggleMixedReality()
        {
            if (MixedReality == null || RoomManager.Instance == null) return;
            MixedReality.TogglePassthrough(RoomManager.Instance.EnvironmentContainer);
            if (MixedReality.IsPassthroughEnabled)
            {
                NotificationCenter.Instance?.Push(
                    LocalizationService.Get("menu.mr_toggle"),
                    LocalizationService.Get("mr.experimental"),
                    NotificationSeverity.Privacy,
                    4.5f);
            }
        }

        public void CycleNextRoom()
        {
            if (RoomManager.Instance == null || SettingsManager.Instance == null) return;
            int next = ((int)SettingsManager.Instance.Data.ActiveRoom + 1) % 7;
            var nextRoom = (RoomId)next;
            RoomManager.Instance.LoadRoom(nextRoom);
            RoomCustomizationManager.Instance?.ApplySavedCustomizationToRoom(nextRoom);
        }

        private void Update()
        {
            // Android Hardware Back key opens Pause & Exit Confirmation (Sections 9 & 14)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (SpatialShell != null)
                {
                    SpatialShell.TogglePauseModal();
                }
                else if (SceneManager.GetActiveScene().buildIndex > 0)
                {
                    Application.Quit();
                }
            }
        }
    }
}
