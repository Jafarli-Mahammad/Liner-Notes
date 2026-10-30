# Storage and authentication coordination redesign

Implementation and local verification record, 2026-10-30. Developer approval covers the architecture changes. Production deployment and developer confirmation are still outstanding. The authorized storage/authentication implementation is locally verified below; production performance and developer confirmation are not established.

## Problems addressed

| Root cause | Change and evidence | Measured effect / limit |
| --- | --- | --- |
| Equivalent full artifact reads repeated within one operation | Initial SHA-256 plus live operation-local kernel proofs; replacement/break/fallback tests | 900 MB single-file component reads 0.9 GB rather than 4.5 GB; fallback keeps all reads |
| Initial scans and password CPU occupied broad admission | Bounded preflight and standard Identity preverification, fresh locked security state | 900 MB login gate hold p50 about 3,065 → 32 ms; paused password verification permits an unrelated admitted write |
| Row-level accounting multiplied trigger work | Exact statement transition tables and complete ledger/witness verification | Correctness/rollback verified; new witness work makes empty-storage sequential cases slower overall |
| Database commit, filesystem rename and restart were separate unjournaled boundaries | Durable reservation/staging/publication intent, versions, flushes and recovery | Four actual process-kill publication cases recover without blind reconciliation; no speed claim |
| Implicit Data Protection writes bypassed coordinated accounting | Standard XML repository joins admission; recursive key creation fails promptly | Actual creation/rotation, old-token decryption and guarded direct-write tests pass |
| Per-copy cleanup repeated integrity admissions | Bounded owned-journal deletion batches with durable removal intent | Twelve-copy test uses three admissions and retains the other owner's authenticated copy; legacy scans remain |
| Unchanged ledger could coexist with growing physical PostgreSQL files | Refresh the physical charge and recheck quota under the unchanged witness | Regression red/green; final 256/256 benchmark operations succeed versus 253/256 baseline |

## Architecture selected

PostgreSQL remains the accounting authority. A singleton commit witness binds the exact managed-row ledger revision, logical charge, row count, schema fingerprint and artifact-category digests to each admitted transaction. Artifact operation rows record reservations and recoverable filesystem transitions. The filesystem inventory is still published durably, but can be rebuilt from the current committed witness after a crash; startup never adopts an unexplained filesystem change.

Ordinary writes first verify the managed namespace outside the global database gate. Under admission they rebase against the latest committed witness, repeat the required checkpoints, allocate capacity, perform the transaction, update the witness last, and durably publish the inventory. A PostgreSQL deferred constraint rejects initialized-ledger mutations that lack a matching final witness. The global capacity boundary remains serialized: removing it would permit oversubscription. Expensive initial hashing and password verification no longer need to occupy that boundary.

A full SHA-256 read is always required initially. An operation may reuse that digest only while a live Linux read lease prevents content writes and repeated no-follow descriptor/path identity checks prove it still refers to the same file. A break request invalidates the proof permanently. This optimization is restricted to supported local filesystems, files at least 4 MiB, and at most 128 files per operation. Unsupported or unavailable leases, unknown/remote filesystems, and small files retain full reads at every checkpoint. Timestamps, sizes, filesystem notifications and saved digests are never substitutes for content verification. No filesystem feature such as fs-verity is mandatory.

Work is bounded: four active storage preflights and 32 queued requests, two hashing lanes, two artifact workers with 16 queued, 64 active publication reservations, and fixed striped artifact-ownership locks. Bounded account cleanup processes known published files in batches of at most 64. Namespace publication has its own short ownership boundary. Cooperative multi-process writers must share the PostgreSQL database and the same local managed filesystem/lock inodes. This is not support for unrelated hosts with separate volumes or network filesystems.

## Integrity and accounting contracts

