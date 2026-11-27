# Guía de Compilación y Descarga del APK Android (Unity 6 + GameCI)

## 1. Estado de la Compilación en el Entorno Actual
- **Auditoría del entorno local**: El contenedor de desarrollo dispone de Linux Debian 12, Python 3.11, Git y GitHub CLI, pero **no dispone de Unity 6 Editor, Android SDK/NDK, Gradle ni licencia de Unity instalada localmente**.
- Conforme a las reglas del proyecto (**prohibido generar APKs falsos, vacíos o simulados**), el repositorio queda **100 % estructurado, validado sintáctica y estructuralmente (`Tools/validate_unity_project.py`: 0 errores)** y equipado con el workflow automatizado en la nube **`.github/workflows/build-android-apk.yml`** basado en **GameCI + Unity `6000.0.36f1`** para producir el APK Android nativo real.

---

## 2. Configurar los Secretos de Licencia en GitHub (Obligatorio una sola vez)
Nunca guardes licencias, contraseñas ni keystores en archivos del repositorio. Configúralos en tu repositorio de GitHub (`Settings` -> `Secrets and variables` -> `Actions` -> `New repository secret`):

### Secretos requeridos para activar Unity 6 en GameCI:
1. `UNITY_EMAIL` — Correo de tu cuenta de Unity ID.
2. `UNITY_PASSWORD` — Contraseña de tu cuenta de Unity ID.
3. `UNITY_LICENSE` — Contenido íntegro XML del archivo de licencia `.ulf` de Unity (Personal o Pro).
   - *Nota*: Para licencias Pro/Plus también puedes añadir `UNITY_SERIAL`. Para generar `UNITY_LICENSE` gratuito con Unity Personal sigue la guía oficial de activación de [GameCI Activation](https://game.ci/docs/github/activation).

### Secretos opcionales para firma Release (Keystore propio):
Si no los defines, el workflow generará automáticamente un APK firmado con clave debug estándar de Android **directamente instalable** en tu Xiaomi Mi 11 Lite 5G. Para firma de producción opcional:
- `ANDROID_KEYSTORE_BASE64` (`base64 -w 0 release.keystore`)
- `ANDROID_KEYSTORE_PASS`
- `ANDROID_KEYALIAS_NAME`
- `ANDROID_KEYALIAS_PASS`

---

## 3. Cómo Compilar y Descargar el APK desde GitHub Actions
1. Sube la rama actual al repositorio en GitHub (`git push`).
2. Entra en la pestaña **Actions** del repositorio `MobileQuestVR-OS` en GitHub.
3. En la barra lateral izquierda selecciona **"Build MobileQuestVR Android APK (Unity 6 + GameCI)"**.
4. Pulsa el botón **"Run workflow"**, elige la rama, deja o ajusta `android_version_code` y confirma **"Run workflow"**.
5. El workflow ejecutará automáticamente:
   - **Job 1 (`validate-unity-project`)**: Verifica el AST C# de todos los scripts, el grafo acíclico de los 17 módulos `.asmdef`, los GUIDs `.meta`/`.unity` y la ausencia de secretos filtrados.
   - **Job 2 (`build-android-apk`)**: Descarga la imagen oficial Unity `6000.0.36f1` con soporte Android IL2CPP ARM64, ejecuta `MobileQuestVR.Editor.BuildScript.BuildAndroidApk`, comprueba que `Builds/Android/MobileQuestVR.apk` existe, pesa > 5 MB y contiene `AndroidManifest.xml` + `lib/arm64-v8a/libil2cpp.so`, calcula su `SHA-256` y lo publica como artefacto.
6. Al finalizar en verde, baja a la sección **Artifacts** del resumen de la ejecución y descarga **`MobileQuestVR-Android-APK-v1.0.0-<n>`**.
7. Descomprime el `.zip` descargado para obtener `MobileQuestVR.apk` y cópialo a tu **Xiaomi Mi 11 Lite 5G** (o instálalo por USB con `adb install -r MobileQuestVR.apk`).

---

## 4. Compilación Local Alternativa con Unity Hub
1. Instala **Unity Hub** y el editor **Unity `6000.0.36f1`** seleccionando los módulos:
   - *Android Build Support*
   - *OpenJDK*
   - *Android SDK & NDK Tools*
2. Abre la carpeta raíz `MobileQuestVR-OS` desde Unity Hub.
3. En la barra superior de Unity, haz clic en **`MobileQuestVR` -> `Build Installable Android APK`**.
4. El APK instalable se generará en `Builds/Android/MobileQuestVR.apk`.
