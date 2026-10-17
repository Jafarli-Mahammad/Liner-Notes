# Guest discovery implementation plan

Date: 2026-10-16

Status: **Design and sequential execution approved by the developer on 2026-10-16. C1–C3, C4 lifecycle and C5 catalog and C6 taste/preparation mechanics are implemented and locally verified; C7–C10 and the documented integrated checks remain. See the evidence ledger for actual outcomes and open gates.**

Design: [Guest discovery and immediate recommendations](../specs/2026-10-16-guest-discovery-design.md).

## Execution contract

- Execute sequentially in the current session unless the developer chooses otherwise. Do not create sub-agents, install workflow skills, push, deploy, buy services, or send external messages as part of this plan.
- Eight stages are expected to produce ten focused implementation commits. Stages 2 and 6 have separate migration/accounting checkpoints. Revise the estimate if the resource probe reveals a concrete issue; do not quietly omit functionality to meet it.
- Establish tests around ownership, migrations, lifecycle, concurrency, and provider boundaries before changing them. Use meaningful behavior tests; do not add tests merely to mirror trivial code.
- Local catalog typing must make zero upstream requests. Ordinary offline verification must deny outbound HTTP. Synthetic data establishes mechanics only.
- Real acquisition requires the source permissions, bounded manifest, and existing project gates applicable to that source. Approval of this design is not approval to reuse evaluation data, enable live defaults, or make production-readiness claims.
- Each commit requires its focused checks and applicable existing regressions. If required verification fails or cannot run, report it and ask before committing. Do not mark a feature confirmed until the developer confirms it works.
- Preserve the unrelated existing `AuthService.cs` edit. Inspect staged content and use focused hunks if an implementation later needs that file. Never stage the entire worktree indiscriminately.
- No dependency change is planned. Verify current stable releases and compatibility before any justified addition/update; keep MediatR 12.5.0 as the approved licensing exception.

## Preflight and baseline

- [ ] Read `AGENTS.md`, `PROGRESS.md`, `MEMORY.md`, the approved design, and relevant OPEN workflow observations. Search codex-mem for newer decisions; repository code takes precedence over stale notes.
- [ ] Inspect `git status --short`, current branch, staged content, existing local processes, and the selected checkout. Capture the unrelated edit's baseline without committing it.
- [ ] Verify `dotnet --version` matches `global.json` and that the local test database is disposable. Use the repository's pinned local PostgreSQL image; do not upgrade an existing volume implicitly.
- [ ] Obtain runtime secrets through the existing private mechanisms. Do not print connection strings, signing secrets, session credentials, or expanded Compose configuration.
- [ ] Run the baseline build/tests using the common commands at the end of this plan. Set `LINER_PHASE1_POSTGRES` through a private environment; confirm database tests actually run rather than being skipped. Record failures predating the feature separately.
- [ ] Inventory actual database, managed artifact, index, WAL, runtime/image, and filesystem use. Do not interpret `GenerationStorage__StopBytes` as total VPS disk capacity.
- [ ] Maintain a local evidence report with commands, environment, outcomes, provenance, and unresolved issues. New reports must distinguish fixture mechanics, real catalog evidence, constrained-host measurements, and developer confirmation.

## Cross-stage contracts

### Ownership and persistence

New names below describe planned files/types; they are not claims that these files already exist.

| Record | Ownership and invariants |
| --- | --- |
| `ListenerProfile` | Stable profile ID; optional registered `UserId`; revision; created/activity/expiry timestamps; guest/account state. Unique account association. |
| `GuestSession` | Credential hash, profile ID, expiry/revocation and audit timestamps. Raw credential exists only in the browser cookie. |
| `DiscoveryBatch` | Profile, stable request ID, initial/on-demand/weekly kind, input revision, evidence versions, completion time, and ranked recommendations. |
| General recommendation | Stable existing recommendation ID where migrated; batch/profile/track, rank, score snapshot, and feedback. |
| `GenerationJob` | Profile, idempotency key, requested revision, job kind, state, lease, attempts, next-attempt time, safe progress/error codes. At most one unfinished generation per profile. |
| Compact recommendation history | Profile/track identities and timestamps needed for suppression. A recommendation is not a claimed listen. |
| Public catalog/evidence | Canonical provider IDs, normalized metadata, source/license references, acquisition time, hashes, freshness and cache policy. No private guest ownership implied. |