- Every managed row, including Identity, Data Protection keys, the witness and publication journal, uses the existing exact accounting formula: UTF-8 bytes of PostgreSQL `to_jsonb(row)::text`, plus 512 bytes per row. Statement transition-table triggers preserve the previous row formula and per-row revision increments; rollback rolls them back too.
- This is exact **policy accounting**, not a claim to predict PostgreSQL pages, WAL, indexes or free disk exactly. Admission also observes physical database size and filesystem headroom. Pending filesystem capacity stays charged until publication completion or verified removal.
- Reservations are committed before the first staging byte. Staged content stays within its reservation and is not charged twice. Published content transfers into the artifact-category charge in the same transaction as business completion. A deletion retains its previous byte/digest contribution until the verified filesystem removal and final transaction complete.
- Each inventory checkpoint still enumerates the namespace and checks lengths, names, root bindings and category digests. Initial full reads and lease-breaking/replacement checks detect same-size and restored-time changes. Unknown additions, missing files, invalid content, symlinks and nonregular files stop admission. Ordinary startup/recovery never reconciles such changes automatically.
- An external writer is not prevented permanently. A Linux lease may briefly block its open, then invalidates the operation and releases the lease. Full-read fallback retains the established checkpoint semantics; it does not turn hashing into continuous protection against arbitrary privileged writes after a checkpoint.
- Native filesystem operations pin no-follow directory components, reject nonregular files, use private staging files, flush data and directories, use no-replace artifact publication, and recheck parent identity. Metadata publication checks the current database witness on both sides of replacement. Unknown temporary files are never silently adopted or deleted.
- Changes to managed-table coverage, trigger definitions or deferred witness coverage alter the schema fingerprint or fail verification. An initialized protocol cannot silently run through a legacy configuration or an unaware direct database writer.

## Publication and crash recovery

A database commit and a filesystem rename are not one atomic transaction. The journal provides the recovery bridge.

| Durable state / interruption | Accounted state | Recovery action |
| --- | --- | --- |
| Before reservation commits | No owned file may exist | SQL rollback; nothing to recover |
| Reserved, no/partial staging file | Full reservation remains charged | Obtain operation ownership; remove only its bounded regular staging file; abandon reservation |
| Data flushed but Prepared not committed | Still Reserved | Same cleanup; never infer a ready artifact from bytes alone |
| Prepared | Full reservation, exact SHA-256/length/mtime recorded | Preserve verified content for business replay; owner deletion may cancel it |
| Publishing, before rename | Full reservation | Verify recorded staging content; replay no-replace rename and directory fsync |
| Publishing, after rename | Full reservation, final file recognized only by this durable intent | Verify final bytes; replay business completion with fresh eligibility/ownership checks |
| Published and business transaction committed | Artifact charge, no remaining reservation | Return/replay success only after verification; no duplicate business mutation |
| Deleting, before/after unlink | Old artifact charge retained until final SQL commit | Verify any remaining file, idempotently unlink, fsync directory, then finalize accounting |
| Canceling | Reservation retained | Verify/remove only owned staging/final names, then abandon |
| SQL committed but inventory publication failed | Latest witness remains authoritative | Reverify the namespace and rebuild that exact committed inventory; no operator reconciliation |
| Known inventory temporary JSON truncated by crash | Committed witness identifies its exact operation | Validate bounded regular temporary name, remove/rebuild after trusted namespace verification |
| Unknown path, invalid hash or unrelated temporary file | No automatic adoption | Fail closed or leave unrelated temporary untouched; require operator investigation |

Versions fence duplicate workers. Fixed lock inodes prevent unlink/recreate ownership splitting. Recovery skips live staging writers. A separately registered recovery service runs bounded sweeps even when discovery consumers are disabled. Prepared/Publishing artifacts require their original business replay; the recovery service does not invent delivery success. Deleted accounts' retained operation ownership permits later bounded cleanup if the process stops after the account transaction. Completed cleanup prunes owner journal rows. Existing account deletion occurs before local-copy cleanup, as before.

## Authentication and implicit writes

The service still uses ASP.NET Core Identity and its installed .NET 10.0.12 implementation. No password algorithm, lockout implementation or session format was replaced.

