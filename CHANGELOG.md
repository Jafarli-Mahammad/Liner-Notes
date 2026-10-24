# Changelog

## Unreleased — renewal
- Honor server retry delays while polling discovery jobs. Reuse public metadata
  populated during provider pacing and avoid an extra response-body array copy.
- Reuse invariant taste/configuration work within scoring batches and index
  candidate aliases during merging, preserving scores, ranking and explanations.
- Batch catalog selection and lookup-result reads, and check both account
  eligibility conditions in one query while preserving immediate revocation.

- Start the normal local website with live ListenBrainz/MusicBrainz providers,
  a persistent dedicated database and private storage configuration. Add
  `bash scripts/run_live.sh`, including selection of the installed pinned SDK.
- Display the actual discovery evidence mode in the frontend.
- Publish eligible core metadata acquired during live preparation into the public
  catalog so real picks can appear in recipient-free Recent recommendations.

- Fix the legacy taste form sending incorrect tag and weight fields to the API.
  Show backend validation messages when saving taste, registering or signing in.
- Keep account controls, including sign out, accessible in the mobile navigation.
- Revalidate frontend assets so browsers load current API integration code after
  an update instead of continuing to use cached, unversioned scripts.
- Preserve canonical recording identity when storing onsite picks; keep recordings
  with useful recording tags even when artist tags are missing.
- Reject weekly saves after optional taste answers change, and allow catalog
  favorites with display names up to the existing 256-character catalog limit.
- Repair PR merge dependency locks and consolidate Qodana analysis while retaining
  the existing Cloud project token configuration.
- Add Qodana .NET configuration and GitHub PR analysis with pinned
  tooling, locked dependency restore, annotations and downloadable reports.
- Add optional, resumable guest/account taste setup: three skippable questions,
  searchable genre/mood/sound tags without a selection cap, broad taste,
  artist/album/recording favorites and explicit review before generation.
- Preserve recording versions, credits and release hints in private favorites;
  disclose reduced album-to-artist similarity influence in stored explanations.
- Add Recent recommendations for everyone, including skipped-input visitors,
  using a separate recipient-free public catalog projection and truthful demo/empty states.
- Keep large saved taste profiles separate from rotating acquisition windows and
  bounded replayable score snapshots. Include answers in export and account transfer.
  Existing name seeds, custom tags and queued request retries remain compatible.
- Add a dependency-free synthetic load driver and document measured resource use,
  search latency, queued recovery and disk budgeting on a constrained runtime.
- Coordinate weekly digest saves, schedule activity and unsubscribe updates with
  onsite discovery storage. Reject unsupported concurrent local email capture
  in the development Worker as well as the website.
- Reclaim expired public recommendation evidence and catalog search records in
  bounded maintenance passes. Preserve the active catalog, fresh lookups,
  resumable imports, canonical identities and private taste/history evidence.
- Add the onsite guest website: artist/album autocomplete, styles, explicit
  catalog lookup, durable progress, saved explanations, feedback, history,
  cooldown, export/clear, preserved registration and explicit account import.
  Synthetic catalog entries/results are labelled; returning does not regenerate.
- Pace explicit catalog lookup independently from recommendation generation,
  retaining the 30-second generation default. Add a synthetic-only local setup
  and browser verification script.
- Fix discovery after positive feedback: derived artist and tag inputs can share
  recommendation provenance without colliding or sharing their distinct weights.
- Activate website discovery consumers only through DiscoveryRuntime:RunJobs,
  with periodic known-inventory refresh and bounded guest expiry cleanup.
  Concurrent local email capture remains blocked pending writer integration.
- Renew guest activity hourly on visits; storage stops keep saved data readable
  and cookies retain the actual server expiry. Separate configurable guest-start
  pacing from password authentication limits.
- Allow guest/account data reclamation at the managed storage cutoff while
  rejecting transactions that grow tracked data. Keep disk, revision and artifact
  guards. Add known-inventory refresh without accepting an unknown baseline.
- Coordinate authentication, legacy taste and feedback writes when discovery
  jobs are enabled. Taste/feedback revisions cancel stale generation atomically;
  independent catalog lookup stays queued. MVC action exceptions roll back writes.
