# Phase 3 verification: honest ingestion in memory

Verified locally on 2026-10-07, branch `Prism`, starting HEAD `f8c4f69` with the
previous Phase 2 changes still uncommitted. The developer requested Phase 3 execution.
Implementation verification is complete; developer confirmation remains pending.
**Mechanics only, no catalog conclusions.**

## Schema preflight

- Read `AppDbContext`, entity configurations, migrations and the model snapshot;
  searched DataAccess for `RawCandidateTrack`, `CandidateTrack` and the discovery/
  hydration interfaces. None is mapped. Presentation/Worker have no consumers of
  these interfaces. No persistence is needed for the Phase 3 contract.
- Changed transient Application interfaces/models and Infrastructure implementations.
  Domain, DataAccess, Presentation and Worker have no Phase 3 source changes.
  No DbSet, entity mapping, migration, schema, public endpoint or export changed.
- Introduced `HydratedCandidateTrack` as an Application ingestion snapshot, separate
  from the legacy scorer's non-nullable `CandidateTrack`. No inferred numeric value
  is supplied to that scorer. Domain scorer, parameters and persisted typed
  `ScoreBreakdown` remain unchanged; later pipeline conversion requires review.
- No project or package was added. The Application.Tests project-file change visible
  in the workspace belongs to Phase 2's approved fixture source link.

## Implemented contract

| Area | Verified behavior |
|---|---|
| Missing/invalid evidence | Nullable parsed match/count plus original text and reason. Matches must be finite and 0–1; counts are nonnegative 64-bit integers. Missing, malformed, overflow, nonfinite and out-of-range values never become default or clamped values. |
| Response provenance | Immutable list envelopes carry provider, method, seed, origin, exact body SHA-256, retrieval time and gaps. Cached envelopes retain their original metadata. No complete received response means null hash/time. |
| Discovery | Canonical track deduplication retains every seed/path, including repeated seeds and duplicate response items, match evidence, track listeners, URLs, raw rank and original similarity/track positions. Malformed skipped items do not renumber later observations. No multi-path match aggregation formula is adopted. |
| Hydration | Retains supplied tag observations, original positions, scope, raw count and exclusion reasons. Existing stoplist is unchanged. Valid positive descriptive counts use `max-observed-descriptive-count-v1`, without a minimum floor. Duplicate names use maximum normalized weight while all observations remain. Empty evidence yields an empty vector and gap. |
| Popularity | Artist-wide popularity is null with `artist_listener_measure_not_supplied`. Original track listeners remain attached to each path. Hydration never fetches top tracks to invent an artist listener estimate. |
| Fixtures/failures | FixtureOnly is explicitly synthetic. Unknown fixture artists/tags yield empty results and gaps. Hybrid remains a configuration spelling, but HTTP errors, missing credentials and transport failures yield live-origin gaps without fixture fallback. |
| Origin isolation | Discovery validates endpoint/seed provenance and rejects mixed origins; raw paths and hydration reject mixing live, recorded and synthetic observations. |
| Recorded replay | Caller injects immutable byte snapshots; constructor verifies hashes, copies bytes and retains original time/request limit. Repeated responses are consumed in supplied order; missing, exhausted or insufficient-limit observations yield explicit gaps. No HTTP, file discovery, credentials or current clock. |
| HTTP/cache | Bounds each complete response at 100,000 bytes; incomplete oversized data has no claimed complete hash. Cancellation propagates. Cache is isolated by mode, provider address, credential fingerprint, canonical request and limit. Only supplied HTTP freshness permits caching; no-store/no-cache and stale responses are not reused. No default retention duration is adopted. |
| Logging | Client/source/hydrator use constant messages and never log an exception or request URI. A secret-bearing transport exception regression confirms no secret reaches the custom client logger. |

Fixtures hash their exact synthetic JSON payloads and use an explicitly declared
Unix epoch timestamp. This is synthetic provenance, not an upstream retrieval claim.
The recorded-origin tests use fabricated bytes only; no real recorded content was
loaded, discovered or inspected, including held-out content.

## Red baseline and green verification

Four regression cases were first executed against the original implementation.
All four failed before the fixes and pass afterward:

| Regression | Original failure | Current assertion |
|---|---|---|
| Unknown fixture seeds | Invented related artists/tracks | Empty data; explicit gaps covered by the additional evidence tests |
| HTTP 429 in Hybrid | Synthetic substitution | Empty live-origin rate-limit gap |
| Missing tag/popularity | Generic `indie` tag and listener estimate | Empty vector, null artist popularity, no top-track hydration call |
| Missing match | Default 0.5 | Null match with retained missing reason |

Existing hybrid-fallback and artist-popularity assertions were updated to the
explicit Phase 3 contract and strengthened with provenance/scope assertions.
No test was removed. Legacy founder coverage remains Phase 2's offline isolation
check. Additional evidence cases cover invalid numbers, exact large integer counts,
hash tampering, copied payloads, replay exhaustion/limits, all repeated paths,
attribution, missing artist identity, tag exclusions, mixed origins, credentials,
malformed responses, original positions, cache freshness, size limits, cancellation,
and stable canonical output/tag weights under seed order and culture changes.

Commands executed:

```text
dotnet test tests/Infrastructure.Tests/Infrastructure.Tests.csproj --no-restore --logger 'console;verbosity=minimal'
dotnet test 'Liner Notes.sln' --no-restore --logger 'console;verbosity=minimal'
git diff --check
python3 scripts/checks/renewal_docs.py
```

Final full solution results:

| Project | Passed | Skipped |
|---|---:|---:|
| Domain.Tests, including existing benchmark/scorer checks | 77 | 0 |
| Application.Tests, including Phase 2 harness | 69 | 0 |
| Infrastructure.Tests, including 40 added Phase 3 cases | 55 | 0 |
| DataAccess.Tests | 25 | 1 |
| Presentation.Tests | 30 | 8 |
| **Total** | **256** | **9** |

The nine existing PostgreSQL-dependent tests were skipped because no Phase 1 test
database was configured. They are not new Phase 3 skips. The existing NU1510 warning
for `System.Security.Cryptography.Xml` remains; no dependency change was made.
Phase 2's 21 fake-recorder checks were verified in its earlier run; recorder files
were not changed in Phase 3. Documentation check and diff whitespace check pass.

## Authoritative documentation

Refreshed 2026-10-07 before coding against response fields:

- [Last.fm terms](https://www.last.fm/api/tos): cache/storage and attribution
  requirements remain relevant; no universal numeric request allowance is adopted.
  Territory, publication, redistribution and email-attribution questions remain open.
- [artist.getSimilar](https://www.last.fm/api/show/artist.getSimilar): match 0–1.
- [artist.getTopTags](https://www.last.fm/api/show/artist.getTopTags): artist-wide
  tags/counts; no documented `limit` request parameter, so the client bounds locally.
- [artist.getTopTracks](https://www.last.fm/api/show/artist.getTopTracks): track
  listeners and popularity-ordered results; these are not artist-wide listeners.
- [tag.getTopTracks](https://www.last.fm/api/show/tag.getTopTracks): track/artist
  attribution is supplied by the response; missing identity is a gap.
- [.NET 9 query redaction](https://learn.microsoft.com/en-us/dotnet/core/compatibility/networking/9.0/query-redaction-logs):
  existing Microsoft.Extensions.Http 9.0.2 uses the default factory query redaction.
  Operator opt-outs/custom HTTP logging were not exercised by the local logger test;
  do not infer safety for every operator logging configuration from that test.

Only documentation pages were browsed. No live Last.fm API call, real recording,
email, provisioning, deployment or database migration was performed.

## Remaining boundaries

- This is an ingestion contract, not an operational recommendation pipeline.
  No scorer formula, feedback policy, public explanation or persistence changed.
- The existing two-track acquisition bound and upstream order remain. Their
  popularity-ordered candidate pool is documented, not adopted as a V1 scoring
  preference or a new representative-track policy.
- Replay does not automatically load a recorder directory, approve a run manifest,
  unseal held-out data or prove real catalog coverage. Those remain Phase 4/5 gates.
- Production coordination, acquisition storage guards, retention, delivery and
  live acquisition scope remain later-phase decisions. A local rate limiter does
  not establish provider entitlement or complete operator safety.
- Applicable skills: brainstorming and modern C# coding standards. MCP code-index
  lookup had no coverage for the transient type; direct source inspection supplied
  the preflight evidence. No external state was mutated.
- Work remains local and uncommitted. The pre-existing change to
  `docs/coverage-spike-founder-seeds.md` was preserved. `MEMORY.md` was not edited.
