# Residue Phase 5 — coordinated acquisition and connected selection

Date: 2026-11-03. Status: implemented and locally verified; developer confirmation and listening validation remain outstanding. Starts from Phase 4 commit `e7fc486`. Phase 6 is not authorized by this implementation. Original baseline captures and the private listening review remain unchanged.

## Resulting behavior

Discovery now requires a supported relevance connection before novelty or soft preferences can influence inclusion. Eligible candidates have sufficient aggregate/individual-interest affinity, or an actual acquisition path from a selected favorite recording. A path naming an unrelated recording does not qualify. Missing tags remain missing: a connected tagless recording receives zero tag affinity, and no acoustic similarity, vocal property or numeric provider match is invented.

Goals, exploration, supported recording-level vocal evidence and recent artist concentration contribute bounded additive adjustments. They no longer act as successive sort keys with unconditional precedence. Conflicting preferences can both affect a result. No genre or popularity penalty, universal exploration percentage, fixed experimental slot or required batch fill was introduced. Batches can contain fewer than five picks, including zero.

The discovery page reports shortfalls and retains its generation action for zero- and one-pick batches. Existing layout and styling remain. Stored explanations distinguish the numerical taste score from the selection score, name a supported individual interest where applicable, and state when a control actually changed selected position. Unknown or conflicting vocal evidence is neutral.

## Policy and experimental configuration

The existing numerical scorer remains pure and historically replayable. The new Domain policy is `connected-selection-v2`; Application supplies owner-filtered inputs and formats explanations. Infrastructure only supplies public metadata and enforces dispatch limits. No new provider route, external dependency or database schema was added.

`Generation:Selection` contains the experimental policy settings:

| Setting | Default | Meaning |
| --- | ---: | --- |
| `MinimumAffinity` | 0.10 | Minimum combined tag affinity unless a favorite-recording path exists |
| `IndividualInterestWeight` | 0.35 | Add at most this fraction of the positive gap between individual and aggregate affinity |
| `MinimumIndividualSharedTags` | 2 | An individual-interest contribution needs multiple matching descriptors |
| `GoalWeight` | 0.25 | Maximum goal adjustment |
| `ExplorationWeight` | 0.35 | Maximum exploration adjustment |
| `VocalWeight` | 0.15 | Signed adjustment using supported recording-level evidence only |
| `RecentArtistWeight` | 0.15 | Upper bound on recent-exposure attenuation |
| `CloseLimit`, `BalancedLimit`, `SurpriseLimit` | 3, 6, 10 | Candidates requested per public pool, capped by existing `Generation:DiscoveryLimit` |
| `BroadTagSlots` | 3 | Maximum broader genre routes when AllStyles permits them |

Aggregate affinity retains any existing album-fallback influence. Individual affinity compares a candidate with full available entity vectors, not only the first eighty receipt seeds. It respects the seed's fallback influence and the positive signal's weight relative to the strongest positive signal. Repeated-descriptor evidence and the bounded combination avoid immediately replacing aggregate affinity with an unconstrained best-seed maximum. Manual tags still contribute to aggregate taste; a one-tag entity coincidence does not receive an individual-interest bonus under the default support rule.

The combined relevance value is `aggregate + individualWeight × max(individual − aggregate, 0)`. Selection utility starts with the existing numerical score and adds that bounded individual contribution and the goal, exploration, vocal and history terms. Favorites favor supported individual/favorite-recording connections; different-styles and surprise favor distance only in the presence of a connection. Close exploration rewards aggregate affinity. New-artists rewards unfamiliarity. Neutral, skipped and unanswered controls add no preference term. The default acquisition budget for unanswered exploration is the balanced limit.

Recent artist exposure uses the latest forty retained recommendations, independent of feedback-note edits. For the most-exposed credited artist, attenuation is `−RecentArtistWeight × n/(n+1)`. Counts are streamed from owner-filtered rows and are not persisted as another taste summary. There is no lifetime genre aversion or inference from free text.

Canonical credited artist IDs prevent duplicate artists within a batch and support concentration across batches. Multiple credited artists are treated conservatively: an overlapping credit prevents another pick with that credit in the same batch. Known canonical credits also prevent an alias spelling from becoming an unfamiliar artist. When canonical identity is absent, normalized display names remain the fallback. Old receipts without canonical credits retain name-based history; ambiguous name-only history is not silently equated with a canonical ID.

These thresholds and coefficients are behavioral choices, not calibrated optima. The 0.35 individual term bounds its gain; the two-descriptor rule rejects one-descriptor coincidences; the relevance floor removes novelty-only candidates. Their musical usefulness still needs listening evidence and broader evaluation.

## Acquisition and request bounds

