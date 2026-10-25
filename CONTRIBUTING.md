# Contributing

Use an issue for a reproducible bug or a concrete feature request, and Discussions
for setup questions. For security vulnerabilities, follow [SECURITY.md](SECURITY.md).

## Development

Use Windows x64 with the .NET 10 SDK and Microsoft Edge WebView2 Runtime.

```powershell
dotnet run --project tests/Tests.csproj -c Release
.\scripts\Build-Release.ps1
```

The ZIP and SHA-256 checksum appear in `artifacts`. Extract the ZIP before running
its launcher. Do not commit compiled output, user settings, crash dumps, logs, or
credentials.

## Pull requests and reviews

Create a focused branch and open a pull request targeting `main`. Include detailed
notes explaining the problem, changed behavior, significant implementation choices,
test commands and results, usage instructions, and limitations. Follow the PR
template and [review guide](.github/REVIEW_GUIDE.md).

Include tests for meaningful behavioral changes. For UI or Windows integration
changes, describe what you exercised and what requires physical hardware or an
actual sign-in/restart to validate. Distinguish automated tests from manual checks.

The main branch requires an independent approving review, resolved conversations,
and the Windows `build` check on an up-to-date branch. New commits invalidate older
approvals. Maintainers squash approved PRs. Keep discussion constructive and focus
review feedback on the code and its behavior.

Dependabot proposes NuGet and GitHub Actions updates weekly. Review those changes
and their CI results before merging; updates are not automatically merged.
