using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MobileQuestVR.Environment;
using MobileQuestVR.Localization;
using MobileQuestVR.Notifications;
using MobileQuestVR.Settings;
using UnityEngine;

namespace MobileQuestVR.RoomCustomization
{
    [Serializable]
    public sealed class RoomCustomState
    {
        public RoomId Room;
        public int FurnitureVariantIndex = 0;
        public bool ShowDecorations = true;
        public int LightingColorPreset = 0;
        public RoomTimeOfDay TimeOfDay = RoomTimeOfDay.Night;
        public string CustomPhotoFilePath = string.Empty;
    }

    [Serializable]
    public sealed class RoomCustomizationCollection
    {
        public List<RoomCustomState> Rooms = new List<RoomCustomState>();
    }

    /// <summary>
    /// Handles furniture swapping, decor toggles, Day/Sunset/Night ambience, accent lighting colors,
    /// custom user wall frame images, and local JSON persistence (Section 3).
    /// </summary>
    public sealed class RoomCustomizationManager : MonoBehaviour
    {
        public static RoomCustomizationManager Instance { get; private set; }

        private RoomCustomizationCollection _collection = new RoomCustomizationCollection();
        private string SavePath => Path.Combine(Application.persistentDataPath, "room_customizations.json");

        private static readonly Color[] AccentPalette =
        {
            new Color(0.0f, 0.85f, 1.0f),  // Quest Cyan
            new Color(0.68f, 0.35f, 1.0f), // Nebula Violet
            new Color(1.0f, 0.65f, 0.20f), // Warm Sunset Gold
            new Color(0.18f, 0.95f, 0.58f),// Bio Emerald
            new Color(0.95f, 0.96f, 1.0f)  // Studio White
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadLocalState();
        }

        private void Start()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnRoomLoaded += ApplySavedCustomizationToRoom;
            }
        }

        public RoomCustomState GetOrCreateState(RoomId room)
        {
            for (int i = 0; i < _collection.Rooms.Count; i++)
            {
                if (_collection.Rooms[i].Room == room) return _collection.Rooms[i];
            }

            var created = new RoomCustomState
            {
                Room = room,
                TimeOfDay = (room == RoomId.TropicalBeach || room == RoomId.Mountain || room == RoomId.ModernBedroom)
                    ? RoomTimeOfDay.Day
                    : RoomTimeOfDay.Night
            };
            _collection.Rooms.Add(created);
            return created;
        }

        public void CycleFurniture(RoomId room)
        {
            var state = GetOrCreateState(room);
            var handles = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : null;
            int count = (handles != null && handles.FurnitureVariants.Count > 0) ? handles.FurnitureVariants.Count : 2;
            state.FurnitureVariantIndex = (state.FurnitureVariantIndex + 1) % count;
            ApplySavedCustomizationToRoom(room);
        }

        public void ToggleDecorations(RoomId room)
        {
            var state = GetOrCreateState(room);
            state.ShowDecorations = !state.ShowDecorations;
            ApplySavedCustomizationToRoom(room);
        }

        public void CycleLightingColor(RoomId room)
        {
            var state = GetOrCreateState(room);
            state.LightingColorPreset = (state.LightingColorPreset + 1) % AccentPalette.Length;
            ApplySavedCustomizationToRoom(room);
        }

        public void SetTimeOfDay(RoomId room, RoomTimeOfDay tod)
        {
            var state = GetOrCreateState(room);
            state.TimeOfDay = tod;
            ApplySavedCustomizationToRoom(room);
        }

        public void AssignCustomImageToWallFrame(RoomId room, string localImagePath)
        {
            var state = GetOrCreateState(room);
            state.CustomPhotoFilePath = localImagePath ?? string.Empty;
            ApplySavedCustomizationToRoom(room);
            SaveLocalState();
        }

        public void ApplySavedCustomizationToRoom(RoomId room)
        {
            var rm = RoomManager.Instance;
            if (rm == null || rm.CurrentRoom == null || rm.CurrentRoom.Id != room) return;

            BuiltRoomHandles h = rm.CurrentRoom;
            RoomCustomState state = GetOrCreateState(room);

            for (int i = 0; i < h.FurnitureVariants.Count; i++)
            {
                if (h.FurnitureVariants[i] != null)
                {
                    h.FurnitureVariants[i].SetActive(i == state.FurnitureVariantIndex);
                }
            }

            if (h.DecorGroup != null)
            {
                h.DecorGroup.SetActive(state.ShowDecorations);
            }

            rm.ApplyTimeOfDay(h, state.TimeOfDay);

            Color accent = AccentPalette[Mathf.Clamp(state.LightingColorPreset, 0, AccentPalette.Length - 1)];
            if (h.AccentPointLight != null)
            {
                h.AccentPointLight.color = accent;
            }
            for (int i = 0; i < h.AccentEmissiveMaterials.Count; i++)
            {
                Material m = h.AccentEmissiveMaterials[i];
                if (m != null && m.HasProperty("_EmissionColor"))
                {
                    m.SetColor("_EmissionColor", accent * 0.85f);
                }
            }

            if (!string.IsNullOrEmpty(state.CustomPhotoFilePath) && File.Exists(state.CustomPhotoFilePath) && h.CustomPhotoFrameRenderer != null)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(state.CustomPhotoFilePath);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        h.CustomPhotoFrameRenderer.material.mainTexture = tex;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RoomCustomization] Failed loading custom frame image: {e.Message}");
                }
            }
        }

        public void SaveLocalState()
        {
            try
            {
                string json = JsonUtility.ToJson(_collection, true);
                File.WriteAllText(SavePath, json, Encoding.UTF8);
                NotificationCenter.Instance?.Push(
                    LocalizationService.Get("custom.title"),
                    LocalizationService.Get("custom.saved"),
                    NotificationSeverity.Info,
                    3f);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RoomCustomization] Save warning: {e.Message}");
            }
        }

        public void LoadLocalState()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath, Encoding.UTF8);
                    var parsed = JsonUtility.FromJson<RoomCustomizationCollection>(json);
                    if (parsed != null && parsed.Rooms != null)
                    {
                        _collection = parsed;
                    }
                }
            }
            catch
            {
                _collection = new RoomCustomizationCollection();
            }
        }
    }
}
