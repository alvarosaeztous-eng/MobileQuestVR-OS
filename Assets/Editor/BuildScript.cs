using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MobileQuestVR.Editor
{
    /// <summary>
    /// Automated Unity 6 Android APK builder for GameCI / GitHub Actions and local Unity Editor CLI.
    /// Target priority device: Xiaomi Mi 11 Lite 5G (Snapdragon 780G / Adreno 642, ARM64, 90Hz Landscape).
    /// </summary>
    public static class BuildScript
    {
        private static readonly string[] BuildScenes =
        {
            "Assets/Scenes/00_Boot.unity",
            "Assets/Scenes/01_RoomSelector.unity",
            "Assets/Scenes/02_MainOS.unity"
        };

        [MenuItem("MobileQuestVR/Configure Android Settings (Mi 11 Lite 5G)")]
        public static void ConfigureAndroidPlayerSettings()
        {
            PlayerSettings.companyName = "MobileQuestVR";
            PlayerSettings.productName = "MobileQuestVR";
            PlayerSettings.bundleVersion = "1.0.0";

            NamedBuildTarget androidTarget = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(androidTarget, "com.mobilequestvr.os");
            PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(androidTarget, ManagedStrippingLevel.Low);

            // Landscape immersive orientation (Section 1 & 19)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;

            // Xiaomi Mi 11 Lite 5G compatibility: ARM64, Android 8.0+ (API 26+)
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = GetVersionCodeFromEnv();

            // Assign futuristic VR visor icon if imported
            var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/AppIcon_512.png");
            if (iconTex != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { iconTex }, IconKind.Any);
            }

            // Configure secure signing strictly from environment variables (never hardcoded!)
            ConfigureSecureSigningFromEnvironment();

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureSecureSigningFromEnvironment()
        {
            string keystorePath = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
            string keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            string keyaliasName = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
            string keyaliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");

            if (!string.IsNullOrEmpty(keystorePath) &&
                File.Exists(keystorePath) &&
                !string.IsNullOrEmpty(keystorePass) &&
                !string.IsNullOrEmpty(keyaliasName) &&
                !string.IsNullOrEmpty(keyaliasPass))
            {
                Debug.Log("[MobileQuestVR Build] Using custom release keystore from secure environment variables.");
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = keyaliasName;
                PlayerSettings.Android.keyaliasPass = keyaliasPass;
            }
            else
            {
                Debug.Log("[MobileQuestVR Build] Release keystore env vars not set; using standard installable Android debug signing.");
                PlayerSettings.Android.useCustomKeystore = false;
            }
        }

        private static int GetVersionCodeFromEnv()
        {
            string raw = Environment.GetEnvironmentVariable("ANDROID_VERSION_CODE");
            return int.TryParse(raw, out int code) && code > 0 ? code : 1;
        }

        [MenuItem("MobileQuestVR/Build Installable Android APK")]
        public static void BuildAndroidApk()
        {
            ConfigureAndroidPlayerSettings();

            string outputPath = GetCommandLineArg("-customBuildPath");
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine("Builds", "Android", "MobileQuestVR.apk");
            }
            else if (Directory.Exists(outputPath) || !outputPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            {
                outputPath = Path.Combine(outputPath, "MobileQuestVR.apk");
            }

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            for (int i = 0; i < BuildScenes.Length; i++)
            {
                if (!File.Exists(BuildScenes[i]))
                {
                    throw new FileNotFoundException($"[MobileQuestVR Build] Required scene missing: {BuildScenes[i]}");
                }
            }

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = BuildScenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[MobileQuestVR Build] Starting Unity 6 Android APK build -> {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"[MobileQuestVR Build] APK compilation failed with status: {summary.result} (Errors: {summary.totalErrors})");
            }

            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException($"[MobileQuestVR Build] Build reported Succeeded, but APK not found at {outputPath}");
            }

            long sizeBytes = new FileInfo(outputPath).Length;
            Debug.Log($"[MobileQuestVR Build] SUCCESS! APK generated at {outputPath} ({sizeBytes / (1024 * 1024)} MB).");
        }

        private static string GetCommandLineArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
