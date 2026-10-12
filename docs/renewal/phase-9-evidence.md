# Phase 9 implementation evidence

In progress, 2026-10-12. [Approved design](phase-9-review.md),
[sequential plan](phase-9-plan.md), [verified version snapshot](phase-9-versions.json).
Developer confirmation remains pending; local verification does not authorize release.

## Approval accounting

| Approved work | Current outcome |
|---|---|
| Latest stable project dependencies/tools | Direct and transitive package versions centrally upgraded and locked; SDK 10.0.401 and EF runtime/tool 10.0.12 verified |
| MediatR licensing exception | Upgraded to Apache-licensed 12.5.0 as explicitly selected by the developer |
| EF 10 compatibility | Existing schema/snapshots preserved; original pending-model and populated migration tests pass |
| Docker/Compose | Implemented files; image/lifecycle verification in progress |
| CI and release preparation | Pending implementation/verification |
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
dotnet restore 'Liner Notes.sln'
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
explicit current package references were not removed. No dependency was removed
or old migration rewritten. No provider calls or actual user data were used.

Container/CI/final evidence will be appended as those approved steps complete.
