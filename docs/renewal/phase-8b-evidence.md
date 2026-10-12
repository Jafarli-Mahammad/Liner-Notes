# Phase 8b implementation and combined verification

Date: 2026-10-12. Concrete design and immediate implementation approved.
Developer confirmation remains pending.

| Authorized scope | Result | Evidence/boundary |
|---|---|---|
| 8a privacy implementation, then separate tests | Locally verified | [8a evidence](phase-8a-evidence.md): 70 passed, zero failures/skips |
| 8b bounded local UTC scheduling | Implemented | Pure `WeeklySchedulePolicy`; bounded Synthetic-only options; Application orchestration; DataAccess lease/CAS; Worker hosted service |
| Failure, contention, cancellation and restart | Locally verified | Named checks below; original ciphertext preserved; claimed slot advances once |
| Runnable local QA | Implemented | [Local run](phase-8b-local-run.md), fixture helper and existing launcher menu |
| Separate 8b checks | Passed | 58 passed, zero failures/skips; combined scenario excluded |
| Combined 8a + 8b checks | Passed | 121 passed, zero failures/skips; overlapping checks counted once |
| Whole solution | Passed | 468 passed, zero failures/skips, after final review and fixture preparation changes |
| External sending, live acquisition, deployment/Phase 9 | Not done | Outside this approved local design |
| Developer confirmation and production capacity | Pending | Automated synthetic results establish neither |

## Implementation and important checks

- `WeeklySchedulePolicyTests` (12): UTC-only values, whole-hour weekday alignment,
  exact-time initialization, one-second-late/future slots, missed-slot advancement,
  ISO-year boundaries (including 2018-12-31 → 2019-W01), invalid settings.
- `LocalScheduleOptionsTests` (15): immutable explicit allowlist, duplicates/empty
  IDs/oversized lists, numeric bounds, Synthetic-only origin and unambiguous command
  selectors. Real host parsing ignores the OS `USER` value and rejects explicit
  legacy `--user`/`--week` options.
- `RunScheduledTickTests` (12): exact claimed week passed to existing generation,
  verified capture required for advance; generation/capture failure, stale advance,
  lost lease and cancellation preserve the due slot; invalid raw schedules stop;
  null future initialization performs no generation.
- `LocalScheduledWorkerTests` (2): busy run ends after one tick; cancellation during
  the interval ends cleanly; the tick scope is disposed before waiting.
- `Phase8ScheduledWorkerPostgresTests` (10): actual migrated PostgreSQL and actual
  composition; concurrent/repeated ticks produce one receipt; advisory contention,
  release and conditional initialization/advance; hidden/opted-out/deleted accounts;
  changed preferences; missing completed receipt; two-tick finite real host;
  changed/missing storage; combined scheduled generation/export/opt-out/deletion.
- The two process-restart theory cases stop at publication before capture SQL commit
  and capture SQL commit before due-slot advance. A second real Worker process
  returns `scheduler_busy`. Kill only the owned first process and its exact blocked
  backend in the disposable database; restart verifies `already_captured`, byte-for-byte
  original ciphertext, one file and `NextDigestAt = claimed slot + 7 days`.
- Eight existing `LocalWorkerPostgresTests` continue covering final opt-out locking,
  cancellation after publication, SQL failure recovery, account ownership/deletion,
  empty digest and missing receipt. They are included in the separate 8b run.

The scheduler uses advisory key `1280200760`, distinct from the existing generation
and account leases, on a dedicated non-pooled connection. Cancellation/disposal closes
that connection. Ownership is checked before jobs and advancement. Reads select at
most the configured count from the explicit allowlist in stable due-time/ID order.
Advancement matches active Identity and Domain account, opt-out absence, original
day/hour and original due slot. No row transaction spans generation/file work/delay.
Each tick has a fresh scope; generation/capture retain their existing inner context
and transaction boundaries. Scheduled output is bounded JSON with no log providers.

No new table, migration, package, export version, scorer weight or provider is adopted.
Scheduled `NextDigestAt` and audit writes use already inventoried/exported columns.
The combined check compares the advanced time, full stored score snapshots and
sanitized copy in export 2.2, skips later opted-out work, deletes owned SQL/Identity
and files, and preserves shared catalog/keys and another account. Existing 8a HTTP
checks separately establish access invalidation and complete scalar/schema coverage.

## Commands and measured outcomes

All commands used disposable loopback PostgreSQL with `LINER_PHASE1_POSTGRES` supplied
outside source. Logs under `/tmp/liner-phase8b-work` contain fabricated fixtures only.
The installed SDK is 10.0.112; PostgreSQL is 16.15; existing package versions were kept.

