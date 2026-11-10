# Antigravity push reviews

`.github/workflows/agy-review.yml` runs on every branch and tag push after the
workflow is pushed to that revision. It also supports manual Actions runs.
It uses Antigravity CLI 1.5.4, Gemini `gemini-3.8-flash-high`, and high reasoning
effort. The CLI archive and GitHub actions are pinned and checksum verified.

## Enable

Add a repository Actions secret named `GEMINI_API_KEY` containing a Gemini API
key authorized for the requested model. Do not put the key in Git or an issue.
The workflow configures `modelProvider: gemini`; a key alone is insufficient.
API authentication uses Gemini API quota/billing, separately from a local
Antigravity account session. No paid plan or quota purchase is configured here.
Model access with your CI key remains to be verified by the first workflow run.

Push this configuration, then inspect **Actions → Antigravity push review**.
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

The agent runs from an empty temporary directory with separate settings,
commands, file access, URL access and MCP denied. It receives changed source
as untrusted data and is instructed not to follow embedded instructions.
Repository code, tests, plugins and agent instructions are not executed.
Source is sent to Google's configured Gemini service for analysis; never commit
secrets. The review has a 10 minute CLI limit and a 15 minute job limit.

Official references: [CI/headless operation](https://antigravity.google/docs/cli/headless/),
[API authentication](https://antigravity.google/docs/cli/install),
[permissions](https://antigravity.google/docs/permissions/?tab=cli), and
[CLI releases](https://github.com/google-antigravity/antigravity-cli/releases).
