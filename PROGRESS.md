
# Liner Notes renewal progress

## Current state and next step
- Authorized: **Phase 1–4, Phase 6 and Phase 7 local work** on `Prism`; the developer requested Phase 2
  execution and approved its exact membership and offline test replacement on 2026-10-07,
  then requested Phase 3 and Phase 4 implementation. Phase 4's concrete live-run manifest
  still needs separate approval. Phase 7 was requested on 2026-10-11, with working local
  unsubscribe, one opt-out column and matching API/export changes selected by the developer.
  Its [concrete design](docs/renewal/phase-7-review.md) was approved on 2026-10-11.
  The [implementation plan](docs/renewal/phase-7-plan.md) and sequential execution were approved
  on 2026-10-11; implementation is locally verified, developer confirmation pending.
  No push or Phase 5/8–9 execution is authorized.
- Baseline: `10d97f9`, verified 2026-10-06; see [baseline evidence](docs/renewal/phase-1-evidence.md).
- Active step: developer confirmation and proceed/narrow/revise reassessment of the
  [Phase 7 implementation evidence](docs/renewal/phase-7-evidence.md), using its
  [runnable local QA procedure](docs/renewal/phase-7-local-run.md).
  Developer review/confirmation of the [Phase 6 implementation evidence](docs/renewal/phase-6-evidence.md)
  remains pending.
  The developer approved the design and explicitly requested implementation on 2026-10-10,
  including its JSON/API/export, selection, feedback and storage contracts. Local verification
  passed on 2026-10-10; developer confirmation of the implementation remains pending.
  Phase 4's concrete live-run manifest still needs approval with a fresh reconciled inventory;
  Phase 1–3 confirmation remains pending. See [Phase 4 evidence and run procedure](docs/renewal/phase-4-evidence.md).
- PRs 1C–1K have local implementation commits. Because the account and host edits overlap in
  shared files, 1C–1F and 1I are grouped in `775a838`; this differs from the planned one-PR-per-
  commit-set split and needs review. No implementation is developer confirmed.
- No implementation is developer confirmed. Verification is not confirmation.
- PR 1A planning revision: the developer approved the revised written plan, numeric evaluation
  thresholds, pilot adequacy bars and flagged interpretations on 2026-10-06. Phase 1 authorization
  was unchanged by that planning approval; the subsequent 2026-10-07 instruction authorizes
  Phase 2 execution. The subsequent Phase 3 request authorizes its transient ingestion scope.
  The Phase 4 request authorizes pilot tooling; concrete live recording runs still require
  separate manifest approval and explicit shared-storage reconciliation. On 2026-10-09,
  the developer requested Phase 6 and explicitly approved bypassing Phase 5 for provisional
  baseline A. Recommendation quality remains unverified and real evaluation remains open.
- Schema/migrations, further API/export changes, framework/CI, test removal or weakening,
  module deletion, history rewrites, pushing, merging and irreversible actions need separate approval.
  The narrowly scoped 1J dependency remediation was implemented after the developer directed
  Phase 1 to continue; no broader package upgrade was made.
- Product decisions and status live here; preferences/gotchas in [MEMORY](MEMORY.md);
  release history in [CHANGELOG](CHANGELOG.md). Older conflicting memory notes are not current policy.

## Decisions and reasons
- Six layers: Domain, Application, DataAccess, Infrastructure, Presentation, Worker.
  Domain is pure; Application orchestrates; provider details remain in Infrastructure.
- V1 is popularity-neutral. Phase 6 generation uses baseline A. Existing popularity penalties
  remain in the legacy scorer for compatibility and are not used by generation.
  No custom ML or claim of owning upstream similarity data.
- Provisional baseline A is authorized for Phase 6 following an explicit Phase 5 evidence bypass
  on 2026-10-09. Quality remains **not verified**; evaluation and later formula choice remain open.
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
2. Harness mechanics — **authorized; implementation verified locally, developer confirmation pending**.
   Fixture-only harness and rejecting HTTP handler; metric implementations; approved founder,
   development, held-out and synthetic sets locked before harness ranking; separate recorder written
   but not run live; acquisition guard verified with fake inventories. The approved legacy founder
   test now runs offline and writes no tracked report. Evidence: 22 focused .NET checks, 21 fake
   recorder checks, full suite 216 passed / 9 existing PostgreSQL skips. **Mechanics only, no catalog
   conclusions.** See [design and approved membership](docs/renewal/phase-2-design.md) and
   [verification/limitations](docs/renewal/phase-2-evidence.md).