Store the minimum private information needed. Every added table/column needs an explicit inventory classification and coverage by the relevant export/deletion/retention boundary in the same stage, not at the end of the feature.

Introduce job contracts/schema before their consumers. New routes that require an unfinished consumer stay disabled until that consumer is available; do not accept jobs that cannot be processed. Every intermediate managed writer must use the existing conservative storage guard and updated inventory. C7 improves coordination/accounting; it is not the first point at which storage protection applies.

### Proposed HTTP surface

Implement these routes through thin controllers and MediatR handlers. Request ownership always comes from authenticated account or validated guest context; do not accept an authoritative profile/user ID from the caller.

| Route | Contract |
| --- | --- |
| `GET /api/discovery/session` | Current session/profile summary and antiforgery bootstrap. Does not allocate a listener profile merely for browsing. |
| `POST /api/discovery/session` | Lazily create guest context after a deliberate action. Set the protected cookie; do not return its credential in JSON. |
| `GET /api/catalog/search` | Local-only artist/album search: bounded `kind`, query, cursor, and results. |
| `POST /api/catalog/lookup` | Explicit lookup request, after guest/account context and request checks; returns a lookup job or cached result. |
| `GET /api/catalog/lookups/{id}` | Owner-scoped lookup progress/result. Acquisition can be coalesced while request ownership remains separate. |
| `POST /api/discovery/batches` | Initial request includes validated selections; Discover more uses current inputs. Save any accepted input changes and enqueue atomically. Require a client idempotency key and enforce input revision/cooldown. |
| `GET /api/discovery/jobs/{id}` | Owner-scoped safe job status. |
| `GET /api/discovery/batches/latest` | Current saved batch; does not generate or contact upstream. |
| `GET /api/discovery/batches/{id}` | Owner-scoped results and stored explanations. |
| `POST /api/discovery/recommendations/{id}/feedback` | Reuse existing feedback semantics, applied to the resolved profile. |
| `POST /api/discovery/claim` | Authenticated attachment/import with proof of guest ownership; transactional and explicit when an account already has a profile. |
| `GET /api/discovery/export` | Export the resolved guest/account profile's complete reviewed discovery data. Preserve the existing account export route. |
| `DELETE /api/discovery/session` | Clear unclaimed guest data and revoke its credential. Must never delete an authenticated account implicitly. Preserve existing account deletion behavior. |

Use `202` for accepted pending work, `200` for existing/completed reads and idempotent replays, safe ownership failures without revealing another profile's records, and structured retryable responses for cooldown/overload. Return `Retry-After` when a meaningful bounded delay is known. No arbitrary path, origin, provider credential, scoring configuration, or approval manifest is accepted from a browser.

### Configuration defaults to implement and validate

- Guest inactivity: seven days, with immediate authorization expiry and bounded physical cleanup.
- Generation cooldown: 30 seconds between accepted jobs, excluding the first request; replay of an idempotency key does not reset it.
- Selections: at least one valid seed; up to five artists/albums combined plus five style tags; UI encourages three favorites. Existing legacy endpoints can retain their documented contract through an adapter while the new flow uses these rules.
- Search: around 150 ms debounce, ten results per type/page, bounded query length/duration and query-specific indexes.
- Polling: about two seconds with backoff when hidden and cancellation of obsolete browser requests.
- Aggregate application memory-cache target: 64 MiB in byte-accounted caches. Initial catalog/index and prepared-evidence targets: 256 MiB each, subject to stage-1 measurements and the existing managed-write guard.
- Start with one local generation lane and one coordinated acquisition lane. Upstream waiting must not block prepared local scoring. There is no maximum registered/guest user count.

## Stage 1 / commit C1 — Measure catalog feasibility and resource costs

C1 outcome: [source policy](../../renewal/guest-discovery-sources.md) and [offline evidence](../../renewal/guest-discovery-evidence.md) are written. Fifteen probe tests and three synthetic PostgreSQL measurements passed; baseline .NET tests passed 516/516 with zero skips. Real catalog acquisition/coverage is not claimed. Proceed to C2 under the existing execution approval.

**Create:**

- `docs/renewal/guest-discovery-evidence.md` — evidence ledger and source-policy decisions.
- `docs/renewal/guest-discovery-sources.md` — field-level source/license/attribution/retention and endpoint policy.
- `scripts/catalog/catalog_probe.py` and `scripts/catalog/test_catalog_probe.py` — streaming input normalization, size/count checks and reproducible synthetic probes.
- Small explicitly synthetic catalog/evidence fixtures under `tests/Fixtures/GuestDiscovery/` if a cross-project fixture is useful; otherwise use the existing test fixture conventions.

