# AetherWall

![AetherWall app icon](AetherWall/Assets/aetherwall-icon.png)

AetherWall is a Windows desktop app for managing animated video wallpapers across one or more monitors.

## Features

- Import and organize video wallpapers in a searchable, sortable library.
- Save favorites and apply different wallpapers to individual displays.
- Choose **Fit**, **Fill**, or **Stretch** per display.
- Control playback, wallpaper audio, and volume.
- Optionally pause playback when a fullscreen app is active or when the PC is on battery power.
- Choose dark or light appearance and start AetherWall when Windows signs in.
- Use the system tray controls to reach common playback actions.

Supported video formats: MP4, MKV, WebM, AVI, MOV, WMV, FLV, M4V, MPEG, MPG, and TS.

## Requirements

- Windows with the .NET 10 Desktop Runtime.
- For building from source: the .NET 10 SDK or Visual Studio with the .NET desktop development workload.

The application uses WPF, LibVLCSharp, and the Windows LibVLC runtime.

## Build and run

```powershell
git clone https://github.com/naveenkr82/AetherWall.git
cd AetherWall
dotnet restore AetherWall.slnx
dotnet build AetherWall.slnx -c Release
dotnet run --project AetherWall/AetherWall.csproj
```

In Visual Studio, open `AetherWall.slnx`, select the AetherWall project, and run it for Windows.

## Wallpaper and settings locations

- Imported wallpapers: `%LOCALAPPDATA%\AetherWall\Wallpapers`
- App settings: `%LOCALAPPDATA%\AetherWall\settings.json`
- Generated thumbnails: `%LOCALAPPDATA%\AetherWall\Thumbnails`

## Project layout

| Folder | Contents |
| --- | --- |
| `AetherWall/Views` | WPF pages and dialogs |
| `AetherWall/ViewModels` | UI state and commands |
| `AetherWall/Services` | Playback, library, metadata, and settings services |
| `AetherWall/Native` | Windows monitor and desktop integration |
| `AetherWall/Themes` | Dark and light theme resources |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the local build and pull request guidelines.

## Security

See [SECURITY.md](SECURITY.md) for reporting security concerns.

## License

No license has been added yet. A license can be added once the project owner chooses the terms for reuse.
