# Residue Phase 4 — recording evidence and discovery connections

Date: 2026-11-03. Status: implemented and locally verified with limited live coverage; developer confirmation and subjective validation remain outstanding. Starts from Phase 3 commit `2a54445`. Phase 1's baseline and private listening review remain unchanged; listening is deferred until Phase 5. Phase 5 is not authorized by this implementation.

## Scope and implementation decision

The existing provider already acquires recording-similarity paths, recording tags and canonical artist credits. Hydration nevertheless discarded a discovered recording when both recording and artist tags were absent. It also fetched artist tags before inspecting available recording tags. Focused synthetic regressions reproduced both behaviors before implementation. The small live sample below did not demonstrate tagless hydration loss; no live yield improvement is claimed.

The implementation reuses existing candidate, gap, receipt and public-cache models:

- Prefer valid recording tags before requesting artist tags. Fetch fallback artist metadata only for candidates missing direct recording tags. This avoids optional requests and their failures when direct evidence already suffices.
- If both tag sources are empty, retain a candidate only when its provenance includes an actual ListenBrainz recording-similarity path. Keep an empty tag vector, `TagScope = "none"`, and `recording_relationship_without_tags` in candidate and pool gaps. Tagless artist/tag radio candidates still abstain.
- Preserve the original discovery path, seed recording identity, response hashes and retrieval timestamps. The relationship supplies no numeric similarity value and no acoustic, vocal, mood or genre facts. The existing aggregate scorer assigns zero tag affinity; any score comes from its existing artist-novelty term. No relationship bonus or new coefficients were added.
- Receipts explicitly explain the missing descriptive evidence. Public cache encoding, stored-score encoding and numerical replay preserve the relationship and gap. Pool version `prepared-pool-v4` invalidates old prepared pools; existing raw HTTP cache rules and seed cache version remain unchanged.
- Treat non-object recording tags, malformed count types and malformed tag scopes as missing evidence instead of throwing. Existing origin, metadata identity, request, working-set, response-size and source-failure checks remain enforced.

This can make a connected, tagless recording eligible for a batch at its existing novelty-only score. That is evidence preservation, not a demonstrated relevance improvement. Coordinated eligibility, exploration, ordering and shortfall rules remain Phase 5 work. Core acquisition failures still fail through the existing source-validation policy; they are not relabeled as successful sparse pools.

No schema, dependency, extra service, paid source, persistent taste summary or production provider route was added. Application only formats an evidence limitation; provider-specific interpretation remains in Infrastructure. Existing owner-bound receipts/export/deletion carry the same nested evidence fields. Public pools contain no listener preferences, feedback, exclusions or account identifiers.

## Bounded live audit and limitations

`scripts/checks/residue_recording_probe.cs` is an explicitly invoked evaluation tool, separate from application defaults. It uses eight public examples spanning electronic, jazz, country, chanson, hip-hop, pop and classical interpretation. These are not founder favorites, quality labels or calibration inputs. Search selects an exact title and artist credit from at most ten results; that selection is not proof of a definitive recording/version.

The tool uses the existing live ListenBrainz adapter and shared MetaBrainz pacing gate. Optional MusicBrainz lookups are limited to one recording's credits/releases and one release's track list per resolved seed. It has a six-minute deadline, 15-second HTTP timeout, 1 MB response bound, and at most 48 public requests per invocation. It sends no credentials, retries no failed requests, traverses no relationship graph, writes no application data, and persists no raw provider responses. Summary output includes source request, retrieval time, response hash, status, latency, counts and limitations; keep captures ignored.

Two runs were performed, with search corrected between them rather than treating title-only misses as source absence:

| Run | Observed evidence |
| --- | --- |
| Initial title-only search | 6/8 examples missed exact credits in the ten-result window. The two resolved versions returned no similar recordings. Optional release lookups contained 100 and 108 distinct recordings, including their seed, illustrating compilation/release ambiguity. This was a search limitation, not a measured absence of similar music. |
| Title-and-artist search | Four seeds resolved: 1950, 1996, 2017 and 2019 first-release metadata. Three searches returned HTTP 503; one had no exact credit. Of four similarity requests, three yielded ten candidates each and one yielded none. |
| Hydration on those 30 candidates | 28 had recording tags, two used artist tags; 30 survived, zero were tagless and zero were lost during hydration. The change's tagless preservation benefit is established by controlled regression, not by this live sample. |
| MusicBrainz optional requests in corrected run | 14 requests including search: eight HTTP 200 and six HTTP 503. Two recording-detail responses exposed 2 and 25 recording-level artist relationships respectively, each with one work relationship. One release lookup returned 20 recordings including its seed; another failed with 503. Other optional detail requests also failed. Missing responses are unknown coverage, not zero credits or zero album tracks. |

The corrected run used eight ListenBrainz adapter requests: four similarity, three recording-metadata, and one artist-metadata request. The three nonempty recording pools each fit their fallback artists within a single metadata chunk. The old unconditional hydration would have requested artist metadata for all three pools; direct-tag preference avoided two such requests on this sample. This is a same-pool request-count comparison, not a measured end-to-end speedup.

Corrected-run pool acquisition plus hydration took approximately 1.79, 2.55 and 1.32 seconds for the three nonempty pools; the empty pool took 0.12 seconds. Optional MusicBrainz request durations, including gate wait, had a median of 0.88 seconds and a maximum of 1.60 seconds. Small, sequential, failure-affected samples do not establish production latency distributions or population coverage. Live request results are not a frozen before/after experiment, and no second listener's private data participated.

