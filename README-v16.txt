HUD LIVE OLED v16

Start Wallpaper.cmd launches the wallpaper. Right-click the HUD Live tray
icon and choose Settings... to open the new settings window. Existing tray
shortcuts remain available. Change Colors.cmd opens this window, including
when the wallpaper is already running. Themes have a sample preview; Apply updates the
wallpaper. Custom colors are saved by their own Save and apply button.

STARTUP AND RECOVERY
The launcher now waits for successful HUD initialization before attaching
to the desktop or registering Windows startup. A missing WebView2 Runtime
produces an actionable error with an optional link to Microsoft's download
page. Page failures and renderer/browser crashes get up to three recovery
attempts, with the retry budget renewed after five minutes of stable use.
Desktop reattachment failures are logged and retried.

The Windows startup checkbox now works. Disabling it persists across later
launches. Remove Startup.cmd also persists the opt-out. Enabling it clears
that preference. Only one edition is registered for Windows startup.

SETTINGS
OLED preferences migrate once from assets into:
  %LOCALAPPDATA%\HudWall\Settings\OLED
JARVIS preferences use a separate JARVIS directory. Existing assets are
retained. Settings writes use atomic file replacement with .bak backups;
missing or syntactically corrupt primary JSON can fall back to its backup.
Settings and logs can be opened from Settings > Startup & support.

PERFORMANCE
Existing all-monitor pause behavior is retained. Monitor palettes and audio
sensitivity are cached between explicit settings updates to avoid repeated
file reads. Current FPS, animation and monitor preferences are preserved.
Short whole-process-tree samples do not establish a general CPU improvement.
Use lower FPS or reduced animations if desired.

BUILD
Windows x64, .NET 10 SDK:
  dotnet publish source\HudWall.csproj -c Release -r win-x64 --self-contained true
Tests:
  dotnet run --project tests\Tests.csproj -c Release
The application still requires the Microsoft Edge WebView2 Runtime.

KNOWN VALIDATION LIMITS
Renderer and browser-process crashes, missing runtime, failed page boot,
settings save/migration/backup recovery, startup opt-out and normal desktop
attachment were exercised. An actual Windows sign-in, Explorer restart,
physical display reconnect and HDR/OLED panel behavior were not exercised.
