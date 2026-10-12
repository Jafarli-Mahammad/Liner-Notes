# Phase 9 implementation evidence

Locally implemented and verified, 2026-10-12. [Approved design](phase-9-review.md),
[sequential plan](phase-9-plan.md), [verified version snapshot](phase-9-versions.json).
Developer confirmation remains pending; local verification does not authorize release.

Focused local implementation commits: `04f26da` (dependency compatibility) and
`73108ed` (container packaging/runbook). CI, final evidence and status are recorded
in a subsequent focused local commit; nothing was pushed.

## Approval accounting

| Approved work | Current outcome |
|---|---|
| Latest stable project dependencies/tools | Direct and transitive package versions centrally upgraded and locked; SDK 10.0.401 and EF runtime/tool 10.0.12 verified |
| MediatR licensing exception | Upgraded to Apache-licensed 12.5.0 as explicitly selected by the developer |
| EF 10 compatibility | Existing schema/snapshots preserved; original pending-model and populated migration tests pass |
| Docker/Compose | All targets built; disposable container lifecycle and private build-context checks passed |
| CI and release preparation | Workflow and release checklist implemented; equivalent local checks passed; actual GitHub Actions run not verified |
| Last.fm public-page approval / external delivery / production operations | Not obtained or implemented; remain separate decisions |

## Dependency verification

Stable versions were retrieved from official NuGet flat-container feeds and .NET
release metadata; source URLs and the explicit MediatR exception are in the
version snapshot. SDK 10.0.401 was downloaded from Microsoft's release metadata
URL and SHA-512 verified before extraction to `/tmp/liner-phase9-sdk`. The normal
host SDK 10.0.112 was not replaced. The pinned SDK/runtime images were inspected:
10.0.401 / 10.0.12. PostgreSQL image inspection reports 18.6.

Latest Swashbuckle 10.3.0 failed with latest OpenAPI 3.10.2: a verified
MissingMethodException in IOpenApiRequestBody.get_Content caused HTTP 500. The
developer declined pinning OpenAPI 2.7.5 and chose investigating replacement.
MIT-licensed NSwag.AspNetCore 14.7.1 preserves the Development document/UI routes
and bearer scheme, with no Microsoft.OpenApi dependency. Its new graph is centrally
pinned to stable versions. JSON generation and Production hiding pass.
MediatR 12.5 delegates accept a cancellation token; validation now forwards it.
Four new regression checks verify bearer security, Production hiding and
cancellation forwarding.

An initial upgrade run detected EF 10 primary-key convention drift:
`public.Users.PK_Users` became `PK_Users1`. A generated, unapplied inspection
migration contained only a constraint rename. The official CLI removed that
probe; the original snapshot was restored. Both existing user-table key names
are now explicit, so no SQL/schema migration is necessary. The original model
comparison passes. New cancellation fixtures initially used a private type that
NSubstitute cannot proxy; making the fixture public corrected those two test
failures without changing assertions.

Final upgrade verification: **472 passed, 0 failed, 0 skipped**:
Domain 117, DataAccess 84, Application 109, Presentation 56, Infrastructure 106.
The earlier 471-pass direct-upgrade graph is superseded by this central/NSwag graph.
This includes disposable PostgreSQL 18.6 at a task-owned loopback port, existing
fresh/populated migrations, process recovery, export/deletion and encrypted
capture checks. Command shape with the verified SDK selected in `PATH`:

```bash
dotnet restore 'Liner Notes.sln' --locked-mode
dotnet tool restore
dotnet build 'Liner Notes.sln' -c Release --no-restore -m:1
# Supply LINER_PHASE1_POSTGRES privately for a disposable local test database.
dotnet test 'Liner Notes.sln' -c Release --no-build --no-restore \
  --logger trx --results-directory /tmp/liner-phase9-tests-472 -m:1
```

Logs are `/tmp/liner-phase9-build.log`, `/tmp/liner-phase9-test-472.log`
and the stated TRX directory. A first central-package test command omitted the
PostgreSQL environment setting, so its skipped results were rejected; the final
run includes the required database and zero skips. The new Production Swagger
fixture initially supplied settings too late for startup; explicit early fixture
settings corrected it without modifying application behavior. Five NU1510 package-pruning warnings remain; the
explicit current package references were not removed. Swashbuckle was replaced
for the proven runtime compatibility failure, not an established security exploit;
the developer selected replacement investigation over retaining OpenAPI 2.7.5.
No old migration was rewritten. No provider calls or actual user data were used.

## Containers and local lifecycle

All web, Worker, tools and private-volume initialization targets built against
the immutable stable image pins in the version snapshot. Web and Worker ran as
UID 1654 and had no installed SDK. The initialization process alone uses root
to prepare the dedicated volume; the explicit operator profile uses the SDK
for existing local inspection commands. Inspected artifact directories had mode
700. A disposable build-context probe excluded fabricated environment, encrypted
capture and key files while retaining the shared build inputs.

`python3 scripts/checks/phase9_compose.py` passed these eight groups:

1. Failed migration prevents web startup.
2. Fresh migrations and repeated application are idempotent.
3. Registration, synthetic generation, encrypted capture and JSON export.
4. Application recreation retains keys, captures and authenticated access.
5. Bounded UTC scheduling and idempotent advancement.
6. Read-only unsubscribe GET, confirmation POST and subsequent capture suppression.
7. Account deletion invalidates access and removes owned copies; web runs non-root.
8. Production Worker mode and live recommendation origins remain rejected.

