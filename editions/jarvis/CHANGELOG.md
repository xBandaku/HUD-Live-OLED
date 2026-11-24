## v41.0.2 - Taskbar persistence and settings recovery (October 26, 2026)

- Keep the visible taskbar flush with the physical monitor bottom instead of placing it above a stale Windows taskbar reservation; repair placement at display and DPI changes.
- Preserve fullscreen hiding on a monitor when focus moves elsewhere, while allowing a normal app placed above that video/game to show its taskbar.
- Preserve intentionally empty taskbar pins across restart/re-enable, migrate existing saved arrays without reimporting them, and import defaults only for fresh settings.
- Validate audio-setting JSON types and recover a valid backup or safe default instead of failing during startup on an edited or invalid setting.
- Recreate HUD taskbars on the active virtual desktop when Windows reports they belong to an inactive desktop, without changing the user's desktop or foreground app.
- Restore non-activating topmost placement over normal apps and retain per-monitor fullscreen hiding. Ignore the wallpaper, shell, and captioned maximized windows when deciding whether to hide.
- Retry transient Explorer/appbar registration failures and refresh after display changes or resume instead of permanently disabling replacement. Make native Windows taskbars available during update failures while preserving the recovery watchdog and enabled setting.

## v41.0.1 - Audio and startup recovery (October 26, 2026)

- Follow changes to the default audio output within approximately 1.5 seconds, reset retired capture samples, and retry stopped or unavailable audio devices.
- Keep launcher attachment retries running when Explorer or the HUD window is not ready after startup; recover without leaving an orphaned preview host that requires a restart.
- Close the active feed listener on disposal even when the requested port was occupied and a fallback port was selected.
- Add JARVIS audio/feed and simulated launcher recovery tests to Windows CI, alongside a build of the JARVIS project.

## v41 - United States globe focus (October 26, 2026)

- Add persistent United States / World focus controls under SHIELD / God's Eye > Focus. Center the U.S. view at 39 N, 98 W with a 6,500 km camera altitude, while World restores the 20,000 km global view.
- Apply the selected focus both in the wallpaper embed and when opening a new interactive console. Use the upstream share-link camera parameters for the initial frame so it starts over the requested area rather than its default city.
- Retain focus when switching aircraft, satellites or earthquake layer presets. Add voice shortcuts such as Jarvis, focus on the United States and Jarvis, show whole globe.
- Preserve the v40 full-desktop size repair and all existing settings. Verify the U.S. camera link and focus label through the actual embedded application alongside existing pause/fallback, layout and custom-icon checks.

## v40 - Full-desktop wallpaper size recovery (October 26, 2026)

- Fix the wallpaper remaining at its 1600x900 preview size after attachment to the full desktop. Check the real wallpaper window and client size every second, independently of monitor-change notifications, and restore the entire physical virtual-desktop rectangle.
- Disable automatic Form scaling on the wallpaper host. Keep genuine standalone preview windows unchanged by applying repair only while parented to Explorer's WorkerW/Progman layer.
- Make the launcher DPI-aware and verify actual window coverage rather than only comparing saved monitor dimensions. Reapply desktop attachment if the wallpaper shrinks even when the display arrangement is unchanged.
- Map screen coordinates into wallpaper-parent client coordinates in both the launcher and native repair path, including monitors to the left or above the primary display. Avoid repeating native resize operations when bounds are already correct.
- Verify native repair from 1600x900 to 5120x1440, negative virtual-desktop origins, resolution transitions and stable no-op behavior. Preserve God's Eye, per-monitor roles, startup and organizer settings.

## v39 - God's Eye View inside SHIELD (October 26, 2026)

