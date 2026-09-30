# Changelog

All notable changes to the Liner Notes project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0] - 2026-09-30

### Added
- **Onion Architecture Skeleton (.NET 10):**
  - `src/Domain`: Zero-dependency domain model containing pure entities, value objects, and scoring logic.
  - `src/Application`: MediatR CQRS orchestration shell with dependency injection registration.
  - `src/DataAccess`: Dedicated EF Core 10 data access layer (AppDbContext, Entity Configurations, Migrations).
  - `src/Infrastructure`: HttpClientFactory (Last.fm, ListenBrainz), email delivery, and in-memory caching.
  - `src/Web`: Lightweight ASP.NET Core presentation composition root.
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
