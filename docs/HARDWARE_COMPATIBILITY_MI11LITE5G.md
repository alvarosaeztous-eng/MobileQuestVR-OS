# Auditoría de Compatibilidad — Xiaomi Mi 11 Lite 5G (`renoir`)

## 1. Especificaciones del Dispositivo Prioritario
| Componente | Especificación Xiaomi Mi 11 Lite 5G | Configuración en MobileQuestVR OS |
|---|---|---|
| **Modelo / Código** | `M2101K9AG` / `M2101K9C` (`renoir`) | Detectado automáticamente en `AndroidPlatformBridge.RunCompatibilityAudit()` |
| **SoC / CPU** | Qualcomm Snapdragon 780G (`SM7350-AB`, 5 nm, Octa-core Kryo 670 64 bits) | `AndroidTargetArchitectures: ARM64` (`arm64-v8a`), backend `IL2CPP` |
| **GPU** | Qualcomm Adreno 642 | Forward Mobile + Shaders ligeros dedicados (`MobileQuestVR/*`), Vulkan / OpenGLES3 |
| **Pantalla** | 6.55" AMOLED, `2400 × 1080` px (20:9), hasta **90 Hz** | Orientación `LandscapeLeft` inmersiva, objetivo adaptativo **90 FPS** |
| **Memoria RAM** | 6 GB / 8 GB LPDDR4X | Geometría procedural ligera sin texturas descomprimidas pesadas (< 180 MB RAM estimados) |
| **Almacenamiento** | 128 GB / 256 GB UFS 2.2 interno (sin microSD en variante 5G) | Guardado en `Application.persistentDataPath` interno (`allowBackup="false"`) |
| **Sistema Operativo** | Android 11 de fábrica, actualizable a Android 13 (MIUI 14, API 30–33) | `minSdkVersion = 26` (Android 8.0+), `targetSdkVersion = Auto` (API 34/35 en GameCI) |
| **Sensores** | Giroscopio hardware, Acelerómetro, Brújula electrónica, Sensor de luz | `Input.gyro` a 90 Hz con recentrado instantáneo y modo de movimiento reducido |
| **Cámara Trasera** | Principal 64 MP f/1.79 + Ultra gran angular 8 MP | Solicitada bajo demanda (`1280×720 @ 30 fps`) para minimizar latencia y calentamiento |

---

## 2. Gestión Térmica y de Batería en Snapdragon 780G
- **Objetivo normal en Xiaomi Mi 11 Lite 5G**: Nivel de calidad `Mi11Lite5G High 90Hz` (MSAA 2x, 90 FPS).
- **Umbral térmico preventivo (`>= 42.0 °C`)**: `AdaptivePerformanceController` lee `BatteryManager.EXTRA_TEMPERATURE` cada 5 segundos; al superar 42 °C cambia automáticamente al perfil `Thermal Saver` y limita a **45 FPS** hasta que el chasis descienda a `<= 39.0 °C`.
- **Alertas de batería**:
  - `< 20 %`: Notificación espacial preventiva de batería baja.
  - `< 10 %`: Notificación crítica, guardado inmediato de preferencias locales y reducción a 45 FPS.

---

## 3. Verificaciones Pendientes en Hardware Físico Real
Siguiendo la regla estricta de honestidad de ingeniería (*no afirmar que una función ha sido validada en hardware físico sin ejecutarla en el terminal*), quedan sujetas a prueba física tras descargar e instalar el APK desde GitHub Actions:
1. Calibración fina de `K1`/`K2` e IPD según la carcasa/visor VR físico concreto donde se introduzca el Xiaomi Mi 11 Lite 5G (sus 6.55" en formato 20:9 requieren centrar el teléfono con el puente nasal del visor).
2. Medición real de latencia fotón-a-fotón de `WebCamTexture` en MIUI 14 para el modo experimental de Realidad Mixta.
3. Prueba prolongada de 30 minutos a 90 Hz para verificar la curva térmica del Adreno 642 dentro de una carcasa VR cerrada.