The same provider abstractions, canonical/recording routes, fairness windows and hard seed/candidate limits are reused. Exploration changes the effective per-pool limit to 3/6/10 under the operator's cap. That effective public request parameter is part of the public pool key. Listener answers, feedback and private ranking inputs are not put into shared caches.

AllStyles remains permission to search broadly. When surprise is requested, or no manual tag routes exist, up to three broader genre routes supplement the entity routes and available selected-tag slots. Focused tag routes rotate within the remaining tag budget. Full positive taste evidence remains available for ranking; acquisition windows do not remove it. Broad candidates still need relevance and can all be rejected.

Source inspection found that the former eighty-request HTTP allowance belonged to each client instance, while independent pools create separate client scopes. `DiscoveryRequestBudget` now shares an allowance across seed materialization and parallel pool clients for a preparation attempt. Both configured HTTP adapters spend it immediately before a real dispatch; cache hits do not spend it. `Generation:MaximumUpstreamRequests` defaults to 80, with a validated maximum of 200. Existing provider pacing, cache rules, response bounds, pool deadlines and job deadlines remain.

The allowance is per preparation attempt, not a persisted counter across retries or process restarts. Existing bounded job attempts still apply: at the default three attempts, the dispatch envelope is at most 240 across failed preparation attempts. A cold large profile can exhaust the allowance; no partial personal batch is committed to disguise that failure. Completed eligible public cache work can remain reusable. This is a bounded request contract, not a production throughput or end-to-end latency claim.

## Selection receipts, storage and privacy

Coverage version `discovery-coverage-v2` retains the full considered candidate pool, normalized decision inputs, relevant exclusions, recent artist counts, effective acquisition requests, configuration, answers, outcomes and shortfall. Each selected score also retains every normalized candidate input and the selected decisions. Unselected decisions are derivable rather than duplicated in every score; `SelectedDecisionsOnly` makes that explicit. Replay reconstructs all decisions and verifies the stored selected decisions, displayed order and shortfall.

Counterfactual selections remove each soft term in turn. Explanations claim a changed position only when that pick's selected rank differs without the term. The receipt can show a nonzero adjustment that did not change the selected position. Selection replay is separate from numerical score replay and does not claim to reconstruct an upstream provider's algorithm or expired raw responses.

All considered candidates are bounded at 400, requested picks at five, exclusions at the relevant alias bound, and recent history at forty recommendations. Repeated aliases equal to the track key are omitted; the track key remains an implicit exclusion identity. The 1,000-favorite/400-candidate regression keeps all favorite coverage states and all normalized selection inputs within the existing 100,000-byte stored-score limit. Its recorded first receipt was approximately 97.7 KB. This is a tested fixture near the limit, not a guarantee for arbitrarily long or high-entropy evidence.

The combined fixture exposed an existing gzip-envelope capacity limit. Large new receipts use the built-in Brotli codec, with stronger compression only if the initial result exceeds the row cap. Plain JSON and historical gzip envelopes remain readable and checksum-validated. The four-megabyte expanded limit and 100,000-byte stored limit were not raised; incompressible or oversized evidence still fails closed. Stronger compression adds CPU cost on unusually large receipts. No provider data or decision inputs are truncated to make the test pass.

Owner-bound batch/recommendation export and deletion carry these nested fields. Streaming recent-history queries remain profile/account filtered, including the legacy weekly path. Public caches contain public metadata and canonical credits only. The anonymous recent-music projection still exposes public recording metadata rather than private scores, preference inputs or receipts. The legacy weekly path uses the same selection implementation and records the extra recent-history inputs in its optimistic revision. Its live empty-pool and insufficient-relevance outcomes remain explicit stops instead of sending an unsupported weekly digest.

Pool namespace `prepared-pool-v5` accounts for retained canonical credits and effective request limits. There is no data migration. A policy rollback must retain the new receipt readers: an older binary cannot decode Brotli receipts or replay the new selection version. Do not rewrite historical receipts or deploy an old reader over newly stored data.

## Verification and actual observations

648 automated cases passed with zero failures or skips:

| Area | Passed | Evidence |
| --- | ---: | --- |
| Domain | 176 | Scorer benchmarks/replay, relevance, all 360 valid preference combinations, joint control effects, vocals, canonical diversity, exposure attenuation, input bounds and full/compact selection replay |
| Application | 174 | Effective acquisition routes/limits, AllStyles, actual versus unrelated recording paths, canonical familiarity, weekly selection, frozen pools, codec compatibility and 1,000-favorite/400-candidate storage |
| Infrastructure | 187 | Public keys/yield by limit, canonical credit/cache round trips, cold/warm/concurrent listener isolation, adapter failures and a shared request cap across parallel clients |
| DataAccess | 85 | Real disposable loopback PostgreSQL: receipts/codec persistence, owner-filtered history, exports/deletion, revision reconstruction, isolation, coordination and reviewed privacy inventory |
| Presentation | 26 | Guest API, job scheduling/fairness and production configuration, with database prerequisites configured |

