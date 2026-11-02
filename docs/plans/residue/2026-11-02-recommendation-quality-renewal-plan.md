# Residue recommendation quality renewal

Date: 2026-11-02  
Status: Proposed; awaiting approval of Phase 1. No implementation phase is authorized.  
Revision: 2026-11-02 — quality baseline, evidence-led scope, complexity gates, and strict listener isolation.

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

## Strict separation of personal taste and global behavior

The algorithm adapts to each listener; the application must never adapt global behavior to the founder. Founder profiles and outcomes are historical case studies, never the target distribution, production training/calibration data, or universal ranking rules. Reusing Phase 4 methodology does not authorize promoting its profiles or outcomes.

Never copy the developer's favorites, artists, recordings, genres, moods, ratings, or other personal preferences into production defaults, shared scoring configuration, fallback candidates, global taste signals, or public-facing explanation templates. New personal listening ratings may describe that listener's results but cannot justify global coefficient tuning. Proposed shared behavior must be evaluated across independent diverse profiles; synthetic correctness is not subjective quality evidence.

### Existing boundaries: source inspection, not full runtime proof

- [ListenerContext](../../../src/Presentation/Discovery/ListenerContext.cs) resolves authenticated identity before guest credentials; an authenticated account's guest cookie does not select another owner.
- [DiscoveryTasteStore](../../../src/DataAccess/Persistence/Repositories/DiscoveryTasteStore.cs) validates signal profile ownership and replaces manual signals within that profile. [DiscoveryBatchStore](../../../src/DataAccess/Persistence/Repositories/DiscoveryBatchStore.cs) scopes taste, feedback, history, and writes by profile and checks recommendation ownership for feedback.
- [PrepareDiscoveryBatch](../../../src/Application/Features/Discovery/PrepareDiscoveryBatch.cs) builds effective inputs for the profile and passes its taste, familiarity, exclusions, and saved answers into deterministic scoring/selection. Pure scoring is an architectural aid, not an authorization check.
- [SharedSeedPreparation](../../../src/Infrastructure/Discovery/SharedSeedPreparation.cs), [SharedDiscoveryPreparation](../../../src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs), and [PersistentDiscoveryPreparation](../../../src/Infrastructure/Discovery/PersistentDiscoveryPreparation.cs) share public seed evidence/pools keyed by entity/request/configuration. Shared contents must remain independent of likes, dislikes, private taste vectors, exclusions, and personalized final ranks. Provider adapters are scoped. Shared public metadata may legitimately be reused by multiple users.
- Existing tests include API ownership rejection, cross-profile public acquisition reuse, coordination/lease ownership, and recording identity persistence across listeners. These inspected tests do not establish complete end-to-end preference isolation and were not rerun for this plan revision.

### Phase 1 isolation investigation and regression matrix

| Boundary | Required investigation and regression evidence |
| --- | --- |
| Identity and authorization | User/guest A cannot read, mutate, replay, or export B's taste, feedback, jobs, batches, or receipts by substituting identifiers. Account switching and guest claim/merge follow explicit ownership semantics. |
| Materialization and scoring | On frozen public evidence and fixed B profile revision/generation ordinal, A's favorite, rating, dislike, known marker, or setting changes cannot change B's effective vector, familiarity, exclusions, or normalized ranking/receipt inputs. Ignore generated IDs/times when comparing. |
| Shared caches | Run B cold, A warm/change, B warm, and reverse/concurrent order with shared seeds. Public cache reuse is allowed; personalized values in public payloads/keys are not. Inspect mutable cached objects, persistent codecs, invalidation, and stale/partial entries. Cache warming can change timing; it must not inject A's preferences. |
| Fallback and missing evidence | Sparse/empty/expired data and provider failure cannot substitute founder candidates, another listener's vector, feedback, batch, or exclusion list. Verify supported failure/abstention behavior without manufacturing missing evidence. |
| Persistence and history | A's completion, feedback edit/clear, suppression, cancellation, or deletion must not change B's private state. Shared track metadata must not merge listener-specific judgments; check aliases and identical display names. |
| Defaults and explanations | Trace production configuration, fallback construction, receipts, and public copy for founder-specific seed leakage. Distinguish legitimate isolated test fixtures from reachable production defaults. No private preferences appear in another user's or public explanations. |
| Privacy and lifecycle | Check export, deletion, guest/account migration, legacy weekly path, and any future summary/receipt owner boundaries. Shared metadata survives only under its public-evidence rules. |

Run controlled A/B/A comparisons and separate beta sessions. Hold provider snapshots fixed to distinguish preference contamination from changing upstream data or cache freshness. Include a third independent profile and varied preferences; this is not an evaluation around the founder alone.

Gaps requiring Phase 1 evidence: complete cold/warm/concurrent end-to-end non-interference coverage has not been established; fallback/legacy routes, object mutation, persistent public payloads, and privacy transitions still need tracing and execution. No cross-user leak is asserted by this document. Report concrete findings before proposing remediation; do not treat the absence of a discovered leak as proof.

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
- DataAccess: profile-owned feedback revisions and versioned selection receipts; rebuildable taste summaries only if the evidence and complexity gate justifies them.
- Presentation: existing controls and concise accurate explanations; only necessary interaction or wording changes.

Every phase adding stored information must cover export, deletion, ownership, retention, and storage accounting. Every selection change must update receipts in that same phase. Historical recommendations keep their original explanations. No unrelated cleanup or new dependency is proposed.

