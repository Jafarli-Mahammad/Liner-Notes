# Manual QA Environment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide a safe Development-only browser environment for manually exercising all current user-facing account, taste, digest, feedback, export and deletion behavior, including Phase 6 generation.

**Architecture:** Keep the QA page as plain same-origin HTML/CSS/JavaScript under a path blocked outside Development. Add a Development-only status, storage-reconciliation and generation endpoint set that reuses existing application services and derives user identity from the bearer principal. A local launcher supplies Synthetic mode and isolated temporary storage roots, while requiring an explicitly named loopback PostgreSQL database.

**Tech Stack:** ASP.NET Core 10 host, existing MediatR commands and repositories, plain HTML/CSS/JavaScript, PostgreSQL, Bash launcher, existing xUnit/WebApplicationFactory test project.

---

## File map

| File | Responsibility |
|---|---|
| `src/Presentation/Program.cs` | Block `/manual-test` outside Development and map the Development-only endpoint extension. |
| `src/Presentation/Development/ManualTestEndpoints.cs` | Safe status, explicit inventory reconciliation and authenticated digest generation handlers. |
| `src/Infrastructure/DependencyInjection.cs` | Register one scoped `LocalGenerationStorage` instance under both concrete and interface types. |
| `src/Presentation/wwwroot/manual-test/index.html` | QA page structure and controls. |
| `src/Presentation/wwwroot/manual-test/manual-test.css` | Responsive, keyboard-accessible styles with local design tokens. |
| `src/Presentation/wwwroot/manual-test/manual-test.js` | In-memory QA session, same-origin API calls, workflow rendering and browser-only checklist. |
| `tests/Presentation.Tests/ManualTestEnvironmentTests.cs` | Development/Production route gates, authorization and safe status payload checks. |
| `scripts/run_manual_qa.sh` | Validate the explicit local QA database target, create temporary artifact roots, apply existing migrations and launch the host in Synthetic Development mode. |
| `docs/renewal/manual-qa.md` | Prerequisites, launch steps, complete manual scenario checklist and known limits. |

## Task 1: Add Development-only QA endpoints and route guards

**Files:**
- Create: `src/Presentation/Development/ManualTestEndpoints.cs`
- Modify: `src/Presentation/Program.cs`
- Modify: `src/Infrastructure/DependencyInjection.cs`
- Create: `tests/Presentation.Tests/ManualTestEnvironmentTests.cs`

- [ ] **Step 1: Add route-boundary tests**

Create `ManualTestEnvironmentTests` using `WebApplicationFactory<Program>`. Provide an in-memory test configuration with a 256-bit test JWT key, a loopback PostgreSQL connection string that is never opened by these unauthenticated requests, and both `Development` and `Production` host variants. Add these facts:

Add four facts named `Development_ServesManualTestPageAndStatus`,
`Production_HidesManualTestPageAndEndpoints`,
`Development_ReconcileAndGenerateRequireAuthentication`, and
`Status_DoesNotExposeSecretsOrFilesystemPaths`.

Assert Development `GET /manual-test` and `GET /api/dev/manual-test/status` return 200. Assert Production requests to the page, status, reconcile and generation paths return 404 (send `X-Forwarded-Proto: https` to avoid the Production HTTPS redirect). Assert unauthenticated POSTs to reconcile and generate return 401 in Development. Serialize the status response and assert it contains no `SecretKey`, `AccessToken`, `RefreshToken`, `Sha256`, `InventoryPath`, `LeasePath`, `ArtifactRoots` or `ConnectionString` property/value. Run:

```bash
dotnet test tests/Presentation.Tests/Presentation.Tests.csproj --no-restore --filter FullyQualifiedName~ManualTestEnvironmentTests
```

Expected before implementation: the new tests fail because the page and routes do not exist.

- [ ] **Step 2: Add the scoped storage concrete registration**

