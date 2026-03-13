# Win AI Recorder

> Dictate anywhere on Windows. Press a hotkey, speak, and your words appear instantly in any app — powered by OpenAI Whisper.

**Version 1.0** · Free & open source · Windows 10/11 x64

---

## What is it?

Win AI Recorder is a lightweight floating overlay that lives in your system tray. Press a global hotkey, speak naturally, and the transcribed text is typed directly into whatever app you were using — your code editor, a chat window, a Word document, anywhere.

No switching windows. No copy-pasting. It just works in the background.

```
You press Ctrl+Shift+Space
→ Mic opens (you see a waveform)
→ You speak
→ Press hotkey again (or click the button)
→ Text appears where your cursor was
```

---

## What it does

- **Transcribes speech to text** using OpenAI Whisper (state-of-the-art accuracy, supports most languages)
- **Types the text directly** into the focused app via keyboard simulation — nothing goes to the clipboard by default
- **Stays out of your way** — tiny floating overlay, no taskbar entry, no splash screen
- **Global hotkey** works even when the app is hidden in the system tray
- **Always on top** — overlay stays above all windows; a periodic refresh timer re-forces it in case Windows revokes it
- **Dark theme** — fixed dark UI
- **Remembers position** across restarts
- **Visual feedback** — animated waveform while recording, spinner while processing

## What it does NOT do

- It is **not a voice command system** — it transcribes speech, it does not execute commands
- It does **not store or send your audio** anywhere other than the OpenAI API for transcription
- It does **not require a subscription** — you pay OpenAI directly per use (Whisper is extremely cheap)
- It does **not require the app to be visible** — hotkey works from the system tray
- It does **not modify your clipboard** by default (optional fallback mode available)

---

## Requirements

| | |
|---|---|
| **OS** | Windows 10 or 11 (64-bit) |
| **API key** | OpenAI account with API access — [platform.openai.com](https://platform.openai.com) |
| **Microphone** | Any microphone, including built-in laptop mic |
| **.NET 8 SDK** | Only needed to build from source — not required to run the published EXE |

> **Cost:** Whisper via API costs roughly $0.006 per minute of audio. A typical 30-second dictation costs less than $0.001.

---

## Getting started

### 1. Get an OpenAI API key

Go to [platform.openai.com/api-keys](https://platform.openai.com/api-keys) and create a key. You'll need a small credit balance ($5 will last a very long time at Whisper prices).

### 2. Run the app

Download `WinAiRecorder.exe` from the [Releases](../../releases) page and run it — no installation required.

### 3. Configure your key

On first launch, the overlay appears in the bottom-right corner. Right-click it → **Settings…**, paste your API key and click **✓ Validar** to test it, then **Guardar**.

### 4. Start dictating

Press `Ctrl+Shift+Space` (default hotkey), speak, press again to stop. Done.

---

## Configuration

All settings are in the **Settings** window (right-click overlay → Settings…).

| Setting | Default | Description |
|---|---|---|
| **API Key** | — | Your OpenAI API key. Stored as a Windows user environment variable, never in a file. |
| **Model** | `gpt-4o-mini-transcribe` | Transcription model. `gpt-4o-mini-transcribe` is the best balance of speed and cost. Use `gpt-4o-transcribe` for maximum accuracy. |
| **Hotkey** | `Ctrl+Shift+Space` | Global hotkey to start/stop recording. Click the field and press your combo to change it. |
| **Clipboard fallback** | Off | If enabled, also puts the text in the clipboard and sends Ctrl+V. Useful for apps that block keyboard simulation (some games, certain terminals). |
| **Refresh (min)** | `5` | How often (in minutes) the overlay re-asserts its Always on Top state, in case Windows silently removes it. |

### Where settings are stored

```
%AppData%\WinAiRecorder\settings.json
```

The API key is stored separately as a Windows user environment variable (`OPENAI_API_KEY`) — it never touches the settings file.

---

## Overlay states

| What you see | Meaning |
|---|---|
| Blue mic | Ready to record |
| Red mic + animated waveform | Recording — waveform amplitude reflects your voice level |
| Spinning arc | Sending audio to the API and waiting for the response |
| Green checkmark (fades out) | Success — text has been typed |
| Orange warning triangle | Error — hover for details, auto-clears after 5 seconds |

---

## Tips

- **Hotkey works from the tray** — you don't need the overlay visible to use it
- **Move the overlay** by dragging it anywhere on screen; position is remembered
- **Hide to tray** via right-click → Hide, or the tray icon context menu
- **Cancel mid-transcription** by pressing the hotkey again while the spinner is showing
- **Max recording time** is 10 minutes; the overlay flashes at 9 minutes as a warning

---

## Building from source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows (WPF apps can only be built on Windows)
- An OpenAI API key for testing

### Build (debug)

```powershell
git clone https://github.com/gpalacios82/WinAIrecorder.git
cd WinAIrecorder\WinAiRecorder
dotnet build
```

Run directly:

```powershell
dotnet run
```

### Publish self-contained EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\publish
```

Output: `publish\WinAiRecorder.exe` (~155 MB, no .NET required on target machine)

### Project structure

```
WinAiRecorder/
├── App.xaml / App.xaml.cs          — startup, tray icon, theme
├── MainWindow.xaml / .cs           — floating overlay + waveform
├── ConfigWindow.xaml / .cs         — settings dialog
├── Models/AppSettings.cs           — settings model (JSON)
├── Services/
│   ├── AudioRecorderService.cs     — microphone capture (NAudio)
│   ├── TranscriptionService.cs     — OpenAI Whisper API client
│   ├── HotkeyService.cs            — global hotkey (Win32)
│   ├── PasteService.cs             — text injection (SendInput)
│   └── SettingsService.cs          — load/save settings
└── Helpers/
    ├── ThemeHelper.cs              — Windows theme detection
    ├── IconHelper.cs               — tray icon (GDI+)
    └── NativeMethods.cs            — Win32 P/Invoke
```

### Dependencies

| Package | Version | Purpose |
|---|---|---|
| `NAudio` | 2.2.1 | Microphone capture, WAV encoding |
| `Hardcodet.NotifyIcon.Wpf` | 2.0.1 | System tray icon |
| `System.Text.Json` | 8.0.5 | JSON serialization |
| `System.Drawing.Common` | 8.0.0 | Tray icon generation |

---

## Known issues

| Issue | Workaround |
|---|---|
| **Text goes nowhere when overlay was hidden in tray** | If focus is lost, use the hotkey (not the button) to start recording — it restores focus to the target app automatically. |

Focus/activation edge cases are caused by Windows restrictions on which processes can change the foreground window. The Refresh timer mitigates the Always on Top issue automatically.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| Settings open instead of recording | No API key set — configure it in Settings first |
| "Failed to start recording" | Check microphone permissions: Windows Settings → Privacy → Microphone |
| Key shows as invalid | Make sure you have a credit balance on your OpenAI account |
| Hotkey conflict | Another app is using the same combo — change it in Settings |
| Text typed into wrong app | Use the hotkey instead of the button; it preserves focus in the target window |
| Overlay not visible | Double-click the tray icon to show it |
| App already running message | Only one instance is allowed; check the system tray |

---

## Privacy

- Audio is recorded locally and sent directly to the OpenAI API over HTTPS for transcription. It is not stored locally after the request completes.
- No analytics, no telemetry, no accounts.
- Your API key is stored as a Windows user environment variable on your own machine.
- Source code is fully auditable above.

---

## License

MIT — free to use, modify and distribute.

---

## Contributing

Pull requests welcome. If you find a bug or have a feature idea, open an issue.