- Integrate the actual MIT-licensed God's Eye View v0.2.1 browser application by Bilawal Sidhu into the SHIELD map panel using its supported embed/view protocol. Retain upstream source, license, provider notices and dependency licenses.
- Add a bundled Node runtime and production browser build, with upstream local-provider middleware. Start automatically with HUDWall on a dynamically chosen 127.0.0.1 port, verify the child process health, stop on normal exit, and monitor owner exit for crash cleanup. No manual Node/npm installation required.
- Add SHIELD / God's Eye tray controls for Global, Air traffic, Space, Earthquakes, enable/disable, restart and a native interactive console with the full upstream UI. Keep the wallpaper click-through and the console's custom executable/window/taskbar icon explicit.
- Add voice shortcuts to show God's Eye, air traffic, satellites, earthquakes or restore the archive map. Preserve JARVIS conversation and organizer settings.
- Use keyless Esri/public sources by default, retain visible provider attribution and leave optional paid/keyed sources for user configuration. Avoid making the external globe a desktop-command bridge. Bind the local service only to loopback; allow embedding only from https://hud.local.
- Keep the archive atlas visible during loading, restore it on disable/failure, and integrate only on SHIELD displays. Pause/resume the upstream renderer from trusted-parent messages and limit embedded rendering to 30 FPS or the lower HUD target. Background feed refreshes can continue during render pause.
- Verified the actual production local server, embed readiness, geographic globe rendering, SHIELD-only placement, disable/re-enable and archive fallback, rendering pause/resume acknowledgments and FPS cap, clock spacing, responsive layouts, JavaScript errors, packaged icon match and the native interactive console icon. Upstream embedding regression tests passed (9/9). Availability of every individual external data provider is not guaranteed.

## v38 - Display recovery and readable telemetry clock (October 26, 2026)

- Fix a HUD taskbar remaining at the old 1024x768 position after the normal multi-monitor desktop returns. Recheck display identity, size, position and primary designation every second and recreate bars when that layout changes, even if a display-change message was missed.
- Rediscover native Windows taskbars every second, including newly created secondary bars whose appearance does not replace Explorer's main taskbar. Hide these while the replacement is active.
- Revalidate registered appbar placement periodically and publish updated taskbar insets to the wallpaper after a layout rebuild.
- Give the telemetry clock independent flex spacing, tabular numerals, a normal colon margin and a separate smaller seconds column. Remove the inherited negative reactor kerning. Verify 12- and 24-hour spacing and containment.
- Regression checks passed: deliberately displaced appbar returns to its edge; stale layout signature recreates bars; live app activate/restore/minimize/normal-close; native tray fallback and normal Windows restoration; clock non-overlap in both formats; dual-interface rendering, responsive containment, video pause and packaged custom icon match. Native hardware testing uses the display currently exposed by the session; the actual monitor reconnection remains to be confirmed on the physical desktop.

## v37 - Avengers interface audit and modern HUD taskbar (October 25, 2026)

- Redesign the taskbar with rounded borderless tiles, 30px application icons, hover highlights, active indicators, readable clock typography, tooltips and accessible control names. Replace cramped text buttons with icon controls and retain JARVIS/SHIELD branding.
- Separate mixed-display roles: JARVIS handles conversation, hardware, clock, CPU/GPU/memory histories and voice; SHIELD handles the archive atlas, actual session events, network and storage. Suppress repeated primary telemetry and conversation on the secondary interface. Single-interface setups retain their information.
- Use the supplied JARVIS annular logo in the core and header. Add the supplied local intro as an optional muted video; decoding tested, pause/off and reduced-motion honored. Internal animations remain stationary within the display.
- Enhance the original geographic atlas with dotted country fills, subtle perspective and decorative animated routes. The remote Envato clip is a visual reference only; no watermarked footage is bundled. Archive sites and links remain illustrative, not live surveillance.
- Keep the command card fully contained and reserve wallpaper space for the native taskbar. Preserve the stable organizer profile and existing app/file paths.
- Correct stale startup BUILD 32 to BUILD 37; rename Frame to HUD FPS and Day to Day of year. Use ISO week numbering and UTC calendar-date arithmetic to avoid daylight-saving day-count errors.
- Stop intro playback cleanly when paused, animations are off, or reduced motion is requested. Release cached icons for windows that close. Keep original Windows tray access and taskbar crash-recovery behavior.
- Validation: self-contained Windows build; dual-interface WebView rendering and role separation; portrait, landscape and ultrawide containment; supplied image/video loading; pause behavior; no JavaScript errors; packaged executable/embedded icon match; live appbar, app activation/minimize/close, native tray access and restoration on the display exposed by the current Windows session; local Whisper normal and quieter/noisy audio tests; stable organizer recovery/path checks. Earlier v36 tests covered native two-monitor and owner-crash recovery. Physical bezel alignment still depends on the user's actual monitor arrangement.