For coordinated login, the standard hasher verifies the immutable hash snapshot and prepares any required standard rehash outside broad admission. Work is bounded to two active attempts and 32 queued. The request retains no reusable credential cache. Under admission, the service locks/reloads the current Identity row, checks current lockout and security fields, rejects changed credentials/stamps, and calls the real `UserManager.CheckPasswordAsync`, `AccessFailedAsync` and `ResetAccessFailedCountAsync`. Only that matching request can reuse its precomputed result. Identity write/concurrency failure aborts the transaction. Hash upgrades, failure counts, lockout resets and session writes remain atomically accounted.

Password changes, resets, security-stamp invalidation and existing session checks retain their current semantics. Registration and reset password creation still use the standard Identity mutation path; no claim is made that every authentication API avoids hashing inside its transaction.

The local unsubscribe protector still uses standard Data Protection algorithms, application name, purposes and XML key material. Its implicit PostgreSQL key writes now participate in admission. Recursive first-key creation while already inside admission fails promptly instead of deadlocking and can retry outside. Existing keys still decrypt older tokens after rotation. The unrelated default Identity keyring remains unchanged to preserve existing password-reset token compatibility.

## Verification

Original redesign checkpoint: Domain 151, Application 147, Infrastructure 259, DataAccess 168, Presentation 114; **839 passed, zero skipped**. PostgreSQL tests use disposable databases in an owned loopback PostgreSQL 18.6 container. The initial nine HTTP environment failures were caused by their placeholder database address; those HTTP-only fixtures now explicitly supply an uninitialized witness. PostgreSQL startup/downgrade tests continue using the real store.

| Requirement | Local evidence |
| --- | --- |
| Same-size/restored-time changes, replacement/deletion/creation, invalid content, symlink/path rejection | `GenerationStorageTests`, `CoordinatedStorageTests`; guarded and forced full-read paths |
| Exact ledger, revisions, witness self-charge, rollback, trigger coverage, schema changes | `ManagedStorageCommitPostgresTests`, `StatementStorageAccountingPostgresTests`, `DiscoveryCoordinationPostgresTests` |
| Parallel capacity reservations, oversized writes, storage pressure | `ArtifactDeletionAndQuotaPostgresTests`, `DiscoveryCoordinationPostgresTests` |
| Partial staging, cancellation, injected ENOSPC, other writes progressing while staging pauses | `ArtifactStagingConcurrencyPostgresTests` |
| Actual process termination at Reserved/Prepared/Publishing/renamed boundaries; fresh-scope restart without reconciliation | `ManagedPublicationCrashPostgresTests` |
| Duplicate/repeated recovery, removal charge retained across interruption, business rollback | `ArtifactPublicationPostgresTests`, `ArtifactDeletionAndQuotaPostgresTests`, `StorageRecoveryPostgresTests` |
| Database backend termination before commit and fresh admission | `DiscoveryCoordinationPostgresTests.KillingBackendBeforeCommitRollsBackRowsLedgerAndAllowsFreshAdmission` |
| Scheduled capture, immutable receipt, exported owner metadata, account cleanup/restart, other owner retained | `ManagedScheduledCapturePostgresTests`, `ManagedEmailPrivacyCleanupPostgresTests`, `ManagedEmailBulkCleanupPostgresTests` |
| Parallel failed logins reaching actual lockout; prepared success cannot bypass new lockout | `CoordinatedAuthenticationPostgresTests`, `CoordinatedAuthenticationSecurityTests` |
| Password change/reset and security-stamp races; actual standard rehash; session rollback/invalidation | `CoordinatedAuthenticationRaceTests`, `CoordinatedAuthenticationSecurityTests` |
| Standard Data Protection key creation/rotation and recursive write refusal | `ManagedSecurityPersistencePostgresTests` |
| Reclamation releases file descriptors/read leases | `CoordinatedStorageTests.RepeatedReclamationDisposesEveryOwnedArtifactDescriptor` |

The frontend production build includes typecheck and passed. All **55 Playwright regression checks passed** with the installed Chromium, covering desktop/mobile widths and discovery/artwork recovery. Separate real-backend browser journeys at 390 px and 1440 px passed sign-in, owner export version 2.6, sign-out, layout checks and no credential storage in localStorage. Browser data were disposable fixtures; provider jobs were disabled. The browser MCP could not launch its configured absent Chrome; the installed repository Chromium was used without adding dependencies. The explicit stopped-writer establishment script also ran successfully against the disposable database before host startup.