- Resolve recommendation bundles through registered adapter keys; provider-specific
  live-use restrictions stay in adapter registration. Existing scoring policy
  identifiers remain unchanged by default.
- Keep EF Core context and transaction types inside DataAccess; Application now
  depends on repository and unit-of-work contracts without an EF Core package.
- Release weekly generation storage leases during provider acquisition, then
  reacquire and recheck before persistence. Enabled discovery reuses shared
  artist/album preparation while keeping ISO-week score identity and recorded
  replay compatibility.
- Add private discovery overview/status/results and feedback endpoints. Responses
  include saved explanations and bounded history; feedback retries preserve
  scores and accepted changes cancel stale generation. Cooldown/conflict errors
  use safe responses and retry guidance.
- Connect explicit artist/album lookup to durable jobs, shared acquisition and
  expiring local publication. Lookup and generation alternate on the acquisition
  lane. Search typing remains local; the website runner is not yet registered.
- Keep the conservative reconciled storage charge when PostgreSQL physical
  size decreases without a tracked revision change. Growth and revision changes
  retain their guards, avoiding false capture failures after a commit.
- Add idempotent discovery submission, transactional cooldown checks and taste
  changes that cancel stale work. Separate prepared and acquisition lanes keep
  prepared profiles responsive while source work waits. The runner remains
  unregistered until endpoint and storage/legacy integration is complete.
- Add replayable discovery batch preparation and atomic score/history/job
  completion, with bounded feedback reads and stale evidence/revision checks.
  The onsite runner and website integration remain pending.
- Reuse durable public artist/album seed and candidate evidence after memory
  restart. Prepared-only work cannot fall through to upstream acquisition.
- Add leased, quota-bounded public evidence storage and compact per-profile
  recommendation identity history. Export 2.5 includes history and batch coverage.
  The background consumer and onsite generation flow remain pending.

- Add durable discovery job leases, idempotent queue admission and coordinated
  storage reservations. Database accounting avoids scanning private history for
  every new write. The onsite runner and website flow remain pending.

- Add canonical artist/album selections and style validation, scoped album taste
  evidence with configurable credited-artist fallback, and reusable public candidate
  pools. Provider and prepared caches now have a combined 64 MiB admission budget.
  Export 2.4 includes selection provenance. The onsite product flow remains disabled
  until the durable runner and storage coordination stages are complete.

- Add indexed local artist/album search with aliases, Unicode and typo handling,
  all album credits, source attribution and versioned catalog publication.
  Bounded imports resume verified chunks while keeping the last good catalog.
  Explicit lookup has private status routes and source-policy checks; submission
  remains disabled until its durable background consumer is installed.

- Add guest credentials, seven-day inactive expiry, private export/clear and
  transactional account attachment/import. Cookie mutations require same-origin
  checks and antiforgery tokens. Guest routes remain disabled pending storage
  and background-processing prerequisites. Weekly scheduling now requires
  explicit email opt-in and a verified address; onsite use stays independent.

- Move taste and recommendation ownership to listener profiles while preserving
  historical IDs, feedback and score evidence. Add non-weekly recommendation
  snapshots and include on-demand history in account exports. Registered and
  weekly APIs remain available; guest access is not enabled by this change alone.

- Add listener profile, discovery batch and pending-job foundations with an
  additive legacy-data backfill. Export 2.5 includes the new owned metadata and
  batch links while excluding operational lease credentials. Guest access and
  on-demand generation are not enabled by this foundation alone.

- Add a bounded synthetic catalog resource probe and source-policy record for
  the approved guest discovery work. Local metadata search feasibility is measured
  separately from recommendation readiness; the guest product flow is not yet implemented.

- Make ListenBrainz the default provider while retaining Last.fm as an operator
  option. Discovery, seed tags and candidate tags resolve together. Add bounded
  public-catalog clients, approved recorded replay and explicitly gated live
  generation; preserve historical evidence and derive email credits from it.
  Local Compose remains synthetic. Per-user provider choice and history imports
  are deferred, and deployment-specific usage/license review remains open.

- Reworked the local web interface into clear discovery, taste profile, account,
  and data areas with responsive navigation, accessible focus states, and plain-language
  labels. Guest tracks are marked as examples, and recommendation details no longer
  show a popularity penalty for the popularity-neutral V1 scorer.

