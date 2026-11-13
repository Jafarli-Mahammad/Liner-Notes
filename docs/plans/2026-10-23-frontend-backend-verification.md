# Frontend/backend verification — 2026-10-23

Implemented and locally verified; developer confirmation and deployed verification
remain pending. Browser runs used disposable loopback PostgreSQL databases and
synthetic provider fixtures. No live provider acquisition was performed.

## Reproduced and repaired

- The default, legacy taste form sent `tagName` and `initialWeight`. The API DTO
  requires `tag` and `weight`; its response was HTTP 400 with missing `Tags[n].Tag`
  validation errors. The corrected form returned HTTP 200, and account export
  contained the saved tags and artist.
- Taste, registration and login forms discarded field-level validation messages.
  They now display the API's validation messages, with detail/title fallbacks.
- Below 760px, CSS hid the sidebar footer containing the only sign-out control.
  Account controls now remain visible. A 390px browser journey clicked Sign out
  and returned to the signed-out preview.
- A browser continued executing the old taste serializer while a fresh request
  returned the corrected script. Static assets and SPA fallback HTML now send
  `Cache-Control: no-cache`, requiring revalidation of unversioned frontend files.
  This permits conditional caching; API responses retain their existing no-store
  policy. An already open page needs a reload after deployment.

## Verification

- Playwright MCP exercised guest onboarding, catalog selection, generation,
  feedback, rating/note persistence, guest downloads, registration preserving
  discoveries, account export/download, logout/login, taste navigation and mobile
  behavior. It separately reproduced the legacy failure and verified the repaired
  POST and export. Swagger navigation and the Development QA page/status returned
  HTTP 200 without page exceptions.
- `scripts/checks/guest_discovery_browser.cjs`: 12 passing journeys, including
  stale search responses, local search, explicit lookup, generated explanations,
  return visits, feedback persistence, guest export, registration, mobile layout
  and visible sign-out, explicit account import, cooldown/exhausted coverage,
  account deletion and guest clear.
- `scripts/checks/optional_taste_browser.cjs`: 9 passing grouped checks covering
  optional/skipped answers, broad taste and alias search, recording favorites,
  resumed setup, generation, feedback, recipient-free recent recommendations,
  guest attachment, mobile layout and more than 200 exported tags.
- `node scripts/checks/phase1_browser.cjs`: passed. Added a legacy request-contract
  regression and validation-message checks; updated its DOM stub for the existing
  page-heading behavior.
- Full PostgreSQL-backed solution suite after the final changes: **685 passed,
  zero failed, zero skipped** (Domain 148, DataAccess 130, Application 137,
  Presentation 80, Infrastructure 190). Five added HTTP cases verify cache policy
  for `/`, both scripts, styles and an SPA fallback route.
- `git diff --check`: passed.

## Repeat the checks

Prepare the isolated runtime using the existing
[synthetic runbook](guest-discovery-local-run.md). Set `LINER_GUEST_DEMO_URL` to
its actual address, `LINER_PLAYWRIGHT_MODULE` to an installed Playwright module
when needed, and `LINER_CHROMIUM_PATH` to a compatible installed executable when
needed. Both browser scripts accept those settings and `--approve-synthetic`.

Run solution tests with the pinned SDK, using only `LINER_PHASE1_POSTGRES` for
the disposable PostgreSQL checks. Do not inherit a running app's discovery,
storage, email or hosting environment overrides into test hosts.

The existing IDE profiles leave `GuestDiscovery:Enabled` and
`DiscoveryRuntime:RunJobs` unset. They show the legacy weekly preview; saving
taste does not generate a weekly digest on demand. Onsite generation needs the
explicit runtime configuration in the runbook. Keep legacy and enabled discovery
verification in separate databases: legacy writes without coordinated storage
invalidate an active discovery inventory when pointed at the same database.

Live recommendation quality, external email delivery and production deployment
were not verified. The Development QA page and Swagger remain Development tools.