These tests are not proof of durability against every kernel, device or power-loss failure. Process-kill tests cover real process interruption; ENOSPC is an injected write failure, not a destructive full-disk test. Connection termination covers precommit rollback, not every ambiguous network partition. Neither a successful test nor a local benchmark is production confirmation.

## Performance methodology and results

Baseline `bc0ccd3` and redesign use the same source harness, SDK/runtime, PostgreSQL container, generated data, pooled connections and page-cache warmup. Each isolated database contains eight disposable accounts and fixed serialized five-track score fixtures. Sequential login and persistence each have 20 samples; mixed workload has three waves of eight operations (four logins, two persistence operations and two exact-snapshot exports per wave). Shapes: zero artifacts, 100 MB in one file, 900 MB in one file, and 900 MB in 1,000 files. No upstream services are called. All failures are reported separately rather than treated as successful latency samples.

The storage component probe retains all five validation checkpoints, measures 20 samples after warmup, and includes a forced full-read variant. Runtime profiling is separate from latency measurements. Reported SQL command counts include intercepted EF executions and direct inventory reads; they are not exact network round trips and exclude internal trigger statements. Fixture score equality is not representative recommendation-quality evaluation.


Local Release measurements; successful-operation p50 / nearest-rank p95 in milliseconds. Sequential cases attempt 20 operations each; mixed cases attempt 24. Failed attempts are excluded from latency distributions and counted separately.

| Managed artifacts | Operation | Baseline p50 / p95 | Redesign p50 / p95 | Baseline / redesign failures |
| --- | --- | ---: | ---: | ---: |
| None | login | 59 / 69 | 75 / 82 | 0 / 0 |
| None | persistence | 37 / 41 | 49 / 58 | 0 / 0 |
| None | mixed-eight | 174 / 332 | 146 / 274 | 0 / 0 |
| 100 MB / 1 file | login | 426 / 464 | 140 / 146 | 0 / 0 |
| 100 MB / 1 file | persistence | 372 / 502 | 108 / 120 | 0 / 0 |
| 100 MB / 1 file | mixed-eight | 1,082 / 2,352 | 211 / 363 | 1 / 0 |
| 900 MB / 1 file | login | 3,065 / 3,241 | 586 / 662 | 0 / 0 |
| 900 MB / 1 file | persistence | 3,026 / 3,386 | 554 / 576 | 1 / 0 |
| 900 MB / 1 file | mixed-eight | 7,560 / 18,171 | 863 / 1,656 | 0 / 0 |
| 900 MB / 1,000 files | login | 3,190 / 3,505 | 1,552 / 1,618 | 1 / 0 |
| 900 MB / 1,000 files | persistence | 3,153 / 3,361 | 1,520 / 1,562 | 0 / 0 |
| 900 MB / 1,000 files | mixed-eight | 8,068 / 19,229 | 4,527 / 8,944 | 0 / 0 |

The baseline completed 253/256 attempts; the final redesign completed 256/256. An initial redesign run also had one physical-size rejection. A focused regression reproduced the retained stop-on-physical-growth check: an unchanged committed row/schema revision can coexist with PostgreSQL background relation growth. Witness-bound admission now refreshes that physical charge and checks the full reservation against quota before invoking a write. Changed revisions and quota overflow still fail. The regression failed before and passed after the correction. All current application cases were rerun afterward; the earlier pass remains separate diagnostic evidence.

The initial matched passes alternated version order. Final corrected application runs followed the retained baseline runs on the same shared development host; there is no claim of host isolation or production percentile accuracy. In particular, the empty-storage sequential cases are slower after adding the ledger witness/guard work. The redesign is not universally faster.

For single-file 900 MB login, median database-gate hold fell from about 3,065 ms to 32 ms: the initial verification runs outside the gate, and the remaining checks reuse only the live proof. For 1,000 smaller files it fell from about 3,190 ms to 1,224 ms; required full reads still occupy part of the gate. Empty-storage login uses about 26 measured SQL executions/inventory reads versus 12 before. This added database work explains a real small-workload cost; these are not wire-round-trip counts.

