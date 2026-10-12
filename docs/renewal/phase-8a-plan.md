# Phase 8a Privacy Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Verify complete reviewed account-data export and physical deletion, and add the approved safe Identity profile to export 2.2.

**Architecture:** Application owns the export DTO and account-profile reader interface; DataAccess projects stored Identity fields. Existing authenticated export/deletion routes and encrypted local-copy services remain the integration boundary. Tests audit the complete relational model and exercise disposable PostgreSQL, HTTP and filesystem behavior.

**Tech Stack:** Existing .NET 10 projects, installed EF Core/Npgsql packages, MediatR, xUnit, NSubstitute and WebApplicationFactory; no new package or project.

**Spec:** [Approved Phase 8a design](phase-8a-review.md), approved by the developer on 2026-10-12.

**Status:** Written plan awaiting review. Execute sequentially in the current session; the developer's order is preserved: 8a → separate 8a tests → 8b → separate 8b tests → combined checks. This plan does not approve a new 8b schema/API/delivery contract.

## Global Constraints

- Export `2.2` adds only the approved `account` profile fields; existing fields and score JSON remain unchanged.
- No schema migration, dependency upgrade, scoring change, retention policy or new settings endpoint.
- Account/credential records are physically deleted; shared catalog, keys, other accounts and backup boundaries are preserved/documented.
- Credential/security internals, encrypted provider tokens and raw unsubscribe capabilities stay outside public export.
- No live acquisition, external sending, public deployment, purchase, provisioning or remote push.
- Synthetic fixtures and local verification establish mechanics only; developer confirmation remains separate.
- No claim of atomic SQL-plus-filesystem export or erasure of backups/out-of-root copies.
- Preserve existing tests and strengthen the explicitly reviewed export-version assertions.

## Review Focus

Each item is pinned to a named test below as well as reviewed against current source:

1. Identity values differ from Domain subscriber values or contain nulls: export stored Identity values without synthesizing them (Task 2).
2. More than two history pages share identical timestamps and contain hidden records: no skipped or duplicated account rows (Task 3).
3. New mapped storage appears, including a shadow property: the explicit coverage contract rejects unreviewed columns (Task 1).
4. Deletion commits SQL but a corrupted copy cannot be authenticated: revoke access and report incomplete file cleanup without deleting another account's copy (Task 3).
5. Export cancellation or downstream read failure: fail the request rather than return a successful partial export (Tasks 2/3).

## Verification environment

Use `LINER_PHASE1_POSTGRES` supplied through the environment, never a real application database. Existing fixture patterns create uniquely named disposable databases and require loopback PostgreSQL. Do not print secrets or connection strings. If a disposable local PostgreSQL instance is needed, use the existing approved Docker tooling, bind loopback, and remove only task-created resources after verification.

Restore once if required: `dotnet restore 'Liner Notes.sln'`.
The commands below require the local PostgreSQL environment for all `[PostgresFact]` / `[LocalPostgresMigrationFact]` cases. A skipped database check is not a passed privacy verification. Use sandbox escalation if socket access prevents a required check. Record commands, passed/failed/skipped counts and limitations in the evidence document.

## Task 1: Explicit storage inventory and relational coverage

**Files:**
- Create: `docs/renewal/phase-8a-inventory.md`.
- Create: `tests/DataAccess.Tests/Phase8PrivacyInventoryTests.cs`.
- Read: `src/DataAccess/DataContexts/DataContext.cs`, Identity/domain entities, entity configurations, migrations, `AuthService`, local recorded-input loader, storage inventory and local-copy services.

**Interfaces:**
- Consumes: EF relational metadata from an Npgsql-configured `AppDbContext` and disposable migrated PostgreSQL `information_schema.columns`.
- Produces: an explicit per-table/per-column contract with export paths or individual exclusion reasons and physical-deletion/shared boundaries. A test-only `ReviewedColumns` map supplies exact schema/table/column identities; it is not generated from the model at assertion time.

