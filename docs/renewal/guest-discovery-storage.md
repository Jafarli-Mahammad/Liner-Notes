# Discovery storage and job coordination

Status (2026-10-17): C7 mechanics are locally verified. The website runner and new
submission flow are not enabled yet. This document describes the guard contract,
not a measured VPS capacity or production-readiness claim.

## Accounting

The generated `AddManagedStorageLedger` migration installs per-row accounting
triggers on application and Identity tables. A separate statement trigger covers
an explicit operator TRUNCATE. The migration performs one initial backfill; normal
admission reads aggregate counters and schema metadata rather than serializing
private history. Logical accounting charges the UTF-8 JSON row size plus a
provisional 512 bytes per row. This is a conservative admission estimate, not an
assertion about each row's actual index size.

Admission uses the larger of logical accounting and `pg_database_size`. Its schema
fingerprint covers relation identities, columns and trigger state. Uncovered or
disabled accounting triggers, missing counters, changed schema and unexpected row
mutations stop admission. Relation size can change without a row mutation; the
coordinated path charges the current observed size when the complete counter and
schema revision remain unchanged. It does not silently discard physical growth.

The ledger keeps both the historical weekly core revision and an all-write
revision. The old weekly/local-capture guard compares its original core scope;
physical-size, artifact and cutoff checks remain. New discovery admission compares
the complete revision. This preserves local Worker mechanics without ignoring
Identity, scheduler, catalog or other managed writes in the new path.

Global counters are operational metadata, not privately owned profile records.
They contain no user payload and are excluded from individual account exports.
Every ledger column is in the reviewed schema inventory. Private jobs remain
exported/deleted through their profile; credentials and lease tokens remain
excluded security internals. Terminal jobs clear their private input payload.

## Short managed writes

`DiscoveryWriteCoordinator` takes the shared database lease, filesystem lease and
reservation only around admission, transaction and inventory publication. Provider
I/O must happen outside that callback. The transaction fences the ledger, verifies
the starting revision, applies accepted input/job changes together, then checks
actual logical and physical growth. A failed check rolls everything back. The
all-write and schema revision are validated again before publication.

After commit, the inventory file is written, flushed and atomically replaced under
the same leases. A failed or interrupted publication leaves a revision mismatch;
new writes stop until explicit reconciliation. An HTTP disconnect does not cancel
the bounded bookkeeping step after a successful database commit.

The existing managed-write stop remains 1,000,000,000 bytes. It is **not** a 10 GB
filesystem size or a total VPS allowance. PostgreSQL WAL, other databases, images,
logs and host files are outside `pg_database_size`; filesystem headroom must also
be measured. `DiscoveryStorage:FilesystemPaths` must name readable paths on every
relevant database/WAL/artifact volume as seen by the application. An unavailable
volume fails closed. `MinimumFreeBytes` defaults to 512 MiB. Operator configuration
must describe the actual deployment mounts; the application cannot infer remote
database free space from its own root filesystem.

The inventory has a five-minute freshness requirement. The enabled runtime renews
only a known baseline every minute and reclaims expired guests and public evidence
in bounded passes. Account writes, weekly persistence, scheduler activity and
unsubscribe updates use coordinated admission. Provider acquisition occurs before
the short final write lease. Concurrent optional local email capture remains
blocked in both hosts. Public-cache quotas and coalescing are implemented; source
approval and real-catalog coverage remain independent gates. See the measured
[resource profile](guest-discovery-capacity.md) and [local runbook](guest-discovery-local-run.md).

## Explicit reconciliation

The local Worker's existing `reconcile` action now verifies ledger totals against
all covered rows before recording filesystem/database inventory. Verification
uses short database coordination and table locks; it is an explicit integrity
operation, not a page-view or per-job history scan. A mismatch stops without
silently repairing counters. Stop writers and investigate/restore from a verified
backup if corruption is reported. Do not manually zero counters to bypass a stop.

Apply migrations with writers stopped and a verified backup. Existing inventory
revisions become stale after the accounting migration and need explicit
reconciliation. `EnsureCreated` does not install the custom SQL triggers and is
not a substitute for migration. Future migrations that add a table must install
both accounting triggers and extend the reviewed inventory in the same change.

## Jobs

Requests use owner/key uniqueness and content hashes for idempotency. The same key
with another intent conflicts. Queue capacity and cooldown are admission controls;
there is no configured user-count ceiling. At most one unfinished generation is
allowed per profile, with an additional store check for lookup work.

Workers claim eligible rows with `FOR UPDATE SKIP LOCKED`. Random lease tokens,
expiry, bounded attempts and retry dates survive process restarts. A replaced or
expired token cannot renew or complete. Compound operations lock the profile
before the job, matching deletion/import order. Completion rechecks the stored
lease and current profile revision; stale tracked objects are reloaded before
fencing. Terminal outcomes revoke leases and clear raw input payloads.

The runtime supplies the consumer, safe error classification, source fairness,
progress API and transactional publication. Routes remain opt-in; start the
consumer with valid source/storage configuration before accepting requests.