**Steps:**

- [x] Re-check primary documentation for MusicBrainz core identifiers/aliases/release groups and supplementary tag assignments, supported album lookup/tag endpoints, ListenBrainz candidate endpoints, cache directives, rate limits, and public deployment conditions. Record exact sources and date. Do not assume an album-similarity endpoint or a numeric Last.fm allowance.
- [x] Specify a compact versioned interchange artifact: canonical entity IDs, aliases, credited artists, album identities, tag observations, source references and checksums. Reject conflicting IDs, invalid Unicode/control characters, unbounded rows, and inconsistent entity links.
- [x] Implement an offline streaming probe that can generate a deterministic broad synthetic name/alias/album corpus without loading the source dump into memory. Separate synthetic mechanics from optional operator-supplied real artifacts.
- [x] Use a disposable database to measure rows, normalized bytes, index size, import staging/temp cost, query plans, search latency, and process memory at increasing corpus sizes. Record the deployed PostgreSQL version. Benchmark short prefixes, typo cases, common names, accents and non-Latin names.
- [x] Inventory compact prepared candidate/evidence samples separately from name metadata; estimate request count and storage per prepared seed without claiming real coverage from fixtures.
- [ ] For actual source acquisition, prepare the required bounded manifest and obtain the existing applicable permission before running it. Do not create live calls simply to satisfy this checklist. Offline stages may continue when a live evidence gate is open; report the gap explicitly. **Not run: no real acquisition manifest has been approved; C1 used synthetic input only.**
- [x] Produce a decision table: data fields permitted, artifact source available, working-set size, estimated preparation cost, measured query behavior, and unresolved live/catalog-quality questions. Set final importer/cache quotas from measurements, including update overlap and guard reconciliation.
- [x] Verify the probe's malformed/oversized input handling and deterministic output; run document checks and a first PostgreSQL query probe. Record what was and was not actually measured.

**Exit:** A reproducible offline resource baseline and explicit source policy exist. Real breadth/readiness remains unverified unless supported by approved real acquisition. No identity migration depends on unverified provider behavior.

**Commit:** `feat(catalog): add bounded catalog feasibility probe and evidence`

## Stage 2 / commit C2 — Add profile and batch foundations

C2 outcome: additive schema/backfill, profile/batch/job invariants and export/privacy coverage are implemented. Final full .NET gate passed 526/526 with zero skips. See [C2 evidence](../../renewal/guest-discovery-evidence.md). No retained database was migrated; continue with the separately verified ownership cutover in C3.

**Create or extend:**

- `src/Domain/Discovery/ListenerProfile.cs`, `DiscoveryBatch.cs`, job records, and discovery state/value types.
- `src/Application/Common/Interfaces/Discovery/` — profile and batch store contracts and owner-neutral models.
- `src/DataAccess/Persistence/Configurations/` — profile, batch and job mappings; job consumption remains disabled.
- `src/DataAccess/Persistence/Migrations/` — additive profile/batch migration.
- `src/DataAccess/DataContexts/DataContext.cs`, existing context interface/DI mappings, and `tests/DataAccess.Tests/Phase8PrivacyInventoryTests.cs`.
- `tests/Domain.Tests/Discovery/` and `tests/DataAccess.Tests/DiscoveryMigrationTests.cs`.

**Steps:**

- [x] Add pure ownership/batch invariants without referencing Identity or provider libraries from Domain.
- [x] Add profile and batch tables and an explicit existing-account mapping; use existing user IDs as historical profile IDs to simplify deterministic backfill. Add a nullable batch association to weekly delivery records where needed.
- [x] Backfill one profile per existing subscriber and one historical batch per existing digest. Preserve existing digest IDs as batch IDs where safe; do not copy every score snapshot into a second permanent table.
- [x] Add bounded shared job contracts/schema for lookup and generation, with explicit kind/state/idempotency fields. The lease runner is implemented in C7/C8. Keep current registered flows runnable during this additive stage; do not expose guest/on-demand routes before complete ownership enforcement exists.
- [x] Classify the new columns in the reviewed privacy inventory and cover account cleanup/export of the new associations immediately.
- [x] Test empty migration and populated pre-migration data: multiple accounts/weeks, feedback, unknown score JSON fields, soft-deleted data, and shared tracks. Verify counts and stable identities, not just schema creation.
- [x] Generate the migration with `--context AppDbContext`; inspect SQL for unintended drops, copied secret data, identity-schema changes and unsupported rename conventions. Test upgrade/recovery using disposable databases.