- [ ] **Step 1: Enumerate current persisted columns and audit writers.** Document every scalar column, including inherited Identity/audit columns, shadow `TrackId`, score JSONB and EF migration history. Audit claims/logins/roles/token writers for personal payloads. Audit generation-use manifests (`UserId`, week), recordings, inventories, receipts, partials and logs. Distinguish configured online ownership from operator-managed files and backups; do not declare the latter erased.
- [ ] **Step 2: Write the coverage tests.** `ReviewedInventory_CoversEveryRelationalTableAndColumn` asserts exact equality between normalized metadata identities and the explicit contract. `CoverageCheck_RejectsAddedColumnOrTable` feeds a synthetic extra identity to the same comparison and asserts rejection. `MigratedSchema_MatchesReviewedInventory` compares the disposable migrated database schema, including `__EFMigrationsHistory`, with the reviewed contract.
- [ ] **Step 3: Verify the contract fails for an intentionally omitted mapped identity**, then restore that reviewed entry and run the inventory checks. An unexpected migration/model discrepancy is a finding, not permission to add a migration.
- [ ] **Step 4: Document all export exclusions and deletion boundaries.** Include every credential/security column individually. Explain shared Artists/Albums, referenced Tracks, protection keys, authorization configuration, backups and restore resurrection. Distinguish a table inventory from proof that its exported values are correct; Task 3 provides that proof.
- [ ] **Step 5: Run and commit the independently reviewable inventory.** Run `dotnet test tests/DataAccess.Tests/DataAccess.Tests.csproj --no-restore --filter 'FullyQualifiedName~Phase8PrivacyInventoryTests|FullyQualifiedName~EfModel_StillMatchesExistingSqlMigrationSnapshot' --logger 'console;verbosity=normal' -m:1`. Require zero failures and skips, then create a focused local inventory/test commit.

## Task 2: Safe Identity profile export 2.2

**Files:**
- Create: `src/Application/DTOs/Export/AccountExportDto.cs`.
- Create: `src/Application/Common/Interfaces/IAccountProfileReader.cs`.
- Create: `src/DataAccess/Persistence/Repositories/AccountProfileReader.cs`.
- Modify: `src/DataAccess/DependencyInjection.cs`.
- Modify: `src/Application/DTOs/Export/UserDataExportDto.cs`.
- Modify: `src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQueryHandler.cs`.
- Modify: `tests/Application.Tests/Features/Export/GetUserDataExportTests.cs`.
- Modify: `tests/DataAccess.Tests/Phase6PostgresTests.cs`, `tests/DataAccess.Tests/LocalWorkerPostgresTests.cs` (retain existing assertions; version becomes `2.2`, add account assertions).
- Modify: `CHANGELOG.md`, `docs/renewal/manual-qa.md`, `docs/renewal/phase-7-local-run.md` (current workflow references only; historical evidence remains historical).
- Verify existing viewer: `src/Presentation/wwwroot/app.js:loadYourData`, which renders the complete JSON through `textContent`.

**Interfaces:**
- Produces: `IAccountProfileReader.GetAsync(Guid userId, CancellationToken cancellationToken = default) : Task<AccountExportDto?>`.
- Produces: `AccountExportDto(Guid Id, string? UserName, string? NormalizedUserName, string? Email, string? NormalizedEmail, bool EmailConfirmed, string? PhoneNumber, bool PhoneNumberConfirmed, DateTime CreatedAt, Guid? CreatedBy, DateTime? LastModifiedAt, Guid? LastModifiedBy, DateTime? DeletedAt, Guid? DeletedBy, bool IsDeleted)`.
- Produces: append `AccountExportDto? Account = null` to `UserDataExportDto` for DTO source compatibility; successful handler exports always include the account.
- Consumes: required `IAccountProfileReader` as the fourth handler constructor argument, before optional `ILocalEmailArchive`. DataAccess registers the concrete reader scoped, for both composition roots.

