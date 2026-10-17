# Guest discovery evidence

Updated: 2026-10-17. Design and sequential execution plan are developer-approved. C1–C3, C4 lifecycle, C5 catalog, C6 taste/preparation and C7 job/admission mechanics are implemented and locally verified; the guest website feature and later stages are not yet implemented or developer-confirmed.

## C7: durable jobs and coordinated admission

Locally verified on 2026-10-17: idempotent owner-scoped job admission, cooldown and
queue backpressure, expiring leases, bounded retries, fencing after reclaim,
terminal payload clearing and deletion checks. Private selections and jobs share
the caller's admitted transaction. A failed reservation rolls both back.

The generated accounting migration backfills global/legacy-core counters and
installs row/TRUNCATE triggers. Normal admission reads counters plus schema
metadata instead of hashing every private row. Explicit reconciliation independently
checks totals against actual rows and fails without repairing mismatches. A short
coordinator reserves logical/physical growth, checks configured filesystem
headroom and atomically publishes committed inventory. No source I/O belongs in
its write callback. See [the storage contract](guest-discovery-storage.md).

Full verification passed 592 .NET tests with zero failures/skips: Domain 136,
Application 120, DataAccess 111, Presentation 65, Infrastructure 160. Required TRX,
secret-default, documentation and whitespace gates passed. After strengthening the
accepted-input rollback assertion and removing the unused unavailable-coordinator
placeholder, all eight coordination PostgreSQL tests passed again. Results:
`/tmp/liner-guest-c6-7_udsfmm/c7-complete-results` and
`c7-final-atomic-results`. Environment: SDK 10.0.401 and isolated PostgreSQL 18.6,
512 MiB/one CPU, existing IDE database untouched.

Evidence includes real backend termination with observed server rollback, fresh
scope/restart lease reclaim, revoked stale tokens, guest deletion during work,
unknown schema/external writes, corruption detection and TRUNCATE accounting.
Near-cutoff occupancy is injected while real PostgreSQL transactions compete;
the developer's actual disk is not filled. Filesystem-full/unavailable and failed
post-commit inventory publication are controlled faults. These checks prove guard
mechanics, not the later constrained-host capacity targets.

The first complete run exposed legacy weekly bookkeeping scope differences;
separate core/all-write counters restored its original guard boundary. One legacy
capture test also stopped at physical inventory drift on an intermediate run;
its isolated rerun and two subsequent complete runs passed. Diagnostic logging
retains size/revision-equality evidence without weakening post-fault assertions.
Each trigger now updates both counters in one atomic row update.

C8 must integrate the consumer, safe source-error policy, periodic inventory
maintenance, remaining legacy website writers, public preparation quotas/coalescing
and the weekly acquisition refactor before enabling the product flow. C9/C10,
real catalog acquisition, live quality, deployment and developer confirmation
remain outstanding. No live provider calls were made.


## C6: canonical album taste and bounded public preparation

Locally verified on 2026-10-17: canonical selection snapshots, five artist/album
plus five style bounds, atomic manual replacement preserving feedback, canonical
account-import deduplication, scoped release-group tags and configurable 0.35
credited-artist fallback. Pure materialization deduplicates repeated entity influence;
versioned scores retain the selected evidence and replay without a fabricated week.
Canonical ListenBrainz artist acquisition bypasses ambiguous name resolution.
Unsupported provider capabilities are explicit gaps, not exact-match claims.

Three dedicated caches admit conservative retained-byte estimates: Last.fm 16 MiB,
MetaBrainz 16 MiB, prepared public pools 32 MiB, aggregate 64 MiB by default.
Fixed synchronization stripes coalesce identical process-local preparation, with
45-second work deadlines and a 2 MiB serialized pool limit. Pools preserve response
provenance, hashes and normalization; actual source cache deadlines govern reuse.
No private taste/feedback state or restricted evaluation data is shared. The
ListenBrainz scoped response working set has a separate 32 MiB estimate bound.
These estimates are not measured RSS or a complete VPS memory budget.

Verification used pinned SDK 10.0.401 and a newly isolated PostgreSQL 18.6 container
(512 MiB, one CPU). All 579 .NET tests passed, zero failures/skips: Domain 134,
Application 120, DataAccess 103, Presentation 65, Infrastructure 157. Required-project
TRX validation, secret-default, documentation and whitespace checks passed.
Temporary final evidence: `/tmp/liner-guest-c6-7_udsfmm/c6-final-results` and
`c6-final-tests.log`; these are disposable local records, not durable release artifacts.
The resumed first run caught an export-version assertion and a fake-clock/cache-clock
mismatch. Final checks include real source-expiry invalidation and deterministic
prepared-evidence hashing that excludes transient domain object IDs.