**Exit:** Existing registered behavior remains usable; profiles and historical batch identities are populated and classified.

**Commit:** `feat(discovery): add listener profiles and historical batch identities`

## Stage 2 / commit C3 — Move ownership and preserve registered contracts

**Modify:** `TasteSignal`, effective taste input/materialization ownership, recommendation entities/configurations, `WeeklyDigest`, repositories, DTO mappings, `IDigestGenerationStore`, current generation/feedback/export/deletion handlers, local Worker adapters, and affected tests. Add owner-neutral persistence models and migration files under existing conventions.

**Steps:**

- [x] Move private taste/recommendation ownership to profiles and generalize recommendation/batch relationships. Keep existing recommendation IDs, snapshots, feedback and track links. Reuse physical storage where possible to avoid unnecessary history copies.
- [x] Use a short documented migration maintenance window with old writers stopped; do not claim rolling compatibility between old binaries and the cutover schema. Verify migration before any retained deployment is touched.
- [x] Preserve public registered endpoints through account-to-profile adapters. Weekly subscriber/week idempotency remains separate from explicit discovery request identity.
- [x] Remove weekly identity as a required input to new discovery ranking/snapshots, while preserving historical week data and readers. Version new snapshot fields; do not fabricate a weekly delivery for a guest request.
- [x] Make feedback, already-known exclusions, scoring inputs, export, account deletion and local capture continue to use the right owner. Preserve encrypted local-email lifecycle boundaries.
- [x] Update exhaustive privacy inventory, account export fixtures, database constraints and architecture checks in the same commit. Assert populated values survive; do not weaken tests to accept missing fields.
- [x] Run Domain/Application/DataAccess and affected Presentation/Infrastructure regressions, including existing phase 6–8 PostgreSQL lifecycle tests. Validate accounts cannot read or mutate another profile's data.

**Exit:** Registered functionality has equivalent behavior over the generalized model; multiple distinct discovery batches can coexist in one calendar week.

**Commit:** `refactor(discovery): generalize ownership while preserving weekly history`

## Stage 3 / commit C4 — Guest sessions, expiry, privacy and account attachment

**Create:** guest session persistence/configuration, `IListenerContext`, guest context resolver and cookie/CSRF services under Presentation, guest lifecycle commands under Application, scoped profile export/cleanup, and guest/API PostgreSQL tests. Extend account registration/login integration only at the necessary handoff points.

**Steps:**

- [x] Resolve authenticated account ownership and guest credentials separately. A caller-provided GUID never grants access. Browsing/search alone must not allocate permanent guest profiles.
- [x] Generate high-entropy credentials, persist only their hashes, issue HttpOnly/SameSite cookies and Secure cookies on HTTPS. Implement explicit development behavior. Apply antiforgery and allowed-origin checks to cookie-authorized mutations, including session/claim flows.
- [x] Implement session bootstrap and deliberate creation. Use `TimeProvider` for seven-day inactive expiry. Throttle activity timestamp writes; do not update profile rows for every keystroke.
- [x] Add bounded expiry cleanup, guest export and guest-only deletion. Expired credentials fail immediately even before cleanup. Cleanup must stop pending/in-flight persistence from recreating removed private data.
- [x] Attach a new registration transactionally after verifying both owners; rotate/revoke guest credentials. Signing into an existing profile requires the explicit import choice, with deterministic handling of duplicated seeds, feedback and history. Preserve progress or fail without partial transfer.
- [x] Keep guest profiles out of subscriber scheduling and preserve registered/consented/verified email eligibility. Add boundary tests without adding email transport or rewriting the delivery feature.
- [ ] Test forged/expired/revoked credentials, CSRF, two-tab attachment, concurrent deletion/generation, account deletion, export field coverage, lost-cookie behavior and authentication precedence. Ensure safe logs and responses contain no credentials. **C4 evidence covers credentials, expiry, CSRF, revoked attachment, import and lost-cookie/account precedence; in-flight generation and integrated deletion races remain for C7–C8 before enabling the host.**

**Exit:** Guest ownership and lifecycle are as well isolated as registered ownership, with seven-day expiry and preservation at registration.

