# Security scan findings


Date: 2026-10-20. Checkout: `717d4ab61b57cd07b62be12131e8c25bded5d004` (detached HEAD).

Read-only, top-level repository assessment. No fixes, refactors, dependency changes, service launches, destructive commands, commits, or pushes were performed. This report is the only intentionally created or modified file. Findings are static observations and advisory matches, not demonstrated production exploits or a security certification.

## Security-scan plan (brainstorming scope, executed)

**Purpose:** Give the developer an evidence-based, top-level view of security issues across the whole repository without changing application behavior. **Boundary:** Read-only checks of source, configuration and local Git history; only this report may be written. **Success criterion:** Every reported finding has a file/location, category, severity and brief evidence, and the report names the checks performed and their limits.

| Step | Coverage and method | Completion evidence |
|---|---|---|
| 1. Establish baseline | Read `AGENTS.md`, `MEMORY.md` and `PROGRESS.md`; record branch/HEAD, clean status, tracked-file inventory and hashes. | 562 tracked files inventoried; original hashes retained for the final preservation check. |
| 2. Map attack surfaces | Follow public requests through Presentation, Application, Domain, DataAccess, Infrastructure and Worker. Prioritize login/refresh, guest cookies, authorization/ownership, export, catalog inputs, email and scheduled work. | Controllers, authentication setup, repositories, provider clients and worker configuration reviewed; findings A01–A05 identify observed gaps. |
| 3. Search vulnerability classes | Search all tracked code/config for credential patterns, unsafe SQL and commands, browser HTML sinks, deserialization, HTTP/redirect behavior, file paths, cryptographic key handling and missing resource controls. Read surrounding code to filter false positives. | Repository-wide pattern passes plus targeted call-path review; H01 and A01–A05 retained with evidence. |
| 4. Check dependencies and delivery | Compare all package lockfiles and the declared container runtime with current public advisory ranges. Inspect Docker, Compose and CI permissions/action pins. | 114 locked package names checked; zero affected locked versions. Fourteen affected shared-runtime advisory matches R01–R14 found. CI/deployment limits recorded below. |
| 5. Verify and report | Run only checks that create no extra files; redact credential values; assign severity and note uncertain reachability; verify file preservation. | Existing secret-default check passed. Each finding has the required fields. Only `Security_Scan.md` appears in working-tree status after hash verification. |

Brainstorming was used to set these coverage priorities before the security checks began. The inventory contained 562 tracked files: 315 under `src`, 125 under `tests`, 32 under `scripts`, 57 under `docs`, and 33 root/configuration/support files. Repository-wide pattern searches and dependency/secret checks covered that inventory; focused source reading followed security-sensitive matches. This is not a claim of exhaustive line-by-line review. All 253 commits reachable through local `git log --all` were searched for selected credential patterns. Remote refs were not refreshed, and remote scanner results were not obtained.

## Findings summary

There are 14 runtime advisory matches and six application/history findings below. Runtime severities are the advisory publisher's ratings; affected-version membership does **not** establish that every vulnerable feature is reachable in this application.

| Finding group | Critical | High | Medium | Low |
|---|---:|---:|---:|---:|
| Shared-runtime advisory matches | 2 | 10 | 2 | 0 |
| Application and historical findings | 0 | 0 | 3 | 3 |

### R01–R14: shared runtimes pinned to an affected version

**Common location:** `Dockerfile:30` pins `mcr.microsoft.com/dotnet/aspnet:10.0.12` by digest. Both `worker` (`Dockerfile:40`) and `web` (`Dockerfile:46`) inherit this runtime stage. `Dockerfile:2` additionally pins SDK 10.0.401. No image was pulled, built or inspected: these are declared-runtime findings.

**Evidence:** The NuGet vulnerability base and update feeds retrieved on this scan date each identify the relevant runtime package and affected interval. Every row below matched **`[10.0.0, 10.0.12]`**, including the declared 10.0.12 runtime. The linux-x64 runtime package identifiers were used to check the Linux image's shared frameworks. Microsoft's [10.0.13 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.13/10.0.13.md) independently confirm a subsequent security servicing release. Explicit NuGet dependencies and shared frameworks are separate inventories.