- Restore the approved MediatR 12.5.0 licensing exception in central versions and package locks. CI generates a disposable database password for each run, and the local runbook selects the current UTC ISO week.

- Add non-root web and Worker container targets, migration and private-volume initialization, and a local PostgreSQL Compose stack with loopback-only web access. CI supplies PostgreSQL and checks that the full test suite runs without skips. Public release remains gated on provider approval and production operational decisions.

- Pin stable .NET SDK 10.0.401, EF Core/runtime/tool 10.0.12, and current stable dependencies with locked restore. Retain the approved MediatR exception and use NSwag for Development-only API documentation.

- Remove credential-shaped literals from secret checks, Compose, and CI. Generate private database connection strings with local credentials; existing ignored Compose environment files need regeneration.

- Registration uses the selected UTC delivery weekday/hour. Added an explicit, guarded
  local repair tool for legacy registration slots. Bounded scheduler ticks track
  candidate attempts so persistently stopped accounts cannot starve healthy peers.

- Added a Development-only synthetic scheduler with an explicit account allowlist,
  bounded ticks, UTC due slots and a PostgreSQL lease. Verified local capture advances
  the claimed slot by one week; failures, opt-out, changed preferences and cancellation
  preserve it. Restart verifies the original encrypted receipt. Storage reconciliation
  remains an explicit operator step. The local QA menu now includes schedule fixture setup.
- Export 2.2 adds stored Identity profile fields, including username, contact
  confirmation and audit values, to the existing account/taste export. Credentials,
  authentication internals and unsubscribe capabilities remain excluded. Added an
  explicit table/column privacy inventory. The privacy page explains credential
  exclusions and the separate account, local-copy, shared-catalog and backup boundaries.

- Encrypt complete local email captures and partial writes using ASP.NET Core Data
  Protection with a separate file purpose. Recovery, sanitized export and account
  cleanup decrypt in memory. Local operator inspection prints sanitized text without
  creating preview files; explicit conversion replaces authenticated legacy plaintext
  copies with encrypted files and reports unverifiable leftovers. Keys remain in the
  existing database key store, whose access and backups require separate protection.

- Added a runnable local email QA launcher and explicit cleanup retry after account
  deletion. Unix sink directories must be owner-only and MIME files are created private;
  capability queries are excluded
  from request logs, and conflicting receipt publication stops. Phase 7 passed local
  process/browser checks and 388 tests; developer confirmation remains pending.

- Added explicit offline Worker reconcile/generate/capture actions, account ownership
  coordination, opt-out checks under PostgreSQL row locks, and restart recovery
  across file publication/SQL commit. Recorded generation requires a separately
  approved exact local-use manifest; credentials cannot enable live generation.

- Added bounded local UTF-8 plaintext/HTML weekly messages with stored explanations,
  ordinary listening links, unsubscribe and no remote resources or tracking. Atomic
  MIME capture validates durable receipts, accounts for failed partials and supports
  sanitized copy export and ownership-based cleanup. External delivery remains pending.

- Added an explicitly enabled Development-only unsubscribe confirmation flow and
  persistent email opt-out. Export 2.1 exposes the opt-out and local copy inspection
  status; account deletion reports incomplete local copy cleanup after SQL deletion.
  Local capture has its own status and does not claim external delivery.

- Added bounded recorder-fed weekly digest generation with atomic PostgreSQL persistence,
  complete stored evidence, canonical ISO weeks, replay and storage/expiry guards. Export 2.0
  includes complete snapshots, audit fields and all digest history through bounded pages.
  Later ratings preserve familiarity; marking a rated track known preserves its numeric rating.
  Local checks pass; developer confirmation and recommendation quality remain pending.

- Added pure provisional baseline-A scoring with popularity-neutral ranking, one pick per
  artist, complete versioned score inputs/contributions and stored-input replay. Legacy
  payloads remain readable; new snapshots retain unknown JSON fields. Recommendation
  quality remains unverified and real evaluation remains open.

- Redesigned the README with a musical identity, clearer product goals and development
  status, architecture diagrams, and a separate guide for navigating the codebase.
  Added original record sleeve artwork and a Codebase Memory Nebula snapshot with
  source and coverage notes in `docs/architecture`.

