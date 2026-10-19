# Optional taste onboarding execution

The developer approved implementation on 2026-10-19, superseding the spec's documentation-only status. Extend the existing listener profile, catalog, job and feedback boundaries; no new framework or dependency.

1. Persist a versioned questionnaire and resumable step on the listener profile. Use its revision, coordinated writes, export, deletion and attachment. Import fills unanswered values and requires an explicit conflict preview/choice. Save taste independently of starting jobs, including an empty selection.
2. Ship an original, maintained local vocabulary (genres, moods and sounds), normalized alias search and paging. No external taxonomy is copied. Remove saved selection count limits. Retain transport and storage admission limits. Rotate bounded acquisition windows by completed generation count. Aggregate the full saved tag profile with a normalized group influence; store a bounded, replayable scoring projection and a full-input hash/count.
3. Extend the catalog to recording identities and immutable credits/version snapshots. Add direct recording preparation behind provider interfaces. Unsupported direct evidence is an explicit coverage gap, never artist substitution. Album similarity queries credited artists and records the original album, queried artist, response provenance and configured reduced influence independently of album tags.
4. Apply versioned deterministic selection: new artists prioritizes unfamiliar artists; favorites prioritizes supported overlap; different styles prioritizes lower overlap among supported provider candidates; mixture retains baseline ordering. Close/surprise use the same supported overlap ordering, balance retains baseline. Goal takes precedence, then exploration, then vocal evidence, then baseline score and ordinal identity. Soup admits an additional rotating vocabulary window without restricting candidates to selected styles. Vocals/instrumental only use explicit candidate descriptive tags; conflicting/missing evidence is unknown and sits between matching and contrary evidence. Questions alone cannot establish a taste reference and route to Recent with an explanation. No new numerical scoring weights are invented.
5. Publish a separate public music projection atomically with successful completion, only from current eligible shared catalog metadata. It has no private foreign keys, counters, timestamps or explanations. Independent sequence, deduplicate music without bumping recency, bounded local cursor reads. Keep at most the latest 1,000 distinct eligible records; delete expired metadata during publication/read maintenance. No recovery outbox containing private IDs. Retry is the existing completion transaction. Synthetic records are separately labeled.
6. Implement welcome/account choice, three optional questions together, searchable tags/soup, artist/album/track favorites, review and results/Recent. Save on each transition. Preserve failed-request drafts and authentication transitions; focus headings after navigation. Reuse existing feedback.
7. Verify Domain/Application/adapter tests, PostgreSQL migrations/accounting/lifecycle, benchmark seeds and desktop/mobile browser journeys. Update changelog, progress and evidence. Make focused local commits after passing checks, no push. Provider fixtures prove mechanics only; developer confirmation and live coverage remain separate.

Verification and implementation details belong in the accompanying evidence document. This is a substantial multi-stage change, comparable to the spec's 5–8 day estimate; report any incomplete acceptance criteria explicitly.


Execution result: implemented and locally verified on 2026-10-19. See
[the evidence](../../renewal/optional-taste-onboarding-evidence.md) for the 669
.NET tests, 21 browser checks, migration/accounting coverage and provider limits.
Developer confirmation remains pending. The favorite-count proposal is resolved
by retaining all saved selections and bounding per-generation work, rather than
adding a new product count cap. No deployment, live acquisition or push occurred.
