# Security reporting

Report suspected vulnerabilities using [GitHub's private vulnerability reporting](https://github.com/xBandaku/HUD-Live-OLED/security/advisories/new).
Do not put credentials, exploit details, or personal diagnostics in a public issue.

Include the affected version, a reproduction, the expected security impact, and
relevant excerpts with private information removed. Maintainers will review the
report and coordinate a fix; no response-time guarantee is currently offered.

Security fixes target the latest release on the current 16.x line. Older versions
may need to upgrade. This project is maintained on a best-effort basis.

GitHub secret scanning, push protection, and Dependabot alerts are enabled for the
repository. Proposed workflows add CodeQL analysis for C# and JavaScript and
dependency vulnerability review on PRs. These checks support, rather than replace,
maintainer review and runtime testing.
