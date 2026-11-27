using System.Collections;
using MobileQuestVR.Audio;
using MobileQuestVR.Core;
using MobileQuestVR.Localization;
using MobileQuestVR.Settings;
using MobileQuestVR.VRRendering;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobileQuestVR.Boot
{
    /// <summary>
    /// Native Unity 6 3D Startup & Animated Loading Sequence (Sections 1 & 9):
    /// - Deep 3D cosmic starfield skybox + drifting star particles
    /// - Animated futuristic VR headset visor logo with orbital holographic rings
    /// - Smooth loading bar & subsystem warmup transitioning into 01_RoomSelector
    /// </summary>
    public sealed class BootSceneEntry : MonoBehaviour
    {
        private Transform _visorLogoRoot;
        private Transform _outerOrbitRing;
        private Transform _innerOrbitRing;
        private Image _progressBarFill;
        private Text _bootStatusLabel;
        private CanvasGroup _canvasGroup;

        private void Start()
        {
            var kernel = OSKernel.EnsureInitialized();
            kernel.InitializeSceneRuntimeStack(spawnMainShell: false);

            BuildCosmicSpaceBackground();
            BuildAnimated3DVisorEmblem();
            BuildLoadingOverlay3D();

            SpatialAudioManager.Instance?.PlayBootChime();
            StartCoroutine(RunBootSequenceCoroutine());
        }

        private void BuildCosmicSpaceBackground()
        {
            var skyDome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skyDome.name = "BootCosmicStarfieldDome";
            Destroy(skyDome.GetComponent<Collider>());
            skyDome.transform.position = new Vector3(0f, 1.62f, 0f);
            skyDome.transform.localScale = Vector3.one * 150f;

            Shader starShader = Resources.Load<Shader>("Shaders/MobileQuestStarfield") ?? Shader.Find("MobileQuestVR/StarfieldSky") ?? Shader.Find("Unlit/Color");
            var skyMat = new Material(starShader);
            skyMat.SetColor("_TopColor", new Color(0.01f, 0.02f, 0.08f, 1f));
            skyMat.SetColor("_HorizonColor", new Color(0.04f, 0.11f, 0.26f, 1f));
            skyMat.SetColor("_BottomColor", new Color(0.005f, 0.01f, 0.04f, 1f));
            skyMat.SetFloat("_StarDensity", 135f);
            skyMat.SetFloat("_StarIntensity", 1.45f);

            var mr = skyDome.GetComponent<MeshRenderer>();
            mr.sharedMaterial = skyMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Subtle 3D star motes floating around the user
            var starsHolder = new GameObject("Floating3DStars");
            Shader unlit = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            var moteMat = new Material(unlit) { color = new Color(0.65f, 0.90f, 1f, 0.85f) };
            Random.InitState(42);
            for (int i = 0; i < 48; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(starsHolder.transform, false);
                Vector3 dir = Random.onUnitSphere;
                if (dir.z < -0.2f) dir.z = -dir.z;
                s.transform.position = new Vector3(0f, 1.62f, 0f) + dir * Random.Range(3.2f, 16f);
                s.transform.localScale = Vector3.one * Random.Range(0.018f, 0.048f);
                s.GetComponent<MeshRenderer>().sharedMaterial = moteMat;
            }
        }

        private void BuildAnimated3DVisorEmblem()
        {
            _visorLogoRoot = new GameObject("AnimatedVisorEmblem3D").transform;
            _visorLogoRoot.position = new Vector3(0f, 1.76f, 2.35f);

            Shader holoShader = Resources.Load<Shader>("Shaders/MobileQuestHologram") ?? Shader.Find("Sprites/Default");
            var cyanHolo = new Material(holoShader) { color = new Color(0f, 0.82f, 1f, 0.55f) };
            var violetHolo = new Material(holoShader) { color = new Color(0.52f, 0.35f, 1f, 0.45f) };

            var ringA = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ringA.GetComponent<Collider>());
            _outerOrbitRing = ringA.transform;
            _outerOrbitRing.SetParent(_visorLogoRoot, false);
            _outerOrbitRing.localScale = new Vector3(0.92f, 0.012f, 0.92f);
            _outerOrbitRing.localRotation = Quaternion.Euler(72f, 15f, 0f);
            ringA.GetComponent<MeshRenderer>().sharedMaterial = cyanHolo;

            var ringB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ringB.GetComponent<Collider>());
            _innerOrbitRing = ringB.transform;
            _innerOrbitRing.SetParent(_visorLogoRoot, false);
            _innerOrbitRing.localScale = new Vector3(0.74f, 0.012f, 0.74f);
            _innerOrbitRing.localRotation = Quaternion.Euler(108f, -20f, 0f);
            ringB.GetComponent<MeshRenderer>().sharedMaterial = violetHolo;

            // Load our generated futuristic visor emblem texture from Resources/Branding/BootVisorLogo
            Texture2D visorTex = Resources.Load<Texture2D>("Branding/BootVisorLogo");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "VisorLogoQuad";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(_visorLogoRoot, false);
            quad.transform.localScale = new Vector3(0.68f, 0.68f, 1f);

            var logoMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent"));
            if (visorTex != null) logoMat.mainTexture = visorTex;
            quad.GetComponent<MeshRenderer>().sharedMaterial = logoMat;
        }

        private void BuildLoadingOverlay3D()
        {
            var canvasObj = new GameObject("BootLoadingCanvas3D");
            canvasObj.transform.position = new Vector3(0f, 1.18f, 2.30f);
            canvasObj.transform.localScale = Vector3.one * 0.0014f;

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            _canvasGroup = canvasObj.AddComponent<CanvasGroup>();
            canvasObj.GetComponent<RectTransform>().sizeDelta = new Vector2(640f, 200f);

            Font font = LocalizationService.GetDefaultFont();

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(canvasObj.transform, false);
            var titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = font;
            titleTxt.text = "MOBILEQUESTVR OS";
            titleTxt.fontSize = 32;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.white;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 52f);
            trt.sizeDelta = new Vector2(600f, 46f);

            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(canvasObj.transform, false);
            _bootStatusLabel = statusGo.AddComponent<Text>();
            _bootStatusLabel.font = font;
            _bootStatusLabel.text = LocalizationService.Get("boot.initializing");
            _bootStatusLabel.fontSize = 18;
            _bootStatusLabel.alignment = TextAnchor.MiddleCenter;
            _bootStatusLabel.color = new Color(0.68f, 0.90f, 1f, 1f);
            var srt = statusGo.GetComponent<RectTransform>();
            srt.anchoredPosition = new Vector2(0f, 6f);
            srt.sizeDelta = new Vector2(600f, 32f);

            var trackGo = new GameObject("ProgressTrack");
            trackGo.transform.SetParent(canvasObj.transform, false);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = new Color(0.10f, 0.18f, 0.32f, 0.9f);
            var trackRt = trackGo.GetComponent<RectTransform>();
            trackRt.anchoredPosition = new Vector2(0f, -42f);
            trackRt.sizeDelta = new Vector2(460f, 14f);

            var fillGo = new GameObject("ProgressFill");
            fillGo.transform.SetParent(trackGo.transform, false);
            _progressBarFill = fillGo.AddComponent<Image>();
            _progressBarFill.color = new Color(0f, 0.86f, 1f, 1f);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 0f);
        }

        private void Update()
        {
            bool reduceAnim = SettingsManager.Instance != null && SettingsManager.Instance.Data.ReduceAnimations;
            if (reduceAnim) return;

            float t = Time.unscaledTime;
            if (_visorLogoRoot != null)
            {
                _visorLogoRoot.position = new Vector3(0f, 1.76f + Mathf.Sin(t * 1.8f) * 0.035f, 2.35f);
            }
            if (_outerOrbitRing != null)
            {
                _outerOrbitRing.Rotate(0f, Time.unscaledDeltaTime * 36f, 0f, Space.Self);
            }
            if (_innerOrbitRing != null)
            {
                _innerOrbitRing.Rotate(0f, -Time.unscaledDeltaTime * 52f, 0f, Space.Self);
            }
        }

        private IEnumerator RunBootSequenceCoroutine()
        {
            string[] phases = { "boot.initializing", "boot.sensors", "boot.rooms", "boot.ready" };
            const float totalDuration = 2.8f;
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float norm = Mathf.Clamp01(elapsed / totalDuration);
                int phaseIdx = Mathf.Min(phases.Length - 1, Mathf.FloorToInt(norm * phases.Length));
                if (_bootStatusLabel != null)
                {
                    _bootStatusLabel.text = LocalizationService.Get(phases[phaseIdx]);
                }
                if (_progressBarFill != null)
                {
                    var rt = _progressBarFill.rectTransform;
                    rt.sizeDelta = new Vector2(460f * Mathf.SmoothStep(0f, 1f, norm), 0f);
                }
                yield return null;
            }

            // Smooth fade transition into Panoramic Room Selector / First-Boot Wizard
            float fade = 0f;
            while (fade < 0.35f)
            {
                fade += Time.unscaledDeltaTime;
                if (_canvasGroup != null) _canvasGroup.alpha = 1f - Mathf.Clamp01(fade / 0.35f);
                yield return null;
            }

            SceneManager.LoadScene("01_RoomSelector");
        }
    }
}
