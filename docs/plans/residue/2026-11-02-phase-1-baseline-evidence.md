# Residue Phase 1: baseline and isolation evidence

Date: 2026-11-02
Status: Technical evidence collected and locally verified; private listening ratings and developer acceptance pending. No recommendation policy change or subsequent phase implemented.

## Scope and provenance

The developer approved Phase 1 of the [revised plan](2026-11-02-recommendation-quality-renewal-plan.md). Changes are evaluation utilities, targeted regression/characterization tests, and documentation. No scorer, selection rule, provider integration, production default, schema, or learning behavior changed. No packages were added or updated.

The initial process on localhost:5080 was a clean worktree at `3cf4a204b9c1728ddc4125a29cf25823cfb0f3b9`, using a separate database with three profiles and no recommendations. It was not the database containing the developer's account. A separately labeled snapshot preserves that distinction. The initially captured main database must not be mistaken for output served by that process.

Before starting another host, a read-only repeatable-read capture preserved the main checkout database: 13 profiles, 37 taste signals, 13 batches, and 65 recommendations. This checkout was at `d776115` when investigation began. The main application's existing live configuration was then served on localhost:5086, overriding only the loopback URL/origin and loading its existing settings into the launched process. The other process was left running. The temporary :5086 host was shut down gracefully after verification. Capture manifests record source/configuration and binary provenance; a clean checkout and on-disk binary hash are not proof of the exact historical binary that generated each old receipt.

Starting the main host activated its existing immediate/periodic maintenance. Two old guest profiles and their ten recommendations disappeared between snapshots, consistent with the expiry-only deletion path in `DiscoveryInventoryService` and `GuestProfileStore.DeleteExpiredAsync`. Those profiles were created on 2026-10-26, more than seven days earlier. The first capture omitted expiry/last-activity fields, so direct pre-start expiry timestamps are unavailable; future captures include them. This is a lifecycle confounder, not an isolation-test deletion assertion. No expired data was restored. The original receipts remain in the private baseline.

All seven registered profiles, their 21 signals, eight batches, and 40 recommendations were compared before/after and were unchanged. The remaining database after maintenance held 11 profiles, 35 signals, 11 batches, and 55 recommendations. Disposable probe guests were cleared through the actual guest API.

## Private artifacts and listening baseline

Artifacts live under the ignored, owner-only `diagnostics/results/residue-phase1-20261102/` directory. Snapshot/review files are mode 0600 and capture directories are 0700. They are not committed. No account email, credentials, tokens, personal favorite identities, ratings, or recommendation titles appear in this report. Capture omits authentication data and feedback-note text. Existing note presence is recorded without interpretation.

- `main-beta-before/`: original main-database snapshot, SHA-256-bound manifest, numerical replay, and reconstructed explanations.
- `running-beta-before/`: separately labeled snapshot of the other checkout's empty recommendation database.
- `main-beta-after/`: post-browser comparison, including the maintenance difference above.
- `founder-review/listen.md`, `ratings.json`, `.blind-key.json`: private listening review for the explicitly identified personal account.
- `main-beta-browser-isolation.json`: successful MCP isolation checks against localhost:5086.
- `main-beta-live-probe.json`: first pair of sequential live requests; API results verified, title selector initially matched nothing and therefore did not verify pick rendering.
- `main-beta-live-ui-probe.json`: fresh verification after correcting the selector; real UI/API/export agreement for both five-pick batches.
- `controlled-api-snapshot.json` and `controlled-api-replay.json`: exported receipts from that latter pair and their numerical replay.

The personal review contains 35 distinct existing recommendations, randomized with opaque item IDs. For a small initial review, rate items 001–010; the remaining items are optional. Artist/title remain visible so listening is possible, but rank, score, provider/formula label, and explanations are concealed. Prior familiarity can still weaken blinding. No alternative algorithm was invented just to manufacture a comparison.

Record enjoyment/relevance from 1–5, already-known status, and potential-favorite status separately. Unanswered fields remain null. At this report's preparation, all 35 enjoyment ratings were unanswered. Existing seven Likes and two Dislikes in the full database are product feedback, not a newly blinded study or representative population labels. Informal historical listening notes were not imported.

