# Unified storage integrity, publication and authentication proposal

Status: design for developer review, 2026-10-29. **No architectural implementation or migration is included.** The current admission, hashing, ledger, authentication and fail-closed recovery boundaries remain in service. Experiments below use generated files and disposable PostgreSQL only.

## Decision requested

Approve a staged implementation of a full-read protocol: retain the existing five artifact-validation checkpoints and global admission ordering; improve the scan primitive; batch eligible ledger updates with exact accounting; and give every writer a recoverable publication receipt. Treat password preverification as the final, separately gated stage of this same protocol.

This proposal does **not** offer constant-time verification of arbitrarily mutable files. Under the current filesystem trust model, removing full reads would weaken detection. If retaining the measured scan floor is unacceptable, a different filesystem enforcement/trust model would need a separate decision and proof.

## 1. Current architecture and measured bottlenecks

`DigestGenerationStore.AcquireBatchAsync` takes PostgreSQL advisory lock 1280200758. `DiscoveryWriteCoordinator` then takes the local filesystem lease, reads the reconciled inventory, reserves headroom, runs a database transaction, verifies actual growth and publishes the new inventory. Acquisition HTTP runs outside this callback. The transaction also takes the advisory transaction lock and the ledger row lock. The session lock spans the full operation, including filesystem work and post-commit publication.

The normal coordinated write performs five complete artifact scans: acquisition, reservation, validation before mutation, stored-growth verification, and post-commit inventory publication. Each scan walks the configured roots, rejects symlinks and excessive file counts, reads every file, hashes content with SHA-256 and binds path, byte length, modification time and root identity into category revisions. Timestamps are inputs to the digest, not substitutes for reading content.

Every managed row mutation fires a ledger trigger. Logical bytes are exactly the project's accounting formula, `octet_length(to_jsonb(row)::text) + 512`; this is not exact physical tuple/index/WAL usage. Admission also charges observed database size and checks configured filesystem free space. Core and all-write revisions have different compatibility roles. Schema/trigger coverage and origin mode are checked; new tables cannot silently escape accounting.

`LocalGenerationStorage.PublishCommittedAsync` validates artifacts after database commit, writes/flushed a temporary inventory and replaces the inventory file. An unknown/missing publication deliberately stops subsequent writes. A successful database commit cannot be undone by an HTTP cancellation. The legacy weekly/capture paths and their own-write exceptions must be included in a single protocol, not repaired by accepting arbitrary new inventory.

Login currently calls `UserManager.CheckPasswordAsync`, access-failure/reset methods and refresh-session persistence inside coordinated admission. Password verification may perform an implicit rehash/update. Moving that entire call outside admission would create unaccounted writes and change race behavior.

### Measurements and interpretation

Original controlled five-checkpoint samples (five observations per shape, warm OS cache):

| Managed artifacts | Median admission primitive | Bytes read per operation |
| --- | ---: | ---: |
| Empty | 3.59 ms | About 1 KB bookkeeping |
| 100 MiB, one file | 537.42 ms | 500 MiB plus bookkeeping |
| 512 MiB, one file | 3,144.26 ms | 2,560 MiB plus bookkeeping |
| 100 MiB, 1,000 files | 765.17 ms | 500 MiB plus bookkeeping |

These are local measurements, not production percentiles. The experiment exercises the storage protocol without a real application mutation; database execution and queue waits add cost. A separate two-connection ledger experiment with a deliberate 200 ms owner hold measured approximately 204 ms waiter delay. The shared lock really serializes competing writers.

New full-read alternatives and ledger experiments are summarized in `diagnostics/proposals/measurements.json`. Scan experiments rotate execution order, discard a warmup, take 20 samples per variant, retain all five reads and check equal category revisions. They demonstrate a scan optimization candidate, not a complete publication protocol. The initial scan experiment overlapped other development checks; use the reproducible probe for an isolated target-VPS acceptance run before making a capacity promise.

The SQL experiment takes 20 observations after warmup for 100, 1,000 and 10,000 inserted rows. It checks exact byte/count/revision totals and rollback after every sample. At 10,000 rows its insert-only statement-trigger prototype measured 28.52 ms median versus 639.16 ms for row-trigger updates. This is **not** a production trigger replacement: update/delete/truncate, cascades, conflicts, core accounting, schema verification and concurrent writers still need acceptance tests. Ordinary ORM operations that issue separate single-row statements do not obtain the same benefit.