- Added a security policy covering supported development code, private vulnerability
  reporting, response targets, and coordinated disclosure.

- Recheck recorder storage inventory after pacing and before each provider request. Preserve blank-name
  Last.fm tag observations, including their count, URL and response position, as excluded evidence.

- Added Phase 4 local pilot tooling: frozen A–D scoring, complete normalized explanations,
  approved-recording replay, shape/adequacy gates, bounded reports and concealed blind review.
  Recorder metadata now retains response receipt time separately from request start time.
  Verified with offline inputs; live manifest approval, recording and developer ratings remain pending.

- Added Phase 3 transient ingestion evidence: missing/invalid values and coverage gaps, every
  discovery path, attribution, response hashes/times and explicit origins. Removed invented tags,
  listener estimates, matches and hybrid fixture substitution; added in-memory recorded replay.
  No schema changes or live acquisition; verification awaits developer confirmation.

- Added Phase 2 fixture evaluation mechanics, reviewed profile membership locks, metric checks,
  and a separate manifest-gated recorder with bounded storage accounting. Replaced the legacy
  founder test with an offline isolation check. No live recording or catalog-quality evaluation
  was performed; implementation verification awaits developer confirmation.

- Revised the renewal plan into nine approval-gated phases, with proposed pilot/evaluation thresholds
  and separate recording manifests; Phase 1 authorization is unchanged and later phases remain unapproved.
- Recorded developer approval of the revised written plan, evaluation thresholds, pilot bars and
  reviewed interpretations; later-phase execution and concrete recording manifests remain gated.

- Added explicit loopback-only development defaults (`postgres` database password and a local JWT
  signing key) at the developer's request. Production credentials remain unset and the development
  signing key is rejected outside Development. Application users still choose their passwords.
- Secured browser sessions and refresh-token rotation, added account lockout and request throttling,
  and made registration, account deletion, and feedback replacement transactional.
- Added CORS, transport and security-header defaults; removed the vulnerable AutoMapper dependency,
  pinned XML cryptography remediation, and stopped tracking generated build output.
- Guarded login and registration against stale responses, preserved familiarity when rating known
  tracks, cleared obsolete rating highlights, and surfaced unsuccessful feedback submissions.
- Added trusted forwarded-header handling for configured proxy IPs and made the Identity FK
  migration tolerate pre-existing domain profiles while enforcing the relationship on new rows.

- Reconciled project documentation with popularity-neutral V1 and explicit approval gates.
- Qualified earlier coverage, delivery, export and privacy claims; previous release entries below are historical assertions, not current verification.

All notable changes to the Liner Notes project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.4.1] - 2026-10-02

### Security & Hardening
- **Secret Zeroization & JWT Validation (P0/Finding 1):** Removed hardcoded development JWT signing key from committed `appsettings.json` and eliminated fallback signing keys. Enforced strict 256-bit (>= 32-byte) key validation in `DependencyInjection` and `JwtService`, backed by .NET user-secrets.
- **Refresh Token Validation & Rotation (P1/Finding 2):** Bound refresh tokens to user identity using ASP.NET Core Identity authentication tokens (`UserManager.SetAuthenticationTokenAsync`/`GetAuthenticationTokenAsync`). Enforced strict verification on `/api/auth/refresh` and automatic token rotation.
- **Atomic Registration & Rollback (P1/Finding 3):** Added compensating rollback to `AuthController.Register` to purge orphaned Identity credentials if domain subscriber registration or taste seeding fails.
- **Anti-XSS Protection (P1 & P2/Findings 12 & 13):** Added `escapeHtml` sanitization to all track metadata (title, artist, album) in `app.js` and migrated raw JSON export viewer from `innerHTML` to safe DOM `textContent`.
- **Export Authorization (P1/Finding 6):** Enforced authentication verification on `/api/export/my-data` and refactored client export download to use authenticated `Bearer` token requests with blob streaming.
- **Identity Cleanup on Account Deletion (P1/Finding 9):** Coordinated deletion of ASP.NET Core Identity credentials when purging subscriber profiles via `DELETE /api/subscribers/me`.

