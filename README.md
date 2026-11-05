# Switch Audio

A lightweight Windows tray app that toggles your default **audio output** between two favorite devices with a global hotkey.

Built with C# and WinForms — no installers, no NuGet dependencies.

## What it does

1. Runs in the **system tray** (near the clock)
2. You pick **Device A** and **Device B** from the right-click menu
3. Press **Ctrl + Alt + S** (customizable) to toggle between them
4. Shows a small balloon notification with the active device name

## Download & run (Windows)

1. Open the [Releases](https://github.com/nilvaes/switch-audio/releases) page and download the latest `SwitchAudio-vX.Y.Z-win-x64.exe`
2. Double-click it (no install needed)
3. Right-click the tray icon → set Device A and Device B → use the hotkey

Settings are saved to:

`%AppData%\SwitchAudio\settings.json`

## Builds and releases

The **Build** workflow (`.github/workflows/build.yml`) runs on GitHub's Windows machines:

| You do | What happens |
|--------|--------------|
| Push to `main`, or open a pull request | Builds the exe and attaches it to the run as an artifact (**Actions** → run → **Artifacts** → `SwitchAudio-win-x64`). No release is created. |
| Push a tag starting with `v` (e.g. `v0.4.0`) | Builds the exe and publishes a **GitHub Release** named after the tag, with auto-generated release notes. |

So pushing commits never changes the Releases page — only pushing a tag does.

### Releasing a new version

1. Bump `<Version>` in `SwitchAudio.csproj` (e.g. `0.4.0`), commit, and push to `main`
2. Tag that commit and push the tag:

   ```bash
   git tag v0.4.0
   git push origin v0.4.0
   ```

3. Wait for the **Build** run for the tag to finish, then check the Releases page

Use a new tag name for each release, and keep it matching the `<Version>` in the csproj.

## Development

This is a Windows-only app (WinForms + Windows audio APIs). You can edit it on any OS and let the **Build** workflow compile it, but running it needs Windows.

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
│   ├── AppSettings.cs         # Load/save JSON settings
│   └── StartupManager.cs      # "Start with Windows" registry entry
└── UI/
    ├── AppIcon.cs             # Loads the embedded tray icon
    ├── DevicePickerForm.cs    # Dialog to pick Device A and B
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
