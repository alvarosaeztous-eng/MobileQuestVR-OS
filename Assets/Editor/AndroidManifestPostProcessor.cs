#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace MobileQuestVR.Editor
{
    /// <summary>
    /// Post-processes the generated Unity 6 Gradle AndroidManifest.xml at build time so we remain 100%
    /// compatible with Unity 6 Activity entry points while enforcing:
    /// - Minimal required permissions (INTERNET, ACCESS_NETWORK_STATE, ACCESS_WIFI_STATE, CAMERA, RECORD_AUDIO)
    /// - Non-blocking optional hardware features (camera, microphone, gyroscope, accelerometer required="false")
    /// - Strict local-only privacy: android:allowBackup="false" and android:fullBackupContent="false"
    /// </summary>
    public sealed class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidXmlNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            string manifestPath = Path.Combine(basePath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[AndroidManifestPostProcessor] Manifest not found at {manifestPath}");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);

            XmlElement manifestRoot = doc.DocumentElement;
            if (manifestRoot == null) return;

            EnsureUsesPermission(doc, manifestRoot, "android.permission.INTERNET");
            EnsureUsesPermission(doc, manifestRoot, "android.permission.ACCESS_NETWORK_STATE");
            EnsureUsesPermission(doc, manifestRoot, "android.permission.ACCESS_WIFI_STATE");
            EnsureUsesPermission(doc, manifestRoot, "android.permission.CAMERA");
            EnsureUsesPermission(doc, manifestRoot, "android.permission.RECORD_AUDIO");

            // Mark hardware features as optional so Package Installer never rejects devices lacking a specific sensor
            EnsureUsesFeature(doc, manifestRoot, "android.hardware.camera", required: false);
            EnsureUsesFeature(doc, manifestRoot, "android.hardware.camera.autofocus", required: false);
            EnsureUsesFeature(doc, manifestRoot, "android.hardware.microphone", required: false);
            EnsureUsesFeature(doc, manifestRoot, "android.hardware.sensor.gyroscope", required: false);
            EnsureUsesFeature(doc, manifestRoot, "android.hardware.sensor.accelerometer", required: false);

            // Enforce local-only privacy: disable automatic cloud backup (Sections 10 & 14)
            XmlNode appNode = manifestRoot.SelectSingleNode("application");
            if (appNode is XmlElement appElem)
            {
                appElem.SetAttribute("allowBackup", AndroidXmlNs, "false");
                appElem.SetAttribute("fullBackupContent", AndroidXmlNs, "false");
            }

            doc.Save(manifestPath);
            Debug.Log("[AndroidManifestPostProcessor] Patched AndroidManifest.xml with minimal permissions and allowBackup=false.");
        }

        private static void EnsureUsesPermission(XmlDocument doc, XmlElement root, string permissionName)
        {
            XmlNodeList list = root.SelectNodes("uses-permission");
            if (list != null)
            {
                foreach (XmlNode node in list)
                {
                    if (node is XmlElement el && el.GetAttribute("name", AndroidXmlNs) == permissionName)
                    {
                        return;
                    }
                }
            }

            XmlElement perm = doc.CreateElement("uses-permission");
            perm.SetAttribute("name", AndroidXmlNs, permissionName);
            root.AppendChild(perm);
        }

        private static void EnsureUsesFeature(XmlDocument doc, XmlElement root, string featureName, bool required)
        {
            XmlNodeList list = root.SelectNodes("uses-feature");
            if (list != null)
            {
                foreach (XmlNode node in list)
                {
                    if (node is XmlElement el && el.GetAttribute("name", AndroidXmlNs) == featureName)
                    {
                        el.SetAttribute("required", AndroidXmlNs, required ? "true" : "false");
                        return;
                    }
                }
            }

            XmlElement feat = doc.CreateElement("uses-feature");
            feat.SetAttribute("name", AndroidXmlNs, featureName);
            feat.SetAttribute("required", AndroidXmlNs, required ? "true" : "false");
            root.AppendChild(feat);
        }
    }
}
#endif