- [ ] **Step 1: Add failing handler cases.** `Handle_ExportsStoredIdentityValuesIncludingNulls` returns a profile whose username/contact values differ from the Domain row; assert equality of the complete profile and export `2.2`. Preserve all current connection/taste/history assertions. `Handle_RejectsMissingIdentityProfile` expects `NotFoundException`. `Handle_PropagatesCancellationAndReadFailure` supplies cancelled/throwing reader/repository calls and asserts no DTO is returned. Update both existing direct handler constructions with the required substitute.
- [ ] **Step 2: Run the export unit tests and record the expected missing-contract/behavior failure.** Command: `dotnet test tests/Application.Tests/Application.Tests.csproj --no-restore --filter FullyQualifiedName~GetUserDataExportTests --logger 'console;verbosity=normal' -m:1`.
- [ ] **Step 3: Implement the reviewed DTO, reader and wiring.** Project only the listed columns from `ApplicationUsers` by ID with `!IsDeleted`, using async NoTracking reads and the caller cancellation token. Check Domain and Identity existence before producing the export. Do not read passwords, tokens or security stamps into the DTO. Set `ExportVersion` to `2.2` and pass the stored profile into `Account`.
- [ ] **Step 4: Preserve all existing export fields and version checks.** Update the two existing PostgreSQL version assertions from `2.1` to `2.2` and assert account ID/profile values without dropping any history, snapshot, capability-sanitization or encryption assertions. Audit all direct constructor references with `rg` before building.
- [ ] **Step 5: Update changelog and runnable QA text.** Explain the added safe profile section and individual exclusions. Existing `textContent` already renders the entire new section; change the viewer only if verification demonstrates a gap within the approved behavior. Keep backup/legal claims accurate.
- [ ] **Step 6: Run and commit.** Run Task 2 unit checks and `dotnet test tests/DataAccess.Tests/DataAccess.Tests.csproj --no-restore --filter 'FullyQualifiedName~Phase6PostgresTests|FullyQualifiedName~LocalWorkerPostgresTests' --logger 'console;verbosity=normal' -m:1`. Require zero failures/skips before the focused export commit. Storage-stop/export and encrypted-copy checks must still pass.

## Task 3: Persisted scalar export, complete physical deletion and access invalidation

**Files:**
- Create: `tests/Presentation.Tests/Phase8PrivacyApiTests.cs`.
- Extend if needed: `tests/DataAccess.Tests/Phase8PrivacyInventoryTests.cs` and Task 2 export tests.
- Reuse: `Phase1SecurityTests`, `LocalWorkerPostgresTests`, `LocalUnsubscribePostgresTests`, `LocalEmailTests` and `UnsubscribeRouteTests`.
- Modify only upon a demonstrated defect: existing export/deletion/account/archive implementations, within the approved contract.
- Create: `docs/renewal/phase-8a-evidence.md`.
- Update: `PROGRESS.md` and inventory evidence links.

**Interfaces:**
- Consumes: Task 1's column/export-path contract, Task 2's account reader/export, actual authenticated `GET /api/export/my-data` and `DELETE /api/subscribers/me`, existing `ILocalEmailArchive` and unsubscribe APIs.
- Produces: separate 8a evidence and reusable API tests that can be included in the final combined Phase 8 run. Use existing disposable-database patterns, FixtureOnly/Synthetic generation, private temp sink roots and real encrypted-copy services.

