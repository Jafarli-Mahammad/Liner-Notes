# Phase 7 implementation plan

Prepared 2026-10-11 against source-checkout commit `a698f05`.
The developer approved the [written design](phase-7-review.md) with the reply
“approved”, then approved this plan and sequential execution in this checkout.
All tasks are now implemented and locally verified; developer confirmation remains pending.
See [current evidence](phase-7-evidence.md) and [runnable QA](phase-7-local-run.md).
The status table and task descriptions below preserve the approved planning snapshot.

Recommended execution: sequential work in the current source checkout on `Prism`,
with focused local commits after the relevant checks. No delegation is needed.
The brainstorming handoff's `writing-plans` skill is unavailable: both installed
skill roots and the plugin skill catalog were searched. This document follows the
approved design and the repository's prior written-plan structure directly.
The modern-csharp-coding-standards and efcore-patterns skills informed this plan.

## Status and scope

| Item | Status and reason |
|---|---|
| Phase 7 written design | Approved, including persistent local unsubscribe, one SQL column, API/export consequences and LocalCaptured state. |
| Phase 7 product code | Not done; this plan is awaiting review. |
| Phase 6 generator, snapshots, selection, feedback and export 2.0 | Present in this checkout; existing local evidence remains separate from developer confirmation. |
| Phase 6 status/cutoff and manual QA follow-ups | Implemented in another worktree, absent here; reconcile their already authorized scope in Task 0. |
| Popularity setting and associated schema/settings/export | Deferred under the earlier instruction to wait; excluded from reconciliation and Phase 7. |
| Live acquisition and real-data generation | Require exact applicable recording approval; the demonstration uses fabricated fixtures only. |
| Scheduling, external email, broad privacy verification and release | Retain their Phase 8/9 approvals; no automatic continuation. |

Expected effort: one prerequisite reconciliation and four Phase 7 implementation
commit sets, with integration checks across all six existing layers. Tests and
verification below are required by AGENTS.md for the authorized implementation;
no tests have been added or run while writing this plan.

## Task 0: Reconcile the existing authorized Phase 6 follow-ups

Audit the complete diff from `886c432` to `ce0e8d8`, not the diff from current HEAD
to that commit: the latter also removes the current Phase 7 design. The existing
follow-ups are `63f2b4a`, `32b661f`, `bd30918`, `86a06b3`, `dd573c1`, `f802b2a`,
`5ed3d1b` and `ce0e8d8`.

Files to reconcile:

- `src/Infrastructure/DependencyInjection.cs` and
  `src/Infrastructure/Storage/LocalGenerationStorage.cs`.
- `src/Presentation/Program.cs`,
  `src/Presentation/Development/ManualTestEndpoints.cs`, and the three existing
  follow-up assets under `src/Presentation/wwwroot/manual-test/`.
- `tests/Infrastructure.Tests/GenerationStorageTests.cs` and
  `tests/Presentation.Tests/ManualTestEnvironmentTests.cs`.
- `scripts/run_manual_qa.sh`, `docs/renewal/manual-qa.md`, and the prior manual-QA
  design/plan artifacts under `docs/superpowers/`.
- Phase 6 evidence/review, contracts and PROGRESS status, preserving this Phase 7
  design, its approval and this plan.

Apply the audited changes without creating commits before verification. Preserve
the 1,000,000,000-byte cutoff as **temporary/unresolved**, with the real value left
to build-order step 9 after hosting selection. Preserve the earlier deferred
popularity setting and all Production 404/authentication boundaries in manual QA.

Verification: storage-boundary and ManualTestEnvironmentTests, shell/JavaScript
syntax checks, existing documentation/security checks, then the existing full
suite with explicitly local disposable PostgreSQL. Use the recorded 341-test
result only as a historical comparison; report the actual current result.

Focused commit: `chore(phase6): reconcile authorized storage and manual QA follow-ups`.

## Task 1: Persistent local unsubscribe and account data boundaries

Primary files:

- Modify `src/Domain/Digest/User.cs`, `src/Domain/Digest/WeeklyDigest.cs` and
  `src/Domain/Enums/DigestStatus.cs`.
- Modify `src/DataAccess/Persistence/Configurations/UserConfiguration.cs`; generate
  `AddEmailUnsubscribedAtUtc` under the existing migrations directory using the
  installed EF CLI, including its generated designer/snapshot changes.
- Add `UnsubscribeUserCommand`/handler under
  `src/Application/Features/Subscribers/Commands/UnsubscribeUser/` and its
  Application token/store contracts under `Common/Interfaces/`.
