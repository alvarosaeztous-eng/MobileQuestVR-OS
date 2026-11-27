using System;
using System.Collections.Generic;
using UnityEngine;

namespace MobileQuestVR.Localization
{
    public enum AppLanguage
    {
        Spanish = 0,
        English = 1
    }

    /// <summary>
    /// Bilingual (Spanish / English) localization service with live reactive UI updates.
    /// </summary>
    public static class LocalizationService
    {
        private static AppLanguage _currentLanguage = AppLanguage.Spanish;
        public static event Action<AppLanguage> OnLanguageChanged;

        public static AppLanguage CurrentLanguage => _currentLanguage;

        private static readonly Dictionary<string, (string es, string en)> Entries =
            new Dictionary<string, (string es, string en)>(StringComparer.OrdinalIgnoreCase)
            {
                // Identity & Boot
                ["app.title"] = ("MobileQuestVR OS", "MobileQuestVR OS"),
                ["app.subtitle"] = ("Entorno Espacial VR Nativo para Android", "Native Spatial VR Environment for Android"),
                ["boot.initializing"] = ("Inicializando núcleo espacial...", "Initializing spatial kernel..."),
                ["boot.sensors"] = ("Verificando sensores 3DoF y pantalla...", "Checking 3DoF sensors & display..."),
                ["boot.rooms"] = ("Preparando entornos 3D locales...", "Preparing local 3D environments..."),
                ["boot.ready"] = ("Sistema listo", "System ready"),

                // First-boot Onboarding (Section 15)
                ["onboarding.welcome.title"] = ("Bienvenido a MobileQuestVR OS", "Welcome to MobileQuestVR OS"),
                ["onboarding.welcome.body"] = ("Explora habitaciones 3D, ventanas espaciales flotantes, multimedia 360° y realidad mixta experimental.", "Explore 3D rooms, floating spatial windows, 360° media, and experimental mixed reality."),
                ["onboarding.headset.title"] = ("Ajuste del Visor y Lentes", "Headset & Lens Adjustment"),
                ["onboarding.headset.body"] = ("Ajusta la distancia interpupilar (IPD) y la distorsión de lentes para ver nítidamente con ambos ojos.", "Adjust interpupillary distance (IPD) and lens distortion for sharp stereo vision."),
                ["onboarding.head.title"] = ("Calibración de Orientación (3DoF)", "Head Orientation Calibration (3DoF)"),
                ["onboarding.head.body"] = ("Mira al frente cómodamente y pulsa Calibrar. El seguimiento usa giroscopio 3DoF (orientación continua sin desplazamiento físico 6DoF).", "Look straight ahead comfortably and press Calibrate. Tracking uses 3DoF gyroscope (continuous rotation, not 6DoF positional walking)."),
                ["onboarding.pinch.title"] = ("Prueba del Gesto de Pinza y Selección", "Pinch Gesture & Gaze Selection Test"),
                ["onboarding.pinch.body"] = ("Apunta con el punto central y selecciona con toque, permanencia o gesto de pinza cuando el seguimiento de manos esté activo.", "Aim with the center reticle and select via tap, gaze dwell, or pinch gesture when hand tracking is enabled."),
                ["onboarding.room.title"] = ("Elige tu Habitación Inicial", "Choose Your Starting Room"),
                ["onboarding.next"] = ("Siguiente", "Next"),
                ["onboarding.finish"] = ("Entrar a MobileQuestVR", "Enter MobileQuestVR"),
                ["onboarding.tips"] = ("Mostrar consejos interactivos", "Show interactive tips"),

                // Rooms (Section 3)
                ["room.selector.title"] = ("Selector Panorámico de Habitaciones 3D", "Panoramic 3D Room Selector"),
                ["room.futuristic_bedroom"] = ("Dormitorio Futurista", "Futuristic Bedroom"),
                ["room.cinema"] = ("Sala de Cine", "Cinema Room"),
                ["room.gaming"] = ("Sala Gaming", "Gaming Room"),
                ["room.space_station"] = ("Estación Espacial", "Space Station"),
                ["room.modern_bedroom"] = ("Dormitorio Moderno", "Modern Bedroom"),
                ["room.tropical_beach"] = ("Playa Tropical", "Tropical Beach"),
                ["room.mountain"] = ("Montaña Alpina", "Alpine Mountain"),
                ["room.enter"] = ("Entrar al Entorno", "Enter Environment"),
                ["room.favorite"] = ("Favorita", "Favorite"),
                ["room.static_notice"] = ("Vista estática 360° (mira alrededor sin desplazamiento físico)", "Static 360° viewpoint (look around without physical walking)"),

                // Room Customization
                ["custom.title"] = ("Personalizar Habitación", "Customize Room"),
                ["custom.furniture"] = ("Cambiar Muebles", "Swap Furniture"),
                ["custom.decor"] = ("Alternar Decoración", "Toggle Decor"),
                ["custom.lighting"] = ("Color de Luz", "Lighting Color"),
                ["custom.day"] = ("Ambiente Diurno", "Daytime Ambience"),
                ["custom.sunset"] = ("Atardecer", "Sunset Ambience"),
                ["custom.night"] = ("Nocturno", "Night Ambience"),
                ["custom.photo"] = ("Imagen Propia en Marco", "Custom Frame Image"),
                ["custom.ambient_audio"] = ("Sonido Ambiental", "Ambient Sound"),
                ["custom.save"] = ("Guardar Cambios", "Save Changes"),
                ["custom.saved"] = ("Personalización guardada localmente", "Customization saved locally"),

                // Main Menu & Quick Bar
                ["menu.home"] = ("Menú Espacial", "Spatial Menu"),
                ["menu.apps"] = ("Aplicaciones", "Applications"),
                ["menu.rooms"] = ("Habitaciones", "Rooms"),
                ["menu.customize"] = ("Decoración", "Decorate"),
                ["menu.media"] = ("Multimedia 360°", "360° Media"),
                ["menu.gallery"] = ("Galería", "Gallery"),
                ["menu.clock"] = ("Reloj y Temporizador", "Clock & Timer"),
                ["menu.settings"] = ("Ajustes", "Settings"),
                ["menu.pause"] = ("Pausa", "Pause"),
                ["menu.arrange_windows"] = ("Distribuir Ventanas", "Auto-Arrange Windows"),
                ["menu.recenter"] = ("Recentrar Vista", "Recenter View"),
                ["menu.vr_toggle"] = ("Modo VR / Normal", "VR / Normal Mode"),
                ["menu.mr_toggle"] = ("Realidad Mixta", "Mixed Reality"),
                ["menu.hands_toggle"] = ("Manos / Pinza", "Hands / Pinch"),
                ["menu.voice"] = ("Asistente de Voz", "Voice Assistant"),

                // Apps & Windows
                ["app.youtube"] = ("YouTube Web", "YouTube Web"),
                ["app.chrome"] = ("Navegador Espacial", "Spatial Browser"),
                ["app.google"] = ("Google Search", "Google Search"),
                ["app.spotify"] = ("Spotify", "Spotify"),
                ["app.spotify.mini"] = ("Minirreproductor", "Mini Player"),
                ["app.spotify.full"] = ("Ventana Completa", "Full Window"),
                ["app.settings"] = ("Ajustes del Sistema", "System Settings"),
                ["window.limit_reached"] = ("Máximo de 3 ventanas simultáneas alcanzado. Cierra o minimiza una ventana.", "Maximum of 3 simultaneous windows reached. Close or minimize one first."),
                ["window.minimize"] = ("Minimizar", "Minimize"),
                ["window.maximize"] = ("Maximizar", "Maximize"),
                ["window.close"] = ("Cerrar", "Close"),
                ["window.move"] = ("Mover", "Move"),
                ["web.restriction_note"] = ("Versión web sujeta a políticas DRM/inserción del proveedor. Sin ejecución arbitraria de APKs externos en 3D.", "Web version subject to provider embedding/DRM policies. Arbitrary Android APKs are not executed in 3D windows."),

                // Mixed Reality & Privacy Indicators (Sections 6 & 14)
                ["privacy.camera_active"] = ("● CÁMARA ACTIVA", "● CAMERA ACTIVE"),
                ["privacy.mic_active"] = ("● MICRÓFONO ACTIVO", "● MICROPHONE ACTIVE"),
                ["mr.experimental"] = ("Realidad Mixta (Experimental): latencia y nitidez dependen del sensor trasero.", "Mixed Reality (Experimental): latency and clarity depend on rear camera sensor."),
                ["mr.permission_denied"] = ("Permiso de cámara denegado. La realidad mixta requiere acceso puntual a la cámara trasera.", "Camera permission denied. Mixed reality requires on-demand rear camera access."),
                ["hands.not_detected"] = ("Manos no detectadas: coloca tu mano abierta frente a la cámara trasera con buena luz y junta pulgar e índice para hacer pinza.", "Hands not detected: hold your open hand in front of the rear camera in good lighting and pinch thumb + index."),

                // Pause & Exit Confirmation (Sections 9 & 14)
                ["pause.title"] = ("Sistema en Pausa", "System Paused"),
                ["pause.continue"] = ("Continuar", "Resume"),
                ["pause.home"] = ("Selector de Inicio", "Home Selector"),
                ["pause.settings"] = ("Ajustes", "Settings"),
                ["pause.exit"] = ("Salir de MobileQuestVR", "Exit MobileQuestVR"),
                ["exit.confirm_title"] = ("¿Deseas salir de MobileQuestVR OS?", "Exit MobileQuestVR OS?"),
                ["exit.confirm_yes"] = ("Confirmar Salida", "Confirm Exit"),
                ["exit.confirm_no"] = ("Cancelar", "Cancel"),

                // Comfort & Alerts (Section 8)
                ["alert.rest_reminder"] = ("Recordatorio de comodidad: descansa la vista unos minutos.", "Comfort reminder: take a short break to rest your eyes."),
                ["alert.battery_low"] = ("Batería baja (<20%). Se recomienda conectar el cargador.", "Low battery (<20%). Connecting charger recommended."),
                ["alert.battery_critical"] = ("Batería crítica (<10%). Guardando estado local y reduciendo consumo.", "Critical battery (<10%). Saving local state and reducing power."),
                ["alert.thermal"] = ("Temperatura elevada detectada: reduciendo calidad gráfica y FPS automáticamente.", "High device temperature detected: lowering graphics quality and FPS automatically."),

                // Settings & PIN
                ["settings.profile"] = ("Perfil Local", "Local Profile"),
                ["settings.display_vr"] = ("Visor VR y Lentes", "VR Headset & Lenses"),
                ["settings.comfort"] = ("Comodidad y Salud", "Comfort & Safety"),
                ["settings.audio"] = ("Audio Espacial", "Spatial Audio"),
                ["settings.system"] = ("Información del Dispositivo", "Device Information"),
                ["settings.pin_locked"] = ("Ajuste protegido por PIN opcional", "Setting protected by optional PIN")
            };

        public static void SetLanguage(AppLanguage language)
        {
            if (_currentLanguage == language) return;
            _currentLanguage = language;
            OnLanguageChanged?.Invoke(_currentLanguage);
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (Entries.TryGetValue(key, out var pair))
            {
                return _currentLanguage == AppLanguage.English ? pair.en : pair.es;
            }
            return key;
        }

        public static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Roboto", "Sans-Serif", "Arial" }, 18);
            }
            return font;
        }
    }
}