## v36 - JARVIS / SHIELD native taskbar replacement (October 25, 2026)

- Add a real native appbar on every monitor, styled from that monitor's JARVIS/SHIELD interface and HUD palette. Reserve work area for maximized windows and hide the original Windows taskbars while enabled; keep Explorer running.
- Add running-window buttons with actual app icons, activate/restore/minimize behavior, normal-close and pin actions, full-title tooltips and an APPS overflow menu. Keep HUD conversation windows accessible too.
- Import existing Windows pinned shortcuts where available, save custom pins, and provide Start, search, Task View, desktop, clock/date, quick settings and notifications controls.
- Expose original Windows notification icons through TRAY: show the native Windows taskbars for 25 seconds, then return to the HUD bars. Notification icons are not reparented or injected into the custom bars.
- Add HUD taskbar > Replace Windows taskbar / Restore Windows taskbar and the emergency recovery shortcut Ctrl+Alt+Shift+T. Voice commands can restore Windows or enable HUD taskbars.
- Add a separate recovery watchdog with a readiness handshake before native taskbars are hidden. Restore the original taskbar auto-hide/always-on-top state on normal exit, startup recovery and unexpected owner-process termination.
- Handle Explorer/taskbar recreation, display changes, fullscreen app visibility and per-monitor theme updates. Poll live windows once a second and keep native-taskbar hiding lightweight.
- Explicitly set the custom application icon on taskbar windows and the conversation window. Retain the stable recovered organizer profile, frames and files.
- Verified actual two-monitor replacement and appbar registration, live enumeration, activate/restore/minimize/normal-close on a controlled app window, original tray access/resume, normal restoration, and restoration of both native taskbars and original state after forcibly terminating a test owner process. Existing cinematic UI, pronunciation, layout, speech, icons and routine checks passed.

## v35 - Reference-inspired interfaces, full command cards and recurring cinematic sequences (October 25, 2026)

- Measure the entire command card and choose an unobstructed area that fits it, rather than cropping it into a short gap. Wrap model status onto its own header row; preserve CPU/frame/week/day readouts.
- Speak J.A.R.V.I.S. as Jarvis and S.H.I.E.L.D. as Shield; retain VEYDR -> Vader and the original displayed branding.
- Add a detailed JARVIS engineering view: theme-aware triangular core, fine grid, framed technical panels and compact side readouts driven by real CPU, memory, network and process telemetry. Keep a tray toggle for the simpler view.
- Restyle enhanced SHIELD geography with dark teal outlines and seven illustrative regional archive markers, amber concentric rings, coordinate callouts and explicit offline/no-live-tracking labeling.
- Add horizontal grid scans, rotating reference reticles, expanding marker pulses and staggered geographic illumination. Honor map animation, speed, pause and reduced-motion controls; keep static reference markers visible when motion is off.
- Add HUD appearance > Cinematic refresh interval: Off / 2 / 5 / 10 / 15 minutes (default 5). Periodic visual sequences run only while idle, with all displays unpaused and Full motion enabled; they do not repeat spoken greetings or invoke tools.
- Verify card height, readout separation, pronunciation normalization, engineering core/map decoration, timed refresh eligibility, pause/off controls, native speech playback, cancellation, custom icons and existing map/voice/AI/multi-monitor regressions.

## v34 - Restore organizer contents and keep core readouts visible (October 25, 2026)

- Fix empty desktop frames after launching through a different desktop app: detect previous DeskNest layouts in regular and app-container-virtualized roaming storage, prefer the populated layout on first migration, and back up every inspected legacy configuration.
- Store organizer configuration in %USERPROFILE%\.HudWall\Organizer so changing launchers no longer selects a fresh app-data profile. Retain DESKNEST_DATA_DIR for isolated testing.
- Restore physical paths to legacy fence items and launcher pins. Preserve original frame geometry, titles, contents and existing fence storage; do not move or delete the underlying files during recovery.
- Retain ownership of recovered fence storage for normal future drag/drop and release behavior. Add atomic configuration writes and last-valid backup recovery.
- Move JARVIS's command/ambient deck beside the hardware panel into a measured free region, instead of over the CPU load / frame / week / day readout. Keep those readouts visible in Ambient, Command and Showcase.
- Check surrounding reactor, captions, panels and footer regions when placing the deck; preserve SHIELD's map-side presentation and independent monitor interfaces.
- Verified populated-profile recovery, physical path rebasing, original-file preservation and no overwrite of an existing stable profile. Read-only recovery preview found all three original frames and all 13 pinned app/folder paths intact.
- Verified all three scene modes against the core readout overlap, matching packaged custom icons, routines, native speech playback, voice envelopes, cancellation and responsive containment.