The generated additive migration preserves legacy selections and guards destructive
downgrade once canonical data exists. Account export is now 2.4. Private historical
selection snapshots do not depend on public cache rows surviving eviction.

No live provider calls or real catalog acquisition were run. Durable prepared
publication, cross-process acquisition coalescing, integrated refresh scheduling,
C7/C8 race checks, web enablement, constrained-host measurements, developer
confirmation and real recommendation quality remain outstanding.


## C5: indexed local catalog and explicit lookup contracts

Implemented a versioned catalog over shared Artist/Album identities, separate release-group IDs, all artist-credit relationships, source/license/attribution metadata, aliases, bounded normalized terms and immutable per-version credit projections. Indexed prefix search and kind-specific GiST trigram fallback run only against PostgreSQL. Searches are capped at ten results per page, bounded offsets/candidates and a 250 ms statement timeout. Names in Turkish/Azerbaijani, accented/non-Latin scripts and punctuation-only names are supported; LIKE wildcards are escaped.

Import verifies bounded UTF-8 JSONL and content hashes before writes, stages no more than 250 records per admitted transaction, detaches imported EF entities between chunks and switches the active pointer only after completion. A restart verifies committed normalized-row fingerprints against the approved artifact. The reader also checks the bytes actually consumed, so neither a cursor nor an earlier hash alone authorizes publication. Unpublished records cannot displace valid active fuzzy matches. Interrupted or invalid work leaves the prior publication searchable. The draft schema was consolidated using EF CLI removal/regeneration before commit; only one new C5 migration remains.

The explicit artist/release-group lookup adapter reuses the allowlisted MetaBrainz client, shared pacing and source gates. It resolves album credits, excludes supplementary search-response tags and rejects recorded evaluation material as product data. Actual transport cache deadlines propagate; no-store, no-cache, private and expired responses grant no shared retention. This client does not log outbound query URLs, and Development hosting diagnostics no longer log inbound query strings. No real provider acquisition was performed.

Verification:

- Final five-project suite: **564 passed, zero failures/skips** (Domain 131, Application 116, DataAccess 102, Presentation 65, Infrastructure 150); strict result validation passed.
- PostgreSQL tests cover alias/Unicode/symbol/typo search, pagination/bounds/cancellation, multi-artist albums, atomic pointer publication, interrupted restart, unchanged last-good results and rejection of a mismatching staged row.
- HTTP tests prove public search does not allocate a profile, lookup status is owner-scoped and omits raw query payloads, and submission cannot create unprocessable jobs.
- Parser/client tests cover bounded lines, consumed-byte hashes, scope/license requirements, malformed IDs, recorded-data rejection, fixed endpoint/literal query construction and source cache restrictions. Ordinary tests use fixtures or stub HTTP, never live providers.
- A fixture initially counted its injected failure threshold from an earlier import; corrected to fail after the intended two subsequent writes. The production cursor/integrity checks remained intact.
- Secret/default, document and whitespace checks passed. Existing package warnings remain unchanged. C1's SQL-only timing is not an API/VPS capacity measurement; C10 still measures the complete schema/path.

Public writes still fail closed through the unfinished C7 admission dependency; lookup submission remains disabled until its C8 consumer. End-to-end shared lookup caching, actual quota races, album materialization, generation/UI and capacity evidence remain. No retained database migration, real catalog breadth, developer confirmation or production readiness is claimed.

## C4: guest lifecycle and account preservation

Implemented hashed guest credentials, protected cookie mutations, deliberate profile creation, no-allocation bootstrap, seven-day inactive expiry, hourly activity write throttling, bounded cleanup, private export/clear, same-profile registration attachment and explicit existing-account import. Operational credential hashes and job lease tokens are excluded from exports. Authenticated account ownership takes precedence over an unrelated guest cookie.

Import preserves history IDs/score JSON/feedback, deterministically keeps the account's duplicate manual seeds, namespaces imported request keys, cancels unfinished jobs for both profiles and increments the target revision. Constraints are deferred only within the atomic transfer. New-account attachment removes the guest credential and revokes its cookie after commit.

Users.WeeklyEmailOptIn defaults false, including existing accounts without recorded opt-in. Weekly scheduling additionally requires verified Identity email and no unsubscribe. Onsite use does not require verification. Existing eligible scheduler fixtures explicitly set consent and verification; a new boundary test excludes guests, unconsented and unverified users. No email transport changes were made.

Verification:

