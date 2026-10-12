# Phase 8a implementation evidence

Verified locally on 2026-10-12. Developer confirmation remains pending.
Approved [design](phase-8a-review.md), [plan](phase-8a-plan.md), and
complete [column inventory and boundaries](phase-8a-inventory.md).

## Per-approval accounting

| Approved item | Status | Evidence / reason |
|---|---|---|
| Complete table/column inventory | Implemented | Explicit contract covers every EF relational column, shadow TrackId, Identity/security columns and migration history; current migrated PostgreSQL schema matches |
| Model changes fail until reviewed | Implemented | Deliberately omitted PhoneNumber failed both checks; added column/table controls are rejected |
| Export 2.2 safe Identity profile | Implemented | All 15 reviewed fields, stored independently from Domain values; null contacts preserved; scoped Application reader with explicit NoTracking SQL projection |
| Existing Domain profile/schedule/opt-out fields | Implemented / verified | Every persisted scalar compared with the authenticated serialized API response |
| Connections, provenance and hidden taste rows | Implemented / verified | Every reviewed scalar, hidden connections and prior hidden-taste regression; EncryptedToken individually excluded |
| All digest/recommendation history and stored audit fields | Implemented / verified | Actual API has 205 timestamp-tied histories and hidden recommendations; existing 1,005-history regression also passes |
| Referenced shared-track metadata | Implemented / verified | Every stored scalar, including nested representation of shadow TrackId, compared with SQL; no whole shared-catalog dump |
| Score JSON, full snapshots and unknown fields | Implemented / verified | Recursive persisted SQL-to-HTTP comparison includes populated policy, weights, vectors, contributions, components, evidence, observations, response metadata, discovery paths and nested unknown fields |
| Credentials/authentication/capability exclusions | Implemented / verified | Credential value/name sentinels absent; authenticated copy alternatives remain sanitized; security columns individually inventoried |
| Same-account isolation and safe viewer/download | Implemented / verified | Foreign userId query cannot select another account; unauthenticated request rejected; actual Chromium displays markup literally and downloads matching export |
| Missing/hidden Identity handling | Implemented / verified | Real SQL reader returns null, leaves no tracked entities; handler rejects missing Identity; active-account JWT checks retained |
| Cancellation and read/partial-page/archive failure | Implemented / verified | Initial/later-page cancellation and failure, archive exception, explicit incomplete archive, and HTTP 500 without partial export payload covered |
| Physical deletion of all owned SQL/Identity dependents | Implemented / verified | Real authenticated deletion checks Domain rows, connections, signals, digests, recommendations, claims/logins/role memberships/tokens; prior hidden-row tests also pass |
| Access and refresh invalidation | Implemented / verified | Previously valid JWT rejected after deletion; refresh state deleted and validation fails |
| Old unsubscribe capability after deletion | Implemented / verified | It still decrypts under the retained shared key ring to the deleted ID, but its database update is a no-op and cannot affect the other account; no cryptographic token-revocation claim |
| Shared catalog/roles/keys and other account preservation | Implemented / verified | Other account rows, credential dependents and original encrypted copy remain; Tracks/Artists/Albums, roles/role claims and keys retained |
| SQL rollback on credential failure | Implemented / verified | Injected credential failure preserves all owned rows, valid access/refresh and exact encrypted copies |
| Post-commit file failure and cleanup retry | Implemented / verified | Corrupt fabricated partial produces accountDeleted=true/localCopiesDeleted=false; access invalid; other copy compared before DELETE, immediately after, and after explicit cleanup retry |
| Capture/publication/cancellation/deletion failure windows | Verified existing behavior | Actual Worker and archive regression checks retained and run; local capture remains distinct from external delivery |
| Account-specific generation-use manifests/operator files | Documented boundary; automatic erasure not implemented | Developer-supplied permission file contains account association; operator must remove it and its copies explicitly; shared upstream recordings are not blanket-deleted |
| Production logs, backups, operator previews and out-of-root copies | Not inspected / not erased | Inventory records actual format/writer boundaries and restore resurrection risk; no universal erasure or legal-compliance claim |
| Atomic concurrent SQL/filesystem export and production maximum history | Deferred | Existing separate reads/full response allocation remain; quiescent complete-history tests do not prove atomicity or production capacity |
| New schema, packages, retention, scorer, live calls or external sending | Not done, by approved scope | No migration/package change; popularity remains neutral and synthetic mechanics do not validate recommendation quality |
| Separate 8a verification | Passed locally | 70 passed, 0 failed/skipped |
| Whole-solution regression check | Passed locally | 417 passed, 0 failed/skipped; all local PostgreSQL checks enabled |
| 8b and final combined Phase 8 run | Not implemented / not run | [Concrete 8b design](phase-8b-review.md) awaiting review; the whole-solution 8a run is not a combined 8a/8b result |

## Implementation and commit boundaries

- `4c83713`: explicit storage inventory and model/migrated-schema guards.
- `a02365d`: safe Identity account export 2.2, Application/DataAccess wiring,
  strengthened export assertions and current QA/version documentation.
- `c37ccc1`: authenticated scalar/history/deletion/copy API checks, independent
  disposable connection pools, accurate privacy page wording and API documentation.
- Final review coverage follow-up: later-page/archive/HTTP failure tests, complete
  populated snapshot equality and foreign-copy comparison before failed cleanup.
  No additional product behavior was needed for these review findings.

No unrelated `.serena/` files were changed or included. No push, merge or public
deployment was performed. Existing .NET 10 / EF Core 9.0.2 packages remain; tooling
version/package-pruning warnings are existing limitations, not an authorized upgrade.

