# Renewal contracts and pending proposals

The 2026-10-06 user-supplied renewal plan takes precedence over older specifications.
Phase 1 execution was authorized with unchanged PRs 1A–1K. On 2026-10-07, the developer requested
Phase 2 execution and explicitly approved its exact membership and offline legacy-test replacement;
see [Phase 2 design](phase-2-design.md) and [evidence](phase-2-evidence.md). The developer subsequently
requested Phase 3 execution; see [design](phase-3-design.md) and [evidence](phase-3-evidence.md).
The developer then requested Phase 4 implementation; its tooling is verified offline, with the
concrete live run still pending. See [Phase 4 evidence and procedure](phase-4-evidence.md).
Phase 6 local work was requested on 2026-10-09, followed by an explicit provisional baseline-A
evidence bypass. Its concrete persistence/API/policy design still requires review; quality remains
unverified and real evaluation remains open. Phases 5/7–9 and live run manifests remain separately
gated. On 2026-10-06, the developer explicitly
approved this PR 1A revised written plan, numeric evaluation thresholds, pilot adequacy bars and
flagged interpretations in response to the written-plan approval question. This is planning approval;
that approval alone did not authorize Phases 2–9, and concrete run manifests retain separate approval.
No recorder execution, live Last.fm calls, real scoring, project scaffolding or code changes form part
of this planning revision. Phase 1 work remains authorized under its existing separate-approval limits.
See [PROGRESS](../../PROGRESS.md) for current decisions and gates.

Approval scope: the evaluation protocol, pilot bars, manifest format and evaluation-only formula
definitions below are approved for planning. Descriptions retained as proposals do not indicate
pending reapproval of those same values. Phase 2 exact profile membership and its offline legacy-test
replacement are now approved as documented above. Production formula adoption, retention, live
acquisition, broader schema/API/test-contract changes and individual run manifests still
require the decisions specified below. No implementation was confirmed by this planning approval.
The three reviewed interpretations are accepted: recorder-only calls apply to evaluation, with
production acquisition unresolved until Phase 6; tags sum to the tag subtotal and named components
sum to the final score; diversity/novelty are formula-selection tie-breakers while the evaluation
formulas retain their novelty component.

## Specification reconciliation

The six directories in PROGRESS replace older five-layer examples: EF/Identity/repositories belong
in DataAccess; provider clients/cache/email in Infrastructure; the host is Presentation, not Web/Api.
.NET 10 is targeted; EF Core 10 remains intended, while checked-in packages are 9.x.
Infrastructure specification §§1, 5.4, 6.2, 7.1, 8.2 still identify integration/import requirements,
but ListenBrainz and both providers' username/history imports are deferred. Spotify APIs are out of V1.
§13 deployment and §15 hardening remain requirements subject to approval; CI is not explicitly specified.
The prior soft-delete design is insufficient for physical account deletion.
User exports must cover taste/account data, not disclose hashes, refresh secrets or protection keys;
the exact versioned export boundary must be reviewed before changing that API.

## Revised phase order

The order and written plan are approved; **not approved** below describes execution status.

Number mapping: old 2 -> new 2-5 (split), old 3 -> new 6, old 3A -> new 7,
old 4 -> new 5, old 5 -> new 8, old 6 -> new 9.

1. **Baseline and security — authorized, unchanged.** PRs 1A–1K retain their existing scopes,
   separate local commit sets, verification requirements and developer-confirmation requirement.
   This revision belongs to PR 1A; it does not authorize scorer, provider or test-contract changes.
2. **Harness mechanics — authorized; implementation verified locally, confirmation pending.** Fixture-only harness with an HTTP handler that rejects
   outbound requests, metric implementations and founder/development/held-out/synthetic profile
   sets. Lock held-out membership before any harness scoring, including synthetic mechanics runs.
   Write the separate recorder but do not run it live. Build the acquisition storage guard using fake
   inventory/response inputs. No Last.fm dependency is needed to complete these mechanics.
3. **Honest ingestion, in memory only — authorized; implementation verified locally, confirmation pending.** First verify the concrete design needs no
   database schema change; if it does, stop and ask. Remove invented tags, listener counts and
   match values. Preserve absent/invalid evidence explicitly, all discovery paths, attribution and
   candidate provenance (`live`, `recorded`, `synthetic`). Unknown seeds produce coverage gaps,
   never substitute fixture artists. Exercise this with offline responses only. No persistence.
