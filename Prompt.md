Liner Notes — Brief: Solution Structure & Domain Layer

This is context, not a spec to execute literally. Understand the product and the reasoning, then make the engineering decisions yourself — naming, exact file layout, how you split things into classes, how you handle edge cases. Where this brief and your own judgment disagree on something small, say so and explain why rather than silently picking one.

What we're building

Liner Notes is an open-source web app that emails each user a personalized weekly music discovery list. It does not run its own recommendation engine or ML model. It pulls similarity and listening data from Last.fm and ListenBrainz, applies a small deterministic scoring layer on top, and stores user feedback to improve future weeks.

The pitch is a stance, not a technology claim: "your taste, explained and owned by you, not a black box optimizing for what's already famous." Three things follow from that positioning, and they should shape naming and structure, not just marketing copy later:

Every recommendation must be explainable. The reason a track was picked has to be a real, inspectable record of what the scoring logic actually computed — shared tags, similarity signal, penalties applied — not a sentence generated after the fact to sound plausible. If a recommendation entity can't reconstruct "why this pick" from data it actually stored, the feature doesn't exist.
Anti-popularity-bias is a real mechanic, not a marketing line. Whatever scoring approach you design needs an actual term that pushes back against globally popular candidates, not just a vague "diversity" intention.
We never claim to have built the underlying similarity/recommendation data. We built a scoring and delivery layer on top of Last.fm and ListenBrainz. Keep that distinction clear in naming and comments — don't let class or namespace names imply we run our own recommendation model.
Architectural shape

Clean/Onion Architecture, five projects, dependencies point inward only:

Domain — entities, value objects, and the scoring logic itself. Zero external dependencies. Everything in here should be unit-testable without a database, an HTTP client, or a DI container. This is the project you're filling in for this pass.
Application — CQRS use cases (MediatR-style commands/queries), the interfaces Infrastructure will implement (music source client, email sender, repositories), orchestration logic. No business rules here that belong in Domain — this layer coordinates, it doesn't decide.
Infrastructure — EF Core, the Last.fm/ListenBrainz HTTP clients, the email sender implementation, caching. Implements Application's interfaces.
Web — thin ASP.NET Core presentation project: endpoints, auth wiring, DI composition root. No scoring logic, no direct external API calls from here — that was a mistake made once already on this project and shouldn't repeat.
Worker — a separate background service project that runs the weekly batch: fetch data, score, persist, send email. Its own composition root, shares the same Application/Infrastructure/Domain core as Web.
Tests — unit tests. The Domain scorer having tests is not optional; treat it the same as the code itself.

Set up all five projects (plus the test project) with the right project reference direction between them now, even though only Domain gets real content in this pass. The others should exist as empty, correctly-wired shells so the next work session drops code into the right place instead of figuring out structure again.

Target .NET 8 / EF Core 8 — that's the stack already used elsewhere in this person's work, not the newer version you might default to.

What NOT to reach for

A related project earlier used an enterprise-scale reference architecture (SignalR + Redis backplane for real-time features, Dapper alongside EF Core for high-frequency reads, a two-tier Redis-backed cache, HTTP/3 and connection-pool tuning for concurrency spikes, several parallel API surfaces for different user roles). None of that was arbitrary there — it matched a live, high-traffic hackathon platform. None of it fits here: a weekly email digest for a small number of users, hosted on the cheapest possible VPS, with no live/push component and one user role. If you notice yourself about to add a message bus, a distributed cache, or a second data access technology "to be safe" or "for scale," stop — that's solving a problem this product doesn't have. Plain EF Core and an in-process memory cache are enough. If a genuine real-time or high-concurrency requirement shows up later, that's a conversation to have then, not something to pre-build.

The Domain layer, in detail
The music catalog

The system needs to represent artists, albums, and tracks well enough to store recommendations and match them against a user's taste — not a full music metadata service. Each of these should be identifiable in a way that lets Infrastructure map them back to Last.fm and/or ListenBrainz records later (both services key primarily off MusicBrainz IDs, with Last.fm also usable by plain name). Don't model audio-level detail (waveforms, embeddings, bitrate) — this system never touches actual audio, and nothing in the scoring approach needs it.

