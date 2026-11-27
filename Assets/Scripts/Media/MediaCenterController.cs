using System;
using System.IO;
using MobileQuestVR.Environment;
using MobileQuestVR.Localization;
using MobileQuestVR.Notifications;
using MobileQuestVR.Settings;
using MobileQuestVR.VRRendering;
using MobileQuestVR.Windows;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MobileQuestVR.Media
{
    /// <summary>
    /// Multimedia subsystem (Section 12):
    /// - Normal 2D Video Player & Photo Viewer in spatial 3D windows
    /// - 360° Equirectangular Photo & 360° Video Sphere Viewer
    /// - Ambient Video binding onto Cinema Screen / Room Decorative Surfaces
    /// - Normal & Stereoscopic VR Screenshots + Experimental VR Frame Recorder
    /// </summary>
    public sealed class MediaCenterController : MonoBehaviour
    {
        public static MediaCenterController Instance { get; private set; }

        private GameObject _sphere360Object;
        private Material _sphere360Material;
        private VideoPlayer _videoPlayer;
        private RenderTexture _videoRenderTexture;
        private bool _is360ModeActive;

        // Experimental VR capture/recording state
        private bool _isExperimentalRecording;
        private float _recordFrameTimer;
        private int _recordedFramesCount;
        private string _activeRecordingSessionDir;

        public bool Is360ViewerActive => _is360ModeActive;
        public bool IsRecordingVR => _isExperimentalRecording;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SetupVideoPipeline();
            BuildSphere360Dome();
        }

        private void SetupVideoPipeline()
        {
            _videoRenderTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
            _videoRenderTexture.Create();

            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = true;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _videoRenderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        }

        private void BuildSphere360Dome()
        {
            _sphere360Object = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere360Object.name = "Equirectangular360ViewerSphere";
            Destroy(_sphere360Object.GetComponent<Collider>());
            _sphere360Object.transform.SetParent(transform, false);
            _sphere360Object.transform.position = new Vector3(0f, 1.62f, 0f);
            _sphere360Object.transform.localScale = Vector3.one * 38f;

            Shader s360 = Resources.Load<Shader>("Shaders/MobileQuestSphere360") ?? Shader.Find("MobileQuestVR/Sphere360") ?? Shader.Find("Unlit/Texture");
            _sphere360Material = new Material(s360);
            _sphere360Object.GetComponent<MeshRenderer>().sharedMaterial = _sphere360Material;
            _sphere360Object.SetActive(false);
        }

        public void OpenMediaHubWindow()
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "MediaHub360",
                LocalizationService.Get("menu.media"),
                new Vector2(700f, 460f),
                PopulateMediaHubUI);
        }

        private void PopulateMediaHubUI(SpatialWindow win)
        {
            RectTransform root = win.ContentRoot;
            SpatialUIBuilder.CreateLabel(
                root,
                "Reproductor 2D / 360°, Pantalla de Cine y Capturas VR",
                new Vector2(0f, 155f),
                new Vector2(640f, 34f),
                19,
                TextAnchor.MiddleCenter,
                new Color(0.65f, 0.90f, 1f),
                true);

            // Preview RawImage inside window
            var previewObj = new GameObject("MediaSurfacePreview");
            previewObj.transform.SetParent(root, false);
            var rawImg = previewObj.AddComponent<RawImage>();
            rawImg.texture = GenerateProceduralAuroraPanorama();
            var prt = previewObj.GetComponent<RectTransform>();
            prt.anchoredPosition = new Vector2(0f, 38f);
            prt.sizeDelta = new Vector2(460f, 185f);

            Color btnCol = new Color(0.10f, 0.34f, 0.56f, 0.95f);
            SpatialUIBuilder.CreateSpatialButton(root, "Foto/Pano 360°", new Vector2(-210f, -88f), new Vector2(195f, 44f), btnCol, () =>
            {
                Toggle360PanoramaTexture(GenerateProceduralAuroraPanorama());
            }, 16);

            SpatialUIBuilder.CreateSpatialButton(root, "Salir Modo 360°", new Vector2(0f, -88f), new Vector2(195f, 44f), new Color(0.28f, 0.22f, 0.48f, 0.95f), Exit360Viewer, 16);

            SpatialUIBuilder.CreateSpatialButton(root, "Enviar a Pantalla Cine", new Vector2(210f, -88f), new Vector2(195f, 44f), btnCol, () =>
            {
                BindAmbientVisualToRoomScreen(GenerateProceduralAuroraPanorama());
            }, 16);

            SpatialUIBuilder.CreateSpatialButton(root, "Captura Normal", new Vector2(-210f, -144f), new Vector2(195f, 44f), new Color(0.12f, 0.46f, 0.42f, 0.95f), CaptureNormalScreenshot, 16);
            SpatialUIBuilder.CreateSpatialButton(root, "Captura Estéreo VR", new Vector2(0f, -144f), new Vector2(195f, 44f), new Color(0.12f, 0.46f, 0.42f, 0.95f), CaptureStereoVRScreenshot, 16);
            SpatialUIBuilder.CreateSpatialButton(root, "Grabación VR (Exp.)", new Vector2(210f, -144f), new Vector2(195f, 44f), new Color(0.54f, 0.20f, 0.26f, 0.95f), ToggleExperimentalVRRecording, 16);
        }

        public void OpenLocalFileInMediaViewer(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".mp4" || ext == ".webm" || ext == ".mkv")
            {
                bool is360 = filePath.IndexOf("360", StringComparison.OrdinalIgnoreCase) >= 0;
                PlayLocalVideo(filePath, is360);
            }
            else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    bool is360 = filePath.IndexOf("360", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 filePath.IndexOf("pano", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 Mathf.Abs((tex.width / (float)Mathf.Max(1, tex.height)) - 2.0f) < 0.15f;
                    if (is360)
                    {
                        Toggle360PanoramaTexture(tex);
                    }
                    else
                    {
                        OpenNormalPhotoWindow(Path.GetFileName(filePath), tex);
                    }
                }
            }
        }

        public void OpenNormalPhotoWindow(string title, Texture2D photo)
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "PhotoViewer2D",
                $"Foto: {title}",
                new Vector2(640f, 440f),
                win =>
                {
                    var imgObj = new GameObject("PhotoRawImage");
                    imgObj.transform.SetParent(win.ContentRoot, false);
                    var raw = imgObj.AddComponent<RawImage>();
                    raw.texture = photo;
                    var rt = imgObj.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.05f, 0.08f);
                    rt.anchorMax = new Vector2(0.95f, 0.92f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                });
        }

        public void PlayLocalVideo(string pathOrUrl, bool equirectangular360)
        {
            _videoPlayer.url = pathOrUrl;
            _videoPlayer.Play();

            if (equirectangular360)
            {
                Toggle360PanoramaTexture(_videoRenderTexture);
            }
            else
            {
                BindAmbientVisualToRoomScreen(_videoRenderTexture);
            }
        }

        public void Toggle360PanoramaTexture(Texture equirectangularTex)
        {
            if (_sphere360Object == null) return;
            _sphere360Material.mainTexture = equirectangularTex;
            _is360ModeActive = true;
            _sphere360Object.SetActive(true);
            if (RoomManager.Instance != null && RoomManager.Instance.EnvironmentContainer != null)
            {
                RoomManager.Instance.EnvironmentContainer.SetActive(false);
            }
            NotificationCenter.Instance?.Push("Visor 360°", "Modo inmersivo 360° activado. Mira alrededor.", NotificationSeverity.Info, 3f);
        }

        public void Exit360Viewer()
        {
            _is360ModeActive = false;
            if (_sphere360Object != null) _sphere360Object.SetActive(false);
            if (RoomManager.Instance != null && RoomManager.Instance.EnvironmentContainer != null)
            {
                RoomManager.Instance.EnvironmentContainer.SetActive(true);
            }
        }

        public void BindAmbientVisualToRoomScreen(Texture tex)
        {
            var room = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : null;
            if (room != null && room.AmbientVideoScreenRenderer != null)
            {
                room.AmbientVideoScreenRenderer.material.mainTexture = tex;
                NotificationCenter.Instance?.Push("Pantalla Ambiental", "Contenido proyectado en la superficie de la habitación.", NotificationSeverity.Info, 3f);
            }
        }

        public void CaptureNormalScreenshot()
        {
            string dir = EnsureMediaDirectory();
            string path = Path.Combine(dir, $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            NotificationCenter.Instance?.Push("Captura Guardada", Path.GetFileName(path), NotificationSeverity.Info, 3f);
        }

        public void CaptureStereoVRScreenshot()
        {
            string dir = EnsureMediaDirectory();
            string path = Path.Combine(dir, $"VRCapture_Stereo_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            var rig = StereoVRRig.Instance;
            if (rig != null && rig.LeftEyeCamera != null && rig.RightEyeCamera != null)
            {
                int w = 1920;
                int h = 960;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                var prevLeft = rig.LeftEyeCamera.targetTexture;
                var prevRight = rig.RightEyeCamera.targetTexture;

                rig.LeftEyeCamera.targetTexture = rt;
                rig.LeftEyeCamera.Render();
                rig.RightEyeCamera.targetTexture = rt;
                rig.RightEyeCamera.Render();

                RenderTexture.active = rt;
                var outTex = new Texture2D(w, h, TextureFormat.RGB24, false);
                outTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                outTex.Apply();

                rig.LeftEyeCamera.targetTexture = prevLeft;
                rig.RightEyeCamera.targetTexture = prevRight;
                RenderTexture.active = null;

                File.WriteAllBytes(path, outTex.EncodeToPNG());
                Destroy(outTex);
                Destroy(rt);
                NotificationCenter.Instance?.Push("Captura VR Estéreo", Path.GetFileName(path), NotificationSeverity.Info, 3.2f);
            }
            else
            {
                CaptureNormalScreenshot();
            }
        }

        public void ToggleExperimentalVRRecording()
        {
            if (_isExperimentalRecording)
            {
                _isExperimentalRecording = false;
                NotificationCenter.Instance?.Push(
                    "Grabación VR Finalizada",
                    $"Secuencia guardada ({_recordedFramesCount} fotogramas) en almacenamiento interno.",
                    NotificationSeverity.Info,
                    4f);
                return;
            }

            _activeRecordingSessionDir = Path.Combine(EnsureMediaDirectory(), $"VRRec_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(_activeRecordingSessionDir);
            _recordedFramesCount = 0;
            _recordFrameTimer = 0f;
            _isExperimentalRecording = true;
            NotificationCenter.Instance?.Push(
                "Grabación VR Experimental",
                "Grabando secuencia estéreo local (límite térmico automático activo).",
                NotificationSeverity.Warning,
                4f);
        }

        private void Update()
        {
            if (!_isExperimentalRecording) return;

            _recordFrameTimer += Time.unscaledDeltaTime;
            if (_recordFrameTimer >= 0.25f) // 4 fps lightweight keyframe sequence to protect mobile flash & thermals
            {
                _recordFrameTimer = 0f;
                _recordedFramesCount++;
                string frameFile = Path.Combine(_activeRecordingSessionDir, $"frame_{_recordedFramesCount:D4}.png");
                ScreenCapture.CaptureScreenshot(frameFile, 1);

                if (_recordedFramesCount >= 120) // Auto-cap at 30s per clip
                {
                    ToggleExperimentalVRRecording();
                }
            }
        }

        private static string EnsureMediaDirectory()
        {
            string dir = Path.Combine(Application.persistentDataPath, "Media");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static Texture2D GenerateProceduralAuroraPanorama()
        {
            var tex = new Texture2D(256, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
            {
                float v = y / 127f;
                for (int x = 0; x < 256; x++)
                {
                    float u = x / 255f;
                    float aurora = Mathf.Sin(u * Mathf.PI * 6f) * 0.12f + 0.58f;
                    float band = Mathf.Exp(-Mathf.Pow((v - aurora) * 9f, 2f));
                    Color sky = Color.Lerp(new Color(0.02f, 0.04f, 0.12f), new Color(0.04f, 0.10f, 0.24f), v);
                    Color glow = Color.Lerp(new Color(0.0f, 0.92f, 0.68f), new Color(0.55f, 0.28f, 1.0f), u) * band;
                    tex.SetPixel(x, y, sky + glow);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
