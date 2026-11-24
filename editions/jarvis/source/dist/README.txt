NEW IN v41.0.2: PERSISTENT, BOTTOM-ALIGNED TASKBAR
HUD taskbars recover across virtual desktops, display/resume and Explorer changes.
The visible bar stays at the physical monitor bottom. Fullscreen video remains
protected when you focus another monitor. Empty pin lists survive restart, and
invalid audio-setting types recover a backup or safe default instead of stopping
startup. Actual shell/display behavior still needs validation on your setup.
Use tray > HUD taskbar > Replace Windows taskbar to enable it. After updating,
exit the old copy and start the new launcher; toggle replacement off/on if needed.
Ctrl+Alt+Shift+T restores Windows taskbars. See HUD TASKBAR.txt for details.

NEW IN v41.0.1: AUDIO AND STARTUP RECOVERY
Audio follows the default Windows multimedia playback device within about
1.5 seconds. If Explorer is not ready when you start the wallpaper, the launcher
keeps retrying every three seconds until HUD Live exits. Fallback feed ports
are released immediately when the feed is disposed.

UPDATING SAFELY
Exit HUD Live using its tray menu before replacing files. Back up your existing
assets folder and extract the new version into a separate folder. Copy your
saved JSON settings and monitor-colors folder into the new assets folder,
keeping the new index.html and other shipped web assets. Keep your old folder
until the new version starts successfully. Organizer files and neural models
remain outside the installation folder. Starting the new copy registers its
location for automatic Windows startup.

NEW IN v40: FULL-DESKTOP SIZE RECOVERY
The wallpaper now repairs accidental preview sizing and follows physical desktop bounds.

NEW IN v39: GOD'S EYE VIEW INSIDE SHIELD
Use tray > SHIELD / God's Eye for globe presets and the interactive console.
See GODS EYE.txt for keyless setup, voice shortcuts and source credits.

HUD LIVE - JARVIS LOCAL EDITION v41.0.2

NEW IN v37: AVENGERS AUDIT AND MODERN TASKBAR
The dock now uses borderless icon tiles and running-app indicators.
On mixed JARVIS/SHIELD displays, each interface has its own role to avoid
repeated conversation and hardware readouts. JARVIS uses the supplied logo
and optional muted intro; SHIELD gets a dotted holographic archive atlas.
HUD appearance controls the intro, map depth and cinematic refresh interval.

NEW IN v36: NATIVE HUD TASKBAR
JARVIS and SHIELD now have matching full taskbar replacements. Use HUD for settings,
SYS for Windows quick settings, TRAY for the native notification icons, and APPS
for all running windows. Ctrl+Alt+Shift+T restores the Windows taskbar immediately.
Read HUD TASKBAR.txt. The recovered organizer profile and files remain in place.

NEW IN v34: FRAME RECOVERY AND LAYOUT FIX
Recover existing organizer frames across app-container/launcher changes. Organizer
settings now live in %USERPROFILE%\.HudWall\Organizer; original files stay in
place. The command/ambient panel no longer covers CPU/frame/week/day readouts.
Read RECOVERY AND PROFILES.txt.

NEW IN v33: AVENGERS COMMAND CENTRE
Tray > Command centre selects Auto, Ambient, Command, or Showcase presentation.
Say "Jarvis command mode", "Jarvis ambient mode", "Jarvis showcase mode", or
"Jarvis prepare my workspace". Configure the app routines in the same tray menu.
Showcase is labelled simulated and does not control apps. Real requests restore Auto.
Read COMMAND CENTRE.txt. Model downloads are automatic if speech models are missing.

