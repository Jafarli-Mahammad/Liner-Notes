Project

Open-source website that emails each user a weekly music discovery list. Every pick shows WHY it was chosen (stored score breakdown). V1 is popularity-neutral; no popularity preference or penalty is adopted. Users can see and export all taste data we hold.

No own recommendation engine, no ML. Candidates come from Last.fm / ListenBrainz; we apply our own deterministic scoring and feedback re-ranking.
Never claim a new algorithm or imply we built the similarity data. Flag README/copy that overclaims.
Structure (Onion; dependencies point inward only)
- src/Domain/ (LinerNotes.Domain): entities, value objects, PURE scorer (tag overlap, novelty, ranking policy). Zero external deps.
- src/Application/ (LinerNotes.Application): MediatR commands/queries + interfaces (IRecommendationSource, IEmailSender, repositories). Orchestration only.
- src/DataAccess/ (LinerNotes.DataAccess): EF Core (AppDbContext, Entity Configurations, Migrations).
- src/Infrastructure/ (LinerNotes.Infrastructure): Last.fm/ListenBrainz HTTP clients (HttpClientFactory), email sender, in-memory caching.
- src/Presentation/ (LinerNotes.Presentation): thin ASP.NET Core host. Endpoints, auth wiring, DI root, UI. No scoring logic, no direct upstream API calls.
- src/Worker/ (LinerNotes.Worker): weekly batch (digest generation + email). Second composition root over the same core.
- tests/Domain.Tests/ (LinerNotes.Domain.Tests): Domain scorer tests (mandatory); benchmark seed set fixture; architectural validation.
Conventions
C# / .NET, EF Core 10, MediatR, async all the way. CQRS handlers, repository interfaces.
Scoring weights are configuration, never hard-coded.
Every recommendation persists its score breakdown. Explanations come from that data, never generated text.
Last.fm and ListenBrainz must stay swappable behind the provider abstraction. Upstream terms changes must not touch Domain or Application.
Cache upstream responses; respect rate limits. Never call upstream per page view.
Data minimization: store only what scoring/feedback needs. The "your data" page and JSON export must cover every stored table/column.
Email: unsubscribe in every message, no tracking pixels.
Secrets never in source control.
Skip layers where there is no real domain logic.
Out of scope for V1 (do not add unless told)

Custom ML / ML.NET, Spotify similarity or audio features, SignalR, payments, social features, playlist sync, native mobile.

Existing helpers to reuse

None yet. Add entries here as they land (rate limiter, upstream cache, etc.), one line each with path.

Rules for agents
Verify API terms, rate limits, endpoints, library versions and pricing before coding against them. If unverified, say so.
Don't call something done until the developer has confirmed it works.
Changes to the scorer must be sanity-checked against the benchmark seed set (tests/ once it exists).
Update CHANGELOG.md for user-visible changes.

Read MEMORY.md and PROGRESS.md at session start; follow the write rules in MEMORY.md.