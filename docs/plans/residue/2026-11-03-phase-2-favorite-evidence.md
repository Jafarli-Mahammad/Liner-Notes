# Residue Phase 2 — consistently usable favorites

Date: 2026-11-03
Status: Implemented and locally verified; developer confirmation pending. Phase 3 has not begun.
Baseline: `5a91411`. Phase 1 is technically completed but neither personally validated nor formally accepted. Listening is deferred until after Phase 5.

## Scope and decisions

This implements the numbered Phase 2 in the [renewal plan](2026-11-02-recommendation-quality-renewal-plan.md): consistently usable favorites. Phase 1's proposed priority list did not renumber those phases. Feedback learning, preference composition, new providers, and global coefficient tuning were not implemented.

Brainstorming and Sequential Thinking MCP were used to compare the options. Increasing the old twenty-entity window would still couple evidence to acquisition. Persisted taste summaries would add ownership, migration, invalidation, and privacy work without evidence that they are necessary. The implemented option reuses existing saved catalog evidence and public caches, keeps acquisition windows, and records missing evidence explicitly. Existing Onion boundaries remain.

## Behavior

Previously, `MaterializeDiscoveryTaste` rotated over at most twenty positive artist/album/recording signals and materialized only those. Even valid saved evidence outside that window disappeared from the vector. Artists were then truncated before another window in batch preparation, and legacy requests filled only leftover slots. That could starve recording or legacy inputs.

Now:

- Every positive entity input is inspected. Fresh, correctly scoped saved tags and existing prepared evidence contribute regardless of the current acquisition window. Existing positive feedback entities follow the same preparation path; feedback interpretation/retention is unchanged.
- At most twenty distinct primary seed requests and twenty distinct credited-artist fallback requests are eligible to acquire per materialization. Repeated canonical reads are reused within that operation. Distinct feedback recording hints count as distinct requests, so one repeated artist name cannot bypass the primary budget.
- Pool acquisition applies one rotating window to canonical artists, recordings, and unresolved legacy artist names. It still permits at most twenty entity pools plus the existing bounded tag pools, with the existing candidate limit and parallelism.
- All known saved credits contribute familiarity even if descriptive evidence is unavailable. Canonical entities and evidence scopes stay distinct; artist tags never become recording-level vocal or acoustic facts.
- Existing canonical maximum aggregation prevents repeated album fallbacks from multiplying one artist's influence. The configured album fallback influence is unchanged. Album-specific tags remain independent, and explicitly saved recordings remain suppressed for their owner.
- Per-input receipts distinguish `supported`, `artist-fallback`, `awaiting`, and `unavailable`, with reasons. Unknown direct album evidence is distinguished from a returned response with missing tags. There is no synthetic musical substitute for absent metadata.

The obsolete Domain limit of 200 evidence entries was removed: it prevented complete processing of valid large profiles. Materialization is linear in saved evidence rather than capped at twenty favorites. Existing request-body, upstream response/cache, candidate, and serialized-receipt limits remain. Large profiles are not promised unlimited capacity.

## Receipts, ownership, and compatibility

`ScoreEvidence` adds `FavoriteEvidence`, `TasteEvidenceCount`, and `TasteEvidenceSha256`. The count/hash describe the complete materialized seed corpus, including empty returned evidence; the states account for positive entity signals, not only manually selected favorites. Full detailed seed records in each pick are capped at eighty. Numeric replay continues to use stored taste weights and unmatched magnitude; this is not full reconstruction of every provider payload after its cache expires.

The configuration policy records `TastePreparation=cached-favorites-v1`. Scoring weights, mathematical formula, popularity neutrality, and preference ordering remain unchanged. No new table, migration, provider, package, summary store, or preference control was added.

These fields live in existing owner-bound recommendation/batch JSON. Existing export includes them and deletion removes them with the profile. The shared public cache schema was not personalized. PostgreSQL and browser tests verified that another listener cannot read the batch, does not inherit its favorite/feedback exclusions, and retains its receipts after the first listener is deleted. UI explanations remain summaries; detailed preparation states are available through receipts/coverage and export.

## Reproducible comparisons

A detached checkout at `5a91411` ran the same new synthetic probe as the changed checkout. Inputs use generated identities and eight synthetic dimensions, with no founder favorites or ratings. No provider/database work occurs in this probe. Both runs use the same host, SDK, warmups, and thirty iterations. These are local resource measurements, not target-VPS capacity or subjective quality evidence.

| 1,000 fresh saved mixed favorites | `5a91411` | Phase 2 |
| --- | ---: | ---: |
| Contributing seed records | 20 | 1,000 |
| Sum of materialized taste weights | 20 | 1,000 |
| Identical vector across acquisition rounds | No | Yes |
| Median local materialization | 0.75 ms | 18.21 ms |
| Sample p95 | 2.55 ms | 30.76 ms |
| Allocation across thirty materializations | 8,681,784 bytes | 198,156,696 bytes |

This is a correctness improvement with additional linear work, not a speedup. Phase 2 allocates about 6.6 MB per materialization in this large local-evidence probe. Retained memory, large persistent-cache-miss profiles, live upstream latency, and VPS capacity were not measured. No costly optimization or persisted summary is justified solely by these results.

