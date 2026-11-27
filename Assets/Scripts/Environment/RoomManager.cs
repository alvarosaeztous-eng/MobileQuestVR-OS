using System;
using System.Collections.Generic;
using MobileQuestVR.Audio;
using MobileQuestVR.Localization;
using MobileQuestVR.Settings;
using UnityEngine;

namespace MobileQuestVR.Environment
{
    public enum RoomTimeOfDay
    {
        Day = 0,
        Sunset = 1,
        Night = 2
    }

    public sealed class BuiltRoomHandles
    {
        public RoomId Id;
        public GameObject Root;
        public Light MainLight;
        public Light AccentPointLight;
        public Material SkyDomeMaterial;
        public readonly List<GameObject> FurnitureVariants = new List<GameObject>();
        public GameObject DecorGroup;
        public MeshRenderer CustomPhotoFrameRenderer;
        public MeshRenderer AmbientVideoScreenRenderer;
        public readonly List<Material> AccentEmissiveMaterials = new List<Material>();
    }

    /// <summary>
    /// Builds and manages all 7 local 3D rooms inside the APK with zero external binary model dependencies.
    /// Viewpoint is static at (0, 1.62, 0) per Section 3 (look around 360°, no free locomotion).
    /// </summary>
    public sealed class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance { get; private set; }

        public BuiltRoomHandles CurrentRoom { get; private set; }
        public GameObject EnvironmentContainer { get; private set; }
        public event Action<RoomId> OnRoomLoaded;

        private Shader _surfaceShader;
        private Shader _skyShader;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _surfaceShader = Resources.Load<Shader>("Shaders/MobileQuestSurface") ?? Shader.Find("MobileQuestVR/Surface") ?? Shader.Find("Standard");
            _skyShader = Resources.Load<Shader>("Shaders/MobileQuestStarfield") ?? Shader.Find("MobileQuestVR/StarfieldSky") ?? Shader.Find("Unlit/Color");

