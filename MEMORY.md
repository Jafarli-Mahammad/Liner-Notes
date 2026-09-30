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
[stated] Be direct and technical. No flattery. Disagree with reasons when you have a better argument. (2026-09-28)
[stated] Ask at most one question at a time, only when the answer changes what you'd do. (2026-09-28)
[stated] Verify before asserting: API behavior, library versions, pricing, terms, competitor claims. If unverified, say "not verified". (2026-09-28)
[stated] Don't call a problem solved until the developer confirms it works. Think through edge cases first. (2026-09-28)
[stated] Don't give UI navigation steps from memory; flag uncertainty first. (2026-09-28)
[stated] When giving code, follow existing patterns (CQRS handlers, repository interfaces). If deviating, say why. (2026-09-28)
[stated] No manufactured urgency. Small, shippable iterations. (2026-09-28)
Decisions (with reasons)
[stated] No custom ML in V1, including ML.NET matrix factorization: no budget, and no feedback data to train on at launch. Revisit in V2 with real data. (2026-09-28)
[stated] No Spotify similarity/audio-feature integration: those endpoints were removed for new apps. Re-check before relying on this. (2026-09-28)
[stated] Don't fork Troi (ListenBrainz's engine): it's Python and we call the API instead of running their engine. (2026-09-28)
[stated] Scoring lives in Domain, not Presentation: it must be pure and unit-testable. (2026-09-28)
[stated] No SignalR unless a real-time requirement appears; weekly email has no live component. (2026-09-28)
Gotchas and corrections
[stated] Onion architecture project layout: src/Domain, src/Application, src/Infrastructure, src/Web, src/Worker, and tests/, targeting .NET 10 / ASP.NET Core 10. (2026-09-30)
[verified] In bash, quote '/clp:NoSummary;ErrorsOnly' to prevent semicolon from splitting commands. (2026-09-30)
<!-- Add dated one-liners as they happen. -->
Environment and constraints
[stated] Near-zero budget: cheap VPS or free tiers, SQLite or small Postgres. Verify current pricing before recommending. (2026-09-28)