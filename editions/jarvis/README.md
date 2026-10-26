# HUDWall Avengers

Windows desktop HUD with separate JARVIS and SHIELD displays, a desktop organizer, optional replacement taskbar, local voice/conversation support, and God's Eye View embedded in SHIELD.

## Build on Windows x64

Requirements: .NET 10 SDK, Git, and internet for dependency restore.

```powershell
.\Initialize-GodsEye.ps1
dotnet publish .\source\HudWall.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\HUDWall
Copy-Item .\source\dist\* .\artifacts\HUDWall -Recurse -Force
Copy-Item .\gods-eye .\artifacts\HUDWall\gods-eye -Recurse -Force
```

Start `artifacts/HUDWall/Start Wallpaper.cmd`. The launcher registers Windows startup. Use the tray menu for themes, monitor roles, voice and the optional taskbar. Restore the Windows taskbar with **Ctrl+Alt+Shift+T**.

The repository has clean defaults instead of personal monitor settings, app pins, weather location, API keys or models. Neural models live outside the repository under `%LocalAppData%\HudWall\Models`. The optional local intro video is omitted and movie intro is disabled by default.

## God's Eye

`Initialize-GodsEye.ps1` downloads pinned upstream source and a checksum-verified portable Node runtime, applies the trusted-parent rendering extension and builds the browser bundle. Generated `gods-eye/` files are ignored by Git.

Open **Tray → SHIELD / God's Eye → Open interactive console** to navigate, search and change layers. The wallpaper is click-through. The Focus submenu switches between United States and World.

Keyless imagery/public feeds need internet. Optional providers require their own configuration. The local server binds only to `127.0.0.1` and exits with HUDWall. Rendering follows HUD pause; background data refreshes may continue.

## Release and credits

After initialization, `source/Package.ps1` creates a self-contained release ZIP. See [CHANGELOG.md](CHANGELOG.md) and guides under `source/dist/`.

[God's Eye View](https://github.com/bilawalsidhu/gods-eye-view) is by Bilawal Sidhu, MIT licensed; its license is retained in `integration/GODS-EYE-LICENSE.txt`. The generated checkout retains upstream provider/dependency notices. Natural Earth geography is public domain. Other third-party notices are in `source/dist/`.

This repository does not assign a new license to third-party assets or dependencies.
