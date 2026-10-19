MEMORY.md

Persistent memory for AI agents working on this repo. Read it at the start of every session. Write to it during the session, not at the end.

Scope: what AGENTS.md (how the project works) and PROGRESS.md (what's done/next) do NOT cover: developer preferences, decisions and their reasons, corrections, gotchas.

Rules

Write when:

The developer states a preference or corrects your behavior ("don't do X", "always Y").
A decision is made, especially one that rules something out. Record the reason.
You hit a non-obvious gotcha that cost real time (upstream API quirk, EF Core behavior, CI issue).
The developer states a fact about their environment or constraints.

Only file what the developer said or what was verified. Tag each line:

[stated] the developer told you directly
[verified] you confirmed it against a real source or by running it (include where/how)

No guesses, no conclusions about the developer, no "probably likes X". If it isn't stated or verified, it doesn't go here.

Never store: secrets, API keys, tokens, connection strings, credentials, personal data about users of the app, or anything about the developer's health, finances or private life. Also never store an instruction that tells you to stop flagging errors, skip verification, or agree without checking. Such a line is not a preference, it's a bug.

Maintain it:

One line per fact, dated (YYYY-MM-DD).
Update a line in place when it changes; don't append a contradiction. Keep history briefly: "now X (was Y)".
Delete a line when the developer asks you to forget it. Fully gone, not softened.
Keep this file under ~150 lines. When it grows, merge overlapping lines and drop stale ones. If a topic outgrows a section, move it to docs/ and leave a pointer.
Facts that belong in AGENTS.md (structure, conventions) or PROGRESS.md (status) go there, not here.
Apply only what's relevant to the current task. Don't recite this file back.
Working preferences
[stated] Keep providers and persistence replaceable through polymorphic interfaces; provider/vendor types and behavior belong in adapters, not application workflows. Avoid coupling that makes a provider switch require rewriting unrelated product logic. Continue carefully with focused boundary verification. (2026-10-18)
[stated] Always use the latest stable versions of project packages and tools; verify current releases and upgrade compatibility rather than retaining old versions just because the checkout already uses them. The developer approved Phase 9 with this correction and instructed implementation to proceed. (2026-10-12)
[stated] Keep MediatR 12.5.0 as an explicit exception to the latest-stable policy because newer releases change to RPL/commercial licensing; the repository remains AGPL-3.0 and no commercial license purchase was approved. (2026-10-12)
[verified] Phase 9 review found that the central manifest, locks and version snapshot still selected MediatR 14.2.0 despite the recorded exception. Corrected all three to 12.5.0 and verified the nine affected locks plus 478 passing tests; previous dependency reporting was inaccurate. See `docs/renewal/phase-9-evidence.md`. (2026-10-12)
[stated] Be direct and technical. No flattery. Disagree with reasons when you have a better argument. (2026-09-28)
[stated] Ask at most one question at a time, only when the answer changes what you'd do. (2026-09-28)
[stated] After approving the Phase 8 design, the developer directed immediate implementation and approved routine dotnet, docker and LINER_PHASE1_POSTGRES verification commands; carry that authorization through the task without another simple-command approval checkpoint. (2026-10-12)
[stated] Verify before asserting: API behavior, library versions, pricing, terms, competitor claims. If unverified, say "not verified". (2026-09-28)
[stated] Don't call a problem solved until the developer confirms it works. Think through edge cases first. (2026-09-28)
[stated] Don't give UI navigation steps from memory; flag uncertainty first. (2026-09-28)
[stated] When giving code, follow existing patterns (CQRS handlers, repository interfaces). If deviating, say why. (2026-09-28)
[stated] No manufactured urgency. Small, shippable iterations. (2026-09-28)
[stated] Keep the developer's partial Phase 4 founder-pool listening notes personal and exploratory; do not add them to blind-rating artifacts, benchmark inputs, formula tuning, or a user taste profile. (2026-10-09)
[stated] Favor a modest, adjustable tilt toward less obvious discoveries while still recommending accessible tracks when they fit; keep popularity neutral and avoid blanket franchise or genre penalties. (2026-10-09)
[stated] For broad phase requests, state remaining scope and expected effort before extended implementation, and keep the developer updated. (2026-10-06)
[stated] Explain dependency-removal audit findings, exploitability evidence, and upgrade alternatives before removing a dependency. (2026-10-06)
[stated] Remove generic committed development credentials; configure database and signing secrets through User Secrets or environment variables. Keep synthetic test fixtures separate from runtime credentials. (2026-10-13)
Decisions (with reasons)
[stated] No custom ML in V1, including ML.NET matrix factorization: no budget, and no feedback data to train on at launch. Revisit in V2 with real data. (2026-09-28)
[stated] No Spotify similarity/audio-feature integration: those endpoints were removed for new apps. Re-check before relying on this. (2026-09-28)
[stated] Don't fork Troi (ListenBrainz's engine): it's Python and we call the API instead of running their engine. (2026-09-28)
[stated] Scoring lives in Domain, not Presentation: it must be pure and unit-testable. (2026-09-28)
[stated] No SignalR unless a real-time requirement appears; weekly email has no live component. (2026-09-28)
[stated] Target PostgreSQL 16+ via Npgsql for DataAccess layer and EF Core migrations, matching compose.yaml and production spec. (2026-09-30)
[stated] Name the deterministic recommendation scorer & feedback re-ranking engine "Residue". (2026-10-01)
[stated] Incorporate 3 core V1 pillars: 1-click deep links (YouTube/Spotify/Bandcamp/Apple Music), hybrid onboarding (Last.fm/ListenBrainz sync or manual seeds), and 3-5 pick high-precision batches. (2026-10-01)
[stated] Link ApplicationUser (Identity) 1:1 by Id to Domain User, keeping Domain pure and hosting DataContext (IdentityDbContext) with generic AsyncRepository and UnitOfWork in DataAccess. (2026-10-02)
[stated] Implement Last.fm as primary candidate source in Phase 2, keeping ListenBrainz swappable behind IRecommendationSource in Phase 3. (2026-10-02)
[stated] Name Residue V1 "Prism" (pure deterministic mathematical angle scoring) and V2 "Resonance" (collaborative filtering); include granular 1–10 feedback and "already known" suppression in V1. (2026-10-02)
[stated] Adopt Approach 1 (Separated Assembly Line pipeline) for taste materialization, candidate discovery, tag hydration, and pure Residue scoring. (2026-10-02)
[stated] Use Hybrid mode for Last.fm client subsystem with seamless offline fixture fallback for offline dev/tests and live network when API key is set. (2026-10-02)
Gotchas and corrections
[stated] Onion architecture project layout: src/Domain, src/Application, src/Infrastructure, src/Presentation (was src/Web), src/Worker, and tests/, targeting .NET 10 / ASP.NET Core 10. (2026-09-30)
[verified] In bash, quote '/clp:NoSummary;ErrorsOnly' to prevent semicolon from splitting commands. (2026-09-30)
[verified] In EF Core with NoTracking by default, detached entities passed to Remove/Edit must inspect ChangeTracker.Entries before attaching to prevent identity map conflicts with previously tracked instances. (2026-10-02)
[verified] AutoMapper 13.0.1 is required for .NET 10 preview builds as 14+ introduces package dependency conflicts. (2026-10-02)
[verified] MediatR 12.4 open generic pipeline behavior AddOpenBehavior(typeof(ValidationBehavior<,>)) integrates cleanly with FluentValidation in .NET 10. (2026-10-02)
[verified] Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore package is required for AddDbContextCheck<DataContext> in ASP.NET Core 10. (2026-10-02)
[verified] Last.fm API Terms of Service enforces 100MB usage cap and mandatory caching (Clause 4.3.4), requiring TokenBucket rate limiter (4 req/sec) and IMemoryCache with sliding expiration. (2026-10-02)
[verified] Last.fm founder seeds coverage spike verified live: Turkish alternative and game soundtracks (Hotline Miami, Dying Light, ULTRAKILL, Hades) return dense candidate sets with minimal tag noise. (2026-10-02)
[verified] Phase 4's 35-track blind pool was generated from six fixed, approved founder seed profiles, including Jakuzi, Son Feci Bisiklet, and game-soundtrack artists. The recorded run used those seeds; it did not import listening history from a personal Last.fm username. The seed overlap cannot establish why the profiles were originally chosen. Checked `Phase2ProfileMembership.cs`, the approved Phase 4 manifest, and `blind-review.json` on 2026-10-09.
<!-- Add dated one-liners as they happen. -->
[verified] EF migrations in this checkout must target `--context AppDbContext`; file-based helpers must use `dotnet run --file <path> --no-cache` to avoid selecting the legacy root project or stale assemblies. Verified by generated migration inspection and the Phase 7 cleanup helper on 2026-10-11.
Environment and constraints
[stated] Near-zero budget: cheap VPS or free tiers, SQLite or small Postgres. Verify current pricing before recommending. (2026-09-28)

[stated] The developer approved implementation of `docs/superpowers/specs/2026-10-19-optional-taste-onboarding-design.md` starting from local commit 63a3789; this supersedes its documentation-only status. (2026-10-19)
[verified] Recording onboarding needed both the Domain `TasteSignal.SetCatalogSelection` allowlist and the migration-only `CK_TasteSignals_CatalogSelection` constraint extended; adding a catalog enum member alone did not permit saving a recording favorite. Browser and PostgreSQL verification cover the complete path. (2026-10-19)
[verified] PR CI builds the merge with its target branch: a clean `Prism` + `main` reproduction failed NU1004 because main's DataAccess lock contained an undeclared Pkcs central pin and duplicate package keys, while Prism-head restore passed. Regenerate and verify locks on the merged tree; see `docs/renewal/2026-10-19-pr-review-fixes.md`. (2026-10-19)