In `src/Infrastructure/DependencyInjection.cs`, replace the current single registration
`services.AddScoped<IGenerationStorage, LocalGenerationStorage>();` with:

```csharp
services.AddScoped<LocalGenerationStorage>();
services.AddScoped<IGenerationStorage>(sp => sp.GetRequiredService<LocalGenerationStorage>());
```

This lets the Development-only operator endpoint invoke the existing explicit reconciliation method while generation continues resolving `IGenerationStorage` from the same scoped instance.

- [ ] **Step 3: Implement the endpoint extension**

Create `ManualTestEndpoints.MapManualTestEndpoints(IEndpointRouteBuilder endpoints)` with exactly these routes:

```csharp
GET  /api/dev/manual-test/status
POST /api/dev/manual-test/storage/reconcile
POST /api/dev/manual-test/digests
```

The status response contains only `environment`, `origin`, `recordingConfigured`, `recordingExpiresAtUtc`, and `storageConfigured`. `storageConfigured` is true only when the inventory path, lease path, positive overhead bound and all six configured categories are present; do not return any configured path, key or hash.

Require authorization on reconcile and generation. Reconcile obtains current database accounting via `IDigestGenerationStore.ReadStorageAsync`, calls `LocalGenerationStorage.ReconcileAsync`, and returns only measured time, database bytes, category byte totals and overall byte total. Generation accepts `{ "week": "2026-W41" }`, validates/parses `IsoWeek`, gets the user ID from `ICurrentUserService`, and dispatches `GenerateDigestCommand`. It cannot accept user ID, provider origin, storage roots, manifest values or scoring parameters from the client. Return the command's actual `generated`, `existing` or `stopped` result and safe diagnostics.

- [ ] **Step 4: Guard and map the Development surface**

In `src/Presentation/Program.cs`, add a middleware check before `UseDefaultFiles`/`UseStaticFiles` that returns 404 for `/manual-test` and descendants when the host is not Development. After `MapControllers`, call `app.MapManualTestEndpoints()` only inside `if (app.Environment.IsDevelopment())`. Keep Swagger behavior unchanged.

- [ ] **Step 5: Run focused route tests**

Run the command from Step 1. Expected: all four focused route-boundary tests pass. Also run `git diff --check`.

- [ ] **Step 6: Commit endpoint boundary**

```bash
git add src/Presentation/Program.cs src/Presentation/Development/ManualTestEndpoints.cs src/Infrastructure/DependencyInjection.cs tests/Presentation.Tests/ManualTestEnvironmentTests.cs
git commit -m "feat(dev): add guarded manual QA endpoints"
```

## Task 2: Add local Synthetic QA launcher and operator instructions

**Files:**
- Create: `scripts/run_manual_qa.sh`
- Create: `docs/renewal/manual-qa.md`

- [ ] **Step 1: Write the local launcher**

The Bash script must fail closed unless `LINER_MANUAL_QA_CONNECTION_STRING` is present and contains both `Host=localhost` or `Host=127.0.0.1` and `Database=liner_notes_manual_qa` as semicolon-delimited connection-string properties. It must never echo the connection string. Create one unique directory using `mktemp -d`, create six child roots named `recordings`, `cache`, `reports`, `copies`, `backups` and `partials`, and remove only that unique directory on exit/signals.

Export `ASPNETCORE_ENVIRONMENT=Development`, `DOTNET_ENVIRONMENT=Development`, `ConnectionStrings__DefaultConnection` from the required variable, `Generation__Origin=Synthetic`, `GenerationStorage__InventoryPath` and `GenerationStorage__LeasePath` inside the unique temp directory, `GenerationStorage__DatabaseOverheadBytesPerPick=1000000`, and the six `GenerationStorage__ArtifactRoots__<category>__0` values. Apply existing migrations, then start Presentation:

```bash
dotnet ef database update --project src/DataAccess/DataAccess.csproj --startup-project src/Presentation/Presentation.csproj
dotnet run --project src/Presentation/Presentation.csproj --no-launch-profile
```

The script must stop if either command fails, must not create/drop a database, must not set Last.fm credentials, and must use a child process/trap arrangement that removes only its own temp directory after the host exits.

- [ ] **Step 2: Document a safe database setup and launch**

In `docs/renewal/manual-qa.md`, explain how to create the local disposable database `liner_notes_manual_qa` in the user's local PostgreSQL instance and set `LINER_MANUAL_QA_CONNECTION_STRING` without committing it or printing it. Include the exact launch command `bash scripts/run_manual_qa.sh`, browser URL `http://localhost:5000/manual-test` with the actual Kestrel URL noted as the source of truth, and how to stop the app. Explain that the script applies tracked migrations to this explicitly named local database and never deletes it. Do not place a connection string or test password in the document.

- [ ] **Step 3: Check shell syntax and documentation formatting**

Run `bash -n scripts/run_manual_qa.sh` and `git diff --check`. Do not launch the script or mutate a local database as part of this step.

- [ ] **Step 4: Commit local setup**

```bash
git add scripts/run_manual_qa.sh docs/renewal/manual-qa.md
git commit -m "docs(dev): add isolated manual QA setup"
```

## Task 3: Build the manual QA page

**Files:**
- Create: `src/Presentation/wwwroot/manual-test/index.html`
- Create: `src/Presentation/wwwroot/manual-test/manual-test.css`
- Create: `src/Presentation/wwwroot/manual-test/manual-test.js`
- Modify: `tests/Presentation.Tests/ManualTestEnvironmentTests.cs`

- [ ] **Step 1: Add a browser route assertion**

Extend `Development_ServesManualTestPageAndStatus` to verify the returned HTML references only same-origin CSS/JavaScript and has an explicit Development-only heading. Keep `Production_HidesManualTestPageAndEndpoints` asserting 404 for the static directory and every API path.

- [ ] **Step 2: Create semantic page sections**

Create `index.html` with labelled sections for environment/status; register/sign-in/profile/session; seed entry and signal inspection; storage reconciliation and digest generation; latest digest and score snapshot; feedback/rating; export preview/download; account deletion; and a manual scenario checklist. Use semantic headings, labels, buttons, `aria-live="polite"` result regions, and no inline scripts, external fonts, images or analytics. Include a visible local-only Development notice.

- [ ] **Step 3: Implement API actions with in-memory session state**

In `manual-test.js`, keep access/refresh tokens only in module state. Add one same-origin fetch helper that sets `credentials: 'omit'`, `cache: 'no-store'`, adds bearer auth when available, parses RFC 7807 errors, and never logs request/response bodies for registration/login/refresh. Log only timestamp, method, same-origin path and status. Add actions for:

```text
GET  /health
GET  /api/dev/manual-test/status
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh
GET  /api/auth/me
GET  /api/subscribers/me
POST /api/taste/seed
GET  /api/taste/signals
POST /api/dev/manual-test/storage/reconcile
POST /api/dev/manual-test/digests
GET  /api/digests/latest
POST /api/digests/recommendations/{id}/feedback
GET  /api/export/my-data
GET  /api/export/my-data?download=true
DELETE /api/subscribers/me
```

Build category/rating payloads with the API's enum names. Preserve familiarity on numeric rating updates; expose an explicit `None` without a rating to test reset semantics. Render full score snapshots from the server's digest/export DTOs. Escaping is mandatory for all server-derived strings. Keep checked manual scenarios in JavaScript memory only.

- [ ] **Step 4: Implement deletion as an explicit final action**

Require a disposable-account warning, typed `DELETE ACCOUNT` phrase and a final confirmation before `DELETE /api/subscribers/me`. On success, clear in-memory tokens and verify a protected request returns 401. Do not add automatic account creation, cleanup, lockout loops or repeated auth requests.