4. **Pilot — tooling authorized/verified; live run pending.** Gate: developer approval of a concrete manifest (endpoints, exact
   seeds, request maximum, pacing, estimated bytes, absolute output directory), pilot adequacy bars
   and numeric scoring rules before the run. One small local recording, about 5 MB, covering all
   six founder clusters plus a few additional seeds. First report yield per seed, tag noise,
   missing popularity, coverage gaps and size against the preapproved bars below. Then compare
   A–D directionally and perform the developer's blind review. Nothing is tuned or promoted.
   Every table, chart, listening result and summary is labelled **pilot, directional**. Inadequate
   evidence is reported as such; no automatic extension, rerun or expanded budget.
5. **Real evaluation — not approved.** Gate: Last.fm reply reviewed for applicable restrictions,
   or the developer's explicit decision to proceed without one. A reply alone is not blanket
   permission. Obtain a second approved manifest for full development and held-out acquisition.
   Record first; compare scoring second; the developer chooses the formula last. Development-only
   tuning precedes configuration freeze. Include IDF corpus size, genre balance and sensitivity,
   and a defined, versioned, persisted rule for multiple match values in C. Evaluate held-out once
   using the predeclared batch below. If results motivate a change, retire the set to diagnostic use
   and ask for a fresh set. If the Last.fm gate takes too long, the developer chooses to wait longer
   or proceed to Phase 6 with baseline A without real evidence. The agent never chooses this escape.
   Waiving the reply to run evaluation and bypassing evaluation entirely are distinct decisions.
6. **Recommendation pipeline and persistence — local work authorized; design review pending.**
   Provisional baseline A was explicitly approved under an evidence bypass on 2026-10-09;
   quality remains unverified and evaluation remains open. See [concrete review](phase-6-review.md).
   Application orchestrates signals →
   materialization → discovery → hydration → pure ranking → persistence; provider details stay in
   Infrastructure. Use the formula chosen in Phase 5, or A on explicit evidence bypass. Resolve the
   hash/ordinal/representative-track selection contract, retention and categorical/numeric feedback
   conflicts. Present concrete schema changes and export consequences for approval before building.
   Verify that complete, versioned breakdowns are extensible JSON and that adding a formula needs
   no migration; JSONB mapping alone does not prove this. Include old/new payload round trips,
   unknown-field preservation, formula/configuration versions and replay from stored evidence.
7. **Worker-to-local-email slice — not approved.** Worker → offline generation → persisted digest →
   HTML/plaintext → local sink, including explanations, unsubscribe, short/empty lists, retries and
   recovery. Mandatory reassessment of end-to-end behavior, security, evidence, storage, terms,
   artist tags' suitability for track discovery, cost and complexity. The developer explicitly
   chooses proceed, narrow or revise before later phases; nothing starts automatically.
8. **Privacy and delivery — not approved; two separate approvals.** (a) Complete table/column
   inventory, export and physical deletion verification, including new Phase 6 data, shared catalog,
   backups and access invalidation. Phase 1F deletion fixes remain authorized and are not moved here.
   (b) Scheduling, unsubscribe enforcement at send time, cancellation and delivery recovery;
   verify database atomicity, idempotency and email failure windows separately. A local sink in
   Phase 7 does not authorize external sending. Approval of (a) does not approve (b), or vice versa.
9. **Reproducibility and release preparation — not approved.** Docker/Compose, CI and documented
   Last.fm written public-page approval. Release stays blocked until that approval is documented,
   even if the Phase 5 reply was waived. Hosting/email choices, purchases, provisioning, public
   deployment and external sending retain their separate approvals.

## Evaluation boundaries and profile locks

- Evaluation code depends on Application and Domain; neither depends on evaluation. Prefer the
  existing Application.Tests project for orchestration/metrics and Domain.Tests for pure checks.
  The harness has no live source or credential discovery, and never instantiates the hybrid client.
  In evaluation, the separate recorder is the only tool allowed live calls. Tests always reject
  outbound requests, including when credentials exist. Replay reads only approved local recordings.
- Check V1_FolderStructure.md before adding any project or folder. Its `scripts/` area can host a
  standalone recorder without a new .NET project. A dedicated evaluation project/folder is not in
  the blueprint: present its exact path and references and obtain approval before adding it.
  No project or folder is added by this revision. Output-directory creation also belongs to the
  approved recording manifest, not this planning pass.
- Approved Phase 2 membership: six founder profiles, 16 development profiles (two per each of the eight benchmark
  genres), 16 held-out profiles (two per genre), and the existing eight-genre synthetic mechanics
  fixture plus missing/invalid/tied-input cases. These are reviewed profile definitions, not acquired real data.
  Exact membership, seeds, canonicalization and split hashes must be locked before any harness
  scoring; named profiles were reviewed in Phase 2. Development/held-out profiles contain two
  to five declared artist/tag seeds; preserve sparse profiles as such, never auto-fill them.
  No response acquisition is needed
  to lock membership. Founder/pilot seeds cannot be held-out seeds; development and held-out seed
  identities are disjoint. Report upstream candidate/artist overlap without changing the split.
