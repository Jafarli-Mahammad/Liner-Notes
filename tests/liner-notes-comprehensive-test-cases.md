# Test Cases: Liner Notes System & Architecture

## Overview
- **Feature**: Core Domain, DataAccess, Application CQRS, Presentation API & Residue: Prism Engine
- **Requirements Source**: `AGENTS.md`, `PROGRESS.md`, `MEMORY.md`, and Onion Architecture Specification
- **Test Coverage**: Pure Scoring, Catalog Entities, Digest Lifecycle, Taste Seeding, Granular Feedback Loop, Repositories, Soft Deletes, Data Export
- **Last Updated**: 2026-10-02

---

## Test Case Categories

### 1. Functional Tests

#### TC-F-001: Deterministic Recommendation Scoring & Explainable Breakdown (Residue: Prism)
- **Requirement**: Core pure scoring engine calculates tag overlap, anti-popularity penalty, novelty boost, and feedback penalty with bitwise determinism.
- **Priority**: High
- **Preconditions**:
  - `RecommendationScorer` instantiated with default parameters.
  - User taste profile configured with genre tag weights.
- **Test Steps**:
  1. Score an underground candidate matching user taste tags.
  2. Score a mainstream candidate with identical tags but high global popularity.
  3. Execute 1,000 iterations of scoring for the same inputs.
- **Expected Results**:
  - Underground candidate receives a significantly higher score than mainstream candidate due to popularity penalty.
  - All 1,000 iterations yield bitwise identical float scores and explanation strings.
- **Postconditions**: No mutable state modified in scorer or inputs.

#### TC-F-002: Deterministic Tie-Breaking
- **Requirement**: Two candidates with identical scores must break ties deterministically by CandidateKey.
- **Priority**: High
- **Preconditions**: Two candidate tracks with identical tags and popularity.
- **Test Steps**:
  1. Rank `[CandidateA, CandidateB]`.
  2. Rank `[CandidateB, CandidateA]`.
- **Expected Results**: Both rankings return identical rank order sorted deterministically by CandidateKey.
- **Postconditions**: Output list matches expected ordinal sorting.

#### TC-F-003: User Taste Profile Seeding (Hybrid Onboarding)
- **Requirement**: Allow subscribers to seed 3–5 initial tags and/or artists with continuous weights [0.0, 1.0].
- **Priority**: High
- **Preconditions**: Registered user exists in database.
- **Test Steps**:
  1. Submit `SeedTasteProfileCommand` with 2 tags and 2 artists (total 4 seeds).
- **Expected Results**:
  - Handler persists 4 `TasteSignal` entities with source `InitialSeedManual`.
  - Normalized target values are lowercased and trimmed.
- **Postconditions**: Signals are inspectable via `ITasteSignalRepository.GetByUserIdAsync`.

#### TC-F-004: Granular Recommendation Feedback Recording (1–10 Rating & Sentiment)
- **Requirement**: Record subscriber rating (1–10 scale) alongside sentiment (`Liked`, `Disliked`, `AlreadyKnown`) and optional comment.
- **Priority**: High
- **Preconditions**: Existing `WeeklyRecommendation` in user digest.
- **Test Steps**:
  1. Dispatch `RecordRecommendationFeedbackCommand` with rating `9`, sentiment `Liked`, and comment.
- **Expected Results**:
  - Recommendation entity updates `Feedback = Liked`, `Rating = 9`, `FeedbackComment`, and `FeedbackGivenAt`.
  - Command returns `true`.
- **Postconditions**: Recommendation record in database reflects rating and feedback.

#### TC-F-005: Taste Signal Generation & Nudging from Feedback
- **Requirement**: Recording feedback must nudge future taste signals (positive reinforcement for high ratings, track suppression for dislikes, familiarity flag for already known).
- **Priority**: High
- **Preconditions**: Existing recommendation with score breakdown containing matched tags.
- **Test Steps**:
  1. Submit feedback with rating $\ge 7$ or `Liked`.
  2. Submit feedback with `AlreadyKnown`.
- **Expected Results**:
  - `Liked` creates positive `TasteSignal` for the artist and matched tags.
  - `AlreadyKnown` creates familiarity signals (weight 0.0) with source `RecommendationAlreadyKnown`.