### Fixed & Improved
- **EF Core Database Migration (P1/Finding 4):** Generated and verified migration `AddRatingToWeeklyRecommendation` adding the `Rating` column to `WeeklyRecommendations`.
- **JSON Enum Binding (P1/Finding 5):** Added `JsonStringEnumConverter` to `UserFeedback` and controller JSON options to reliably bind feedback strings (`Liked`, `Disliked`, `AlreadyKnown`).
- **Data Integrity in Upstream Mapping (P1/Findings 7 & 8):** Prevented artist MBID propagation onto track identifiers and eliminated placeholder/invented songs when upstream results are empty.
- **Idempotent Feedback & Contradiction Resolution (P1/Findings 10 & 11):** Purged existing signals before writing updated preferences, preventing duplicate signal accumulation and teaching conflicting tastes.
- **Digest Contract Alignment (P2/Finding 14):** Aligned frontend DTO mapping with backend schema (`noveltyBoost`, `matchedTags`, `tagName`, `feedback`, `rating`).
- **Memory Cache Bounding (P2/Finding 15):** Capped in-memory cache size (`SizeLimit = 10,000`, `CompactionPercentage = 0.20`) with explicit entry size weights (`Size = 1`).
- **Deterministic Cancellation (P2/Finding 16):** Propagated `OperationCanceledException` across candidate discovery and hydration to prevent returning partial recommendation results upon cancellation.

## [0.4.0] - 2026-10-02

### Added
- **Upstream Recommendation Source Abstractions (Phase 2):**
  - Added `IRecommendationSource` contract (`GetCandidatesByArtistsAsync`, `GetCandidatesByTagsAsync`) in `src/Application`.
  - Added `ICandidateHydrator` contract (`HydrateCandidateAsync`, `HydrateCandidatesBatchAsync`) in `src/Application`.
  - Added immutable record `RawCandidateTrack` holding unhydrated candidate metadata and upstream match scores.
- **Last.fm Client Subsystem & Infrastructure:**
  - Implemented typed `LastFmApiClient` using `HttpClientFactory` and identifiable `User-Agent`.
  - Built thread-safe `LastFmRateLimiter` implementing token-bucket pacing (4 requests/sec max) complying with Last.fm's fair-use limits.
  - Implemented in-memory response caching adhering to Last.fm's 100 MB reasonable usage cap and HTTP caching terms.
  - Implemented **Hybrid Fixture Fallback Mode** (`LastFmClientMode.Hybrid`): automatically queries live Last.fm APIs when an API key is present and gracefully falls back to deterministic offline fixtures (`LastFmFixtureProvider`) when offline, rate-limited, or in CI environments.
  - Implemented `LastFmRecommendationSource` and `LastFmCandidateHydrator` with logarithmic listener popularity normalization and stoplist tag noise filtering.
  - Wired Last.fm subsystem in `src/Infrastructure/DependencyInjection.cs`.
- **Infrastructure Test Suite (`tests/Infrastructure.Tests`):**
  - Created new test project with 15 passing tests covering rate limiting, caching, HTTP fakes (`MockHttpMessageHandler`), error handling (HTTP 429), and candidate hydration.
  - Solution test suite expanded to 186 passing tests with 100% pass rate.
- **Founder Seeds Coverage Spike:**
  - Executed live coverage probe against Last.fm for the 6 founder taste clusters (Jakuzi, Son Feci Bisiklet, Hotline Miami, Dying Light, ULTRAKILL, Hades).
  - Generated comprehensive findings report in `docs/coverage-spike-founder-seeds.md`.

## [0.3.0] - 2026-10-02

### Added
- **Granular Feedback & Preference Scaling (Phase 6):**
  - Added optional 1–10 preference rating scale alongside `Liked`, `Disliked`, and `AlreadyKnown` feedback on `WeeklyRecommendation`.
  - Closed feedback loop in `RecordRecommendationFeedbackCommandHandler`: recording feedback now automatically generates provenance-tagged `TasteSignal` entries that nudge tag weights for future weeks.
  - Added `TasteSignalSource.RecommendationAlreadyKnown` and `TasteSignalSource.RecommendationRating` enum flags.
  - Added interactive 1–10 rating bar to web dashboard in `wwwroot/app.js`.
  - Added 64 new unit and integration tests across Domain, DataAccess, Application, and Presentation test suites. Total passing test suite expanded from 107 to 171 tests (0 failures, 0 skipped).
  - Generated comprehensive requirement-driven test cases document `tests/liner-notes-comprehensive-test-cases.md` covering functional, edge cases, error handling, and state transitions.