The existing frozen eight-genre contract covers sixteen genre/acquisition-mode outputs. Exact before/after payloads were compared: after removing only the added evidence metadata, preparation-version policy entry, configuration checksum, coverage JSON encoding, and payload-byte reservation, every other field matched. That includes picks, order, scores, contributions, existing evidence, and exclusions. A deliberately changed score was rejected as a negative control. The old full JSON hash is `6b8e6dd62d50a4ce6060a333ef7a2867879bd09f7077b29bd017fc596135faef`; the reviewed Phase 2 hash is `7b1861f521d3a9258496d26dfa6e8a8298099307f4e24e350f84555aaa89ba7a`. The frozen test was updated only after this comparison.

All 65 preserved Phase 1 scores replayed with zero numerical error under the new code, writing results into the Phase 2 directory. Original baseline artifacts were not rewritten.

## Automated and application verification

| Targeted verification | Passed | Skipped |
| --- | ---: | ---: |
| Application: favorites, preparation, export, JSON/contract compatibility | 49 | 0 |
| Domain: materialization, selection, scorer and benchmark arithmetic | 39 | 0 |
| Infrastructure: caches, public preparation, recording/artist identity | 34 | 0 |
| PostgreSQL: evidence persistence/export/deletion, onboarding, ownership | 3 | 0 |
| Presentation: guest API, job service, production configuration | 26 | 0 |
| **Total .NET cases** | **151** | **0** |

New tests cover 41/205/1,000 fresh mixed favorites, bounded progressive cold preparation, expired evidence, canonical fallback reuse, distinct recording-hint budgets, combined artist/recording/legacy rotation, and bounded 1,000-input receipts with numerical replay. PostgreSQL exercises the actual coordinated runtime protocol. An initial fixture used legacy runtime settings and hit the storage inventory guard; it was corrected to enable `RunJobs` and explicitly establish its disposable inventory, following existing coordination tests. Production guards were not disabled or changed.

Playwright MCP ran against the real application on localhost:5094 with a new disposable PostgreSQL database prepared by the existing `guest_discovery_demo.cs` helper. Provider mode was explicitly Synthetic/FixtureOnly; no live or founder data was used. The default MCP Chrome path was unavailable, so an installed MCP stdio server used `/usr/bin/chromium-browser` explicitly. Thirty-nine checks passed:

- Save and render 72 artist/album/recording favorites, all present in export.
- Account for all 72 fresh favorite inputs in the receipt while acquiring only twenty candidate pools.
- Produce three picks after excluding the owner's two saved recordings; show those picks in the actual UI.
- Persist the first listener's dislike through the existing feedback path.
- Give an independent listener all five candidates, with no leaked favorite evidence or exclusions; deny access to the foreign batch.
- Preserve the second listener's receipts after deleting the first; clear both disposable guests through the API.

The three exported browser receipts numerically replayed with zero error. This verifies application behavior with synthetic provider data, not musical enjoyment or fresh live-provider coverage. Production, account taste data, and the Phase 1 review were not used as mutable test fixtures.

Presentation built with zero errors. Existing NU1510 pruning warnings and existing analyzer warnings remain outside this phase. Documentation/secret-default checks and `git diff --check` were also run.

## Artifacts and reproduction

Separate, ignored evidence is under `diagnostics/results/residue-phase2-20261103/`: test/build logs, matched favorite-probe logs, exact eight-genre payloads/comparison, browser result and receipts/replay, historical receipt replay, and preservation hashes. Do not commit private evidence or credentials. The original `diagnostics/results/residue-phase1-20261102/` and Phase 1 evidence document were checked against twenty saved SHA-256 values and remain unchanged.

Example probe commands:

```bash
# Current implementation; synthetic local evidence only.
dotnet run --file scripts/checks/residue_favorite_probe.cs --no-cache -- 1000

# Same script against the original source in an isolated checkout.
git worktree add --detach /tmp/residue-reference 5a91411
cp scripts/checks/residue_favorite_probe.cs /tmp/residue-reference/scripts/checks/
(cd /tmp/residue-reference && dotnet run --file scripts/checks/residue_favorite_probe.cs --no-cache -- 1000)

# Capture the existing contract test's detailed output in each checkout.
dotnet test tests/Application.Tests/Application.Tests.csproj -m:1 \
  --filter FullyQualifiedName~DiscoveryOutputContractTests \
  --logger 'console;verbosity=detailed' > AFTER_LOG
python3 scripts/checks/residue_phase2_contract.py BEFORE_LOG AFTER_LOG
```

Run `scripts/checks/residue_phase2_browser.cjs` through Playwright MCP's code tool only against the configured disposable Synthetic host at localhost:5094. It is not a live-instance script. Reuse `scripts/guest_discovery_demo.cs --prepare-synthetic` with a fresh loopback database, private inventory/artifact paths, guest discovery and durable jobs enabled, and a one-second test cooldown. Provider fixtures and the cooldown override are test configuration, not production defaults.

## Limits and stopping point

Only fresh permitted evidence contributes. Evicted, expired, unsupported, or uncached data may remain unavailable/awaiting across generations; real providers may not permit long-lived caching. Coverage depends on those providers and budgeted acquisition. Detailed seed receipts are bounded, and oversized serialized receipts still fail the existing limit. This phase does not guarantee every possible imported profile fits a small VPS, canonical-alias familiarity is still limited by existing name-based familiarity semantics, and complete causal selection replay remains later work.

No global musical-quality claim follows from these tests. Phase 1 is technically completed but not personally validated or formally accepted. Its private review stays deferred until after Phase 5. Phase 2 implementation and evidence are separate from Phase 1; Phase 3 requires explicit authorization.