The founder's new ratings may provide directional evidence for that listener and a later paired comparison against preserved baseline outputs. They cannot train/calibrate global weights, defaults, fallback candidates, global taste, or universal ranking rules. No subjective improvement claim is made. These artifacts are a local approved evaluation capture, not a new indefinite product-retention policy; keep them private and dispose of them after this comparison under the developer's retention decision. Do not regenerate or overwrite a review once ratings begin.

## Observed recommendation baseline

| Observation | Result and limit |
| --- | --- |
| Actual persisted receipts | 65 across 13 batches before maintenance; all candidate-tag response provenance is Live ListenBrainz. |
| Candidate coverage | 6–211 hydrated candidates per historical batch; sum 1,360 across batches, not distinct recordings across the catalog. Six candidates suppressed in those coverage summaries. |
| Selected evidence scope | 46 artist-scoped, 19 recording-scoped. This measures selected-pick scope, not full catalog coverage or acoustic ground truth. |
| Numerical replay | All 65 stored scores and raw similarities replayed with zero error. This does not reconstruct the complete competing pool or final selection. |
| Selected overlap/score | Raw tag overlap 0.3162–1; score 0.4622–1.2. No zero-overlap pick in this sample; this does not establish a quality threshold or guarantee against weak selections elsewhere. |
| Artist exposure | 45 distinct stored display names across 65 picks; ten picks marked familiar by the stored scorer. This is not a comprehensive measure of what listeners know. |
| Feedback | Nine recommendations with feedback in the original snapshot; too little data for population-wide quality or optimal-weight conclusions. |
| Personal review | 35 current-beta items preserved privately; initial ten-item listening review pending. |

## Running application and performance

Playwright MCP's default configuration could not find `/opt/google/chrome/chrome`, and this user could not create that system path. An installed Playwright MCP 0.0.84 server was launched through its stdio MCP client with `--headless --isolated --executable-path /usr/bin/chromium-browser`. This was actual Playwright MCP execution, not a direct-browser substitute. No package installation, global MCP configuration change, or system Chrome modification was made.

Three independent guest contexts on the main host saved jazz, rock, and classical preferences. Seventeen browser assertions passed: independent starting identities, own-profile saves, private exports, ignoring an untrusted profile-ID field in A's onboarding body, B/C isolation from A's changed tag/settings, B's persisted taste UI, private overview, and unknown-job rejection. All contexts were cleared in `finally`. Browser-only checks do not prove unseen ranking behavior; the frozen-evidence pipeline and database tests below supply separate evidence.

Two bounded live probes, two submissions each, used one generic jazz tag with identical saved settings and separate disposable guest profiles. No founder preferences were used. No failed acquisition was automatically retried by the probe, though normal application retry semantics were not altered.

| Probe | First request | Second request | Interpretation |
| --- | --- | --- | --- |
| Initial API probe | 13,604 ms; eight candidates; five picks | 10,169 ms; eight candidates; five picks | Submission through completed-result observation, including one-second polling. Initial UI selector was inadequate. |
| Corrected UI/API/export probe | 11,292 ms; eight candidates; five picks | 10,311 ms; nine candidates; five picks | All five titles per batch matched rendered UI and returned API; own export retained the receipts. |

Both pairs had zero selected-recording overlap between their requests. Upstream evidence/sampling was not frozen, and a globally cold cache or a warm cache hit was not established. Therefore these are successful sequential requests, not a before/after performance experiment, proof of cache failure, or ranking-isolation failure. The latter pair reported missing recording metadata and absent artist tag votes as coverage gaps. Its ten selected receipts used four recording-scoped and six artist-scoped descriptions; all ten scores replayed with zero error.

Warm pure numerical replay of the original 65 receipts took about 3.05 ms median and 3.66 ms sample p95 across 30 loops after five warmups on this host. Allocation in the initial 30-loop measurement was 43,703,376 bytes total, about 22 KB per receipt replay. This is a local replay micro-measurement, not a production scoring benchmark, retained heap measurement, or end-to-end capacity claim. The ten exported live receipts replayed in 0.86 ms median / 1.03 ms sample p95. Three private export reads in the browser check took approximately 6.8–7.3 ms.