## v33 - Avengers command-centre experience (October 25, 2026)

- Add Command centre > Auto, Ambient, Command and clearly labelled Showcase modes, with saved selection and voice commands.
- Coordinate JARVIS lead-display and SHIELD supporting-display panels around the same live request. Show relevant context for weather, system, apps, file searches and conversation; preserve physical-monitor layouts and per-monitor interfaces.
- Add a bounded, in-memory session timeline of real requests, tool activity, routine launches and responses; show the latest entries on the HUD and the full session from the tray.
- Add configurable app routines: prepare my workspace / work mode / gaming mode. Launch only configured, valid app aliases when explicitly requested, report unavailable apps, and respect the PC-tools setting.
- Add quiet acknowledgement/response tones routed through the selected voice output, with an independent effects toggle and 0-40% volume. Suppress effects while paused and attenuate them while speaking.
- Add subtle backdrop illumination and panel materialization; preserve fixed full-screen geometry, reduced motion, FPS controls, pause rules, theme colors and monitor-bezel protection.
- Display resource warnings from actual memory, GPU temperature and drive usage; amber warnings and red extreme memory/drive pressure, without flashing or automatic system changes.
- Showcase presents simulated activity only and never invokes PC tools. It stops an existing AI response on entry; a real voice request restores Auto mode.
- Begin neural speech after the first sentence is synthesized while subsequent sentences are prepared in a bounded queue. Preserve cancellation and per-sentence voice-reactor envelopes.
- Add local speech-model readiness to the HUD and tray, and automatic first-run download of missing Kokoro/Whisper models with SHA-256 verification. Keep model weights in %LocalAppData%\HudWall\Models and reuse them across updates.
- Explicitly set the matching custom embedded icon on organizer windows/dialogs as well as the executable, wallpaper window and tray.
- Verified packaged icon pixels/native window icon, WPF icon assignment, configured shortcut routine launch and disabled-tools guard, all scenes, literal transcript/timeline rendering, telemetry alerts, dual-display layouts, pause/reduced-motion handling, actual neural sentence playback/envelopes/cancellation, and existing voice/AI/map/multi-monitor regression checks.

## v32 - Cinematic JARVIS and S.H.I.E.L.D. (October 25, 2026)

- Add a startup boot sequence. J.A.R.V.I.S.: reactor ignition shockwave, ring spin-up, light sweep, decoding headings, materializing panels and a REACTOR OUTPUT counter. S.H.I.E.L.D.: insignia authentication (ACCESS GRANTED), countries lighting up west to east as the scan crosses the map, pinging reference cities and a GRID CALIBRATION counter.
- Show a boot console with this PC's real processor, graphics, memory, storage, telemetry, voice and assistant status; the SHIELD console stays factual (offline schematic, no tracking claims).
- Greet the user aloud when systems come online: time, date, weather (when web search and a weather city are set) and a system check that names anything needing attention. The SHIELD primary display greets "Agent". Skipped while a fullscreen app pauses the HUD.
- Make the reactor react to JARVIS's actual voice: HUD Live measures each spoken reply's loudness as it plays and the page draws radial voice bars and a core glow. SHIELD displays get a comms oscilloscope trace and a glowing insignia. The Windows voice fallback uses a synthetic pulse.
- Add Jarvis voice commands: power up / boot sequence / wake up (replay the boot and greet), good morning / afternoon / evening / I'm home, status report.
- Add HUD appearance > Boot sequence at startup and Play boot sequence, and JARVIS voice > Spoken greeting at startup.
- Respect Full/Reduced/Off animation, the FPS cap, pause rules, the Reactor rings setting and Windows reduced motion; preload the voice model during the boot so the greeting lands on cue.
## v31 - Desktop organizer built in (October 25, 2026)

