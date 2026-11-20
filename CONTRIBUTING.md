# Contributing

Thanks for your interest in AetherWall. Bug reports, feature suggestions, and focused pull requests are welcome.

## Before opening an issue

- Search existing issues for the same problem or request.
- For a bug, include your Windows version, steps to reproduce it, and any relevant error message.
- Do not include passwords, access tokens, private wallpaper files, or other personal data in an issue.

## Build locally

1. Install the .NET 10 SDK or Visual Studio with the .NET desktop development workload.
2. Open `AetherWall.slnx` or run:

   ```powershell
   dotnet restore AetherWall.slnx
   dotnet build AetherWall.slnx -c Release
   ```

3. Run the app on Windows. Wallpaper rendering and monitor integration depend on Windows APIs and LibVLC.

## Pull requests

- Keep each change focused and describe the user-facing behavior it changes.
- Include the steps you used to verify the change.
- Update documentation when setup or app behavior changes.
- Do not commit build output, local Visual Studio settings, or wallpaper media files.
