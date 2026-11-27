using System;
using UnityEngine;

namespace MobileQuestVR.AndroidBridge
{
    /// <summary>
    /// Experimental Mixed Reality rear-camera passthrough controller (Section 6).
    /// Requests CAMERA permission strictly on demand, binds the rear camera to a fullscreen
    /// background surface behind spatial 3D windows, and updates the active camera indicator.
    /// </summary>
    public sealed class MixedRealityPassthroughController : MonoBehaviour
    {
        private WebCamTexture _rearCamTexture;
        private GameObject _passthroughQuad;
        private MeshRenderer _quadRenderer;
        private Material _quadMaterial;
        private bool _isPassthroughEnabled;

        public bool IsPassthroughEnabled => _isPassthroughEnabled;
        public string ActiveCameraName { get; private set; } = "None";
        public int CurrentCameraWidth => _rearCamTexture != null ? _rearCamTexture.width : 0;
        public int CurrentCameraHeight => _rearCamTexture != null ? _rearCamTexture.height : 0;

        public event Action<bool> OnPassthroughStateChanged;
        public event Action<string> OnPassthroughError;

        public void AttachToHeadTransform(Transform headTransform)
        {
            if (_passthroughQuad != null) return;

            _passthroughQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _passthroughQuad.name = "MR_RearCameraPassthroughQuad";
            Destroy(_passthroughQuad.GetComponent<Collider>());

            _passthroughQuad.transform.SetParent(headTransform, false);
            // Placed beyond 3D windows (windows sit at 1.5m-3.5m; passthrough plane sits at 18m filling FOV)
            _passthroughQuad.transform.localPosition = new Vector3(0f, 0f, 18f);
            _passthroughQuad.transform.localRotation = Quaternion.identity;
            _passthroughQuad.transform.localScale = new Vector3(44f, 25f, 1f);

            Shader unlitTex = Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
            _quadMaterial = new Material(unlitTex);
            _quadRenderer = _passthroughQuad.GetComponent<MeshRenderer>();
            _quadRenderer.sharedMaterial = _quadMaterial;
            _quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _quadRenderer.receiveShadows = false;
            _passthroughQuad.SetActive(false);
        }

        public void TogglePassthrough(GameObject virtualEnvironmentRoot)
        {
            if (_isPassthroughEnabled)
            {
                DisablePassthrough(virtualEnvironmentRoot);
            }
            else
            {
                EnablePassthrough(virtualEnvironmentRoot);
            }
        }

        public void EnablePassthrough(GameObject virtualEnvironmentRoot)
        {
            var bridge = AndroidPlatformBridge.Instance;
            if (bridge == null) return;

            bridge.RequestCameraPermissionOnDemand(granted =>
            {
                if (!granted)
                {
                    OnPassthroughError?.Invoke("mr.permission_denied");
                    return;
                }
                StartRearCamera(virtualEnvironmentRoot);
            });
        }

        private void StartRearCamera(GameObject virtualEnvironmentRoot)
        {
            try
            {
                string selectedDevice = null;
                var devices = WebCamTexture.devices;
                if (devices != null && devices.Length > 0)
                {
                    for (int i = 0; i < devices.Length; i++)
                    {
                        if (!devices[i].isFrontFacing)
                        {
                            selectedDevice = devices[i].name;
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(selectedDevice))
                    {
                        selectedDevice = devices[0].name;
                    }
                }

                if (!string.IsNullOrEmpty(selectedDevice))
                {
                    ActiveCameraName = selectedDevice;
                    _rearCamTexture = new WebCamTexture(selectedDevice, 1280, 720, 30);
                    _rearCamTexture.Play();
                    if (_quadMaterial != null)
                    {
                        _quadMaterial.mainTexture = _rearCamTexture;
                    }
                }
                else
                {
                    ActiveCameraName = "Simulated Passthrough (No Rear Camera Found)";
                    if (_quadMaterial != null)
                    {
                        _quadMaterial.color = new Color(0.14f, 0.17f, 0.20f, 1f);
                    }
                }

                if (_passthroughQuad != null) _passthroughQuad.SetActive(true);
                if (virtualEnvironmentRoot != null) virtualEnvironmentRoot.SetActive(false);

                _isPassthroughEnabled = true;
                if (AndroidPlatformBridge.Instance != null)
                {
                    AndroidPlatformBridge.Instance.IsCameraActive = true;
                }
                OnPassthroughStateChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                OnPassthroughError?.Invoke($"Camera error: {ex.Message}");
            }
        }

        public void DisablePassthrough(GameObject virtualEnvironmentRoot)
        {
            if (_rearCamTexture != null)
            {
                if (_rearCamTexture.isPlaying) _rearCamTexture.Stop();
                Destroy(_rearCamTexture);
                _rearCamTexture = null;
            }

            if (_passthroughQuad != null) _passthroughQuad.SetActive(false);
            if (virtualEnvironmentRoot != null) virtualEnvironmentRoot.SetActive(true);

            _isPassthroughEnabled = false;
            if (AndroidPlatformBridge.Instance != null)
            {
                AndroidPlatformBridge.Instance.IsCameraActive = false;
            }
            OnPassthroughStateChanged?.Invoke(false);
        }

        private void OnDestroy()
        {
            if (_rearCamTexture != null && _rearCamTexture.isPlaying)
            {
                _rearCamTexture.Stop();
            }
        }
    }
}
