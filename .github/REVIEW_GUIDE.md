# Code review checklist

Each change needs an independent approval, a passing Windows `build` check,
resolved review conversations, and an up-to-date branch before merging to main.
New commits dismiss older approvals. Maintainers use squash merge; automatic
merging is disabled. CODEOWNERS requests review from the other maintainer once
this file and the ownership file reach the default branch.

Review the final diff and the PR's validation evidence. Pay particular attention
to these areas:

- **Windows lifecycle:** disposal of processes, timers, audio capture, and WebView2;
  bounded external-process execution; shutdown, sleep/resume, and Explorer restart.
- **Performance:** avoid blocking the UI thread, overlapping telemetry polls, or
  collecting audio and statistics while every monitor is paused.
- **Settings and profiles:** validate saved input, preserve edition separation,
  retain backups and recovery behavior, and avoid changing startup preferences
  as a side effect of applying a profile.
- **Display and OLED behavior:** per-monitor preferences, hot-plug behavior,
  transparency readability, drift bounds, and pause-to-black behavior. OLED care
  does not guarantee burn-in protection.
- **Web content:** validate data crossing the native/WebView boundary; avoid
  introducing HTML injection, remote scripts, or unnecessary network access.
- **CI and releases:** keep action references pinned to commit hashes, minimize
  token permissions, and exclude local settings and credentials from artifacts.

CodeQL and dependency review complement human review. They do not verify visual
quality, real monitor behavior, or all runtime failures. A clean scan is not a
claim that the application has no vulnerabilities.