The five-checkpoint component comparison (20 samples per shape) measured:

| Component shape | Baseline p50 | Guarded redesign p50 | Forced full-read p50 |
| --- | ---: | ---: | ---: |
| None | 1.4 ms | 2.3 ms | 1.7 ms |
| 100 MB / 1 file | 352.7 ms | 64.1 ms | 302.0 ms |
| 900 MB / 1 file | 3,015.3 ms | 538.3 ms | 2,635.4 ms |
| 900 MB / 1,000 files | 3,101.9 ms | 1,562.1 ms | 1,574.0 ms |

The single-file 900 MB case reads about 4.5 GB before and 0.9 GB with a guarded proof. The 1,000-file case still reads 4.5 GB; smaller allocations and bounded buffered/parallel hashing help without dropping checks. Full fallback remains mandatory and was measured, not assumed equivalent.

For 20 single-file 900 MB login attempts, process CPU measured approximately 125.0 s before and 23.6 s after; allocation traffic increased from about 8.1 MiB to 16.3 MiB because of additional managed coordination. For 1,000 files it measured about 130.5 s versus 88.6 s CPU and 987 MiB versus 515 MiB allocations. Peak process working sets were similar/slightly higher in the redesign. These are console-workload observations, not retained-heap or production-capacity claims.

The catalog query retains table/column/trigger binding checks and complete relevant function definitions, but deparses each distinct trigger function once. Twenty paired query samples measured p50 9.27 → 7.86 ms and p95 11.08 → 9.36 ms. Individual EXPLAIN samples were noisy and do not independently prove an execution-plan speedup. Full plans and distributions are retained in the sanitized measurement artifact.

Launch-only `dotnet-trace` and `dotnet-counters` 10.0.750501 ran separately for baseline and redesign. Runtime counters observed peak working sets around 200 and 203 MiB respectively in the small profiled workload. Managed thread samples were dominated by waits and contain missing native symbols, so they do not establish per-method CPU attribution. No dump was taken. The process CPU and exact scan-byte measurements above provide the useful quantitative evidence.

Complete serialized recommendation results match baseline across **16 fixed-genre scenarios / 80 picks / 364,769 bytes**, including scores, ranking, provenance, gaps, exclusions, coverage and expiry behavior. SHA-256: `6b8e6dd62d50a4ce6060a333ef7a2867879bd09f7077b29bd017fc596135faef`. This verifies the frozen contract, not new representative listening quality.

Reproduction instructions, per-sample timing/failure data, stage distributions, query plans and runtime summaries: [harness README](diagnostics/storage-benchmarks/README.md), [primary measurements](diagnostics/storage-benchmarks/measurements-20261030.json), [runtime observations](diagnostics/storage-benchmarks/runtime-observations-20261030.json). No upstream requests were needed. Cold/warm live-provider discovery time, provider cache effectiveness and production concurrency remain unmeasured by this storage workload; the earlier scoped fixture results remain in [the remediation report](DISCOVERY_PERFORMANCE_REMEDIATION.md).

## Migration and operations

Three EF migrations add the commit witness, replace row accounting with equivalent statement accounting, and add publication reservations plus the deferred witness guard. No new service, database or dependency is required. The default VPS architecture remains PostgreSQL plus local managed storage.