3. Honest ingestion, in memory only — **authorized; implementation verified locally, developer confirmation pending**.
   Schema preflight confirmed transient models are unmapped; no schema or public API changes.
   Removed invented tags, listener estimates and matches; preserved missing/invalid values, all
   discovery paths, original positions, attribution, response hashes/times and origins. Unknown
   fixture seeds and failed HTTP requests produce gaps; no hybrid substitution. Recorded replay
   consumes injected immutable byte snapshots without HTTP or file discovery. Evidence: all 55
   Infrastructure checks pass, including four baseline failures; full suite 256 passed / 9 existing
   PostgreSQL skips, with 77 Domain checks passing. **Mechanics only, no catalog conclusions.**
   See [design](docs/renewal/phase-3-design.md) and [verification/limitations](docs/renewal/phase-3-evidence.md).
4. Pilot — **tooling authorized and verified locally; live pilot not executed**. Frozen untuned
   A–D evaluation, complete contributions/provenance, replay bridge, shape gates, storage accounting,
   concealed six-profile blind pool and complete-rating arithmetic are implemented. Verification:
   21 new .NET checks; full suite 277 passed / 9 existing PostgreSQL skips; 32 fake recorder/operator
   checks; CLI metadata and fabricated-input smoke checks. **Mechanics only, no catalog conclusions.**
   Concrete manifest approval, current shared-storage inventory, actual recording, data-shape results
   and developer blind ratings remain pending. Failed yield/tag/integrity bars stop before comparisons;
   missing match/popularity bars mark their checks unassessable. No tuning/promotion. Real outputs
   must say **pilot, directional**. See [evidence and run procedure](docs/renewal/phase-4-evidence.md).
5. Real evaluation — **not approved**. Last.fm reply or explicit developer decision to proceed without
   one; a second approved manifest for full development/held-out recording, then scoring comparison
   and developer formula choice. Include IDF corpus size/balance/sensitivity and persisted C match
   aggregation. Freeze configuration before the one held-out evaluation. Developer alone chooses
   whether to wait or bypass real evaluation and proceed to Phase 6 with baseline A. On 2026-10-09
   the developer explicitly chose a provisional baseline-A bypass; real evaluation remains deferred.
6. Recommendation pipeline and persistence — **design approved; implementation verified locally, developer confirmation pending**.
   Provisional baseline A was explicitly approved with quality unverified. The concrete
   JSON/API/export, ordinal one-artist selection, feedback and storage-mechanics contracts were approved
   with the implementation instruction on 2026-10-10. The separately approved
   `Users.PopularityPenaltyEnabled` setting, migration, settings API/UI and export field are not
   implemented and were omitted, not sequenced to a later phase. The enforced
   1,000,000,000-byte (1 GB) storage threshold is a temporary unresolved placeholder, not a real
   capacity decision; the real number is set at build-order step 9 once hosting is chosen.
   A Development-only [manual QA frontend and run guide](docs/renewal/manual-qa.md) covers the
   implemented user-facing flows; developer browser review remains pending. See [approval
   status and evidence](docs/renewal/phase-6-evidence.md).
   Implemented recorder-fed bounded generation, pure materialization/baseline A, complete versioned
   snapshots, canonical ISO-week idempotency, PostgreSQL atomic persistence, storage/expiry guards,
   feedback conflict/familiarity rules and export 2.0 with stable history pages. No SQL migration.
   Old/new/unknown-field JSON and unsupported-formula inspection/replay behavior are verified.
   Full suite: **337 passed / 0 failed / 0 skipped**, including local PostgreSQL checks.
   **Mechanics only, no catalog conclusions.** Quality and supported capacity remain unverified.
   See [evidence and configuration](docs/renewal/phase-6-evidence.md).