**Commit:** `feat(discovery): add guest sessions and safe account attachment`

## Stage 4 / commit C5 — Local catalog search and explicit resolution

**Create:** catalog search/lookup commands, DTOs and repository interfaces; compact catalog mappings/import store in DataAccess; bounded importer using the stage-1 artifact; `CatalogController`; provider-resolution adapter methods; lookup persistence/status and tests. Extend the existing catalog entities, including `Domain/Catalog/Album.cs`, where their identities are sufficient rather than creating duplicate concepts.

**Steps:**

- [x] Store canonical artist/release-group identities, aliases, credited-artist links, normalized search forms, disambiguation and manifest version references. Maintain a separate relationship for multi-artist albums.
- [x] Apply the measured prefix/token/trigram indexes. Short queries use bounded prefix behavior; no unbounded fuzzy full-index scan or arbitrary client sorting. Enforce query length, result limit, timeout and safe parameterized SQL.
- [x] Implement local `GET /api/catalog/search` and prove its dependency path cannot issue provider HTTP calls. Use useful matches and stable tie-breaks; application scoring remains popularity-neutral.
- [x] Add atomic/resumable artifact import with hashes, content bounds, staging reservations, interrupted-import recovery and last-good publication. Do not load the full artifact into memory.
- [x] Add explicit lookup submission/status contracts with session ownership, coalesced public acquisition, server-selected provider configuration and existing source gates, using the shared job schema. Reuse existing evidence parsers/replay clients where their formats apply. Keep lookup submission disabled outside controlled tests until C8 enables its consumer; do not leave callers with permanently pending jobs.
- [ ] Cache successful resolved public entities under their actual source/cache policy; allow arbitrary missing entities to be requested without defining a fixed list of permitted artists or albums. Keep unsupported/ambiguous cases actionable. **C5 provides the parser, cache-policy deadline and public publication path. End-to-end lookup acquisition/coalescing/publication remains disabled until the C8 consumer.**
- [ ] Test duplicates, multi-artist albums, aliases, accents, Turkish/Azerbaijani/non-Latin input, short queries, pagination, malformed IDs, unavailable provider, stopped source gates, zero HTTP while typing, cancellation and import quota races. **C5 covers the listed search/schema/source/ownership cases and cancellation; actual reservation races remain for C7 before public writers are enabled.**

**Exit:** A browser-independent local search API works over an operator-prepared catalog. Explicit lookup has tested owner-safe contracts and remains disabled until its worker is available in C8. Catalog breadth is reported honestly.

**Commit:** `feat(catalog): add indexed local search and explicit provider lookup`

## Stage 5 / commit C6 — Album taste and shared prepared candidates

**Modify/create:** `TasteTargetType`, seed DTOs and materialization/evidence versions; new discovery seed validation; provider-neutral seed evidence/cache contracts under `Application/Common/Interfaces/Recommendation/`; source-specific album lookup/tag adapters; prepared candidate/evidence stores and refresh service; dedicated byte-accounted caches and tests.

**Steps:**

- [x] Implement the approved new-flow seed bounds and canonical-ID resolution. Treat manual selection replacement as replacement of manual seeds, preserving feedback-derived signals. Record profile revision changes atomically.
- [x] Materialize actual album evidence when supplied by a verified endpoint/artifact. Artist-based fallbacks must carry the correct tag scope, missing reason and configurable influence; never invent album-specific tags. Handle multi-artist credits and deduplicate repeated entity influence.
- [x] Keep external discovery behind `IRecommendationSource` and related seed/hydration contracts. Reuse `LastFmEvidenceParser` and `RecordedLastFmApiClient` where applicable. Do not add unsupported provider calls or pull source-specific code into Domain/Application.
- [x] Share prepared candidates across profiles but rank per profile. Preserve origin, acquisition time, evidence hashes, normalization version and source conditions. Enforce policy-driven freshness; restricted evaluation/held-out material is ineligible.
- [ ] Bound preparation by seeds/candidates/calls/time/bytes; coalesce identical work and honor cross-process source pacing. Refresh in bounded slices. **C6 implements bounded process-memory pools and coalescing; durable acquisition ownership and integrated refresh remain C7/C8 dependencies.** A missing/expired cache entry becomes explicit acquisition work or a coverage gap, never fixture fallback in a real request.
- [x] Implement a 64 MiB aggregate application cache budget with byte accounting across relevant caches. Audit existing `.Set(... Size = 1)` users; do not change a shared cache's units and leave its writers charging inconsistent sizes.
- [x] Reserve durable public-cache growth through the current conservative managed-write guard until C7 introduces improved coordination. If the current mechanism cannot safely reserve a new write class, keep that persistent preparation path disabled until it can; an already-accounted bounded artifact can support offline verification. Do not bypass the guard between commits.
- [x] Run benchmark seed fixtures, baseline scorer reconstruction/replay, permutation/culture determinism, sparse/empty evidence, feedback and provider parity tests. Report quality as unverified unless supported by separately authorized evaluation.