## Implementation phases

The six phases below are a conditional backlog, not a commitment to execute six phases or spend the originally estimated 19–30 engineering days. Only Phase 1 is the next approval candidate. After its results, re-rank, split, combine, defer, or remove later work. Preserve all agreed recommendation behavior while choosing the smallest changes with credible benefits. Estimates below describe the original candidate scope and must be replaced after the baseline decision.

Before persisted taste summaries, providers, integrations, or new abstractions are proposed for implementation, demonstrate an existing-mechanism limitation using reproducible evidence. Compare a simpler change using existing queries, feedback state, caches, and provider capabilities. Require a concrete expected benefit, measured coverage where relevant, acceptable latency/request/storage costs, privacy implications, and rollback. If meaningful benefit cannot be established, defer the added mechanism. Additional infrastructure spend remains $0.

### Phase 1 — Actual recommendation baseline, isolation audit, and priority decision

Proposed scope only; explicit approval is still required. Initial timebox: 2–3 engineering days, reassessed if evidence access or browser setup prevents completion. Report incomplete work rather than silently expanding scope. No production scoring, acquisition policy, learning behavior, schema, provider integration, or defaults change belongs in this phase. Targeted regression tests and minimal evaluation-only capture/replay support may be added after approval where existing tools are insufficient.

1. **Preserve the current beta's actual output before modifying Residue.** Record the running revision and effective non-secret configuration, profile revision, generation ordinal, batch/pick IDs, displayed order, explanations, saved score/selection evidence, and available candidate provenance. Preserve existing recommendations before any controlled new generation. Use bounded, permitted current-provider acquisition through the beta when needed; retain raw responses only where permitted and necessary. Keep personal artifacts local, private, ignored, and covered by existing storage/retention rules. Do not publish tastes, listening notes, or account identifiers.
2. **Establish musical-quality evidence where practical.** Prepare a small concealed listening review of actual current-beta picks: hide score, rank, formula/provider labels, and explanations during rating; randomize presentation and retain a private key. Record enjoyment/relevance, already-known status, and potential favorite status separately; unanswered items are missing. Use matched comparisons only when legitimate comparable outputs exist. Freeze current results for a later baseline-versus-change review; do not fabricate an alternative or alter production merely to create a comparison. If only baseline ratings are feasible now, label them baseline ratings rather than comparative proof. Report the developer's ratings as directional evidence for that listener only. Preserve prior restrictions on informal historical notes; new ratings must have an explicit evaluation purpose.
3. **Investigate Phase 4 fairly.** Reconcile commit evidence and surviving permitted artifacts; distinguish acquisition, formula variants, and listening outcomes. Compare rankers on identical evidence where possible. Separate provider, catalog, seed membership, time, and policy differences. Do not pool unlike providers into a claimed before/after result or treat founder outcomes as global targets.
4. **Trace behavior and costs.** Freeze reproducible inputs and measure candidate yield, metadata scope/coverage, favorite participation, feedback-window effects, exclusions, selection, receipt accuracy, and cold/warm total-job latency, requests, database work, memory, and bytes. Reuse existing harnesses and diagnostics. Exercise independent diverse profiles, sparse metadata, conflicting preferences, and all meaningful exploration/control combinations. Use synthetic profiles for behavior only.
5. **Audit and regression-test listener isolation.** Apply the boundary matrix below across API identity, caches, fallback paths, materialization, scoring, persistence, exports, and explanations. Run targeted tests and controlled beta journeys with separate browser contexts via Playwright MCP. Check guest/account transitions and concurrent requests. Capture exact test outcomes and remaining gaps; do not infer isolation from architecture alone.
6. **Choose the smallest next work.** Deliver a ranked benefit/cost/risk table, evidence for each problem, simpler alternatives, proposed verification, revised effort, and explicit adopt/defer/drop decisions for the backlog. Separate verified behavior improvements from listening-based quality evidence. No later phase starts automatically.

Exit artifacts: a private reproducible beta baseline; concealed review protocol/results or an explicit feasibility limitation; historical comparison with confounders; profile-diverse behavior results; isolation coverage matrix and regression outcomes; matched performance baseline; and a reduced, justified next-phase proposal. No claim of subjective improvement is warranted without listening evidence. Findings needing product fixes require a subsequent approved scope.

Planning-time environment observation: Playwright MCP failed to initialize because `/opt/google/chrome/chrome` was missing. Localhost:5080 returned HTTP 200, which is not recommendation verification. Recheck browser availability in Phase 1 and restore working MCP execution before claiming end-to-end verification.

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

First demonstrate why a targeted correction using existing feedback state cannot provide durable and recent components. Persisted summaries are a conditional option, not a predetermined implementation.

- Keep explicit favorites independent from learned feedback.
- Prefer bounded existing-state queries and incremental reuse where sufficient. Introduce compact rebuildable summaries only after the complexity gate demonstrates their necessity and costs.
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
- Consider an AcousticBrainz probe only if existing evidence has a demonstrated gap and a bounded probe can establish useful coverage and benefits; integration is not presumed.
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

The three requested adjustments and strict founder/multi-user isolation safeguards are included. Previously agreed favorite handling, exploration, durable taste, cautious negatives, short high-quality batches, explainability, privacy, and performance objectives remain requirements.


Phase 1 is the recommended next step. This document records the proposal; its creation and the documentation-directory move do not authorize implementation. After each separately approved phase, report evidence and limitations, create the focused verified local commit, and stop for instruction.