- Five-project verification: **541 passed, zero failures/skips** (Domain 124, Application 116, DataAccess 97, Presentation 64, Infrastructure 140). Four project results came from the final solution run; DataAccess was rerun after correcting a missing test-only namespace import. The final result directory contains one passing result per project and passes the strict results checker.
- PostgreSQL tests cover expiry before cleanup, cleanup bounds, hash exclusion, no guest subscriber, same-profile attachment, revoked credentials, request-key collision handling, seed deduplication and imported score/feedback preservation.
- HTTP tests use real antiforgery services/cookies. They reject missing/forged tokens, wrong origin, missing/forged credentials; preserve progress on registration; and verify cookie properties. Unit checks cover production HTTPS requirements and simultaneous account/guest identity precedence.
- An initial HTTP fixture used DataContext for migrations, which has no migration set; it now uses the repository's AppDbContext migration root. This was a fixture defect, not evidence that production boot provisions the schema.
- Repeated existing capture-fixture stops were traced to database_inventory_changed during initial generation setup. Bounded explicit reconciliation now requires unchanged managed-row revision, no digest and no email artifact. No production guard or post-publication/fault assertion was weakened. Subsequent DataAccess suite passed.
- Secret/default, renewal-document, results and whitespace checks passed. Existing package warnings remain.

Guest routes still default disabled, and C4's default write admission fails closed. Tests use an explicitly named transactional mechanics admission. C7 must install coordinated accounting; C8 must prove in-flight generation/deletion races and enable consumers before users can submit work. C5–C10 remain. No retained database migration, real-source acquisition, developer confirmation or deployment readiness is claimed.

## C3: profile ownership and registered compatibility

Implemented mandatory private profile ownership for taste and recommendations, batch ownership for recommendations, and owner-consistent weekly links. Legacy account/weekly recommendation references remain nullable for compatibility. Registered accounts create their profile together with the domain subscriber. Legacy taste writers and weekly generation resolve the account's actual profile, including profiles whose IDs differ from the account ID. Export includes every profile-owned recommendation, including non-weekly history; account deletion removes that history without deleting shared tracks.

Historical score JSON is retained in place. `discovery-score-v2` snapshots identify a batch and have no calendar week; historical `phase6-score-v1` replay remains supported. The ranking formula and benchmark expectations are unchanged.

Verification against the isolated C1 PostgreSQL container:

- Full suite: **529 passed, zero failures/skips** (Domain 124, Application 116, DataAccess 93, Presentation 56, Infrastructure 140).
- After the final generation-input adapter adjustment: Application 116 and PostgreSQL generation/migration 17 checks passed. These are additional checks, not additional unique tests in the full-suite count.
- Populated legacy migration and down/reapply preserve original recommendation IDs, unknown score fields and feedback. New constraints reject cross-owner batch links. Independent batches coexist without weekly delivery identity.
- An attached-profile fixture with a profile ID different from its account ID exercises seed adapters, recommendation access, account export and deletion. Guest rows with null legacy account references remain private until attachment and are removed by account deletion afterward.
- Three initial in-memory repository fixtures lacked the owning account/profile. Fixtures now include it; production ownership checks were retained. The final suite passed.
- Secret/default, renewal-document and whitespace checks passed. Existing package advisory warnings remain unchanged.

No retained database was migrated. Old writers must be stopped for this schema cutover. Guest HTTP access and new managed writers remain disabled until their lifecycle and storage prerequisites are implemented. C4–C10 remain; no developer confirmation or deployment capacity is claimed.

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

## C8 shared-cache and history checkpoint (2026-10-17)

The full offline suite passed 595 checks with zero failures or skips:
Domain 136, Application 120, DataAccess 112, Presentation 65, Infrastructure 162.
Evidence is in the private temporary `c8-cache-verified-results` TRX directory.
PostgreSQL checks cover shared acquisition lease fencing, exact payload checksums
and expiry. Codec checks preserve public evidence without transient track IDs.

Public cache entries are bounded by bytes, count and expiry; they contain no
profile identity. Compact private recommendation aliases survive removal of
expensive score snapshots and are included in export 2.5. The migration verifies
and rebases storage accounting around schema changes and backfills aliases.
The background consumer, cache adapter, endpoints and website are still pending.
These checks establish local mechanics only; no real acquisition was performed.

### C8 persistent preparation adapter

A subsequent full offline run passed 600 checks with zero failures or skips
(Domain 136, Application 121, DataAccess 112, Presentation 65, Infrastructure 166).
Prepared-only pool/seed misses perform neither acquisition nor writes; durable
pools and seeds serve after memory restart. Expired evidence is rejected and
non-retainable responses are returned only to their current acquisition. Provider
resolution runs outside managed admission. Local catalog album tags can
materialize taste without provider fallback. This adapter is wired in DI; the
job consumer and onsite routes are still pending. Private TRX directory:
`c8-adapter-verified-results`.