- Add the purpose-isolated token implementation in `src/Infrastructure/Email/`
  and a dedicated persisted-key provider in `src/DataAccess/Persistence/`.
- Add an explicit `FrameworkReference` to `Microsoft.AspNetCore.App` in
  `src/Infrastructure/Infrastructure.csproj` for its Data Protection types. This
  uses the already targeted .NET 10 shared framework, not a new package/version.
- Add `src/Presentation/Controllers/UnsubscribeController.cs`; wire the dedicated
  provider and local-email options in both composition roots.
- Modify `SubscriberDto`, mapping, `UserDataExportDto` and its query handler;
  update the authenticated account inspection UI and existing deletion workflow
  only where needed for the new local-email data.

Implementation steps:

1. Add nullable `EmailUnsubscribedAtUtc` with an idempotent UTC opt-out method.
   Append `LocalCaptured = 4`, preserving all existing enum values; add an explicit
   local-capture transition with `SentAt == null`. Correct affected comments that
   equate a unique digest row with exactly-once external delivery.
2. Generate the one-column additive migration via `dotnet ef migrations add`,
   using DataAccess and Presentation as project/startup-project. Inspect its diff:
   only the nullable Users timestamp column and generated model metadata may
   change. Do not manually author migration SQL or apply it to the app database.
3. Create a dedicated unsubscribe Data Protection provider using the existing
   DataProtectionKeys table and a distinct application discriminator/purpose.
   Isolate its registration from Identity/JWT. Infrastructure implements the
   Application token interface; DataAccess supplies the persisted-key adapter.
   Use a single lazily owned provider with explicit disposal, not an ephemeral
   provider per request. Use the explicit existing-shared-framework reference
   above; no new NuGet package or framework version is required.
4. Enable the flow only with explicit Development/local-email configuration and
   a literal loopback application origin. GET validates a bounded capability and
   returns a confirmation form; POST validates it and executes idempotent opt-out.
   Return generic results, avoid account-detail disclosure, and redact token query
   strings before request logging. GET has no mutation. Use no external resources.
5. Add the opt-out field to profile/data inspection and export **2.1**. Introduce
   `ILocalEmailArchive` for the account's captured copies: list/inspect sanitized
   message content and delete account-owned files. A no-capture account can export
   an empty collection; missing/corrupt captured data must be reported as incomplete,
   not silently converted to an empty collection. Cap scans and per-file reads.
6. Keep SQL/Identity deletion within its existing transaction. Perform filesystem
   cleanup after SQL commit, outside EF's automatic retry callback. Capture and
   deletion coordinate on account ownership; cleanup can be retried by the local
   operator if SQL deletion succeeded but file cleanup failed. API/UI results must
   distinguish that incomplete cleanup from complete copy deletion. Public export
   excludes the protected capability, protection keys and secret-bearing headers.

The archive and deletion integration use the sink contract implemented in Task 2.
Do not claim that an adapter-only test verifies filesystem cleanup; finish the
real-file cases before completing the Phase 7 evidence pass.

Tests: add focused Domain opt-out/lifecycle tests; PostgreSQL populated-migration
and idempotent opt-out tests; token cross-host/restart/tampering tests; local route
GET/POST/Production boundary tests; profile/export 2.1 and deletion-result tests.
Keep existing cases, updating only reviewed additive expectations. Introduce
test doubles for new Application contracts where needed for existing unit tests.

Focused commit: `feat(phase7): add persistent local unsubscribe and account data boundaries`.

## Task 2: Render persisted digests and capture bounded MIME files

Primary additions under `src/Infrastructure/Email/`:
`LocalEmailOptions.cs`, `DigestEmailRenderer.cs`, `LocalEmailMessageSerializer.cs`,
`LocalEmailSink.cs` and `LocalEmailArchive.cs`. Add their immutable message/result
records and interfaces under `src/Application/Common/`; register implementations
through the existing Infrastructure DI extension.

Implementation steps:

1. Define renderer inputs from the persisted digest/profile and configured link
   settings. Render in persisted rank order, using `WhyThisPick` from the stored
   breakdown. Explicitly support 0, 1, 3 and 5 picks. Escape all text/attributes,
   bound URLs, reject header control characters, and validate allowed URL schemes.
2. Produce UTF-8 plaintext and HTML alternatives containing the actual list size,
   complete readable explanations, attribution, ordinary links and functional
   local unsubscribe. Use inline CSS and system fonts with no scripts, remote
   images/fonts/CSS, tracking resources or embedded players.