The script uses uniquely named, task-owned projects, fabricated accounts and
fixture-only providers, and removes its own containers and volumes in `finally`.
Startup connection resets are retried. The first QA assumptions about arbitrary
tag seeds and the scheduler JSON envelope were corrected to use known artist
fixtures and the existing `Ticks` response; application contracts were preserved.
Final logs: `/tmp/liner-phase9-container-build-final.log` and
`/tmp/liner-phase9-compose-final.log`. See the [local runbook](phase-9-local-run.md).

## CI and final local checks

CI pins verified stable checkout/setup-dotnet action revisions, provides a
healthy disposable PostgreSQL service, restores locked packages, builds Release
and executes all five .NET test projects. Its TRX validator requires positive
test counts, matching results and zero non-passing outcomes in every project.
A separate container job builds and exercises the supported local lifecycle.
The workflow has read-only repository permissions and no publication step.

Local equivalents passed:

- Locked package restore and all five TRX reports: **472 passed, 0 failed, 0 skipped**.
- Existing offline Python tools: **32 tests passed**.
- New credential/result helper checks: **4 test methods passed**, including
  missing/failed/skipped/forged results and exclusive private credential creation.
- Secret-default, renewal-document and whitespace checks.
- Final outdated-package report: 11 projects with no reported outdated entries;
  the explicit MediatR licensing exception remains recorded from the stable feed.

Local logs and results are disposable evidence, not tracked artifacts. No actual
GitHub Actions execution, remote push, external email, live provider acquisition
or public deployment occurred. Developer confirmation remains pending.

## Push-recovery merge verification

The attempted push was non-fast-forward: the fetched `origin/Prism` had three
commits absent locally (registration weekday/legacy repair and scheduler
fairness fixes), while the local Phase 9 branch had five commits absent remotely.
Those histories were merged without rebasing or rewriting commits; the text
conflicts in `CHANGELOG.md`, `PROGRESS.md` and the observation log retain both
sides' entries. Observation numbers were made unique.

After merging, locked restore and Release build passed. The complete integrated
suite passed **478 tests, zero failures and zero skips**: Domain 117, DataAccess
87, Application 112, Presentation 56 and Infrastructure 106. The strict five-file
TRX validator passed. The 32 existing offline Python tests, four new Phase 9
helper methods, repository checks and all eight rebuilt Compose lifecycle groups
passed again. A sandboxed VSTest attempt could not open its local socket; rerunning
the same suite with the permitted local test runner passed. Only task-owned
PostgreSQL and Compose containers/volumes were used and cleaned up.

The local merge makes the branch fast-forwardable to the fetched remote tip as
of this verification. The remote may advance again before any later push. No push
was performed.

## Pull-request finding verification and correction

The review found that the checked-in central version, nine affected package locks
and version snapshot selected MediatR 14.2.0 despite the approved 12.5.0 licensing
exception. Earlier statements that the original graph used 12.5.0 were inaccurate.
The actual pin, locks and snapshot now select 12.5.0; its approved exception and
[Apache-2.0 license](https://raw.githubusercontent.com/jbogard/MediatR/v12.5.0/LICENSE)
are recorded explicitly. No other central package version was changed.

| Finding | Verified outcome |
|---|---|
| GitGuardian 38143099, CI | The former fixed value initialized only a disposable test database. Replaced with a generated password per run, masked in Actions logs, supplied through environment configuration; database port binds to loopback and cleanup always runs |
| GitGuardian 38143098, Swagger tests | Unused fixture password literals removed from both startup configuration locations. Both Development document and Production hiding tests remain functional without database credentials |
| GitGuardian 38143097, Compose | No embedded password value: all connection strings require private `DB_PASSWORD` interpolation. Credential interpolation was subsequently moved into generated private connection variables to avoid recurring scanner triggers |
| Runbook week | Commands now compute the UTC ISO week when executed rather than retaining a dated example |

No flagged value was confirmed to be a real service credential, so no real
credential was rotated. Existing commits were not rewritten and GitGuardian
incidents were not remotely closed. A fresh scanner result is not verified.

Validation after these corrections: locked restore and Release build passed;
all five TRX reports passed the strict validator with **478 tests, zero failures
and zero skips**. The new workflow database startup and cleanup steps were
executed locally with generated credentials and the full suite used that database.
All nine affected locks resolve MediatR 12.5.0. Repository checks and focused
helper checks passed. CodeRabbit CLI is not installed; no local CodeRabbit review
was run. GitHub Actions execution remains separate from these local results.

## Unfinished release work

Last.fm written public-page approval is missing. Recommendation quality is not
verified; production email, scheduling, hosting/capacity and key/backup operations
remain separate work. Existing deployments and plaintext copies were not inspected
or converted. See the [release checklist](phase-9-release-checklist.md).


### Follow-up secret scan remediation (2026-10-13)

Removed the password-shaped assertion literal in `secret_defaults.py`; the guard
now rejects any literal password assignment in DataAccess configuration. Compose
reads full connection strings from the ignored private environment file. CI
builds its connection string from fields at runtime and masks the result.
Gitleaks 8.30.1 found zero findings in the full working tree. This is a separate
scanner; a successful GitGuardian rescan and remote incident closure remain
unverified until GitGuardian evaluates the replacement PR history.