- **Postconditions**: Signals persist to `ITasteSignalRepository`.

#### TC-F-006: Weekly Digest Creation & Recommendation Attachment
- **Requirement**: Manage weekly digest batches per subscriber with ISO week tracking and track recommendations.
- **Priority**: High
- **Preconditions**: Valid user and candidate tracks.
- **Test Steps**:
  1. Create `WeeklyDigest` for ISO week `2026-W40`.
  2. Add recommendation with `ScoreBreakdown`.
- **Expected Results**:
  - Recommendation rank assigned (1..5).
  - JSONB score breakdown stored in database without loss of precision.
- **Postconditions**: `WeeklyDigest.Recommendations` contains added tracks.

#### TC-F-007: Transparent Data Export (GDPR JSON Verbatim Dump)
- **Requirement**: Export all stored user data (subscriber details, connections, taste signals, digests) as versioned JSON.
- **Priority**: High
- **Preconditions**: Authenticated user with taste signals and digests.
- **Test Steps**:
  1. Call `GET /api/export/my-data?download=true`.
- **Expected Results**:
  - Returns HTTP 200 with `UserDataExportDto` (`ExportVersion = "1.0.0"`).
  - `Content-Disposition` attachment header is set with user ID in filename.
- **Postconditions**: No database state altered.

#### TC-F-008: Repository Query Operations
- **Requirement**: Verify repository retrieval logic for users, digests, tracks, and signals.
- **Priority**: Medium
- **Preconditions**: Test database with seeded entities.
- **Test Steps**:
  1. Query `WeeklyDigestRepository.GetRecentDigestsForUserAsync(userId, count: 2)`.
  2. Query `TrackRepository.FindByArtistAndTitleAsync("artist", "title")`.
- **Expected Results**:
  - Digests returned ordered by ISO week descending.
  - Track matched case-insensitively.
- **Postconditions**: Database unaffected.

---

### 2. Edge Case Tests

#### TC-E-001: User Profile with Empty or Null Sets
- **Requirement**: `UserTasteProfile` must handle null/empty sets safely without throwing `NullReferenceException`.
- **Priority**: Medium
- **Preconditions**: Empty `WeightedTagVector` and null collections passed to constructor.
- **Test Steps**:
  1. Query `IsArtistFamiliar("any")`, `IsTrackFamiliar("any")`, `IsTrackRejected("any")`.
- **Expected Results**: All queries return `false` cleanly.
- **Postconditions**: Profile remains immutable.

#### TC-E-002: ISO Week Boundary Transitions & Year Wrapping
- **Requirement**: Accurate ISO-8601 week number calculation on year boundaries (e.g. week 52/53 in early Jan or week 1 in late Dec).
- **Priority**: High
- **Preconditions**: DateTime instances for edge dates.
- **Test Steps**:
  1. Convert `2026-10-02` (Friday) to `IsoWeek`.
  2. Format and parse `IsoWeek` strings.
- **Expected Results**:
  - Produces `2026-W40`.
  - Format string conforms to `YYYY-Www`.
- **Postconditions**: String and value representations match.

#### TC-E-003: Taste Seeding Boundary Values (Min 3, Max 5)
- **Requirement**: Total seeds (tags + artists) must strictly fall in the range [3, 5].
- **Priority**: High
- **Preconditions**: `SeedTasteProfileCommandValidator`.
- **Test Steps**:
  1. Validate command with 3 seeds.
  2. Validate command with 5 seeds.
- **Expected Results**: Both commands pass validation without errors.
- **Postconditions**: Validation passes.

#### TC-E-004: Candidate Track Key Generation (MBID vs Normalized)
- **Requirement**: TrackKey prefers MBID when present; falls back to normalized `artist:title`.
- **Priority**: Medium
- **Preconditions**: Tracks with and without MBID.
- **Test Steps**:
  1. Check `TrackKey` for `Track.Create("Song", "Band", mbid: "MBID-123")`.
  2. Check `TrackKey` for `Track.Create("  Song  ", "  Band  ")`.
- **Expected Results**:
  - MBID track key: `mbid:mbid-123`.
  - Normalized track key: `band:song`.
- **Postconditions**: Track keys deterministic and lowercase.