3. Serialize multipart/alternative with plaintext first and HTML second, proper
   CRLF/encoded parts and a stable digest-derived Message-ID. Enforce configurable
   100,000-byte bounds per alternative and 400,000 total serialized MIME bytes.
   Use existing .NET primitives; the format is local capture, not a new SMTP client.
4. Compute a versioned render-input fingerprint over account/recipient, digest/week,
   ordered track and stored explanation data, and template/link settings. Exclude
   lifecycle, feedback and audit fields. Validate the embedded unsubscribe
   capability against the same owner independently during recovery.
5. Require an explicit ignored sink root counted under `copies`, with no overlapping
   roots, symlinked ancestors, tracked targets or path escapes. Create bounded unique
   partial files in the same filesystem, flush, and publish `<digest-id>.eml`
   without overwrite. Validate any existing final file before accepting it as the
   durable receipt. A conflict/truncated file stops; it is never silently replaced.
6. Implement bounded ownership-based archive inspection/export/removal. Derive
   ownership from validated message metadata; do not delete another account's file
   or guess ownership of corrupt files. Sanitize capabilities from public message
   export. Report missing/corrupt/undeletable owned copies explicitly.
7. Count exact serialized bytes and outstanding partials in the existing storage
   categories. Introduce the Application storage reservation contract needed by
   local writes and reuse the exclusive artifact lease. Check revisions and the
   temporary threshold before publication; partials remain counted after failure.
   Track only the active reservation's own partial/final file changes, validating
   every other file against the baseline. Do not refresh the operator inventory
   or accept arbitrary artifact changes as part of a retry.

Tests: HTML escaping and header/URL injection, Unicode MIME decoding, no automatic
remote resources, list sizes, stable fingerprints after feedback/state changes,
same-file recovery, conflicts, ownership, symlinks, oversized files, exact storage
boundaries, failed-partial accounting and actual archive/deletion cleanup.

Focused commit: `feat(phase7): render and capture bounded local digest messages`.

## Task 3: Worker actions, persistence ownership and restart recovery

Primary files:

- Add `CaptureLocalDigestCommand`/handler under
  `src/Application/Features/Digests/Commands/CaptureLocalDigest/`.
- Add `ILocalDigestCaptureStore` and its DataAccess implementation under the
  existing interface/persistence areas; update DataAccess DI.
- Modify `src/Worker/Program.cs`; add `LocalWorkerOptions.cs` and
  `LocalDigestWorkerRunner.cs` in the existing Worker project.
- Add a bounded explicit recorded-input adapter under
  `src/Infrastructure/RecommendationSources/LastFm/`.
- Add exactly the Worker reference to `tests/DataAccess.Tests/DataAccess.Tests.csproj`
  for composed integration checks; no new test project/package.

Implementation steps:

1. Expose explicit one-shot `reconcile`, `generate` and `capture` actions. Validate
   user/week/origin and all local paths/options before writes; exit with bounded
   success/stop/cancellation codes. No idle or periodic loop on missing arguments.
   Generate calls the existing MediatR command. Capture loads an existing digest;
   it does not reacquire provider data or rescore it.
2. Document and exercise the operator sequence: reconcile, generate, reconcile,
   capture. The second reconciliation is necessary because generation changes the
   database inventory revision. Reconciliation is an explicit action, not an
   automatic refresh inside generation or capture. An empty digest follows the
   same persistence/render/capture flow.
3. Use explicit Synthetic fixtures for the demonstration. Recorded mode requires
   exact manifest/input paths, an independently supplied approved digest, expiry,
   origin and response hashes, and explicit permission for this generation use.
   Enforce bounded reads, containment and no symlinks/discovery; reject held-out
   or evaluation-only data, unauthorized use, mixed origins and credential data.
   Adapt only validated immutable responses to the existing replay client. An
   unavailable suitable approval is a stopped result, never a fixture substitution.
4. Use a distinct per-account capture advisory lease, with lock ordering documented
   as ownership lease, artifact lease, then the final account-row transaction.
   Release generator ownership before starting capture. Serialize attempts and
   coordinate deletion without recursively acquiring generation's batch lock on
   another connection. Always release session/file leases after cancellation.
5. Check account existence/opt-out before each attempt and under a row lock just
   before publication. SQL retryable state operations use fresh scopes and never
   contain filesystem publication inside `ExecuteInTransactionAsync` retry logic.
   Use an owned capture-session DataContext with automatic transaction replay
   disabled for the transaction spanning the row lock and publication; the
   orchestrator handles bounded fresh-session recovery. Generation retains its
   existing retry configuration. Avoid an InProgress write before inventory
   validation, which would invalidate the fresh database revision. Coordinate
   the final eligibility check with unsubscribe and deletion.
