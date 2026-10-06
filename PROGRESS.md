# Liner Notes renewal progress

## Current state and next step
- Authorized: **Phase 1 only**, local commits on `Prism`; no push or later-phase execution.
- Baseline: `10d97f9`, verified 2026-10-06; see [baseline evidence](docs/renewal/phase-1-evidence.md).
- Active step: Phase 1 implementation and offline verification are ready for developer review.
- PRs 1C–1K have local implementation commits. Because the account and host edits overlap in
  shared files, 1C–1F and 1I are grouped in `775a838`; this differs from the planned one-PR-per-
  commit-set split and needs review. No implementation is developer confirmed.
- No implementation is developer confirmed. Verification is not confirmation.
- PR 1A planning revision: the developer approved the revised written plan, numeric evaluation
  thresholds, pilot adequacy bars and flagged interpretations on 2026-10-06. Phase 1 authorization
  is unchanged; this planning approval does not approve later-phase execution or concrete runs.
- Schema/migrations, further API/export changes, framework/CI, test removal or weakening,
  module deletion, history rewrites, pushing, merging and irreversible actions need separate approval.
  The narrowly scoped 1J dependency remediation was implemented after the developer directed
  Phase 1 to continue; no broader package upgrade was made.
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
The plan below is approved; **not approved** refers to phase execution and its remaining gates.

1. Baseline and security — **authorized**, unchanged PRs 1A–1K; implementation verified locally,
   developer confirmation pending.
2. Harness mechanics — **not approved**. Fixture-only mode with an outbound-rejecting HTTP handler;
   metrics; founder/development/held-out/synthetic sets, with held-out membership locked before any
   scoring; recorder written but not run; acquisition storage guard.
3. Honest ingestion, in memory only — **not approved**. No invented evidence; missing stays missing;
   live/recorded/synthetic provenance; unknown seeds are coverage gaps. First verify no schema change
   is needed; stop and ask if one is needed.
4. Pilot — **not approved**. Developer approval of a concrete run manifest and adequacy bars before
   one roughly 5 MB local recording covering six founder clusters plus a few seeds. Data-shape report,
   then A–D and blind listening; no tuning or promotion; every result labelled **pilot, directional**.
5. Real evaluation — **not approved**. Last.fm reply or explicit developer decision to proceed without
   one; a second approved manifest for full development/held-out recording, then scoring comparison
   and developer formula choice. Include IDF corpus size/balance/sensitivity and persisted C match
   aggregation. Freeze configuration before the one held-out evaluation. Developer alone chooses
   whether to wait or bypass real evaluation and proceed to Phase 6 with baseline A.
6. Recommendation pipeline and persistence — **not approved**. Resolve hash/ordinal/representative
   selection, retention and feedback conflicts; approve concrete schema changes before building.
   Use the Phase 5 choice, or baseline A on explicit evidence bypass. Extensible JSON compatibility
   across formula changes remains **not verified**, despite the existing JSONB mapping.
7. Worker-to-local-email slice — **not approved**. Mandatory reassessment and explicit decision
   to proceed, narrow or revise before later phases.
8. Privacy and delivery — **not approved**, with **two separate approvals**: (a) export and deletion
   verification; (b) scheduling, unsubscribe enforcement, cancellation and delivery recovery.
9. Reproducibility and release preparation — **not approved**. Docker/Compose, CI and Last.fm written
   public-page approval. Release stays blocked until that approval is documented.

Number mapping: old 2 -> new 2-5 (split), old 3 -> new 6, old 3A -> new 7,
old 4 -> new 5, old 5 -> new 8, old 6 -> new 9.

Detailed future-phase constraints and unresolved proposals: [renewal contracts](docs/renewal/contracts.md).
Numeric thresholds, pilot bars and manifest format there are approved for planning, not permission
to record or score real data. Concrete run manifests retain separate approval. Recordings and
evaluation reports must remain local, gitignored and untracked.
Binding-spec coverage: integrations/imports in infrastructure §§1, 5.4, 6.2, 7.1, 8.2;
Docker/deployment §13; hardening §15. CI is not explicitly specified.

