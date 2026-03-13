# VoiceType — Prompt para Claude Code

## Descripción
Aplicación de escritorio Windows ligera que transcribe voz a texto usando la API de OpenAI y pega el resultado directamente donde esté el cursor. Se manifiesta como un pequeño overlay flotante y un icono en el SysTray.

---

## Stack tecnológico

- **Lenguaje**: C# (.NET 8)
- **UI**: WPF (Windows Presentation Foundation)
- **SysTray**: `System.Windows.Forms.NotifyIcon` o `Hardcodet.NotifyIcon.Wpf`
- **Audio**: `NAudio` (captura de micrófono, max 10 min)
- **API**: OpenAI REST API (multipart/form-data para audio)
- **Config persistente**: JSON en `%APPDATA%\VoiceType\settings.json`
- **API Key**: Variable de entorno de usuario (`OPENAI_API_KEY`) — se guarda vía `Environment.SetEnvironmentVariable(..., EnvironmentVariableTarget.User)`
- **Build**: Publicar como `win-x64` self-contained single-file `.exe`

---

## Funcionalidad core

1. El usuario hace clic en el botón mic (o usa hotkey) → empieza a grabar
2. Graba hasta que vuelve a hacer clic (o hotkey) — **máximo 10 minutos**, con advertencia visual al llegar a 9 min
3. Al parar: envía el audio a la API de OpenAI (endpoint `/v1/audio/transcriptions`)
4. Pega el texto transcrito directamente donde estaba el cursor antes de que el overlay tomara foco
5. Feedback visual: el botón hace una animación de check verde (~1.5s) y vuelve a idle

---

## Overlay flotante

### Apariencia
- Ventana pequeña sin chrome (sin barra de título, sin bordes de sistema)
- Bordes redondeados, sombra suave
- Botón micrófono centrado con reborde blanco fino y sutil
- Barra de nivel de audio debajo del botón (animada mientras graba)
- Detecta automáticamente **modo claro / oscuro** de Windows (`AppsUseLightTheme` en registro)
- Icono de micrófono SVG de calidad, sin assets externos

### Posición y comportamiento
- **Posición inicial**: esquina inferior derecha, con margen de 24px respecto a bordes de pantalla
- **Arrastrable** con clic izquierdo en cualquier zona de la ventana
- La posición se **persiste** en settings.json al cerrar / arrastrar

### Menú clic derecho sobre el overlay
```
[ Fijar (siempre visible)    ]   ← toggle, marca ✓ cuando está activo
[ Ocultar                    ]
───────────────────────────────
[ Configuración...           ]
[ Salir                      ]
```

- **Fijar**: `Topmost = true`, la ventana siempre queda por delante de todo
- **Desfijado**: comportamiento de ventana flotante normal (puede quedar detrás)
- **Ocultar**: `Visibility = Hidden`, solo recuperable desde SysTray

---

## SysTray

- Icono representativo (micrófono)
- **Tooltip**: "VoiceType — Listo" / "VoiceType — Grabando…"
- Doble clic en el icono: mostrar/ocultar overlay
- Menú clic derecho:
```
[ Mostrar / Ocultar          ]
───────────────────────────────
[ Configuración...           ]
[ Salir                      ]
```

---

## Panel de Configuración

Ventana modal accesible desde overlay (clic derecho) y systray. Secciones:

### 🔑 API Key
- Campo de texto (tipo password) para introducir la key
- Botón "Guardar" → llama a `Environment.SetEnvironmentVariable("OPENAI_API_KEY", value, User)`
- Muestra si hay una key guardada actualmente (ej. `sk-…ab3f`)

### 🎙️ Modelo de transcripción
- Desplegable que carga los modelos disponibles desde la API de OpenAI en el momento de abrir config
  - Endpoint: `GET /v1/models`, filtrar los que contengan `transcri` o sean de audio
  - Si la llamada falla, mostrar lista hardcoded de fallback
- **Modelo por defecto**: `gpt-4o-mini-transcribe` (marcado como "⭐ Recomendado")
- El usuario puede cambiar el modelo; se persiste en settings.json