- Synthetic data establishes mechanics only, never coverage or musical quality. Founder/pilot data
  is diagnostic, not held-out. Do not inspect held-out tag frequencies, corpus composition, rankings
  or outcomes during development. Allow acquisition-integrity checks only (hashes, parse success,
  request/byte limits); keep held-out content and statistical reports sealed until freeze.
- Freeze hashes for code, profile membership, recordings, normalization/stoplists, A–D definitions,
  weights, candidate eligibility, tie policy, IDF corpus/factors, match aggregation, metrics,
  thresholds and blind-review protocol. Persist this local record before held-out evaluation.
  The once-only held-out batch includes all predeclared formulas, cutoffs and perturbations;
  those checks are one frozen experiment, not opportunities to tune and rerun. Any result-driven
  revision retires the set and requires a newly approved confirmation set.
- Identical eligible pools and common ordering rules for A–D, at k=3 and k=5. Never pad short lists.
  Known/disliked tracks are excluded; siblings remain eligible. Proposed evaluation-only tie rule:
  canonical track key ordinal. Freeze it for comparisons; it does not settle Phase 6 production
  selection. Preserve upstream rank separately. Popularity must never enter A–D ranking weights.
- Recordings, run manifests, locks, score outputs, ratings and generated reports stay local,
  gitignored and untracked. Proposed containment: an approved absolute path under `recordings/`,
  whose ignore rule already exists. Fail if a target is tracked, resolves outside that root or
  would overwrite an earlier run. Plans and the manifest template here are not generated reports.

## Numeric evaluation contract — approved for planning on 2026-10-06

The developer approved these evaluation thresholds before any real scoring. They are not
measurements, scientific guarantees or adoption of a production scoring policy. Changes require review.
Metric denominators include short/empty lists and missingness as specified; show raw counts as well
as percentages. Evaluate guardrails on all 16 held-out profiles, and report each genre separately.

| Check | Proposed definition and acceptance bar |
|---|---|
| Finite scores | 100% of emitted scores, components and contributions finite; zero NaN/infinity. Invalid input must be rejected or marked missing with a reason, never coerced into evidence. |
| Authentic evidence | Zero fabricated tags, counts, matches or source labels. Every real datum traces to a response hash/path; 100% of candidates carry provenance. Zero live/synthetic blending. |
| Determinism | 100 repetitions and 100 seeded permutations of candidates, seeds, tags and discovery paths, under invariant, en-US, tr-TR and az-Latn-AZ cultures. Exact ranked keys and canonical numeric breakdowns must match on the pinned runtime. No clock/process hash dependence. |
| Explanation arithmetic | Store every weighted normalized tag contribution, not only the top five. Their sum equals the tag score within `1e-10 * max(1, abs(score))`; the tag subtotal plus separately named novelty/match components equals the final score at the same tolerance. Never assign non-tag evidence to invented tags. |
| Network isolation | Zero outbound attempts in ordinary tests and the harness; a rejecting handler makes any attempt fail. Recorder tests use fakes. The legacy live spike cannot remain an ordinary test entry point. |
| List coverage | Fraction of all profiles reaching at least 3 eligible picks. Challenger may lose at most 5 percentage points versus A. With 16 held-out profiles this permits zero additional failing profiles. Also report full 5-pick coverage and per-seed yield. |
| Artist concentration | At each k, macro-average the largest single-artist share per profile (`largest artist count / actual list length`; empty list conservatively 1). Challenger may exceed A by at most 0.10. Across all pick slots, the most represented artist's share may exceed A by at most 5 percentage points. |
| Weight stability | Change each positive configurable coefficient one at a time by ±5% and ±10%; leave zero coefficients zero, then also test joint extreme combinations. Use mean top-k overlap `intersection / max(original length, perturbed length)`; both-empty = 0. At k=3 and 5: at least 0.80 for ±5%, 0.70 for ±10%, and no more than 0.05 below A under the same perturbation protocol. Worst perturbation must pass. |
| Popularity-collapse revisit | Diagnostic only. Using a predeclared comparable listener measure, flag if over 60% of selected known-popularity picks come from the highest candidate-pool popularity decile AND this exceeds A by over 15 percentage points; also flag a rise of 0.20 in median within-pool popularity percentile versus A. Either flag requires developer review before promotion; never auto-add a penalty. |
| Popularity missingness | At least 80% of pooled eligible candidates and 80% of selected picks must have that same authentic measure to assess the collapse check. Otherwise mark the guardrail unassessable and the replacement decision inconclusive pending explicit review. Never replace missing counts with zero or use top-track listeners as artist-wide popularity. |

