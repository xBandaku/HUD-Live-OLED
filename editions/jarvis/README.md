# HUDWall Avengers

Windows desktop HUD with separate JARVIS and SHIELD displays, a desktop organizer, optional replacement taskbar, local voice/conversation support, and God's Eye View embedded in SHIELD.

## Download and update

Download the JARVIS Windows x64 ZIP and its SHA-256 checksum from the [41.0.1 release](https://github.com/xBandaku/HUD-Live-OLED/releases/tag/jarvis-v41.0.1). This edition uses its own version sequence; it is separate from OLED 16.1.0.

Extract the whole ZIP before launching `Start Wallpaper.cmd`. Windows x64 and the Microsoft Edge WebView2 Runtime are required; the .NET runtime is bundled. Local AI and neural voice may require their optional setup/model downloads. God's Eye public feeds require internet access.

For an update, exit HUD Live from its tray menu and back up the existing `assets` folder. Extract into a **new folder**, then copy your saved JSON settings and `monitor-colors` folder into the new `assets` folder; keep the newly shipped HTML, JavaScript, and other application assets. Do not overwrite your old installation with clean default settings. Keep the old folder until the new version starts successfully. Start the new copy once to update its automatic-startup path.

Organizer configuration remains under `%USERPROFILE%\.HudWall\Organizer`; your original files stay in their existing locations. Neural models remain under `%LOCALAPPDATA%\HudWall\Models`. Preserve those directories. The encrypted OpenAI key, if configured, remains outside the installation folder. Do not share your settings backups or key file.

### What's fixed in 41.0.1

- Audio follows default playback-device changes within approximately 1.5 seconds and recovers after capture failure.
- The launcher continues retrying attachment every three seconds if Explorer or the wallpaper window is late becoming available.
- Headless feed shutdown closes its active listener, including a fallback port.
- Windows CI now tests JARVIS recovery and builds the full JARVIS project. Physical device switching and actual Explorer restart recovery still need manual validation.

See the [patch notes](CHANGELOG.md) for details. Use **Ctrl+Alt+Shift+T** to restore the Windows taskbar if needed; startup and globe troubleshooting are also described in the packaged guides.

## Build on Windows x64

Requirements: .NET 10 SDK, Git, and internet for dependency restore.

```powershell
.\Initialize-GodsEye.ps1
dotnet publish .\source\HudWall.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\HUDWall
Copy-Item .\source\dist\* .\artifacts\HUDWall -Recurse -Force
Copy-Item .\gods-eye .\artifacts\HUDWall\gods-eye -Recurse -Force
```

Start `artifacts/HUDWall/Start Wallpaper.cmd`. The launcher registers Windows startup. Use the tray menu for themes, monitor roles, voice and the optional taskbar. Restore the Windows taskbar with **Ctrl+Alt+Shift+T**.

If Explorer is not ready at startup, the launcher continues retrying desktop attachment every three seconds until the host exits. Audio capture follows changes to the default multimedia playback device within approximately 1.5 seconds.

## HUD taskbar persistence (next build)

Enable **Tray → HUD taskbar → Replace Windows taskbar**. Each monitor keeps its own JARVIS/SHIELD styling. The bars recover after desktop switches, display changes, resume, or Explorer changes without activating themselves. They remain above ordinary apps, including maximized windows, and hide on monitors covered by a fullscreen game or video. Returning from fullscreen shows them again.

Virtual-desktop recovery checks Windows' supported desktop-membership API approximately once a second and recreates bars on the active desktop when needed. It does not switch your desktop or move application windows. If the API is temporarily unavailable, desktop following retries without disabling the taskbar. Live virtual-desktop switching, Explorer restart, and fullscreen transitions still require manual validation on the installed build.

The Windows tray button still returns to native taskbars for 25 seconds. **Ctrl+Alt+Shift+T** remains the recovery shortcut to restore Windows taskbars and disable HUD replacement. During transient update failures, native taskbars become available while HUD recovery retries.

The visible bar is anchored to the physical bottom edge, including mixed-DPI and negative-origin monitors, even while Windows still reports its old taskbar reservation. Fullscreen video on another monitor stays protected when you focus an app elsewhere; a normal app covering that video on the same monitor brings the taskbar back. An intentionally empty pin list now survives restart/re-enable. Existing saved pin arrays are preserved; only fresh settings import Windows pins or fallback shortcuts.

Edited audio settings with invalid JSON types no longer stop startup: the loader tries a valid backup and otherwise uses 100% sensitivity. Supported numeric values remain clamped to 0–300%.

## Recovery tests

From the repository root, run:

```powershell
dotnet run --project editions/jarvis/tests/Recovery.Tests.csproj -c Release
./editions/jarvis/tests/Launcher.Tests.ps1
```

The tests compile the JARVIS audio/feed sources, simulate audio sessions and native attachment calls, and verify real loopback socket cleanup without changing your audio devices or desktop windows. Windows CI also builds the full JARVIS project. Live device switching and Explorer recovery still need manual validation.

The repository has clean defaults instead of personal monitor settings, app pins, weather location, API keys or models. Neural models live outside the repository under `%LocalAppData%\HudWall\Models`. The optional local intro video is omitted and movie intro is disabled by default.

## God's Eye

`Initialize-GodsEye.ps1` downloads pinned upstream source and a checksum-verified portable Node runtime, applies the trusted-parent rendering extension and builds the browser bundle. Generated `gods-eye/` files are ignored by Git.

Open **Tray → SHIELD / God's Eye → Open interactive console** to navigate, search and change layers. The wallpaper is click-through. The Focus submenu switches between United States and World.

Keyless imagery/public feeds need internet. Optional providers require their own configuration. The local server binds only to `127.0.0.1` and exits with HUDWall. Rendering follows HUD pause; background data refreshes may continue.

## Release and credits

After initialization, `source/Package.ps1` creates a self-contained release ZIP. See [CHANGELOG.md](CHANGELOG.md) and guides under `source/dist/`.

[God's Eye View](https://github.com/bilawalsidhu/gods-eye-view) is by Bilawal Sidhu, MIT licensed; its license is retained in `integration/GODS-EYE-LICENSE.txt`. The generated checkout retains upstream provider/dependency notices. Natural Earth geography is public domain. Other third-party notices are in `source/dist/`.

This repository does not assign a new license to third-party assets or dependencies.