## Exact verification

The local PostgreSQL administrator setting was supplied through the environment;
each fixture created and dropped a uniquely named disposable database. No real
application database or live provider acquisition was used.

Separate 8a command, with `LINER_PHASE1_POSTGRES` enabled:

```bash
dotnet test 'Liner Notes.sln' --no-restore \
  --filter 'FullyQualifiedName~Phase8Privacy|FullyQualifiedName~GetUserDataExportTests|FullyQualifiedName~ExportControllerTests|FullyQualifiedName~Phase1SecurityTests|FullyQualifiedName~Export_PagesAllHistory|FullyQualifiedName~AccountDeletion_RemovesPhase6|FullyQualifiedName~StorageStop_LeavesExistingDigestFeedbackExportAndDeletionAvailable|FullyQualifiedName~LocalWorkerPostgresTests|FullyQualifiedName~LocalUnsubscribePostgresTests|FullyQualifiedName~LocalEmailTests|FullyQualifiedName~UnsubscribeRouteTests' \
  --logger 'console;verbosity=normal' -m:1
```

Result: DataAccess 16, Application 10, Presentation 22, Infrastructure 22: **70
passed, 0 failed, 0 skipped**. Domain has no matching checks in this targeted run;
its 105 checks were included in the whole solution run below.

```bash
dotnet test 'Liner Notes.sln' --no-restore --logger 'console;verbosity=quiet' -m:1
```

Result: Domain 105, DataAccess 57, Application 95, Presentation 54, Infrastructure
106: **417 passed, 0 failed, 0 skipped**.

Initial inventory and version regression runs failed for deliberately missing
PhoneNumber and export version 2.1 before their reviewed changes. Sandbox test
sockets required escalation. An earlier broad run lost database connections; a
later broad run encountered an existing fail-closed `database_inventory_changed`
capture stop. New disposable fixtures were isolated from unrelated connection
pools; subsequent separate/full runs passed with all concurrency assertions intact.
The exact cause of the earlier inventory stop was not established; no guard was
weakened and no claim of production stability follows from the final runs.

The actual browser ran the existing Development QA launcher against a disposable
QA database. Playwright MCP could not initialize its configured absent Chrome.
Existing local Playwright/Chromium successfully verified registration, export 2.2,
literal markup with zero executable children/script, corrected privacy exclusions
and downloaded-content equivalence (excluding the independently generated export
timestamp). This is automated browser verification, not developer confirmation.

Rerun locally using [the existing QA launcher](phase-7-local-run.md): inspect the
Your Data & Privacy tab and download 2.2; its `account` values are independent of
`subscriber`. Do not paste real credentials, capabilities or exports into reports.

## Fresh review and regression proof

One fresh gpt-6-astra reviewer inspected the approved plan/spec and implementation
range `dc83fc9..c37ccc1`. It found no production defect, but three Important
coverage gaps: partial-progress/HTTP failures, foreign-copy baseline placement,
and complete snapshot-to-export equality. All three were closed in one pass.

Temporary mutations deliberately swallowed later-page failures/cancellation,
skipped archive reads, dropped snapshot evidence and deleted foreign copies.
The new checks failed: four Application cases and three API cases. Original
production files were restored byte-for-byte before the final 70/417 green runs;
no mutation was committed. This proves regression detection for existing behavior,
not a claim that these production defects originally existed.

Deferred minor: the existing missing-Domain-user unit test stubs `GetByIdAsync`
rather than the called `GetForExportAsync`, relying on substitute defaults. The
real missing/hidden Identity and API authorization checks pass. The reviewer
declined production scale/cross-store atomicity/backups/external delivery (outside
scope), independently rerunning the browser, and 8b completion (future scope).

## Execution rulings

1. Use the user-selected Prism source checkout, not a new worktree. This is not
   main/master; status was checked before commits. Cost if wrong: interference
   from concurrent unrelated edits.
2. Use native bookkeeping in `/tmp/liner-phase8-work` because the fetched workflow's
   scripts were absent. Cost if wrong: less portable helper automation; the
   evidence and commit history preserve the actual steps.
3. Inventory migration history separately from EF-mapped entities. Cost if wrong:
   an incomplete claim about operational database storage.
4. Scratch file-based EF introspection requires PublishAot=false under the current
   SDK. Product settings were untouched. Cost if wrong: broken scratch tooling.
5. API fixtures call Application capture and Infrastructure reconciliation directly
   because Presentation.Tests does not reference Worker. Actual Worker regressions
   run separately. Cost if wrong: missed process wiring, bounded by existing Worker
   composition checks and actual hosted-browser verification.
6. New fixtures disable only their own pooling instead of clearing every process
   pool. Cost if wrong: slower setup; concurrency assertions remain intact.
7. Close review coverage gaps using wrong temporary mutations against already-correct
   behavior instead of inventing product changes. Cost if wrong: uncovered regressions;
   observed failures and subsequent complete green runs supply the evidence.

## Remaining gates

Developer confirmation of 8a and previous phases remains pending. Operator-managed
permission files/copies, real logs/backups, protection-key access/wrapping, backup
expiry/restore procedure, concurrent point-in-time export, production capacity and
recommendation quality are not established by these fixtures.

Next: review the [bounded local UTC 8b design](phase-8b-review.md), then its written
implementation plan. Implement and verify 8b separately and finally run the
combined privacy/delivery scenario and full solution as requested. No new live
acquisition, external email transport or production readiness is inferred.