**Exit:** Album and explicit tag inputs affect documented taste evidence; prepared candidates can be reused without losing personal ranking or provenance.

**Commit:** `feat(taste): add album seeds and bounded shared preparation`

## Stage 6 / commit C7 — Durable jobs and coordinated storage accounting

**Create/modify:** generation job model/state rules, job store and leases in DataAccess, accounting/reservation interfaces, `Infrastructure/Storage/LocalGenerationStorage.cs`, store coordination, privacy inventory, and real PostgreSQL crash/concurrency tests.

**Steps:**

- [ ] Implement the shared job store over the C2 schema: atomic enqueue, unique profile/idempotency keys, a partial uniqueness constraint for one unfinished generation per profile, bounded payloads and explicit cleanup rules. Initial enqueue and accepted input changes share a transaction.
- [ ] Implement lease acquisition/renewal/recovery, bounded retry scheduling, stale lease fencing and terminal failures. Distinguish retryable transport failures from permission/configuration/schema failures. Store safe codes rather than raw HTTP bodies or private runtime values.
- [ ] Add short transactional managed-storage reservations for profiles, jobs, catalog publication, shared evidence and recommendation snapshots. Document conservative overhead, physical database growth, WAL/filesystem headroom, inventory changes and operator reconciliation. No class of new managed writes is silently unaccounted.
- [ ] Replace repeated full-history revision scans on the new generation path with a proven coordinated accounting/revision mechanism. Keep an explicit bounded reconciliation/integrity procedure and compatibility with the existing local Worker. The guard remains mandatory and fail-closed.
- [ ] Serialize only the short admission/accounting/persistence sections that require it. No global storage/database lease may span provider I/O. Source processes and account deletion must participate in compatible coordination.
- [ ] Prove reservation races near cutoff, stale inventory, external/untracked growth, interrupted publication, uncertain filesystem inventory, actual bytes exceeding estimate, lease expiry and concurrent deletion. Use real PostgreSQL transactions/process termination for crash boundaries; wait for actual server rollback before asserting outcomes.
- [ ] Validate that a failed required reservation rolls back accepted private input/job changes when they were part of the same action, or returns the documented existing state. Do not leave orphaned jobs or advertise a successfully queued generation that cannot exist.

**Exit:** Durable ownership and resource admission are safe before the web host starts accepting background generation work.

**Commit:** `feat(discovery): add durable jobs and coordinated storage reservations`

## Stage 6 / commit C8 — Background generation and on-demand API

**Create:** `GenerateDiscoveryBatch` and job dispatch handlers, generation API/controller/status DTOs, `Presentation/Background/DiscoveryJobService.cs`, coordinator options/DI and tests. Refactor existing `GenerateDigestCommandHandler` into a weekly adapter over shared generation components; preserve the registered/local Worker entry points.

**Steps:**

- [ ] Extract the reusable acquisition/materialization/scoring/persistence pipeline. Request and profile revision replace calendar-week identity for on-demand work; the weekly adapter retains subscriber/week identity.
- [ ] Implement initial and Discover more submission, server-side 30-second cooldown, idempotent retries and ownership-derived inputs. Replayed keys with the same intent return the original job even during cooldown; reusing a key with different inputs returns a conflict. Input edits invalidate stale work through revision/fencing checks; do not persist results against the wrong inputs.
- [ ] Schedule prepared local scoring separately from waiting acquisition. Begin with one local lane and one shared acquisition lane; allow fair interleaving within provider request limits. Enable the explicit lookup consumer and then its submission route. Reconcile generation and explicit lookup work without indefinite priority starvation.
- [ ] Exclude already-recommended track identities and explicit feedback exclusions across concurrent jobs and restarts. Keep compact suppression history independently of expensive full score snapshots; do not silently erase account history to reclaim cache space.
- [ ] Commit completed batches, recommendation history, evidence snapshots and job completion transactionally. Requests that produce fewer supported picks expose coverage honestly. Persistence must recheck profile existence/revision, lease fencing, source permissions/expiry and storage reservations.
- [ ] Start a bounded hosted background runner with clean shutdown, fresh DI scopes, cancellation and lease recovery. Keep it configurable/disabled until storage/source configuration is valid. Do not reuse Development-only Worker restrictions as the website execution path or relax its guards implicitly.
- [ ] Implement status/result/feedback endpoints with safe states and bounded polling guidance. Do not leak another profile's existence, provider credentials, filesystem paths, manifests or unreviewed raw responses.
- [ ] Test two tabs, duplicate submissions, cooldown races, shared acquisition, queue saturation, fairness under a repeatedly failing profile, restart during acquisition/persistence, expiry/deletion, changing inputs, hidden/resumed page status, sparse coverage, provider outages and storage stops.

