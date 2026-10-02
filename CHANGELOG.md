# Changelog

All notable changes to the Liner Notes project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