Correctness failures block the affected comparison entirely. Guardrails cannot be traded away for a
better average score. Stability measures ranking robustness; it is not a preference signal. Retain
the per-profile results so a macro-average cannot hide a coverage failure.

### Blind preference signal and decision rule

For each of the six founder profiles, pool the top five from every formula, deduplicate by canonical
track identity, shuffle reproducibly with a concealed seed, and hide formula, rank, score and
explanation labels. The developer rates each track like / neutral / dislike. The same rating serves
all formulas containing that track within that profile. Maximum workload: 20 tracks per profile,
120 across six before deduplication. Smaller option: three profiles chosen before results are seen,
at most 60 tracks; retain the same margins but label the narrower preference evidence explicitly.
Pilot and final reviews are separate. Before the Phase 5 freeze, identify already auditioned pilot
tracks and exclude them from the final blind pool for all formulas, documenting lost coverage.

Proposed numeric encoding: like=1, neutral=0, dislike=-1. For each formula/profile compute the sum
of its top-five ratings divided by 5; unfilled recommendation slots contribute 0 and remain visible
in coverage. Every available pooled track needs a rating; an unrated track is not neutral and makes
the comparison incomplete. Macro-average over profiles. Set the winning margin in advance to
**at least +0.20 versus A**, with a positive difference on at least **4 of 6 profiles**, or **2 of 3**
for the smaller option. Report paired differences and counts; this is the founder's preference
evidence, not a population-level efficacy claim. No additional listening rounds to chase a margin.

A challenger is eligible to replace A only if it passes correctness, all held-out guardrails and
the blind-rating margin. Otherwise A remains; **inconclusive** is a valid result. An unassessable
guardrail cannot be called a pass. If multiple challengers qualify, prefer the higher blind mean;
within 0.05, use higher distinct-artist fraction and then observed novelty fraction only as
tie-breakers, leaving the final choice to the developer. Neither diversity nor novelty can make
a failing challenger eligible. Novelty here is relative to declared familiarity, not a claim that
the listener has never heard a track. A itself failing correctness stops scoring; retaining A is
not permission to ship an incorrect implementation.

### Pilot adequacy bars — approved; concrete recording manifest still required

Proposed scope: the existing eight named founder seeds across six clusters, plus three additional
non-held-out seeds chosen in the concrete manifest. Founder clusters: Jakuzi; Son Feci Bisiklet;
Hotline Miami (M.O.O.N., Perturbator, Jasper Byrne); Dying Light (Paweł Błaszczak); ULTRAKILL
(Heaven Pierce Her); Hades (Darren Korb). These are planning inputs from the legacy spike, not
verified coverage. Proposed extra seeds: Agalloch, Pharoah Sanders and Altın Gün, subject to approval.

- Attempt all 11 seeds and all six clusters within the fixed budget. At least 8/11 seeds should
  yield five distinct eligible tracks; each founder cluster should yield at least ten tracks
  across three artists. Overall aim: at least 60 distinct tracks and 20 distinct artists.
  Report raw, deduplicated and eligible yield separately; every absent seed/cluster is a gap.
- At least 80% of distinct candidate artists should have two usable supplied positive-weight tags.
  Raw tag noise should be at most 30% of observed tag entries using a predeclared noise rubric;
  list raw/filtered counts, denominator and ambiguous tags. Do not edit the stoplist during pilot.
  Proposed rubric: the current hydrator's stoplist, frozen by hash after review, using trimmed
  invariant case normalization. The noise denominator is all nonblank artist/tag entries before
  filtering, counting each normalized tag once per artist. Ambiguous tags are reported separately,
  not retrospectively classified to improve the bar.
- Report listener-field availability at its actual track/artist scope, including denominator and
  endpoint, and every absent/invalid value. At most 20% missing is the proposed bar for a usable
  popularity diagnostic; failure limits that diagnostic, not permission to invent values or add
  endpoints. C needs valid comparable match evidence on at least 80% of artist-similarity paths
  for a useful directional comparison; report tag-source paths separately without assigning matches.
- 100% of retained responses must parse or have an explicit error/gap record and provenance.
  At least 95% of attempted requests should return usable responses. Any rate-limit response stops
  recording immediately. Authentication/permission errors also stop; retries are off for the pilot.
- Roughly 5 MB means a budget, not a minimum to fill: estimate 4,000,000 response bytes and cap at
  5,000,000 uncompressed bytes; cap the complete run directory at 6,000,000 bytes including reports,
  manifests and derived files. Lower size is fine if yield bars pass. Record actual bytes by category.