### ⌨️ Hotkey global
- Campo de captura de tecla: el usuario pulsa la combinación deseada y se registra
- **Default sugerido**: `Ctrl + Shift + Space`
- Se registra como hotkey global con `RegisterHotKey` (Win32 API)

### 📋 Portapapeles
- Checkbox: "Copiar también al portapapeles como fallback"
- **Por defecto: desactivado**
- Si está activo, además del pegado directo, copia el texto al portapapeles

### 🚀 Auto-arranque con Windows
- Checkbox: "Iniciar VoiceType con Windows"
- **Por defecto: desactivado**
- Implementar escribiendo/borrando en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

---

## Lógica de pegado (foco)

El mayor reto es recuperar el foco correcto. Implementar así:

1. Cuando el usuario pulsa el botón mic **o la hotkey**, capturar `GetForegroundWindow()` inmediatamente (antes de que el overlay tome foco)
2. Al finalizar la transcripción:
   - Llamar a `SetForegroundWindow(hwndCapturado)`
   - Esperar 150ms
   - Usar `SendInput` para simular `Ctrl+V` — **sin pasar por el portapapeles** usando `keybd_event`
   - Si la opción de portapapeles está activa: `Clipboard.SetText(texto)` + `SendInput Ctrl+V`
   - Si no: usar `SendInput` enviando los caracteres uno a uno como `WM_KEYDOWN` / `SendKeys` como fallback

> Nota para Claude Code: investigar si `InputSimulator` (NuGet) simplifica el SendInput sin portapapeles. Si hay problemas con ventanas UWP/WinUI, añadir modo fallback portapapeles automático.

---

## Estados del botón micrófono

| Estado | Apariencia |
|--------|-----------|
| Idle | Micrófono, color accent del tema |
| Grabando | Micrófono rojo, pulso/blink suave, barra de audio animada |
| Procesando | Spinner / icono de carga |
| Éxito | Check verde, animación fade ~1.5s, vuelve a Idle |
| Error | Icono de advertencia ⚠, tooltip con el error |

---

## Archivos del proyecto

```
VoiceType/
├── VoiceType.csproj
├── App.xaml / App.xaml.cs          ← entry point, SysTray init
├── MainWindow.xaml / .cs           ← overlay flotante
├── ConfigWindow.xaml / .cs         ← panel de configuración
├── Services/
│   ├── AudioRecorderService.cs     ← NAudio, captura mic
│   ├── TranscriptionService.cs     ← llamada a OpenAI API
│   ├── HotkeyService.cs            ← RegisterHotKey Win32
│   └── SettingsService.cs          ← leer/escribir settings.json
├── Models/
│   └── AppSettings.cs              ← modelo de configuración
└── Resources/
    └── mic-icon.ico                ← icono para SysTray y exe
```

---

## Settings.json (esquema)

```json
{
  "model": "gpt-4o-mini-transcribe",
  "hotkey": "Ctrl+Shift+Space",
  "useClipboardFallback": false,
  "autoStart": false,
  "alwaysOnTop": false,
  "windowPosition": { "x": 0, "y": 0 },
  "theme": "auto"
}
```

---

## Build y distribución

```xml
<!-- En .csproj -->
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
```

Comando de build:
```
dotnet publish -c Release -r win-x64 --self-contained true
```

Resultado: un único `VoiceType.exe` sin dependencias externas.

---

## Dependencias NuGet

```
Hardcodet.NotifyIcon.Wpf
NAudio
Microsoft.Extensions.Http   ← para HttpClient tipado
System.Text.Json
```

---

## Notas adicionales para Claude Code

- No usar portapapeles en el flujo principal de pegado — resolver con `SendInput` directo
- El overlay **no aparece en la barra de tareas** (`ShowInTaskbar = false`)
- Al cerrar la ventana overlay (si el usuario pulsa Alt+F4), minimizar al systray en lugar de cerrar la app
- Manejar el caso donde no hay API key configurada: mostrar el panel de config automáticamente al primer arranque
- Añadir timeout de 30s a la llamada de transcripción con mensaje de error claro
- El audio se graba en memoria (MemoryStream), no en disco