NEW IN v32: CINEMATIC MODE
J.A.R.V.I.S. and S.H.I.E.L.D. displays play a boot sequence at startup, JARVIS
greets you aloud with the time, weather and a system check, and the reactor (or
SHIELD's comms trace) moves with his voice. Say "Jarvis, power up" to replay it.
Read CINEMATIC MODE.txt.

NEW IN v31: DESKTOP ORGANIZER
Folder portals, shortcut fences, a tabbed launcher (Ctrl+Alt+Space) and desktop
auto-sort now run inside HUD Live, drawn in the HUD theme. Tray > Desktop organizer,
or say "Jarvis open launcher", "Jarvis sort desktop". Read DESKTOP ORGANIZER.txt.

LOCAL AI AND BRITISH NEURAL VOICE
Read LOCAL AI GUIDE.txt for the current setup and controls.
On a new PC, run Setup Local AI.cmd once, then Start Wallpaper.cmd.
This downloads Ollama, Qwen3 8B and the Kokoro speech model. Large model weights
are not included in this ZIP. They are already installed on the current PC.
The default local provider does not need an API key or OpenAI balance.
Tray > AI conversation > AI provider settings selects Ollama or optional OpenAI.
Tray > JARVIS voice > Response voice selects British neural George/Lewis/Daniel.
Tray > JARVIS voice > Speech speed adjusts the delivery.
Tray > AI conversation > Open conversation provides typed chat and full replies.
AI GUIDE.txt describes the optional OpenAI setup from v18, not the local default.

Extract the entire ZIP into a permanent folder. Run Start Wallpaper.cmd.
Windows x64 and the Microsoft Edge WebView2 Runtime are required.
Wallpaper Engine and an installed .NET runtime are not required.

JARVIS edition: full-screen, stationary interface. Reactor rings, lighting,
audio bars, clock, and live system telemetry continue updating normally.
OLED edition: full-screen interface with gentle drift within safe margins.
Monitors > display > OLED care controls motion, brightness, idle dimming,
and minute pixel shifting. Paused OLED HUDs disappear instead of freezing.
There is no reduced-size floating HUD in v15.

Both editions have 37 themes including JARVIS - Arc Reactor, larger labels,
a segmented reactor display, and real CPU/GPU/memory history graphs.
Themes and pause settings can be changed independently for each monitor.

JARVIS v17 VOICE AND MONITOR SPANNING
Voice listening starts using the Windows default microphone. VOICE shows ON,
OFF, SPEAK, or CHECK. Tray > JARVIS voice shows detailed status and controls
listening, spoken replies, response voice, app commands, and microphone settings.
Local voice commands and local AI conversation need no API key.
This app does not save microphone audio.
An English Windows speech recognizer and desktop microphone access are needed.

Say Jarvis before every command:
- Jarvis hello / help
- Jarvis what time is it / what is the date / system status
- Jarvis open notepad / calculator / file explorer / settings / browser
- Jarvis pause wallpaper / resume wallpaper
- Jarvis transparency on / transparency off
- Jarvis span monitors / separate monitors
- Jarvis stop listening (re-enable through the tray)

Configure app commands lets you choose an EXE or Start menu shortcut and assign
a unique spoken name. Known Discord, Steam, Chrome, Edge, and Spotify shortcuts
are detected when present. Command guide lists this computer's configured apps.
Recognition is ignored while JARVIS speaks and briefly afterward.
Preset commands remain available; other questions use AI when configured.

Span HUD across all monitors is enabled by default. One clock, reactor, and
telemetry layout fill the combined Windows desktop. This changes the wallpaper
layout, not Windows display mode or how other applications see the monitors.
The primary display supplies the span's theme and transparency preference.
Any display's pause rule pauses the shared HUD. Turn spanning off to restore
independent monitor layouts with their saved settings.

JARVIS v16 CONTROLS
Right-click the tray icon > HUD appearance:
- Hardware panel, History graphs, Audio spectrum, Reactor rings: show/hide.
- Transparent panel darkness: Light 70%, Balanced 88%, Solid 100%.
- Reset appearance restores these appearance defaults only.
Settings apply across all displays, save immediately, and survive restart.

The status strip shows download/upload Mbps and free storage by volume.
POWER shows AC or battery status and percentage when available.
Status labels flag CPU/memory >=90%, disk use >=90%, or battery <=20% on
battery power. Labels clear automatically; no external notification is sent.
Network rates summarize active non-loopback interfaces. They describe system
traffic, not an Internet speed test. Telemetry OFFLINE means the feed is stale.

TRANSPARENCY
Tray > Transparent background (Windows wallpaper) toggles all screens.
Monitors > display > Transparent background changes just that monitor.
Your wallpaper remains visible between the HUD elements. Dark panels behind
the text and clock improve readability. Transparency mode keeps OLED HUD
opacity at least 90% while active (72% when idle), even at a lower brightness.
OLED care does not protect static wallpapers, desktop icons, or the taskbar.
Burn-in prevention is not guaranteed; keep manufacturer panel care enabled.

STARTUP AND SWITCHING EDITIONS
Automatic Windows startup is registered after a successful wallpaper launch.
Only one edition runs at a time. Exit through the tray before starting the
other edition. Starting that edition updates startup to its own folder.
Keep both folders in permanent locations. Remove Startup.cmd removes startup.

OTHER CONTROLS
Tray settings include theme presets, custom colors, audio sensitivity,
Celsius/Fahrenheit, 12/24-hour time, FPS, decorative animations, fullscreen
pausing, selected-app pausing, and separate monitor settings.
The current release starts with the cyan JARVIS theme. Other saved time,
temperature, audio, transparency, and performance preferences are retained.
See CHANGELOG.md for the complete release history.
Natural AI conversation uses the selected provider; local Ollama is the default.
