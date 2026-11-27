# Arquitectura Modular — MobileQuestVR OS (Unity 6)

## 1. Visión General
**MobileQuestVR OS** es un entorno espacial nativo de realidad virtual móvil desarrollado desde cero para **Unity 6 (`6000.0.36f1`)** con destino **Android APK (`com.mobilequestvr.os`)**, priorizando el dispositivo **Xiaomi Mi 11 Lite 5G** (Snapdragon 780G / Adreno 642, ARM64, AMOLED 90 Hz).

No utiliza ninguna aplicación o maqueta web como contenedor principal: toda la renderización 3D estereoscópica, habitaciones tridimensionales, menús flotantes con profundidad holográfica, seguimiento 3DoF por giroscopio, audio espacial sintetizado y gestión de ventanas se ejecutan de forma nativa en C# / IL2CPP sobre el motor gráfico de Unity 6.

---

## 2. Flujo de Escenas (`Assets/Scenes/`)
1. **`00_Boot.unity`** (`BootSceneEntry`):
   - Cúpula estelar 3D procedural (`MobileQuestVR/StarfieldSky`) + partículas estelares tridimensionales.
   - Logotipo animado de visor futurista (`Resources/Branding/BootVisorLogo.png`) con anillos orbitales holográficos.
   - Barra de progreso suave, acorde espacial de inicio y calentamiento de subsistemas.
2. **`01_RoomSelector.unity`** (`RoomSelectorSceneEntry`):
   - **Asistente interactivo de primer arranque** (4 pasos: Idioma/Consejos, Ajuste de Visor/IPD, Calibración 3DoF de Cabeza, Prueba de Selección/Pinza).
   - **Selector panorámico 360°** con vista previa en vivo de las 7 habitaciones incluidas localmente en el APK y marcado de favoritas.
3. **`02_MainOS.unity`** (`MainOSSceneEntry`):
   - Escritorio espacial completo con habitación 3D activa, muelle rápido personalizable, barra de estado (hora, batería, Wi-Fi, indicadores de cámara/micrófono activos), menú principal 3D, hasta 3 ventanas flotantes simultáneas, centro multimedia 2D/360°, galería local, realidad mixta experimental y menú de pausa con confirmación de salida.

---

## 3. Los 17 Módulos Desacoplados (`Assets/Scripts/`)

