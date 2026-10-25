# HUD Live OLED

A Windows desktop wallpaper HUD with system telemetry, audio visualization,
themes, per-monitor controls, transparency, and OLED care settings.

## Build and run

Requirements: Windows x64, .NET 10 SDK, and Microsoft Edge WebView2 Runtime.

From the repository root:

```powershell
dotnet publish source\HudWall.csproj -c Release -r win-x64 --self-contained true -o publish
Copy-Item -Path *.cmd,*.ps1 -Destination publish
```

Run `publish\Start Wallpaper.cmd` to launch the wallpaper, or
`publish\Preview.cmd` for a preview. The launcher can register automatic Windows
startup after a successful launch. Use `Remove Startup.cmd` to disable it.

The repository includes source, assets, launcher scripts, and documentation.
Compiled applications, bundled .NET runtime files, and build output are ignored.
Machine-specific `assets/monitors.json` and monitor color overrides are also
ignored; shared theme and performance presets remain included.

User settings and logs are stored under `%LOCALAPPDATA%\HudWall`. OLED settings
use the `Settings\OLED` subfolder. Keep all published application files together.

See [README-v16.txt](README-v16.txt) for current features and validation limits,
[README.txt](README.txt) for earlier usage notes, and [CHANGELOG.md](CHANGELOG.md)
for release history. The historical test command in README-v16.txt refers to a
`tests` directory that is not included in this project folder.