#### TC-E-005: Case-Insensitive Matching for Queries & Familiarity
- **Requirement**: All lookups for artists, tags, tracks, and emails must ignore casing and leading/trailing whitespace.
- **Priority**: Medium
- **Preconditions**: Seeded artist `"Cocteau Twins"`.
- **Test Steps**:
  1. Check `IsArtistFamiliar("  COCTEAU TWINS  ")`.
- **Expected Results**: Returns `true`.
- **Postconditions**: None.

---

### 3. Error Handling Tests

#### TC-ERR-001: Invalid Seeding Input (< 3 or > 5 seeds, out-of-range weights)
- **Requirement**: Seeding validation must reject fewer than 3 seeds, more than 5 seeds, or weights outside [0.0, 1.0].
- **Priority**: High
- **Preconditions**: `SeedTasteProfileCommandValidator`.
- **Test Steps**:
  1. Test command with 2 seeds.
  2. Test command with 6 seeds.
  3. Test command with weight `1.5`.
- **Expected Results**: Validation errors generated for each scenario.
- **Postconditions**: Request rejected with validation details.

#### TC-ERR-002: Invalid Feedback Rating Range
- **Requirement**: Rating must be between 1 and 10 inclusive if provided.
- **Priority**: High
- **Preconditions**: `RecordRecommendationFeedbackCommandValidator`.
- **Test Steps**:
  1. Validate with rating `0`.
  2. Validate with rating `11`.
  3. Validate with rating `-5`.
- **Expected Results**: `ShouldHaveValidationErrorFor(x => x.Rating)` triggers for all invalid ratings.
- **Postconditions**: Request blocked before handler execution.

#### TC-ERR-003: Non-Existent Entity Lookups
- **Requirement**: Handlers and repositories must throw `NotFoundException` when referencing non-existent entities.
- **Priority**: Medium
- **Preconditions**: Empty database / mock repository returning null.
- **Test Steps**:
  1. Submit feedback for unknown recommendation ID.
  2. Delete non-existent user account.
- **Expected Results**: `NotFoundException` thrown with descriptive entity name and key.
- **Postconditions**: No state change or commit.

#### TC-ERR-004: Duplicate External Music Connection
- **Requirement**: User cannot link two accounts of the same external service type (e.g. two Last.fm accounts).
- **Priority**: Medium
- **Preconditions**: User with existing `LastFm` connection.
- **Test Steps**:
  1. Add a second connection with `MusicServiceType.LastFm`.
- **Expected Results**: Throws `InvalidOperationException`.
- **Postconditions**: User connections collection remains at 1.

#### TC-ERR-005: Out-of-Range Constructor Parameters
- **Requirement**: Value objects and entities reject out-of-range numerical parameters.
- **Priority**: Medium
- **Preconditions**: Constructors for `IsoWeek`, `User`, `CandidateTrack`, `ScoringParameters`.
- **Test Steps**:
  1. Construct `IsoWeek` with week 54.
  2. Construct `User` with delivery hour 25.
  3. Construct `CandidateTrack` with popularity 1.5.
  4. Construct `ScoringParameters` with negative weights.
- **Expected Results**: All throw `ArgumentOutOfRangeException`.
- **Postconditions**: Object instantiation rejected.

#### TC-ERR-006: Empty or Whitespace Names
- **Requirement**: Entity names (Track title, Artist name, User email, Album title) cannot be empty or whitespace.
- **Priority**: Medium
- **Preconditions**: Constructors for `Artist`, `Track`, `Album`, `User`.
- **Test Steps**:
  1. Instantiate with `""` or `"   "`.
- **Expected Results**: Throws `ArgumentException`.
- **Postconditions**: Object instantiation rejected.

---

### 4. State Transition Tests

#### TC-ST-001: WeeklyDigest Lifecycle State Transitions
- **Requirement**: Digest must transition sequentially: `Pending` -> `InProgress` -> `Sent`.
- **Priority**: High
- **Preconditions**: Newly created `WeeklyDigest`.
- **Test Steps**:
  1. Check initial state (`Pending`).
  2. Call `MarkInProgress()` -> state is `InProgress`.
  3. Call `MarkSent(timestamp)` -> state is `Sent`, `SentAt` recorded.
  4. Attempt `MarkInProgress()` after `Sent`.
