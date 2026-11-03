# Residue Phase 3 — durable and reversible feedback

Date: 2026-11-03. Status: implemented and locally verified; developer confirmation and subjective validation remain outstanding. Implementation starts from Phase 2 commit `bcc1cb1`. Phase 1 (`5a91411`) remains technically completed, not personally validated or formally accepted. Listening remains deferred until after Phase 5.

## Scope and complexity decision

The approved phase addresses durable interests, recent context, reversible feedback, careful negative inference, and listener isolation. Brainstorming and Sequential Thinking MCP were used. The smallest sufficient change reuses authoritative recommendation feedback, existing timestamps, provider preparation, and private receipts. There is no new provider, summary table, migration, dependency, or global musical preference.

The original discovery query decoded only the latest 20 full feedback receipts, including notes. The Phase 1 characterization demonstrated an older like being displaced by 20 note-only rows. Increasing that window cannot provide durable memory. Reading every historical full receipt would make expensive decoding grow with history. Instead, a shared DataAccess query reads lifetime scalar votes and at most the configured recent window of detailed receipts. The legacy weekly path uses the same projection with a 20-receipt context window; the durable-job weekly path continues through shared discovery preparation.

Existing `FeedbackGivenAt` now records changes to feedback or rating. Existing `LastModifiedAt` records audit changes, including notes. An initially proposed extra timestamp column proved unnecessary and was removed before delivery. Historical audit timestamps cannot reconstruct which earlier updates were notes, so existing data is left untouched. New note-only rows have no musical timestamp. Existing note-only rows may remain neutral scalar history entries, but cannot occupy the detailed musical context window.

The scalar scan remains linear in retained feedback, not constant cost. Its measured cost and limited beta data do not justify persisted taste summaries yet. Large-history growth remains a monitoring concern; expensive full-receipt decoding is bounded rather than extended to all history.

## Resulting behavior

- Durable artist evidence comes from current positive votes across retained history. A single representative per learned artist prevents many likes of that artist becoming many acquisition seeds. Existing cached evidence supplies its musical descriptors; unavailable metadata remains unavailable rather than invented.
- Recent positive feedback additionally contributes the existing top-three matched tags from recent stored receipts, at the existing configured feedback-tag weight. Each learned tag retains its strongest signal once, so duplicate likes do not inflate the normalization group and weaken the contribution. This is recent context, not a claim that matched tags identify why the listener liked a recording.
- Transitive recording aliases are merged with a disjoint-set pass. The latest musical state wins deterministically. Clearing or changing a vote replaces its contribution, including when an older recommendation of the same recording is edited.
- Exact dislikes and low ratings suppress their recording aliases. Known status remains distinct from dislike and contributes no negative vote without an actual low rating. Retained known markers are read correctly after subsequent likes. Clearing feedback can clear those markers; recommendation-history suppression remains separately intact.
- Notes are never interpreted. In discovery, editing only a note preserves the taste revision and queued generation job; a musical change advances the revision and cancels stale generation. Legacy account feedback retains its existing coordinated revision invalidation, but notes no longer refresh musical time there either.
- Explicit favorites and imported/manual signals remain independent from inferred feedback. No other user's signals participate.

Negative inference is intentionally narrow: at least `MinimumNegativeFeedbackRecordings` independent recordings (default 3), and more negative than positive recordings for that artist, soften only its learned positive contribution. The factor is the positive share of positive-plus-negative recordings, floored at the existing `MinimumPositiveFeedbackWeight` (default 0.4) of its original learned weight. Positive counterevidence and edited/cleared votes can remove attenuation. A single dislike does not imply artist or genre aversion. The support threshold and rule are conservative, configurable behavioral choices, not listening-calibrated optima.

An artist with no learned positive contribution has no such contribution to attenuate. This phase does not add a direct candidate penalty for that case, infer disliked genres/moods, or weaken explicit favorites. Broader ranking interactions remain for the separately approved selection phase and must be evidence-led.

## Architecture, explanations, and privacy

Domain owns pure alias resolution, vote interpretation, aggregation, and attenuation. Application orchestrates materialization and records the policy. DataAccess projects only owner-filtered scalar history and bounded recent receipts. Provider integrations and acquisition request budgets remain unchanged. The numerical scorer is unchanged.

Private score evidence and batch coverage store `durable-artists-recent-tags-v1`, artist support counts, resulting weights, and attenuation flags. Detailed support is limited to 80 artists, with total count and a SHA-256 of the complete ordered support. The hash is calculated once per preparation, not once per candidate. Complete input hashes, existing score-vector receipts, and numerical replay remain available. This does not claim complete causal replay of final selection; that remains later work.