Password-hash measurements use Identity's installed default hasher, generated credentials, 25 samples after warmup and current/default iteration settings. They measure verification CPU, including computing a replacement on a rehash-needed result. They do not measure a complete login, database writes or a proposed authentication boundary. No password strength or work factor is reduced.

## 2. Alternatives and tradeoffs

| Alternative | Performance opportunity | Integrity/accounting consequence | Decision |
| --- | --- | --- | --- |
| Keep current implementation | No migration risk | Keeps current checks and their cost; unknown publication remains an operator stop | Supported fallback |
| Cache hashes by timestamp/size; watchers; periodic full scans | Avoid most reads | Same-size edits with restored timestamps, missed events and changes between scans can escape a required check | Rejected as equivalent replacement |
| Signed/DB-anchored manifest alone | Faster trusted metadata lookup | Authenticating the expected digest does not prove current file bytes still match it | Useful for publication/recovery, never permission to skip reads |
| Immutable names/read-only permissions | Easier writer ownership | An external writer able to replace/delete files can bypass naming and ordinary permission conventions | Insufficient on its own |
| fs-verity/dm-verity or immutable snapshots | Kernel-enforced integrity can avoid repeated userspace hashing in some models | Needs filesystem/platform support, trust and namespace proofs; measuring a verity digest does not proactively read every possibly corrupted block at each current checkpoint | Separate design; no equivalent guarantee claimed |
| Larger bounded read buffers, limited parallel full-file hashes | Reduce syscall/allocation overhead and overlap independent files | Retains full reads; ordering, cancellation, resource limits and races must be proven | Recommended experiment-to-implementation stage |
| Statement-level transition-table ledger deltas | One ledger update for a multi-row statement | Exact deltas and per-row revision increments can be retained; operation coverage is substantially more complex | Recommended after complete proof suite |
| Independent per-user admission without global reservation | More concurrent writes | Global quota, schema and artifact publication can overcommit or race | Rejected without a separate reservation protocol |
| Pure password preverification plus locked state validation | Move hash CPU outside global admission | Safe only with exact Identity compatibility, state fencing and all mutations still coordinated | Final design stage, not yet implemented |