No profiler was attached to an existing process. No complete live per-job upstream request count, CPU attribution, retained-heap baseline, or representative latency distribution was collected. Pool limits, configured per-client limits, distinct response references, and fixture call counts are not interchangeable with measured total job HTTP calls. Existing stage instrumentation remains available for a separately bounded diagnostic session if needed. The observed job/replay difference cannot attribute the remaining time to a particular provider, queue, database, or stage.

## Listener and founder isolation

| Boundary | Source and executed evidence | Remaining limit |
| --- | --- | --- |
| Listener identity/API | `ListenerContext` resolves account identity before guest credentials. Existing guest/API and account/session tests passed. Browser contexts were isolated and private exports remained separate. | Not an exhaustive penetration test or every browser/account migration permutation. |
| Shared public pools | New `ListenerChangesAndConcurrentRankingDoNotPersonalizeSharedPublicPools` uses actual shared preparation and a synthetic provider; B cold, concurrent A/B/C, and B warm/cache-only produce identical B picks/receipts while A's dislike/settings differ. Even deliberately foreign signals/feedback passed to B are filtered. Shared pool serialization remains unchanged. | Synthetic fixture verifies behavior, not subjective music quality. The test uses tags and public recording evidence, not every entity/fallback combination. |
| Shared public persistence | Existing codec, persistent preparation, cross-profile public album lookup reuse, expiry, cancellation, and origin tests passed. Cache keys are entity/request/configuration based; payloads hold public evidence before private scoring. | Mutable object surfaces and future codec additions still require regressions; no universal proof of all possible cache states. |
| Database feedback/history | New PostgreSQL test rejects A writing B's recommendation, prevents A reading B's batch, preserves B's revision/taste/feedback during A's dislike, and preserves B after A's deletion. Shared recording identity remains public while feedback stays owner-specific. Existing alias/identity and lease-ownership tests passed. | Does not enumerate every future summary or account import pathway. |
| Notes and durable feedback | New PostgreSQL characterization creates one older Like and 20 newer note-only rows. All 20 loaded rows are note-only and the stored Like produces no positive effective input. | Passing characterization confirms a defect; it is not desired behavior or subjective quality proof. |
| Failure and fallback | Source failures do not become successful padded batches; recorded evaluation material is rejected by product preparation. Live/Hybrid Last.fm HTTP-failure tests verify no fixture fallback. | Not every future provider outage/partial-response combination. |
| Founder fixtures/defaults | Legacy Last.fm fixtures still contain founder-specific material. They are existing explicitly Synthetic fixtures, not new defaults. Current production validation requires Live ListenBrainz; DI rejects live Last.fm generation, and the client does not silently fall back to those fixtures on a live failure. No global taste or founder-specific production weighting was added. | Fixture presence is an existing boundary risk: preserve origin/mode tests and keep evaluation material out of production. Removing/replacing historical fixtures was not authorized in Phase 1. |
| Explanations/privacy | Receipts derive from per-profile inputs; personal artifacts stay ignored/private. Existing account/session and guest import/registration tests passed. | Complete selection replay, exhaustive export/deletion non-interference, and every legacy weekly-path combination remain unverified. |

No observed cross-user contamination was established in these scenarios. This is bounded evidence, not a blanket security guarantee. Popularity remains neutral, no model was trained, and no personal ratings or profile content was used to tune shared behavior.

## Historical Phase 4 reconciliation

Commit `f26d14e` records a real 2026-10-08 Last.fm acquisition and analysis: 129 requests, 96 eligible tracks, 48 artists, and a concealed review with 35 slots across six founder profiles. It records adequate shape/integrity checks and A–D comparisons, with complete listening ratings and formula selection still pending. Current mechanics documentation had omitted that historical section and incorrectly suggested no live run had occurred.

A summed tag affinity, B best-seed affinity, C best-seed plus supplied Last.fm match evidence, and D IDF-adjusted affinity were experimental alternatives. Reusable assets are the frozen-input method, concealed review, integrity checks, perturbation tests, and separation of acquisition from ranking. Founder seeds/outcomes are not the target distribution for Residue. No old recordings were reacquired, expired recordings revalidated, informal ratings imported, or formula promoted.

Differences in provider catalog, acquisition endpoints, tag normalization/coverage, seed membership, timing, and final policy prevent a causal old-versus-current quality claim. The 35 historical slots are not the 35 distinct current-beta personal review items. Their equal count is not evidence that the datasets match.

