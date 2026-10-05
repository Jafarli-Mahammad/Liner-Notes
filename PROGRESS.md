# Liner Notes renewal progress

## Current state and next step
- Authorized: **Phase 1 only**, separate local commit sets on `Prism`.
- Baseline: `10d97f9`, verified 2026-10-06; see [baseline evidence](docs/renewal/phase-1-evidence.md).
- Active step: PR 1A documentation reconciliation; security fixes follow with red/green checks.
- No implementation is developer confirmed. Verification is not confirmation.
- Schema/migrations, API/export changes, dependencies/framework/CI, test removal or weakening,
  module deletion, history rewrites, pushing, merging and irreversible actions need separate approval.
- Product decisions and status live here; preferences/gotchas in [MEMORY](MEMORY.md);
  release history in [CHANGELOG](CHANGELOG.md). Older conflicting memory notes are not current policy.

## Decisions and reasons
- Six layers: Domain, Application, DataAccess, Infrastructure, Presentation, Worker.
  Domain is pure; Application orchestrates; provider details remain in Infrastructure.
- V1 is popularity-neutral. Existing popularity penalties are legacy implementation, not adopted policy.
  No custom ML or claim of owning upstream similarity data.
- Known/disliked tracks are excluded; sibling tracks stay eligible. Negative feedback never subtracts
  artist tags. Replacing a like removes its positive contribution. Familiarity is independent of rating.
- Synthetic fixtures test mechanics only. Real recordings require a separately approved run,
  remain local and gitignored, and must never be committed. No secret-bearing URLs in logs.
- User-facing taste/account records must be inspectable/exportable; credential/security internals
  are outside the public export. Exact inventory and export changes require review.
- Physical deletion must remove account-owned data and invalidate access; shared catalog records
  and backups need explicit documented boundaries. No claim of legal compliance from a soft-delete flag.
- Nothing is purchased or provisioned without approval. No public deployment or external email sending.
- Last.fm discovery is in scope for the future pipeline; ListenBrainz and both username/history imports
  are deferred. Streaming search links do not constitute Spotify API integration.
- Email: unsubscribe, HTML/plaintext, text attribution and ordinary links; no remote images, fonts,
  CSS resources, tracking pixels, or other automatic third-party requests.

## Renewal phases
1. Baseline and security — **authorized**, in progress.
2. Offline evaluation and recording preparation — **not approved**.
3. Recommendation pipeline — **not approved**.
3A. Worker-to-local-email slice and mandatory reassessment — **not approved**.
4. Scoring evaluation — **not approved**; never starts automatically after 3A.
5. Privacy and delivery completion — **not approved**.
6. Reproducibility and release preparation — **not approved**.

Detailed future-phase constraints and unresolved proposals: [renewal contracts](docs/renewal/contracts.md).
Binding-spec coverage: integrations/imports in infrastructure §§1, 5.4, 6.2, 7.1, 8.2;
Docker/deployment §13; hardening §15. CI is not explicitly specified.

## Per-PR status
| PR / local commit-set identifier | Concern | Status | Evidence | Developer confirmation |
|---|---|---|---|---|
| 1A / renewal-1a | Progress/specification/copy reconciliation | verified—awaiting confirmation | [1A](docs/renewal/phase-1-evidence.md#1a) | pending |
| 1B / renewal-1b | Secret defaults and rotation instructions | not started | — | pending |
| 1C / renewal-1c | Refresh storage, expiry, rotation, replay | not started | — | pending |
| 1D / renewal-1d | Login lockout and abuse throttling | not started | — | pending |
| 1E / renewal-1e | Atomic registration and rollback | not started | — | pending |
| 1F / renewal-1f | Physical deletion and access invalidation | not started | — | pending |
| 1G / renewal-1g | Feedback validation and atomic replacement | not started | — | pending |
| 1H / renewal-1h | Browser/session security and refresh | not started | — | pending |
| 1I / renewal-1i | Transport, CORS and security headers | not started | — | pending |
| 1J / renewal-1j | Dependency remediation | not started; approval gate | — | pending |
| 1K / renewal-1k | Tracked build artifacts | not started | — | pending |

Commit-set identifiers are the unique prefixes in local commit subjects; no PR has been published.

## Open decisions
- Track selection: versioned weekly SHA-256 tie-break and upstream-ranked representative proposals
  are alternatives, neither adopted. Within-artist popularity preference requires explicit approval.
- Scoring alternatives A–D, stoplists, IDF corpus and weights require real development recordings
  and explicit adoption approval. No catalog conclusions from synthetic data.
- Retention (24h cache, 30d recordings/backups, 12mo recommendations) is proposed, not approved.
- Shared storage inventory/acquisition guard and alert-and-degrade policy are future work;
  actual persisted complete-breakdown sizes and supported capacity are **not verified**.
- Feedback conflicts between categorical sentiment and numeric rating need an explicit contract.
- Dependency, schema, API/export and test-contract changes must be presented concretely for review.

## Open Last.fm questions
- Azerbaijan/EEA territorial conditions, required consents and cross-border transfers.
- Recording redistribution, benchmark publication and research-use permission.
- Written public-page approval; attribution-button placement and permission for local hosting.
- Text-only email attribution acceptability is **not verified**.
- Current terms do not specify a universal numeric request allowance. Four/five requests per second
  in older files are not verified allowances. One request/second is only proposed conservative pacing.
- [Terms](https://www.last.fm/api/tos) and
  [track endpoint](https://www.last.fm/api/show/artist.getTopTracks) checked 2026-10-06.
- Last.fm enquiry remains draft only; repository URL must be confirmed before any sending.

## Historical baseline
- The previous implementation includes scorer/domain tests, Identity/JWT controllers, EF persistence,
  Last.fm client/fixtures, and a static dashboard. Worker scheduling/delivery is not complete.
- Prior agents' completion, privacy, coverage and compliance claims are not carried forward.
- The old coverage report has hybrid fallback and hard-coded conclusions; it cannot establish live
  provenance or musical quality. See its [qualification](docs/coverage-spike-founder-seeds.md).
- Runtime targets .NET 10; several packages are 9.x. No dependency migration is implied by this document.