### Changed
- **Residue Scorer ("Prism") Feedback Granularity:**
  - Updated `RecommendationScorer.DetermineFeedbackPenalty` to distinguish specific track rejection (1.0 penalty) from artist rejection (0.7 penalty), preventing single-track dislikes from permanently banning an entire artist.
- **Copy & Provenance Hardening:**
  - Removed overclaiming language ("anti-algorithmic") in favor of "transparent scoring with popularity-bias control".
  - Clarified streaming links as direct 1-click search links rather than authenticated API URIs.
  - Established engine version codenames: Residue V1 "Prism" and Residue V2 "Resonance".

## [0.2.0] - 2026-10-02

### Added
- **ASP.NET Core Presentation Layer (`src/Presentation`):**
  - Lightweight composition root wiring Onion Architecture layers (`Application`, `DataAccess`, `Infrastructure`, `Presentation`).
  - ASP.NET Core Identity integration with `ApplicationUser` in `identity` schema, authenticated via JWT Bearer tokens with configurable `JwtOptions`.
  - Concrete service implementations:
    - `CurrentUserService` providing authenticated user context and claims extraction.
    - `JwtService` generating cryptographically signed access and refresh tokens.
    - `AuthService` handling user registration, password verification, and credentials management.
  - RFC 7807 compliant `ApiExceptionFilterAttribute` mapping `ValidationException` (400), `NotFoundException` (404), `UnauthorizedAccessException` (401), and `InvalidOperationException` (400) to standard `ProblemDetails`.
  - Feature-complete REST API controllers:
    - `AuthController`: register, login, refresh token, and authenticated profile (`/api/auth/me`).
    - `SubscribersController`: subscriber profile resolution and GDPR account deletion (`DELETE /api/subscribers/me`).
    - `TasteController`: hybrid onboarding taste profile seeding (3-5 seed tags/artists) and atomic taste signal inspection.
    - `DigestsController`: latest weekly digest retrieval and 1-click sentiment feedback (`Liked`, `Disliked`, `AlreadyKnown`).
    - `ExportController`: complete GDPR verbatim JSON data export (`/api/export/my-data`).
  - Single unified OpenAPI / Swagger UI (`/swagger`) with JWT Bearer security scheme.
  - Health checks endpoint (`/health`) with EF Core `DataContext` connectivity verification.
  - Guest-first discovery and subscriber dashboard in `wwwroot/`:
    - Responsive dark-mode interface with zero-friction 1-click streaming deep-links (YouTube, Spotify, Bandcamp, Apple Music).
    - Stored Residue score breakdown inspector displaying tag overlap, anti-popularity penalty, and novelty boost.
    - Interactive 3-5 seed tag/artist selector for cold-start onboarding.
    - "Your Data" viewer and 1-click JSON export.
- **Presentation Test Suite (`tests/Presentation.Tests`):**
  - 25 unit and controller tests verifying exception filtering, JWT token lifecycle, claims principal extraction, and controller contracts.
  - Total solution test suite now stands at 100 passing tests across Domain, Application, DataAccess, and Presentation.

## [0.1.0] - 2026-09-30

### Added
- **Onion Architecture Skeleton (.NET 10):**
  - `src/Domain`: Zero-dependency domain model containing pure entities, value objects, and scoring logic.
  - `src/Application`: MediatR CQRS orchestration shell with dependency injection registration.
  - `src/DataAccess`: Dedicated EF Core data access layer targeting PostgreSQL 16+ via Npgsql (AppDbContext, Entity Configurations, Migrations).
  - `src/Infrastructure`: HttpClientFactory (Last.fm, ListenBrainz), email delivery, and in-memory caching.
