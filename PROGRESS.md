PROGRESS.md

Roadmap and status for V1. Tick a box only after the developer has confirmed it works. After each phase: review together before moving on.

Current phase: 0 (naming/branding, in progress outside the repo)

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
IRecommendationSource abstraction
Verify current Last.fm terms (licence, attribution, caching, rate limits)
Last.fm client (HttpClientFactory) with caching and rate limiting
Tests with faked HTTP
Phase 3: ListenBrainz
Verify current endpoints, auth, rate limits, terms
ListenBrainz client behind the same abstraction
Decide primary upstream and blending (see Open decisions)
Phase 4: Domain scorer ("Residue" Engine)
Name: "Residue" (pure deterministic recommendation and feedback re-ranking engine)
Tag-overlap similarity
Popularity penalty
Novelty penalty/boost
Feedback adjustment
Configurable weights
Score breakdown model (feeds "why this pick")
Unit tests (mandatory)
Benchmark seed set (metal, hip-hop, electronic, jazz, pop, indie, classical, regional)
Phase 5: Accounts and taste seeding
ASP.NET Identity + hardening (Data Protection, anti-forgery, auth rate limiting)
Taste seeding flow (Hybrid onboarding: Last.fm/ListenBrainz sync + manual 3–5 seed artists/tags fallback)
Phase 6: Feedback
Like / dislike / comment per recommendation
Persistence
Feedback used in re-ranking
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
Primary upstream in V1 and how to blend both
Project name, branding, license
Email provider and hosting (verify first)
ML.NET matrix factorization: V2 only, and only with real feedback data

Core Product & Retention Pillars (V1 Guidelines)
1. **Zero-Friction Listening (The "Copy-Paste" Problem)**: Every pick in the email and web UI must feature direct 1-click deep-links (YouTube, Spotify, Bandcamp, Apple Music search links) so users don't have to manually search in streaming apps.
2. **Hybrid Onboarding (The Cold-Start Problem)**: Allow power users to connect existing Last.fm / ListenBrainz profiles, while offering casual users a simple 3–5 seed artist/tag selector to start discovering immediately.
3. **High-Precision Batches (Weekly Retention)**: Limit weekly discovery batches to 3–5 high-confidence picks to avoid overwhelm and recommendation misses, paired with effortless 1-click feedback (thumbs up / thumbs down / "already know this").

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

* Artist discovery notifications — notify verified artists when their music generates meaningful aggregate discovery through the service; never expose individual listener identities or let artist participation influence Residue ranking — 2026-10-01

Ideas parking lot

Not in V1. Add anything that comes up mid-build, one line each, with the date.

Log

Short dated entries for decisions and notable changes (mirrors CHANGELOG for user-visible ones).
- 2026-09-30: Solution skeleton set up (Onion architecture: Domain, Application, Infrastructure, Web, Worker, tests). Pure deterministic recommendation scorer with 4 components, score breakdown, and benchmark fixture implemented. Ready for developer review.
- 2026-09-30: DataAccessLayer finished: PostgreSQL 16+ (via Npgsql) configured, AppDbContext with IAppDbContext, IDataProtectionKeyContext, automatic audit timestamps, complete entity configurations, repositories (User, WeeklyDigest, TasteSignal, Track), InitialCreate migration generated, and 11 unit/model tests passing.
- 2026-10-01: Named the deterministic recommendation & feedback re-ranking engine "Residue". Added 3 core product pillars to PROGRESS.md (Zero-Friction Deep Links, Hybrid Onboarding, and 3-5 Track High-Precision Batches).
