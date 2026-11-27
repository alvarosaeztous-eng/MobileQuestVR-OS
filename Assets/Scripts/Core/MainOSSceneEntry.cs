using UnityEngine;

namespace MobileQuestVR.Core
{
    /// <summary>
    /// Scene entry for 02_MainOS.unity — launches the full 3D Spatial VR OS environment,
    /// active 3D room, floating windows, status bar, quick dock, and sensors.
    /// </summary>
    public sealed class MainOSSceneEntry : MonoBehaviour
    {
        private void Start()
        {
            var kernel = OSKernel.EnsureInitialized();
            kernel.InitializeSceneRuntimeStack(spawnMainShell: true);
        }
    }
}
