# Project workflow

The project owner requires pull requests for all repository changes.

- Create a descriptive feature, fix, or documentation branch before editing.
- Commit and push changes to that branch, then open a pull request targeting
  `main`. Do not push changes directly to `main`.
- Do not merge a pull request unless the project owner explicitly requests it.
- Keep each pull request focused on the requested work.

## Pull request notes

Provide detailed, concrete notes covering:

1. The problem or requested feature and the resulting behavior.
2. What changed, including significant files or components and design decisions.
3. Validation performed, with commands and their outcomes; distinguish local
   testing from GitHub Actions results and call out anything not tested.
4. User-facing instructions, compatibility considerations, remaining limitations,
   and any migration or release steps that apply.

Use a title describing the actual change. Rewrite the description if the scope
changes so it matches the final diff. Do not claim validation that was not run.

## Validation

- Regression tests: `dotnet run --project tests/Tests.csproj -c Release`.
- Windows release package: `./scripts/Build-Release.ps1` in PowerShell with the
  .NET 10 SDK available on PATH.
- Keep generated outputs, release archives, and local settings out of Git.
