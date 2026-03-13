# Win AI Recorder

Dictation overlay for Windows. Press a global hotkey, speak, and the transcribed text gets typed into whatever app has focus — no copy-paste needed.

## How it works

```
Hotkey pressed → microphone opens → you speak → WAV sent to OpenAI Whisper → text typed into active window
```

The overlay is a small floating button that lives in a corner of your screen. It has no taskbar entry; access it via the system tray icon.

## Requirements

| Requirement | Version |
|---|---|
| Windows | 10 / 11 (x64) |
| .NET SDK | 8.0+ (build only) |
| OpenAI API key | Any account with audio transcription access |

## Build

```powershell
cd C:\devops\WinAIrecorder\WinAiRecorder
dotnet build
```

Build output goes to `bin\Debug\net8.0-windows\win-x64\`.

### Run directly

```powershell
dotnet run
```

### Publish as single self-contained EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\publish
```

Output: `publish\WinAiRecorder.exe`

> The published EXE (~155 MB) is fully self-contained — no .NET installation required on the target machine.

## Setup

### 1. Set your OpenAI API key

- Launch Win AI Recorder → right-click the overlay → **Settings…**
- Paste your key in the API Key field → click **✓ Validar** to test it → **Guardar**
- The key is stored as a user-level environment variable (`OPENAI_API_KEY`)

Or set it manually:

```powershell
[System.Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "sk-...", "User")
```

### 2. First run

On first launch without a key configured, recording is blocked. The Settings window opens automatically when you try to record.

## Usage

| Action | How |
|---|---|
| Start / stop recording | Default hotkey `Ctrl+Shift+Space`, or click the overlay button |
| Move overlay | Drag it anywhere |
| Show / hide overlay | Double-click tray icon, or right-click tray → **Show / Hide** |
| Change hotkey / model | Right-click overlay → **Settings…** |
| Pin on top | Right-click overlay → **Pin (Always on top)** |
| Exit | Right-click tray → **Exit** |

### Recording states

| Icon | Meaning |
|---|---|
| Blue mic | Idle — ready to record |
| Red mic (pulsing) + sine waveform | Recording — waveform amplitude reflects volume |
| Spinning arc | Processing (sending to API) |
| Green checkmark (fades) | Done — text typed |
| Orange triangle | Error — hover for details, auto-clears in 5 s |

### Transcription models

Configured in Settings. Defaults to `gpt-4o-mini-transcribe` (fastest/cheapest). Available models are fetched live from the API when a valid key is entered.

- `gpt-4o-mini-transcribe` ⭐ recommended
- `gpt-4o-transcribe`
- `whisper-1`

### Max recording duration

Hard limit: **10 minutes**. The overlay flashes at 9 minutes as a warning, then stops automatically.

## Configuration file

Settings are saved to:

```
%AppData%\WinAiRecorder\settings.json
```

| Field | Default | Description |
|---|---|---|
| `Model` | `gpt-4o-mini-transcribe` | Transcription model |
| `Hotkey` | `Ctrl+Shift+Space` | Global hotkey |
| `UseClipboardFallback` | `false` | Also copy to clipboard (for apps that block SendInput) |
| `AutoStart` | `no` | `no`, `yes`, or `minimized` (start hidden in system tray) |
| `AlwaysOnTop` | `true` | Overlay always on top |
| `Theme` | `auto` | `dark`, `light`, or `auto` (follows Windows setting) |

## Pasting behaviour

By default Win AI Recorder uses `SendInput` with Unicode key events — text appears exactly where the cursor is in any app without touching your clipboard.

If you enable **"Copiar al portapapeles como fallback"** in Settings, the app additionally puts the text in the clipboard and sends `Ctrl+V`, which works with apps that block `SendInput` (e.g. some games, certain terminals).

## Troubleshooting

| Problem | Fix |
|---|---|
| "Failed to start recording" | Check microphone permissions: Settings → Privacy → Microphone |
| Settings open instead of recording | No API key configured — add it in Settings |
| "Invalid API key" | Key is wrong or revoked — update in Settings |
| Hotkey already in use | Another app registered the same combo — change it in Settings |
| Text appears in wrong window | Use the hotkey instead of the button (keeps focus in target app) |
| Overlay not visible | Double-click tray icon to show it |
| Dropdown invisible in dark mode | Already fixed — update to latest build |

## Project structure

```
WinAiRecorder/
├── App.xaml / App.xaml.cs          — startup, tray icon, theme management
├── MainWindow.xaml / .cs           — floating overlay UI + waveform visualizer
├── ConfigWindow.xaml / .cs         — settings dialog
├── Models/
│   └── AppSettings.cs              — settings model
├── Services/
│   ├── AudioRecorderService.cs     — microphone capture (NAudio)
│   ├── TranscriptionService.cs     — OpenAI Whisper API client
│   ├── HotkeyService.cs            — global hotkey registration (Win32)
│   ├── PasteService.cs             — text injection (SendInput / clipboard)
│   └── SettingsService.cs          — settings load/save (JSON)
├── Helpers/
│   ├── ThemeHelper.cs              — Windows theme detection
│   ├── IconHelper.cs               — programmatic tray icon (GDI+)
│   └── NativeMethods.cs            — Win32 P/Invoke declarations
└── Resources/
    └── mic-icon.ico                — application icon
```

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `NAudio` | 2.2.1 | Microphone capture, WAV encoding |
| `Hardcodet.NotifyIcon.Wpf` | 2.0.1 | System tray icon |
| `System.Text.Json` | 8.0.5 | JSON serialization |
| `System.Drawing.Common` | 8.0.0 | Programmatic icon generation |
