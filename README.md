# MobileQuestVR OS

**Entorno Espacial Nativo de Realidad Virtual Móvil para Android — Unity 6 (`6000.0.36f1`)**  
Optimizado prioritariamente para **Xiaomi Mi 11 Lite 5G** (`Snapdragon 780G` · `Adreno 642` · `ARM64` · `AMOLED 90 Hz`).

---

## Características Principales Implementadas

- **Identidad y Arranque Nativo (`00_Boot.unity`)**:
  - Nombre de aplicación Android: `MobileQuestVR` (`com.mobilequestvr.os`), orientación horizontal inmersiva a pantalla completa e icono propio de visor VR futurista (`Assets/Icons/AppIcon_512.png`).
  - Secuencia de arranque 3D con fondo espacial estrellado procedural, logotipo animado con anillos holográficos, acorde espacial sintetizado y barra de carga suave.
- **Primer Arranque y Selector Panorámico (`01_RoomSelector.unity`)**:
  - Asistente interactivo de primer inicio en 4 pasos (idioma ES/EN y consejos, ajuste de visor/IPD, calibración de orientación 3DoF, prueba de selección/pinza).
  - Selector panorámico 360° con vista previa en tiempo real y favoritos.
- **7 Habitaciones 3D Incluidas Localmente en el APK (`MobileQuestVR.Environment`)**:
  1. Dormitorio Futurista *(entorno insignia de Fase 1)*
  2. Sala de Cine *(con gran pantalla para proyección multimedia)*
  3. Sala Gaming *(estación triple monitor e iluminación neón)*
  4. Estación Espacial *(cúpula orbital con vista al espacio y planeta)*
  5. Dormitorio Moderno
  6. Playa Tropical
  7. Montaña Alpina
  - Punto de vista estático 360° cómodo (sin locomoción libre) y personalización persistente en almacenamiento local JSON (cambio de muebles, decoración, color de luz, modo Diurno/Atardecer/Nocturno, sonido ambiental e imágenes propias en marco mural).
- **Interfaz Espacial 3D y Multitarea (`02_MainOS.unity`)**:
  - Estética tridimensional inspirada en Meta Quest con placas holográficas con profundidad (`MobileQuestVR/Hologram`), muelle rápido personalizable, botón virtual de menú principal y barra de estado (hora, batería, Wi-Fi, FPS/temperatura e indicadores de privacidad de cámara/micrófono).
  - Hasta **3 ventanas flotantes simultáneas**, movimiento espacial con la mirada, distribución automática en arco, minimizar, maximizar, cerrar y ajuste de tamaño/distancia.
  - Accesos espaciales para **YouTube**, **Chrome**, **Google**, **Ajustes** y **Spotify** (con conmutación entre **Minirreproductor** y **Ventana Completa**).
- **Renderizado VR, Seguimiento y Comodidad**:
  - Visión estereoscópica dividida para ambos ojos + modo pantalla única normal, perfiles de visor, ajuste manual de IPD y distorsión de lentes (`K1`/`K2`).
  - Seguimiento de cabeza **3DoF** continuo por giroscopio con calibración y recentrado, más punto central de selección por permanencia/toque/pinza.
  - Módulo experimental de **Realidad Mixta** con cámara trasera y **Seguimiento de Manos** preparado para MediaPipe JNI con permisos solicitados estrictamente bajo demanda.
  - Modo de movimiento reducido, recordatorios de descanso, alertas de batería baja/crítica, control térmico adaptativo, reloj/cronómetro/temporizador, asistente de voz opcional por botón, menú de pausa y confirmación antes de salir.

---

## Estructura del Proyecto

```text
MobileQuestVR-OS/
├── .github/workflows/
│   └── build-android-apk.yml          # Workflow manual GameCI para compilar y publicar el APK
├── Assets/
│   ├── Editor/                        # BuildScript.cs + AndroidManifestPostProcessor.cs
│   ├── Icons/                         # Iconos nativos de visor futurista (512px + adaptativos)
│   ├── Resources/
│   │   ├── Branding/BootVisorLogo.png # Emblema animado de arranque
│   │   └── Shaders/                   # 5 shaders móviles optimizados (Surface, Hologram, Starfield, Sphere360, LensDistort)
│   ├── Scenes/                        # 00_Boot.unity, 01_RoomSelector.unity, 02_MainOS.unity
│   ├── Scripts/                       # 17 módulos desacoplados con Assembly Definitions (.asmdef)
│   └── Tests/EditMode/                # Suite de pruebas unitarias NUnit EditMode
├── Packages/manifest.json             # Dependencias oficiales Unity 6000.0.36f1
├── ProjectSettings/                   # Configuración Android ARM64 IL2CPP Landscape Immersive
├── Tools/validate_unity_project.py    # Auditoría estática de AST C#, DAG asmdef, GUIDs y seguridad
└── docs/                              # Documentación de arquitectura, compatibilidad Mi 11 Lite 5G y compilación APK
```

---

## Validación y Compilación APK

Ejecuta el validador estático del proyecto en cualquier entorno con Python 3.11+:

```bash
python3 Tools/validate_unity_project.py
```

Para compilar y descargar el archivo instalable **`MobileQuestVR.apk`** mediante **GitHub Actions + GameCI** o **Unity Hub (`6000.0.36f1`)**, sigue las instrucciones paso a paso en [`docs/BUILD_AND_APK_GUIDE.md`](docs/BUILD_AND_APK_GUIDE.md).
