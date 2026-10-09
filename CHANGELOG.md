# Changelog

## Unreleased — renewal

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