## Verification

All completed targeted runs passed with no skipped cases:

| Check | Passed |
| --- | ---: |
| Domain selection/materialization/scorer, including benchmark fixture arithmetic | 39 |
| Application preparation/taste and existing evaluation harness/scoring/metrics | 46 |
| Infrastructure cache/codec/preparation, including new isolation test | 23 |
| Infrastructure provider/failure/origin boundaries | 24 |
| PostgreSQL ownership/identity/public sharing/feedback characterization | 5 distinct tests (runs of four and two, one repeated) |
| Presentation guest/job tests | 7 |
| Presentation production/session/coordinated account tests | 34 |
| Python capture/review safeguards | 3 |
| Documentation regression tests | 5 |

Presentation built with zero errors. Existing NU1510 package-pruning warnings and existing xUnit2031 analyzer warnings remain; no dependency remediation was attempted. `secret_defaults.py`, `renewal_docs.py`, local Markdown-link checks, and `git diff --check` passed. Browser results are described separately above. Production binaries have no product-source change.

Tools reuse existing project dependencies. `residue_baseline.py capture` is explicitly read-only, bounded, and refuses a tracked/nonignored artifact destination or output reuse. `review` verifies the capture hash and creates unrated owner-scoped concealed sheets and a private key. `residue_replay.cs` uses the existing scorer and score codec; it supports stored DB receipts and exported API receipt casing. The browser functions run through Playwright MCP, create disposable guests, and clean up through the API.

Example commands (supply the actual loopback container/database/user explicitly; never put a password in command arguments):

```bash
python3 scripts/checks/residue_baseline.py capture \
  --container LOCAL_CONTAINER --database LOCAL_DATABASE --db-user LOCAL_DB_USER \
  --settings .linernotes/live/settings.json --runtime-checkout . \
  --output diagnostics/results/NEW_PRIVATE_RUN/before

python3 scripts/checks/residue_baseline.py review \
  --baseline diagnostics/results/NEW_PRIVATE_RUN/before --profile PRIVATE_PROFILE_UUID \
  --output diagnostics/results/NEW_PRIVATE_RUN/review

dotnet run --file scripts/checks/residue_replay.cs --no-cache -- \
  diagnostics/results/NEW_PRIVATE_RUN/before/snapshot.json \
  diagnostics/results/NEW_PRIVATE_RUN/numeric-replay.json

python3 -m unittest discover -s scripts/checks -p test_residue_baseline.py
```

Do not rerun a live acquisition or overwrite reviewed files simply to reproduce a number. The browser functions target the explicitly launched localhost:5086 host and need corresponding configuration if reused. No personal secrets or private listening data belong in test fixtures or committed reports.

## Revised priorities and stopping point

The original six phases are a conditional backlog, not an approved 19–30 day commitment.

| Priority | Smallest next investigation/change | Why and provisional effort |
| --- | --- | --- |
| 1 | Correct feedback eligibility/recency using existing stored feedback, with explicit latest-state/reversal semantics. Measure query behavior before considering any summary table. | Note-only displacement reproduced in PostgreSQL. A focused 1–2 day scope is plausible; this alone does not solve all long-term memory. |
| 2 | Define and implement composable goal/exploration/vocal behavior with truthful decision receipts, initially using existing providers. | Source and tests confirm preference masking. Propose a focused 2–3 day behavior-contract change after listening review, without tuning to founder taste. |
| 3 | Separate valid favorite evidence from bounded acquisition windows; investigate actual larger-profile coverage before broad redesign. | Structural limit is verified, but current small data does not quantify subjective harm or justify a large persistence redesign. Scope after evidence. |
| Defer | New providers, AcousticBrainz integration, new persisted summaries, global weight/threshold optimization, and a wholesale scorer rewrite. | No demonstrated benefit/cost case from this baseline. Selected recording metadata is incomplete, but that first warrants measuring existing-provider capabilities. Nine existing feedback records do not justify a training/calibration program. |

Subjective quality remains unverified pending listening. End-to-end isolation beyond the tested matrix, full causal selection replay, exact live HTTP totals, and representative performance/capacity evidence remain explicit gaps. The captured evidence is sufficient to propose small changes, not to declare recommendation quality solved. Await private ratings and developer review; no next phase begins automatically.