6. Verify an existing final file before reconciling LocalCaptured after a restart.
   LocalCaptured with a missing/conflicting file is an integrity stop. Never call
   MarkSent for local capture. Store bounded reason codes in the existing error
   field, excluding recipients, paths, tokens and raw exceptions.
7. Retry transient local I/O at most three times with cancellable 250 ms/1 s waits.
   Permanent validation/configuration/integrity failures stop immediately. If
   cancellation occurs after publication, recovery validates the artifact before
   updating state; it must not publish a second completed message.

Tests: command options, generation/capture boundaries, actual Worker composition,
repeated and concurrent capture, missing/conflicting receipts, failure before/during/
after publication and before/after DB commit, opt-out/deletion races, cancelled leases,
safe retry reasons, expired/changed/unauthorized recordings, and rejecting network
handlers even when Last.fm credentials exist. Use disposable local PostgreSQL for
locks/transactions, not EF InMemory as evidence of concurrency correctness.

Focused commit: `feat(phase7): run offline worker capture with restart recovery`.

## Task 4: Demonstrate, verify and reassess the complete local slice

Add `docs/renewal/phase-7-evidence.md` and an operator run procedure; update
`CHANGELOG.md`, PROGRESS and contracts. Extend the reconciled manual-QA instructions
for profile opt-out/export visibility and the local confirmation flow.

Use a unique disposable local database and fabricated subscriber/fixture inputs.
Run the actual Worker composition through reconcile/generate/reconcile/capture,
inspect both MIME alternatives, restart/repeat and exercise a publication-before-
status-update recovery. Use Playwright MCP for local unsubscribe confirmation;
verify opt-out suppresses subsequent capture and account deletion removes owned
copies or reports its exact cleanup failure. Keep fixture messages/recordings and
all reports outside tracked source. Measure MIME and aggregate category bytes.

Relevant verification commands, after implementation and restored assets:

```bash
dotnet test tests/Domain.Tests/Domain.Tests.csproj --no-restore
dotnet test tests/Application.Tests/Application.Tests.csproj --no-restore
dotnet test tests/Infrastructure.Tests/Infrastructure.Tests.csproj --no-restore
dotnet test tests/DataAccess.Tests/DataAccess.Tests.csproj --no-restore
dotnet test tests/Presentation.Tests/Presentation.Tests.csproj --no-restore
dotnet test 'Liner Notes.sln' --no-restore
python3 scripts/checks/renewal_docs.py
python3 scripts/checks/secret_defaults.py
bash -n scripts/run_manual_qa.sh
node --check src/Presentation/wwwroot/manual-test/manual-test.js
git diff --check
```

Run focused checks as each commit set becomes reviewable, then the full suite once
after the final integrated changes. `LINER_PHASE1_POSTGRES` must identify only an
explicitly local disposable administrator connection; tests create/drop their own
databases. If assets are missing, restore before using `--no-restore`. Never print
credentials. Use Rider MCP for affected C#/migration/DI diagnostics and codebase
MCP for navigation, checking indexed-path coverage against source.

Document the exact scope implemented/deferred/not done, local test results, file/SQL
failure windows, synthetic storage measurements, remaining Last.fm attribution/
territorial/public-page questions, tag-scope limitations, quality/capacity/cost
uncertainty and complexity. **Mechanics only, no catalog conclusions.** End with
the mandatory explicit proceed/narrow/revise decision before later phases.

Focused commit: `docs(phase7): record local worker email evidence and reassessment`.

## Review and commit rules

Every task maps to the approved design. No TODO placeholders or unreviewed retention,
formula, package/provider or scheduling choices are introduced. Exact added schema,
routes, export version, byte bounds and default retry policy match that design.
Review file-publication/SQL failure windows and token logging as implementation
risks; synthetic checks establish mechanics only.

Before each focused local commit, inspect changed paths and run its relevant checks.
Do not weaken existing tests to make the changes pass. If checks fail or cannot
run, report the result and ask before committing implementation. Keep developer
confirmation distinct from verification. No history rewrite or push is authorized.

**Execution handoff completed:** the developer approved this plan and sequential execution.
Tasks 0–4 were implemented with focused local commits. Confirmation and the explicit
proceed/narrow/revise decision remain pending; later phases retain separate approvals.