- Merge the DeskNest desktop organizer into HudWall.exe: folder portals, shortcut fences, the tabbed launcher (Ctrl+Alt+Space) and desktop auto-sort rules.
- One tray icon (new Desktop organizer menu; left-click opens the launcher), one Windows startup entry and one Exit for both.
- Draw frames and the launcher as JARVIS panels (square corners, accent border, corner brackets, Consolas headings), colored from the HUD theme of the monitor each frame is on and updated live when themes change. Spanning HUDs use the primary display's theme.
- Add "HUD theme" to the frame Color menu and make it the default; frames on DeskNest's stock colors switch to it, custom colors are kept.
- Add Jarvis voice commands: open/close launcher, sort desktop, auto sort on/off, edit sort rules, show/hide frames, bring frames back, new fence, new portal (for downloads, documents, pictures, music, videos, desktop), show/hide desktop icons. Natural variants such as "tidy up my desktop" work with local Whisper input.
- Keep existing layouts in %AppData%\DeskNest; remove the standalone DeskNest startup entry, and wait for a running standalone DeskNest to exit before taking over.
- Run the organizer on its own UI thread so frames stay responsive while the wallpaper renders; Tray > Desktop organizer > Run desktop organizer turns it off.
- Preview mode no longer changes the automatic startup shortcut.
- Developer aid: HudWall.exe --organizer-snapshot <folder> renders frames and the launcher to PNGs.
## v30 ? Animated tactical map (October 24, 2026)

- Add a subtle scanning sweep across the current geographic view and staggered pulses around reference-city markers.
- Keep countries, labels and geographic positions stationary; the animation is a visual effect.
- Honor global/per-monitor pause, Full/Reduced/Off animation mode, motion speed and Windows reduced-motion preference.
- Add HUD appearance > Animate SHIELD map, saved automatically with other appearance preferences.
- Adapt sweep bounds to World and regional views; retain independent monitor interfaces and startup restoration.

## v29 ? User-provided S.H.I.E.L.D. insignia (October 24, 2026)

- Replace the placeholder eagle with the supplied S.H.I.E.L.D. logo in the command-display header and monitor brand mark.
- Use the original image asset, with its portrait margins excluded by the SVG viewport and black blended into the interface background.
- Preserve logo proportions, monitor interface choices and all saved preferences.

## v28 ? Restore every preference and correct map proportions (October 24, 2026)

- Persist the global Pause wallpaper state, including pause/resume voice commands, and restore its tray checkmark on launch.
- Retain immediate saving for per-monitor interfaces, colors/themes, pauses, transparency/OLED preferences, map region, appearance/visibility, FPS/animation settings, voice/apps/weather city, AI provider/tool options, units, clock format and audio sensitivity.
- Save settings through flushed temporary files and atomic replacement, keeping a last-valid backup and recovering from invalid/missing primary JSON files.
- Register automatic Windows startup at launcher entry and native app startup, so startup registration does not depend on successful desktop attachment. Saved options are loaded on launch without resetting them.
- Make the SHIELD Global Operations area taller by compacting lower history/communications strips.
- Restore uniform map scaling and focus the world extent on major landmasses, removing the stretched appearance while retaining a large geographic display.
- Verify settings with separate writer/reader processes, global pause restoration in a new HUD/tray, backup recovery, startup shortcut checks, and existing interface/voice/AI/multi-monitor regressions.

## v27 ? Fill the Global Operations map area (October 24, 2026)

- Expand the geographic map across the full width and height of the Global Operations viewport, removing the large side margins.
- Keep the entire selected world/region view visible rather than cropping countries to fill the panel; the map adapts to the panel's proportions.
- Apply the same fill behavior after region changes and monitor layout rebuilds.
- Preserve reference markers, borders, coordinate grid, saved map view and independent monitor interfaces.

## v26 ? Real geographic tactical basemap (October 24, 2026)

- Replace the hand-drawn continent schematic with Natural Earth 1:50m country geography: 242 features and 99,613 source vertices.
- Render detailed coastlines, islands and country borders from bundled vector data with an equirectangular WGS84 coordinate grid.
- Replace fabricated PC-subsystem positions and connection routes with correctly projected reference cities: Washington D.C., London, Tokyo and Sydney.
- Add HUD appearance > SHIELD map view > World / North America / Europe / Asia-Pacific, with saved selection and immediate region focus.
- Bundle all geometry locally: no map API key, online tile service or internet access required to display it.
- Include source provenance and SHA-256 in assets/map-source.txt. Natural Earth data is public domain.
- Retain separate JARVIS and SHIELD monitor interfaces, local voice, AI conversations, transparency and automatic startup.
- Verify geographic feature presence, removal of fabricated routes, all four native map-view choices, persistence after layout rebuilds, and existing layout/voice/AI regressions.

