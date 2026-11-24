## JARVIS / SHIELD v41.0.2 - Taskbar patch (October 26, 2026)

- Add taskbar persistence, physical bottom-edge alignment, per-monitor fullscreen handling, and retries after transient Explorer/display failures.
- Preserve intentional empty pin lists and recover semantically invalid audio settings using backup/default values.
- Extend automated coverage to 30 scenarios and document update steps, taskbar recovery, and live-validation limits. Earlier 41.0.1 fixes remain included; OLED remains 16.1.0.

## JARVIS / SHIELD v41.0.1 - Recovery patch (October 26, 2026)

- Fix default audio output switching, persistent launcher attachment retries, and fallback feed-port cleanup in the JARVIS edition.
- Add nine JARVIS recovery checks and a full JARVIS build to Windows CI; retain all 13 OLED regression checks.
- Document separate edition downloads, requirements, update/back-up steps, and validation limits. OLED remains at 16.1.0.

## JARVIS / SHIELD v41 - Source edition

- Add the v41 JARVIS / SHIELD source edition under `editions/jarvis/` without replacing the OLED application or its release scripts.
- Include voice/conversation, organizer, optional taskbar, full-desktop bounds recovery and United States / World God's Eye focus controls.
- Include clean defaults, custom icons, build guides, pinned upstream bootstrap/patch, and the complete edition history. Exclude personal settings, API keys, model weights and generated runtimes.

# HUD Live — Standalone Changelog

**Latest changes: October 25, 2026**

HUD Live now runs as an independent Windows live wallpaper, with tray controls, customizable themes, audio reactivity, performance settings, and separate settings for each monitor. Wallpaper Engine is no longer required.

## v16.1 - Profiles, telemetry reliability, and release builds

- Added Gaming, Work, Minimal, and Night starter profiles, named profile saving
  in Settings, and immediate profile switching from the tray. Profiles preserve
  custom palettes and per-monitor OLED settings without changing Windows startup.
- Bounded NVIDIA process execution and both output streams, prevented overlapping
  GPU polls, and cleared failed or stale readings and the corresponding load bar.
  Polls finishing after pause/resume or disposal cannot publish their old result.
- Serialized telemetry snapshots and disposed process enumeration handles.
- Added Windows regression tests, a self-contained ZIP packaging script with a
  SHA-256 checksum, and GitHub Actions builds with assets for published releases.

## v15 - JARVIS and OLED editions

- Split the release into two packages: JARVIS with a stationary full-screen layout and OLED with gentle HUD drift, brightness controls, idle dimming, pixel shifting, and pause-to-black behavior.
- JARVIS keeps animated reactor rings, lighting, live audio bars, the clock, and telemetry. The interface itself stays in place even if older saved settings enabled OLED movement.
- Rebuilt the interface with a JARVIS-inspired reactor display, segmented rings, cyan Arc Reactor theme, larger system labels, and real CPU/GPU/memory history graphs. There are now 37 themes.
- Kept the HUD full-screen during OLED movement. Replaced the reduced-size floating layout with small shifts inside safe margins; removed the obsolete floating-size control.
- Improved transparency readability with dark backing behind information panels, the clock, header, graphs, and audio section; brighter labels and clock text; and a minimum visible HUD brightness in transparent OLED mode.
- Added responsive layouts for portrait, landscape, and ultrawide screens while retaining independent monitor themes, temperature/time controls, audio sensitivity, fullscreen pausing, and automatic Windows startup.
- Verified stationary positioning with animated rings, full-screen sizing during OLED motion, live history samples, contrast settings, portrait/ultrawide containment, native attached-window transparency, and existing tray controls.
- These are wallpaper HUD editions with local telemetry, not a voice assistant or conversational AI. OLED movement reduces static HUD exposure but does not guarantee protection against burn-in.

## v14 - Moving OLED edition and transparent backgrounds

