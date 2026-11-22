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

- Windows 10 or later, 64-bit.
- The installer includes the .NET runtime; users do not need to install it separately.
- For building from source: the .NET 10 SDK or Visual Studio with the .NET desktop development workload.

The application uses WPF, LibVLCSharp, and the Windows LibVLC runtime.

## Build and run

```powershell
git clone https://github.com/NaveenSharma-Dev/AetherWall.git
cd AetherWall
dotnet restore AetherWall.slnx
dotnet build AetherWall.slnx -c Release
dotnet run --project AetherWall/AetherWall.csproj
```

In Visual Studio, open `AetherWall.slnx`, select the AetherWall project, and run it for Windows.

## Download and install

Download [AetherWall-Setup.exe](https://github.com/NaveenSharma-Dev/AetherWall/releases/latest/download/AetherWall-Setup.exe) from the latest GitHub Release. Run the installer, choose an installation folder, and optionally create a desktop shortcut. The installer adds a Start menu shortcut and an uninstall entry for the current Windows user. Uninstalling keeps imported wallpapers and settings.

## Build the installer

On Windows with the .NET 10 SDK installed, run:

```powershell
.\installer\build-installer.ps1
```

The self-contained x64 installer is created at `artifacts/setup/AetherWall-Setup.exe`. Build outputs are kept out of Git.

## Publish a release

Push a version tag after the release changes are on `main`. GitHub Actions builds the self-contained installer on Windows and publishes it with generated release notes:

```powershell
git switch main
git pull
git tag v2.1.0
git push origin v2.1.0
```

Use the next version number for each release. Tags may include a prerelease suffix, such as `v2.1.0-rc.1`.

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
| `installer` | Setup app, uninstaller, and installer build script |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the local build and pull request guidelines.

- [Report a bug](https://github.com/NaveenSharma-Dev/AetherWall/issues/new?template=bug_report.yml)
- [Request a feature](https://github.com/NaveenSharma-Dev/AetherWall/issues/new?template=feature_request.yml)

## Security

See [SECURITY.md](SECURITY.md) for reporting security concerns.

## License

This project is available under the [AetherWall Community License 1.0](LICENSE).
You may use, modify, and incorporate its code into other applications, including
commercial applications. You may not sell AetherWall itself as a standalone
product, including a substantially unchanged or rebranded copy. See the license
for the full terms and the treatment of third-party components.

This custom license restricts standalone resale, so the project is source
available and is not an OSI-approved open source project.
