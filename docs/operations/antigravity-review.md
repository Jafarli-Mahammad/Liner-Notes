# Antigravity push reviews

`.github/workflows/agy-review.yml` runs on every branch and tag push after the
workflow is pushed to that revision. It also supports manual Actions runs.
It uses Antigravity CLI 1.5.4, Gemini `gemini-3.8-flash-high`, and high reasoning
effort. The CLI archive and GitHub actions are pinned; the archive checksum is
verified. Jobs run on a Linux x64 self-hosted runner labeled `agy-login`, using
that operating-system user's existing Antigravity login.

## Enable

1. On the runner machine, sign in interactively with `agy` as the user who will
   run the GitHub runner. Verify `agy models` lists `gemini-3.8-flash-high`.
2. Open the repository's **Settings → Actions → Runners → New self-hosted runner**.
   Follow GitHub's generated Linux x64 download/registration commands, keeping
   the runner installation outside this source checkout. During registration,
   add the custom label `agy-login`.
3. Start the registered runner with `./run.sh` from its installation directory
   as the same logged-in user, in the same desktop/keyring session. Keep the
   machine awake and the runner running. A service running as another user or
   without access to the unlocked credential store cannot reuse your login.
4. Push the workflow, then inspect **Actions → Antigravity push review**. If no
   matching runner is online, jobs remain queued. Reauthenticate interactively
   on the runner if Antigravity reports an expired or missing session.

No API key or repository authentication secret is required. Login credentials
stay on the runner; the workflow does not export them or replace your settings.
GitHub-hosted `ubuntu-*` runners cannot reuse this machine's existing login.
Runner registration and an end-to-end Actions run still need verification.
See GitHub's [runner setup instructions](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners).

Only trusted users should be able to push branches or change workflows using
this runner: workflow steps execute as its operating-system user. This workflow
does not run on pull requests from forks. Restrict the runner to this repository.

Find findings in the job summary and the `antigravity-review-*` artifact,
retained for 14 days. Missing credentials or a model/CLI failure fail the job;
review findings are advisory and do not fail it. No comments, edits or commits
are posted by the reviewer. The workflow has only `contents: read` permission
and does not persist the checkout credential.

## Review scope and limits

Pushes compare the event's exact before/after commits, including non-ancestor
force-pushes. New branches/tags compare the entire new tree to the empty tree;
manual runs compare the selected revision to its first parent (or empty tree
for a root commit). Unavailable historical objects fail visibly. Ref deletions
have no new code to review and are skipped.

The input includes the diff and committed changed text files, never dirty local
files. Binary files are explicitly identified as unreviewed. Inputs above 1 MB
fail instead of silently truncating; split the push or review it manually.
Unchanged callers and dependencies are outside the supplied context, so this
review complements tests, profiling and security scanners.

The agent runs in plan mode from a temporary directory, using a dedicated custom
agent that requests no tools, command execution off, and no MCP/customization
inheritance. Existing login settings are preserved. A local login smoke test
returned the expected response without any tool calls, but the CLI still
advertised its default tools despite the empty allowlist. These settings are
not an operating-system security boundary. Only run trusted pushes on this
account. Any review stream containing tool activity fails validation; this
detects activity after execution, rather than preventing it. Source is supplied
as untrusted data with instructions to ignore embedded commands and perform
only a static snapshot review.
Source is sent to Google's configured Gemini service for analysis; never commit
secrets. The review has a 10 minute CLI limit and a 15 minute job limit.

Official references: [CI/headless operation](https://antigravity.google/docs/cli/headless/),
[login authentication](https://antigravity.google/docs/cli/install),
[custom agent controls](https://antigravity.google/docs/build-plugins), and
[CLI releases](https://github.com/google-antigravity/antigravity-cli/releases).