## v25 ? Dedicated S.H.I.E.L.D. command-centre layout (October 24, 2026)

- Give S.H.I.E.L.D. a separate operations-room composition instead of the JARVIS reactor layout.
- Replace the central reactor with a wide tactical schematic and a compact clock/agency insignia in the command-display header.
- Move resource allocation and hardware inventory into a stacked right-hand column, with compact inventory tiles.
- Add live assistant-link, voice-channel and network-throughput readouts below the tactical schematic.
- Give the audio and voice area communications-console labeling and retain live response/action feedback.
- Label map geography as decorative schematic imagery; it does not claim to track devices or locations.
- Preserve JARVIS's existing reactor layout and each monitor's independent interface selection, colors, transparency and pause settings.
- Verify the distinct layouts side by side, live readouts, compact clock, native style switching/persistence, and portrait/landscape/ultrawide containment.

## v24 ? Independent interfaces per monitor (October 24, 2026)

- Add Monitors > [display] > Interface style with J.A.R.V.I.S. reactor, S.H.I.E.L.D. operations, and Use global interface.
- Save interface selection by Windows display device ID, independently of each monitor's theme, colors, transparency and pause settings.
- Scope interface styling and insignia to each display so JARVIS and SHIELD can run side by side.
- Choosing an explicit per-monitor interface automatically uses one complete HUD per monitor. Both share one voice recognizer, assistant and AI conversation.
- Preserve global interface selection as the default for displays set to Use global interface.
- Retain spanning as a separate layout option; a single spanning HUD follows the primary monitor's interface selection.
- Configure this PC with JARVIS on the primary display and SHIELD on the companion display.
- Verified native per-monitor menu choices, simultaneous styles and seals, persistence and reload, global inheritance, explicit override behavior, voice/AI regressions, and monitor layout checks.

## v23 ? Physical-monitor layout and S.H.I.E.L.D. option (October 24, 2026)

- Fix the reactor and clock being split behind the centre bezels in spanning mode.
- Send physical monitor rectangles and the primary-monitor identity to the HUD instead of discarding that information when combining displays.
- Keep one spanning HUD/session while placing the reactor, title and voice console inside the primary display, and diagnostics/history inside the companion display.
- Keep header text, status chips, graphs and panels within a physical screen; account for mixed sizes, screen offsets and stacked arrangements.
- Add ?Keep widgets clear of monitor bezels? to the tray menu, enabled by default. Turning it off restores the previous centred spanning layout.
- Add HUD appearance > Interface style > S.H.I.E.L.D. operations / J.A.R.V.I.S. reactor, with saved selection and immediate switching.
- Add a navy operations-console appearance with a geometric eagle seal, restrained silver details and amber statuses; existing accent controls remain available.
- Include the full v22 command-centre redesign and its live voice/action feedback.
- Verify native physical-screen metadata, widget containment, left/right primary, mixed size, offset and stacked layouts, opt-out restoration, style switching, persistence and rebuild behavior.

## v22 ? JARVIS command-centre interface (October 24, 2026)

- Redesign the stationary JARVIS edition with a layered arc reactor, segmented rings, radial ticks, reticle, angular hardware/telemetry panels, fine perimeter details, and cyan/amber visual accents.
- Add a live voice transcript alongside the response, with Listen / Decode / Think / Act / Respond indicators linked to real recognition, AI and desktop-tool states.
- Show the active microphone below the reactor and recent desktop-tool activity alongside the response.
- Preserve theme color controls, transparent wallpaper backgrounds, full-screen geometry, monitor spanning, and existing animation/pause controls.
- Keep the whole interface stationary; reactor details animate inside their fixed bounds.
- Improve transparent clock readability with a dark circular core and retain panel-darkness controls.
- Verify all five request phases, literal transcript rendering, state restoration after layout changes, native voice/AI actions, and portrait/landscape/ultrawide containment.

## v21 ? Local Whisper voice recognition (October 24, 2026)

