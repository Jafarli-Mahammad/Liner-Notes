# GitHub Pages Nebula and contributor onboarding

**Status:** Draft for review
**Date:** 2026-10-27

## Goal

Give developers a public, interactive map of the Liner Notes codebase that is refreshed automatically as the default branch changes. Make the first contribution easier with one concise guide and GitHub forms that direct reports to the right information.

The Nebula is an interactive static site backed by a generated graph snapshot. It is not a live connection to a Codebase Memory server. A successful Pages deployment publishes a new snapshot for the commit that triggered it.

## Current state

- `docs/architecture/README.md` and the root and developer READMEs show a Codebase Memory graph screenshot captured on 2026-10-10.
- The Codebase Memory index available to this workspace contains 2,804 nodes and 10,789 edges, was last indexed on 2026-10-09, and is not being watched for changes. It is unsuitable as the initial published snapshot without a fresh index.
- The repository's default branch is `main`. The local checkout is on `Prism`; deployment triggers must target `main` regardless of the branch used to develop this feature.
- The repository is public. The graph must contain only repository structure and source metadata, never application user data, local configuration, or secrets.
- Codebase Memory's published native release includes its Lens browser UI. Its exact static export contract has not yet been established. The implementation must validate the pinned release's indexing and local UI data endpoints before relying on them.
- GitHub's Pages settings could not be read through the available connector. Successful publication requires Pages to use **GitHub Actions** as its source; verify and configure this as part of deployment if the available GitHub access supports it.

## User experience

- Add a clear **Explore the Nebula** link to the project and developer READMEs, opening the repository's GitHub Pages site directly in graph view for the `LinerNotes` project.
- Keep graph navigation in the browser. Include a visible generated timestamp and source commit so a reader can tell which repository revision the graph describes.
- Continue to explain that graph relationships are static analysis hints; source code and tests remain authoritative.
- If indexing or validation fails, fail the deployment. Do not publish an old graph while labeling it as current.
- Preserve the existing architecture screenshot and architecture documentation as a lightweight fallback and point-in-time reference.

## Build and deployment

Add a dedicated Pages workflow with these behaviors:

1. Run on pushes to `main`, on `workflow_dispatch`, and on pull requests for build and validation. Only `main` may deploy; reject a manual deployment selected from another ref.
2. Check out the triggering revision, install a pinned stable Codebase Memory release, and verify the release checksum before execution.
3. Index the checked-out repository on the runner. Start any required local service bound to loopback only, export the graph and viewer assets into a temporary static-site directory, and stop the service before packaging.
4. Set the site base path to `/Liner-Notes/`. The static viewer must load without calls to a hosted MCP service, source repository writes, or secrets.
5. Validate the generated data before deployment: required files and fields exist, source commit matches the workflow revision, graph counts are nonzero, and no absolute runner paths or excluded files appear in the artifact.
6. Deploy the validated artifact using GitHub Pages Actions. Keep the deployment job's permissions limited to `pages: write` and `id-token: write`; keep build permissions to repository contents read. Pin Actions to reviewed immutable revisions.
7. Use one Pages deployment concurrency group and cancel superseded runs so an older or non-default-branch run cannot replace the latest graph.

The export mechanism is a design risk. Prefer a documented Codebase Memory export format. If the pinned release exposes only its local UI API, use a version-pinned adapter and check in a small fixture for its response shape; do not scrape a changing public page or silently accept incompatible output. Record the viewer's upstream license and attribution alongside vendored assets. Re-index the source on every run; viewer asset upgrades can be handled separately from graph refreshes.

The workflow must not add, remove, or commit generated graph files back to `main`. The Pages artifact is the deployable output. A workflow failure must leave the last successful Pages deployment intact and clearly report the failed revision.

## Contributor onboarding

Add:

- `CONTRIBUTING.md`: prerequisites and first local run, a short map of the layers, links to `README-Developer.md`, diagnostics, and relevant setup/runbook pages, plus what to include in a change. Avoid copying detailed setup instructions that already have an authoritative home.
- GitHub issue forms for bug reports and feature requests. Ask for reproducible steps and expected behavior in bug reports; ask for the user problem and intended outcome for requests. Do not request secrets or personal listening data.
- A pull request template covering purpose, related issue, scope, documentation, verification performed, and any provider, privacy, or stored-data effects.

Link the contribution guide from the root README. Do not add a devcontainer or promise a “good first issue” list in this change; the existing local setup has database and provider configuration that first needs a separately validated container workflow, and issue suitability changes over time.

## Scope boundaries

- No changes to recommendation scoring, provider behavior, application data, schemas, or runtime hosting.
- No tests or product dependencies are added for this documentation and static-site feature.
- Do not publish source code to a third-party service. Index and build on GitHub's runner, then publish only the static viewer and graph data needed by Pages.
- Do not run the application's test suite for this work. Verify the static-site build and workflow directly.
- The user requested a working Pages deployment. After the local artifact and workflow are verified, use the authorized repository access to publish the change and configure Pages to use GitHub Actions if needed. If the available access cannot change that setting or the default branch, report the exact remaining step instead of claiming deployment succeeded.

## Acceptance criteria

1. A push to `main` and a manual workflow run both build a fresh graph from the triggering commit and deploy the Pages artifact after validation.
2. A pull request can validate the generated site without deploying it; manual runs selected from a non-`main` ref cannot deploy.
3. The deployed page works at the repository's Pages subpath, opens in graph view, and reports the graph's source commit and generation time.
4. Index, export, or validation errors fail before deployment and cannot publish a stale graph as current.
5. Workflow permissions are least-privilege, Actions and indexer versions are pinned, checksum verification is enforced, and no secrets are needed.
6. The graph excludes generated output, caches, local settings, and any application data; vendored viewer code retains its license and attribution.
7. The existing screenshot remains available, and both READMEs link to the deployed viewer with an accurate explanation of snapshot freshness.
8. The contribution guide and issue/PR templates use the existing documentation as their source of truth and do not ask contributors for sensitive data.

## Verification and delivery

Use two focused implementation commits:

1. Add the graph export, static viewer adapter, Pages workflow, and README links. Validate workflow YAML, exercise the pinned indexer/export against this checkout, inspect the generated artifact under `/Liner-Notes/`, and verify failure paths stop before deploy.
2. Add the contribution guide and GitHub templates, update the changelog, and check the links and template fields.

Do not run the application test suite. Before calling deployment complete, confirm GitHub Pages is configured to publish with Actions and inspect a successful Pages workflow run and the live site. A local artifact build alone is not evidence that GitHub Pages is live.

## References

- [GitHub Pages custom Actions workflow](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages)
- [Codebase Memory releases](https://github.com/DeusData/codebase-memory-mcp/releases)
- [Codebase Memory repository and license](https://github.com/DeusData/codebase-memory-mcp)
- [DevJourney Nebula example](https://jafarli-mahammad.github.io/DevJourney/?tab=graph&project=home-mahammadjafarli-source-repos-DevJourney)
