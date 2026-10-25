# GitHub project setup

## Repository settings

The repository is public and has Issues and Discussions enabled. The default
branch is `main`. Branches are deleted after merge, squash merge is the enabled
merge method, and automatic merging is disabled.

An active ruleset protects `main` with no configured bypass actors:

- A pull request and one independent approval are required.
- Pushing new commits dismisses previous approvals.
- Review conversations must be resolved.
- GitHub Actions' `build` check must pass and the branch must be up to date.
- Force pushes and deletion of `main` are blocked; history must remain linear.

`xBandaku` owns the repository. `ricorevere` has write access and can review PRs.
GitHub does not let authors approve their own PRs. CODEOWNERS lists both
maintainers for automatic review requests after that file is merged.

## Workflows proposed in this PR

| Workflow | When | Purpose |
| --- | --- | --- |
| Windows build and release | Main pushes, PRs, manual runs, published releases | Run regression tests, build the Windows ZIP and checksum, retain artifacts for 14 days, and attach assets to published releases. |
| CodeQL security scanning | Main pushes, PRs, weekly, manual runs | Analyze C# and JavaScript for potential vulnerabilities. |
| Dependency review | PRs | Reject newly introduced dependencies with known high or critical vulnerabilities. |
| Dependabot | Weekly after configuration reaches main | Open PRs for NuGet and GitHub Actions updates. |

Actions are pinned to verified commit hashes. Checkout credentials are not
persisted. PR workflows run with read-only repository contents permissions;
CodeQL has the security-events permission needed for results, and only the
release attachment job gets contents write permission. No `pull_request_target`
workflow executes contributor code with elevated privileges.

The initial required merge check is `build`, which has already passed on main.
CodeQL and dependency review report separately; they are not required by the
ruleset until their first runs have been validated. A maintainer can add them as
required checks in Settings → Rules → Rulesets after this PR is merged.

Repository settings were configured directly through GitHub's administration
API. Workflows, ownership, templates, and documentation take full effect after
this PR is reviewed and merged. Existing PR #1 independently records the owner's
requirement to always use PRs; this setup does not merge it.

## Security and review

Secret scanning, secret push protection, Dependabot vulnerability alerts and
security updates, and private vulnerability reporting are enabled. The workflow
token defaults to read-only and cannot approve PRs. Human reviews remain required;
no paid Copilot review service has been enabled.

See [CONTRIBUTING.md](../CONTRIBUTING.md), [SECURITY.md](../SECURITY.md), and the
[review guide](../.github/REVIEW_GUIDE.md). Reviewers should check Windows resource
lifecycle, telemetry deadlines, profile persistence, OLED behavior, and packaging.

## Sponsorship

The owner selected https://paypal.me/xWandaku as the funding destination.
`.github/FUNDING.yml` configures this as a custom link. GitHub will display the
repository's Sponsor button after this file is merged into the default branch.

No GitHub Sponsors enrollment is required for this custom PayPal link. This
configuration links to the owner's funding page; it does not create a payment,
subscription, or payout account.

## Remaining owner decisions

- Choose a license before describing the repository as open source; public
  visibility alone does not grant a general reuse license.
- Publish a versioned GitHub Release when a build is ready for users. Publishing
  a release triggers packaging and attachment of the ZIP and checksum.
