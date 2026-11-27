using MobileQuestVR.Settings;
using UnityEngine;

namespace MobileQuestVR.VRRendering
{
    /// <summary>
    /// Per-eye post-render barrel lens distortion and brightness adjustment (Section 11).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class EyeLensDistortionEffect : MonoBehaviour
    {
        public bool EnableDistortion = true;
        public float K1 = 0.18f;
        public float K2 = 0.06f;
        public float LensCenterOffset = 0f;
        public float Brightness = 1.0f;

        private Material _material;

        private void EnsureMaterial()
        {
            if (_material != null) return;
            Shader s = Resources.Load<Shader>("Shaders/MobileQuestLensDistort") ?? Shader.Find("MobileQuestVR/LensDistortion");
            if (s != null)
            {
                _material = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            EnsureMaterial();
            if (!EnableDistortion || _material == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            _material.SetFloat("_K1", K1);
            _material.SetFloat("_K2", K2);
            _material.SetFloat("_LensCenterOffset", LensCenterOffset);
            _material.SetFloat("_Brightness", Brightness);
            Graphics.Blit(src, dest, _material);
        }

        private void OnDestroy()
        {
            if (_material != null) DestroyImmediate(_material);
        }
    }

    /// <summary>
    /// Dual-eye stereoscopic VR camera rig + instant toggle to normal single-screen 3D mode.
    /// Supports IPD adjustment (56-72mm), configurable barrel distortion, brightness, and viewer presets.
    /// </summary>
    public sealed class StereoVRRig : MonoBehaviour
    {
        public static StereoVRRig Instance { get; private set; }

        public Transform HeadPivot { get; private set; }
        public Camera LeftEyeCamera { get; private set; }
        public Camera RightEyeCamera { get; private set; }
        public Camera MonoCenterCamera { get; private set; }

        private EyeLensDistortionEffect _leftDistort;
        private EyeLensDistortionEffect _rightDistort;
        private EyeLensDistortionEffect _monoDistort;

        public bool IsStereoActive { get; private set; } = true;

        public Camera ActiveCenterOrLeftCamera => IsStereoActive ? LeftEyeCamera : MonoCenterCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildRigHierarchy();
        }

        private void Start()
        {
            SyncWithSettings();
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnSettingsChanged += _ => SyncWithSettings();
            }
        }

        private void BuildRigHierarchy()
        {
            // Disable any pre-existing default scene Main Camera to prevent duplicate AudioListeners
            var existingCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < existingCameras.Length; i++)
            {
                if (existingCameras[i].transform.root != transform)
                {
                    existingCameras[i].gameObject.SetActive(false);
                }
            }

            transform.position = new Vector3(0f, 1.62f, 0f);
            HeadPivot = new GameObject("HeadPivot_3DoF").transform;
            HeadPivot.SetParent(transform, false);
            HeadPivot.localPosition = Vector3.zero;
            HeadPivot.localRotation = Quaternion.identity;

            HeadPivot.gameObject.AddComponent<AudioListener>();

            LeftEyeCamera = CreateEyeCamera("LeftEyeCamera", new Rect(0f, 0f, 0.5f, 1f), -0.0315f, out _leftDistort);
            RightEyeCamera = CreateEyeCamera("RightEyeCamera", new Rect(0.5f, 0f, 0.5f, 1f), 0.0315f, out _rightDistort);
            MonoCenterCamera = CreateEyeCamera("MonoCenterCamera", new Rect(0f, 0f, 1f, 1f), 0f, out _monoDistort);
            MonoCenterCamera.tag = "MainCamera";
            LeftEyeCamera.tag = "MainCamera";
        }

        private Camera CreateEyeCamera(string camName, Rect viewport, float localX, out EyeLensDistortionEffect distort)
        {
            var go = new GameObject(camName);
            go.transform.SetParent(HeadPivot, false);
            go.transform.localPosition = new Vector3(localX, 0f, 0f);
            go.transform.localRotation = Quaternion.identity;

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.08f, 1f);
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 260f;
            cam.fieldOfView = 92f;
            cam.rect = viewport;
            cam.allowHDR = false;
            cam.allowMSAA = true;

            distort = go.AddComponent<EyeLensDistortionEffect>();
            return cam;
        }

        public void SyncWithSettings()
        {
            var data = SettingsManager.Instance != null ? SettingsManager.Instance.Data : new OSPreferencesData();
            IsStereoActive = data.StereoscopicVRMode;

            float halfIpdMeters = Mathf.Clamp(data.InterpupillaryDistanceMm, 54f, 74f) * 0.0005f;
            LeftEyeCamera.transform.localPosition = new Vector3(-halfIpdMeters, 0f, 0f);
            RightEyeCamera.transform.localPosition = new Vector3(halfIpdMeters, 0f, 0f);

            LeftEyeCamera.gameObject.SetActive(IsStereoActive);
            RightEyeCamera.gameObject.SetActive(IsStereoActive);
            MonoCenterCamera.gameObject.SetActive(!IsStereoActive);

            float brightness = Mathf.Clamp(data.Brightness, 0.25f, 1.25f);
            bool reduceFx = data.ReduceEffects;

            _leftDistort.EnableDistortion = IsStereoActive && !reduceFx;
            _leftDistort.K1 = data.LensDistortionK1;
            _leftDistort.K2 = data.LensDistortionK2;
            _leftDistort.LensCenterOffset = 0.02f;
            _leftDistort.Brightness = brightness;

            _rightDistort.EnableDistortion = IsStereoActive && !reduceFx;
            _rightDistort.K1 = data.LensDistortionK1;
            _rightDistort.K2 = data.LensDistortionK2;
            _rightDistort.LensCenterOffset = -0.02f;
            _rightDistort.Brightness = brightness;

            _monoDistort.EnableDistortion = true;
            _monoDistort.K1 = 0f;
            _monoDistort.K2 = 0f;
            _monoDistort.LensCenterOffset = 0f;
            _monoDistort.Brightness = brightness;
        }

        public void ToggleStereoVRMode()
        {
            if (SettingsManager.Instance == null) return;
            SettingsManager.Instance.Data.StereoscopicVRMode = !SettingsManager.Instance.Data.StereoscopicVRMode;
            SettingsManager.Instance.SaveAndNotify();
        }
    }
}
