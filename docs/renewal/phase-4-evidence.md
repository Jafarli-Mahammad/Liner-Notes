# Phase 4 tooling: verification and pilot run procedure

Verified locally on 2026-10-07 on `Prism`, starting at `236dbc6`. The developer requested
Phase 4 implementation and repeated the instruction after reviewing the proposed scope.
Scoring/replay mechanics are committed in `e004f1d`; the operator/docs form the second focused
local commit. Developer confirmation remains pending.
**Mechanics only, no catalog conclusions.** No real recording, real scoring, listening result,
formula choice or provider permission is established by this implementation.

## Implemented scope

- Evaluation-only A–D lives in `tests/Application.Tests/Common`, using Application/Domain
  types. The bridge in `tests/Infrastructure.Tests/RecommendationSources` consumes explicitly
  injected `RecordedLastFmResponse` snapshots, `RecordedLastFmApiClient`, the shared evidence
  parser and existing discovery/hydration. It has no HTTP/DI, credential lookup or file discovery.
- Extracted the existing tag interpretation into `LastFmTagEvidence` for seed/corpus materialization
  and candidate hydration. The 12-entry stoplist and normalization behavior are unchanged.
  Stoplist hash: `737b1d5474dfa7abd6c6715e827567ce9275e96b3282d213f838bfdfa6ee8f27`.
- A uses the sum of supplied seed vectors; B/C explain the best supplied seed; C uses
  `max-valid-match-v1` over the current profile's paths only. Missing matches remain null.
  D applies the specified square-root IDF transformation to A, counting each received pilot artist
  once, including received empty vectors. No held-out responses/statistics enter this pipeline.
- `scripts/phase4_weights.json` supplies `pilot-weights-v1` coefficients through an embedded
  configuration resource in the existing test bridge. The scorer has no hardcoded weights or file discovery.
  `pilot-protocol-v1` stores those untuned coefficients (tag 1, novelty 0.2; C match 0.1), definitions,
  rules and reviewed membership hash. Changed protocols fail closed. All normalized tag contributions,
  factors, winning seed/match path and separate components are retained. Popularity never affects ranking.
- The artist-only profiles declare seed artists familiar; other artists are unlisted. Novelty is
  relative to that declaration. Known/disliked track sets are empty in this frozen pilot protocol.
  This is not evidence of what the developer has actually heard. Exclusion mechanics preserve siblings.
- `scripts/phase4_replay.cs` is a .NET 10 file-based utility, referencing the existing Infrastructure
  test bridge. No tracked project, folder outside the approved areas, package, framework upgrade,
  schema, migration, public API/export or production scorer change was introduced.
- `scripts/phase4_pilot.py` prepares controls and a nonexecutable draft, proposes exact manifest
  bytes from an explicit reconciled inventory, reads only the approved Phase 4 directory, verifies
  response hashes/receipt times and writes bounded local artifacts. Phase 5 is rejected before
  any response content is read. The separate recorder remains the sole live entry point.
- Receipt time is now recorded as `retrieved_at_utc`, independently of `started_at_utc`.
  Replay never invents a receipt time from the request start. No legacy real recordings were acquired.

## Shape, scoring and listening sequence

