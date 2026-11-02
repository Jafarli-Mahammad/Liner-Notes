# Residue recommendation quality renewal

Date: 2026-11-02  
Status: Proposed; awaiting approval of Phase 1. No implementation phase is authorized.  
Investigation baseline: branch `Prism`, local commit `5a31db7`.

## Objective and working agreement

Improve Liner Notes' relevance, personalization, and ability to discover music individual users will love while preserving the functioning beta application, explainability, security, privacy, and performance. Support strong favorite-connected matches and exceptional discoveries without defining universal musical quality. Prefer fewer convincing recommendations over filling a quota with weak matches. Learn from actual feedback while retaining established interests.

This is an incremental improvement project. Preserve the Clean/Onion boundaries, deterministic scoring, provider abstraction, and popularity-neutral policy. Additional infrastructure budget is effectively $0. Exclude free-text feedback interpretation, paid AI APIs/subscriptions, model training, GPU infrastructure, ML.NET recommendation models, and a requirement to use Last.fm. Notes remain stored, displayed, and exported without becoming recommendation signals.

The planning investigation used the brainstorming skill and Sequential Thinking MCP, inspected current implementation and tests, reviewed the taste-input audit at local HEAD, and checked official provider documentation. No application behavior was changed and no tests were rerun during that investigation.

Each phase requires explicit approval. After an approved phase, implement only that phase, verify it, report changes, actual observed behavior, limitations, and unresolved issues, and create a focused local commit when verification permits. Stop for the developer's next instruction. Do not push or create pull requests without explicit authorization. Developer confirmation is required before calling the problem solved.

## Current implementation assessment

Preserve these strengths: deterministic scoring, canonical entity handling, provider separation, stored numerical explanations, bounded acquisition, and explicit evidence provenance. The implementation distinguishes recording-level evidence from artist-level fallback and suppresses selected favorite recordings and previously recommended identities.

| Area | Verified behavior | Implication |
| --- | --- | --- |
| Favorites | Artists, albums, recordings, and positive feedback share a rotating entity-materialization window, default 20. Acquisition has additional limits. | A favorite can temporarily disappear from scoring evidence, not only acquisition requests. |
| Affinity | Active scoring primarily combines aggregate tag cosine similarity and an unfamiliar-artist bonus. | Separate interests may be diluted in one vector; impact on musical enjoyment remains a hypothesis. |
| Recording evidence | ListenBrainz recording similarity already exists. Hydration uses recording tags when available, otherwise artist tags; candidates without usable tags can be dropped. | Useful recording relationships may survive where tags do not. Measure actual losses. |
| Preferences | Goal, exploration, vocals, and score are successive sorting keys. | Earlier keys can neutralize later controls. Different-styles selection can favor low overlap without another convincing connection. |
| Feedback | Positive learning uses recent feedback rows. Comment-only changes can consume the window and update recency. | Established interests can disappear without a musical preference change. |
| Negative feedback | Disliked tracks are suppressed without broad artist/genre penalties. | Preserve that restraint while investigating repeated negative patterns. |
| Selection | One result per normalized artist name, then take the requested number. | Basic diversity exists, but sufficient relevance is not established for every pick. |
| Explanations | Numerical replay is stronger than complete selection replay. | A receipt can describe a requested preference without showing whether it affected selection. |

Source references:

- [Taste-input audit](../../architecture/2026-11-02-taste-input-audit.md).
- [Taste materialization](../../../src/Application/Features/Discovery/MaterializeDiscoveryTaste.cs).
- [Effective feedback inputs](../../../src/Domain/Taste/EffectiveTasteInputs.cs).
- [Feedback loading](../../../src/DataAccess/Persistence/Repositories/DiscoveryBatchStore.cs).
- [Active scorer](../../../src/Domain/Scoring/BaselineAScorer.cs).
- [Selection policy](../../../src/Domain/Discovery/DiscoverySelectionPolicy.cs).
- [Batch preparation](../../../src/Application/Features/Discovery/PrepareDiscoveryBatch.cs).

These are source-verified behaviors, not measured subjective quality conclusions. Runtime configuration, actual evidence coverage, and user outcomes need the baseline phase.

### Historical Phase 4 findings

