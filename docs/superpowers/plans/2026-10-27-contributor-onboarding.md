# Contributor Onboarding Implementation Plan

> **For agentic workers:** Execute inline in small steps. Keep the commit focused and do not run the application test suite.

**Goal:** Give a new contributor one short path from repository checkout to a well-scoped pull request.

**Architecture:** A concise `CONTRIBUTING.md` links to existing setup, architecture, and diagnostics documentation. GitHub issue forms and a pull-request template collect useful context without duplicating the guide or asking for sensitive data.

**Tech Stack:** Markdown and GitHub Issue Forms YAML; no runtime dependencies.

---

## Files and responsibilities

- Create `CONTRIBUTING.md`: prerequisites, setup links, layer map, change checklist, and verification guidance.
- Create `.github/ISSUE_TEMPLATE/bug_report.yml`: reproducibility fields and privacy reminder.
- Create `.github/ISSUE_TEMPLATE/feature_request.yml`: user problem and desired outcome.
- Create `.github/PULL_REQUEST_TEMPLATE.md`: purpose, related issue, changes, verification, documentation, and provider/data/privacy effects.
- Modify `README.md` and `CHANGELOG.md`.

## Task 1: Write the contributor guide

- [ ] Link to `README-Developer.md` for layer boundaries and code paths, `diagnostics/README.md` for targeted checks, `docs/architecture/README.md` for diagrams/Nebula, `docs/renewal/local-configuration.md` for local settings, `docs/renewal/local-live-run.md` for the local app, and `docs/renewal/listenbrainz-provider-runbook.md` for provider setup.
- [ ] Describe first local-development steps by linking to the authoritative setup guide instead of copying its commands.
- [ ] Explain the expected small-PR shape, ask authors to report only checks they actually ran, and warn them not to include credentials or personal listening history.
- [ ] Add `CONTRIBUTING.md` to the README's project references.

## Task 2: Add GitHub contribution forms

- [ ] Write the bug form with summary, reproduction steps, expected/actual behavior, environment, and checks already tried. Include a visible instruction to remove secrets and private listening data.
- [ ] Write the feature form with the user problem, desired outcome, and affected flow. Do not promise that requested features will be accepted.
- [ ] Write the PR template with purpose, related issue, change summary, documentation impact, verification run, and explicit “none” choices for provider, privacy, and persisted-data impact.
- [ ] Use GitHub-supported Issue Forms fields and YAML syntax. Do not add a test suite or third-party package.

## Task 3: Update the changelog and verify

- [ ] Add a concise entry to `CHANGELOG.md` describing the contributor guide and issue forms.
- [ ] Check every relative Markdown link in `CONTRIBUTING.md` and the changed README sections against the repository tree.
- [ ] Parse both issue-form YAML files with an available YAML parser. If none is installed, validate indentation and supported field keys by inspection without adding a dependency.
- [ ] Run `git diff --check`; do not run application tests.
- [ ] Commit only the guide, forms, PR template, README link, and changelog.
