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
Phase 4: Domain scorer
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
Taste seeding flow (see Open decisions)
Phase 6: Feedback
Like / dislike / comment per recommendation
Persistence
Feedback used in re-ranking
Phase 7: Worker and email
Verify current free/low-cost email options
Weekly digest generation (MediatR command)
Email sender implementation
Unsubscribe in every message, no tracking pixels
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
Copy reviewed for overclaims (no "new algorithm")
Privacy copy matches what the code stores
Open decisions (raise when relevant, don't decide silently)
Taste seeding: Last.fm username, ListenBrainz username, manual artist picks, or a combination, and the privacy implications of each
Primary upstream in V1 and how to blend both
Project name, branding, license
Email provider and hosting (verify first)
ML.NET matrix factorization: V2 only, and only with real feedback data
Ideas parking lot

Not in V1. Add anything that comes up mid-build, one line each, with the date.

Log

Short dated entries for decisions and notable changes (mirrors CHANGELOG for user-visible ones).
- 2026-09-30: Solution skeleton set up (Onion architecture: Domain, Application, Infrastructure, Web, Worker, tests). Pure deterministic recommendation scorer with 4 components, score breakdown, and benchmark fixture implemented. Ready for developer review.