- If yield, tag or integrity bars fail, issue the shape report and stop before formula/listening
  comparisons pending developer review. Missing popularity/match bars mark the affected comparisons
  unassessable. Pilot D uses only the pilot's distinct artists for its explicitly exploratory small
  corpus, with no held-out statistics. No pilot result adopts weights, corpus,
  stoplists, aggregation, formula or production tie policy. All results: **pilot, directional**.

### Phase 5 IDF corpus and C evidence

Propose a development-only IDF target of 800 distinct artists, minimum 400, with eight preassigned
genre strata (target 100, minimum 50 each). Assign each artist once for balance accounting using a
frozen rubric; report multi-genre ambiguity, unknown labels and duplicate identities separately.
No known stratum should exceed 25%; unknown labels should be at most 20% of the inventory. Unknown
artists do not fill named-stratum quotas. Failure makes D insufficiently supported, not grounds to
manufacture labels or expand acquisition without approval. These are practical sampling bars, not
a claim of catalog representativeness. No extra endpoint is implied by these targets.

Report corpus artist/tag counts, raw and balanced stratum counts, missing tags/labels, corpus hash,
candidate overlap and evidence provenance. Count each distinct artist once for document frequencies.
Fit IDF exclusively from development; never learn frequencies from held-out, even without labels.
On development only, compare 400 versus 800 artists if available, ten fixed balanced subsamples,
and eight leave-one-genre-out variants. Report score deltas, top-3/top-5 overlap, list coverage and
concentration. Proposed D robustness bar: mean top-five overlap at least 0.80 for subsamples and
0.70 for leave-one-genre-out; all coverage/concentration guardrails must hold. If only 400 artists
are available, label the larger-size comparison unavailable and seek approval before admitting D
to the frozen confirmation comparison. Freeze one corpus/factor set before held-out evaluation.

C's proposed `max-valid-match-v1` rule is specified below. Persist the rule version, all raw path
observations, validity/missing reasons, aggregated value and winning path in local evaluation
artifacts in Phase 5. Database persistence waits for Phase 6 schema approval. This avoids making
Phase 5 depend on the recommendation persistence pipeline.

## Draft run-manifest format — not executable authorization

The following is a template, not an approved run. Exact values and hashes must replace placeholders;
the recorder must fail closed while any required field or approval is missing. No credentials or
credential-bearing URLs belong in the manifest. Endpoint names below are existing-client proposals;
current endpoint parameters, terms and response fields require fresh verification before recorder
implementation/use. This planning pass made no live calls and did not reverify Last.fm documentation.

```yaml
manifest_version: 1
run_id: <unique-id>
phase: 4                         # 5 requires a separate manifest and approval
status: draft
label: "pilot, directional"      # mandatory for all Phase 4 outputs
approval: {developer_reference: null, approved_manifest_sha256: null}
lastfm_gate: {reply_reference: null, explicit_waiver_reference: null} # required in Phase 5
verification: {terms_date: null, endpoint_contract_reference: null}
profile_lock_sha256: <hash>
protocol_sha256: <approved-bars-formulas-metrics-and-listening-protocol>
code_revision: <commit>
provider: Last.fm
credential_source: LASTFM_API_KEY # environment variable name only
seeds:                          # all 11 literal entries required in concrete pilot manifest
  - {set: founder, cluster: Jakuzi, kind: artist, value: Jakuzi}
  # Remaining seven founder and three additional seeds, with set/cluster membership.
endpoints:
  - {method: artist.getSimilar, per_seed_limit: 5, max_calls: 11}
  - {method: artist.getTopTracks, per_artist_limit: 5, max_calls: 66}
  - {method: artist.getTopTags, per_artist_limit: 15, max_calls: 66}
expansion: {depth: 1, max_distinct_artists_including_seeds: 66, order: canonical_ordinal}
request_budget: {max_attempts: 160, max_concurrency: 1, automatic_retries: 0}
pacing: {minimum_start_interval_ms: 1000, burst: false, respect_stricter_server_limits: true}
stop_on: [rate_limit, auth_error, permission_error, byte_limit, unknown_inventory, cancellation]
response_budget: {estimated_uncompressed_bytes: 4000000, hard_cap_bytes: 5000000, max_single_response_bytes: 100000}
storage_budget: {run_directory_max_bytes: 6000000, shared_stop_bytes: 80000000}
storage_inventory: {path: <absolute-local-path>, sha256: <hash>, measured_at_utc: <timestamp>}
output_directory: <approved-absolute-repo-path>/recordings/<run-id>
outputs: [responses, provenance, gaps, request_ledger, shape_report, evaluation, blind_ratings]
output_rules: {require_gitignored: true, reject_tracked: true, reject_overwrite: true, reject_path_escape: true}
retention: {expires_at_utc: <explicit-approved-date>, extension_requires_approval: true}
```