1. Back up the database and managed roots together. Stop every web/Worker/maintenance writer; confirm no publication is active. Keep the same artifact roots and ownership identities.
2. Apply migrations using the existing EF deployment process. Do not start the new host before establishing its witness. Migration application does not silently adopt filesystem content.
3. Review unexplained external changes first. Explicitly establish the initial trusted inventory with `dotnet run --file scripts/reconcile_managed_storage.cs --no-cache -- --establish --writers-stopped --configuration /absolute/private/settings.json`. The operator command verifies accounting and hashes every artifact, refuses pending operations and pending migrations, and emits a bounded status. Treat this as an intentional trust establishment, not a routine retry button.
4. Set `DiscoveryRuntime:EnforceStorageAdmission=true` for hosts performing managed writes even when `RunJobs=false`. Enable ordinary discovery workers separately using existing provider approvals/configuration. Synthetic local schedule runs require jobs disabled with storage admission enabled.
5. Start one host with the same Linux volumes, paths and database; verify startup/recovery. Start other cooperative writers afterward. Keep existing upstream budgets and the ten-minute job limit unchanged.
6. Monitor bounded reasons for integrity stops, preflight/gate wait and hold times, full-read/guarded-reuse bytes, artifact queue/recovery age, disk headroom and reservation pressure. Never add user IDs, listening data, keys or tokens to metric labels/logs. No persistent telemetry/exporter was enabled.

Rollback requires stopping all writers and completing/canceling pending publication operations with the new implementation first. The reservation migration refuses rollback with active operations. Retain a consistent database/filesystem backup; downgrade migrations and explicitly re-establish the older inventory while stopped. Never run an old writer against an initialized new schema: the deferred guard rejects it. Roll-forward from a failed inventory publication should use the current witness/recovery, not destructive reconciliation.

## Remaining limitations

Full-account exports use bounded database pages inside one read-only snapshot, but still materialize the final DTO. This is not a constant-memory streaming response; large-history peak-memory work remains a separate confirmed limitation. Changing the HTTP streaming/snapshot lifetime needs its own implementation and failure verification, not an unsupported performance claim.

Full-content reads remain necessary without an enforceable operation-local proof. Many small files can therefore remain expensive. Metadata inventory replacement and exact global quota allocation still serialize; this implementation does not promise constant-time admission at arbitrary namespace size. Native durable publication currently requires Linux and supported filesystem primitives; unsupported platforms fail explicitly. Shared local filesystem semantics do not establish distributed/network-filesystem durability. Filesystem metadata durability still depends on the operating system and underlying storage honoring fsync.

No recommendation weights, seed/candidate coverage, exclusions, supporting evidence requirements or ranking policy changed. Live-provider latency, quality concerns, provider approvals and previously deferred recommendation shortcuts remain separate issues. Nothing has been deployed to production.

## Authoritative references

- [PostgreSQL explicit locking](https://www.postgresql.org/docs/current/explicit-locking.html) and [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).
- Installed Identity behavior: [UserManager v10.0.12](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Extensions.Core/src/UserManager.cs), [SignInManager v10.0.12](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Core/src/SignInManager.cs). This service calls UserManager directly.
- [Linux file leases](https://man7.org/linux/man-pages/man2/F_SETLEASE.2const.html), [fsync](https://man7.org/linux/man-pages/man2/fsync.2.html), [fs-verity](https://docs.kernel.org/filesystems/fsverity.html). fs-verity was considered, not required or implemented.

## Delivery state

Source changes are split into local commits `fac2d77` (filesystem/integrity primitives), `6d6497f` (ledger, publication, recovery and integration), and `d3cb0d6` (Identity preverification). The first two intermediate commits were independently built in the disposable checkout; the final combined solution also builds. Existing package warnings were not suppressed, and no dependencies were upgraded.

Owned PostgreSQL/container volumes, detached verification checkouts, private connection/configuration files, browser host, raw profiler resources and generated test/benchmark scratch were removed after collecting the sanitized evidence. Unrelated staged documentation deletions and the developer runtime/database were preserved. No push or deployment was performed.

## Startup upgrade follow-up — 2026-10-30

The existing local development database initially lacked all three new migrations. Startup now reports that requirement and exits cleanly. Its stopped local upgrade was backed up, checked against the original artifact baseline, and verified to preserve existing rows across 28 tables. Details and backup location are in [the crash follow-up](STARTUP_CRASH_FIX.md). Production remains undeployed.

Startup follow-up verification passed 116 Presentation, 168 DataAccess and 260 Infrastructure checks, including the new process-exit and strict establishment cases. A temporary real host returned `/health` HTTP 200 against the upgraded local database with provider jobs disabled, then shut down normally.