- Added a transparent background mode that reveals the user's existing Windows wallpaper, available globally and separately on each monitor.
- Added an OLED edition with the entire HUD slowly moving across the screen, enabled by default. Movement remains active when decorative animations are off.
- Added per-monitor OLED controls: floating HUD on/off, size (50/65/80%), brightness (25/40/60/80/100%), minute pixel shifting, and idle dimming after two minutes.
- OLED mode uses a black background, removes decorative glow, and hides the HUD while paused. Transparency can override the black background to reveal the wallpaper.
- Added 12 game-inspired themes: Witcher - White Wolf, Skyrim - Northern Sky, Diablo - Sanctuary, Final Fantasy - Crystal, Hollow Knight - Hallownest, Hades - Underworld, Stardew Valley - Twilight Farm, No Mans Sky - Atlas, Warframe - Tenno, Overwatch - Payload, Dark Souls - Bonfire, and Sea of Thieves - Ghost Ship.
- Added six OLED palettes: Midnight, Emerald, Amber, Ice, Crimson, and Violet. There are now 36 presets.
- Kept independent monitor settings, saved preferences, and automatic Windows startup.
- Verified native transparency in both a normal window and an attached child window, alongside movement, dimming, pause behavior, theme switching, and existing controls.
- OLED features reduce static HUD exposure; they do not guarantee protection against burn-in or change the display's hardware protection settings. Transparent mode leaves the underlying wallpaper and desktop visible.

## v13 — Game-inspired theme collection

- Added 12 game-inspired color presets, bringing the collection to 18 themes.
- New palettes: Cyberpunk - Night City, Halo - Spartan, Fallout - Vault Terminal, DOOM - Hellwalker, Mass Effect - N7, Portal - Test Chamber, Elden Ring - Lands Between, Zelda - Hyrule, Minecraft - Overworld, Resident Evil - Survival Horror, Destiny - Guardian, and Metroid - Samus.
- Every new theme is available globally and separately on each monitor.
- Retained live previews, menus that stay open while comparing presets, and restoration of custom colors.

## v12 — Theme comparison without reopening menus

- Global and per-monitor theme menus stay open when selecting presets, so multiple themes can be compared in succession.
- Added Done to dismiss the theme menu after choosing a palette.
- Kept normal menu dismissal for outside clicks, Escape, and actions that open a color dialog.
- Retained immediate theme application and saved selections.

## v11 — Automatic Windows startup

- Automatic startup is now registered after the first successful wallpaper launch, without enabling a checkbox or running a separate setup command.
- Subsequent normal launches refresh the startup shortcut to the current application folder.
- Replaced the optional startup toggle with a startup status indicator in the tray menu.
- Kept a Remove Startup command for removing the shortcut before uninstalling.
- Added this complete changelog to the package.

## v10 — Independent monitor settings

- Added a Monitors tray submenu showing connected displays and their resolutions.
- Each screen can use its own theme preset or custom colors, or follow the global theme.
- Added independent pause/resume controls for each monitor, saved between launches.
- Fullscreen apps pause only the displays they cover; other screens continue updating.
- Selected windowed games and apps pause only their active monitor.
- Added per-monitor fullscreen-pause preferences alongside the global master control.
- Pausing every monitor stops shared animation, telemetry updates, GPU polling, and audio capture. Resuming one monitor leaves other paused monitors frozen.
- Replaced estimated widescreen layout with actual Windows monitor positions and dimensions, including portrait, vertical, and offset layouts.
- Added a brief desktop-attachment retry period for Windows sign-in.

## v9 — Themes, audio sensitivity, and custom icon

- Added six tray-selectable themes: Rose HUD, Cyber Cyan, Matrix Green, Ember, Arctic Blue, and Monochrome.
- Preserved custom color choices when switching to a preset; Custom restores the last custom palette.
- Added audio sensitivity presets and a custom 0–300% slider.
- Sensitivity affects the spectrum and audio-reactive glow without changing system playback volume.
- Setting sensitivity to 0% disables audio reactivity and stops audio capture.
- Added a custom cyan reticle/equalizer icon to the application and notification area.
- Included a multi-resolution ICO, PNG preview, and editable SVG source.