The queue-fairness fixture now gives each profile a relevant pool. Its former unrelated ambient/jazz pool correctly abstained under the new policy; increasing its timeout would have hidden the fixture problem. Required-source failures still fail and do not publish partial successful pools. A transport test limits sixteen parallel calls across four client instances to three real dispatches, then verifies a new generation allowance.

Playwright MCP used installed Chromium against a separate Synthetic host on localhost:5098. Thirty-two checks passed. One listener's close setting acquired and displayed three supported picks; an unrelated classical-tag listener got zero instead of padded novelty picks. Widening the first listener to surprise acquired five candidates, suppressed the original three by history, and displayed two new picks. Stored coverage recorded the limits, exclusions and shortfalls. The checks also exercised empty/short regeneration actions, a narrow reduced-motion viewport, private export, foreign-batch denial, deletion and unchanged second-listener state. Test guests were deleted. This was actual application behavior with fixtures, not live-provider or listening evidence.

All five browser-exported picks pass complete selection replay and exact numerical replay. All sixty-five original beta receipts and ten controlled Phase 1 receipts still replay numerically with zero error. SHA-256 checks preserve ninety-three prior-phase files, including the listening review and previous evidence reports.

On identical eight-genre fixtures in both acquisition modes, the old policy displayed eighty picks and the new policy displayed sixteen. Every retained paired numerical score is unchanged. This characterizes removal of unsupported padding in that deliberately sparse synthetic pool; it does not prove that those sixteen tracks are enjoyable. The new frozen output hash is `e02ca68de09fa3159896535b0ecd02a7d42a88c6cbf82eed667f2e4fcf393877`. The Phase 5 comparator also rejects altered paired scores.

Eight independent synthetic two-interest profiles were evaluated against the same sixteen-candidate pools. Best-seed ranking changed the aggregate top pick in four profiles; the bounded combination changed none of those top picks and added at most about 0.113 affinity. This supports a bounded trial instead of adopting an unconstrained maximum; it establishes no subjective superiority. No founder listening notes, personal ratings or held-out outcomes were used to set coefficients.

A thirty-iteration warm probe over 400 normalized selection candidates measured about 1.93 ms median, 4.90 ms p95 and 466 KB allocated per selection. It excludes individual-affinity materialization, receipt compression, SQL, provider requests and UI. No end-to-end speedup is claimed. The frontend typecheck and production build passed. Existing dependency/pruning/tooling warnings were not remediated.

## Reproduction and remaining limits

Private ignored evidence is in `diagnostics/results/residue-phase5-20261103/`: verification logs, comparisons, browser exports/replays, storage probes and preservation hashes. Keep personal captures and future listening ratings out of source control and shared calibration.

```bash
dotnet test tests/Domain.Tests/Domain.Tests.csproj --no-restore -m:1
dotnet test tests/Application.Tests/Application.Tests.csproj --no-restore -m:1
dotnet test tests/Infrastructure.Tests/Infrastructure.Tests.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~ListenBrainz|FullyQualifiedName~Recording|FullyQualifiedName~Evidence|FullyQualifiedName~DiscoveryPreparation|FullyQualifiedName~LastFm'
# Configure LINER_PHASE1_POSTGRES for disposable loopback databases before database/API checks.
dotnet test tests/DataAccess.Tests/DataAccess.Tests.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~DiscoveryCoordinationPostgresTests|FullyQualifiedName~Phase8PrivacyInventoryTests|FullyQualifiedName~Phase6PostgresTests'
dotnet test tests/Presentation.Tests/Presentation.Tests.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~GuestDiscoveryApiTests|FullyQualifiedName~DiscoveryJobServiceTests|FullyQualifiedName~ProductionConfigurationTests'
```

Run `residue_phase5_contract.py` against the preserved Phase 3 detailed contract log and the new detailed Application log. Run `residue_phase5_browser.cjs` through Playwright MCP only on a new disposable Synthetic host prepared with `guest_discovery_demo.cs`, private storage/key paths and test cooldown configuration. `residue_replay.cs` checks selection replay when a receipt exists and preserves numerical-only replay for historical receipts.

Broader live acquisition coverage and latency, listener enjoyment, calibration, cold-profile request exhaustion, name-only identity limitations, large-receipt CPU/storage cost and target-host capacity remain limits. No new live-provider acquisition or personal taste mutation was performed in this phase. Phase 6 integrated evaluation and any listening/calibration decisions require the developer's next instruction.