7. Worker-to-local-email slice — **design and plan approved; implementation verified locally,
   developer confirmation pending**. The authorized Phase 6 status/cutoff and Development
   manual-QA follow-ups were reconciled first. Implemented explicit offline Worker actions,
   persisted HTML/plaintext rendering, bounded private MIME capture, authenticated receipts,
   exact recorded-input permission, storage accounting, retry/restart and account/row locks.
   Working local unsubscribe adds one nullable opt-out column, isolated persisted capabilities,
   API/export 2.1 and honest ownership-based copy cleanup after account deletion.
   Final suite: **388 passed / 0 failed / 0 skipped**, including PostgreSQL migration,
   file/SQL failure windows, cancellation, concurrency, token isolation and logging checks.
   Actual separate Worker processes captured empty/five-pick fixture lists; Playwright MCP
   verified read-only GET, confirmation POST and suppressed later capture. Export, restart,
   account cleanup and the [runnable QA launcher](docs/renewal/phase-7-local-run.md) were exercised.
   See [per-approval evidence and reassessment](docs/renewal/phase-7-evidence.md),
   [approved review](docs/renewal/phase-7-review.md) and [plan](docs/renewal/phase-7-plan.md).
   **Mechanics only, no catalog conclusions.** Popularity stays deferred; the 1 GB stop
   remains temporary/unresolved. Mandatory explicit **proceed, narrow or revise** decision
   remains required before later phases; Phase 8a/8b approvals are separate.
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
- Set the real shared storage limit at build-order step 9 once hosting is chosen. The current
  1,000,000,000-byte (1 GB) cutoff is a temporary hard-stop placeholder, not a capacity decision.
- Phase 6 adopts canonical ordinal track-key ties and the highest scoring eligible pick per
  artist, up to five artists. Weekly hash and upstream-ranked representative proposals remain
  unadopted. Within-artist popularity preference requires separate approval.
- Scoring alternatives B–D, stoplists, IDF corpus and tuning require real development recordings
  and explicit adoption approval. Baseline A is provisionally approved under the explicit
  2026-10-09 evidence bypass; its quality remains unverified.
  No catalog conclusions from synthetic data. Pilot evidence cannot promote or tune a formula.
- Correctness gates, held-out guardrails against A and blind-rating margin are approved as written;
  changes require review before real scoring. Diversity and novelty are formula-selection tie-breakers,
  never substitutes for ratings; the approved evaluation formulas retain their novelty component.
- Phase 2 and Phase 4 follow existing test folders and `scripts/`; no tracked evaluation project or package was added.
  Evaluation depends on Application/Domain only; the harness has no live source. Phase 6 implements
  the [approved recorder-fed design](docs/renewal/phase-6-review.md).
- Phase 6 adopts no automatic history deletion. Active seeds/feedback/familiarity remain until
  changed or account deletion; recorded generation requires the approved manifest's expiry.
  The older 24h/30d/12mo retention suggestions remain unadopted.
- Phase 2's offline acquisition guard and recorder inventory checks are implemented;
  Phase 4 adds bounded local analysis/report writes and an explicit refreshed inventory gate.
  Phase 6 adds an exclusive local batch lease, five-minute inventory and artifact/database revision
  checks, configured overhead reservation and transactional actual-byte checks. Shared production
  coordination/alerting remain later work. Synthetic snapshot sizes are measured; supported
  capacity and real-data size distributions are **not verified**.
- Phase 1 rejects contradictory sentiment/rating pairs (Liked 1–3, Disliked 7–10) and
  preserves familiarity independently. Phase 6 implements latest-feedback materialization with
  an ordinal ID tie-break and preserves familiarity across later ratings. `None` without a rating
  explicitly resets that recommendation's rating and familiarity.
- Dependency, schema, API/export and test-contract changes must be presented concretely for review.

## Open Last.fm questions
- Azerbaijan/EEA territorial conditions, required consents and cross-border transfers.
- Recording redistribution, benchmark publication and research-use permission.
- Written public-page approval; attribution-button placement and permission for local hosting.
- Text-only email attribution acceptability is **not verified**.
- Current terms do not specify a universal numeric request allowance. Four/five requests per second
  in older files are not verified allowances. One request/second is only proposed conservative pacing.
- [Terms](https://www.last.fm/api/tos) and similarity/tag/track endpoint documentation refreshed
  2026-10-07; see [Phase 3 references](docs/renewal/phase-3-evidence.md#authoritative-documentation).
- Last.fm enquiry remains draft only; repository URL must be confirmed before any sending.

## Historical baseline
- The previous implementation includes scorer/domain tests, Identity/JWT controllers, EF persistence,
  Last.fm client/fixtures, and a static dashboard. Worker scheduling/delivery is not complete.
- Prior agents' completion, privacy, coverage and compliance claims are not carried forward.
- The old coverage report has hybrid fallback and hard-coded conclusions; it cannot establish live
  provenance or musical quality. See its [qualification](docs/coverage-spike-founder-seeds.md).
- Runtime targets .NET 10; several packages are 9.x. No dependency migration is implied by this document.
