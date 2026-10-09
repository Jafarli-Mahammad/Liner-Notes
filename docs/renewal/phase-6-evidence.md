# Phase 6 implementation and verification

The developer approved the written design and explicitly requested implementation on
2026-10-10. Implementation is locally verified on `Prism`; developer confirmation is pending.
**Mechanics only, no catalog conclusions.** Baseline A remains provisional and recommendation
quality remains **not verified**. Live acquisition, scheduling and email delivery are later work.

## Implemented flow

`GenerateDigestCommand(UserId, IsoWeek)` is a MediatR Application command. Both existing
composition roots register its dependencies. There is no new HTTP request endpoint or schedule.

1. Validate configuration/week, acquire a PostgreSQL session batch lease and check the existing
   canonical week. A retry returns the existing digest before provider/storage reads.
2. Materialize current signals and latest feedback in pure Domain code. Feedback uses descending
   `FeedbackGivenAt`, then descending ordinal recommendation ID. Merge MBID and artist/title
   aliases transitively, keep familiarity independently and rebuild current positive taste.
   Negative feedback excludes tracks; it never subtracts artist tags or excludes sibling tracks.
3. Read bounded recorded/synthetic provider evidence through the existing interfaces, parser and
   immutable replay client. A scoped tag cache shares seed and candidate evidence. Merge duplicate
   track aliases and retain discovery paths, gaps, positions, observations, hashes and timestamps.
4. Apply aggregate cosine with configured tag/novelty weights (defaults 1/0.2), zero popularity and
   feedback penalties, binary familiar-seed-artist novelty, ordinal track-key ties and one pick per
   artist. Short/empty lists remain valid. Limits stop rather than silently truncate input.
5. Reserve exact UTF-8 breakdown bytes plus an explicitly configured database-overhead bound.
   Under the write transaction, revalidate taste/artifact/database revisions, write selected tracks
   and digest together, verify actual `pg_column_size` and database growth, then commit. Failure or
   cancellation rolls back catalog and digest writes. Brief PostgreSQL table locks prevent account,
   feedback and catalog changes between the final revision check and commit.

The SQL model still matches the existing migration snapshot. No SQL tables, columns, indexes,
migrations or package versions changed. New data uses the existing ScoreBreakdown JSONB column.
The legacy scorer and its tests remain; composition registers baseline A for new generation.

## Stored score and export compatibility

Snapshots store formula/payload/materialization/normalization/ranking/configuration versions,
the actual weights and complete configuration policy/hash, candidate and aggregate taste vectors,
magnitudes, familiarity, every normalized overlap contribution, named components and complete
selected evidence. Derived effective feedback inputs carry `rec:` context referencing the source
recommendation; they are reconstructed materialization inputs rather than additional table writes.
Replay reads these stored inputs without current taste/provider access. Unknown fields are retained
at the breakdown and nested record levels. Legacy payloads remain legacy; unsupported versions
remain inspectable and fail explicit replay.

Export 2.0 includes complete snapshots and previously omitted scalar, ownership, normalization
and audit fields. Digest history uses pages of at most 100 ordered by creation timestamp and ID;
retained deleted account records are inspectable too. A 1,005-digest test verifies that the old
1,000 limit is gone and timestamp ties do not duplicate or omit records. Credentials remain
outside the export. These checks cover the approved Phase 6 boundary; Phase 8a still inventories
all account tables/columns and deletion/backups independently.

`AlreadyKnown` and numeric ratings coexist. A later rating preserves known status and its signal;
marking an already rated pick known preserves the earlier numeric rating. `None` without a rating
is an explicit reset for that recommendation. Categorical contradictions still reject Liked 1–3
and Disliked 7–10. Cross-digest likes/rejections are rebuilt from latest effective track feedback;
familiarity accumulates independently. No automatic history deletion was adopted.

## Local configuration and invocation

`Generation` binds `GenerationConfiguration`: `Origin` is `Recorded` by default or explicitly
`Synthetic`. Live/unknown origins are rejected. `LastFm:ApiKey` and `LastFm:Mode` cannot change
the generation provider into HTTP. Recorded input is supplied as explicit, hash-checked
`RecordedLastFmResponse` objects registered in DI; generation does not discover files. The caller
must validate the approved recording manifest and supply only its authorized, unexpired inputs.
Set `Generation:ApprovedRecordingManifestSha256` and `Generation:RecordingExpiresAtUtc` from
that manifest. Missing/expired metadata stops new generation; expiry is rechecked around persistence.
The input adapter deliberately supports injected immutable bytes rather than a new artifact loader.

The configuration carries tag/novelty and feedback weights plus these maximums: 20 artist seeds,
20 tag seeds, discovery limit 10, 400 candidates, 100,000 UTF-8 bytes/breakdown and five picks.
All actual values enter the stored configuration hash. Defaults are mechanical bounds, not provider
entitlements or demonstrated capacity. Limits can be reduced through configuration.