The pilot expansion bounds 11 seed similarity calls plus tags/top tracks for at most 66 artists:
143 planned calls before cache reuse; the 160-attempt ceiling is not an invitation to expand.
One request/second is proposed conservative pacing, **not a verified allowance**. A 100,000-byte
per-response bound is a storage safety proposal, not a measured response size. Abort and discard
an oversized partial response with a gap record; never silently parse truncation. Reserve space
for the bounded response and local metadata before every call. Cached reuse carries original
timestamps/provenance and still counts toward storage inventory. No bursts or automatic resumption.

Phase 5 uses the same format with explicit development/held-out partitions, sealed held-out output,
development IDF acquisition plan, founder blind-review candidate inputs, complete literal seed lists,
endpoint verification and limits
estimated from the pilot's bytes/yield. Approve exact numbers in that second manifest before any
full acquisition; no open-ended recording or automatic 5 MB budget multiplication. Partial runs
are incomplete evidence. Any retry/resume or changed manifest requires fresh approval.

## Track-selection proposals: adoption requires approval

Weekly tie-break: SHA256(Encode(version, userId, ISOYear, ISOWeek, canonicalTrackKey)), using
versioned length-prefixed UTF-8, canonical GUID format and supplied ISO week. Sort hash bytes
ascending, canonical key only on collision. Persist policy version and digest week for replay.
No clock, random values or GetHashCode. It avoids permanent alphabetical preference, not proof of fit.

