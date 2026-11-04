# Switch Audio

A lightweight Windows tray app that toggles your default **audio output** between two favorite devices with a global hotkey.

Built with C# and WinForms — no installers, no NuGet dependencies.

## What it does

1. Runs in the **system tray** (near the clock)
2. You pick **Device A** and **Device B** from the right-click menu
3. Press **Ctrl + Alt + S** (customizable) to toggle between them
4. Shows a small balloon notification with the active device name

## Download & run (Windows)

1. Push this repo to GitHub (or open a PR) — CI builds `SwitchAudio.exe` automatically
2. Go to **Actions** → latest successful run → **Artifacts** → download `SwitchAudio-win-x64`
3. Copy `SwitchAudio.exe` to your Windows PC and double-click it
4. Right-click the tray icon → set Device A and Device B → use the hotkey

Settings are saved to:

`%AppData%\SwitchAudio\settings.json`

## Develop on Mac, test on Windows

This is a Windows-only app (WinForms + Windows audio APIs). On a Mac you can edit code and push to GitHub; the **Build** workflow compiles on `windows-latest` and uploads the `.exe` as an artifact.

You cannot run or fully test the app on macOS.

### Project layout

```
src/
├── Program.cs                 # App entry point
├── Audio/
│   ├── CoreAudioApi.cs        # Windows COM interface declarations
│   └── AudioManager.cs        # List devices, switch default
├── Input/
│   └── HotkeyManager.cs       # Register global hotkey (user32.dll)
├── Settings/
│   └── AppSettings.cs         # Load/save JSON settings
└── UI/
    ├── HotkeyWindow.cs        # Hidden window for hotkey messages
    ├── HotkeyPickerForm.cs    # Dialog to pick a new hotkey
    └── TrayApplicationContext.cs  # Tray icon + menus
```

### Build locally on Windows

```bash
dotnet publish SwitchAudio.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Output: `publish/SwitchAudio.exe`

## How it works (quick tour)

| Piece | Role |
|-------|------|
| `CoreAudioApi.cs` | Declares Windows COM interfaces for audio devices |
| `AudioManager.cs` | Lists playback devices and sets the default via `IPolicyConfig` |
| `HotkeyManager.cs` | Calls `RegisterHotKey` so the shortcut works system-wide |
| `HotkeyWindow.cs` | Invisible form that receives `WM_HOTKEY` messages |
| `TrayApplicationContext.cs` | Wires everything together — tray menu, switch logic, notifications |

## License

MIT License