**Exit:** Guest and registered clients can request, await and retrieve distinct batches without blocking prepared users behind remote waits or losing work on restart.

**Commit:** `feat(discovery): serve immediate and on-demand recommendation batches`

## Stage 7 / commit C9 — Complete the website flow

**Modify:** `src/Presentation/wwwroot/index.html`, `styles.css`, `app.js`, relevant manual QA files, frontend browser scripts/tests, and `CHANGELOG.md`.

**Steps:**

- [ ] Replace fixed demo-only discovery as the guest's primary action with the working onboarding/results flow. Clearly distinguish any retained illustrative content from generated results.
- [ ] Add accessible artist/album comboboxes with local search, disambiguation, selected chips, style tags and explicit Search more. Debounce and cancel outdated responses; do not let an older response overwrite a newer query.
- [ ] Lazily create the guest context for a deliberate mutation, submit selections with a stable retry key, show preparation stages, and resume the current job/profile after reload. Keep a single pending job visible and retain available results during retryable errors.
- [ ] Render 3–5 eligible recommendations, stored explanations, feedback and Discover more. Show server-derived cooldown and busy/retry states. Preserve selected inputs after failure; handle partial coverage, missing evidence and empty batches without fabricating content.
- [ ] Implement guest expiry disclosure, export/Clear my data, registration preservation, and explicit existing-account import choice. Email remains separate and never starts as a side effect of guest discovery.
- [ ] Reuse the current lightweight frontend and design language. Verify keyboard, focus, loading announcements, small screens, reduced motion, touch targets, error placement and safe output encoding. No frontend framework or image-generation work is required by this plan.
- [ ] Run browser assertions over the real fixture-backed application and disposable database, including search → submit → wait → results → feedback → more → register → retained history. Stubbed UI checks alone cannot establish end-to-end correctness. Keep live-provider verification separate.
- [ ] Update user-visible changelog and manual QA instructions with the actual behavior and known evidence limits.

**Exit:** A visitor completes the website discovery loop as a guest and can retain it by registering.

**Commit:** `feat(web): add guest onboarding and continuous discovery flow`

## Stage 8 / commit C10 — Capacity evidence, operation and completion review

**Create/update:** a repeatable local load driver using available tooling, constrained Compose/runtime configuration, `docs/renewal/guest-discovery-evidence.md`, a local operation/runbook document, `PROGRESS.md`, and related release notes/check instructions. Keep raw measurements and any real acquired data private/untracked where existing contracts require it.

**Steps:**