Alternative: highest upstream-ranked eligible track per artist, skipping known/disliked tracks.
[artist.getTopTracks](https://www.last.fm/api/show/artist.getTopTracks) documents popularity order
and rank (checked 2026-10-06); other providers are not verified. Preserve order/provenance.
This adds within-artist popularity preference and cannot silently replace popularity-neutral policy.

## Scoring proposals: adoption requires approval

A: ordinary cosine + configurable novelty. B: best-seed cosine + novelty, preserving winning seed.
C: B + authentic supplied match evidence. D: separately evaluate IDF cosine before combinations.
Proposed initial coefficients for the untuned pilot: tag similarity 1.0 and novelty 0.2 for all four;
C adds match weight 0.1. Popularity and feedback penalty coefficients are zero; known/disliked
track exclusion is eligibility, not a score penalty. All coefficients live in versioned configuration.
A uses the aggregate taste vector; B/C use the highest cosine over the profile's supplied seed
vectors (ties by canonical seed); D uses IDF on A's aggregate vector, not a B/C combination.
An empty/missing tag vector has no overlap evidence and gives a zero tag subtotal with a reason;
it must never be filled with a generic tag. These initial evaluation weights were approved with
the written plan; subsequent tuning is development-only in Phase 5, never on pilot or held-out outcomes.
The approved interpretation of diversity/novelty as tie-breakers applies to **formula selection**;
novelty remains a component of the A–D evaluation definitions. Production adoption remains separate.

For C, deduplicate by provider/seed/method; M(c) = max valid finite supplied match over paths.
Missing stays missing and gives no bonus. Persist `max-valid-match-v1`, evidence and winning path;
ties by canonical seed. Do not pool incomparable provider scores; blending remains deferred.
Validity includes the documented source range; invalid/out-of-range values remain invalid, never
clamped into evidence. Preserve repeated observations and their response hashes. Where the same
provider/seed/method repeats, use the maximum valid value as the path value, then maximum across
paths; equal values choose canonical seed, method and response hash ordinal. Record missing as
null in the evidence, although its numerical bonus is zero. Pilot C only exercises this proposed
rule directionally; Phase 5 freezes and persists the rule for the real evaluation.
For D, each distinct artist counts once; d(t) = 1 + log((A+1)/(df(t)+1)); unseen vocabulary uses 1.
Here A denotes corpus artist count, not formula A. Apply sqrt(d) to both vectors. Per-tag
contribution is `tagWeight*d(t)*c(t)*u(t)/(sqrt(sum d*c²)*sqrt(sum d*u²))`.
For ordinary cosine use d=1; B/C explain the winning seed vector. Zero denominators yield zero
tag subtotal, not division by zero. Store all normalized contributions, factors and non-tag
components so explanations reconstruct the entire score rather than raw dot-product fragments.
Report artist/tag counts, genre balance, missing labels and overlap. Development-only balanced
subsampling and leave-one-genre-out sensitivity must report metric/ranking changes.
Prefer aggregate A/df counts, version/hash and composition summaries over duplicated corpus;
aggregates still count as derived data. Retain actual factors in per-pick explanations.

## Storage and retention proposals

Shared inventory covers recordings, HTTP cache, derived database data, reports/copies/backups.
Count uncompressed bytes conservatively, category totals, headroom and inventory age.
Acquisition fails closed at projected 80,000,000 bytes or unknown accounting; bound responses.
At threshold, stop acquisition/enrichment and alert. Continue account, feedback, unsubscribe,
delete and delivery-state writes. Pause new derived recommendation snapshots if headroom is
insufficient/unknown; serve existing snapshots. Evict only under approved expiry policy;
never silently delete recommendation history. Count derived feedback without duplicating metadata.
Initially only Worker/recording execution acquires data, never concurrently; reconcile inventory
and conservative headroom before each batch. Reassess cross-process coordination at Phase 7.
Measure complete breakdown and provenance UTF-8 and PostgreSQL pg_column_size: median, p95, max,
total. Current sizes are not verified. Estimate at up to 260 picks/user-year plus all categories.
Retention proposal: response headers plus 24h cache maximum, 30d recordings (explicit extensions),
12mo recommendations/explanations, active seeds/feedback/familiarity until changed/deleted,
30d maximum backups included in deletion disclosures. None adopted. Adjust enrollment/retention
to measured capacity; expired recordings require another approved run for replay.

Phase 2's guard must work offline with fake inventories: aggregate all categories, reserve the
maximum next response plus derived-output overhead, enforce single-writer acquisition, reject
unknown/stale accounting and stop before projected usage reaches 80,000,000 bytes. Proposed
inventory freshness: reconcile at run start and after every write; an external change invalidates
the inventory and stops acquisition. Reports/copies/partial downloads count. The per-run cap is
additional to the shared cap, not a replacement. Guard tests cover exact-boundary, overflow,
oversized responses, cancellation, unknown inventory and concurrent acquisition attempts.
For Phase 4/5, retention is a required run-manifest decision; the general 30-day suggestion is
not silently adopted. Plans may name paths, but no recording/report output is tracked.

## Verified conflicts and approval points (read-only inspection, 2026-10-06)

The table preserves the original audit. The ingestion and hybrid/unknown-fixture conflicts were
addressed locally in [Phase 3](phase-3-evidence.md); the unsafe legacy spike was replaced with
an approved offline check in [Phase 2](phase-2-evidence.md). Their baseline descriptions below
are historical, not descriptions of the current implementations. Endpoint/terms documentation
was refreshed on 2026-10-07 for Phase 3; unresolved production decisions retain their gates.

| Finding | Consequence for this plan |
|---|---|
| [Hydrator](../../src/Infrastructure/RecommendationSources/LastFm/LastFmCandidateHydrator.cs) inserts `indie=0.5`, defaults listeners to 10,000 and treats a top track's count as artist popularity. [Source](../../src/Infrastructure/RecommendationSources/LastFm/LastFmRecommendationSource.cs) invents matches 0.5/0.70 and discards later duplicate paths. | Phase 3 must remove these behaviors in memory and retain authentic scope and all paths; none is valid real evaluation evidence. |
| [RawCandidateTrack](../../src/Application/Common/Models/Recommendation/RawCandidateTrack.cs) and [CandidateTrack](../../src/Domain/Scoring/CandidateTrack.cs) have non-nullable numeric evidence and no complete provenance model. Neither is mapped by DataAccess. | A no-schema route is feasible using reviewed transient contract changes. Source inspection confirms these are not mapped entities; the concrete implementation's no-schema property still requires a Phase 3 preflight. Do not touch mapped Track/Artist fields, migrations or public APIs under this phase. Stop and ask if persistence becomes necessary. |
| [Hybrid client](../../src/Infrastructure/RecommendationSources/LastFm/LastFmApiClient.cs) falls back to synthetic data; [fixtures](../../src/Infrastructure/RecommendationSources/LastFm/Fixtures/LastFmFixtureProvider.cs) substitute data for unknown seeds. | Evaluation cannot reuse hybrid selection. Explicit provenance and coverage gaps require future changes; fixture-only mechanics must remain independent of credentials. |
| [Legacy spike](../../tests/Infrastructure.Tests/Spikes/FounderSeedCoverageSpikeTests.cs) reads credentials, can call live endpoints and writes a tracked documentation path. | Conflicts with zero-network tests and untracked reports. Replacement with a separate recorder and fixture-only test requires explicit test-contract approval; no deletion/weakening is authorized by PR 1A. Until then, exclude it from safe verification and do not claim the entire suite is network-isolated. |
| [Scorer](../../src/Domain/Scoring/RecommendationScorer.cs) applies popularity/feedback penalties; [parameters](../../src/Domain/Scoring/ScoringParameters.cs) default popularity to 0.3; contributions are only five raw products. | Existing behavior is not baseline A or a complete explanation. Evaluation needs approved pure A–D implementations plus all normalized contributions; no Phase 1 scorer rewrite is implied. Scorer changes must run the benchmark seed set without deleting policy-conflicting tests before review. |
| [JSONB mapping](../../src/DataAccess/Persistence/Configurations/WeeklyRecommendationConfiguration.cs), initial migration and current snapshot store ScoreBreakdown as JSONB. [ScoreBreakdown](../../src/Domain/Scoring/ScoreBreakdown.cs) is a fixed typed record with no formula version or unknown-field extension storage. | JSONB storage is verified in source only. Extensibility, preservation of unknown fields, runtime round trips and migration-free formula evolution are **not verified**. Resolve and test in Phase 6; do not claim JSONB alone fulfills the contract. |
| Existing ranking uses ordinal keys; the source selects only two upstream top tracks per artist. | An evaluation-only ordinal tie policy and common acquisition pool must be frozen before scoring. Upstream candidate-pool popularity bias is reported, not mistaken for a popularity weight. Production hash/ordinal/representative selection remains a Phase 6 decision; a materially different policy requires reassessing applicability of evaluation evidence. |
| V1_FolderStructure.md includes `scripts/` and a test suite, but no evaluation project; older tree examples combine DataAccess and Infrastructure. | Six-layer reconciliation still applies. Prefer existing projects; any new folder/project deviation needs approval before creation. Documentation edits here add neither. |
| V1_Infrastructure.md §§6–7 assigns live acquisition to the Worker/provider pipeline; the requested recorder-only live-call rule is broader if read literally. | This plan enforces recorder-only calls for evaluation. Production live acquisition is an unresolved scope question: before Phase 6, approve either a recorder-fed offline pipeline or an explicit production acquisition exception. Do not silently give the Worker live access. |
| Requested per-tag sums cannot equal a total that also contains independent novelty/match bonuses without misattributing those bonuses. | Approved interpretation: all tags sum to the tag score, and named components sum to the final score. |
| Prior A–D proposals include novelty as a weighted component; the new decision rule says diversity/novelty are tie-breakers only. | Approved interpretation: the tie-breaker restriction applies to selecting a formula after blind ratings, not the score formula itself. |
| Phase 1F already covers deletion fixes and Phase 1G feedback validation/replacement; Phase 6 introduces a feedback-conflict decision and Phase 8a verifies deletion/export. | Phase 1 scopes remain intact. Broader schema/API/export/test-contract changes retain separate review; unresolved categorical/numeric precedence must be presented before any Phase 1G change that depends on it, not guessed until Phase 6. Phase 8a re-verifies all later data. |
| Phase 1 approval excludes unapproved dependency/schema/API/export/test-contract changes; test isolation and evaluation require some such changes later. | This revision authorizes none. Present concrete diffs/contracts in their phases. Phase 9 dependency/CI work also remains gated. |
| Old specs/memory contain numeric API-rate and compliance claims; current Last.fm response-field availability and endpoint contracts have not been refreshed in this pass. | Verify authoritative documentation before coding against it or approving a real run. Endpoint names/pacing in the draft are proposals. No unsupported rate-limit, coverage or legal-compliance claims are adopted. |

No new README similarity-ownership claim was found in this pass: it describes deterministic
re-ranking of upstream candidates and qualifies unverified delivery. Historical changelog claims
remain explicitly qualified. This review does not certify the rest of the implementation.

## Attribution and enquiry: draft only

[Last.fm terms](https://www.last.fm/api/tos) checked 2026-10-06: storage cap/caching (§4.3.4),
rate limits without a universal numeric quota (§4.4), attribution/public-page approval (§2.7).
UI needs artist/track links and prescribed button; local button hosting permission is unresolved.
Text-only email attribution acceptability, Azerbaijan/EEA conditions, redistribution and benchmark
research permission remain unresolved. No automatic email third-party requests, including hosted images.

Draft subject: Liner Notes — API use, benchmark publication, attribution, and page approval

Hello Last.fm team,

I am developing Liner Notes, an open-source discovery project, from Azerbaijan. It uses Last.fm
candidate/tag data with transparent deterministic re-ranking and does not own the similarity data.
Please clarify territorial consent/hosting/transfers under §§2.3/2.5; whether recorded responses
could ever be redistributed (current policy: local/uncommitted); written public-page approval and
local attribution-button hosting under §2.7; text-only attribution and ordinary artist/track links
in HTML/plaintext emails without automatic third-party requests; and whether publishing benchmark
methodology/aggregate results requires research permission without publishing responses.
We plan shared inventory, acquisition stopping at 80 MB, retention and degradation controls below
the stated 100 MB cap. Repository: **URL must be confirmed by the developer before sending**.
Thank you.