The final controlled fixture runs temporarily disabled automatic vacuum in the
task-owned PostgreSQL container, with the normal setting restored afterward.
Application storage guards, data-change detection and explicit reconciliation
remained enabled. An earlier full 468-test pass ran with automatic vacuum enabled.
The exact source of every earlier physical-size change was not established; this
controlled setup is not evidence of production behavior under database maintenance.

Separate 8b:

```bash
dotnet test 'Liner Notes.sln' --no-restore \
  --filter '(FullyQualifiedName~WeeklySchedulePolicyTests|FullyQualifiedName~LocalScheduleOptionsTests|FullyQualifiedName~RunScheduledTickTests|FullyQualifiedName~Phase8ScheduledWorkerPostgresTests|FullyQualifiedName~LocalScheduledWorkerTests|FullyQualifiedName~LocalWorkerPostgresTests)&FullyQualifiedName!~Combined_' \
  --logger 'console;verbosity=quiet' -m:1
```

Domain 12, DataAccess 34, Application 12: **58 passed**. Presentation/Infrastructure
contain no checks selected by this filter; these are not skipped tests.

Combined: use the 8a filter recorded in [8a evidence](phase-8a-evidence.md) plus the
six 8b class filters above, including `Combined_`. Domain 12, DataAccess 43,
Application 22, Presentation 22, Infrastructure 22: **121 passed**.

Whole solution:

```bash
dotnet test 'Liner Notes.sln' --no-restore --logger 'console;verbosity=normal' -m:1
```

Domain 117, DataAccess 84, Application 107, Presentation 54, Infrastructure 106:
**468 passed, zero failures/skips**. The final sequence reverified **8a 70 → 8b 58
→ combined 121 → full 468** on the final source and test fixtures. The fixture
maintenance setting was restored and verified `on` after these runs. Existing
package-pruning warnings remain; no dependency remediation was introduced.

Focused local implementation commits: `82c3329` (pure policy/configuration),
`0c0f7d7` (orchestration/store/host, real recovery tests, QA helper/menu and guarded
fixture preparation). Evidence/status updates form a following documentation commit.

## Rulings, failures and limits

1. The developer approved the design and directed immediate implementation. Another
   plan approval was superseded; the selected 8a → 8b → combined order was retained.
2. Preserve the existing changed-inventory guard. Generation changes inventory, so
   initial capture stops and needs explicit reconciliation. First physical page
   allocations can cause a further stop. Test/operator retries are bounded to three
   explicit reconciliations, accept only `database_inventory_changed`, and assert no
   early advancement/publication. Product scheduling never performs reconciliation.
3. A killed client does not immediately stop a PostgreSQL backend blocked inside a
   test trigger. Releasing that trigger can permit an autocommit UPDATE to finish.
   Crash tests terminate the exact owned blocked backend with a wait before unlock,
   ensuring the intended rollback boundary. No product crash-hook or test table exists.
4. Fix actual host issues found by the process tests: environment `USER` collided with
   the legacy selector, and reading `host.Services` after `RunAsync` accessed a disposed
   host. Parse explicit selectors and retain the result-state reference before running.
5. Keep fresh scopes per bounded tick and existing per-operation transaction ownership;
   dispose the tick scope before its timer delay. No SQL transaction is held for a tick.

One fresh final review found no Critical/Important issue or material scope gap. Its
Minor progress-document status follow-up was corrected. The reviewer did not rerun
verification; the executing agent owns the measured results. Direct fixture-helper
invocation updated a newly registered disposable account to its explicit UTC due
slot and returned `schedule_fixture_set`; a non-Development invocation stopped.
`bash -n scripts/run_phase7_local_qa.sh` passed.

Early full runs each had 467 passes and one capture/restart-fixture failure. Separate
8a/combined rechecks also exposed inherited generation/capture fixture instability. Fixture preparation
now provisions protection keys and warms existing row paths before measurement.
Guarded operator retries in inherited fault tests are permitted only when their
final-row/fault hook has not run, with no published file; any later stop fails the
test. Generation setup retries require no digest, pick or catalog writes. Legacy
fixtures now use independent non-pooled connections without global pool clearing.
This preserves opt-out/publication/commit assertions and the product guard.
Local SQL/files publication is not one transaction. Power-loss durability,
distributed production scheduling, delivery-provider uncertainty, quality, actual
capacity, backup/operator-file erasure and deployment/key custody remain unverified.
The 1,000,000,000-byte cutoff remains temporary. These results do not confirm prior
phases on the developer's behalf or authorize Phase 9.

The lifetime/lease design was checked against Microsoft's [hosted-service guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)
and PostgreSQL's [advisory-lock documentation](https://www.postgresql.org/docs/16/explicit-locking.html#ADVISORY-LOCKS).
The test-only backend termination follows [PostgreSQL 16 administration functions](https://www.postgresql.org/docs/16/functions-admin.html).