`GenerationStorage` binds `GenerationStorageOptions`. Supply absolute `InventoryPath` and
`LeasePath` outside measured artifact roots, an explicit positive `DatabaseOverheadBytesPerPick`,
and an `ArtifactRoots` dictionary with all six categories: `recordings`, `cache`, `reports`,
`copies`, `backups`, `partials`. Every category must be declared; an explicitly empty root array
represents a reconciled empty category. Only operator-approved roots are read; linked or overlapping
roots fail closed. The database is counted separately using the larger of whole database size and
uncompressed derived-row bytes, with a derived-data revision hash. Inventory categories hash file
contents, paths, lengths and timestamps. No process-local usage counter is trusted.

For an explicit local operator invocation, resolve `IDigestGenerationStore` from a fresh DI scope,
call `ReadStorageAsync`, then use `LocalGenerationStorage.ReconcileAsync` to write the reviewed
inventory. Generation never refreshes inventory automatically. Resolve `ISender` and send:

```csharp
var result = await sender.Send(new GenerateDigestCommand(userId, IsoWeek.Parse("2026-W41")), cancellationToken);
```

The result distinguishes `generated` (including empty digests), `existing` and `stopped`; it returns
nonpersisted coverage diagnostics and a stop reason. Cancellation propagates. Inventory must be
reconciled within five minutes and unchanged immediately before writing. New reservations stop at
projected usage **at or above 80,000,000 bytes**. Refresh inventory explicitly after a successful batch.
Account/feedback/export/deletion and existing-digest reads continue when generation is stopped.
The PostgreSQL lease coordinates generation across hosts sharing the database; the file lease
coordinates participating local writers. Operators must keep acquisition/artifact writes exclusive
during a batch. Production coordination, alerting and capacity assessment remain later work.

## Verification

Baseline before edits: 277 passed / 9 existing PostgreSQL skips. Final full suite with explicit
local PostgreSQL: **337 passed / 0 failed / 0 skipped**.

| Test assembly | Passed | Focus |
|---|---:|---|
| Domain | 103 | Legacy tests; all 16 benchmark tracks, ordinary cosine, complete sums, replay, empty/nonfinite inputs, ordinal eligibility, pure materialization |
| Application | 86 | Limits, empty diagnostics, retries without provider calls, origin isolation, recording expiry including final persistence |
| Infrastructure | 74 | Existing parser/replay; inventory boundaries, stale/unknown accounting, leases, revisions, symlinks, cancellation, offline composition |
| DataAccess | 36 | Existing repositories/migration; ten Phase 6 checks covering SQL model neutrality, atomicity, concurrency, JSON/export, feedback, deletion and storage stops |
| Presentation | 38 | Existing endpoint and local PostgreSQL account/security checks |

The ten Phase 6 database/model checks include nine PostgreSQL cases. Tests create uniquely named
disposable databases; no application database or live Last.fm input is used. The DataAccess test
project gained only an Infrastructure project reference for composition; no test package/project
was added and legacy tests were preserved. The export test contract changed to the approved 2.0
version and page interface. Documentation checks and the configured pre-commit hook also pass.
Rider MCP inspected generation, persistence and storage files: no compiler errors, with style and
EF/reflection-analysis suggestions only. Codebase-memory MCP supplied current architecture/symbol
navigation, checked against source. The existing NU1510 package-pruning warning remains.

### Measured synthetic selected-pick sizes

Two selected picks from the recorded synthetic-response test, PostgreSQL 16:

| Bytes | Median | p95 (nearest rank) | Maximum | Total |
|---|---:|---:|---:|---:|
| Full UTF-8 breakdown | 5,817.5 | 6,930 | 6,930 | 11,635 |
| `pg_column_size(ScoreBreakdown)` | 2,675.5 | 2,991 | 2,991 | 5,351 |

The test overhead bound is 1,000,000 bytes/pick solely to exercise atomic storage mechanics.
Production overhead remains unconfigured and generation fails closed until a reviewed bound is
supplied. Two fabricated picks cannot establish real-data size distributions, enrollment capacity,
personal fit, provider permission or production readiness. No tuning or formula promotion occurred.

### Reproduce locally

```bash
dotnet test 'Liner Notes.sln' --no-restore
python3 scripts/checks/renewal_docs.py
python3 scripts/checks/secret_defaults.py
```

For database checks, set `LINER_PHASE1_POSTGRES` to an explicitly local disposable PostgreSQL
administrator connection, then run the same suite. Test setup creates/drops its own databases.
Do not put real credentials in tracked settings or command history. Builds reuse existing packages;
the runtime is .NET 10 with existing EF/Npgsql 9.x references, not an implicit EF 10 upgrade.

Authoritative references refreshed on 2026-10-10:
[Last.fm terms](https://www.last.fm/api/tos),
[artist.getTopTags](https://www.last.fm/api/show/artist.getTopTags),
[System.Text.Json unknown-field preservation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/handle-overflow),
[EF transaction/retry guidance](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency),
[PostgreSQL advisory/table locks](https://www.postgresql.org/docs/current/explicit-locking.html).
Last.fm documents discretionary rate limits rather than a universal numeric quota. No live call,
external email, deployment, push or later-phase execution was performed.
