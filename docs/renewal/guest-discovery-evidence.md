# Guest discovery evidence

Updated: 2026-10-16. Design and sequential execution plan are developer-approved. C1–C2 are implemented and locally verified; the guest website feature and later stages are not yet implemented or developer-confirmed.

## C2: additive profile, batch and job foundation

Implemented listener ownership independent of registration, non-calendar batch identity, bounded pending-job records, schema mappings/constraints and an additive migration. The migration backfills profiles for existing subscribers and batches for existing digests, preserving IDs and leaving taste/recommendation rows and stored score JSON in place. Existing writers can still operate during this additive stage; C3 must cover records created between migrations.

Export 2.3 includes owned profile/batch/job metadata and the new digest batch link. JSON job payload/results remain structured. Operational lease credentials are excluded. The explicit relational inventory covers every new column; populated tests compare stored values with exported values and verify that account deletion removes owned metadata while preserving other accounts and shared tracks. See [privacy inventory](guest-discovery-privacy.md).

Verification:

- Five new Domain behavior tests cover expiry, throttled activity, safe attachment, revision conflicts, separate same-day batches and bounded job inputs.
- Four PostgreSQL tests cover populated legacy backfill, exact unknown score JSON/feedback preservation, schema constraints/owner isolation, full metadata export, account-deletion cascades, and additive down/reapply recovery.
- Application export coverage now propagates discovery-reader failures; existing lifecycle expectations correctly assert version 2.3.
- Final full .NET gate: **526 passed across all five projects, zero failures/skips**, validated by the existing TRX validator.
- The generated idempotent C2 SQL was inspected: new tables/indexes, a nullable digest link and metadata backfill; no existing table/column or historical score data is dropped by the upgrade.
- An initial full run found three stale version assertions and one existing capture fixture's fail-closed inventory-change race at its publication barrier. Version expectations were updated without removing content checks. The capture safety assertion was left intact; its focused run and the final full gate passed. This is not evidence of production capture/storage behavior.

No retained application database was migrated. Guest sessions, general ownership cutover, job execution, catalog endpoints and the discovery UI remain for C3–C10. Existing package advisories recorded under C1 remain unresolved.

## C1: offline source/resource feasibility

Implemented:

- `scripts/catalog/catalog_probe.py`: deterministic synthetic generation, bounded streaming validation with disk-backed identity/reference checks, atomic non-overwriting output, prepared-evidence size sampling, and isolated PostgreSQL import/query measurements.
- `scripts/catalog/test_catalog_probe.py`: parser, resource limits, identity conflicts/references, output preservation, SQL COPY/schema boundaries, provenance and dedicated-loopback database checks.
- [Source policy](guest-discovery-sources.md): verified metadata/tag distinction, release-group capability, provider limits and unresolved actual acquisition/use gates.

No product HTTP endpoints, identity/schema migration, provider defaults or package versions changed in C1. No live artist/album/candidate data was acquired. Synthetic probes establish mechanics and cost on the recorded environment only.

### Baseline

Using `/home/mahammadjafarli/.dotnet/dotnet` selected the repository's SDK 10.0.401; the system `dotnet` did not contain that SDK. Locked restore and baseline build passed. All five PostgreSQL-backed test projects ran: **516 passed, zero failures/skips**, validated by `scripts/checks/phase9_test_results.py`.

The build emitted existing NuGet warnings. These are not a clean security bill of health:

- `System.Security.Cryptography.Pkcs` 10.0.12: [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) (moderate) and [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) (high).
- `System.Security.Cryptography.Xml` 10.0.12: [GHSA-7hqp-7c5m-4vq6](https://github.com/advisories/GHSA-7hqp-7c5m-4vq6), [GHSA-c399-9853-6jmj](https://github.com/advisories/GHSA-c399-9853-6jmj), and [GHSA-r955-8q5j-3h83](https://github.com/advisories/GHSA-r955-8q5j-3h83) (high).
- NU1510 package-pruning warnings also existed. Warning lines repeat across project/build summaries.

These advisories were verified against their vendor advisory records. An exploitable application path was not established by C1. Dependency remediation was not performed or claimed; no dependency was removed. Record this as an existing release concern rather than silently changing the approved feature scope.

### Recorded environment and method

- Linux x86_64 development workstation, Intel Core i5-14500HX, local NVMe-backed Docker data. This is not the proposed Xeon VPS.
- Dedicated PostgreSQL 18.6 Alpine container, repository-pinned image digest `77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873`, one CPU quota, 512 MiB memory limit, 64 MiB shared buffers, 4 MiB work memory, 32 MiB maintenance work memory.
- Separate loopback port and private temporary credentials. The existing IDE database was not modified. Each probe created and dropped only its random schema in a dedicated probe database.
- Corpus: two albums per synthetic artist, two tag assignments per entity, two artist aliases and one album alias. Script-generated names cover ASCII, accents, Turkish/Azerbaijani folding, Cyrillic and CJK. UUIDs and normalized artifact hashes are deterministic; index size and timing can vary between runs.
- B-tree `(kind, term, entity_id)` and GiST trigram indexes with 32-byte signatures. Prefix queries and a bounded nearest-neighbor typo prototype return at most ten distinct IDs from at most eighty candidate terms. These are SQL prototypes, not the final website search endpoint/relevance policy.
- Nine sequential EXPLAIN ANALYZE executions per case per corpus; p95 is the nearest-rank maximum of nine observations. Times are PostgreSQL execution times, excluding process startup, connection, API/network, and browser work. This is not a concurrent load test.
- Temporary probe artifacts were placed on this workstation's `/tmp` tmpfs; SQL data was in the container's local Docker storage. Normalization/spooling latency is not a VPS-disk measurement.

### Measurements

Sizes are bytes. The total includes all four probe tables, their indexes and related PostgreSQL storage; database-wide system tables/extension/WAL are not included in the per-schema totals.

| Artists / albums | Search terms / tag assignments | Normalized artifact | SQLite validation scratch | Tables | Indexes | Schema total | COPY/index/analyze seconds |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 / 2,000 | 7,000 / 6,000 | 758,961 | 671,744 | 1,753,088 | 1,605,632 | 3,358,720 | 0.573 |
| 10,000 / 20,000 | 70,000 / 60,000 | 7,588,836 | 6,651,904 | 13,484,032 | 15,417,344 | 28,901,376 | 2.989 |
| 50,000 / 100,000 | 350,000 / 300,000 | 37,943,836 | 33,280,000 | 64,602,112 | 77,758,464 | 142,360,576 | 14.340 |

Largest corpus: about **135.8 MiB** per-schema database storage. Its SQL spool was 56,357,343 bytes and streaming validation took 8.096 seconds. Artifact + scratch + SQL spool + imported database reached about 257.4 MiB by simple addition; this is an illustrative overlap sum, not a measured peak free-space reservation and excludes WAL, page/cache duplication and other host data.

| SQL query case | 3,000 entities p95 ms | 30,000 entities p95 ms | 150,000 entities p95 ms |
| --- | ---: | ---: | ---: |
| Short prefix | 0.413 | 0.344 | 0.422 |
| Common prefix | 0.358 | 0.409 | 0.428 |
| Artist alias | 0.403 | 0.488 | 0.410 |
| Accent-folded prefix | 0.650 | 0.325 | 0.485 |
| Cyrillic prefix | 0.493 | 0.445 | 0.396 |
| CJK prefix | 0.663 | 0.350 | 0.480 |
| Album prefix | 0.292 | 0.314 | 0.523 |
| Typo/nearest-neighbor prototype | 9.855 | 20.425 | 84.003 |

Queries returned ten IDs in these synthetic cases. Plans used B-tree index-only scans or indexed bitmap paths for prefixes and GiST/incremental sort for the typo case. The typo case already spends much of a 100 ms API budget on the SQL alone on this CPU. Final search must favor bounded prefix/token matches, apply an appropriate typo query threshold/deadline and validate actual relevance and API latency. Do not infer that the API p95 target has passed.

The probe process's `/proc/self/status` high-water RSS was 23,228 KiB for the first corpus and at most 25,028 KiB for the later corpora in the same process. Disk-backed validation grew while measured process RSS remained small. `getrusage().ru_maxrss` incorrectly suggested 506,692 KiB for this executable because usage accounting can survive exec; [Linux documents that preservation](https://man7.org/linux/man-pages/man2/getrusage.2.html#NOTES). Both counters are retained in raw reports, with the process-specific values used here.

One post-probe Docker working-set sample was 89.83 MiB. Container `memory.peak` reached its 512 MiB cap across the combined baseline/probe run; there were 369 memory-max events but **zero OOM/OOM-kill events**. Peak includes reclaimable cache and baseline tests, so it is not attributed to the catalog alone. Whole-app-plus-database peak memory and sustained-load behavior remain unmeasured.

Prepared evidence cost sample: 16 synthetic seeds, ten candidates per seed, two tags per candidate; serialized bundle minimum/median/maximum **5,307 / 5,362 / 5,439 bytes**. This is an illustrative shared bundle, excluding table/index overhead, larger real source payloads, multiple discovery paths and personalized score snapshots. It does not establish how many real seeds fit the cache.

### Verification

The following actually ran:

- Locked restore, build and all five baseline .NET projects: 516 passing, zero skipped/failed.
- `python3 -W error::ResourceWarning -m unittest discover -s scripts/catalog -p 'test_*.py'`: 15 passing tests, including malformed/deep/nonfinite JSON, conflicting IDs, missing artist references, byte/entity limits, output preservation, SQL boundaries and unsafe database targets.
- Three isolated PostgreSQL import/query runs at the corpus sizes above, with cleanup of their schemas.
- Existing offline suites: 32 phase-tool tests and four Phase 9 helper tests passed. Secret-default, renewal-document and whitespace checks passed.
- Source documentation review without live catalog acquisition.

### Decisions for subsequent stages

1. Keep the approved PostgreSQL-based local catalog and 256 MiB initial working-catalog target; the synthetic result supports trying it, not a promised real row count. Actual real-data import remains byte bounded and manifest driven.
2. Keep prefix/token search as the fast path and typo matching bounded. Measure the implemented API under concurrent load in C10.
3. Continue with the approved additive profile/batch migration using synthetic verification. Source permissions/real catalog coverage remain separate gates; they do not require blocking owner-model work.
4. Keep the mandatory storage guard and reconcile all new classes. Do not enlarge the temporary shared cutoff or adopt an unmeasured whole-host budget based on per-schema results.
5. No maximum user count is introduced. The above is data-size evidence, not a registration limit or a claim of supporting 50 simultaneous generations.

## Reproduce the offline probe

Use a new private working directory on the intended storage medium. The script refuses existing output files. Synthetic artifacts can be generated and validated without PostgreSQL or outbound HTTP:

```bash
python3 scripts/catalog/catalog_probe.py generate --artists 50000 --albums-per-artist 2 --output /tmp/catalog-synthetic.jsonl
python3 scripts/catalog/catalog_probe.py validate --input /tmp/catalog-synthetic.jsonl --output /tmp/catalog-normalized.jsonl
python3 scripts/catalog/catalog_probe.py evidence --input /tmp/catalog-normalized.jsonl
```

For SQL measurements, create an isolated database with the required prefix and supply libpq `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER` and private credentials through the environment. `PGHOST` must be an explicit loopback IP, and `PGDATABASE` must start `liner_catalog_probe_`. The script refuses an ordinary application database. It installs `pg_trgm` if absent in that dedicated database and cleans up its own random schema; only the disposable database may be used.

```bash
python3 scripts/catalog/catalog_probe.py postgres --input /tmp/catalog-normalized.jsonl --repeats 9
```

The default input/output ceilings are 512 MiB, one million unique entities and 32 KiB per line. PostgreSQL probe admission conservatively checks four times the normalized artifact size against its configurable schema budget and checks actual schema bytes after import. This is an isolated probe bound, not a promise that peak import/WAL growth cannot overshoot that estimate; the production importer requires real reservations.

Do not commit actual recordings, private environment files or raw operator reports. Follow the existing acquisition/evaluation boundaries for any future real artifact. Later stages must add their own evidence here instead of presenting C1 as a working guest recommendation feature.