- **PostgreSQL DataAccess Layer & Migrations:**
  - `AppDbContext` targeting PostgreSQL 16+ with `IAppDbContext` abstraction and `IDataProtectionKeyContext` for at-rest token protection.
  - Automatic UTC timestamp tracking (`CreatedAt` and `LastModifiedAt`) for all `IAuditableEntity` models in `SaveChangesAsync`.
  - Global query filter for soft-deleted `User` entities (`!u.IsDeleted`).
  - Robust entity configurations mapping all 8 domain models, enum string conversions, value object conversions (`IsoWeek`, `ScoreBreakdown` stored as `jsonb`), and cascade rules.
  - Repository interfaces (`IUserRepository`, `IWeeklyDigestRepository`, `ITasteSignalRepository`, `ITrackRepository`) in `Application` and implementations in `DataAccess`.
  - Initial EF Core migration (`InitialCreate`) with PostgreSQL snapshot.
  - Test suite (`tests/DataAccess.Tests`) with 11 tests verifying model configuration, soft-deletes, relationships, audit timestamps, and architectural isolation.
  - `src/Presentation`: Lightweight ASP.NET Core presentation composition root (renamed from `src/Web`).
  - `src/Worker`: Dedicated background service composition root for scheduled batch processing.
  - `tests/Domain.Tests`: Unit test project referencing Domain.
- **Pure Deterministic Recommendation Scorer (`RecommendationScorer`):**
  - Pure function evaluating candidate tracks against user taste profiles with zero I/O, zero randomness, and zero side effects.
  - Four configurable scoring components via `ScoringParameters`:
    1. Tag-overlap cosine similarity over weighted tag vectors ($W_{\text{tag}}$).
    2. Anti-popularity penalty ($W_{\text{pop}}$) penalizing mainstream artists.
    3. Novelty boost ($W_{\text{nov}}$) prioritizing tracks/artists unfamiliar to the user.
    4. Feedback penalty ($W_{\text{fb}}$) heavily penalizing explicitly disliked items.
  - Structured, persistent `ScoreBreakdown` providing full inspectability and deterministic explanation generation without text hallucination.
- **Tag Vector Abstraction (`WeightedTagVector`):**
  - Reusable, immutable value object mapping normalized tag strings to continuous weights.
  - Optimized cosine similarity math and top overlapping tag contribution extraction.
  - `Tag` value object enforcing lowercase, trimmed normalized identity.
- **Atomic Taste Signals (`TasteSignal`):**
  - Provenance-tagged atomic records (`TasteTargetType`, `TasteSignalSource`, `Context`) to support data minimization, user data inspection, and verbatim export/deletion.
- **Weekly Digest & Idempotency (`WeeklyDigest`, `IsoWeek`):**
  - ISO-8601 calendar week value object (`IsoWeek`) enforcing idempotent batch delivery: at most one digest per user per calendar week.
  - `WeeklyRecommendation` holding ranked tracks, stored score breakdowns, and user feedback.
- **Benchmark Seed Set Fixture (`BenchmarkSeedFixtures`):**
  - Fixed benchmark set spanning 8 genres: Metal, Hip-Hop, Electronic, Jazz, Pop, Indie, Classical, and Regional (Anatolian Rock/Mugham).
- **Unit & Architectural Test Suite:**
  - 22 unit tests covering taste similarity, anti-popularity penalty, feedback penalty, novelty boost, determinism across 1,000 iterations, tie-breaking, and architectural zero-dependency verification.
- **Continuous Integration:**
  - GitHub Actions workflow (`.github/workflows/ci.yml`) targeting .NET 8.

### Engineering Decisions & Judgment Calls
1. **WeightedTagVector Design:**
   - Designed as an immutable value object with lazy magnitude caching, case-insensitive normalized tag lookups, and dedicated top-overlapping tag contribution breakdown to feed `ScoreBreakdown`.
2. **Taste Signal Aggregation vs. Storage:**
   - Maintained atomic `TasteSignal` records as discrete immutable records in `Domain` with source provenance, while providing `UserTasteProfile` as the pure input snapshot to the scorer. This decouples the storage/audit requirement from the scorer.
3. **Deterministic Ranking & Tie Breaking:**
   - Resolved candidate ties deterministically using `CandidateKey` (`mbid` or `normalized_artist:normalized_title`) with ordinal comparison so rankings are bitwise repeatable across machines and runs.
4. **Weekly Idempotency:**
   - Implemented `IsoWeek` value object and unique constraint on `WeeklyDigest` to guarantee a subscriber cannot receive two digests in the same calendar week.