- [ ] **Step 5: Add responsive accessible styling**

In `manual-test.css`, define local CSS custom properties, readable contrast, visible keyboard focus, compact cards for each workflow, responsive single-column layout on narrow screens and reduced-motion behavior. Keep CSS same-origin and avoid inline style attributes so the host Content Security Policy remains effective.

- [ ] **Step 6: Run the focused route test and source checks**

Run `dotnet test tests/Presentation.Tests/Presentation.Tests.csproj --no-restore --filter FullyQualifiedName~ManualTestEnvironmentTests`, then `git diff --check`. Expected: Development assets/routes are reachable; Production remains 404; unauthenticated mutations remain 401.

- [ ] **Step 7: Commit the page**

```bash
git add src/Presentation/wwwroot/manual-test tests/Presentation.Tests/ManualTestEnvironmentTests.cs
git commit -m "feat(dev): add manual workflow test page"
```

## Task 4: Complete the manual scenario guide and verify the composed workflow

**Files:**
- Modify: `docs/renewal/manual-qa.md`
- Modify: `docs/renewal/phase-6-evidence.md`
- Modify: `PROGRESS.md`

- [ ] **Step 1: Document every browser scenario**

List and describe the expected visible result for: health/security headers; registration with default delivery settings; duplicate/invalid registration; valid and invalid login; profile inspection; manual refresh and logout; invalid seed count and valid three-to-five seed save; taste signal read; explicit storage reconcile; generated then existing digest on same ISO week; digest stop due to unconfigured/expired recording or changed inventory; rating 1–10; `AlreadyKnown` with rating; later rating preserving familiarity; explicit reset; export 2.0 preview/download; and final account deletion plus denied post-delete access. For failed-login tests, warn that five failures trigger a 15-minute account lockout and ten auth requests per minute trigger rate limiting; use only a disposable account and do not auto-run repetitions.

- [ ] **Step 2: Add safe-browser/manual test limitations**

Document that the page exercises actual current Presentation behavior and Phase 6 generation; it does not test internal database token hashes, all migration preservation assertions, scorer benchmark conclusions, Phase 2–4 CLI recorder/evaluation gates, live Last.fm acquisition, scheduled email or deferred features. Identify browser developer tools as the place to inspect transport headers. Do not claim that manual UI results prove recommendation quality or production readiness.

- [ ] **Step 3: Run authorized verification**

Run `dotnet build 'Liner Notes.sln' --no-restore`, then the focused `ManualTestEnvironmentTests` command. If PostgreSQL integration tests are configured, run the existing suite with the project's documented local-only connection variable; do not add or use a production connection. Run `python3 scripts/checks/renewal_docs.py` and `git diff --check`. Record actual results in the Phase 6 evidence and progress status.

- [ ] **Step 4: Commit docs and verification record**

```bash
git add docs/renewal/manual-qa.md docs/renewal/phase-6-evidence.md PROGRESS.md
git commit -m "docs(dev): document manual QA scenarios"
```

## Plan self-review

- Spec coverage: Development-only static assets/routes, safe status, explicit inventory reconciliation, authenticated generation, real account/taste/digest/feedback/export/delete flows, local Synthetic launcher, recorded-mode boundary, safe logs, accessibility, and manual limitations map to Tasks 1–4.
- Placeholder scan: no TODO/TBD tasks; run commands, routes, request fields, status fields and expected outcomes are specified.
- Type consistency: route handlers use `GenerateDigestCommand`, `IsoWeek`, `ICurrentUserService`, `IDigestGenerationStore`, `LocalGenerationStorage`, `GenerationConfiguration` and `GenerationStorageOptions` as found in the approved source checkout.

## Execution handoff

The developer has already directed inline execution (“just do what I say”), so continue task-by-task in this worktree. Do not use subagents. Keep the popularity toggle and all other unimplemented approvals out of scope.