## Source decisions

| Evidence source | Decision | Reason and incremental cost |
| --- | --- | --- |
| Existing recording-similarity paths | Adopt preservation | Already acquired and source-scoped. Retaining a tagless relationship adds no provider request. No numeric match value or acoustic interpretation is manufactured. Live tagless yield remains unmeasured. |
| Existing recording tags and canonical credits | Retain; prefer direct tags | Current scope and provenance are sufficient for descriptive tags and identity. On the 30-candidate sample, 28 direct scopes and two artist fallbacks remained distinct. Two unnecessary fallback requests were avoided. |
| Additional recording/work relationships | Defer production enrichment | Two successful detail responses demonstrate available connections, but variable counts, missing responses and no matched candidate-yield or listening benefit do not justify traversal. At least one extra lookup per recording, then further lookups for discovery, would add request and failure costs. A shared contributor is not proof of similar sound. |
| Album membership as recording seeds | Defer production route | Membership was verified in three successful release samples, but two contained 100+ recordings. One arbitrary release/version or compilation cannot identify which tracks a listener likes. A bounded recording-detail plus release lookup already costs up to two extra requests before similarity acquisition. No sampled album track is promoted to an explicit favorite, and no new seeds are injected. |
| AcousticBrainz | Reject integration for this phase; no API probe | The official closure announcement describes confidence/coverage problems and discontinued collection. Existing evidence does not establish that a bounded acoustic-descriptor probe would improve this pipeline. Do not add a legacy dependency or claim reliable melody/vocal evidence. |

No optional enrichment was integrated, so its outages cannot block core renewal. Reconsider deferred routes only after independent, diverse coverage and matched-budget yield evidence demonstrate useful benefit. No global setting was tuned from these examples or personal listening notes.

Official references checked on 2026-11-03:

- [ListenBrainz recording metadata](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html): separate recording/artist tag scopes; request only consumed metadata.
- [ListenBrainz API limits and identification](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html) and [similar-recordings viewer](https://labs.api.listenbrainz.org/similar-recordings).
- [MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API) and [rate limiting](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting): explicit relationship includes and shared-IP pacing.
- [MusicBrainz data licensing](https://musicbrainz.org/doc/About/Data_License): core and supplementary data have different conditions. No optional raw responses or new data source are published by this phase; existing provider release/attribution gates remain.
- [MetaBrainz AcousticBrainz closure announcement](https://musicbrainz.wordpress.com/2022/02/16/acousticbrainz-making-a-hard-decision-to-end-the-project/).

## Verification and preservation

497 automated cases passed with zero failures or skips: 162 Domain, 166 Application and 169 targeted Infrastructure. The Domain suite includes benchmark genres and scorer replay. The infrastructure selection covers recording ingestion, malformed/missing evidence, provider transport/provenance, shared/persistent pools and public-evidence codecs. A source-failure regression now injects failure into required recording metadata for pools, rather than into an artist endpoint that complete recording evidence no longer needs; required-source failure still prevents cache publication and a later acquisition retries correctly.

A new Application A/B/A test prepares the same public tagless pool for different listeners and verifies separate taste vectors, zero inferred affinity, retained relationship/gaps, explanatory copy, stored-score round trip, exact numerical replay and unchanged public candidate data. This is application correctness, not a new browser or full production isolation proof. No UI layout was changed, and no browser, production deployment or database migration was run for this phase.

The frozen eight-genre, two-acquisition-mode contract has the same exact SHA-256 as Phase 3, `fd538cedd9fe4344586e5f351bc76cabfab6fbe146b92ecc9f62392d81146c58`. All 65 original beta receipts replay with zero score and raw-similarity error; ten controlled Phase 1 receipts also replay exactly. SHA-256 checks protect 74 prior-phase files, including the private review and Phase 2/3 reports. They remain unchanged.

Private ignored evidence is under `diagnostics/results/residue-phase4-20261103/`: test logs, initial and corrected live summaries, replay results, contract output and preservation hashes. Raw provider bodies were not persisted. Existing dependency/pruning and tooling warnings were not remediated as part of this task.

Reproduction from repository root, using the pinned .NET SDK:

```bash
dotnet test tests/Domain.Tests/Domain.Tests.csproj --no-restore -m:1
dotnet test tests/Application.Tests/Application.Tests.csproj --no-restore -m:1
dotnet test tests/Infrastructure.Tests/Infrastructure.Tests.csproj --no-restore -m:1 \
  --filter 'FullyQualifiedName~ListenBrainz|FullyQualifiedName~Recording|FullyQualifiedName~Evidence|FullyQualifiedName~DiscoveryPreparation'
dotnet run --file scripts/checks/residue_recording_probe.cs --no-cache -- --help
# Explicit live probe; availability and results may differ. Keep output private and ignored.
dotnet run --file scripts/checks/residue_recording_probe.cs --no-cache -- --live
```

Rollback uses the prior provider/hydration code and prepared-pool namespace; there is no schema or user-data migration to undo. Do not modify stored historical receipts. The outstanding questions are tagless live yield, broader source coverage, relevance under the existing novelty-only treatment, optional-route benefit and subjective enjoyment. Phase 5 remains a separate approval.