- Replace primary Windows dictation with local Whisper base.en recognition; no API key or cloud transcription required.
- Capture the selected Windows communications microphone explicitly and switch automatically when it changes.
- Add in-memory speech activity segmentation, a 400ms pre-roll, and an 850ms pause allowance to preserve words and natural pauses.
- Keep recognition separate from response generation; prevent duplicate Windows/Whisper command dispatch.
- Recover automatically after capture errors or microphone reconnects, retaining Windows speech as a fallback.
- Add microphone name, signal activity, recognition state, Last heard, and Restart microphone recognition to the tray menu.
- Accept common wake-name punctuation and ?Hey Jarvis?. Preserve continuous headphone listening and speaker echo suppression.
- Retain no raw microphone recordings or transcript files. Recognized text is visible in the current session's Last heard menu.
- Update local setup with a SHA-256-verified Whisper model download (about 148 MB).
- Verified four recorded phrases (including quieter audio with noise), capture PCM conversion, silence rejection, natural pauses, wake normalization, and actual Arctis microphone initialization. Recorded transcription took about 0.6 seconds per test phrase on this PC; real-world accuracy depends on speech and noise.

## v20 ? Automatic headphone listening and response improvements (October 24, 2026)

- Add direct weather requests with remembered city, current conditions, three-day forecast and selected temperature units (Open-Meteo).
- Add graceful close-app requests and running-app lookup, with explicit close-intent checks and no forced termination.
- Route common weather/close commands directly, bypassing AI latency.
- Live-tested a controlled app closing normally and real weather lookup.
- Automatically use the selected communications headset; otherwise follow the default media output.
- Continuous natural conversation on headphone/headset outputs, without a wake word or 90-second expiry.
- Automatically switch to wake-name listening on speaker outputs; cancel ongoing speech when the output changes.
- Interrupt a spoken or pending response by speaking again on headphones.
- Select the British George neural voice automatically; retain Windows speech fallback.
- Pronounce VEYDR as Vader while preserving the displayed spelling.
- Reduce recognition endpoint delays and input debounce; tune recognition acceptance for natural questions.
- Keep the local AI model warm for 30 minutes and prefer shorter replies.
- Serialize neural synthesis requests to prevent overlapping inference during interruptions.
- Keep explicit Stop listening, automatic Windows startup, and existing monitor preferences.
- Verified pronunciation, device classification, listening policy, native commands, AI tools/context/cancellation, monitor spanning, and stationary full-screen layout.

# HUD Live — Standalone Changelog

**Latest release: October 24, 2026**

HUD Live now runs as an independent Windows live wallpaper, with tray controls, customizable themes, audio reactivity, performance settings, and separate settings for each monitor. Wallpaper Engine is no longer required.

## v19 - Local Ollama AI and British neural speech

- Installed Ollama and downloaded Qwen3 8B on the development/user PC; switched JARVIS to local conversation by default, with no OpenAI key or API balance required.
- Added a selectable AI provider and local model field. OpenAI remains optional and uses its own encrypted key when selected; local requests use the loopback Ollama API.
- Added local multi-step tool calling for filename searches, requested text reading, installed app discovery and opening, with session memory, cancellation and the existing access boundaries.
- Added online search-result retrieval with snippets and source links for the local model, without a paid search API. Only requested web searches send queries online; ordinary local conversation and file context remain with the local model.
- Added local Kokoro neural speech with three British male choices: George, Lewis and Daniel. Added Measured, Natural and Brisk speech speeds, with Windows voices retained as an option and fallback.
- Added a calm, articulate British assistant style to local AI replies. These are distinct synthetic voices, not an actor voice clone.
- Kept neural voice synthesis on the CPU so the GPU can run the language model. Preserved microphone suppression during speech, cooldown, stopping responses and stationary/spanning HUD behavior.
- Added Setup Local AI.cmd for other PCs, including Ollama/model setup and a SHA-256 check for the neural voice model. Large language/voice model weights live outside the wallpaper ZIP.
- Added a British neural voice preview and third-party license notices.
- Live-tested GPU inference, local file search/read, follow-up context, online search with sources, actual app launching through the local model's tool calls, and neural speech generation. Retained verification of the OpenAI option and prior tray/HUD controls.

## v18 - AI conversation and PC search tools

