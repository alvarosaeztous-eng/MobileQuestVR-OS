using System.Collections.Generic;
using System.IO;
using MobileQuestVR.Localization;
using MobileQuestVR.Media;
using MobileQuestVR.RoomCustomization;
using MobileQuestVR.Settings;
using MobileQuestVR.Windows;
using UnityEngine;

namespace MobileQuestVR.Gallery
{
    public sealed class GalleryMediaItem
    {
        public string FileName;
        public string FullPath;
        public bool Is360Candidate;
        public bool IsVideo;
    }

    /// <summary>
    /// Local storage gallery for normal photos, 360° panoramas, videos, VR captures,
    /// and assigning user photos to 3D room wall frames (Sections 10, 12, 13).
    /// </summary>
    public sealed class GalleryManager : MonoBehaviour
    {
        public static GalleryManager Instance { get; private set; }

        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".mp4", ".webm" };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SeedStarterSamplesIfEmpty();
        }

        private void SeedStarterSamplesIfEmpty()
        {
            try
            {
                string mediaDir = Path.Combine(Application.persistentDataPath, "Media");
                if (!Directory.Exists(mediaDir)) Directory.CreateDirectory(mediaDir);

                string samplePano = Path.Combine(mediaDir, "Sample_Aurora_360_Pano.png");
                if (!File.Exists(samplePano))
                {
                    Texture2D aurora = MediaCenterController.GenerateProceduralAuroraPanorama();
                    File.WriteAllBytes(samplePano, aurora.EncodeToPNG());
                    Destroy(aurora);
                }
            }
            catch
            {
                // Ignore if storage not writable during tests
            }
        }

        public List<GalleryMediaItem> ScanLocalMediaFiles()
        {
            var results = new List<GalleryMediaItem>();
            var folders = new List<string> { Path.Combine(Application.persistentDataPath, "Media") };

            if (SettingsManager.Instance != null && SettingsManager.Instance.Data.CompatibleMediaFolders != null)
            {
                for (int i = 0; i < SettingsManager.Instance.Data.CompatibleMediaFolders.Count; i++)
                {
                    string f = SettingsManager.Instance.Data.CompatibleMediaFolders[i];
                    if (!string.IsNullOrEmpty(f) && !folders.Contains(f)) folders.Add(f);
                }
            }

            for (int f = 0; f < folders.Count; f++)
            {
                string dir = folders[f];
                if (!Directory.Exists(dir)) continue;

                try
                {
                    string[] files = Directory.GetFiles(dir);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string ext = Path.GetExtension(files[i]).ToLowerInvariant();
                        bool ok = false;
                        for (int e = 0; e < SupportedExtensions.Length; e++)
                        {
                            if (SupportedExtensions[e] == ext) { ok = true; break; }
                        }
                        if (!ok) continue;

                        string name = Path.GetFileName(files[i]);
                        results.Add(new GalleryMediaItem
                        {
                            FileName = name,
                            FullPath = files[i],
                            Is360Candidate = name.IndexOf("360", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             name.IndexOf("pano", System.StringComparison.OrdinalIgnoreCase) >= 0,
                            IsVideo = ext == ".mp4" || ext == ".webm"
                        });
                    }
                }
                catch
                {
                    // Skip unreadable folder
                }
            }

            return results;
        }

        public void OpenGalleryWindow()
        {
            SpatialWindowManager.Instance?.OpenOrFocusWindow(
                "LocalGallery",
                LocalizationService.Get("menu.gallery"),
                new Vector2(700f, 460f),
                PopulateGalleryWindow);
        }

        private void PopulateGalleryWindow(SpatialWindow win)
        {
            List<GalleryMediaItem> items = ScanLocalMediaFiles();
            SpatialUIBuilder.CreateLabel(
                win.ContentRoot,
                $"Almacenamiento Interno ({items.Count} elementos locales — sin nube)",
                new Vector2(0f, 155f),
                new Vector2(640f, 32f),
                18,
                TextAnchor.MiddleCenter,
                new Color(0.7f, 0.92f, 1f),
                true);

            int maxRows = Mathf.Min(items.Count, 5);
            for (int i = 0; i < maxRows; i++)
            {
                GalleryMediaItem item = items[i];
                float y = 95f - i * 52f;
                string badge = item.Is360Candidate ? "[360°] " : (item.IsVideo ? "[VIDEO] " : "[FOTO] ");

                SpatialUIBuilder.CreateLabel(
                    win.ContentRoot,
                    badge + item.FileName,
                    new Vector2(-130f, y),
                    new Vector2(360f, 38f),
                    16,
                    TextAnchor.MiddleLeft);

                SpatialUIBuilder.CreateSpatialButton(
                    win.ContentRoot,
                    "Abrir",
                    new Vector2(125f, y),
                    new Vector2(105f, 38f),
                    new Color(0.10f, 0.36f, 0.56f, 0.95f),
                    () => MediaCenterController.Instance?.OpenLocalFileInMediaViewer(item.FullPath),
                    15);

                if (!item.IsVideo)
                {
                    SpatialUIBuilder.CreateSpatialButton(
                        win.ContentRoot,
                        "En Marco",
                        new Vector2(245f, y),
                        new Vector2(115f, 38f),
                        new Color(0.18f, 0.42f, 0.34f, 0.95f),
                        () =>
                        {
                            RoomId current = SettingsManager.Instance != null ? SettingsManager.Instance.Data.ActiveRoom : RoomId.FuturisticBedroom;
                            RoomCustomizationManager.Instance?.AssignCustomImageToWallFrame(current, item.FullPath);
                        },
                        15);
                }
            }
        }
    }
}