Kernel documentation explicitly describes fs-verity as read-time verification and distinguishes the constant-time measurement of its digest from reading content. It also requires authentication of expected digests. This proposal therefore does not present verity as a transparent substitute for the current checks. See [kernel fs-verity documentation](https://docs.kernel.org/filesystems/fsverity.html).

## 3. Recommended protocol

### A. Preserve full content validation

Keep the five existing validation points, SHA-256 algorithm, namespace enumeration, root/symlink restrictions, maximum files/bytes, filesystem headroom, current expiry rules and cancellation behavior. Evaluate 64 KiB pooled reads and at most two independent file hashes. Fold results in the exact existing directory/file order, irrespective of completion order.

Buffers, descriptors and pending results must be bounded. Do not run hundreds of open files or put a database write inside parallel work. Restat/length checks, errors and cancellation must fail closed. A file becoming unreadable cannot become a cache hit. The experimental primitive is not production-ready: it measures unchanged and same-size/restored-time mutation cases, not the complete concurrency/error matrix.

Keep validation inside the same logical critical sections. Moving a scan before acquiring coordination, reducing five scans to one, or returning from a write before publication would change the protocol and is outside this recommendation.

### B. Preserve exact ledger arithmetic while reducing redundant updates

For supported multi-row statements use separate INSERT/UPDATE/DELETE statement triggers and transition relations. Compute the exact sum of the existing row accounting expression, then apply one locked ledger delta. For UPDATE, use actual old and new rows and keep row count unchanged. For DELETE subtract the old rows. Increment all-write/core revisions by the exact number of affected row events, preserving current semantics even for no-op updates. Preserve the current special TRUNCATE behavior.

Do not infer ORM writes from application intentions: direct SQL, cascades, Identity writes, scheduler heartbeats, exports' ancillary writes and every covered table remain in scope. Keep row-level fallback on operations/tables whose semantics cannot be shown equivalent. Zero-row statements must not create a revision bump. Upsert, cascades, partition movement, repeated updates of the same row and nested triggers need explicit comparison with the current implementation.

The schema fingerprint and `ManagedStorageIntegrity` verifier must recognize and verify the complete new trigger/function definition, mode and coverage. Checking only that a trigger name exists is insufficient. Managed migrations remain offline operations with a verified backup and explicit inventory establishment.

PostgreSQL documents transition tables and statement/row trigger distinctions in [CREATE TRIGGER](https://www.postgresql.org/docs/current/sql-createtrigger.html). Bulk-statement measurements cannot predict single-row application throughput.

### C. One publication protocol for every writer

Use an immutable, database-anchored publication receipt and a versioned filesystem manifest. The manifest binds canonical roots, all category revisions, prior manifest digest, protocol version and publication identity. SHA-256 of its canonical bytes is stored in the receipt in the same transaction as the managed mutation. The trusted database anchors expected manifest identity; no new key service is proposed.

The exact state machine is:

1. **Validated baseline:** hold database session coordination and filesystem lease; verify previous manifest, ledger/schema and full artifact baseline. Refuse unexplained differences.
2. **Reserved:** reserve declared database/artifact growth, including receipt, staging files and temporary-manifest overhead. Perform the existing second/third checks. No provider I/O under admission.
3. **Prepared:** identify every owned artifact path and expected old/new SHA-256/length. Fully validate unchanged paths and each staged artifact. The receipt names only permitted transitions; it never grants permission to reconcile unrelated changes.
4. **Database committed:** commit the business mutation and immutable receipt together. The receipt contains enough exact before/expected-after ledger/schema information to reject intervening unknown writes. It is itself managed/accounted; there is no exempt outbox table.
5. **Published:** under the same ownership, perform only receipt-authorized file transitions; run the final full validation; durably write/flush the manifest and replace it atomically on the same filesystem. Validate directory durability on supported deployment platforms. Return success only after publication.
6. **Recovery required:** if the process stops anywhere after commit, new writes stop until the exact receipt transition is verified and completed. Recovery is idempotent. Unknown files, wrong hashes, mismatched schema/ledger, missing staged/final copies or ambiguous commit identity remain operator errors.

The implementation must explicitly solve receipt self-accounting: a receipt containing its own after-counter values cannot cause an endless update/revision loop. One candidate is a single immutable insert with fixed-width validated counter encodings and a precomputed exact row charge, followed by an in-transaction comparison against actual final counters. An update to 'mark published' would itself be another managed write and cannot be ignored. Receipt completion is evidenced by the matching filesystem manifest, not an unaccounted database flag. This mechanism requires its own boundary/overflow tests before acceptance.

Receipt retention must be bounded and accounted. A compact checkpoint plus a bounded receipt chain may replace acknowledged receipts only within a coordinated transaction and publication. Never delete the one receipt needed for an unfinished transition. Reserve maintenance headroom; storage pressure must not make recovery depend on adding unlimited rows. Final limits and actual row sizes must be validated in the implementation, not assumed from this design.

Proposed initial limits for review: one unfinished publication under global admission, at most 128 receipt slots, 256 KiB canonical payload per slot, a 64 KiB canonical filesystem manifest and at most two scan workers with 64 KiB buffers. Receipt retention therefore has a 32 MiB payload ceiling plus exactly accounted relational/index/physical overhead, not a claim that its disk usage is exactly 32 MiB. Reject oversized transitions rather than truncate proof. Retire only acknowledged receipts covered by the manifest/checkpoint; a slot needed by unfinished recovery is never reusable. The checkpoint/slot writes themselves participate in the expected ledger delta. Validate these limits against actual existing roots/writer shapes before migration; unsupported configurations need an explicit decision rather than silent adoption.

A database commit and a filesystem rename are not a single native transaction. Logical atomic publication therefore requires readers of newly committed results to verify that their publication sequence is covered by the authenticated manifest/checkpoint. A raw `Completed` row alone must not expose an unpublished result. Previously published records remain readable; an unrelated failed publication must not erase their ownership or imply they were never committed. This reader fence is part of the architectural proposal and its tests.

Weekly persistence, capture partials/final renames, scheduler transitions, unsubscribe/account mutation, artwork persistence, public imports, reclamation and maintenance must all use the same protocol or a formally specified adapter. The existing concurrent-capture block remains until that adapter passes its failure matrix. Do not auto-run blind `ReconcileAsync` after a failure.

### D. Authentication as part of this protocol

Snapshot only the required Identity fields into request-local memory, perform pure password verification outside admission, then reacquire coordinated admission and reload/lock the current user. Compare password hash, security stamp, concurrency stamp, deletion state, lockout state and relevant counters. A changed state invalidates preverification; retry only within a strict bounded policy or return a retryable failure. Never accept a password verified against a superseded hash.

Do not move `UserManager.CheckPasswordAsync` itself outside admission: it can rehash and persist. A narrowly scoped adapter must preserve Identity's verification/rehash semantics while keeping rehash updates, failed-attempt increments, successful resets, lockout and refresh-session writes inside the normal transaction/accounting/publication protocol. A stale failed-attempt snapshot must not overwrite another attempt; parallel successful attempts must not clear a newly imposed lockout or password reset.

The hashing snapshot is ephemeral, never logged, exported or stored in public caches. No plaintext password in a queue, receipt, diagnostic artifact or recovery record. Tokens are returned only after committed publication; a lost response has a defined retry behavior and does not justify bypassing admission. Rehash work may also need precomputation outside the lock, with the same state fence and installed hasher options.

This stage needs measured real Identity concurrency and fault tests before approval to change the boundary. The current hash-primitive benchmark is not that proof. Keep the current boundary if the full equivalent adapter cannot be demonstrated.

## 4. Integrity and consistency arguments

| Invariant | Preservation argument | Evidence and remaining obligation |
| --- | --- | --- |
| Persistent external content edits | Every required checkpoint reads every byte and compares SHA-256-bound revisions | Prototype detects a same-length mutation with restored timestamp; needs all race/error scenarios below |
| External add/delete/rename/symlink | Namespace is enumerated under the same policy; path/root identity remains in digest; symlinks rejected | Existing production tests plus required prototype equivalence/error suite |
| Exact logical accounting | Same row-charge function and exact event-count deltas; same transactions/rollback; no exempt receipt writes | Insert/rollback primitive checked; full operation/core/cascade suite still mandatory |
| Physical capacity | Continue charging observed physical database size and checking all configured volumes/staging overhead | No logical counter is presented as a disk-capacity measurement |
| Concurrent writes | Same lock order and quota linearization; no writer publishes outside the receipt protocol | Existing admission tests; new multi-process receipt/recovery tests required |
| Atomic publication | Reader fence plus committed receipt and durable manifest define the logical publication point | Design argument only; native cross-resource atomicity is not claimed |
| Recovery | Only the exact recorded old→new transition can be replayed; unexplained state stops | Requires crash-at-every-transition implementation tests |
| SHA-256 | Algorithm/content coverage unchanged; canonical manifest adds another SHA-256 binding | Unchanged-file revisions equal in the scan experiment |

Important limit: the current application scan is not continuous filesystem surveillance or a single atomic snapshot of arbitrary external writers. A file can be changed and restored between checks, or after it was read. Neither the current code nor this proposal can honestly promise detection of every such transient event. The preserved guarantee is full detection coverage at the existing validation checkpoints for changes observable by those reads, plus fail-closed mismatches. If the requirement means stronger continuous/instantaneous detection, that is a requirement expansion needing an enforced filesystem/snapshot trust boundary; no application hash cache supplies it.

## 5. Required crash, concurrency and modification scenarios

- Kill the **disposable test host** before/after reservation, staged-file flush, receipt insert, database commit, each rename, manifest flush/replace and directory flush. Restart and verify either exact prior state or exact receipt-completed state; never an invented third state.
- Cancel an HTTP caller during commit/publication, lose the result response, replay the same intent, replace/expire the job lease and delete the profile concurrently. Only the current fenced worker may publish; a timeout never becomes partial success.
- Run at least two independent hosts sharing the configured database/artifact storage and pacing. Exercise lock order, owner termination, quota at cutoff, reclamation, maintenance and scheduled capture. Prove no double publication or quota overcommit.
- Edit content with unchanged length/restored timestamp, replace the inode, delete/rename a file, add a nested file, exchange a directory/symlink, revoke read permission and mutate/grow/truncate during hashing. Repeat between each protocol checkpoint and during recovery.
- Corrupt/truncate/swap manifest and receipt identities; remove the key receipt; alter ledger counters; disable/replace accounting triggers/functions; change schema; use replica mode. Every unexplained condition stops without automatic adoption.
- Compare ledger results for INSERT/UPDATE/DELETE/TRUNCATE, zero/no-op operations, upsert, cascades, partition moves, savepoints/rollback and nested triggers, raw SQL and all ORM/Identity paths. Include counter overflow and receipt self-accounting/retention pressure.
- Race correct/wrong passwords, threshold lockout, password reset, security-stamp change, account deletion, rehash, refresh rotation and transaction failure. Compare externally visible results and final Identity rows with the current implementation under controlled ordering.
- Exhaust disk/quota during every staging/publication step, including receipt retention cleanup and inventory replacement. Recovery must retain required proof and stop safely when space is genuinely unavailable.

## 6. Performance expectations

Full artifact verification remains O(total artifact bytes + names) at each checkpoint. The scan prototype suggests bounded I/O improvements, especially with many files, not elimination of the scan floor. Two workers on a VPS with one disk/core can be slower; measure and choose the smallest useful setting. Do not extrapolate warm page-cache numbers to cold storage.

Statement-level accounting can substantially reduce work for genuinely multi-row statements. It does not eliminate global lock serialization or make five scans cheap. Publication receipts add bounded writes/bytes and may partially offset gains for tiny operations. Preverification can remove measured hash CPU from lock occupancy only after its equivalence gate; it does not remove storage work or all login cost.

Acceptance measurements must compare the same data, artifact shape, GC/runtime, quota state, cache state, caller count and provider fixture. Report p50/p95, lock wait/hold, bytes hashed, CPU/allocations, SQL/WAL and completed/failed operations. Include cold and warm storage, 1/2/4 concurrent owners, scheduled writes and storage near cutoff. No production latency promise is made by this proposal.

## 7. Migration and rollback

1. Freeze the accepted design and protocol/manifest schema. Back up database, manifests, artifacts and required configuration; restore-test the backup.
2. Stop all web/Worker/operator writers. Verify complete existing ledger totals, schema/trigger definitions, all SHA-256 artifact baselines and free space. Abort on unexplained differences.
3. Apply versioned migrations for receipts/ledger functions with exact backfill accounting. Establish the first DB-anchored manifest through an explicit verified migration operation. Do not treat `EnsureCreated` as migration.
4. Deploy compatible web/Worker readers/writers together. Keep ordinary acquisition/page-render paths unchanged. Refuse mixed protocol versions; do not let an old Worker write new storage.
5. Run the crash/identity/multi-host matrix on disposable restored data before enabling writes. Enable staged features only when their complete equivalence checks pass.
6. Roll back with writers stopped. Resolve a pending receipt first or restore the matched database+artifact+manifest backup. Recompute/verify the old ledger under its original formula, reinstate exact original functions and explicitly establish the old inventory. A binary-only rollback or deletion of receipt evidence is unsafe.

No external infrastructure, broker, additional database service or recurring fee is proposed. New receipt storage, migrations and reader/writer protocol changes remain subject to approval. Linux filesystem durability support, old-host exclusion and receipt self-accounting are acceptance gates, not completed features.

## 8. Acceptance and evidence status

The scan, insert-only ledger and password-hash probes are reproducible in `diagnostics/proposals/`. Their results and limitations are in `measurements.json`. They do not constitute a verified architectural implementation. The recommended protocol has explicit invariants and failure behavior, but its receipt, reader-fence, all-operation accounting and authentication adapter still require the tests above before it could replace the current implementation.

**Review request:** approve or revise this staged design. Until then, keep the current architectural boundaries and their known cost. Safe discovery/artwork/diagnostic corrections continue independently.