- Added OpenAI-powered natural conversation with spoken replies, arbitrary speech recognition after Jarvis, and follow-up listening for 90 seconds after a reply. Say stop conversation to return to wake-name listening.
- Added a conversation window for typed messages, full replies, tool activity, clickable web sources, cancelling a response, and starting a new conversation.
- Added AI settings for the model, AI PC tools, web search, and an API key encrypted for the current Windows account. Keys are stored outside the app folder and release ZIPs.
- Added a session-only conversation memory, bounded to six recent exchanges and a size limit; requests use store:false. User messages, relevant tool results and requested text excerpts are sent to OpenAI when using AI conversation. Provider policies and API charges apply.
- Added tools to search accessible fixed drives by filename, discover installed apps through Start menu shortcuts and App Paths, open requested apps/files/folders/websites, and read requested text/code documents.
- File search is bounded to 10 seconds, 250000 entries or 40 matches and reports partial results. Protected directories, directory links and obvious credential files are excluded. PDF/Office document content extraction is not included.
- Added OpenAI web search with source links. Scripts, installers, arbitrary shell commands, destructive actions and administrator operations are not AI tools in this release.
- Preserved existing local app commands and added natural variations such as can you open Steam. Existing local commands work without an API key.
- Added clear setup, thinking, listening, speaking, authentication, network, rate-limit and cancellation feedback. An API key must be entered privately before a live OpenAI conversation can run.
- Verified encrypted key roundtrips, a simulated multi-step Responses/tool exchange, real local search and text reading, credential exclusion, opening intent checks, context/reset, error/cancellation behavior, and installed Windows natural-speech grammars. No paid OpenAI request was made during automated verification.

## v17 - Local voice commands and spanning monitors

- Added local Windows speech recognition with a Jarvis prefix, confidence filtering, spoken replies, and visible voice status in the HUD and tray.
- Added commands for greetings, help, time/date, system status, opening configured apps, pause/resume, transparency, spanning/separate layouts, and stopping listening.
- Added a JARVIS voice tray menu with listening and spoken-reply controls, response voice selection, app configuration, a command guide, response testing, and microphone settings.
- Added spoken app aliases for EXE files and shortcuts. Includes Notepad, Calculator, File Explorer, Settings, and browser; discovers supported Discord, Steam, Chrome, Edge, and Spotify shortcuts when present.
- Suppressed command execution during speech output and a short cooldown. This application does not save microphone audio or send it to a cloud service.
- Added Span HUD across all monitors, enabled by default, with one shared clock, reactor, telemetry display, and spectrum using the exact combined desktop bounds.
- Spanning uses the primary display's theme/transparency and pauses the shared HUD when any display's pause rule applies. Separate layouts retain saved monitor preferences.
- Verified installed-recognizer grammar emulation, real synthesized speech output, actual configured shortcut launching, native command dispatch, saved listening stop, combined bounds, layout restoration, and span pause behavior.
- Provides supported local commands and scripted replies, not open-ended AI conversation. Requires an English Windows speech recognizer, a default microphone, and desktop-app microphone access.

## v16 - JARVIS telemetry and appearance controls

- Added a HUD appearance tray menu with saved controls for the hardware panel, history graphs, audio spectrum, and reactor rings, plus Reset appearance.
- Added transparent panel darkness presets: Light (70%), Balanced (88%), and Solid (100%). Changes apply immediately to all displays and persist across launches.
- Added a status strip with network download/upload rates and free space for connected fixed volumes.
- Added power status for AC power and battery operation, including battery percentage when Windows reports a battery. Desktops without a battery show AC status without an invented percentage.
- Added visible status labels for CPU load at or above 90%, memory use at or above 90%, drives at or above 90% used, and battery at or below 20% while on battery power. Labels clear when the condition clears; these are status indicators, not notifications or hardware fault diagnoses.
- Added an explicit telemetry-offline status so stale data is easier to recognize.
- Prevented negative network rates when interface byte counters reset and disposed temporary process handles after process counting.
- Retained JARVIS's stationary full-screen layout, animated rings and lighting, live telemetry, independent monitor themes/pausing, and automatic Windows startup.
- Verified saved native tray changes, live visibility/darkness changes, reset, power states, load/status recovery, responsive screen layouts, existing personalization controls, and native transparency.

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