- [ ] **Step 1: Implement `Export_AllReviewedScalarsMatchPersistedValues_ExcludesSecretsAndOtherAccounts`.** Seed distinctive profile/contact/schedule/opt-out/connection/taste/digest/recommendation/track/feedback/audit values for two accounts. Read back persisted values after audit interception and timestamp precision normalization, then compare each exported scalar at its reviewed JSON path. Store credential sentinels and assert their values and field names are absent from serialized API output. Verify unknown score fields and complete snapshots, nulls, hidden rows and correct nested `TrackId`. Reject unauthenticated requests and verify a foreign `userId` query cannot change the authenticated account.
- [ ] **Step 2: Implement `Export_AllHistoryPagesIncludeHiddenRowsAndTimestampTies`.** Seed 205 distinct weeks with equal `CreatedAt`, hidden digests/recommendations and a second account; assert exactly 205 distinct digest IDs and every owned hidden recommendation. Preserve the existing 1,005-history test. Cancellation and repository/archive failure tests must not produce successful truncated exports; explicitly documented incomplete archive inspection remains allowed.
- [ ] **Step 3: Implement `Delete_AllOwnedTablesAndEncryptedCopies_RevokesEveryCapability_PreservesOtherAccountsAndSharedData`.** Seed Domain owned tables and Identity claims/logins/roles/tokens, two private encrypted captures and shared catalog/keys. Delete through HTTP; assert every deleted account-owned row is gone with filters disabled, the other account/copy survives, shared roles/catalog/keys survive, access and refresh tokens fail, and the old unsubscribe capability cannot update any account. Keep the column inventory current if a verified writer reveals a missed store.
- [ ] **Step 4: Implement `Delete_CredentialFailureRollsBackOwnedRowsAndKeepsCopies` and `Delete_CopyIntegrityFailureRevokesAccessAndReportsIncompleteCleanup`.** Inject credential deletion failure in test DI; verify transaction rollback and access/copies remain valid. For post-commit cleanup, use a fabricated corrupt file alongside an authentic owned copy: expect `accountDeleted=true`, `localCopiesDeleted=false` and a bounded issue; verify tokens fail and another account's copy is byte-identical. Remove only the fabricated corrupt file and exercise the explicit cleanup retry after deletion. Reuse publication/cancellation/lease failure-window tests rather than reimplementing them.
- [ ] **Step 5: Verify the browser view and download.** Use the disposable local QA host with Playwright MCP. Assert export `2.2`, profile visibility, matching downloaded JSON, and literal display of a username containing markup without creating executable DOM elements. Use fixture data only and avoid capability/credential dumps. Report tool availability or browser failures explicitly.
- [ ] **Step 6: Run 8a independently.** With local PostgreSQL enabled, run `dotnet test 'Liner Notes.sln' --no-restore --filter 'FullyQualifiedName~Phase8Privacy|FullyQualifiedName~GetUserDataExportTests|FullyQualifiedName~ExportControllerTests|FullyQualifiedName~Phase1SecurityTests|FullyQualifiedName~Export_PagesAllHistory|FullyQualifiedName~AccountDeletion_RemovesPhase6|FullyQualifiedName~StorageStop_LeavesExistingDigestFeedbackExportAndDeletionAvailable|FullyQualifiedName~LocalWorkerPostgresTests|FullyQualifiedName~LocalUnsubscribePostgresTests|FullyQualifiedName~LocalEmailTests|FullyQualifiedName~UnsubscribeRouteTests' --logger 'console;verbosity=normal' -m:1`. Require zero failures/skips and record the actual result without claiming the entire solution was selected by this filter.
- [ ] **Step 7: Write evidence and commit.** Begin with an exhaustive per-approval implemented/deferred/not-done matrix. Include exact inventory, secret exclusions, SQL versus file outcomes, logs/manifest/backup/restore boundaries, browser result, test counts and developer-confirmation limits. Commit verified changes in focused sets; ask before committing if a required check fails or cannot run.

## Handoff to 8b and combined verification

After the separate 8a checks pass, proceed directly to the separately gated 8b concrete design. Preserve the developer's instruction to implement/test it after 8a; do not repeat the phase-order question. No external sending/provider is approved by the 8a spec. Existing local capture recovery can be reused, but its results do not prove external-provider delivery idempotency.

The 8b plan must name its dedicated test filter and extend the inventory/export/deletion checks for any new persistent state. Run that filter separately first. Then run a combined privacy/delivery scenario covering scheduled work → stored recommendations → capture/recovery → export → unsubscribe → suppressed later work → account deletion/access invalidation, followed by `dotnet test 'Liner Notes.sln' --no-restore --logger 'console;verbosity=quiet' -m:1` with local PostgreSQL enabled. Keep 8a, 8b and combined outcomes distinct in final evidence.

## Plan self-review

- Approved account fields, exclusions, missing-account behavior and no-schema constraints map to Tasks 1/2/3.
- Every stored scalar, soft-delete filters, complete history, unknown score JSON, cross-account isolation and cancellation map to Tasks 1/3.
- Actual API deletion, Identity dependents, revoked tokens/capabilities, encrypted files, SQL rollback and post-commit failure/retry map to Task 3 and existing named regression checks.
- Shared catalog/keys, recordings/manifests/logs, out-of-root copies and backup restore limits map to Task 1 inventory and Task 3 evidence; no unapproved erasure/retention policy is introduced.
- Separate 8a, separate 8b and final combined verification are preserved. Interface names/signatures match across tasks. Execution is sequential; no task is marked complete before its check runs.

Planning workflow source: [upstream writing-plans skill](https://github.com/obra/superpowers/blob/main/skills/writing-plans/SKILL.md), read on 2026-10-12 because no local copy was available. Its required written-plan review remains pending; execution order is already selected.