| # | Módulo (`.asmdef`) | Responsabilidad Principal |
|---|---|---|
| 1 | `MobileQuestVR.Localization` | Diccionario bilingüe Español/Inglés en vivo y tipografía compatible con Unity 6 (`LegacyRuntime.ttf` / `Roboto`). |
| 2 | `MobileQuestVR.AndroidBridge` | Permisos bajo demanda (`CAMERA`, `RECORD_AUDIO`), modo inmersivo horizontal, telemetría térmica/batería Android JNI, auditoría de hardware Xiaomi Mi 11 Lite 5G y passthrough experimental de cámara trasera. |
| 3 | `MobileQuestVR.Settings` | Perfil de usuario, temas visuales, IPD, distorsión de lentes, comodidad, PIN SHA-256 opcional **solo** para ajustes sensibles (nunca al abrir la app) y persistencia JSON local sin nube. |
| 4 | `MobileQuestVR.Notifications` | Panel y avisos flotantes 3D para recordatorios de descanso, batería baja/crítica, avisos térmicos e indicadores de privacidad. |
| 5 | `MobileQuestVR.Performance` | Calidad gráfica adaptativa, FPS adaptativos (45/60/72/90 Hz) y protección térmica automática (>42 °C). |
| 6 | `MobileQuestVR.Audio` | Síntesis PCM procedural de efectos espaciales 3D, paisajes sonoros ambientales por habitación y asistente de voz opcional activado únicamente por botón. |
| 7 | `MobileQuestVR.VRRendering` | Rig estereoscópico de doble cámara (ojo izquierdo/derecho), ajuste IPD (54–74 mm), shader de distorsión de barril configurable (`K1`/`K2`), brillo y conmutación instantánea VR / 2D. |
| 8 | `MobileQuestVR.HeadTracking` | Orientación continua 3DoF mediante giroscopio hardware + respaldo táctil/ratón, calibración inicial, botón de recentrado y puntero central de selección con permanencia/pinza. |
| 9 | `MobileQuestVR.HandTracking` | Arquitectura híbrida preparada para MediaPipe Hands JNI + estimador óptico experimental por cámara trasera, visualización holográfica de mano, gesto de pinza, calibración y guía cuando no se detectan manos. |
| 10 | `MobileQuestVR.Environment` | Construcción 3D procedural local de las **7 habitaciones** (Dormitorio Futurista, Sala de Cine, Sala Gaming, Estación Espacial, Dormitorio Moderno, Playa Tropical y Montaña) con punto de vista estático 360°. |
| 11 | `MobileQuestVR.RoomCustomization` | Cambio de variantes de mobiliario, decoración, color de iluminación, ambiente Diurno/Atardecer/Nocturno, marcos con imágenes propias del usuario y guardado JSON local. |
| 12 | `MobileQuestVR.Windows` | Gestor espacial de ventanas 3D con placa holográfica física, límite estricto de **hasta 3 ventanas visibles**, mover con mirada, distribución automática en arco, minimizar, maximizar y cerrar. |
| 13 | `MobileQuestVR.Browser` | Ventanas espaciales para YouTube, Chrome, Google y Spotify (con modo Minirreproductor y Ventana Completa), respetando políticas CORS/DRM y sin ejecutar APKs externos arbitrarios en 3D. |
| 14 | `MobileQuestVR.Media` | Reproductor de vídeo 2D y 360°, visor de fotografías normales y panorámicas 360° (`Sphere360`), proyección en pantalla de cine, capturas normales/estéreo VR y grabación VR experimental por fotogramas. |
| 15 | `MobileQuestVR.Gallery` | Explorador de almacenamiento interno (`persistentDataPath/Media`) y carpetas locales compatibles, integrado con el visor 2D/360° y los marcos decorativos de las habitaciones. |
| 16 | `MobileQuestVR.Core` | Kernel central (`OSKernel`), muelle espacial, barra de estado, menú principal 3D, reloj/cronómetro/temporizador, menú de pausa y diálogo obligatorio de confirmación de salida. |
| 17 | `MobileQuestVR.Boot` | Secuencia de arranque espacial animada y transición hacia el selector panorámico. |

---

## 4. Limitaciones Técnicas Documentadas con Transparencia
1. **Seguimiento 3DoF vs 6DoF**: El Xiaomi Mi 11 Lite 5G dispone de giroscopio, acelerómetro y magnetómetro para orientación rotacional continua **3DoF**. El sistema no afirma ni finge desplazamiento posicional 6DoF libre.
2. **Servicios Web y DRM (YouTube, Spotify, Chrome, Google)**: Servicios con cabeceras `X-Frame-Options` o cifrado Widevine DRM restringen la reproducción incrustada arbitraria en texturas 3D sin un motor Chromium/Gecko nativo embebido; `SpatialWebAppsController` ofrece previsualización, accesos espaciales, modo minirreproductor/ventana completa y lanzamiento seguro compatible con Android sin prometer ejecución de APKs Android arbitrarios dentro de ventanas 3D.
3. **Realidad Mixta y Seguimiento de Manos**: El passthrough emplea `WebCamTexture` sobre la cámara trasera y está catalogado como **experimental** (sujeto a latencia de exposición de cámara Android e iluminación). El módulo `HandTracking` incluye el puente JNI listo para enlazar `com.google.mediapipe:tasks-vision` (`.aar`) en Fase 4 avanzada y opera mientras tanto con un estimador óptico experimental + gestos de prueba y mensajes claros de ayuda cuando no detecta manos.
4. **Grabación VR**: La captura de vídeo VR está implementada de forma experimental como secuencia controlada de fotogramas estéreo limitada a 30 s para evitar saturación térmica y de almacenamiento UFS antes de integrar codificación hardware `MediaProjection`/`MediaCodec` en pruebas sobre dispositivo físico.