The operator saves `shape-report.json`, `shape-report.md` and `evidence.json` **before** invoking
A–D. It applies the approved yield/tag/integrity bars from [contracts](contracts.md#pilot-adequacy-bars--approved-concrete-recording-manifest-still-required).
Raw, deduplicated and eligible counts, all gaps, actual attempted seeds and response denominators
are retained. Missing snapshots do not count as attempted calls. Incomplete acquisition blocks scoring.

Raw tag noise covers all received nonblank artist/tag entries before filtering, once per normalized
name per artist, including the full recorded top-tags payload. Vectors retain the existing first-15
normalization bound. Non-stoplist tags are listed for ambiguity review; no automatic semantic labels
or post-hoc stoplist edits are made. Responses and hashes retain the original observations.

The recorder retains five top tracks per artist; existing discovery uses its first-two bound.
The shape report measures that actual candidate pool. Upstream popularity order is documented;
it is not an adopted ranking weight or production representative-track policy.

If shape bars fail, the durable shape/evidence artifacts and completion index remain, with no
evaluation or blind files. Below 80% valid similarity paths disables C. Track-listener missingness,
conflicts and values above exact double-integer precision make the popularity diagnostic unassessable;
artist-wide popularity remains unknown. No endpoint is added to fill those gaps.

On adequate inputs, comparisons report k=3/5 coverage/concentration, authentic track-listener
diagnostics and the predeclared individual/joint weight perturbations. IDF is deliberately the small
pilot corpus; genre labels/balance are `not verified`, and no labels are manufactured. Scores and
explanations must be finite/reconciled. A comparison hitting a later bound leaves the shape report
and a blocked completion index; no automatic rerun or budget extension follows.

The six founder profiles' deduplicated top-five unions form one blind pool (at most 120 entries).
`blind-review.json` exposes profile, cluster, opaque ID, artist and title only. A private mode-0600
`.blind-key.json` retains the concealed reproducible shuffle seed/mapping; no seed/formula/rank/score
or explanation is exposed in the review. Every available slot must have one rating (like=1,
neutral=0, dislike=-1); missing is not neutral. Sum/5 includes unfilled slots. Paired differences,
the +0.20 mean margin and 4/6 positive-profile count are directional founder evidence only.
No result promotes/tunes a formula. Pilot/final listening remain separate.

All real report artifacts carry **pilot, directional**. Frozen hashes bind analysis and the
blind pool; changed/duplicate/incomplete ratings and output overwrites are rejected.

## Verification

Executed with fabricated bytes/inventories only:

```text
dotnet test 'Liner Notes.sln' --no-restore --logger 'console;verbosity=minimal'
python3 -m unittest discover -s scripts -p 'test_phase*_*.py'
dotnet run --file scripts/phase4_replay.cs --no-restore --no-cache -- metadata
dotnet run --file scripts/phase4_replay.cs --no-restore --no-cache -- shape /tmp/liner-phase4-smoke-input.json
git diff --check
python3 scripts/checks/renewal_docs.py
```

| Project/check | Passed | Skipped |
|---|---:|---:|
| Domain.Tests, including existing benchmark/scorer checks | 77 | 0 |
| Application.Tests (8 added pilot scoring checks) | 77 | 0 |
| Infrastructure.Tests (13 added pilot replay/gate checks) | 68 | 0 |
| DataAccess.Tests | 25 | 1 |
| Presentation.Tests | 30 | 8 |
| **Full .NET suite** | **277** | **9** |
| Existing fake recorder checks | 21 | 0 |
| Added fake pilot operator/blind checks | 11 | 0 |

The nine existing PostgreSQL-dependent skips reflect the absence of the Phase 1 test database.
NU1510 remains for the existing XML cryptography reference. No dependency remediation was attempted.
The final configuration-resource change was rechecked with all 68 Infrastructure tests. The CLI
operator uses `--no-cache`: a final smoke check caught the file application's default cache
reusing an earlier referenced assembly after that change. Forced rebuilds validate the current protocol.

Independent arithmetic cases cover aggregate versus best-seed cosine, IDF square-root contributions,
all 12 overlapping tags, missing/invalid/profile-specific match evidence, known-track versus sibling
eligibility, conflicting/imprecise listeners and popularity-independent scores. Canonical ranked keys
and complete numeric breakdowns match across 100 seeded permutations under invariant, en-US,
tr-TR and az-Latn-AZ cultures. Replay checks cover exact 80% tag coverage, failed adequacy classes,
partial runs, receipt/hash tampering, held-out rejection, protocol changes and cancellation.
Operator checks cover temporary-copy accounting, inventory freshness, phase sealing, path/overwrite
and byte limits, durable shape-before-score ordering, blocked comparisons, complete concealed pools,
no reruns and final byte reconciliation. No existing test was removed or weakened.

Applicable skills: brainstorming (the repeated implementation request follows the presented scope)
and modern C# coding standards. The codebase-memory MCP index lacked current coverage for the
recorder/metrics; direct source inspection supplied that evidence. .NET CLI smoke checks use the
installed SDK 10.0.112. No remote state was mutated.

## Preparing the concrete run

Run these only as the appropriate gated step; commands below are a procedure, not authorization.
The preparation command creates local controls, not the recording output directory. A clean pinned
checkout is required, and existing control/run paths are never overwritten.

```bash
python3 scripts/phase4_pilot.py prepare \
  --run-id phase4-pilot-20261007-r1 \
  --expires-at 2026-11-06T18:00:00Z \
  --terms-date 2026-10-07 \
  --endpoint-reference docs/renewal/phase-4-evidence.md
```

Proposed recording directory:
`/home/mahammadjafarli/source/repos/Liner Notes/recordings/phase4-pilot-20261007-r1`.
Controls use the adjacent `phase4-pilot-20261007-r1-controls` directory. The draft contains the exact
eight reviewed founder seeds plus Agalloch, Pharoah Sanders and Altın Gün; 11 similarity calls,
top tags/tracks for at most 66 artists (143 requests), depth one, no retries/bursts, 1-second pacing,
4,000,000 estimated / 5,000,000 maximum response bytes, 100,000 per response, and a 6,000,000-byte
complete run cap. The expiry is a proposed run-specific decision, not adoption of general retention.

Before a proposal, reconcile all seven categories (`recordings`, `http_cache`, `derived_database`,
`reports`, `copies`, `backups`, `partials`) and every provider-data filesystem root, including controls
and the stable `.acquisition.lock`. Unknown external cache/database/backup totals cannot be set to zero.
The developer confirmed on 2026-10-07 that no Last.fm data is stored elsewhere. The concrete inventory
may use zero for external stores on that basis; local controls, temporary copies and the existing
coverage report are still counted. This confirmation does not approve live acquisition.
Use the inventory JSON contract in [Phase 2 evidence](phase-2-evidence.md#recorder-preflight-and-remaining-run-gates).
The inventory must be at most five minutes old and rechecked at start; delays require a freshly
reconciled inventory and a new proposed digest/approval, not changed bytes under an old approval.

Keep the inventory and proposed manifest in explicit temporary files outside their measured roots
to avoid a self-referential hash. Account both files' bytes separately in `copies` before proposal.
They stay local/untracked and are copied into the approved recording directory as applicable.
Recordings, reports, locks and protocols stay under the gitignored `recordings/` root.

```bash
python3 scripts/phase4_pilot.py propose \
  '/absolute/control/draft-manifest.json' \
  --inventory '/absolute/reconciled-inventory.json' \
  --developer-reference 'conversation reference for the proposed digest approval' \
  --output '/tmp/phase4-proposed-manifest.json'
```

`propose` prints the exact digest. Its candidate bytes use the recorder's executable format;
this **does not authorize execution**. Obtain explicit developer approval of those exact bytes,
including endpoints, literal seeds, request/byte bounds, output path, inventory and expiry.
The current draft is intentionally nonexecutable while the inventory/approval is absent.
Only after that approval, with the explicit digest and execution flag, may the separate recorder run.

After recording, refresh the inventory for offline report generation. `analyze` takes the retained
approved manifest/digest and refreshed inventory; `ratings` additionally takes the completed ratings
JSON. Both remain under the approved 6 MB run and strict 80 MB shared stop thresholds. The operator
also reserves 16 MB for serialized transient replay input/captured output. That reservation is not
a measurement of the .NET heap or proof of total resident/external storage capacity. Those remain
operator accounting obligations. Full real breakdown sizes/capacity are **not verified**.

```bash
python3 scripts/record_lastfm.py '/tmp/phase4-proposed-manifest.json' \
  --approved-sha256 APPROVED_DIGEST --execute-approved-run
python3 scripts/phase4_pilot.py analyze '/absolute/approved-run/manifest.json' \
  --approved-sha256 APPROVED_DIGEST --inventory '/absolute/refreshed-inventory.json'
python3 scripts/phase4_pilot.py ratings '/absolute/approved-run/manifest.json' \
  --approved-sha256 APPROVED_DIGEST --inventory '/absolute/refreshed-inventory.json' \
  --ratings '/absolute/completed-blind-ratings.json'
```

No automatic cleanup/retention extension, repeated listening, scoring retry or enlarged recording
is authorized. Retention expiry requires the operator's separately reviewed disposal action.

## Provider/documentation boundaries

- [Last.fm terms](https://www.last.fm/api/tos) and [API introduction](https://www.last.fm/api/intro),
  refreshed 2026-10-07: storage/caching/attribution conditions and discretionary rate limits.
  One request/second is conservative pacing, not a verified universal allowance. Research/academic
  applicability and territorial/publication questions remain unresolved; provider permission is not
  inferred from passing tests or a run-manifest approval.
- Current [similarity](https://www.last.fm/api/show/artist.getSimilar),
  [artist tags](https://www.last.fm/api/show/artist.getTopTags) and
  [top tracks](https://www.last.fm/api/show/artist.getTopTracks) references are recorded in
  [Phase 3 evidence](phase-3-evidence.md#authoritative-documentation). No new endpoint was added.
- [.NET file-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)
  confirms .NET 10 `#:project` and `dotnet run --file` support. The .NET 10.0.300 `#:include`
  feature was not used. SDK-generated utility cache is not a new tracked evaluation project.

Remaining Phase 4 work: explicit complete inventory and concrete manifest approval; actual acquisition;
real shape/size report; directional comparison if gates pass; developer blind ratings and confirmation.
Phase 4 is **not complete** and Phase 5 remains unapproved.
