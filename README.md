# HUD Live OLED

[![Windows build](https://github.com/xBandaku/HUD-Live-OLED/actions/workflows/windows.yml/badge.svg)](https://github.com/xBandaku/HUD-Live-OLED/actions/workflows/windows.yml)

A Windows desktop wallpaper HUD with system telemetry, audio visualization,
themes, per-monitor controls, transparency, and OLED care settings.

[Report a bug](https://github.com/xBandaku/HUD-Live-OLED/issues/new/choose) ·
[Ask a question](https://github.com/xBandaku/HUD-Live-OLED/discussions) ·
[Contributing](CONTRIBUTING.md) · [Security](SECURITY.md)

## Profiles

Open **Settings → Profiles** to load Gaming, Work, Minimal, or Night, then click
**Apply** to update the wallpaper. You can also switch immediately using
**Profiles** in the tray menu.

To create a profile, adjust the settings, enter a name on the Profiles tab, and
click **Save current settings as profile**. Saving captures unapplied edits too.
Using an existing name replaces that profile. Profiles include theme and custom
colors, clock and temperature units, audio sensitivity, performance preferences,
and per-monitor themes, brightness, transparency, and OLED settings. Windows
startup and the temporary pause-all switch are separate.

Profiles are stored in `profiles.json` in the edition's settings folder, with a
backup. Per-monitor settings follow the saved Windows display IDs; a different
display uses its normal defaults until configured.

GPU telemetry currently uses NVIDIA's `nvidia-smi` for GPU 0. Queries have a
deadline and cannot overlap. Failed or stale readings show as unavailable rather
than retaining an old load value. AMD/Intel sensor support is not included yet.

## Build and run

Requirements: Windows x64, .NET 10 SDK, and Microsoft Edge WebView2 Runtime.

From the repository root:

```powershell
dotnet publish source\HudWall.csproj -c Release -r win-x64 --self-contained true -o publish
Copy-Item -Path *.cmd,*.ps1 -Destination publish
Copy-Item source\hud.ico publish
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
regression suite now included in this repository:

```powershell
dotnet run --project tests\Tests.csproj -c Release
```

## Release packages

Build a self-contained Windows ZIP and SHA-256 checksum locally:

```powershell
.\scripts\Build-Release.ps1
```

The files are written to `artifacts\HUD-Live-OLED-win-x64.zip` and its `.sha256`
companion. Extract the ZIP before running the launcher. WebView2 Runtime is
still required; the .NET runtime is bundled.

The **Windows build and release** GitHub Actions workflow runs regression tests
and builds a downloadable artifact on pushes to `main`, pull requests, and manual
runs. Publish a GitHub Release with a version tag such as `v16.1.0` to build that
tag and attach the ZIP and checksum automatically. Update the project version in
`source/HudWall.csproj` before tagging a new version.

Repository review rules, CI, dependency maintenance, and sponsorship setup are
documented in [GitHub project setup](docs/GITHUB_SETUP.md).


## Optional JARVIS / SHIELD edition

The independent [JARVIS / SHIELD edition](editions/jarvis/README.md) adds local neural voice and conversation, a desktop organizer, an optional native taskbar, and God's Eye View inside the SHIELD display. It keeps separate source and packaging under `editions/jarvis/`; the OLED application's source and release workflow are unchanged.

See its [build instructions](editions/jarvis/README.md) and [complete changelog](editions/jarvis/CHANGELOG.md). The repository contains clean defaults, not personal app pins, locations, API keys or model files. God's Eye source/runtime are initialized from a pinned upstream revision rather than committed as generated bundles.