## v8 — Pausing and performance controls

- Added automatic pausing for active fullscreen apps and games, with automatic resume when returning to the desktop or switching away.
- Added a selected-app list for windowed games and other applications, including an executable picker.
- Added manual pause/resume from the tray.
- Added 15, 30, 60, and 120 FPS limits.
- Added Full, Reduced, and Off animation modes.
- Added Slow (0.5x), Normal (1x), and Fast (2x) decorative motion speeds while keeping clock time accurate.
- Reduced mode limits decorative motion and transitions; Off stops the spectrum and decorative animation while retaining clock and telemetry updates.
- Saved performance preferences between launches.
- Improved audio capture shutdown and restart handling when pausing or disabling animations.

## v7 — Tray startup controls

- Added a checked Start with Windows option to the tray menu, sharing the same startup shortcut as the original startup scripts.
- Added startup status refresh and shortcut validation.
- This optional behavior was replaced by automatic registration in v11.

## v6 — Clock format and reliable live colors

- Added a saved 12-hour AM/PM / 24-hour clock selector to the tray menu.
- Changed color application from relying on reloading a settings script to sending saved settings directly to the running wallpaper.
- Connected remaining fixed-color elements to the selected palette, including borders, secondary effects, background, and clock text.
- Applied settings immediately without restarting the wallpaper.

## v5 — Temperature unit selector

- Added Celsius and Fahrenheit choices to the tray menu.
- Added a checkmark for the current unit and saved the choice between launches.
- Updated both GPU temperature displays when switching units.

## v4 — Fahrenheit display

- Converted GPU temperature readings from Celsius to Fahrenheit for display.
- Added the °F label and retained unavailable-data handling for missing readings.

## v3 — Tray color settings and Windows 11 detection

- Added color settings directly to the tray menu.
- Added controls for primary accent, secondary accent, background, and text colors.
- Corrected Windows 11 identification when the registry ProductName still reports Windows 10.
- Connected accent colors to glow and spectrum effects.
- Rebuilt the native host and included the updated source.

## v2 — Desktop attachment fixes

- Corrected wallpaper window discovery.
- Improved Explorer wallpaper-layer discovery, including Windows 11 layouts with a WorkerW inside Progman.
- Used a replacement launcher to avoid the supplied host's repeated desktop reattachment behavior.
- Added visible startup errors, a diagnostic launcher, and launcher logging.

## Initial standalone package

- Reused the native WebView2 host and bundled .NET runtime supplied in the original archive, and packaged them into a portable standalone distribution.
- Removed the need to run Wallpaper Engine or a separate Python stats service.
- Retained the original HUD design, live system statistics, and system-audio spectrum.
- Replaced Panda branding with the current Windows username.
- Added Start Wallpaper, Stop Wallpaper, Preview, and color-setting launch controls.
- Added initial startup-management scripts.
- Set the clock to 12-hour time with AM/PM by default.
- Included editable page assets, C# source, and usage documentation.

## Requirements and installation

- Windows x64 and Microsoft Edge WebView2 Runtime.
- No separately installed .NET runtime, Python service, or Wallpaper Engine is needed to run the package.
- NVIDIA GPU telemetry depends on nvidia-smi; unavailable readings are shown as unavailable.
- Extract the complete package to a permanent folder, then run Start Wallpaper.cmd once. Automatic startup is registered for the current user and runs when that user signs in to Windows.
- Launching the application from a new folder updates its startup shortcut. Keep the application folder in place while using automatic startup.
- Preferences are saved with the application. Preserve the settings JSON files and monitor-colors folder when moving to a new distribution folder.
