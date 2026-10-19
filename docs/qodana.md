# Qodana PR analysis

The root `qodana.yaml` configures Qodana for .NET against the complete
`Liner Notes.sln` in Release mode with the recommended inspection profile.
The workflow installs the SDK pinned in `global.json` and runs the linter in
native mode. Its bootstrap restores locked dependencies and builds the solution.
It does not start the application, connect to a database, or run the test suite;
the existing CI workflow owns tests and container lifecycle checks.

To activate `.github/workflows/qodana.yml`, create a Qodana Cloud project using
the .NET linter and store its project token in the GitHub repository
Actions secret `QODANA_TOKEN`. Tokens must never be added to `qodana.yaml`.

The workflow runs on pushes to `main` and `Prism`, same-repository pull requests,
and manual dispatch. It checks out the actual analyzed commit with full history,
uses the action's PR mode for changed files, and publishes annotations, a PR
comment, and a downloadable `qodana-report` artifact. Fork PRs are skipped because
they cannot access the project token. There is no `pull_request_target` workflow.

The first scans report findings without an issue-count quality gate. Scan or
build errors still fail the job. Choose a threshold after reviewing the initial
report; no baseline or suppression of existing findings is supplied.

The selected linter covers C# and the site's JavaScript and requires a compatible
Ultimate or Ultimate Plus license. No license purchase is assumed. JetBrains
currently labels Community .NET as Early Access, so it is excluded by this
repository's stable-only tooling policy.

For local native analysis, install the SDK from `global.json`, supply the token
through the environment, and run `qodana scan --within-docker false`.

References: [JetBrains .NET configuration](https://www.jetbrains.com/help/qodana/dotnet.html),
[GitHub Actions setup](https://www.jetbrains.com/help/qodana/github.html), and
[pinned action release](https://github.com/JetBrains/qodana-action/releases/tag/v2026.2.2).