| ID | File/location | Category | Severity | Brief evidence and applicability |
|---|---|---|---|---|
| R01 | `Dockerfile:30`, web stage | Dependency CVE / HTTP request smuggling | Critical | [GHSA-fpq8-4vhr-rcxx / CVE-2026-70188](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-fpq8-4vhr-rcxx): affected ASP.NET runtime version; Kestrel HTTP/2 trailer/header handling. Application uses Kestrel, but HTTP/2 exposure and proxy behavior were not verified. |
| R02 | `Dockerfile:30`, web stage | Dependency CVE / HTTP request smuggling | Critical | [GHSA-rv87-ccxw-8ggf / CVE-2026-88092](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-rv87-ccxw-8ggf): affected ASP.NET runtime version; aborted HTTP/1 connections can reinterpret remaining body bytes. Kestrel is used; an exploitable deployment path was not tested. |
| R03 | `Dockerfile:30`, web stage | Dependency CVE / Identity | High | [GHSA-x3vp-85cm-q97r / CVE-2026-70175](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-x3vp-85cm-q97r): affected ASP.NET runtime version. Advisory concerns built-in recovery/confirmation email handling. This checkout uses custom auth controllers; no built-in Identity UI or `MapIdentityApi` registration was found. Reachability not established. |
| R04 | `Dockerfile:30`, web stage | Dependency CVE / resource exhaustion | High | [GHSA-w5qq-qrpp-rf4c / CVE-2026-70181](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-w5qq-qrpp-rf4c): affected ASP.NET runtime version; `FileBufferingReadStream` resource handling. No explicit `EnableBuffering` call found; the host also caps request bodies at 64 KiB. Framework-path exploitability not tested. |
| R05 | `Dockerfile:30`, web stage | Vulnerable shared framework | Medium | [GHSA-57xr-hfxx-jw5x](https://github.com/advisories/GHSA-57xr-hfxx-jw5x): ASP.NET runtime 10.0.12 falls in the feed's affected interval. Version match only; feature reachability not established. |
| R06 | `Dockerfile:30`, web stage | Vulnerable shared framework | High | [GHSA-494h-xx7x-5frh](https://github.com/advisories/GHSA-494h-xx7x-5frh): ASP.NET runtime 10.0.12 falls in the feed's affected interval. Version match only; feature reachability not established. |
| R07 | `Dockerfile:30`, web stage | Dependency CVE / localization resource exhaustion | High | [GHSA-f4g7-52wc-p8x7 / CVE-2026-96236](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-f4g7-52wc-p8x7): affected ASP.NET runtime version; request-localization culture caching. No `UseRequestLocalization` registration found, so this path was not established in the checkout. |
| R08 | `Dockerfile:30`, web stage | Dependency CVE / HTTP/3 resource exhaustion | High | [GHSA-mf4x-w4gh-4656 / CVE-2026-70186](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-mf4x-w4gh-4656): affected ASP.NET runtime version; HTTP/3 abort/reset handling. HTTP/3 is not explicitly enabled in repository code; deployment exposure unverified. |
| R09 | `Dockerfile:30`, web and worker stages | Vulnerable .NET runtime | High | [GHSA-2j93-m82w-73j2](https://github.com/advisories/GHSA-2j93-m82w-73j2): `Microsoft.NETCore.App.Runtime.linux-x64` 10.0.12 matches the affected interval. Version match only; application reachability not established. |
| R10 | `Dockerfile:30`, web and worker stages | Vulnerable .NET runtime | High | [GHSA-w6wh-p4g7-mpj2](https://github.com/advisories/GHSA-w6wh-p4g7-mpj2): same declared .NET runtime matches the affected interval. Version match only; application reachability not established. |
| R11 | `Dockerfile:30`, web and worker stages | Vulnerable .NET runtime | High | [GHSA-mc4q-93x5-f6hj](https://github.com/advisories/GHSA-mc4q-93x5-f6hj): same declared .NET runtime matches the affected interval. Version match only; application reachability not established. |
| R12 | `Dockerfile:30`, web and worker stages | Vulnerable .NET runtime | High | [GHSA-87vj-4hqv-22p9](https://github.com/advisories/GHSA-87vj-4hqv-22p9): same declared .NET runtime matches the affected interval. Version match only; application reachability not established. |
| R13 | `Dockerfile:30`, web and worker stages | Vulnerable .NET runtime | Medium | [GHSA-wxxw-5cvc-53j7](https://github.com/advisories/GHSA-wxxw-5cvc-53j7): same declared .NET runtime matches the affected interval. Version match only; application reachability not established. |
| R14 | `Dockerfile:30`, web and worker stages | Vulnerable .NET runtime | High | [GHSA-3rxq-g8rj-r2cf](https://github.com/advisories/GHSA-3rxq-g8rj-r2cf): same declared .NET runtime matches the affected interval. Version match only; application reachability not established. |

Advisory evidence: [NuGet base feed, 2026-10-19](https://api.nuget.org/v3-vulnerabilities/2026.10.19.23.33.13/vulnerability.base.json) and [update feed, 2026-10-20](https://api.nuget.org/v3-vulnerabilities/2026.10.19.23.33.13/2026.10.20.11.33.15/vulnerability.update.json). Numeric severities 1/2/3 were rendered as Medium/High/Critical. No claim is made that the published container digest was independently mapped to its installed binaries.

### A01: signing out does not revoke the server-side refresh session

- **Location:** `src/Presentation/wwwroot/app.js:545–556`; `src/Presentation/Controllers/AuthController.cs:169–244`; `src/Presentation/Services/AuthService.cs:153–202`; `src/Presentation/Options/JwtOptions.cs:13–14`.
- **Category / severity:** Authentication and session lifecycle — **Medium**.
- **Evidence:** `logout()` clears browser state without a server request. The controller exposes register/login/refresh/me, with no logout/revocation action. Refresh validation checks the stored hashed token, expiry, security stamp and active user; browser logout changes none of them. A previously copied access/refresh pair therefore remains redeemable after sign-out until server-side invalidation or expiry. Default refresh lifetime is seven days, and rotation issues a new expiry.
- **Confidence / limits:** High confidence from control flow. This requires prior token possession; token theft was not demonstrated. Browser tokens are held in memory, and refresh rotation is atomic.

### A02: legacy authenticated endpoints permit repeated work without endpoint throttling

- **Location:** `src/Presentation/DependencyInjection.cs:48–67`; `src/Presentation/Controllers/TasteController.cs:17–49`; `src/Presentation/Controllers/ExportController.cs:12–36`; `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommandHandler.cs:41–71`; `src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQueryHandler.cs:62–94`; `src/DataAccess/Persistence/Repositories/DiscoveryProfileReader.cs:34–57`.
- **Category / severity:** Resource exhaustion / abuse controls — **Medium**.
- **Evidence:** Named rate-limit policies exist, but there is no global limiter and the legacy taste/export controllers have no rate-limit attribute. Each valid seed request appends signals; the three-to-five-item validation is per request, not a lifetime total or deduplication check. Export accumulates all digest pages and discovery collections in memory. An authenticated account can repeatedly grow its taste data and request expensive full exports without an application request/concurrency limit on these routes.
- **Confidence / limits:** High confidence in the missing controls; actual exhaustion not measured. Ownership filters limit access to the caller's data. The storage coordinator can reject writes when enabled, but does not throttle exports; request body limits do not cap accumulated export size. Proxy limits and deployment capacity are unknown.

### A03: local-email protection keys are stored without application-level wrapping

- **Location:** `src/DataAccess/Persistence/LocalUnsubscribeProtection.cs:19–26`; `src/DataAccess/DataContexts/DataContext.cs:43`.
- **Category / severity:** Cryptographic key storage — **Medium**, conditional on local-email use and database disclosure.
- **Evidence:** The private service provider calls `AddDataProtection().SetApplicationName(...).PersistKeysToDbContext<DataContext>()` without configuring key encryption. It uses those keys for both unsubscribe capabilities and captured email encryption. A database reader who obtains the key ring can forge protected capabilities and, if also given the encrypted files, decrypt captures.
- **Confidence / limits:** High confidence in configuration. Explicit persistence disables automatic at-rest key protection unless separately configured, as documented by [Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0). Database compromise was not demonstrated. File permissions and purpose separation remain useful controls; disk/backup encryption was not assessed.

### A04: registration discloses existing email and username membership

- **Location:** `src/Presentation/Services/AuthService.cs:43–54`; `src/Presentation/Controllers/AuthController.cs:51–69`; `src/Presentation/Filters/ApiExceptionFilterAttribute.cs:137–149`.
- **Category / severity:** Information disclosure / account enumeration — **Low**.
- **Evidence:** Anonymous registration checks email and username separately, throws distinguishable “already exists” errors, and the exception filter returns the message in `ProblemDetails.Detail`. This lets a caller test membership without authenticating.
- **Confidence / limits:** High confidence. The auth policy limits requests to ten per minute per IP; it reduces enumeration speed but does not remove the disclosure.

### A05: access-token validation does not consult the security stamp

- **Location:** `src/Presentation/Services/JwtService.cs:37–45`; `src/Presentation/DependencyInjection.cs:127–136`; `src/Presentation/Services/AuthService.cs:111–120,187–202`.
- **Category / severity:** Authentication revocation gap — **Low / latent**.
- **Evidence:** Access tokens carry no security-stamp claim. `OnTokenValidated` checks only that Identity and domain accounts still exist and the Identity account is not deleted. Refresh validation checks the stamp, but access-token validation does not. A password reset/stamp change can therefore invalidate refresh credentials while already issued access tokens remain valid until expiry.
- **Confidence / limits:** High confidence in validation behavior. The default access lifetime is 15 minutes, with one minute of validation skew; configurable lifetime is bounded to 60 minutes. A reset method exists, but no public reset endpoint was found. This is a latent revocation limitation, distinct from A01's refresh-token persistence.

### H01: historical development credentials remain recoverable from local Git history

- **Location:** Commit `a87d86a10483`, `src/Presentation/appsettings.Development.json:10,13`; commit `3b72c0f06fb7`, `src/Presentation/appsettings.json:11,14`. Both commits were verified as ancestors of the scanned HEAD. Additional database defaults occur at `287815674ac6:src/DataAccess/DependencyInjection.cs:18` in a local backup ref.
- **Category / severity:** Historical secrets / credential hygiene — **Low**, conditional risk if defaults were reused.
- **Evidence:** Redacted history scanning found literal database credentials and development signing keys in these earlier runtime configuration files. `git show` confirmed the literals and development/localhost markers; `git merge-base --is-ancestor` confirmed the ancestry stated above. Values are intentionally omitted.
- **Confidence / limits:** Historical literals are confirmed. They appear to be development defaults, not verified live credentials. Current settings contain no corresponding credentials and startup rejects known insecure signing-key defaults. Production reuse, rotation status and current remote reachability were not verified. No provider-token/private-key match was found by the selected patterns.

## Coverage outcomes and exclusions

| Area | Review outcome |
|---|---|
| Domain | Project dependency boundary and repository-wide risky API patterns checked. No external dependency or independent scoring-layer security finding identified. |
| Application | Reviewed orchestration boundaries, validation, export aggregation, seed writes, feedback and discovery ownership. A02 applies. No confirmed caller-controlled SQL/command execution path identified. |
| DataAccess | Reviewed raw/interpolated SQL sites, parameters, account/profile scoping, deletion/export queries, migrations and key storage. Raw catalog queries bind values with `NpgsqlParameter`; dynamic lock-table identifiers originate from fixed internal tables. A03 applies. |
| Infrastructure | Reviewed upstream endpoints and transport controls, bounded response parsing/caches, typed JSON, catalog import validation, local file paths, encryption and email rendering. No confirmed SSRF, unsafe polymorphic deserialization, XXE, shell injection or path traversal identified. This does not prove their absence. |
| Presentation/browser | Reviewed controller authorization, guest ownership and CSRF, JWT/refresh flows, middleware, browser token storage and HTML encoding. A01, A02, A04 and A05 apply. No confirmed cross-account read/write or XSS identified. |
| Worker | Reviewed entrypoint/configuration and local-email/storage boundaries; inherits R09–R14's declared runtime and A03's key handling. No worker process or scheduled job was executed. |
| Secrets/tests/scripts/docs | Current-tree and Git-history patterns were reviewed with values redacted. Current credential-pattern hits were synthetic/test inputs. Existing `python3 -B scripts/checks/secret_defaults.py` passed. H01 remains historical. Regex scanning is not equivalent to a dedicated secrets scanner. |
| NuGet dependencies | Parsed all 11 `packages.lock.json` files, covering 114 distinct resolved package names, including transitives. Comparing their stable versions with the live base/update advisory ranges returned zero affected explicit locked packages. This does not clear the shared runtimes, SDK tools, OS packages or container layers. |
| CI/deployment/root files | Reviewed both GitHub workflows, Dockerfile, Compose, build/tool manifests, hooks and settings. Actions are SHA-pinned; checkout disables credential persistence; Qodana excludes fork PRs. Local Compose binds web access to loopback. No confirmed workflow expression injection found. Remote Qodana/GitGuardian results, deployed TLS/proxy settings, OS packages, SDK-specific advisory coverage and container SBOMs remain unverified. |

No builds, restores, .NET tests, browser runs, load tests or exploit probes were executed because they can create additional files or mutate services. The read-only secret-default check and in-memory advisory comparisons were executed. No source was uploaded to a scanner; outbound requests retrieved public advisory information.

## Skills and tools actually used

| Skill/tool | Actual use and availability |
|---|---|
| `brainstorming` | Read `/home/mahammadjafarli/.agents/skills/brainstorming/SKILL.md` first to scope the investigation and communicate the scan plan. No product implementation was undertaken. |
| `octopus-security-audit` | **Available and explicitly invoked.** Read `/home/mahammadjafarli/.agents/skills/octopus-security-audit/SKILL.md`; applied its deep-mode OWASP/STRIDE, secrets-history and CI review coverage. Its documented `~/.claude-octopus/plugin/scripts/orchestrate.sh` is absent. A cached 11.14.2 runner exists but creates result/log/plan directories. The runner was **not executed**, to respect the one-file constraint. This report is a manual/tool-assisted fallback, not an Octopus provider-generated result or adversarial-team run. Global plugin integrity was outside this repository scan. |
| `task-observer` | Read its skill and applied observation in-session. Log writes, scheduled reviews and skill changes were suppressed by the explicit one-file constraint. |
| Serena MCP | Executed `initial_instructions` and `search_for_pattern` for dangerous deserialization, permissive TLS/CORS, raw SQL and process execution patterns. No editing or onboarding tool was invoked. |
| Codex-Mem MCP | Executed `search` for prior security context. Current repository evidence determined findings; old remote scan status was not treated as current. No memory was saved. |
| Shell/Python/Git | Used `rg`, file reads, tracked-file inventory, SHA-256 snapshots, read-only Git history/ancestry inspection, redacted pattern scanning, the existing secret-default check, and in-memory JSON/version-range comparisons. Python's standard-library HTTP client fetched NuGet feeds after network approval; it wrote no downloaded files. |
| Web | Retrieved official Microsoft documentation and dotnet advisories/release notes to verify framework and key-storage claims. |
| Tool discovery | Inspected callable tool metadata after `tool_search` returned “unsupported call.” No dedicated Octopus/security-scanner MCP endpoint was exposed. |
| Unavailable/not executed | `gitleaks`, `ggshield`, `semgrep`, `trivy`, and `osv-scanner` were not found on PATH. Qodana is configured in the repository but was not run, and no current remote result was retrieved. No tools or plugins were installed. |

## Preservation verification

The working tree was initially clean. All 562 tracked file hashes were recorded before scanning and checked again after report creation. Report verification checks that those hashes remain unchanged and that `git status` lists only the new `Security_Scan.md`. No fixes are included in this report.