There is no extra persisted taste materialization to reconcile or delete. New nested receipt fields follow existing owner-bound recommendation/batch export and cascade deletion. Existing export already includes both timestamps and private notes. Public candidate caches retain public metadata only; support counts and feedback history are never published into them or application defaults. No founder account, ratings, favorite lists, or historical founder calibration were used.

## Verification and observed evidence

304 automated cases passed without failures or skips:

| Area | Passed | Relevant evidence |
| --- | ---: | --- |
| Domain | 162 | Entire suite, including scorer/benchmark fixtures, durable and recent tastes, aliases, negative support, reversal, known status, note timing, and foreign-history rejection |
| Application | 51 | Feedback/weekly paths, favorite evidence, receipt codec, replay of attenuated evidence, and frozen eight-genre contracts |
| DataAccess | 31 | Real loopback PostgreSQL: >20 musical votes, note-only displacement regression, note/job behavior, concurrent edits, known-marker reversal, foreign ownership, export/deletion, migration round trips and reviewed privacy inventory |
| Infrastructure | 34 | Existing shared preparation, public evidence/cache codecs, recording identity and feedback seed boundaries |
| Presentation | 26 | Existing guest discovery API, jobs and production configuration contracts |

Playwright MCP, using installed Chromium through the MCP stdio server, passed 30 checks against a separate Synthetic application at localhost:5096. Two independent browser contexts saved different taste tags and generated their own five-pick batches. Real API writes and private exports verified like → note edit → dislike → like → known → clear; musical timestamp and revision preservation for notes; retained recommendation history; foreign-feedback and foreign-batch denial; unchanged second-listener taste/feedback/receipts; and deletion isolation. Test guests were cleared. This is application correctness evidence, not live-provider or listening evidence.

All 16 genre/acquisition fixture results match Phase 2 after explicit receipt/policy metadata normalization. An exact new payload hash is frozen in the contract test; the comparator rejects changed musical scores and nonempty feedback in these no-feedback fixtures. All 65 original Phase 1 receipts replay with zero numerical error, including both artist- and recording-scope evidence.

A local synthetic pure-aggregation probe, 30 measured iterations after warmup, reported:

| Scalar votes | Median | p95 | Allocated per aggregation |
| --- | ---: | ---: | ---: |
| 1,000 | 1.42 ms | 2.27 ms | 1.34 MB |
| 10,000 | 16.61 ms | 26.35 ms | 13.25 MB |

These measurements exclude SQL, provider I/O and the UI. They are not an end-to-end speedup claim. The PostgreSQL 1,000-vote check separately verified 1,000 scalar observations, only 20 detailed receipts, and 20 artist support groups. Existing provider request caps and storage admission remain enabled. Compilation reports existing dependency/tooling warnings; no dependency remediation was undertaken.

## Preservation and reproduction

Separate private, ignored evidence is under `diagnostics/results/residue-phase3-20261103/`: verification logs, browser results and receipts, the no-feedback comparison, historical replay, feedback probe, and preservation manifest. SHA-256 checks protect 42 prior-phase files, including both reports, all baseline evidence and private review files. They remain unchanged. Do not commit private evidence or credentials.

Use the repository-pinned SDK and configured loopback `LINER_PHASE1_POSTGRES` for database tests; never substitute a personal beta database. Targeted verification filters and counts are recorded in the private manifest. Example checks:

```bash
dotnet test tests/Domain.Tests/Domain.Tests.csproj --no-restore -m:1
dotnet test tests/Application.Tests/Application.Tests.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~Feedback|FullyQualifiedName~GenerateDigestTests|FullyQualifiedName~DiscoveryOutputContractTests|FullyQualifiedName~FavoriteEvidenceTests|FullyQualifiedName~ScoreBreakdownJsonTests'
dotnet test tests/DataAccess.Tests/DataAccess.Tests.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~ResidueFeedback|FullyQualifiedName~NoteOnlyRows|FullyQualifiedName~Phase8PrivacyInventoryTests|FullyQualifiedName~DiscoveryMigrationTests|FullyQualifiedName~Phase6PostgresTests'
dotnet run --file scripts/checks/residue_feedback_probe.cs --no-cache
```

`scripts/checks/residue_phase3_contract.py` compares the detailed contract log with the preserved Phase 2 reference. `scripts/checks/residue_phase3_browser.cjs` runs through Playwright MCP only against a disposable Synthetic host. Reuse `guest_discovery_demo.cs --prepare-synthetic` and Phase 2's isolated QA configuration procedure, with a fresh database/private artifact root, URL localhost:5096, durable jobs enabled and one-second test cooldown. No production process, account, provider data or listening review should be modified.

Phase 3 is technically verified, not proof that recommendations sound better. Historical timestamp ambiguity, missing artist descriptors, conservative negative inference, and linear scalar-history cost remain explicit limits. Phase 4 has not begun and requires separate authorization.