- **Expected Results**: Transition 1–3 succeeds; Step 4 throws `InvalidOperationException` (terminal state).
- **Postconditions**: Digest status is `Sent`.

#### TC-ST-002: User Soft-Deletion & Global Query Filter
- **Requirement**: Soft-deleted users must have `IsDeleted = true` and be automatically filtered out from EF Core queries unless `IgnoreQueryFilters()` is specified.
- **Priority**: High
- **Preconditions**: Database with 1 active and 1 soft-deleted user.
- **Test Steps**:
  1. Query `context.Users.ToListAsync()`.
  2. Query `context.Users.IgnoreQueryFilters().ToListAsync()`.
- **Expected Results**: Query 1 returns only active user; Query 2 returns both users.
- **Postconditions**: Deleted records preserved for audit compliance.

#### TC-ST-003: UserMusicConnection Activation / Deactivation
- **Requirement**: Connections can be deactivated and reactivated without deleting historical links.
- **Priority**: Medium
- **Preconditions**: Active connection.
- **Test Steps**:
  1. Call `Deactivate()` -> `IsActive == false`.
  2. Call `Activate()` -> `IsActive == true`.
  3. Call `MarkSynced(timestamp)` -> updates `LastSyncedAt`.
- **Expected Results**: State correctly toggles; audit timestamp updated.
- **Postconditions**: Connection status is `Active`.

#### TC-ST-004: Duplicate Track Prevention in Digest
- **Requirement**: A digest cannot contain duplicate tracks.
- **Priority**: High
- **Preconditions**: Digest containing Track A.
- **Test Steps**:
  1. Call `AddRecommendation(TrackA, breakdown, rank: 2)`.
- **Expected Results**: Throws `InvalidOperationException`.
- **Postconditions**: Digest continues to have only 1 recommendation.

---

## Test Coverage Matrix

| Requirement / Component | Functional Tests | Edge Cases | Error Handling | State Transitions | Automated Test Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Residue Scorer (Prism)** | TC-F-001, TC-F-002 | TC-E-006 | TC-ERR-005 | — | ✓ Complete (`RecommendationScorerTests`, `BenchmarkSeedFixtures`) |
| **Catalog Entities (Track/Artist/Album)** | TC-F-008 | TC-E-004 | TC-ERR-006 | — | ✓ Complete (`TrackAndArtistTests`) |
| **Subscriber & User Aggregate** | TC-F-008 | TC-E-005 | TC-ERR-005, TC-ERR-006 | TC-ST-002, TC-ST-003 | ✓ Complete (`UserAndConnectionTests`, `SubscriberQueryAndCommandTests`) |
| **Weekly Digest & Lifecycle** | TC-F-006 | TC-E-002 | TC-ERR-005 | TC-ST-001, TC-ST-004 | ✓ Complete (`WeeklyDigestTests`, `ComprehensiveRepositoryTests`) |
| **Taste Seeding & Signals** | TC-F-003, TC-F-005 | TC-E-001, TC-E-003 | TC-ERR-001 | — | ✓ Complete (`SeedTasteProfileTests`, `TasteSignalTests`, `UserTasteProfileExtendedTests`) |
| **Granular Feedback (1–10)** | TC-F-004, TC-F-005 | — | TC-ERR-002, TC-ERR-003 | — | ✓ Complete (`DigestAndFeedbackTests`) |
| **Repositories & DataContext** | TC-F-008 | TC-E-005 | TC-ERR-003 | TC-ST-002 | ✓ Complete (`ComprehensiveRepositoryTests`, `AppDbContextModelTests`) |
| **Export & GDPR Transparency** | TC-F-007 | — | TC-ERR-003 | — | ✓ Complete (`ExportControllerTests`, `GetUserDataExportTests`) |
| **Architecture Boundaries** | — | — | — | — | ✓ Complete (`OnionArchitectureTests`, `ArchitectureTests`) |

---

## Notes
- All 171 automated unit and integration tests across the 4 test projects (`Domain.Tests`, `Application.Tests`, `DataAccess.Tests`, `Presentation.Tests`) execute synchronously in under 2 seconds.
- Database access tests utilize EF Core in-memory isolation per test run to prevent cross-test state pollution.
- Upstream external HTTP calls remain strictly isolated and mocked behind repository and provider interfaces.