- [ ] Start with 50 persisted profiles, five active visitors, and a burst of 20 simultaneous searches plus five generation submissions. Separately explore 50 active visitors. Include mixed prepared/unprepared requests, feedback, repeated batches and accumulated history. Record corpus counts, history counts, database version, CPU, storage and cache state for reproducibility.
- [ ] Constrain combined application/database resources around the 2 GB host case; include a realistic host/runtime headroom calculation. Do not equate container memory limits with the entire machine's footprint. Model the 10 GB total-storage case using an isolated test environment rather than filling or modifying the developer's real root filesystem.
- [ ] Measure local-search p50/p95/p99, prepared-batch p95, queue delay, provider delay, throughput, RSS/peak memory, cache bytes, query plans/counts, database/WAL growth and staging overhead. Report cold/restart behavior separately. Existing targets are search p95 ≤100 ms and prepared batch p95 ≤5 s under normal load, not unconditional guarantees.
- [ ] Exercise provider outage/rate limits, deliberate guest-cookie resets, flooding of different input strings, full queues, low disk, stale inventory, interrupted imports, restart recovery, guest expiry and account conversion under load. Existing results remain accessible when new generation is stopped.
- [ ] Tune only issues demonstrated by these measurements. Keep concurrency, cooldown, queue capacity, cache quotas and query bounds configurable. Re-run only the affected scenario after a tuning change, then the required final suite.
- [ ] Record source/catalog gaps and unprepared-batch latency candidly. Passing fixture mechanics does not establish real recommendation quality, catalog breadth, provider permission or public-hosting readiness.
- [ ] Run full applicable .NET/PostgreSQL/offline/browser checks, validate zero skipped required tests, and run the existing Compose lifecycle gate if packaging/host behavior changed. Review export coverage and actual deletion of new tables/columns again.
- [ ] Update runbook defaults from measured evidence and document backup/migration recovery, cache refresh, guest cleanup, queue inspection, source gating, storage reconciliation and operation without optional email.
- [ ] Request developer confirmation against a concrete local launch and short acceptance checklist. Record implemented/local-verified/confirmed/deferred separately. Preserve production and real-quality gates that were not satisfied.

**Exit:** Evidence supports the chosen operating defaults and the entire requested guest discovery flow is reviewable. No hard-coded user ceiling or unlimited-capacity claim is introduced.

**Commit:** `chore(discovery): record constrained-host verification and operation`

## Common commands and verification gates

Commands below are execution instructions, not checks already run during planning. Use the SDK pinned by `global.json`. Supply private database configuration before PostgreSQL tests; never place credentials in this document or commit them.

```bash
dotnet --version
dotnet restore 'Liner Notes.sln' --locked-mode
dotnet build 'Liner Notes.sln' --no-restore -m:1
dotnet test tests/Domain.Tests/Domain.Tests.csproj --no-restore -m:1
dotnet test tests/Application.Tests/Application.Tests.csproj --no-restore -m:1
dotnet test tests/DataAccess.Tests/DataAccess.Tests.csproj --no-restore -m:1
dotnet test tests/Infrastructure.Tests/Infrastructure.Tests.csproj --no-restore -m:1
dotnet test tests/Presentation.Tests/Presentation.Tests.csproj --no-restore -m:1
```

Use meaningful `--filter` selections during a stage where appropriate. Stage boundaries run the affected full projects. Before final completion, run all projects with TRX output and the existing required-project validator:

```bash
dotnet test 'Liner Notes.sln' --no-restore --logger trx --results-directory /tmp/liner-guest-discovery-results -m:1
python3 scripts/checks/phase9_test_results.py /tmp/liner-guest-discovery-results
python3 -m unittest discover -s scripts -p 'test_phase*.py'
python3 -m unittest discover -s scripts/checks -p 'test_phase9*.py'
python3 scripts/checks/secret_defaults.py
python3 scripts/checks/renewal_docs.py
git diff --check
```

Use a fresh results directory for each final run; do not mix stale TRX files with a new result. Add the new probe checks explicitly when C1 creates them:

```bash
python3 -m unittest discover -s scripts/catalog -p 'test_*.py'
```

Generate and inspect migrations against the real context:

```bash
dotnet ef migrations add AddListenerProfilesAndDiscoveryBatches --context AppDbContext --project src/DataAccess --startup-project src/Presentation --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --context AppDbContext --project src/DataAccess --startup-project src/Presentation
```

Use distinct descriptive migration names for later stages. SQL generation does not authorize applying the migration to a retained database. Test the populated-data upgrade against an isolated disposable database before any real deployment.

When packaging or host execution changes, run the existing lifecycle gate after the focused checks:

```bash
python3 scripts/checks/phase9_compose.py --build
```

Inspect the exact staged diff before each focused commit. Record actual counts/outcomes rather than copying historical totals from `PROGRESS.md`.

## First execution checkpoint

The developer approved sequential execution. C1 results are recorded in the evidence ledger and reported before the ownership migration. A missing real acquisition permission leaves real coverage unverified; it does not prevent independent fixture-backed engineering. Do not call the feature complete after the feasibility probe or the onboarding UI alone.

The brainstorming workflow requires a written-plan review and an execution-method choice before implementation. The proposed method is sequential execution in this session. The referenced `writing-plans` skill was not installed; this repository checklist is the explicit planning deliverable, with no hidden dependency on an unavailable execution skill.