Current progress documentation says the Phase 4 live pilot was not run, but historical commit `f26d14e` records a completed acquisition: 96 eligible tracks, 48 artists, and 35 blind-review slots. Its A–D comparisons covered aggregate taste similarity, best individual seed similarity, best-seed similarity plus Last.fm similarity evidence, and IDF-adjusted similarity. No winning formula or completed blind evaluation was established in the inspected evidence.

Strong seed alignment, candidate coverage, best-seed matching, and provider differences are hypotheses for the developer's good experiences. Investigate each separately. Do not assume Last.fm was objectively superior or transfer its numerical match semantics into ListenBrainz. Keep informal personal listening notes outside benchmark labels, formula tuning, and taste profiles. Reconcile conflicting status documents in Phase 1 using commit evidence and permitted surviving artifacts.

## Recommended direction and alternatives

| Approach | Assessment |
| --- | --- |
| Retune tag weights | Useful experiment, but cannot fix missing evidence, forgotten feedback, or ineffective controls. |
| Replace the pipeline with audio analysis or a learned recommender | Poor fit for budget, feedback availability, operations, and exclusions. |
| Improve evidence, taste memory, acquisition, and selection within existing boundaries | Recommended: addresses verified weaknesses and supports isolated comparisons and rollback. |

Preserve multiple identifiable interests instead of assuming one average taste describes a listener. A candidate can connect strongly to a favorite recording, album, or established interest without resembling everything the listener enjoys.

Do not immediately adopt maximum seed similarity: it can overvalue one weak coincidence. Compare aggregate affinity, best-seed affinity, and a bounded combination on identical candidate pools before selecting a policy.

| Input | Proposed responsibility |
| --- | --- |
| Discovery goal | Desired kind of discovery: new artists, favorite-connected music, different styles, or mixture. |
| Exploration | Permitted distance in acquisition and selection. |
| Vocals | Soft preference using supported recording-level evidence. |
| All styles welcome | Permission to search broadly, not equal affinity for every genre. |
| Explicit exclusions | Hard restrictions that inferred behavior cannot override. |
| Learned preferences | Reversible tendencies informed by repeated feedback. |

For favorites plus surprise, seek less obvious discoveries with a defensible favorite connection. Do not silently discard either input. Explain when available evidence cannot satisfy their combination. Do not impose a universal exploration ratio or mandatory experimental pick. Avoid extra onboarding controls without demonstrated benefit.

## Provider capabilities and constraints

Official documentation was checked during planning on 2026-11-02. Documentation availability does not establish live coverage, service reliability, or permission for every deployment use.

| Source | Realistic use | Boundary |
| --- | --- | --- |
| ListenBrainz | Existing artist/tag acquisition and recording-similarity discovery. | Association does not prove shared melody, atmosphere, or vocal style. Measure coverage and behavior. |
| MusicBrainz | Canonical identities, album membership, performer/composer relationships, and recording-specific credits. | Shared contributors are discovery connections, not proof of sounding alike. Completeness varies. |
| AcousticBrainz | Optional investigation of existing recording descriptors and predictions. | Collection stopped in 2022. Current coverage, availability, and usefulness are unverified. Label predictions as predictions. |
| Last.fm | Historical comparison and optional existing provider. | Not required. Match values are not directly comparable to ListenBrainz. Existing use/attribution gates remain. |

No sufficiently broad, reliable source of detailed melody similarity or vocal delivery was established. Missing evidence must remain missing. Never substitute artist-level claims for recording-level acoustic facts.

References:

- [ListenBrainz radio endpoints](https://listenbrainz.readthedocs.io/en/latest/users/api/core.html).
- [ListenBrainz rate limits and user-agent requirements](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html).
- [ListenBrainz similar-recordings viewer](https://labs.api.listenbrainz.org/similar-recordings).
- [MusicBrainz API and relationship lookups](https://musicbrainz.org/doc/MusicBrainz_API).
- [MusicBrainz rate limits](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting).
- [MusicBrainz licenses](https://musicbrainz.org/doc/About/Data_License): core data and supplementary tags have different licenses; retain actual source and applicable conditions.
- [AcousticBrainz status and CC0 data](https://acousticbrainz.org/).
- [AcousticBrainz API](https://acousticbrainz.readthedocs.io/api.html).
- [Last.fm API terms](https://www.last.fm/api/tos).

## Architectural implications

- Domain: pure taste combination, feedback interpretation, eligibility, scoring, and deterministic selection.
- Application: saved-taste coordination, bounded acquisition, evidence preparation, and persistence through interfaces.
- Infrastructure: provider requests, normalization, provenance, freshness, caching, and optional enrichment.
- DataAccess: rebuildable taste summaries, feedback revisions, and versioned selection receipts.
- Presentation: existing controls and concise accurate explanations; only necessary interaction or wording changes.

Every phase adding stored information must cover export, deletion, ownership, retention, and storage accounting. Every selection change must update receipts in that same phase. Historical recommendations keep their original explanations. No unrelated cleanup or new dependency is proposed.

## Implementation phases

Phases are sequential and separately approved. Effort estimates are provisional engineering workdays, excluding approval waits and optional listening time. Reassess scope after baseline evidence.

### Phase 1 — Baseline and evaluation contract

Estimated effort: 2–3 days. No recommendation-policy change.

- Freeze code revision, effective non-secret configuration, profile revisions, generation ordinals, provider evidence, and selection outputs.
- Reuse the evaluation harness, benchmark fixtures, replay utilities, and discovery diagnostics.
- Reconcile historical Phase 4 evidence and identify reproducible, permitted comparisons.
- Capture candidate yield, metadata coverage, favorite participation, suppression, selected results, and explanation completeness.
- Establish matched cold/warm measurements for total job latency, provider calls, database work, memory, and receipt size.
- Define preference interaction cases and provisional acceptance criteria for later phases.

Verification: trace controlled beta UI/API/database journeys from saved inputs through generated picks and receipts. Confirm deterministic replay on frozen evidence.

Exit evidence: baseline report separating implementation defects, provider limitations, and untested quality hypotheses.

Planning-time environment observation: Playwright MCP is exposed but failed to initialize because `/opt/google/chrome/chrome` is missing. The local beta returned HTTP 200 at localhost:5080; this does not verify recommendation behavior. Restore working Playwright MCP browser execution before claiming browser verification.

### Phase 2 — Consistently usable favorites

Estimated effort: 3–5 days.

Separate remembering supported taste evidence from choosing acquisition seeds for a particular generation.

- Preserve canonical artist, album, and recording identities and distinct evidence scopes.
- Reuse valid cached evidence without fetching every favorite each generation.
- Introduce fair bounded acquisition scheduling with measurable coverage across batches.
- Keep favorite recordings suppressed from recommendations to their owner.
- Preserve album-specific evidence; label and limit credited-artist fallback.
- Avoid duplicate counting through overlapping favorites.
- Retain individual favorite evidence for aggregate-versus-seed comparisons.

Verification: profiles beyond the current window, mixed favorite types, duplicate credits, sparse metadata, repeated generations, and measured request/memory costs. Rotating acquisition alone must not erase otherwise valid scoring evidence.

Exit evidence: each favorite is represented, awaiting bounded acquisition, or explicitly unavailable with a reason.

### Phase 3 — Durable and reversible feedback learning

Estimated effort: 4–6 days.

Replace recent-row-only learning with durable and recent components.

- Keep explicit favorites independent from learned feedback.
- Maintain compact rebuildable summaries from actual feedback state without unlimited per-generation scans.
- Separate musical-feedback timestamps from note-only edits.
- Make edits and clearing feedback reverse previous contributions correctly.
- Distinguish exact-track dislikes, known-track status, and recommendation-history suppression.
- Apply cautious bounded soft adjustments for repeated negative patterns across independent recordings.
- Include positive counterevidence and later changes so inferred aversion can weaken or disappear.

A single disliked rock track must not become a general dislike of rock. Repeated feedback may support a tendency without identifying which characteristic caused it.

Verification: long-standing rock preferences followed by sustained jazz likes; note edits; more than 20 feedback entries; dislike-to-like changes; clearing; aliases; contradictory evidence; concurrent updates; migration; export/deletion; reconstruction.

Exit evidence: durable interests survive irrelevant updates, recent interests influence results, and changes are reversible. Notes remain uninterpreted.

### Phase 4 — Recording evidence and discovery connections

Estimated effort: 3–5 days; reject unsuitable optional sources early.

- Audit recording-similarity coverage and candidates lost during hydration.
- Preserve usable relationships without manufacturing acoustic properties.
- Evaluate bounded MusicBrainz relationships and recording-specific credits.
- Investigate album membership as a source of recording seeds without treating every album track as an explicit favorite.
- Probe AcousticBrainz coverage and consistency as optional enrichment.
- Retain scope, source, freshness, and uncertainty for each evidence item.

Verification: representative recordings across styles and release periods; absent/conflicting/malformed/incomplete evidence; incremental requests, latency, and yield.

Exit evidence: measured adopt/defer/reject decisions for each proposed evidence source. Optional enrichment failure does not block core renewal.

### Phase 5 — Coordinated exploration, relevance, diversity, and explanations

Estimated effort: 5–7 days. Main selection-policy change.

- Make exploration affect acquisition budgets/routes and final ordering.
- Combine goals and soft preferences without unconditional lexicographic masking.
- Compare aggregate and individual-interest affinity on frozen pools.
- Require a defensible relevance connection; novelty alone cannot justify inclusion.
- Allow shorter batches when supported candidates run out.
- Diversify among relevant candidates using canonical artist identities where available and concentration across batches.
- Avoid universal exploration ratios and mandatory experimental slots.
- Persist bounded decision inputs sufficient to reconstruct ordering, suppression, diversity decisions, and shortfalls.

Receipts distinguish favorite connections, broader exploration, and unavailable vocal information. They identify what actually affected selection.

Verification: valid goal/exploration/vocal combinations, AllStyles, sparse pools, conflicts, repeated generations, provider gaps, separate acquisition/ranking comparisons, and complete selection replay from stored decision inputs.

Exit evidence: observable and explainable control effects when supported; explicit limitations otherwise. Document experimental justification for thresholds and coefficients without claiming optimality.

### Phase 6 — Integrated evaluation and cautious calibration

Estimated effort: 2–4 days, plus optional listening.

- Run the original baseline protocol against the integrated implementation.
- Compare rankers on identical pools; compare acquisition under matched request budgets.
- Exercise complete beta workflows with controlled accounts.
- Verify performance, migrations, historical receipt compatibility, privacy, and rollback.
- Use a small concealed comparison from actual beta results when listening feedback is available.
- Freeze settings before held-out evaluation; do not repeatedly tune against held-out outcomes.

Exit evidence: before/after report distinguishing verified behavior, evidence/acquisition coverage, directional listening findings, and subjective quality still unverified. No volunteer recruitment or separate research platform is required.

## Evaluation strategy

| Question | Evidence |
| --- | --- |
| Acquisition usefulness | Yield by seed/type, recording evidence coverage, duplication, gaps, request cost. |
| Ranking on the same pool | Paired comparisons, supported favorite connections, preference behavior, listening judgments where available. |
| Shorter-batch usefulness | Quality per shown pick and useful discoveries per requested batch; report unfilled slots. |
| Connected exploration | Favorite/evidence paths, requested breadth, outcomes; low tag overlap alone is insufficient. |
| Learning correctness | Durable/recent scenarios, reversals, conflicting evidence. |
| Operational practicality | Matched latency distributions, total provider calls across pools, memory, database work, stored bytes. |

Cover multiple taste profiles and genres, exploration settings, sparse inputs, incomplete responses, metadata gaps, and conflicting preferences. Inspect actual API responses, persistence, feedback, and explanations. Use Playwright MCP for relevant end-to-end verification. Use targeted tests by default and diagnostics appropriate to the actual question. Do not attach profilers to a process without an explicitly provided PID.

Synthetic tests establish correctness and regression resistance, not subjective enjoyment. Hundreds of passing tests do not prove recommendations improved. Sparse real feedback cannot establish a reliable probability of becoming a favorite or universally optimal coefficients. Separate development and held-out profiles; do not tune against held-out data. All real evidence must obey existing acquisition, privacy, storage, and retention requirements.

## Risks and constraints

Incomplete recording metadata, provider sampling bias, stale optional datasets, request fan-out, negative-feedback overgeneralization, and oversized receipts are the principal risks. Stronger relevance requirements may underfill sparse profiles or favor well-documented music; measure and disclose those effects. Keep latency and total request budgets bounded across the entire job, not merely within individual provider clients. Avoid invented facts, padding, silent preference precedence, unrelated cleanup, and unsupported recommendation-quality claims.

## Next approval

Phase 1 is the recommended next step. This document records the proposal; its creation and the documentation-directory move do not authorize implementation. After each separately approved phase, report evidence and limitations, create the focused verified local commit, and stop for instruction.
