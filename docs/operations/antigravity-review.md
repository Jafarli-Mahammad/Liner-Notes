# Local Antigravity push reviews

The existing `.githooks/pre-push` runs a local `agy` review after its build,
test and credential checks. It uses your saved login, model
`gemini-3.8-flash-high`, and high thinking. No GitHub Actions runner or API key
is involved. Existing GitHub CI remains unchanged.

This checkout already uses `core.hooksPath=.githooks`. For a fresh clone, run:

```sh
git config core.hooksPath .githooks
```

Keep `agy` on your PATH and sign in interactively once. Each push prints the
findings and saves a Markdown report in the ignored `.linernotes/agy-reviews/`
directory. Reports cover code quality, performance and security with file/line
references. Findings are advisory; an unavailable CLI, expired login or failed
review blocks the push with diagnostics. To bypass only this review for one
push, use `SKIP_AGY_REVIEW=1 git push`. Existing `SKIP_PUSH_GATE` / `SKIP_GATE`
bypasses still skip the entire hook; Git's `--no-verify` also bypasses hooks.

The review uses the exact before/after revisions Git supplies to the hook,
including explicit refspecs, multiple refs and force-pushes. Deleted refs are
skipped. A new remote ref compares its entire tree against the empty tree.
Missing remote objects cause a visible failure; fetch the remote and retry.
Manual review of a commit range is also available:

```sh
python3 scripts/checks/agy_review.py <before-full-sha> <after-full-sha> /tmp/agy-review.md
```

Input includes the diff and committed changed text files, excluding dirty local
files. Binary files are identified as unreviewed. Inputs over 1 MB fail without
silently truncating; split the push or review manually. Unchanged callers are
outside the supplied context. This complements tests, profiling and scanners.

The agent runs in plan mode from a temporary directory with a dedicated custom
agent requesting no tools and no inherited MCP/customizations. Login settings
are preserved. The CLI still advertises default tools despite its empty tool
list, so this is not operating-system isolation. The prompt requires static
review without tool use; any observed tool activity rejects the report after
the fact. A login/model smoke test completed without tools. Source is sent to
Google for review, and each review has a 10 minute CLI limit.

References: [headless mode](https://antigravity.google/docs/cli/headless/),
[login](https://antigravity.google/docs/cli/install), and
[custom agents](https://antigravity.google/docs/build-plugins).