            EnvironmentContainer = new GameObject("VirtualEnvironmentRoot");
            EnvironmentContainer.transform.SetParent(transform, false);
        }

        public static string GetRoomLocalizationKey(RoomId id)
        {
            return id switch
            {
                RoomId.FuturisticBedroom => "room.futuristic_bedroom",
                RoomId.CinemaRoom => "room.cinema",
                RoomId.GamingRoom => "room.gaming",
                RoomId.SpaceStation => "room.space_station",
                RoomId.ModernBedroom => "room.modern_bedroom",
                RoomId.TropicalBeach => "room.tropical_beach",
                RoomId.Mountain => "room.mountain",
                _ => "room.futuristic_bedroom"
            };
        }

        public BuiltRoomHandles LoadRoom(RoomId roomId)
        {
            if (CurrentRoom != null && CurrentRoom.Root != null)
            {
                Destroy(CurrentRoom.Root);
                CurrentRoom = null;
            }

            var handles = new BuiltRoomHandles
            {
                Id = roomId,
                Root = new GameObject($"Room_{roomId}")
            };
            handles.Root.transform.SetParent(EnvironmentContainer.transform, false);

            BuildSkyDomeAndBaseLighting(handles, roomId);

            switch (roomId)
            {
                case RoomId.FuturisticBedroom:
                    BuildFuturisticBedroom(handles);
                    SpatialAudioManager.Instance?.SetRoomAmbientProfile(AmbientSoundProfile.CosmicDrone);
                    break;
                case RoomId.CinemaRoom:
                    BuildCinemaRoom(handles);
                    SpatialAudioManager.Instance?.SetRoomAmbientProfile(AmbientSoundProfile.CinemaWarmth);
                    break;
                case RoomId.GamingRoom:
                    BuildGamingRoom(handles);
                    SpatialAudioManager.Instance?.SetRoomAmbientProfile(AmbientSoundProfile.CyberPulse);
                    break;
                case RoomId.SpaceStation:
                    BuildSpaceStation(handles);
                    SpatialAudioManager.Instance?.SetRoomAmbientProfile(AmbientSoundProfile.CosmicDrone);
                    break;
                case RoomId.ModernBedroom:
                    BuildModernBedroom(handles);
                    SpatialAudioManager.Instance?.SetRoomAmbientProfile(AmbientSoundProfile.CinemaWarmth);
                    break;
                case RoomId.TropicalBeach:
                    BuildTropicalBeach(handles);
                    SpatialAudioManager.Instance?.SetRoomAmbientProfile(AmbientSoundProfile.OceanWaves);
                    break;
                case RoomId.Mountain:
                    BuildMountainLodge(handles);
                    SpatialAudioManager.Instance?.SetRoomAmbientProfile(AmbientSoundProfile.MountainWind);
                    break;
            }

            CurrentRoom = handles;
            if (SettingsManager.Instance != null && SettingsManager.Instance.Data.ActiveRoom != roomId)
            {
                SettingsManager.Instance.Data.ActiveRoom = roomId;
                SettingsManager.Instance.SaveAndNotify();
            }

            OnRoomLoaded?.Invoke(roomId);
            return handles;
        }

        private void BuildSkyDomeAndBaseLighting(BuiltRoomHandles h, RoomId id)
        {
            var skySphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skySphere.name = "SkyDome_120m";
            Destroy(skySphere.GetComponent<Collider>());
            skySphere.transform.SetParent(h.Root.transform, false);
            skySphere.transform.localScale = Vector3.one * 180f;

            h.SkyDomeMaterial = new Material(_skyShader);
            var mr = skySphere.GetComponent<MeshRenderer>();
            mr.sharedMaterial = h.SkyDomeMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var sunObj = new GameObject("RoomDirectionalLight");
            sunObj.transform.SetParent(h.Root.transform, false);
            sunObj.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            h.MainLight = sunObj.AddComponent<Light>();
            h.MainLight.type = LightType.Directional;
            h.MainLight.intensity = 1.0f;

            var accentObj = new GameObject("RoomAccentPointLight");
            accentObj.transform.SetParent(h.Root.transform, false);
            accentObj.transform.localPosition = new Vector3(0f, 2.5f, 1.2f);
            h.AccentPointLight = accentObj.AddComponent<Light>();
            h.AccentPointLight.type = LightType.Point;
            h.AccentPointLight.range = 12f;
            h.AccentPointLight.intensity = 1.15f;
            h.AccentPointLight.color = new Color(0.15f, 0.8f, 1.0f);

            RoomTimeOfDay defaultTod = (id == RoomId.SpaceStation || id == RoomId.CinemaRoom || id == RoomId.GamingRoom || id == RoomId.FuturisticBedroom)
                ? RoomTimeOfDay.Night
                : RoomTimeOfDay.Day;
            ApplyTimeOfDay(h, defaultTod);
        }

        public void ApplyTimeOfDay(BuiltRoomHandles h, RoomTimeOfDay tod)
        {
            if (h == null || h.SkyDomeMaterial == null || h.MainLight == null) return;

            switch (tod)
            {
                case RoomTimeOfDay.Day:
                    h.SkyDomeMaterial.SetColor("_TopColor", new Color(0.12f, 0.42f, 0.85f, 1f));
                    h.SkyDomeMaterial.SetColor("_HorizonColor", new Color(0.55f, 0.78f, 0.96f, 1f));
                    h.SkyDomeMaterial.SetColor("_BottomColor", new Color(0.18f, 0.24f, 0.32f, 1f));
                    h.SkyDomeMaterial.SetFloat("_StarIntensity", 0.0f);
                    h.MainLight.color = new Color(1.0f, 0.97f, 0.90f);
                    h.MainLight.intensity = 1.15f;
                    h.MainLight.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
                    RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.54f);
                    break;

                case RoomTimeOfDay.Sunset:
                    h.SkyDomeMaterial.SetColor("_TopColor", new Color(0.14f, 0.12f, 0.36f, 1f));
                    h.SkyDomeMaterial.SetColor("_HorizonColor", new Color(0.95f, 0.44f, 0.22f, 1f));
                    h.SkyDomeMaterial.SetColor("_BottomColor", new Color(0.12f, 0.07f, 0.12f, 1f));
                    h.SkyDomeMaterial.SetFloat("_StarIntensity", 0.35f);
                    h.MainLight.color = new Color(1.0f, 0.62f, 0.36f);
                    h.MainLight.intensity = 0.92f;
                    h.MainLight.transform.rotation = Quaternion.Euler(14f, -65f, 0f);
                    RenderSettings.ambientLight = new Color(0.35f, 0.24f, 0.28f);
                    break;

                case RoomTimeOfDay.Night:
                default:
                    h.SkyDomeMaterial.SetColor("_TopColor", new Color(0.015f, 0.03f, 0.09f, 1f));
                    h.SkyDomeMaterial.SetColor("_HorizonColor", new Color(0.05f, 0.12f, 0.26f, 1f));
                    h.SkyDomeMaterial.SetColor("_BottomColor", new Color(0.01f, 0.015f, 0.04f, 1f));
                    h.SkyDomeMaterial.SetFloat("_StarIntensity", 1.25f);
                    h.MainLight.color = new Color(0.45f, 0.60f, 0.90f);
                    h.MainLight.intensity = 0.48f;
                    h.MainLight.transform.rotation = Quaternion.Euler(32f, 25f, 0f);
                    RenderSettings.ambientLight = new Color(0.16f, 0.20f, 0.30f);
                    break;
            }
        }

        private void BuildFuturisticBedroom(BuiltRoomHandles h)
        {
            Transform root = h.Root.transform;
            Material floorMat = CreateMat(new Color(0.09f, 0.11f, 0.17f), 0.25f);
            Material wallMat = CreateMat(new Color(0.13f, 0.16f, 0.24f), 0.45f);
            Material neonMat = CreateEmissiveMat(h, new Color(0f, 0.85f, 1f));

            // Room shell with open panoramic star window ahead (+Z)
            CreateBox(root, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(8.4f, 0.1f, 8.4f), floorMat);
            CreateBox(root, "Ceiling", new Vector3(0f, 3.4f, 0f), new Vector3(8.4f, 0.1f, 8.4f), wallMat);
            CreateBox(root, "BackWall", new Vector3(0f, 1.65f, -4.2f), new Vector3(8.4f, 3.5f, 0.15f), wallMat);
            CreateBox(root, "LeftWall", new Vector3(-4.2f, 1.65f, 0f), new Vector3(0.15f, 3.5f, 8.4f), wallMat);
            CreateBox(root, "RightWall", new Vector3(4.2f, 1.65f, 0f), new Vector3(0.15f, 3.5f, 8.4f), wallMat);
            // Panoramic front window frame
            CreateBox(root, "FrontFrameLow", new Vector3(0f, 0.35f, 4.2f), new Vector3(8.4f, 0.7f, 0.15f), wallMat);
            CreateBox(root, "FrontFrameTop", new Vector3(0f, 3.05f, 4.2f), new Vector3(8.4f, 0.7f, 0.15f), wallMat);
            CreateBox(root, "NeonTrimLeft", new Vector3(-4.08f, 0.05f, 0f), new Vector3(0.05f, 0.05f, 8.0f), neonMat);
            CreateBox(root, "NeonTrimRight", new Vector3(4.08f, 0.05f, 0f), new Vector3(0.05f, 0.05f, 8.0f), neonMat);

            // Furniture Variant A (Levitating Cyber Bed + Holo Desk)
            var varA = new GameObject("FurnitureVariant_0");
            varA.transform.SetParent(root, false);
            CreateBox(varA.transform, "FloatingBedBase", new Vector3(-2.4f, 0.38f, 1.2f), new Vector3(2.1f, 0.22f, 2.5f), CreateMat(new Color(0.18f, 0.22f, 0.32f), 0.3f));
            CreateBox(varA.transform, "BedUnderglow", new Vector3(-2.4f, 0.24f, 1.2f), new Vector3(2.15f, 0.04f, 2.55f), neonMat);
            CreateBox(varA.transform, "Mattress", new Vector3(-2.4f, 0.56f, 1.2f), new Vector3(1.92f, 0.18f, 2.32f), CreateMat(new Color(0.78f, 0.84f, 0.94f), 0.7f));
            CreateBox(varA.transform, "CurvedDesk", new Vector3(2.5f, 0.75f, 1.5f), new Vector3(1.4f, 0.08f, 2.2f), CreateMat(new Color(0.15f, 0.20f, 0.30f), 0.2f));
            h.FurnitureVariants.Add(varA);

            // Furniture Variant B (Minimalist Lounge Pod + Center Table)
            var varB = new GameObject("FurnitureVariant_1");
            varB.transform.SetParent(root, false);
            CreateCylinder(varB.transform, "LoungePodBase", new Vector3(-2.2f, 0.32f, 1.5f), new Vector3(2.2f, 0.22f, 2.2f), CreateMat(new Color(0.24f, 0.18f, 0.36f), 0.35f));
            CreateCylinder(varB.transform, "CenterHoloTable", new Vector3(1.8f, 0.45f, 1.6f), new Vector3(1.3f, 0.35f, 1.3f), neonMat);
            varB.SetActive(false);
            h.FurnitureVariants.Add(varB);

            BuildCommonDecorAndScreens(h, new Vector3(-4.08f, 1.75f, 0.4f), Quaternion.Euler(0f, -90f, 0f), new Vector3(4.08f, 1.8f, 0.5f), Quaternion.Euler(0f, 90f, 0f));
        }

        private void BuildCinemaRoom(BuiltRoomHandles h)
        {
            Transform root = h.Root.transform;
            Material darkAcoustic = CreateMat(new Color(0.06f, 0.06f, 0.09f), 0.85f);
            Material velvetRed = CreateMat(new Color(0.42f, 0.08f, 0.12f), 0.75f);
            Material warmStrip = CreateEmissiveMat(h, new Color(1.0f, 0.58f, 0.18f));

            CreateBox(root, "TheaterFloor", new Vector3(0f, -0.1f, 1f), new Vector3(11f, 0.2f, 13f), darkAcoustic);
            CreateBox(root, "TheaterCeiling", new Vector3(0f, 4.6f, 1f), new Vector3(11f, 0.2f, 13f), darkAcoustic);
            CreateBox(root, "LeftWall", new Vector3(-5.5f, 2.25f, 1f), new Vector3(0.2f, 4.8f, 13f), darkAcoustic);
            CreateBox(root, "RightWall", new Vector3(5.5f, 2.25f, 1f), new Vector3(0.2f, 4.8f, 13f), darkAcoustic);
            CreateBox(root, "BackWall", new Vector3(0f, 2.25f, -5.5f), new Vector3(11f, 4.8f, 0.2f), darkAcoustic);
            CreateBox(root, "AisleGlowL", new Vector3(-2.6f, 0.02f, 1f), new Vector3(0.06f, 0.03f, 10f), warmStrip);
            CreateBox(root, "AisleGlowR", new Vector3(2.6f, 0.02f, 1f), new Vector3(0.06f, 0.03f, 10f), warmStrip);

            // Giant Cinema Screen ahead at Z = +6.4m
            var screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenQuad.name = "GiantCinemaScreen";
            Destroy(screenQuad.GetComponent<Collider>());
            screenQuad.transform.SetParent(root, false);
            screenQuad.transform.localPosition = new Vector3(0f, 2.35f, 6.4f);
            screenQuad.transform.localScale = new Vector3(7.8f, 4.35f, 1f);
            var screenMat = CreateEmissiveMat(h, new Color(0.14f, 0.24f, 0.42f));
            h.AmbientVideoScreenRenderer = screenQuad.GetComponent<MeshRenderer>();
            h.AmbientVideoScreenRenderer.sharedMaterial = screenMat;

            var varA = new GameObject("FurnitureVariant_0");
            varA.transform.SetParent(root, false);
            for (int r = -1; r <= 1; r++)
            {
                CreateBox(varA.transform, $"VIPSeat_{r}", new Vector3(r * 1.45f, 0.45f, -0.2f), new Vector3(1.05f, 0.85f, 0.95f), velvetRed);
            }
            h.FurnitureVariants.Add(varA);

            var varB = new GameObject("FurnitureVariant_1");
            varB.transform.SetParent(root, false);
            CreateBox(varB.transform, "LuxurySofaWide", new Vector3(0f, 0.45f, -0.2f), new Vector3(3.8f, 0.8f, 1.15f), CreateMat(new Color(0.14f, 0.22f, 0.36f), 0.65f));
            varB.SetActive(false);
            h.FurnitureVariants.Add(varB);

            BuildCommonDecorAndScreens(h, new Vector3(-5.35f, 2.0f, 1.2f), Quaternion.Euler(0f, -90f, 0f), new Vector3(5.35f, 2.0f, 1.2f), Quaternion.Euler(0f, 90f, 0f), createAmbientScreen: false);
        }

        private void BuildGamingRoom(BuiltRoomHandles h)
        {
            Transform root = h.Root.transform;
            Material darkSlate = CreateMat(new Color(0.08f, 0.09f, 0.14f), 0.45f);
            Material neonMagenta = CreateEmissiveMat(h, new Color(0.95f, 0.15f, 0.85f));
            Material neonCyan = CreateEmissiveMat(h, new Color(0.0f, 0.9f, 1.0f));

            CreateBox(root, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(7.6f, 0.1f, 7.6f), darkSlate);
            CreateBox(root, "Ceiling", new Vector3(0f, 3.2f, 0f), new Vector3(7.6f, 0.1f, 7.6f), darkSlate);
            CreateBox(root, "FrontWall", new Vector3(0f, 1.6f, 3.8f), new Vector3(7.6f, 3.3f, 0.15f), darkSlate);
            CreateBox(root, "BackWall", new Vector3(0f, 1.6f, -3.8f), new Vector3(7.6f, 3.3f, 0.15f), darkSlate);
            CreateBox(root, "LeftWall", new Vector3(-3.8f, 1.6f, 0f), new Vector3(0.15f, 3.3f, 7.6f), darkSlate);
            CreateBox(root, "RightWall", new Vector3(3.8f, 1.6f, 0f), new Vector3(0.15f, 3.3f, 7.6f), darkSlate);

            // RGB light bars
            CreateBox(root, "RGBBarTop", new Vector3(0f, 3.05f, 3.68f), new Vector3(6.8f, 0.06f, 0.06f), neonMagenta);
            CreateBox(root, "RGBBarMid", new Vector3(0f, 1.15f, 3.68f), new Vector3(5.2f, 0.05f, 0.05f), neonCyan);

            var varA = new GameObject("FurnitureVariant_0");
            varA.transform.SetParent(root, false);
            CreateBox(varA.transform, "BattlestationDesk", new Vector3(0f, 0.78f, 2.9f), new Vector3(3.2f, 0.08f, 1.0f), darkSlate);
            CreateBox(varA.transform, "MonitorLeft", new Vector3(-0.95f, 1.28f, 3.05f), new Vector3(0.85f, 0.52f, 0.05f), neonCyan);
            CreateBox(varA.transform, "MonitorCenter", new Vector3(0f, 1.28f, 3.12f), new Vector3(1.0f, 0.56f, 0.05f), neonMagenta);
            CreateBox(varA.transform, "MonitorRight", new Vector3(0.95f, 1.28f, 3.05f), new Vector3(0.85f, 0.52f, 0.05f), neonCyan);
            CreateBox(varA.transform, "PCTower", new Vector3(1.35f, 1.12f, 2.85f), new Vector3(0.28f, 0.55f, 0.52f), neonMagenta);
            h.FurnitureVariants.Add(varA);

            var varB = new GameObject("FurnitureVariant_1");
            varB.transform.SetParent(root, false);
            CreateBox(varB.transform, "SimRigCockpit", new Vector3(0f, 0.55f, 2.4f), new Vector3(1.6f, 0.7f, 2.0f), CreateMat(new Color(0.18f, 0.18f, 0.28f), 0.3f));
            CreateBox(varB.transform, "ArcadeCabinet", new Vector3(-2.8f, 1.05f, 2.5f), new Vector3(0.85f, 2.1f, 0.9f), neonMagenta);
            varB.SetActive(false);
            h.FurnitureVariants.Add(varB);

            BuildCommonDecorAndScreens(h, new Vector3(-3.68f, 1.75f, 0.5f), Quaternion.Euler(0f, -90f, 0f), new Vector3(3.68f, 1.75f, 0.5f), Quaternion.Euler(0f, 90f, 0f));
        }

        private void BuildSpaceStation(BuiltRoomHandles h)
        {
            Transform root = h.Root.transform;
            Material hullWhite = CreateMat(new Color(0.76f, 0.80f, 0.88f), 0.35f);
            Material hullDark = CreateMat(new Color(0.14f, 0.18f, 0.25f), 0.30f);
            Material telemetryCyan = CreateEmissiveMat(h, new Color(0.1f, 0.85f, 1.0f));

            // Circular orbital deck + structural ribs leaving open 360 cupola view of deep space & Earth!
            CreateCylinder(root, "OrbitalDeckFloor", new Vector3(0f, -0.08f, 0f), new Vector3(9.2f, 0.08f, 9.2f), hullDark);
            CreateCylinder(root, "OrbitalDeckRing", new Vector3(0f, 0.55f, 0f), new Vector3(9.4f, 0.12f, 9.4f), hullWhite);
            CreateCylinder(root, "CupolaTopRing", new Vector3(0f, 3.85f, 0f), new Vector3(6.4f, 0.09f, 6.4f), hullWhite);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Sin(angle) * 4.4f, 1.9f, Mathf.Cos(angle) * 4.4f);
                var rib = CreateBox(root, $"CupolaRib_{i}", pos, new Vector3(0.18f, 3.9f, 0.22f), hullWhite);
                rib.transform.LookAt(new Vector3(0f, 1.9f, 0f));
            }

            // Procedural Earth sphere floating outside the cupola!
            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earth.name = "OrbitalPlanetEarth";
            Destroy(earth.GetComponent<Collider>());
            earth.transform.SetParent(root, false);
            earth.transform.localPosition = new Vector3(14f, -8f, 38f);
            earth.transform.localScale = Vector3.one * 26f;
            earth.GetComponent<MeshRenderer>().sharedMaterial = CreateMat(new Color(0.14f, 0.46f, 0.88f), 0.4f);

            var varA = new GameObject("FurnitureVariant_0");
            varA.transform.SetParent(root, false);
            CreateBox(varA.transform, "CommandConsole", new Vector3(0f, 0.78f, 2.8f), new Vector3(2.4f, 0.75f, 0.7f), hullWhite);
            CreateBox(varA.transform, "HoloNavDisplay", new Vector3(0f, 1.25f, 2.75f), new Vector3(1.8f, 0.12f, 0.45f), telemetryCyan);
            h.FurnitureVariants.Add(varA);

            var varB = new GameObject("FurnitureVariant_1");
            varB.transform.SetParent(root, false);
            CreateBox(varB.transform, "ZeroGSleepPod", new Vector3(-2.5f, 1.1f, 2.0f), new Vector3(1.2f, 2.1f, 1.1f), hullWhite);
            CreateCylinder(varB.transform, "HydroponicsColumn", new Vector3(2.5f, 1.2f, 2.0f), new Vector3(0.9f, 1.2f, 0.9f), telemetryCyan);
            varB.SetActive(false);
            h.FurnitureVariants.Add(varB);

            BuildCommonDecorAndScreens(h, new Vector3(-3.4f, 1.65f, -1.8f), Quaternion.Euler(0f, -120f, 0f), new Vector3(3.4f, 1.65f, -1.8f), Quaternion.Euler(0f, 120f, 0f));
        }

        private void BuildModernBedroom(BuiltRoomHandles h)
        {
            Transform root = h.Root.transform;
            Material oakWood = CreateMat(new Color(0.54f, 0.38f, 0.24f), 0.55f);
            Material warmCream = CreateMat(new Color(0.88f, 0.86f, 0.82f), 0.75f);
            Material linenGrey = CreateMat(new Color(0.45f, 0.48f, 0.52f), 0.8f);

            CreateBox(root, "HardwoodFloor", new Vector3(0f, -0.05f, 0f), new Vector3(8.0f, 0.1f, 8.0f), oakWood);
            CreateBox(root, "Ceiling", new Vector3(0f, 3.2f, 0f), new Vector3(8.0f, 0.1f, 8.0f), warmCream);
            CreateBox(root, "BackWall", new Vector3(0f, 1.6f, -4.0f), new Vector3(8.0f, 3.3f, 0.15f), warmCream);
            CreateBox(root, "LeftWall", new Vector3(-4.0f, 1.6f, 0f), new Vector3(0.15f, 3.3f, 8.0f), warmCream);
            CreateBox(root, "RightWall", new Vector3(4.0f, 1.6f, 0f), new Vector3(0.15f, 3.3f, 8.0f), warmCream);
            // Open scenic terrace window in front
            CreateBox(root, "WindowSill", new Vector3(0f, 0.35f, 4.0f), new Vector3(8.0f, 0.7f, 0.15f), oakWood);

            var varA = new GameObject("FurnitureVariant_0");
            varA.transform.SetParent(root, false);
            CreateBox(varA.transform, "WoodPlatformBed", new Vector3(-2.1f, 0.25f, 1.2f), new Vector3(2.2f, 0.32f, 2.4f), oakWood);
            CreateBox(varA.transform, "ModernMattress", new Vector3(-2.1f, 0.48f, 1.2f), new Vector3(2.0f, 0.22f, 2.2f), linenGrey);
            CreateBox(varA.transform, "MinimalistCredenza", new Vector3(2.6f, 0.48f, 1.4f), new Vector3(0.8f, 0.75f, 2.4f), oakWood);
            h.FurnitureVariants.Add(varA);

            var varB = new GameObject("FurnitureVariant_1");
            varB.transform.SetParent(root, false);
            CreateBox(varB.transform, "ReadingSectionalSofa", new Vector3(-2.0f, 0.42f, 1.5f), new Vector3(2.4f, 0.72f, 1.3f), linenGrey);
            CreateCylinder(varB.transform, "OakCoffeeTable", new Vector3(1.6f, 0.32f, 1.5f), new Vector3(1.2f, 0.3f, 1.2f), oakWood);
            varB.SetActive(false);
            h.FurnitureVariants.Add(varB);

            BuildCommonDecorAndScreens(h, new Vector3(-3.88f, 1.75f, 0.4f), Quaternion.Euler(0f, -90f, 0f), new Vector3(3.88f, 1.75f, 0.4f), Quaternion.Euler(0f, 90f, 0f));
        }

        private void BuildTropicalBeach(BuiltRoomHandles h)
        {
            Transform root = h.Root.transform;
            Material oceanTurquoise = CreateMat(new Color(0.06f, 0.58f, 0.72f), 0.15f);
            Material warmSand = CreateMat(new Color(0.88f, 0.80f, 0.62f), 0.85f);
            Material teakWood = CreateMat(new Color(0.48f, 0.32f, 0.18f), 0.65f);
            Material palmGreen = CreateMat(new Color(0.16f, 0.56f, 0.24f), 0.7f);

            CreateBox(root, "OceanPlane", new Vector3(0f, -0.35f, 22f), new Vector3(140f, 0.1f, 140f), oceanTurquoise);
            CreateCylinder(root, "SandIsland", new Vector3(0f, -0.22f, -2f), new Vector3(26f, 0.15f, 22f), warmSand);
            CreateBox(root, "WoodenBeachDeck", new Vector3(0f, -0.05f, 0.5f), new Vector3(6.8f, 0.12f, 6.8f), teakWood);
            CreateBox(root, "PergolaRoof", new Vector3(0f, 3.1f, 0.5f), new Vector3(6.4f, 0.1f, 6.4f), teakWood);

            // 4 Pergola pillars + Palm trees
            Vector3[] posts =
            {
                new Vector3(-3.0f, 1.5f, -2.5f), new Vector3(3.0f, 1.5f, -2.5f),
                new Vector3(-3.0f, 1.5f,  3.5f), new Vector3(3.0f, 1.5f,  3.5f)
            };
            for (int i = 0; i < posts.Length; i++)
            {
                CreateCylinder(root, $"PergolaPost_{i}", posts[i], new Vector3(0.18f, 1.6f, 0.18f), teakWood);
            }

            Vector3[] palms = { new Vector3(-6.2f, 2.2f, 5.5f), new Vector3(6.8f, 2.4f, 6.2f), new Vector3(-8.0f, 2.1f, -3.5f) };
            for (int i = 0; i < palms.Length; i++)
            {
                CreateCylinder(root, $"PalmTrunk_{i}", palms[i], new Vector3(0.32f, 2.4f, 0.32f), teakWood);
                CreateBox(root, $"PalmCanopyA_{i}", palms[i] + new Vector3(0f, 2.35f, 0f), new Vector3(2.8f, 0.22f, 0.75f), palmGreen);
                CreateBox(root, $"PalmCanopyB_{i}", palms[i] + new Vector3(0f, 2.38f, 0f), new Vector3(0.75f, 0.22f, 2.8f), palmGreen);
            }

            var varA = new GameObject("FurnitureVariant_0");
            varA.transform.SetParent(root, false);
            CreateBox(varA.transform, "SunLoungerL", new Vector3(-1.8f, 0.32f, 2.1f), new Vector3(0.95f, 0.35f, 2.1f), warmSand);
            CreateBox(varA.transform, "SunLoungerR", new Vector3(1.8f, 0.32f, 2.1f), new Vector3(0.95f, 0.35f, 2.1f), warmSand);
            h.FurnitureVariants.Add(varA);

            var varB = new GameObject("FurnitureVariant_1");
            varB.transform.SetParent(root, false);
            CreateBox(varB.transform, "DaybedCabana", new Vector3(-1.8f, 0.42f, 2.0f), new Vector3(2.1f, 0.5f, 2.1f), warmSand);
            CreateCylinder(varB.transform, "TikiTable", new Vector3(1.7f, 0.45f, 2.0f), new Vector3(1.1f, 0.4f, 1.1f), teakWood);
            varB.SetActive(false);
            h.FurnitureVariants.Add(varB);

            BuildCommonDecorAndScreens(h, new Vector3(-2.95f, 1.65f, 0.5f), Quaternion.Euler(0f, -90f, 0f), new Vector3(2.95f, 1.65f, 0.5f), Quaternion.Euler(0f, 90f, 0f));
        }

        private void BuildMountainLodge(BuiltRoomHandles h)
        {
            Transform root = h.Root.transform;
            Material alpineStone = CreateMat(new Color(0.32f, 0.35f, 0.40f), 0.8f);
            Material snowWhite = CreateMat(new Color(0.92f, 0.95f, 0.99f), 0.55f);
            Material cedarWood = CreateMat(new Color(0.42f, 0.26f, 0.16f), 0.65f);
            Material pineDark = CreateMat(new Color(0.11f, 0.32f, 0.18f), 0.8f);
            Material fireGlow = CreateEmissiveMat(h, new Color(1.0f, 0.48f, 0.12f));

            CreateBox(root, "ObservationDeck", new Vector3(0f, -0.08f, 0f), new Vector3(8.2f, 0.16f, 8.2f), cedarWood);
            CreateBox(root, "StoneBackWall", new Vector3(0f, 1.65f, -4.1f), new Vector3(8.2f, 3.4f, 0.35f), alpineStone);
            CreateBox(root, "TimberRoof", new Vector3(0f, 3.35f, -1.2f), new Vector3(8.4f, 0.18f, 5.8f), cedarWood);

            // Surrounding snow-capped procedural mountain peaks!
            Vector3[] peaks =
            {
                new Vector3(-18f, 6f, 34f), new Vector3(-4f, 9.5f, 42f),
                new Vector3(12f, 8f, 38f),  new Vector3(26f, 6.5f, 30f),
                new Vector3(-25f, 5.5f, 18f)
            };
            for (int i = 0; i < peaks.Length; i++)
            {
                var basePeak = CreateBox(root, $"MountainPeak_{i}", peaks[i], new Vector3(18f, 18f, 18f), alpineStone);
                basePeak.transform.rotation = Quaternion.Euler(35f, 45f + i * 18f, 35f);
                var snowCap = CreateBox(root, $"SnowCap_{i}", peaks[i] + new Vector3(0f, 4.2f, 0f), new Vector3(9.5f, 9.5f, 9.5f), snowWhite);
                snowCap.transform.rotation = basePeak.transform.rotation;
            }

            // Pine trees near lodge
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                Vector3 pPos = new Vector3(i * 4.2f, 1.2f, 10.5f + Mathf.Abs(i) * 1.4f);
                CreateCylinder(root, $"Pine_{i}", pPos, new Vector3(1.8f, 2.8f, 1.8f), pineDark);
            }

            var varA = new GameObject("FurnitureVariant_0");
            varA.transform.SetParent(root, false);
            CreateCylinder(varA.transform, "StoneFirePit", new Vector3(0f, 0.32f, 2.5f), new Vector3(1.35f, 0.28f, 1.35f), alpineStone);
            CreateSphere(varA.transform, "WarmEmbers", new Vector3(0f, 0.52f, 2.5f), new Vector3(0.65f, 0.32f, 0.65f), fireGlow);
            CreateBox(varA.transform, "AlpineBenchL", new Vector3(-2.1f, 0.42f, 1.8f), new Vector3(0.9f, 0.55f, 2.0f), cedarWood);
            h.FurnitureVariants.Add(varA);

            var varB = new GameObject("FurnitureVariant_1");
            varB.transform.SetParent(root, false);
            CreateBox(varB.transform, "ChaletSofa", new Vector3(-1.9f, 0.45f, 2.0f), new Vector3(2.3f, 0.75f, 1.1f), alpineStone);
            CreateBox(varB.transform, "CedarTable", new Vector3(1.8f, 0.45f, 2.0f), new Vector3(1.2f, 0.5f, 1.2f), cedarWood);
            varB.SetActive(false);
            h.FurnitureVariants.Add(varB);

            BuildCommonDecorAndScreens(h, new Vector3(-3.8f, 1.7f, -3.85f), Quaternion.Euler(0f, 180f, 0f), new Vector3(2.4f, 1.7f, -3.85f), Quaternion.Euler(0f, 180f, 0f));
        }

        private void BuildCommonDecorAndScreens(
            BuiltRoomHandles h,
            Vector3 photoPos,
            Quaternion photoRot,
            Vector3 ambientPos,
            Quaternion ambientRot,
            bool createAmbientScreen = true)
        {
            Transform root = h.Root.transform;
            h.DecorGroup = new GameObject("RoomDecorationsGroup");
            h.DecorGroup.transform.SetParent(root, false);

            var orbMat = CreateEmissiveMat(h, new Color(0.25f, 0.85f, 1.0f));
            CreateSphere(h.DecorGroup.transform, "HoloSculptureOrb", new Vector3(2.2f, 1.18f, 2.2f), Vector3.one * 0.28f, orbMat);
            CreateCylinder(h.DecorGroup.transform, "DecorPedestal", new Vector3(2.2f, 0.45f, 2.2f), new Vector3(0.32f, 0.45f, 0.32f), CreateMat(new Color(0.2f, 0.24f, 0.32f), 0.3f));

            // User Custom Photo Wall Frame (Section 3 & 13)
            var frameQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frameQuad.name = "UserCustomImageFrame";
            Destroy(frameQuad.GetComponent<Collider>());
            frameQuad.transform.SetParent(root, false);
            frameQuad.transform.localPosition = photoPos;
            frameQuad.transform.localRotation = photoRot;
            frameQuad.transform.localScale = new Vector3(1.45f, 0.95f, 1f);
            var photoMat = new Material(Shader.Find("Unlit/Texture") ?? _surfaceShader);
            photoMat.mainTexture = GenerateDefaultProceduralArtwork();
            h.CustomPhotoFrameRenderer = frameQuad.GetComponent<MeshRenderer>();
            h.CustomPhotoFrameRenderer.sharedMaterial = photoMat;

            if (createAmbientScreen && h.AmbientVideoScreenRenderer == null)
            {
                var ambQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ambQuad.name = "DecorativeAmbientVideoSurface";
                Destroy(ambQuad.GetComponent<Collider>());
                ambQuad.transform.SetParent(root, false);
                ambQuad.transform.localPosition = ambientPos;
                ambQuad.transform.localRotation = ambientRot;
                ambQuad.transform.localScale = new Vector3(1.85f, 1.05f, 1f);
                var ambMat = CreateEmissiveMat(h, new Color(0.10f, 0.32f, 0.55f));
                h.AmbientVideoScreenRenderer = ambQuad.GetComponent<MeshRenderer>();
                h.AmbientVideoScreenRenderer.sharedMaterial = ambMat;
            }
        }

        private static Texture2D GenerateDefaultProceduralArtwork()
        {
            var tex = new Texture2D(128, 84, TextureFormat.RGBA32, false);
            for (int y = 0; y < 84; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float u = x / 127f;
                    float v = y / 83f;
                    float wave = Mathf.Sin(u * 9f) * 0.08f + 0.38f;
                    Color c = v < wave
                        ? Color.Lerp(new Color(0.05f, 0.14f, 0.28f), new Color(0.0f, 0.72f, 0.92f), v / wave)
                        : Color.Lerp(new Color(0.16f, 0.08f, 0.34f), new Color(0.03f, 0.05f, 0.14f), (v - wave) / (1f - wave));
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private Material CreateMat(Color albedo, float roughness)
        {
            var m = new Material(_surfaceShader) { color = albedo };
            if (m.HasProperty("_Roughness")) m.SetFloat("_Roughness", roughness);
            return m;
        }

        private Material CreateEmissiveMat(BuiltRoomHandles h, Color emission)
        {
            var m = new Material(_surfaceShader) { color = emission * 0.55f };
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission * 0.85f);
            h.AccentEmissiveMaterials.Add(m);
            return m;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateSphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}