## Per-PR status
| PR / local commit-set identifier | Concern | Status | Evidence | Developer confirmation |
|---|---|---|---|---|
| 1A / renewal-1a | Progress/specification/copy reconciliation | earlier reconciliation verified; revised written plan approved | [1A](docs/renewal/phase-1-evidence.md#1a) | plan approved 2026-10-06; implementation confirmation pending |
| 1B / renewal-1b | Secret defaults and rotation instructions | verified; revised for explicit local defaults in `a87d86a` | [1B](docs/renewal/phase-1-evidence.md#1b) | pending |
| 1C / renewal-1c | Refresh storage, expiry, rotation, replay | implemented in grouped `775a838`; offline tests pass | [1C–1I](docs/renewal/phase-1-evidence.md#1c-1i) | pending |
| 1D / renewal-1d | Login lockout and abuse throttling | implemented in grouped `775a838`; offline tests pass | [1C–1I](docs/renewal/phase-1-evidence.md#1c-1i) | pending |
| 1E / renewal-1e | Atomic registration and rollback | implemented in grouped `775a838`; PostgreSQL test passes | [1C–1I](docs/renewal/phase-1-evidence.md#1c-1i) | pending |
| 1F / renewal-1f | Physical deletion and access invalidation | implemented in grouped `775a838`; PostgreSQL tests pass | [1C–1I](docs/renewal/phase-1-evidence.md#1c-1i) | pending |
| 1G / renewal-1g | Feedback validation and atomic replacement | implemented in `43e3fc9`; PostgreSQL tests pass | [1C–1I](docs/renewal/phase-1-evidence.md#1c-1i) | pending |
| 1H / renewal-1h | Browser/session security and refresh | implemented in `88d53a3`; browser check passes | [1C–1I](docs/renewal/phase-1-evidence.md#1c-1i) | pending |
| 1I / renewal-1i | Transport, CORS and security headers | implemented in grouped `775a838`; PostgreSQL host tests pass | [1C–1I](docs/renewal/phase-1-evidence.md#1c-1i) | pending |
| 1J / renewal-1j | Dependency remediation | implemented in `cf09a61`; audit clear, NU1510 warning remains | [1J](docs/renewal/phase-1-evidence.md#1j) | pending |
| 1K / renewal-1k | Tracked build artifacts | 816 removals committed in `4f4d24a` | [1K](docs/renewal/phase-1-evidence.md#1k) | pending |

Local commit subjects use the identifiers shown above; 1C–1F/I share one commit. No PR has been published.

## Open decisions
- Track selection: versioned weekly SHA-256 tie-break and upstream-ranked representative proposals
  are alternatives, neither adopted. Within-artist popularity preference requires explicit approval.
- Scoring alternatives A–D, stoplists, IDF corpus and weights require real development recordings
  and explicit adoption approval, except baseline A if the developer explicitly bypasses real evidence.
  No catalog conclusions from synthetic data. Pilot evidence cannot promote or tune a formula.
- Correctness gates, held-out guardrails against A and blind-rating margin are approved as written;
  changes require review before real scoring. Diversity and novelty are formula-selection tie-breakers,
  never substitutes for ratings; the approved evaluation formulas retain their novelty component.
- Phase 2 placement must follow V1_FolderStructure.md; any new project/folder deviation needs approval.
  Evaluation depends on Application/Domain only; the harness has no live source. Clarify production
  acquisition against the recorder-only live-call rule before Phase 6 implementation.
- Retention (24h cache, 30d recordings/backups, 12mo recommendations) is proposed, not approved.
- Shared storage inventory/acquisition guard and alert-and-degrade policy are future work;
  actual persisted complete-breakdown sizes and supported capacity are **not verified**.
- Phase 1 rejects contradictory sentiment/rating pairs (Liked 1–3, Disliked 7–10) and
  preserves familiarity independently. Phase 6 still needs a full conflict/retention contract.
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