Tags matter more than genre labels here — Last.fm and ListenBrainz both expose free-form, weighted tags per artist/track ("black metal", "90s", "atmospheric"), and that weighted tag data is the raw material the scorer compares against a user's taste. Model tags as their own concept with a normalized identity (so "Metal" and "metal" don't become two different things), and model the association between a track (or artist, if track-level tags aren't available from a given provider) and a tag as carrying a strength/weight, not just a boolean "has this tag."

Taste signals

How a user's taste gets represented needs to support three things at once, and this is genuinely unresolved — don't lock in a specific seeding mechanism (Last.fm username import vs. manual artist picks vs. something else) while building this:

It has to be usable as input to the scorer (a weighted set of tags/artists).
It has to grow from feedback over time — a like or dislike should be able to nudge future scoring, with some record of why a given adjustment exists.
It has to be fully exportable and deletable, verbatim, for the "your data" page and account deletion. Whatever you store here, assume it will be read back to the user exactly as stored.

Model this as small, atomic, provenance-tagged records rather than a single mutable "taste profile" blob — a record of where a given taste signal came from (an initial seed, a like, a dislike, a comment) is worth more than a single aggregated number, both for the transparency requirement and because aggregating those signals into an actual scoring input is arguably an Application-layer concern, not something Domain should own the computation of.

Recommendations and feedback

A recommendation is tied to a user, a track, and a specific week, and it has to carry a full, structured record of how its score was computed — not a single number. That structured record is the entire "why this pick" feature; treat it as load-bearing, not decorative. Feedback (like/dislike/comment) is tied to a specific recommendation and should be independently exportable and deletable, same as taste signals.

Enforce that a user can't get two digests for the same week — that's a real constraint that came up in the reference architecture for a good reason (idempotent batch delivery), and it still applies here even without a Worker implementation yet.

The scorer

This is the most important part of this pass. Four things need to combine into a single score, and — critically — the weight given to each of them must be configurable, not hard-coded, because the first set of weights is guaranteed to be wrong and will need tuning against real feedback later:

Tag-overlap similarity between a candidate and the user's taste. Cosine similarity over weighted tag vectors is the intended approach — figure out the right shape for a reusable "weighted tag vector" concept, since you'll want it on both the candidate side and the user-taste side, and you'll want to reuse whatever similarity math you write in more than one place later.
A popularity penalty — this is the actual anti-popularity-bias mechanic. A more globally popular candidate should score lower, all else equal.
A novelty adjustment — something the user hasn't already encountered should be preferred over something they already know.
A feedback adjustment — something the user has explicitly rejected before should be penalized independently of the general taste-overlap score, so an outright "no" persists even if the surrounding taste vector would otherwise recommend it again.

The scorer itself must be pure: no I/O, no randomness, no hidden state — same inputs, same output, every time. That determinism is what makes the "why this pick" explanation trustworthy; if the same candidate could score differently between two runs with nothing else changing, the explanation stops meaning anything. Whatever breakdown structure you persist per recommendation should be rich enough that a human-readable explanation can be built directly from it without inventing anything not present in the data.

Testing expectation

Unit tests for the scorer aren't optional — cover at least: a close taste match scoring higher than a distant one, a highly popular candidate scoring lower than an equally-matching obscure one, a previously-rejected candidate being penalized, and determinism (same inputs producing the same output). Also set up a small fixed benchmark set of seed artists spanning a handful of genres (metal, hip-hop, electronic, jazz, pop, indie, classical, some regional/local scene) as a fixture — real artist choices can come later, but the fixture should exist as a place to sanity-check future changes to the scoring weights against a consistent, known input.

What this pass is not

Don't implement Application, Infrastructure, Web, or Worker logic — those projects should exist structurally (correct references, ready to receive code) but stay empty otherwise. Don't add ML.NET, matrix factorization, or any trained model — there's no feedback data yet to train on, and that's explicitly a later-stage idea gated on having real data. Don't pick a license or finalize the project name — both are still open. Don't add real-time delivery, notifications, or anything resembling a push channel.

When you're done

Update the README and CHANGELOG to reflect this as the first real scaffold of the project (not placeholder text — actually describe what exists). If you had to make a judgment call anywhere this brief left open, note it clearly rather than letting it pass silently — that includes anything you decided about file/folder naming, how you structured the taste-signal aggregation, or how you shaped the tag-vector abstraction.