# Performance and network audit: static review and profiling plan

Date: 2026-10-24 (Asia/Baku)
Repository baseline: `3ae263c73dc1f16510a8033481b51a14cf349e16`
Audit execution baseline: `a930ebb87246a4709492ae06a543f996f85baac0` (approved plan commit; product sources unchanged).
Continuation baseline: `eac7d74b3d6f4b21841a6d4ad4ad3f2be0a6419e`.
Status: **Static inventory pass complete at the audit baseline: 495/495 files, 63,948 lines accounted for using the review methods below. Authorized bounded remediation is locally verified below: four findings addressed, three partially addressed; larger decisions and measure-first items remain open. Runtime attribution and developer confirmation remain incomplete.**

## Authorized remediation — 2026-10-24

The developer subsequently authorized bounded fixes, leaving large design/policy decisions for later review. The static findings, line references and hashes below describe baseline `fa42ae4` and are retained as historical audit evidence; they no longer describe every current source hash.

| Finding | Current disposition |
| --- | --- |
| P01 | Deferred: artifact ownership/change-detection design decision |
| P02 | Partially addressed: post-pacing cache reuse; in-flight coalescing remains open |
| P03 | Partially addressed: one full-body copy removed; bounded buffering/owning JSON retained |
| P04 | Addressed and regression-verified: server retry delay honored |
| P05 | Deferred: measure stripe contention before choosing a concurrency design |
| P06 | Deferred: complete-export memory/atomicity design decision |
| P07 | Measure first: queue polling responsiveness versus idle SQL cost |
| P08 | Partially addressed: invariant preparation reused; complete snapshots still built per candidate |
| P09 | Deferred: feedback revision/locking design decision |
| P10 | Addressed and regression-verified: indexed alias lookup in both merges |
| P11 | Addressed and PostgreSQL-verified: batched, eligibility-filtered item reads |
| P12 | Deferred: measure quota aggregates before choosing counter/schema changes |
| P13 | Deferred: archive layout and legacy-copy migration decision |
| P14 | Measure first: representation/canonicalization and integrity-boundary costs |
| P15 | Deferred: authentication versus global admission coordination decision |
| P16 | Deferred: decide whether artist supporting evidence/gaps remain mandatory for direct-tag picks |
| P17 | Addressed and PostgreSQL-verified: one eligibility query, both checks retained |

First implementation group: P04 now honors numeric and HTTP-date `Retry-After` values, falls back to five seconds for missing/invalid headers, resumes ordinary two-second polling after success, and preserves tab/session cancellation. Long delays are split into timer-safe intervals without early HTTP retries. P02 is **partially addressed**: the ListenBrainz client rechecks shared memory after provider pacing and only consumes request budget when dispatching; overlapping in-flight acquisitions still need a separate coalescing design. P03 is **partially addressed**: parsing uses the existing bounded stream buffer, removing `ToArray`; complete-body buffering and the owning JSON clone remain for integrity/lifetime safety.

Verification: the new polling test failed on the original premature retry and passed after the change; the new transport concurrency test failed on duplicate dispatch and passed after the change. All 201 Infrastructure tests passed with zero skips; both dependency-free Node browser checks passed. VSTest required permission for its local socket outside the sandbox. No live provider calls were used. Larger decisions and subsequent implementation groups are recorded here as work proceeds; developer confirmation remains pending.

Second implementation group: P08's repeated taste sorting/magnitude and configuration hashing are prepared once per scoring enumeration, with separate cached hashes for the two existing formula versions. The onsite preparation and weekly ranking paths use this batch operation. Cached taste/policy dictionaries are read-only; scalar scoring and replay remain available. Every candidate still receives its complete snapshot, so P08 is **partially addressed**, with no snapshot/schema or scoring-policy change. P10's onsite and legacy weekly merges now find matching groups through an ordinal alias index and retain group preference, transitive identity, provenance and exclusion behavior. Duplicate-heavy group merging can still copy growing groups; no claim of universally linear complexity is made.

Verification: configuration-enumeration regression failed at 100 enumerations before the change and passes afterward; full Domain suite passed 150 tests, including benchmark seeds and replay. Application suite passed 139 tests, including transitive alias bridges, recording-evidence preference and legacy whole-group exclusions. A new fixture expectation was corrected to match `RawCandidateTrack`'s existing canonical path sorting; production path sorting was preserved.

Fresh isolated Release comparison on .NET 10.0.12: 400 distinct candidates, ten tags each, 271 taste tags, five selected picks, empty score evidence, fixed batch identity. Sixty warm-ups precede 25 synchronous samples; allocation uses `GC.GetAllocatedBytesForCurrentThread` and includes measurement overhead. Merge timing includes reflection invocation of the actual private method. Helpers/logs are under `/tmp/liner-perf-remediation/`; these are temporary local artifacts, not permanent capture storage.

| Operation | Before median allocated bytes | After median allocated bytes | Before median ms | After median ms | Output contract |
| --- | ---: | ---: | ---: | ---: | --- |
| Batch ranking | 13,068,520 | 2,940,728 | 13.7313 | 9.1366 | Same serialized selected-breakdown hash |
| Onsite merge | 43,254,096 | 1,431,024 | 10.4940 | 1.1732 | Same ordered track/tag/path hash |

Ranking SHA-256: `f29b93cf21bf901b9d21858ea70170d2f2ffe219f907d33d5b7b7792d952dcc0`; merge SHA-256: `e63f61168c44552ef22895379b10013214ebcd2d947d746d3e797d72e37d6323`. Hashes omit random transient track IDs. These fresh measurements use a different fixture/configuration from the older audit and must not be compared numerically with its table. Shared-host timing is indicative, not an end-to-end latency or retained-memory claim; real evidence graphs and duplicate distributions vary.

Third implementation group: P11 adds a provider-independent batched catalog read, folds publication eligibility into the same SELECT, preserves requested order/duplicates and omits unavailable IDs. Taste selection and completed lookup mapping use it. Retained private favorite fallback and owner checks remain; tag evidence is parsed once per row. P17 combines Identity and subscriber eligibility in one SQL command, retaining both soft-delete conditions and per-request revocation checks. No schema migration, dependency update, provider endpoint or pacing-policy change was made.

PostgreSQL command interceptors reproduced two SELECTs for one eligible item and two eligibility commands for a valid bearer token before the changes. Afterward: ten catalog items plus a duplicate/missing ID require one SELECT; an empty batch requires none; cancelled reads propagate cancellation; expired items are excluded. Authenticated requests require one eligibility command, and hiding either the Identity or Domain user immediately returns 401. Complete export, private ownership, guest flow, legacy adapters, model/migration consistency and process recovery remain covered by the full regression suite.

Final local verification: 702 Release tests passed, zero failures/skips (Domain 150, Application 139, DataAccess 131, Infrastructure 201, Presentation 81). The existing TRX gate verifies all five projects. Both `node scripts/checks/discovery_polling.cjs` and `node scripts/checks/phase1_browser.cjs` passed; `git diff --check` passed. Database tests used fresh task-owned PostgreSQL containers and disposable databases; their containers were removed afterward. Existing live data/settings were not used. Existing package-pruning and unrelated test-analyzer warnings remain; no dependency removal was attempted.

### Larger decisions retained for developer review

- **P01 — Artifact integrity and write latency.** Each ordinary discovery write currently makes five full passes over configured artifact roots. Each pass recursively enumerates files and reads them to calculate SHA-256. The passes protect tamper detection, cross-process coordination, and fail-closed storage accounting, but they extend the shared write lease and sometimes the database transaction; heartbeats and unrelated writes can wait behind them. The choice is between keeping all five integrity checks, accepting their cost, or defining a trusted-writer contract: for example, only this process may change artifacts and every mutation must advance a durable generation/version. Under that contract, some repeated hashes could be replaced by version checks, while startup/recovery or periodic audits still hash content. That option is faster only if every writer (including crashes, operators, and other processes) obeys the contract; otherwise a changed file could escape detection. **Decision:** retain strict rehashing, or authorize design of a versioned/exclusive-writer integrity contract? Do not reduce checks before that contract is specified.
- **P06/P13 — Complete data export and shared email archive.** The account data export promises the user's full stored history. Its reader loads batches, jobs, recommendations/tracks, taste signals, aliases and mapped DTOs into overlapping in-memory arrays. Separately, when the local email archive is enabled, exporting one account scans the shared copies directory, reads and decrypts each copy before checking its owner, then searches that account's digests for matches. Cost therefore grows with both the exporting account's history and, for archive copies, other accounts' retained mail. Keeping the current design preserves complete, failure-atomic export behavior and the existing archive layout. A bounded redesign could stream or page database history and stage output privately before publishing the completed export; it could also store/archive by owner or add an indexed owner manifest, with a migration path for existing shared-directory copies. Direct streaming could leave a partial export if a later read/decrypt/serialization fails, while staging requires temporary storage, cleanup and disk-space limits. Per-owner storage must retain authentication before reads/deletes and safely migrate old copies. **Decision:** keep current semantics/layout and address only after realistic scale measurements, or authorize a design for staged bounded exports plus owner-indexed archive access and legacy-copy migration? No history should be omitted to save memory.
- **P09/P12 — Database work under shared write coordination.** Weekly generation materializes and fingerprints all historical feedback to establish the revision/snapshot it is processing. Public-evidence publication also calculates cache-wide byte and row totals before each quota decision, then may sort/select eviction victims. These database operations extend the shared mutation's lock/transaction duration; the cost rises with feedback history or cache entries. One possible redesign maintains revision and quota totals transactionally so each operation reads a small counter, and narrows locks around unrelated work. That introduces a correctness contract: every insert/update/delete/expiry/eviction path must update counters exactly once, including retries, concurrent processes and crash recovery. A missed update could grant excess quota or hide new feedback. **Decision:** keep deriving totals from source rows, or authorize a schema/transaction design for maintained counters after query-plan and contention measurements? This should be scoped separately for feedback revision (P09) and evidence quota (P12); a database migration is part of the latter design if counters are chosen.
- **P05/P15 — Who must wait for whom.** Prepared-pool acquisition uses 32 semaphore stripes; distinct cold pool keys that map to one stripe serialize during upstream discovery and hydration. Separately, when background jobs are enabled, login runs inside the global write admission used by discovery changes and heartbeats, so password-hash verification can hold that admission even for failed logins. The first may cause avoidable waits between unrelated pools; the second may delay unrelated writes during CPU-heavy password verification. Their real impact depends on concurrent reachability and traffic, so changing synchronization before measuring could add complexity without helping. Options for pool acquisition include retaining stripes or coalescing/waiting by exact pool key while allowing unrelated keys to proceed. For login, admission might cover only the lockout/session mutations, but then failure counts, lockout rules and successful session storage need a defined atomicity and cross-process concurrency model. **Decision:** first authorize a bounded contention measurement for both paths, then decide whether to redesign either boundary? Any redesign must keep provider pacing, password hashing, lockout/session guarantees and managed-write accounting intact.
- **P16 — Evidence required for recording-tag recommendations.** Hydration currently fetches and prepares artist metadata for candidates before checking whether the recording itself already has usable tags. For candidates with direct recording tags, the chosen score vector comes from those recording tags, so the artist request may add latency and network traffic without changing the selected vector. However, artist responses also provide supporting evidence and may report missing/gap information; dropping that request could change explanation or evidence-completeness behavior even when the score stays the same. Keeping it preserves current evidence behavior and cost. Skipping it for direct-tag candidates would reduce requests, but requires deciding what the stored recommendation should say about supporting artist evidence and whether a recording-only explanation is sufficient. **Decision:** require artist supporting evidence/gap reporting for every recording pick, or allow recording tags alone when present and represent artist evidence as not requested/not applicable? No acquisition was skipped pending that policy choice.

P07 and P14 remain profiling tasks, not requests to approve speculative changes. Full retained-memory evidence, deployment/proxy transfer measurements and representative live-provider profiling remain outstanding. The older dotTrace license/dotMemory limitations still apply; these fixes do not establish attribution of every millisecond/byte or production readiness. Developer confirmation remains pending. Unrelated workflow, security-report and skill-observation changes remain excluded from task commits.

## Scope and success criteria

The developer requested a line-by-line performance and network review, using appropriate skills and MCP servers, especially Rider, dotTrace and dotMemory. They first selected **plan first, with initial findings**, then approved execution and instructed scanning every line. This document records the completed static pass, preserves the earlier isolated measurements and retains the profiling plan. The original audit was report only; subsequent authorized product fixes are documented above. No project dependency updates or production configuration changes were made. Earlier workload helpers and private evidence were under `/tmp/liner-performance-audit-20261024/`; that directory is absent during this continuation, so those raw captures could not be reinspected. No new live provider requests or live application database mutations were made; remediation tests used disposable databases.

The inventory covers first-party application code, migrations, tests, scripts, workflows and build/runtime configuration. A search-pattern match is a lead, not a completed review. All handwritten `src/` files received a complete numbered source read, with reported issues traced through their implementations and callers. Repeated test/tooling lines use exact references to already-read lines while every novel line is inspected in file order. Generated schema files use unchanged-block equality plus review of every changed declaration; lockfiles use JSON parsing and exact baseline comparison plus changed-line review. These are distinct methods, not a claim that all 63,948 lines were independently reread verbatim. The ledger records the method and content hash per file. Static review does not establish runtime coverage or clear a file of all performance defects.

The goal is to identify attributable CPU time, latency, allocation, retained memory, disk I/O, SQL work and network traffic. No profiler can guarantee attribution of every millisecond or byte: sampling, instrumentation overhead, scheduler behavior, native memory, TLS, network transport and external services limit precision. Record uncertainty, profiler overhead and residual/unattributed work explicitly. Do not invent savings.

Preserve provider abstraction, deterministic scoring and persisted explanations, complete data export, guest access, privacy boundaries, mandatory provider pacing and fail-closed storage accounting. Existing source changes in `.github/workflows/qodana.yml`, `skill-observations/log.md`, and the untracked `Security_Scan.md` belong to other work and are excluded from this task's edits and commit.

## Approach considered

| Approach | Benefit | Limitation |
| --- | --- | --- |
| Static review alone | Exhaustive source coverage; detects repeated work and complexity without running the site | Cannot establish actual latency, retention, execution frequency or wire bytes |
| Profiling alone | Identifies costly executed paths under a defined workload | Misses unexercised paths and can mistake mandatory waits for waste |
| **Recommended: tracked source review plus separate CPU, memory, SQL and network measurements** | Connects code-level causes to reproducible costs and exposes coverage gaps | Requires a controlled environment, workload definitions and working capture tools |

Execution skills: `performance-reviewer` for evidence-based triage and `task-observer` for workflow observations. No brainstorming skill was used during audit execution. Product implementation remains outside scope.

## Verified tool access and capture gaps

This table and B1 describe the original planning preflight. The execution checkpoint below supersedes capture/browser availability and records the new license and browser-launch blockers.

| Capability | Current evidence | Plan |
| --- | --- | --- |
| Rider MCP connection | `get_solution_projects(rootFolder=...)` succeeded and returned Domain, Application, DataAccess, Infrastructure, Presentation, Worker and test projects | Use the explicit repository root on each Rider call |
| Rider launch discovery | `get_run_configurations` succeeded; configurations include `Presentation: Liner Notes`, `Presentation: Manual QA`, `Presentation: Swagger` and `Worker` | Select the exact existing configuration; verify its target checkout, build mode, process and data before execution |
| Launch overrides | Returned configurations report `supportsDynamicLaunchOverrides=false` | Do not assume that MCP can override their environment or arguments |
| dotTrace MCP analysis | Tools listed below are exposed | Use them once a valid snapshot exists; analysis-tool presence does not establish recording access |
| dotTrace recording via MCP | No recording/start/attach profiling tool was found in the exposed Rider tool names/descriptions | Capture through supported Rider UI or a verified local CLI, then analyze through MCP |
| dotMemory via MCP | No dotMemory MCP tool was found in this session's tool registry | Use a verified local profiler or Rider memory UI; retained-object analysis access remains to be established |
| Local profiler files | Rider's `tools/profiler` contains `dotTrace.sh`, `dotMemory.sh`, `dottrace` and `dotmemory` | Presence is insufficient: see blocker B1 |
| Existing evidence | No `.dtp` or `.dmw` file was found in the repository inventory | No snapshot has been opened or analyzed; this does not assert that none exists elsewhere |
| Project memory | Codex-Mem `build_context` was queried for performance/profiling context; repository `MEMORY.md` and `PROGRESS.md` were read | Prefer current code over older checkout notes; the active frontend is `wwwroot/*.js`, with no React package manifest in this inventory |

Exposed dotTrace MCP tools, by suffix under `mcp__rider__`:

- `dotTraceGetSnapshotInfo`
- `dotTraceGetSnapshotFilters`
- `dotTraceGetCallTree`
- `dotTraceGetTimeline`
- `dotTraceGetTimelineEvents`
- `dotTraceOpenReport`

**B1 — Planning-pass local CLI capture was not verified usable (execution update below).** Running the installed `dotTrace.sh --help` failed because its referenced sibling `runtime-dotnet.sh` is missing. Invoking the newer `dottrace --help` failed with permission denied; `ls -l` shows both newer wrappers lack execute permission. Reading both legacy launchers confirms they reference the same missing runtime script. No install, permission change or tool repair was attempted. Establish whether Rider UI capture works, or prepare a compatible standalone CLI distribution, before scheduling runtime capture. This is a tooling blocker, not an application performance finding.

**B2 — Linux profiling has platform limits.** JetBrains documents that line-by-line profiling is unavailable for .NET Core applications on Linux/macOS. The project pins SDK `10.0.401`; validate profiler support for its exact runtime and OS before capture. Use supported sampling/timeline/tracing modes and source inspection. Even where line instrumentation is supported, JetBrains warns its high overhead distorts call times. [CLI platform limitations](https://www.jetbrains.com/help/profiler/Performance_Profiling__Profiling_Using_the_Command_Line.html), [profiling configuration guidance](https://www.jetbrains.com/help/rider/Profiling_Configurations.html).

## Proposed audit sequence and effort

Provisional effort is **6–10 focused working days**, not a runtime guarantee: environment/tool preflight 0.5–1 day; source review 3–5 days; workload captures and attribution 1.5–3 days; reconciliation/report verification 0.5–1 day. The inventory currently contains 486 tracked text files and 63,157 lines, including generated migrations, lockfiles and test fixtures. Capture-tool setup and realistic dataset preparation can extend the estimate. Re-estimate after the first complete layer review.

| Stage | Work and output | Completion check |
| --- | --- | --- |
| 0. Freeze scope and environment | Recheck HEAD and dirty paths; regenerate inventory; record host CPU/RAM/OS, SDK/runtime, profiler version, process command, configuration, database scale and deployment topology | The measured checkout and workload are identifiable; unrelated changes remain untouched |
| 1. Establish ordinary baselines | Use a Release build with matching symbols and no debugger; warm JIT separately; record idle and representative journeys without profiling | Timing distributions and resource counters exist before introducing profiler overhead |
| 2. Read every file | Review the ledger in dependency order: Domain, Application, DataAccess, Infrastructure, Presentation, Worker, then tests/scripts/build/deployment; follow hot paths across layers | Every inventoried line has an explicit reviewed disposition, with generated/declarative content distinguished |
| 3. CPU and waits | Capture supported dotTrace sampling/timeline profiles of the same workloads; trace inclusive time to expensive descendants; separate queue, locks, SQL, upstream pacing and remote service waits | Hotspots and waits have call paths, workload IDs and evidence references |
| 4. Allocation and retention | In separate runs, capture dotMemory baseline/burst/post-quiescence snapshots; analyze allocation stacks, dominators, roots, cache residency, LOH and object deltas | Retention claims have live-object/root evidence; allocation churn is not mislabeled a leak |
| 5. SQL, disk and network | Correlate query counts/plans, artifact read bytes, HTTP requests, payload sizes and browser waterfalls | Duplicate work and wire cost are attributed to specific operations and deployments |
| 6. Reconcile findings | Resolve overlapping causes, disprove false positives, rank costs under the observed workload, and list unmeasurable gaps | Every finding has evidence status and a reproducible validation procedure |
| 7. Deliver report | Review this document, its ledger and evidence references; verify no product changes; make a focused local report commit after verification | Developer reviews the report before any remediation or claim of developer-confirmed completion |

Suggested local documentation commits during a later audit: (1) reviewed source ledger and static findings, (2) CPU/SQL/network evidence and attribution, (3) memory evidence, reconciled priorities and final coverage. Do not commit raw profiles, HAR files, database contents or other sensitive capture artifacts. No fixes or pushes are included.

## Measurement protocol

1. Use disposable test accounts and isolated database/storage roots. Record bounded dataset sizes and hashes of nonsecret workload inputs. Do not run write-heavy scenarios against the developer's persistent live database by default. Its normal IDE profile selects real providers; do not accidentally use it as an offline load-test target.
2. Measure ordinary execution first, then separate CPU and memory runs. Start with at least five comparable timing runs after warm-up, report median/range, and add repetitions when variance matters. Report p95/p99 only with enough operation samples and the sample count. Record a monotonic timeline and workload boundaries.
3. Vary one dimension at a time: cold/warm cache, one/many distinct keys, small/large history, artifact bytes/file counts, request concurrency and queue depth. Proposed load points are 1, 5 and 20 concurrent local clients; adjust to host capacity. Do not send that load to external providers.
4. Separate offline/replayed mechanics from live provider performance. Reuse existing immutable replay clients for controlled source behavior where appropriate; explicitly label that evidence. Real provider captures require current terms/rate-limit review and a bounded call budget. Never bypass the request gate, refresh private data indiscriminately, or turn cache tests into upstream load tests.
5. Preserve raw private captures outside Git; save sanitized aggregates and precise evidence paths in the report. HTTP headers/bodies, SQL parameters, memory dumps, HAR and profiler output can contain cookies, tokens, email addresses, taste data or query text. Do not print launch environment values or private settings into this report.
6. For time attribution, separate wall-clock critical path from summed thread time. Overlapping spans and parent/child call-tree values must not be added together. Break down CPU, GC, queueing, lock contention, storage validation, DB round trips, network setup, provider pacing and remote response latency. Mandatory pacing is a constraint, not automatically wasted time.
7. For bytes, distinguish allocation rate, live managed heap, native memory, process RSS, disk reads/writes, serialized/decompressed body size, compressed HTTP transfer size and actual transport bytes. Cache size estimates are not measured RSS. HTTP payload sizes do not include TLS/TCP overhead; only attribute transport bytes when a suitable scoped observation is available.

### Workload matrix

| ID | Scenario | Variations and observations |
| --- | --- | --- |
| W0 | Idle web host and worker | Jobs enabled/disabled; idle SQL rate, CPU wakeups, memory baseline, periodic maintenance |
| W1 | Guest entry, optional onboarding, local catalog search | First/repeat visit; empty/skipped/full taste; rapid typing, aborted searches, long result pages; SQL counts, DOM work, waterfall |
| W2 | Upstream catalog lookup | Same/distinct keys, existing public evidence, 429/503/slow response, cancellation; provider calls and pacing, cache misses and job polls |
| W3 | Discovery generation | Prepared/unprepared pool, repeated/overlapping seeds, bounded maximum candidates, feedback/history sizes; queue wait through persisted results |
| W4 | Concurrent discovery | Identical keys, overlapping metadata, distinct keys sharing a stripe, separate host processes; coalescing, contention and duplicate bytes |
| W5 | History, recent picks and JSON export | Empty/typical/large histories; query plans, materialized rows, peak memory, time to first/last byte; preserve complete export |
| W6 | Scheduled digest and local email capture | 1/10/100 due recipients in an isolated environment, pending recovery and failures; per-user SQL/upstream/disk work and serial bottlenecks |
| W7 | Storage admission, heartbeat and maintenance | Empty/100 MB/near-limit artifacts; many small versus few large files; query and full-file hash counts per mutation; never weaken integrity checks |
| W8 | Browser transfer and rendering | Cold/warm cache, compressed/uncompressed negotiation, realistic mobile throttling, background/foreground tab; bytes, revalidations, long tasks and abandoned polling |

### Rider and dotTrace analysis procedure

After capture, call `dotTraceGetSnapshotInfo` with the absolute snapshot path and repository `rootFolder` first. Preserve snapshot type, duration/time range and active filters in the evidence record. Then:

1. For **timeline** profiles, discover available filters with `dotTraceGetSnapshotFilters`; inspect timeline phases and use `dotTraceGetTimelineEvents` grouped summaries (`maxEvents=0`) for SQL, HTTP, file I/O and contention. Verify which event providers actually produced data; absent SQL/HTTP events do not prove absence of that work.
2. Drill into call trees with `dotTraceGetCallTree`, preserving returned node IDs as strings. Its default own-time threshold is 1%; set `minOwnTimePercent=0` when examining inclusive-time wrappers or many individually small calls. Explicitly record thresholds and truncation; repeatedly expand unexplained branches.
3. Filter by numeric thread IDs from snapshot filters and isolate the scenario time window. Do not pass friendly thread names as numeric IDs. Separate running/waiting and event units: time-related payloads use nanoseconds; memory payloads use bytes.
4. For **performance/sampling/tracing** profiles, use the call tree. Do not interpret ignored timeline filters or time-window arguments as successful scoping; those profiles do not provide the same timeline information.
5. Link each result to the source baseline, route/job, input scale, call count, own/inclusive time and relevant SQL/network evidence. Use `dotTraceOpenReport` only after reading its required report schema; the project-root Markdown remains the authoritative issue list.

Rider's documented capture workflow is selecting the run configuration and supported profiling configuration, starting a profile session, reproducing the scenario and saving the snapshot. The MCP run-configuration tool is an ordinary launcher, not proof of a profiled run. [Rider profiling workflow](https://www.jetbrains.com/help/rider/Profiling_Applications.html).

### dotMemory procedure

Establish a working CLI or UI and exact .NET 10 compatibility first. A documented CLI capability is attaching by PID or taking a `.dmw` snapshot using `get-snapshot`; verify syntax with the actual working installed tool before executing it. [JetBrains dotMemory CLI documentation](https://www.jetbrains.com/help/dotmemory/Working_with_dotMemory_Command-Line_Profiler.html).

For each of W3, W5 and W7, capture (A) warmed idle state, (B) immediately after a bounded workload, and (C) after completion, cancellation, disposal and a defined cache-expiry/quiescence period. Compare repeated equal-size bursts to distinguish expected bounded caches from sustained retention. Inspect response JSON, byte arrays, `MemoryStream`, hydrated pools, EF tracking, score snapshots and exports. Keep forced-GC experiments separate and label their perturbation. Managed snapshots alone do not explain native memory or RSS. If retained-object/dominator analysis cannot be accessed from this environment, leave that part explicitly blocked rather than substituting dotTrace allocation totals.

### SQL, network and browser procedure

- Count SQL commands per journey/job using available profiler events or an isolated DB diagnostic session. Analyze representative SELECTs with `EXPLAIN (ANALYZE, BUFFERS)` on the isolated database; `ANALYZE` executes the query. Check cardinality estimates, returned rows, index use, sequential scans, joins, sorts, locks, transaction duration and round-trip counts. Do not parallelize queries on one EF `DbContext` as a proposed shortcut.
- Trace outbound calls by provider/method and normalized nonsecret request identity. Record cache hits/misses, duplicates, pool reuse, DNS/connect/TLS timing where observable, response headers, status, retries, cancellation and compressed/decompressed bytes. Confirm actual HTTP handler defaults and deployment behavior before flagging missing connection pooling or compression.
- Capture browser journeys using browser/network tools and DevTools/HAR. The planning pass ran no browser measurement; the execution checkpoint records the controlled polling reproduction and MCP launch failure. Complete API waterfalls, search debounce/abort, main-thread work, repeated DOM rebuilding and asset transfers remain outstanding.
- Inspect deployed proxy/static asset headers separately from Kestrel. `Cache-Control: no-cache` permits conditional caching/revalidation; it does not mean every asset body is downloaded again. Private API `no-store` is intentional. Check Brotli/gzip negotiation and ETag/304 behavior before assigning an issue to the application.

## Execution checkpoint — 2026-10-24

### Coverage and environment

The regenerated inventory matches the approved plan: 486 files, 63,157 lines, no line-count differences. SHA-256 was frozen for every inventoried file; all 486 hashes still matched after the workloads. Existing unrelated work is excluded from edits. Product source and runtime configuration are unchanged.

| Area | Complete numbered source read | Disposition |
| --- | --- | --- |
| Domain | 47 files / 2,668 lines | Pure types, vector operations, both scorers, selection, feedback grouping, schedules and bounded evidence read; scoring callers traced into Application. Whole-layer reconciliation remains pending. |
| Application | 98 files / 3,412 lines | All handlers, interfaces, mappings, DTOs, configuration and lockfile read. Persistence/provider/endpoint and test reconciliation remains pending. |
| Traced implementation | 8 files / 1,540 lines | `DigestGenerationStore`, `DiscoveryBatchStore`, `DiscoveryWriteCoordinator`, `CatalogReader`, both storage files, `DiscoveryJobService`, all 521 lines of `discovery.js`. |
| Remaining | 333 files / 55,537 lines | Pending complete source read, including remaining DataAccess, Infrastructure, Presentation, Worker, tests, scripts and root/deployment files. |

The ledger's `Read 1–N` means every numbered line was read, **not** that the file is cleared or all paths exercised. No file is promoted to fully reviewed yet. P01/P04/P08/P10 have isolated measurements; other findings remain structural or conditional.

Host: Linux `7.2.11-200.fc44.x86_64`, Intel Core i5-14500HX, 20 logical CPUs, approximately 31.1 GiB RAM. This shared development host had uncontrolled background activity. SDK `/home/mahammadjafarli/.dotnet/dotnet` `10.0.401`; measured runtime `.NET 10.0.12`. Helpers reference the actual Application/Infrastructure projects, use optimized Release builds and portable symbols, and run without a debugger. An initial parallel MSBuild failed without diagnostics; rebuilding with `-m:1 --disable-build-servers` succeeded with zero warnings/errors. No application issue is inferred from that tooling failure.

### Tool preflight updates

- **B1 partially resolved: dedicated dotTrace capture works.** Official stable `JetBrains.dotTrace.GlobalTools` `2026.2.3.1` was verified and installed into the task-private tool directory. Sampling captured the isolated scorer on this exact runtime. A sandbox host-connection permission error required authorized unsandboxed execution. Product processes were not attached or restarted. Rider's bundled wrapper remains unusable as described above. [Official CLI distribution and capture](https://www.jetbrains.com/help/profiler/Performance_Profiling__Profiling_Using_the_Command_Line.html).
- **B3: Rider dotTrace analysis is license-blocked.** `dotTraceGetSnapshotInfo` with the explicit repository root and captured path returned: `dotTrace is not licensed. Activate a dotTrace / dotUltimate license to use these tools.` Snapshot pieces exist, but no call tree, hotspot attribution, timeline or own/inclusive method times were obtained. No license activation was attempted.
- Standalone dotTrace/dotMemory UI distributions exist under JetBrains Toolbox, but UI presence is not capture/analysis evidence. No dotMemory MCP tool or verified usable memory-analysis path was established. No `.dmw`, dominator/root, post-quiescence live-object or leak result exists. A failed lookup of a guessed global-tool package is not proof that the official memory CLI is unavailable. [Official memory capture distribution](https://www.jetbrains.com/help/dotmemory/Working_with_dotMemory_Command-Line_Profiler.html).
- **B4: Playwright MCP Chrome is missing.** Its launch failed before running the test because `/opt/google/chrome/chrome` was absent. The reproduction used cached Playwright `1.64.1` and Chromium `155.0.8059.12` through the Node API. This is browser evidence, not a successful MCP browser run. A sandbox socket error required authorized unsandboxed Chromium execution. No global browser installation or project package change was made.

### W7-S: Actual storage phases with isolated artifacts

`StorageAudit` calls the actual `LocalGenerationStorage` implementation in the coordinator's five-phase sequence: acquire coordinated lease, reserve, validate, verify stored bytes, publish committed inventory. It supplies a fixed accounting state and makes **no database connection or mutation**. Six category directories and deterministic zero-filled artifact files are private to this task. Reconciliation and one complete warm-up precede five ordinary samples per shape. Artifact construction/deletion are outside measurement.

**`/tmp` is tmpfs here.** These are warm, memory-backed filesystem results. `/proc/self/io` `rchar` deltas measure application read characters, not storage-device/network bytes. Physical `read_bytes` was zero in every sample. Small extra reads cover inventory and process-counter data. Wall time includes validation, hashing, async execution and inventory publication. Process CPU totals cover all helper threads; they must not be added to wall time.

| Artifact bytes | Files | Samples | Median wall ms (range) | Median process CPU ms | Median allocated bytes | Application read characters per operation |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 0 | 5 | 1.479 (1.205–2.686) | 1.663 | 89,112 | 869–3,365 |
| 100,000,000 | 1 | 5 | 453.742 (344.902–563.416) | 946.629 | 145,616 | 500,000,889–500,000,891 |
| 100,000,000 | 1,000 | 5 | 677.829 (412.721–905.112) | 1,454.673 | 50,875,488 | 500,006,011–500,007,036 |
| 900,000,000 | 1 | 5 | 3,847.635 (3,296.892–5,004.048) | 8,435.898 | 143,152 | 4,500,000,895–4,500,000,900 |

Process-wide allocation uses `GC.GetTotalAllocatedBytes(precise: true)` deltas and includes runtime work. The roughly 5× payload read relationship corroborates the source trace. Many-file runs also expose allocation cost. Physical-disk latency, SQL/transaction/advisory-lock wait, concurrent admission and heartbeat timeout were not measured. Near-limit publication itself took 644–1,060 ms; its five-second deadline was not exceeded. This measures P01's storage phases, not the total application write-path cost. Integrity checks remain unchanged.

### W3-S: Pure scorer and candidate merge

`ScorerAudit` uses actual `GenerationConfiguration.Scoring()` and `BaselineAScorer.RankForBatch`: ten candidate tags, taste vectors of 20 or 271 tags, distinct tracks/artists, empty evidence and no exclusions. Each shape receives at least 50 warm-up calls and one second of warm-up, then five measured ranks in each of five independent processes (25 samples). Allocation uses `GC.GetAllocatedBytesForCurrentThread`; scoring is synchronous. Selecting five finite scores is asserted. Selected-breakdown serialization occurs **after** timing/allocation measurement.

| Taste tags | Candidates | Samples | Median wall ms (range) | Allocated bytes per rank | JSON bytes for 5 selected breakdowns |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 20 | 10 | 25 | 0.499 (0.409–1.587) | 137,584–146,224 | 21,561 |
| 20 | 100 | 25 | 0.608 (0.535–1.155) | 1,338,104 | 21,577 |
| 20 | 400 | 25 | 3.429 (2.371–11.845) | 5,349,704 | 21,593 |
| 271 | 10 | 25 | 0.884 (0.334–1.327) | 375,824 | 21,076 |
| 271 | 100 | 25 | 3.861 (3.276–10.732) | 3,750,104 | 21,092 |
| 271 | 400 | 25 | 38.146 (14.275–47.488) | 14,997,704 | 21,108 |

Wide ranges and small operations limit latency comparisons; warm-up does not establish that tiering or host noise disappeared. Allocation supports P08 but does not attribute every byte to invariant setup. Selected JSON is serialized payload with empty evidence, not persisted column size, full recommendation payload, retained memory or wire traffic. No policy tuning or recommendation-quality evaluation occurred.

Five ordinary control processes each executed 500 warmed C=400/T=271 ranks: median 9,267.943 ms, range 8,417.096–10,985.032 ms. One matching dotTrace sampling run measured 9,617.793 ms inside the workload; capture lifecycle was 13.599 seconds. The profiled result lies inside ordinary variance, so a reliable overhead percentage cannot be inferred. Earlier short-warm-up/mixed-build experiments were excluded. B3 prevented opening the snapshot or attributing CPU to individual methods.

`MergeAudit` invokes actual private `PrepareDiscoveryBatch.Merge` through reflection, using distinct generated text identities and valid offline provenance. Construction/warm-up are outside measurement; reflection overhead is included. Each shape receives at least 20 calls and one second of warm-up, then five samples in each of five independent processes. Output count is asserted unchanged.

| Distinct candidates | Samples | Median wall ms (range) | Allocated bytes per merge |
| ---: | ---: | ---: | ---: |
| 10 | 25 | 0.024 (0.015–0.052) | 55,000 |
| 100 | 25 | 1.788 (0.663–2.669) | 3,139,904–3,140,152 |
| 400 | 25 | 19.101 (10.102–38.489) | 47,133,544–47,133,792 |

These generated shapes measure isolated costs; they are not demo recommendations or real-provider evidence. Callers establish use in live-capable discovery, but these runs exclude provider pacing, SQL, real evidence graphs, persistence and HTTP serialization. Do not add medians to estimate an end-to-end latency.

### W2-P: Controlled frontend retry reproduction

The exact unchanged `discovery.js` ran in Chromium with a minimal DOM, ordinary `fetch`, and routed session/overview/job responses. Every request under a reserved `.invalid` audit origin was intercepted; unrelated URLs were aborted and service workers blocked. `clock.install`, `pauseAt` and `runFor` controlled Date/timers. This measures timer policy and attempted HTTP requests, not elapsed transport time or live backend throughput. [Playwright clock semantics](https://playwright.dev/docs/clock).

With a running job and `429` plus `Retry-After: 60`, attempted poll timestamps in milliseconds were **0, 5,000, 10,000, 15,000, 20,000, 25,000, 30,000, 35,000, 40,000, 45,000, 50,000, 55,000, 60,000**. A controlled completed response arrived at 60 seconds. This establishes 11 premature retries before the first requested delay elapsed; subsequent rejections also each requested another 60 seconds. No more polls occurred during 65 virtual seconds after completion. In a fresh active-job scenario, leaving immediately after the first rejection prevented further attempts for 65 virtual seconds. No page errors occurred. Missing/malformed/date-form retry headers and complete UI journeys remain untested.

### Evidence, reproduction and outstanding work

Private root: `/tmp/liner-performance-audit-20261024/`, **volatile tmpfs rather than durable archive**. `evidence-manifest.json` records SHA-256/byte lengths for helper sources, build/capture logs, selected samples/aggregates and snapshot pieces. `inventory.json` and `coverage.json` retain frozen source hashes and read ranges. The committed ledger preserves source-read state if private files disappear. No raw snapshot, HAR, DB contents or private settings are committed.

| Evidence | Private paths under that root |
| --- | --- |
| Scorer | `ScorerAudit/{ScorerAudit.csproj,Program.cs}`, `scorer-steady-baseline-{1..5}.json`, `scorer-steady-control-{1..5}.json`, `scorer-aggregate.json`, `scorer-steady-sampling.log`, `scorer-steady-sampling.dtp*` |
| Storage | `StorageAudit/{StorageAudit.csproj,Program.cs}`, `storage-baseline.json`, `storage-aggregate.json`; generated artifacts deleted after measurement |
| Merge | `MergeAudit/{MergeAudit.csproj,Program.cs}`, `merge-baseline-{1..5}.json`, `merge-aggregate.json` |
| Browser | `polling-audit.cjs`, `polling-evidence.json`, `polling-error.log`; cached executable paths are recorded inside the helper |
| Tool setup | `dottrace-help.txt`, `dottrace-start-help.txt`, `dottrace-install.log`, `dottrace-versions.json`, `*-build.log` |

Rebuild helpers with the full pinned SDK path and `build <private-project-path> -c Release -o <private-output-path> -m:1 --disable-build-servers`. Run `dotnet scorer-bin/ScorerAudit.dll` five times for shape samples, and five times with `--profile` for 500-rank controls. Run the storage and merge DLLs similarly without overlapping measured CPU workloads. Browser command: `node /tmp/liner-performance-audit-20261024/polling-audit.cjs`. Input construction rules, assertions and bounds are above and in the private helper sources.

Sampling command after ordinary controls:

```sh
/tmp/liner-performance-audit-20261024/tools/dottrace start --framework=NetCore --profiling-type=Sampling --save-to=/tmp/liner-performance-audit-20261024/scorer-steady-sampling.dtp --propagate-exit-code --no-check-for-updates -- /home/mahammadjafarli/.dotnet/dotnet /tmp/liner-performance-audit-20261024/scorer-bin/ScorerAudit.dll --profile
```

This was the first execution checkpoint. The continuation below supersedes its source-coverage totals. Application-level W0–W8 workloads, SQL plans/counts, network/transfer attribution, concurrency/cold-cache sweeps and retained-memory analysis remain outstanding beyond these named subworkloads. No live-provider performance, production readiness, exhaustive clean bill or developer-confirmed completion is established.

Workflow observations: verify capture and licensed analysis separately; distinguish source reading from call-path/runtime reconciliation; freeze helper builds before aggregation; record tmpfs versus physical storage; use intercepted HTTP and a virtual clock to reproduce retry policy. These stay here so the pre-existing unrelated skill-observation log is preserved.

## Static continuation — 2026-10-24

Reviewed the remaining handwritten DataAccess, Infrastructure, Presentation and Worker sources, migration operations/SQL, tests and supporting scripts/configuration. Cross-checked the reported paths against dependency injection, controller/filter placement, provider bundles and regression-test contracts. The original checkpoint's 153 source-read entries were carried forward after checking their frozen hashes. All inventoried source hashes remained unchanged during this continuation; only this report was edited by the task.

| Review method | Files | Accounted lines |
| --- | ---: | ---: |
| Complete numbered source reads, including prior checkpoint | 362 | 28,911 |
| Ordered novel-line inspection with exact references to previously read test/tooling lines | 109 | 12,895 |
| Generated schema: exact unchanged blocks plus every changed declaration/index/relationship | 15 | 18,501 |
| Lockfile JSON parsing, exact baseline comparison and changed-line inspection | 9 | 3,641 |
| **Total** | **495** | **63,948** |

This includes nine additions to the earlier inventory: `.config/dotnet-tools.json`, `.dockerignore`, `.env.example`, both `.githooks` files and four `.cjs` browser/load checks (791 lines). Documentation, IDE/agent state, private runtime settings, ignored recordings, binaries and dependency internals remain outside this source count. No new executable source was found outside the expanded inventory in the tracked C#/JS/CJS/Python/shell/build/workflow paths. Source hashes and method dispositions are preserved below; temporary comparison maps and scan helpers are under `/tmp/liner-audit-continuation/` and are not permanent evidence storage.

Safeguards reconciled during the review:

- Global EF no-tracking and split queries are configured. Local catalog search batches result mapping; P11 concerns individual selection/lookup reads.
- Shared pool/public-evidence leases and tests cover same-key reuse. P02 concerns overlapping lower-level metadata requests across distinct pool scopes, not a claim that identical pools always duplicate work.
- Active discovery bounds feedback history; P09 is qualified to weekly generation's input/fingerprint path. The finite synthetic scheduler is not evidence of production scheduling capacity.
- Guest activity has an hour threshold before write admission. Private API `no-store`, complete exports, password hashing, account revocation, archive ownership verification and storage-integrity checks remain required constraints.
- Direct recording-tag tests support P16's path; origin checks and supporting-response policy still require preservation. Test source inspection is not a fresh passing test run.

Rider MCP was consulted again with the explicit project root: project discovery succeeded; project problems returned zero cached diagnostics. That is not a fresh performance inspection or profiler result. Earlier dotTrace analysis was license-blocked; no dotMemory analysis interface or retained-object evidence was established. The earlier raw evidence directory is absent, so its committed measurements remain historical results with the limitations above. This continuation adds static evidence only: no new timings, allocation totals, SQL plans or network-byte measurements.

Remaining runtime work is the W0–W8 plan: establish ordinary application baselines, capture usable licensed CPU/wait and memory evidence, collect SQL/lock/trigger plans and counts, then browser/provider wire-byte evidence and concurrency/cold-cache sweeps. Run approved live-provider scenarios only with their configured use gates and a bounded workload. Developer confirmation and deployment verification remain outstanding.

Workflow observation: inventory by tracked paths as well as extension, because hidden hooks and `.cjs` checks were omitted initially. Treat truncated/compressed source output as unread until recovered in bounded chunks. Compare generated blocks exactly and label that method explicitly; file/line totals are not proof of runtime attribution. These notes stay here to preserve the unrelated existing observation log.

## Findings register

All priorities below are **provisional investigation priorities**, not measured production severities. No hypothetical optimization saving is claimed. `Structural` means work visible in source; `conditional` means cost requires the stated trigger. Measurements apply only to their named isolated workloads and units above. Static review is complete for the ledger; runtime attribution remains unfinished.

The finding narratives below describe the original audit baseline. The remediation table above is the current disposition; historical "no fix applied" statements must be read in that baseline context.

| ID | Priority | Evidence status | Concern | Primary validation |
| --- | --- | --- | --- | --- |
| P01 | High | Measured storage phases; DB contention pending | Multiple full-artifact hashing passes per coordinated write | W7-S: five full reads; near-limit median 3.85 s on tmpfs |
| P02 | Medium | Conditional | Duplicate upstream metadata requests after concurrent cache misses | W4: count same-key outbound calls across distinct pools |
| P03 | Medium | Structural; cost unmeasured | Full-response buffering, extra array copy and JSON clone | W2/W3: allocation stacks and body-size sweep |
| P04 | Medium | Reproduced in controlled browser | Generation polling ignores the longer server retry delay | W2-P: 5-second attempts despite Retry-After 60 |
| P05 | Medium | Structural contention; cost unmeasured | Unrelated cold pools serialize on 32 stripe gates | W4: same-stripe versus different-stripe keys |
| P06 | Medium | Structural; cost unmeasured | Full discovery export is materialized in memory | W5: large valid history, peak live memory and allocation |
| P07 | Low | Structural; cost unmeasured | Idle discovery lanes poll the database every second | W0: count SQL at idle per host |
| P08 | Medium | Measured pure scorer allocation; CPU attribution pending | Invariant setup and snapshots repeated before selecting five picks | W3-S: 15.0 MB allocated at C=400/T=271 |
| P09 | Medium | Structural; scale-dependent cost unmeasured | Weekly generation reads/hashes full feedback history twice | W3/W6: history sweep and SQL/lock evidence |
| P10 | Medium | Measured isolated merge; end-to-end cost pending | Quadratic alias grouping rebuilds identities repeatedly | W3-S: 47.1 MB allocated at 400 distinct candidates |
| P11 | Medium | Structural query fan-out; runtime count pending | Sequential item fetching for catalog selection/completed lookup | W1/W2: 2N eligible-item SELECTs plus surrounding queries |
| P12 | Medium | Structural; cost unmeasured | Cache-wide quota aggregates on each public-evidence publication | W3/W7: cardinality sweep, plans and admission time |
| P13 | Medium, local capture | Structural; cost unmeasured | Account export/deletion scans and decrypts the shared email archive | W5/W6: fixed owned history, growing unrelated copies |
| P14 | Medium | Structural; cost unmeasured | Cold pools serialized and immediately decoded into another object graph | W3: cold/persistent/memory allocation comparison |
| P15 | High | Structural; concurrency impact unmeasured | Login password verification holds global write admission | W4: authentication plus unrelated writes |
| P16 | Medium | Structural; avoidability conditional | Artist metadata acquired and parsed before direct recording tags are selected | W2/W3: request count and allocations with valid direct tags |
| P17 | Low | Structural; cost unmeasured | Two sequential account-existence SQL checks on authenticated requests | W1/W2: SQL round trips under job polling |

### P01 — Repeated complete artifact scans inside serialized write admission

Locations: [LocalGenerationStorage.cs:139](src/Infrastructure/Storage/LocalGenerationStorage.cs#L139), [CoordinatedGenerationStorage.cs:94](src/Infrastructure/Storage/CoordinatedGenerationStorage.cs#L94), [DiscoveryWriteCoordinator.cs:51](src/DataAccess/Persistence/Repositories/DiscoveryWriteCoordinator.cs#L51), [DiscoveryJobService.cs:97](src/Presentation/Background/DiscoveryJobService.cs#L97).

`ValidateArtifactsAsync` calls `MeasureAsync`, which recursively enumerates configured artifact roots and opens/hashes each file with SHA-256 (lines 148–193). A successful ordinary `DiscoveryWriteCoordinator.ExecuteAsync` invokes artifact validation during acquisition, reservation, pre-write validation, post-write verification and committed-inventory publication: **five complete passes**, excluding caller-level work. The batch/storage lease surrounds this work; some passes also occur inside the DB transaction. Heartbeat renewal uses the same coordinator for a small mutation.

If managed artifacts contain A bytes across F files, this path performs approximately O(5A) application-level content reads and O(5F) file work per write. OS page cache can reduce physical reads but not all hashing/enumeration cost. Near the configured storage cap, the amount of revalidation can dwarf the mutation. Mandatory integrity validation is intentional: the issue to investigate is repeated total work and lock duration, not whether checks should be removed.

Execution update: W7-S above reproduced the five passes and measured ordinary storage-phase time, process CPU, allocation and application read counts at 0/100 MB/near-limit artifacts, including a many-file case. Physical-disk behavior, method-level profile attribution, transaction time, lease contention and heartbeat latency remain outstanding. Preserve fail-closed tamper and cross-process guarantees. No fix applied.

### P02 — Cache miss is checked before pacing, without a second lookup

Locations: [ListenBrainzApiClient.cs:25](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzApiClient.cs#L25), [MetaBrainzRequestGate.cs:43](src/Infrastructure/RecommendationSources/ListenBrainz/MetaBrainzRequestGate.cs#L43), [SharedDiscoveryPreparation.cs:49](src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs#L49).

The metadata response cache is checked once before awaiting the cross-process request gate. Different pool acquisitions can miss the same metadata key, wait for pacing, then each send HTTP even if an earlier request has populated the cache. Pool-level stripe gating and persistent public-evidence leases reduce some duplication but do not themselves coalesce every shared metadata key across different pools/processes. The gate enforces spacing, not cache-key deduplication.

For C overlapping acquisitions of an otherwise cacheable metadata key, up to C requests are possible instead of one, conditional on timing, process cache boundaries and the acquisition paths actually running concurrently. Each duplicate consumes provider quota and body bytes; the gate's configured 1.05-second spacing can increase queue time. Reproduce with distinct pools sharing metadata and a bounded deterministic upstream stand-in; separately test same-process and cross-process behavior. Count requests and cache state before proposing coalescing or a second lookup. Do not relax mandatory pacing. No fix applied.

### P03 — Extra response-sized allocations during metadata parsing

Locations: [ListenBrainzApiClient.cs:49](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzApiClient.cs#L49), [ListenBrainzContracts.cs:103](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzContracts.cs#L103).

The bounded response is accumulated in a growing `MemoryStream`; `output.ToArray()` creates a separate full-body array; `JsonDocument.Parse` then parses it and `RootElement.Clone()` preserves an independently owned JSON value. The temporary body, copied array and parsed/retained representation overlap during processing. The response cap is 1,000,000 bytes, which limits individual exposure but does not eliminate churn across many responses.

One explicit avoidable-looking copy allocates N additional bytes for an N-byte body, in addition to stream capacity and JSON storage. Exact peak memory and JSON internal allocation behavior need profiling; this is not a leak claim. Capture allocation stacks for 10 KB/100 KB/near-limit valid bodies, compare LOH/GC activity and peak memory, and include cancellation/error responses. Any future approach must retain response byte caps, provenance hashing and independent JSON lifetime. No fix applied.

### P04 — Generation poll retry loop disregards Retry-After

Locations: [discovery.js:45](src/Presentation/wwwroot/discovery.js#L45), [discovery.js:125](src/Presentation/wwwroot/discovery.js#L125).

The shared request helper reads and clamps `Retry-After` and, on 429, stores a deadline in `page.nextRequestAtUtc`. The generation polling error branch nevertheless schedules its next request after a fixed 5,000 ms. The stored deadline feeds the generation button's cooldown, not this polling schedule. A server asking the client to wait 60 seconds can therefore receive repeated premature polls, roughly twelve polling attempts per minute ignoring response time, with rejected traffic and repeated error handling.

Execution update: W2-P above reproduced five-second attempted requests during controlled `429`/`Retry-After: 60`, then verified completion and active-view cancellation. Absent/malformed/date-form headers and complete journeys remain outstanding. Existing two-second success polling is a separate policy choice whose cost should be measured, not automatically called a defect. No fix applied.

### P05 — Unrelated cold pool keys share a long-held stripe gate

Location: [SharedDiscoveryPreparation.cs:15](src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs#L15), especially lines 49–60 and the surrounding acquisition/hydration body.

Prepared-pool acquisition uses 32 semaphores selected from the first hash byte modulo stripe count. It holds the selected semaphore across upstream discovery, hydration and publication preparation, with a 45-second linked deadline started **after** acquiring the gate. Different keys mapping to one stripe serialize despite not being the same pool. At enough distinct cold keys, collisions are unavoidable; even two chosen keys can reproduce the condition.

The synchronization is bounded and may be an acceptable tradeoff. Profile paired same-stripe/different-stripe cold requests and a 33-distinct-key offline burst. Attribute semaphore wait independently from mandatory provider pacing, public-evidence leases and the job runner's intentionally limited acquisition concurrency. Verify concurrent reachability before elevating this lead; a single active acquisition lane may mask it. No fix applied.

### P06 — Export holds all discovery history and mapped results simultaneously

Location: [DiscoveryProfileReader.cs:32](src/DataAccess/Persistence/Repositories/DiscoveryProfileReader.cs#L32); reconciled with `GetUserDataExportQueryHandler`, `ExportController`, `app.js` and `discovery.js` during the continuation.

The reader separately materializes all profile batches, jobs, recommendations with tracks, taste signals and recommended identity aliases using `ToArrayAsync`, then constructs DTO arrays. JSON fields are parsed into retained elements. There is no history pagination/bound within this complete-export reader. Work and live memory grow with the profile's stored history; some entity arrays overlap the mapped output arrays.

Full export is a privacy requirement. This is a scale-dependent memory/latency concern, not a recommendation to omit data or cap the export. Measure realistic and maximum plausible account histories, time to first/last byte, allocated bytes, peak live memory and concurrent exports. Trace the complete response pipeline before estimating serialization overhead or recommending a snapshot-consistent streaming design. No fix applied.

### P07 — Empty job queues still cause recurring SQL round trips

Locations: [DiscoveryJobService.cs:12](src/Presentation/Background/DiscoveryJobService.cs#L12), [DiscoveryJobStore.cs:9](src/DataAccess/Persistence/Repositories/DiscoveryJobStore.cs#L9).

When enabled, the host runs a prepared lane and an acquisition lane. At idle, the prepared lane checks generation readiness once and the acquisition lane checks lookup and generation readiness, then each sleeps one second. `HasReadyAsync` is a database `AnyAsync`, so an idle instance performs approximately three readiness SQL commands per loop interval plus execution time, independent of useful jobs. This scales with enabled web-host instances.

These indexed existence checks may be cheap and the one-second responsiveness may be intentional. Measure idle commands/sec, query plans, DB network traffic and CPU before treating the polling policy as worth changing. Keep it separate from expensive coordinated write admission: no-ready checks do not themselves invoke that coordinator. No fix applied.

### P08 — Repeated batch-invariant scorer work before selecting five picks

Status: **structural duplication plus measured whole-scorer allocation; method CPU attribution pending**. Locations: [BaselineAScorer.cs:59](src/Domain/Scoring/BaselineAScorer.cs#L59), [BaselineAScorer.cs:85](src/Domain/Scoring/BaselineAScorer.cs#L85), [BaselineAScorer.cs:156](src/Domain/Scoring/BaselineAScorer.cs#L156), [PrepareDiscoveryBatch.cs:76](src/Application/Features/Discovery/PrepareDiscoveryBatch.cs#L76), [GenerateDigestCommandHandler.cs:102](src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs#L102).

Every candidate's `ScoreCore` validates configuration, recreates a sorted taste dictionary through an intermediate dictionary, recalculates taste magnitude, and reads `configuration.Sha256`, which validates, orders/formats policy and hashes it again. It constructs a complete score snapshot before ranking keeps at most five picks. Large tastes still incur full sorting even when persisted taste is trimmed to overlaps. At C candidates and T taste tags, the repeated invariant taste preparation alone has O(C × T log T) work; configuration hashing also repeats C times. Candidate vectors, overlap calculation and output explanations still require candidate-specific work.

Reachability: `RunDiscoveryJob.ExecuteAsync` calls shared preparation; `PrepareDiscoveryBatch` calls `ScoreForBatch` or weekly `Score`, both using `ScoreCore`. The older weekly branch calls `Rank`, which also scores before selection. `GenerationConfiguration` caps candidates at 400 and picks at five. W3-S measured actual `RankForBatch` (same core) at 400 candidates/271 taste tags: 14,997,704 allocated bytes per rank, median 38.146 ms with 14.275–47.488 ms range. Five selected breakdowns serialized to 21,108 bytes with empty evidence. This is allocation churn, not retained memory or a proven leak. Full real-evidence generation may have a different cost distribution.

Future remediation to evaluate: prepare immutable batch-invariant state once, and investigate delaying full snapshot construction until selected scores are known. Preserve exact deterministic scores/order, configuration hashes, selected explanation evidence, unknown JSON fields and snapshot limits; sanity-check existing scorer/benchmark fixtures. The measurement does not establish what fraction would be saved. No fix applied.

### P09 — Weekly generation materializes and fingerprints all historical feedback

Status: **structural scale-dependent work, runtime SQL/lock cost unmeasured**. Locations: [DigestGenerationStore.cs:38](src/DataAccess/Persistence/Repositories/DigestGenerationStore.cs#L38), [DigestGenerationStore.cs:123](src/DataAccess/Persistence/Repositories/DigestGenerationStore.cs#L123), [GenerateDigestCommandHandler.cs:38](src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs#L38).

`ReadInputsAsync` fetches all profile taste signals and all feedback-bearing recommendations with tracks, then serializes revision input to a complete UTF-8 array and hashes it. No feedback window exists in this method. Generation calls it before choosing the shared path; `PersistCoreAsync` calls it again while holding `SHARE ROW EXCLUSIVE` table locks on Users, TasteSignals, WeeklyRecommendations, WeeklyDigests and Tracks. Therefore the shared runtime's bounded preparation does not eliminate these two full-history loads/fingerprints. Cost grows with stored feedback/signals; materialization/JSON conversion and the lock-holding portion need measurement. A non-existing digest that reaches persistence is the trigger; existing-digest fast returns do not take this path.

Important false-positive boundary: active discovery's `DiscoveryBatchStore.ReadInputsAsync` uses `Take(feedbackWindow)`, default 20, validated maximum 40. `EffectiveTasteInputs.Build` has nested alias-group scans, but it must **not** be reported as unbounded history in that active discovery path. The older weekly branch can feed it the unbounded history from `DigestGenerationStore`; with shared jobs enabled, this branch is not used. Weekly full-history reads/fingerprints still occur before and during persistence in the shared branch.

Validate W3/W6 with isolated increasing histories, SQL command/row/payload counts, allocation/peak live memory and lock duration; include idempotent existing-digest controls. Consider a transactional revision/projection approach only after proving consistency with feedback and account mutations. Never truncate exports or silently change feedback policy to optimize this. No fix applied.

### P10 — Candidate deduplication repeatedly reconstructs aliases in a nested scan

Status: **structural quadratic growth plus measured isolated allocation/time**. Locations: [PrepareDiscoveryBatch.cs:126](src/Application/Features/Discovery/PrepareDiscoveryBatch.cs#L126), [GenerateDigestCommandHandler.cs:170](src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs#L170), [BaselineAScorer.cs:181](src/Domain/Scoring/BaselineAScorer.cs#L181).

The shared preparation merge visits every existing group and its members for each new candidate. Each comparison rebuilds the existing track's aliases and a LINQ intersection. With N distinct one-member groups this performs N(N−1)/2 comparisons: 79,800 at the configured maximum of 400. The weekly legacy merge similarly reconstructs tracks and aliases inside its comparison loop. Transitive grouping is required, but these particular allocations are repeated work.

W3-S exercised the actual shared private merge, not a reimplementation: 100 distinct candidates allocated about 3.14 MB; 400 allocated about 47.13 MB. At 400, observed median was 19.101 ms, range 10.102–38.489 ms, across 25 samples. Reflection overhead is included. The test uses distinct generated text aliases with valid offline provenance; real duplicate/MBID distributions can differ. It does not measure the older merge or whole generation, and no CPU call tree or retention result was obtained.

Evaluate precomputed aliases and an identity-to-group index/union approach in future remediation, preserving transitive MBID/text identity merging, provenance aggregation, deterministic preferred-record selection, exclusion behavior and output ordering. Test bridge-alias cases as well as all-distinct/duplicate-heavy shapes before any optimization claim. No fix applied.

### P11 — Catalog item loops fan out into sequential database reads

Status: **structural query fan-out, actual SQL count/latency unmeasured**. Locations: [SelectDiscoveryTaste.cs:40](src/Application/Features/Discovery/SelectDiscoveryTaste.cs#L40), [CatalogLookupJobs.cs:44](src/Application/Features/Catalog/CatalogLookupJobs.cs#L44), [CatalogReader.cs:74](src/DataAccess/Persistence/Repositories/CatalogReader.cs#L74).

Saving onboarding/taste resolves selected catalog IDs one at a time. Reading a completed provider-lookup result similarly calls `catalog.GetAsync` for each of at most ten IDs. The concrete reader issues an eligibility SELECT and then a records/publication SELECT for each eligible ID. Structurally, N eligible items require 2N item SELECTs in sequence, plus the surrounding profile/job work; a ten-result completion therefore has 20 item SELECTs before surrounding work. Expired/missing items can take only the first query. Execution-strategy retries and connection/transaction overhead are not included in this source count.

`SearchAsync` already batches its selected result IDs; typing local search is a separate path and should not inherit this finding. No upstream calls occur in `CatalogReader`. The impact depends on round-trip latency, selected-item count and how often a completed lookup is read. Trace actual SQL in W1/W2 before assigning latency. A future batch reader could reuse existing mapping while preserving per-item eligibility, order, expiry and retained selection fallback; do not parallelize against the shared DbContext. No fix applied.

### P12 — Public evidence publication scans cache-wide quota totals

Status: **structural database work; latency and query plans unmeasured**. Priority: medium. Location: [PublicEvidenceStore.cs:48](src/DataAccess/Persistence/Repositories/PublicEvidenceStore.cs#L48); caller: `SharedPublicEvidence.GetAsync` publication through coordinated write admission.

Every successful publication computes `SumAsync(StoredBytes)` and `CountAsync` over all `PublicEvidenceCache` entries before quota decisions. The current indexes cover key and expiry, without a maintained aggregate for these totals. At E entries, each publication requests cache-wide aggregate work, even when its own payload is small; the configured maximum permits 50,000 entries. Eviction additionally selects/sorts up to 100 victims. This happens inside the global admitted mutation, so aggregate time also extends its lock/transaction lifetime. It is separate from P01's filesystem scans.

Measure increasing cache cardinality and payload size with SQL plans/buffers, publication latency and admission contention. A future maintained total would need the same transactional and cross-process quota guarantees. Bounded cache size is acknowledged; no measured bottleneck or savings is claimed. No fix applied.

### P13 — Account archive export reads and decrypts other accounts' email copies

Status: **structural cross-account work; runtime cost unmeasured**. Priority: medium when local capture is enabled. Locations: [LocalEmailArchive.cs:29](src/Infrastructure/Email/LocalEmailArchive.cs#L29), [GetUserDataExportQueryHandler.cs:90](src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQueryHandler.cs#L90).

An enabled account export enumerates the shared copy directory, reads each encrypted file and parses/decrypts its MIME and receipt **before** checking `Receipt.UserId`. The global directory limit is 10,000 entries; one account's export therefore scales with everybody's retained copies, not only its own. For each owned copy, another linear search over that account's digest list runs. Deletion similarly scans/authenticates the shared archive while holding the artifact lease. Authentication before deleting an unknown file is required; this finding concerns the shared layout/access path, not removing ownership checks.

The ordinary live guest host rejects concurrent local email capture, so this is an optional legacy/local-capture path, not an assertion about every live page. Measure fixed own history while growing other accounts' copies, recording bytes read, decrypt/parse allocations and lease time. A future authenticated ownership index or per-owner layout must preserve legacy-copy discovery, complete export and safe deletion. No fix applied.

### P14 — Freshly acquired prepared pools are serialized and decoded immediately

Status: **structural repeated representation work; cost unmeasured**. Priority: medium. Locations: [PersistentDiscoveryPreparation.cs:13](src/Infrastructure/Discovery/PersistentDiscoveryPreparation.cs#L13), [PublicEvidenceCodec.cs:22](src/Infrastructure/Discovery/PublicEvidenceCodec.cs#L22), [SharedDiscoveryPreparation.cs:88](src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs#L88).

On a cold pool acquisition the inner preparation already constructs hydrated candidates and serializes them for size/hash accounting, then caches that prepared object when eligible. The persistent adapter encodes that object into another JSON representation for public evidence and unconditionally decodes the returned document into new tracks, vectors and candidate arrays, replacing the memory entry with the decoded pool. The fresh path therefore constructs two object graphs and multiple serialized representations of the same bounded pool. Persistent-hit decoding is necessary to restore stored evidence; immediate decoding of a just-produced object is the additional work to measure.

`SharedSeedPreparation` also caches a serialized document and decodes it on each memory hit; this is the same representation tradeoff at seed scale. Profile separate cold, persistent-hit and memory-hit cases up to 400 candidates, including checksum and provenance validation. Any future shortcut must validate equivalent evidence and must not bypass persisted-document integrity boundaries. No fix applied.

### P15 — Login verification holds global write admission

Status: **structural critical-section expansion; concurrent effect unmeasured**. Priority: high. Locations: [DiscoveryWriteFilter.cs:12](src/Presentation/Filters/DiscoveryWriteFilter.cs#L12), [AuthController.cs:21](src/Presentation/Controllers/AuthController.cs#L21), [AuthService.cs:140](src/Presentation/Services/AuthService.cs#L140).

With `DiscoveryRuntime.RunJobs=true`, the filter admits every non-GET action on `AuthController` before executing it. The login action consequently performs account lookup, password-hash verification, failure-count updates and successful-session storage inside the globally coordinated transaction/lease. Even a rejected login follows the admitted path. Required password hashing and lockout accounting can therefore hold admission needed by unrelated taste, discovery and heartbeat writes. P01's artifact revalidation adds to this cost, but the broad placement of CPU-heavy authentication inside admission is a separate concern.

Validate successful, wrong-password and unknown-account requests alongside unrelated discovery writes, measuring admission duration and failed/busy responses as well as latency. Future design must retain serialized accounting and atomic lockout/session updates; do not weaken password hashing or security checks to improve timing. No fix applied.

### P16 — Artist metadata is fetched before direct recording tags are selected

Status: **structural acquisition and parsing; avoidability conditional on evidence requirements and cache misses**. Priority: medium. Location: [ListenBrainzRecommendationProvider.cs:115](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecommendationProvider.cs#L115); regression boundary: [RecordingDiscoveryTests.cs:12](tests/Infrastructure.Tests/RecommendationSources/RecordingDiscoveryTests.cs#L12).

Hydration gathers every candidate's uncached artist IDs, requests metadata in chunks of 100, and builds artist tag vectors per candidate. Only afterward does it inspect direct recording tags and select those when valid. In that case the selected vector and tag observations come from the recording, yet artist acquisition/parsing has already happened. The existing recording test explicitly supports candidates with direct tags and empty artist tags. At A uncached distinct artist IDs, hydration requests ceil(A/100) artist-metadata responses even when every candidate has usable direct tags; HTTP/public cache hits can avoid wire traffic, and calls are not one per candidate.

Artist responses also remain in `SupportingResponses`, their origins are checked, and artist gaps are carried forward. Whether every such response is mandatory is a policy question; a change must preserve required provenance and gap semantics. Measure a controlled direct-tag-rich batch against an artist-fallback batch, recording cold/warm request counts, bytes and parsing allocations. Missing direct tags still require the approved fallback behavior. No live request, runtime saving or fix is claimed.

### P17 — Authentication adds two sequential existence queries per request

Status: **structural round trips; cost unmeasured**. Priority: low. Location: [DependencyInjection.cs:129](src/Presentation/DependencyInjection.cs#L129).

For an otherwise valid bearer token, `OnTokenValidated` awaits an Identity user existence query and then a Domain user existence query before allowing the endpoint to execute. Each authenticated job poll or API read therefore carries these two checks in addition to its endpoint queries. Missing/invalid accounts short-circuit. Both checks use IDs and may be cheap; actual latency depends on database placement and polling volume.

Immediate account-revocation semantics are intentional and tested. Investigate a single database-side eligibility query only after measuring command count and duration; do not introduce stale authorization caching or remove either eligibility condition. Keep this separate from P07's background queue polling and P11's item retrieval. No fix applied.

## Pending checks that are not confirmed issues

- The host sets API `no-store` and static assets `no-cache` in [Program.cs:133](src/Presentation/Program.cs#L133). No application response-compression middleware was found in the initial search. Actual compression and cache behavior may be handled by a reverse proxy. Measure negotiated encoding, transfer size, ETag/304 and deployment headers first; do not report absent compression or complete repeated downloads as established facts.
- `ListenBrainzRecommendationProvider.HydrateCandidatesBatchAsync` repeatedly searches `responses.Values.FirstOrDefault` for recording metadata inside its candidate loop (lines 123–138). Candidate count is capped at 400 and artist metadata is already batched in chunks of 100. This is a CPU lead, not yet evidence that indexing it would matter.
- `DiscoveryReader.ReadAsync` makes several sequential queries and reloads the profile's onboarding field after querying its revision (lines 15–32). Count round trips and inspect consistency needs before proposing consolidation. Do not parallelize against a shared DbContext.
- `RecentRecommendations.ReadAsync` checks catalog eligibility by canonical recording identity, while the catalog's unique index leads with publication ID. Inspect the actual join/EXISTS plan at representative catalog sizes before calling this a missing-index bottleneck; the planner can choose set-based work.
- Managed-storage row triggers serialize old/new rows with `to_jsonb` and update a shared ledger row. This is required accounting and a possible write-amplification/lock cost during large imports; measure trigger time and WAL on an isolated database before proposing a transactionally equivalent aggregate strategy.
- The account data screen previews a complete JSON export, then download can request another complete export. This may repeat SQL, serialization and bytes for a preview-then-download journey (P06). Measure the journey and establish freshness/privacy requirements before recommending reuse.
- All inventory entries now have a static disposition. None establishes a clean bill of health for paths that were not exercised, deployed proxy behavior, retained objects or live-provider performance.

## Required finding format for the full audit

For each issue append: stable ID; provisional/measured priority; status (`structural`, `conditional`, `measured`, `disproved`, `accepted tradeoff`); exact baseline file/line/symbol; reachable caller and route/job; trigger and input scale; evidence paths and tool settings; call/query/request counts; cost in correctly labeled units; reproduction steps; confidence and uncertainty; possible remediation and constraints; verification that would accept/reject it; developer disposition. Disproved leads stay documented with their disproof, rather than silently disappearing. Merge duplicate root causes and cross-reference their affected workloads.

Completion requires all ledger entries reviewed or explicitly outside scope with reasons, every proposed workload executed or explicitly blocked, every measured finding tied to reproducible evidence, and remaining uncertainty listed. Separate static review coverage, runtime scenario coverage, provider evidence, deployment evidence and developer confirmation. Source coverage alone does not establish runtime coverage or production readiness.

## Verification of this report

Only this Markdown document is intended for the task commit. Validate that every referenced file and starting line exists, the full inventory matches the frozen checkout plus the recorded dirty baseline, finding IDs are unique, every entry has a static disposition, and aggregate totals match the ledger. Review the Git diff for accidental product/configuration changes. No new build or application test run is required for this documentation-only continuation; no application behavior was changed or new runtime performance verified. Commit locally after these document checks; do not push.

Continuation verification passed: `git diff --check`; 495 ledger entries totaling 63,948 lines; all 495 frozen SHA-256 hashes and line counts unchanged; 42 local source links/starting lines resolve; P01–P17 appear exactly once as finding sections; tracked code-path inventory reconciliation found no omitted C#/JS/CJS/MJS/Python/shell/SQL/project/solution/build/workflow/hook files. The unrelated workflow, observation-log and security-report changes were preserved and excluded from staging. Application tests were inspected, not rerun. Developer confirmation remains pending.

## Task-observer note

A useful reusable profiling preflight is: enumerate actual MCP tools, confirm the IDE's active project, probe installed capture-tool help, verify runtime/platform support, and only then describe an executable capture plan. Tool registration, installed filenames, allocation totals and retained-object analysis are distinct evidence. This observation is recorded here without modifying the pre-existing observation log or installed skills.

## Coverage ledger

The inventory covers tracked text source, tests, helper scripts, workflow files and root build/runtime configuration, including the nine continuation additions. Documentation, private/ignored state, IDE/agent state, binaries and external dependency internals are excluded. Counts include blank/comment lines. `R` records a complete numbered source read; `E` records ordered inspection of novel test/tooling lines with exact previously read line references; `G` records generated-schema unchanged-block comparison and changed-declaration review; `L` records JSON lockfile comparison and changed-line review. Each disposition covers lines 1–N; it is a static review status, not proof of execution, zero defects or developer-confirmed completion. SHA-256 identifies the reviewed content, including the pre-existing dirty Qodana workflow.

<!-- COVERAGE_LEDGER -->

| Area | Files | Lines |
| --- | ---: | ---: |
| .config | 1 | 11 |
| .githooks | 2 | 103 |
| .github | 2 | 125 |
| Root build/config | 14 | 591 |
| scripts | 34 | 4,248 |
| src/Application | 98 | 3,412 |
| src/DataAccess | 79 | 24,387 |
| src/Domain | 47 | 2,668 |
| src/Infrastructure | 48 | 4,225 |
| src/Presentation | 37 | 6,221 |
| src/Worker | 8 | 743 |
| tests/Application.Tests | 29 | 3,349 |
| tests/DataAccess.Tests | 25 | 4,755 |
| tests/Domain.Tests | 23 | 1,938 |
| tests/Infrastructure.Tests | 28 | 3,615 |
| tests/Presentation.Tests | 20 | 3,557 |
| **Total** | **495** | **63,948** |

| File | Lines | Static disposition | SHA-256 |
| --- | ---: | --- | --- |
| `.config/dotnet-tools.json` | 11 | E: 1–11; no separate finding; runtime limitations apply | `8fff2d5078c590b5233a87dc47cea2f10f7fea99894bc6e5c02c03d2bdc7b8d4` |
| `.dockerignore` | 30 | E: 1–30; no separate finding; runtime limitations apply | `491bca91b2b29666ce52be19063be7c4fc36be2ea4507d11b87ac1447bfc5184` |
| `.env.example` | 6 | E: 1–6; no separate finding; runtime limitations apply | `4528e400d0f2722ef11cf9eb57c5443219082dc8343f938b4698c0e4dbb2a591` |
| `.githooks/pre-commit` | 37 | E: 1–37; no separate finding; runtime limitations apply | `b30d72cf80465539b57f8f71cf18148b02392f3c29a9bb76dbadd711fef37531` |
| `.githooks/pre-push` | 66 | E: 1–66; no separate finding; runtime limitations apply | `555f09fd589fee92ee60a436bd2ea20cbc97ba123c671455ec3dd8a87499ad59` |
| `.github/workflows/ci.yml` | 97 | E: 1–97; no separate finding; runtime limitations apply | `099ad828e5c9d6c652a13edd96dea31ed5efb8094a3122fd39797c46efc5274a` |
| `.github/workflows/qodana.yml` | 28 | E: 1–28; no separate finding; runtime limitations apply | `c3dc05d9245cc281c19652e49ae9c765e427f20973c776cfb7da089544f03d03` |
| `Directory.Build.props` | 9 | E: 1–9; no separate finding; runtime limitations apply | `73118fbef773f142f1c4e6c30e639c3cd6f8a934b02cac98c2982b3c4bc882c1` |
| `Directory.Packages.props` | 64 | E: 1–64; no separate finding; runtime limitations apply | `6ceff15c7e790a1d7b8dbbbb03d39a8562c4c84abe4384ee211a5a898a013bac` |
| `Dockerfile` | 50 | E: 1–50; no separate finding; runtime limitations apply | `3bb642b4fb882ca4117e787832eedea5cd87fcc8c2a0e64883b58731cd39c9db` |
| `Liner Notes.csproj` | 39 | E: 1–39; no separate finding; runtime limitations apply | `0821381aec6c91ebea4064b5cc048be8d7a9876664730637dc7972421eee3954` |
| `Liner Notes.sln` | 197 | E: 1–197; no separate finding; runtime limitations apply | `7a27825c78650b9c296b0a42ea1aba5bf1d65fed7d11a12034a5c3eccc3057d2` |
| `Liner Notes.slnLaunch` | 11 | E: 1–11; no separate finding; runtime limitations apply | `bde7a77aa1dc8d4f6bd44dff21359069abe0ca1c26cd9cd6aa2383fc1ec4c2a5` |
| `Program.cs` | 3 | E: 1–3; no separate finding; runtime limitations apply | `8b19830b25f98c46c4879557c4c3f9aed8bd772dff4d821a84d074af69bbca80` |
| `compose.ide.yaml` | 6 | E: 1–6; no separate finding; runtime limitations apply | `38850b904ee107bd648c8fd30a87b21669b210d522a1952279ac7533a1bf9ab4` |
| `compose.yaml` | 132 | E: 1–132; no separate finding; runtime limitations apply | `20829b8b528f94b2d437185d34303883b8c867262729380a8b4666c84327e3d9` |
| `dotnet-tools.json` | 13 | E: 1–13; no separate finding; runtime limitations apply | `12af4a55d97fd3fe6d9db3377ddb36b28c33281f0218033610da89f323f52ef8` |
| `global.json` | 7 | E: 1–7; no separate finding; runtime limitations apply | `4e7d5022b2a5a8d02bb951bb264a4c8613eabc332572a0af5f9743d23f23d84e` |
| `qodana.yaml` | 24 | E: 1–24; no separate finding; runtime limitations apply | `116ee1e33afd314880a6617e832d6024ced8a7ea10a4a8da4bb33622d8fdf066` |
| `scripts/acquisition_guard.py` | 97 | E: 1–97; no separate finding; runtime limitations apply | `178c91d20669c20bcc5d627bc5f9792302107aa29dc4bd1a923e5387c6552b29` |
| `scripts/catalog/catalog_probe.py` | 471 | E: 1–471; no separate finding; runtime limitations apply | `910bb8cb731ea4819801ed5cfc2206fc63d065d9764d2b1c3a4c3cdfa6bdb24f` |
| `scripts/catalog/test_catalog_probe.py` | 175 | E: 1–175; no separate finding; runtime limitations apply | `459e0f3cbcd3912425bf5ceae73045bc509ce8fd483173e29907b71858564461` |
| `scripts/checks/guest_discovery_browser.cjs` | 213 | E: 1–213; no separate finding; runtime limitations apply | `f8892f0ff69e30e55b325c847f9dea70f689154f8b9d0df031c5e080c69e33b0` |
| `scripts/checks/guest_discovery_load.cjs` | 111 | E: 1–111; no separate finding; runtime limitations apply | `310c34cde6364bbe03ef8e753b21f520c6d2dd6eeed4dd5487abdaa127816881` |
| `scripts/checks/optional_taste_browser.cjs` | 126 | E: 1–126; no separate finding; runtime limitations apply | `9d68bd4fc5f1591cf926f7bf14bf09bec845af4fb9159fe7a529f4e601ddf6f2` |
| `scripts/checks/phase1_browser.cjs` | 191 | E: 1–191; no separate finding; runtime limitations apply | `fc808218d52980e608df5e7a2cdbf341c01ec78a25490117045e53ffba49b8fe` |
| `scripts/checks/phase9_compose.py` | 215 | E: 1–215; no separate finding; runtime limitations apply | `01600de888934a66a01427c7adf1d88de9a0e8a9cdeaf22563038db9b90a505e` |
| `scripts/checks/phase9_test_results.py` | 55 | E: 1–55; no separate finding; runtime limitations apply | `f5fcd56a8dc012eb05c0bc062052dfe6b5fd99e9b0d49ff2bf02a870d4535966` |
| `scripts/checks/renewal_docs.py` | 13 | E: 1–13; no separate finding; runtime limitations apply | `543c2c5f2601a1fc56c295714c2494ef0ed068b50c6de3a97225d7a0c0937987` |
| `scripts/checks/secret_defaults.py` | 44 | E: 1–44; no separate finding; runtime limitations apply | `0595243f4a3be0597f7248f74ff3e933f0103f23ac321e47c1b4067e2be51b17` |
| `scripts/checks/test_phase9_helpers.py` | 77 | E: 1–77; no separate finding; runtime limitations apply | `d6cff3efc8f2b767391c43f7f759fa0c8e2476264d30888e2bf1463d0e74736a` |
| `scripts/git-qa-reviewer.py` | 26 | E: 1–26; no separate finding; runtime limitations apply | `26a205c5c640b04312ef19440db30ef69f3f69a03921f30404bf5e763d6ad284` |
| `scripts/git-security-reviewer.py` | 111 | E: 1–111; no separate finding; runtime limitations apply | `8da97f99f909475ff2fb1654e2ae75185923c4b98511daf942d1ed69df717728` |
| `scripts/guest_discovery_demo.cs` | 83 | E: 1–83; no separate finding; runtime limitations apply | `917366f80f8e5b650fba733c03b419b41aa03e71bc0c20e796bf1c9a2c8e9fcb` |
| `scripts/listenbrainz_smoke.cs` | 39 | E: 1–39; no separate finding; runtime limitations apply | `549e9bdaf4816a6584f71eded68b2c736482b62d9ebd0e9f9c39cd7194470435` |
| `scripts/phase4_pilot.py` | 431 | E: 1–431; no separate finding; runtime limitations apply | `1ba220dd69a27c6fd601904a486ec5695c563063ec4578f237cd2eb55f30a857` |
| `scripts/phase4_replay.cs` | 38 | E: 1–38; no separate finding; runtime limitations apply | `41e0ce957c051c1b8a94597e187a2e4d19ccd0e2b75805b1fed7eb1f4b983496` |
| `scripts/phase4_weights.json` | 9 | E: 1–9; no separate finding; runtime limitations apply | `85c3f261815d689a2f212913e39510c9cabc93326d81e383e27cbf272e864a47` |
| `scripts/phase7_cleanup_local_email.cs` | 38 | E: 1–38; no separate finding; runtime limitations apply | `fe42cd8a2e147511ec3fb7f976a7fa9ee486dc371777ddef6d1642b82fd0601a` |
| `scripts/phase7_local_email.cs` | 74 | E: 1–74; no separate finding; runtime limitations apply | `d853daaadd6a80e163e26b1cb9d0786804a63aa6b55158071fe8e7556346bec5` |
| `scripts/phase8_repair_registration_slot.cs` | 57 | E: 1–57; no separate finding; runtime limitations apply | `8be76f468822f4383fe995c4055d46d9ee63e6da63208e779e50433d75bd35d1` |
| `scripts/phase8_schedule_fixture.cs` | 50 | E: 1–50; no separate finding; runtime limitations apply | `1510abdb46b3b18ad72549b986e11264433e2e9457437b95d2347de4906a8db1` |
| `scripts/phase9_init_local.py` | 34 | E: 1–34; no separate finding; runtime limitations apply | `13ce898fb4ca76dea138e8ad9bd9d1297d3f3c7beec93a1b68f374dc72ee2e52` |
| `scripts/record_lastfm.py` | 497 | E: 1–497; no separate finding; runtime limitations apply | `37892936a8dc3bb9f9d481c2f3e8af2647a5462ce0908c9b897fc83872a0d5c0` |
| `scripts/run-schemathesis.sh` | 7 | E: 1–7; no separate finding; runtime limitations apply | `7ffcc0c442e149d37f33bee58dc26e951a21a2491ad19616e98941b2752387df` |
| `scripts/run-zap-scan.sh` | 5 | E: 1–5; no separate finding; runtime limitations apply | `7d01b49aef275a70b238aef8ff02f936875aafc7392c0c5983e503e4245475d3` |
| `scripts/run_live.sh` | 16 | E: 1–16; no separate finding; runtime limitations apply | `e34376f0fdfde106c44adac2191a6ddb9b1e84ff66e6562fa6a6e0d5c4099ead` |
| `scripts/run_manual_qa.sh` | 47 | E: 1–47; no separate finding; runtime limitations apply | `7c7e3d5523d4f606c966988ca14ad02e67a320fa073324384a0de18607672efe` |
| `scripts/run_phase7_local_qa.sh` | 98 | E: 1–98; no separate finding; runtime limitations apply | `c30ec271f61cbad595906ffdc193c9533d6b4c1dfb17d6b071bf489d298c1582` |
| `scripts/setup_local_live.cs` | 126 | E: 1–126; no separate finding; runtime limitations apply | `53df51e1df5660b6a82425d462b564559d26ee180516dcce1fc86fc363aa6baa` |
| `scripts/test_phase2_recorder.py` | 376 | E: 1–376; no separate finding; runtime limitations apply | `eed50a8f07cd326af8cda08f0e22c12903f1d0b1f1873a0f3a711ef7ae8f2f31` |
| `scripts/test_phase4_pilot.py` | 243 | E: 1–243; no separate finding; runtime limitations apply | `edb616db67f02432affabab3930ffaf082b7be0569e24757a6c35a9bc0971198` |
| `scripts/validate_manual_qa_connection.cs` | 55 | E: 1–55; no separate finding; runtime limitations apply | `affc0c757e80af93faf49c8d7ec2370a699b05d6de63561ab55747c5430e511a` |
| `src/Application/Application.csproj` | 19 | R: 1–19; no separate finding; runtime limitations apply | `cb67ed348b70e895da62fa3590305022dd39d73b92b754f3723e03dc3fa04511` |
| `src/Application/Common/Behaviors/ValidationBehavior.cs` | 47 | R: 1–47; no separate finding; runtime limitations apply | `9f164865e55d78b0c0722a46b1fc376401312ebc375d3aa54ed036a4cfa331ce` |
| `src/Application/Common/Exceptions/NotFoundException.cs` | 16 | R: 1–16; no separate finding; runtime limitations apply | `04318a0c8f8cefef81b4dfb1bad7f20725b3814f799fa1a5976580fc1c7e5e7f` |
| `src/Application/Common/Exceptions/ValidationException.cs` | 36 | R: 1–36; no separate finding; runtime limitations apply | `e3b0d319d9c4b4e608fa40f0dc4909f79faaea4882ce4cc132110071eb10330a` |
| `src/Application/Common/Interfaces/Catalog/ICatalogMaintenance.cs` | 7 | R: 1–7; no separate finding; runtime limitations apply | `38da8ffffb54a729a5fc629405c931e3ed76e989649d096139fcf3b073770ee9` |
| `src/Application/Common/Interfaces/Catalog/ICatalogStore.cs` | 63 | R: 1–63; no separate finding; runtime limitations apply | `f85de283c6dc9b6928306c1b4a47fec31cc5d1b792e5a7af15596eb4e37b8197` |
| `src/Application/Common/Interfaces/Discovery/ICoordinatedGenerationStorage.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `7238ce59223992dde7918a0dad868bdb82795e081a0d346a552322431b350702` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryBatchStore.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `fc01ab238f1d82797541830a857ae69594bc0824594820ca2c20806abeccc8d0` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryJobStore.cs` | 21 | R: 1–21; no separate finding; runtime limitations apply | `d014e8e89fad25c8be37cc795e7f0c85c084f38ada352ca8d96f18582bde3b53` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryProfileReader.cs` | 8 | R: 1–8; no separate finding; runtime limitations apply | `5ab7a8530e9105061686cff47163f231c276e231f34296053ef2cd3f9316c770` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryReader.cs` | 7 | R: 1–7; no separate finding; runtime limitations apply | `0d928f29f1800508644c5d37c1647e6bd9cf260d0f37acb9eb0860f86f380a68` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryTasteStore.cs` | 8 | R: 1–8; no separate finding; runtime limitations apply | `8f2e809d211f135ebf62287a515e496c14cb3bd5815b1c5e277aa188da4b4541` |
| `src/Application/Common/Interfaces/Discovery/IGuestProfileStore.cs` | 25 | R: 1–25; no separate finding; runtime limitations apply | `33f11464b0062741155840fc7bf2430ae353290bd727fef028ef3342c906ea62` |
| `src/Application/Common/Interfaces/Discovery/IManagedStorageIntegrity.cs` | 3 | R: 1–3; no separate finding; runtime limitations apply | `79016258a4a58cc1dec1ebbe450ada35e2dae85e931ab1e28c62c01467323e76` |
| `src/Application/Common/Interfaces/Discovery/IPublicEvidenceStore.cs` | 18 | R: 1–18; no separate finding; runtime limitations apply | `49545e08335bd33924be571b70d9e7703bb21e8587a4dabffd0c84ce698cdbd4` |
| `src/Application/Common/Interfaces/Discovery/IRecentRecommendations.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `30262e35d2903f6c74fd38bec334e896456331f41ff53218171e11064a8a4ec9` |
| `src/Application/Common/Interfaces/IAccountProfileReader.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `fa7f0a8bd202898634c7c28c331bb025b794b0cdd80509f9d34487c184177cfa` |
| `src/Application/Common/Interfaces/ILocalDigestCaptureStore.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `46d3a149d85145e19d1faf8d36d093c603ee5291fc3b4ba2eacf660f94910306` |
| `src/Application/Common/Interfaces/ILocalEmail.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `2bb61653051743d0fb22f0cb5d6c3bc1a7b84b2e858cda1ecc925dc679dc185f` |
| `src/Application/Common/Interfaces/ILocalEmailCapture.cs` | 37 | R: 1–37; no separate finding; runtime limitations apply | `0f9a1a491cbd1c62be6f9af6799b8eea912ce290640689fd2a7e289fb0cbe075` |
| `src/Application/Common/Interfaces/IUnitOfWork.cs` | 10 | R: 1–10; no separate finding; runtime limitations apply | `05dde484e56807a50deb867dfca63c304380c5e254300ebf63f4fecf13132608` |
| `src/Application/Common/Interfaces/IWeeklyScheduleStore.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `b75a4fb864535b151c6159bad5775d9b8eedc36286be64dba164b22995b26911` |
| `src/Application/Common/Interfaces/Recommendation/ICandidateHydrator.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `785bd7047fd6f10ef99c67e7344d0ecde2d14a4017138f0325baed131ed3c2ca` |
| `src/Application/Common/Interfaces/Recommendation/IDigestGenerationStore.cs` | 56 | R: 1–56; no separate finding; runtime limitations apply | `aa257bc19fc7dd13a44293479fcf000e384fbaadd248c834b2d988a4f00b399f` |
| `src/Application/Common/Interfaces/Recommendation/IDiscoveryPreparation.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `4ac239b9a4759e3931d11035b6a5a511371979bac1e8393cb66cca1e588fb313` |
| `src/Application/Common/Interfaces/Recommendation/IRecommendationProviderResolver.cs` | 10 | R: 1–10; no separate finding; runtime limitations apply | `d01413de66e466a9eaba8eeba7e9b3d77269b8a27e123173bedf7e3717245d8a` |
| `src/Application/Common/Interfaces/Recommendation/IRecommendationSource.cs` | 39 | R: 1–39; no separate finding; runtime limitations apply | `6f55ad9452746707dc728cbe4249cb4d3e9364824a202a7393956b8e7342bdd3` |
| `src/Application/Common/Interfaces/Recommendation/IRecordingRecommendationSource.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `78f5f65960ed40cde884ac84ea7868d5112ce94e9e59c19bd40131b292474be9` |
| `src/Application/Common/Interfaces/Repositories/IAsyncRepository.cs` | 39 | R: 1–39; no separate finding; runtime limitations apply | `a3e2ece916910895c4c70c0bea06b769ff9dd7120ae0654c465cfc36deaa54c4` |
| `src/Application/Common/Interfaces/Repositories/IListenerProfileRepository.cs` | 7 | R: 1–7; no separate finding; runtime limitations apply | `18a183032be7b41e12cdab00a713a6e6eeaaf4e66e021eb99606418cf85f9f74` |
| `src/Application/Common/Interfaces/Repositories/ITasteSignalRepository.cs` | 12 | R: 1–12; no separate finding; runtime limitations apply | `adfa6bda64b09be2b2b8e35c6987c213d3067669efe240f3a61faa3d0b7ef6a2` |
| `src/Application/Common/Interfaces/Repositories/ITrackRepository.cs` | 11 | R: 1–11; no separate finding; runtime limitations apply | `780ea3e89f81704c45ab5599793c7687ceff0ddb36557c2d66b963e92b158adf` |
| `src/Application/Common/Interfaces/Repositories/IUserRepository.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `cfdd3749fb54f0bba3d9f160a301c0fb856a9878c73c5438257f4b72dfa44df5` |
| `src/Application/Common/Interfaces/Repositories/IWeeklyDigestRepository.cs` | 17 | R: 1–17; no separate finding; runtime limitations apply | `6e1c7a7f7dd0fea141df25b251135bafb8c753e9447c1924aada27c1ed10ed4a` |
| `src/Application/Common/Mappings/MappingExtensions.cs` | 95 | R: 1–95; no separate finding; runtime limitations apply | `7dea546ab7cbbac4a5ccbe753605bb1086babf19b272f9c1b6ac8bd2922825b2` |
| `src/Application/Common/Models/PagedResult.cs` | 16 | R: 1–16; no separate finding; runtime limitations apply | `4f34e06673d15f9dea5ed16b195a5288d90d688bf595bae90adf47dc60893dfd` |
| `src/Application/Common/Models/Recommendation/GenerationConfiguration.cs` | 60 | R: 1–60; no separate finding; runtime limitations apply | `c6520277286fdce60d98f3555295838f85132f6799e797ebf91ae2b2751d3456` |
| `src/Application/Common/Models/Recommendation/GenerationEvidenceMapping.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `2d40af02922f9f27db678721889b459c2ba8090ccfae569615541d64725267dc` |
| `src/Application/Common/Models/Recommendation/IngestionEvidence.cs` | 76 | R: 1–76; no separate finding; runtime limitations apply | `b1ec882cf8b53be48dca2821ba065cac8172cd06d65b0ceceec7e05618c9510f` |
| `src/Application/Common/Models/Recommendation/RawCandidateTrack.cs` | 41 | R: 1–41; no separate finding; runtime limitations apply | `03f26905bdf5c51a82317961454ae0e59791a46106083287c07e571e8a16624f` |
| `src/Application/DTOs/Digests/ScoreBreakdownDto.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `3dcef0d28caac561e54faca692283e4777fc1d89ab094dda7dcabd36fcfd95f3` |
| `src/Application/DTOs/Digests/TrackDto.cs` | 16 | R: 1–16; no separate finding; runtime limitations apply | `759f297427e9fac7c8b305824594ae8b52565fb933e870d3411c2790bd71e3c4` |
| `src/Application/DTOs/Digests/WeeklyDigestDto.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `d309ae79856245babfc788f6de7fc8eee7c9a762fbd0ea8196c1d44fa5faf8e6` |
| `src/Application/DTOs/Digests/WeeklyRecommendationDto.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `7f9f07e1d87bc791aa94b5148523f1a308099a8b7b745531ac844ca66da30f81` |
| `src/Application/DTOs/Export/AccountExportDto.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `b749331f31aeba4b97e392ab5e8adbd091bce6de4b353db83de75f9f396c9760` |
| `src/Application/DTOs/Export/DiscoveryExportDto.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `45585dd65f77bdf88c686ddf7398f49e34ca343fbd4f9325a5da9a7541a22648` |
| `src/Application/DTOs/Export/UserDataExportDto.cs` | 38 | R: 1–38; no separate finding; runtime limitations apply | `7362cc27a811d511317cdc6ed302a5c2ad4a78e766cb48a009d46990736c8935` |
| `src/Application/DTOs/Subscribers/SubscriberDto.cs` | 23 | R: 1–23; no separate finding; runtime limitations apply | `cefe48889fee3b49cb864c47cd5583d49f43196472ba1735a4a7b4e3c8e010b9` |
| `src/Application/DTOs/Taste/SeedItemDto.cs` | 17 | R: 1–17; no separate finding; runtime limitations apply | `f0e9821cf3024b31eaecb8e762032050f943a3133e66480d3f92c0c34e4c6361` |
| `src/Application/DTOs/Taste/TasteSignalDto.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `f460413ad44e1d4d7469c748022dcbbea8dfb50f40467b192e24cfc2212752cb` |
| `src/Application/DependencyInjection.cs` | 39 | R: 1–39; no separate finding; runtime limitations apply | `502c0b5aa8be891afab767709b0cfdfd44e77552580d2e31beb391acb072a3b7` |
| `src/Application/Features/Catalog/CatalogLookupJobs.cs` | 53 | R: 1–53; P11 | `2badada147c3b7d269dbf2066e2a3eff58597573ba20c5f24dcdf5864f6852d4` |
| `src/Application/Features/Catalog/ImportCatalog.cs` | 75 | R: 1–75; no separate finding; runtime limitations apply | `87cc38dfb5d7cb45ed312f986cf23fec05e99146ecdd1b1586f50f53db0e8517` |
| `src/Application/Features/Catalog/LookupCatalog.cs` | 31 | R: 1–31; no separate finding; runtime limitations apply | `22270c7683968763fe4887920cebd14f506e0e8200b7b9500d01728c89d9bfae` |
| `src/Application/Features/Catalog/RunCatalogLookup.cs` | 70 | R: 1–70; no separate finding; runtime limitations apply | `edd72df6f18fff405eccb112d55b52a7dca1c298757a55f4b5680f448e12c617` |
| `src/Application/Features/Catalog/SearchCatalog.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `95cc17716c07a25dc8a4e5c719dea6e69fb79e9401b69e394a0a3eba14cc6d81` |
| `src/Application/Features/Digests/Commands/CaptureLocalDigest/CaptureLocalDigestCommand.cs` | 58 | R: 1–58; no separate finding; runtime limitations apply | `6c4141a3a56158260bb29fca793e41bd2f095dfcd1fd8bc925e730a56bd74b02` |
| `src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommand.cs` | 10 | R: 1–10; no separate finding; runtime limitations apply | `46ad5926c15fd15f67d6087afbcbc298dc8335204704712170d8639dac521c07` |
| `src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs` | 190 | R: 1–190; P08, P09, P10 | `ade5864506f5b637a73a34890de6f5a81ca32f0b01f9392a590ce08e3eb1d3df` |
| `src/Application/Features/Digests/Commands/RecordFeedback/RecordRecommendationFeedbackCommand.cs` | 14 | R: 1–14; no separate finding; runtime limitations apply | `fc39f231944dc2f7c1429c6f1b9c91cfb2badccab140539b9012eadb97b6d12c` |
| `src/Application/Features/Digests/Commands/RecordFeedback/RecordRecommendationFeedbackCommandHandler.cs` | 137 | R: 1–137; no separate finding; runtime limitations apply | `f54e8d0a225d5625b7c948a4c3c833d75a1a65fd42bcb9cc6cb72eefbd386b77` |
| `src/Application/Features/Digests/Commands/RecordFeedback/RecordRecommendationFeedbackCommandValidator.cs` | 34 | R: 1–34; no separate finding; runtime limitations apply | `08c65d64b37fc44bd84aace4e2877111748e4d400b737ddd8c5e6187b4856682` |
| `src/Application/Features/Digests/Commands/RunScheduledTick/RunScheduledTickCommand.cs` | 81 | R: 1–81; no separate finding; runtime limitations apply | `db6700eb9ae9ad712c3d59c8b008875c323514cd7b45a550fbb50822a85eada0` |
| `src/Application/Features/Digests/Queries/GetLatestDigest/GetLatestDigestQuery.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `1b791f82a64bc5583d76248135dec7188ee959b61f1d70d3ce2c5efbf914bf0d` |
| `src/Application/Features/Digests/Queries/GetLatestDigest/GetLatestDigestQueryHandler.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `c141df4ac0af1ff17014520a0207bbd1d6e984494987511e17bfc95d900f6fc9` |
| `src/Application/Features/Discovery/CleanupPublicDiscovery.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `fdf06d94277163891c78847985c65288d9f001761fe06cbe9bdec9e1d43e4596` |
| `src/Application/Features/Discovery/DiscoveryRuntimeOptions.cs` | 21 | R: 1–21; no separate finding; runtime limitations apply | `1dd1baedc68b9063bf23c7b1aa41327ec5a2024732aae388f1e91dc8b2199d38` |
| `src/Application/Features/Discovery/DiscoveryViews.cs` | 22 | R: 1–22; no separate finding; runtime limitations apply | `2e8988f1abdeaea3f87f157843af974e6bd6966b1d673cd488110028d589819d` |
| `src/Application/Features/Discovery/GuestLifecycle.cs` | 52 | R: 1–52; no separate finding; runtime limitations apply | `890697060fb8c72c01d770c143073c6e645b47640520cdfa8cd404d67ac76243` |
| `src/Application/Features/Discovery/MaintainDiscoveryStorage.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `57294bca9e922143d1440ccc4f82581d10ee090b452de9abf1a1926f1dfb8e95` |
| `src/Application/Features/Discovery/MaterializeDiscoveryTaste.cs` | 146 | R: 1–146; no separate finding; runtime limitations apply | `72eaf911c57a795c06b5bb8c54041f4682d980a96d9d0e3cb8ccfad47d15c15b` |
| `src/Application/Features/Discovery/PrepareDiscoveryBatch.cs` | 150 | R: 1–150; P08, P10 | `22b033be58de9794113937ddbc970162d745c104856e9c89fb3176765d8117b9` |
| `src/Application/Features/Discovery/RecordDiscoveryFeedback.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `3b65dff805c9765e85b08c3f15d5ed47a2f40d119c4b15ab41cbd524ab77d3e6` |
| `src/Application/Features/Discovery/RunDiscoveryJob.cs` | 30 | R: 1–30; no separate finding; runtime limitations apply | `835c75358588be49d170a9937b037051f74731d2630b3f785022f289bde4a783` |
| `src/Application/Features/Discovery/SaveTasteOnboarding.cs` | 33 | R: 1–33; no separate finding; runtime limitations apply | `c81a3d856e45d53ab90ef7b9620e798a53e159636224fcf853d52b46b6f89ae0` |
| `src/Application/Features/Discovery/SelectDiscoveryTaste.cs` | 79 | R: 1–79; P11 | `68ffb28ab0a76a569fdd27ed3897d7c307c67f8c736f84acda27b5f78ba9c4fc` |
| `src/Application/Features/Discovery/SubmitDiscovery.cs` | 65 | R: 1–65; no separate finding; runtime limitations apply | `3632358ee016c8527dd20e5119e800d50e7bf8230d11ddd27922a8bc98d72b75` |
| `src/Application/Features/Discovery/TasteVocabulary.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `5a2557847eea918bbae55e9086a02e0d2394222618de88a924a581c45b7a5db3` |
| `src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQuery.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `b982f6a8a42f2c0cf376be223bf9af26a72329a8dc272c026574610e26e611c3` |
| `src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQueryHandler.cs` | 96 | R: 1–96; P13 | `a574e7a478f478e0a21c2329dac24d96a0f72d6a0cb0f32045510b1c484c0120` |
| `src/Application/Features/Subscribers/Commands/DeleteUserAccount/DeleteUserAccountCommand.cs` | 8 | R: 1–8; no separate finding; runtime limitations apply | `ca64eaac95b53880585c1e6f8749a365794012e3f6a1fd8bb2e5a782764f5106` |
| `src/Application/Features/Subscribers/Commands/DeleteUserAccount/DeleteUserAccountCommandHandler.cs` | 34 | R: 1–34; no separate finding; runtime limitations apply | `21de73969a8cec32ce5f43d5266531bab98a802dbd14e1fb7ad91a8a0c92f216` |
| `src/Application/Features/Subscribers/Commands/RegisterSubscriber/RegisterSubscriberCommand.cs` | 17 | R: 1–17; no separate finding; runtime limitations apply | `3c3c07a278c8bea8a0ad4c71c38649de04391b80f03fffd4231796acdcfd3c63` |
| `src/Application/Features/Subscribers/Commands/RegisterSubscriber/RegisterSubscriberCommandHandler.cs` | 64 | R: 1–64; no separate finding; runtime limitations apply | `aabdac5872a9a31ed2f146674c313b3284a08cf437336aff992b54cd14c785fa` |
| `src/Application/Features/Subscribers/Commands/RegisterSubscriber/RegisterSubscriberCommandValidator.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `7850d090732a5067871b0067df8d2c9a6918e2069be21bd55c7cb09d85fb8349` |
| `src/Application/Features/Subscribers/Commands/UnsubscribeUser/UnsubscribeUserCommand.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `bb4cfc00e3bcf5baa25f9c0042cb6edd88bdfe334cab3ecce33e8eefe911650b` |
| `src/Application/Features/Subscribers/Queries/GetSubscriberProfile/GetSubscriberProfileQuery.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `d06aed9c359e1ca4118a9d85d72cd6a248c33e1750ebdd1b11b74c7450856c3e` |
| `src/Application/Features/Subscribers/Queries/GetSubscriberProfile/GetSubscriberProfileQueryHandler.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `4cb5ee73c6cb9222d52f1f20718c4fa962b2efc195c9c8aa4bef4683259e2c46` |
| `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommand.cs` | 12 | R: 1–12; no separate finding; runtime limitations apply | `b6606820f227c069519c25cc5a5890f82f6bdb855ddbda33d82b165e770976dd` |
| `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommandHandler.cs` | 75 | R: 1–75; no separate finding; runtime limitations apply | `76d455a3320a4a59d4db6e663e1f81adfba50325a64362cf5fda9caf6d52fe35` |
| `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommandValidator.cs` | 41 | R: 1–41; no separate finding; runtime limitations apply | `4432fb0b57065d327d65b5de5b98b1d8097408f7f62a7bb801f9e7c91ba97ddf` |
| `src/Application/Features/Taste/Queries/GetUserTasteSignals/GetUserTasteSignalsQuery.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `cf677a3321ad3076c47295fea527c93c6da1abd7f9a64beb8005e3cff3f9df3b` |
| `src/Application/Features/Taste/Queries/GetUserTasteSignals/GetUserTasteSignalsQueryHandler.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `57910076272b03f2626826a7392d520e566e901b81b099417e65f21a5e6d5984` |
| `src/Application/Services/IAuthService.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `54b8d6fd57ba863de2bc463e04b75622ed405d1e394e5f72313bdbf380bbb9f7` |
| `src/Application/Services/ICurrentUserService.cs` | 11 | R: 1–11; no separate finding; runtime limitations apply | `4739d61ad42d20978f1a5c2a1e61ef18488cae24cc6626bf147aea1d1bb84885` |
| `src/Application/Services/IJwtService.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `9fd69fccbff48d23accdb8cdca3d7eef83f423b40200c527a5b6ef79be53f76f` |
| `src/Application/Services/ValidatorInterceptor.cs` | 3 | R: 1–3; no separate finding; runtime limitations apply | `2c1cb22cd7556791b989d021eab40514312e573c5a0538aab064ca082bdcdaf0` |
| `src/Application/packages.lock.json` | 47 | R: 1–47; no separate finding; runtime limitations apply | `c9865699dc1c96c42abacf325fa2cd30f567b2385a956691589fc804c0c4046f` |
| `src/DataAccess/Core/AsyncRepository.cs` | 146 | R: 1–146; no separate finding; runtime limitations apply | `17913c9dafd44b4b589a28847c9bace86abe4f32ce4a2368c74f2e307385367a` |
| `src/DataAccess/Core/UnitOfWork.cs` | 49 | R: 1–49; no separate finding; runtime limitations apply | `15acd21d538002f8f22109492d0909cb5f543a14085dc67fe789513dedbb3dd2` |
| `src/DataAccess/DapperPagedRepositoryBase.cs` | 90 | R: 1–90; no separate finding; runtime limitations apply | `904c0bc165f03556c5575c62a09894b023c76f45b97d4e6e79306244b67b0e80` |
| `src/DataAccess/DataAccess.csproj` | 31 | R: 1–31; no separate finding; runtime limitations apply | `b605293a510f1385c9132a8be8df9adf3b926e22af2b40fae5590c03e7de5ce4` |
| `src/DataAccess/DataContexts/DataContext.cs` | 146 | R: 1–146; no separate finding; runtime limitations apply | `9ee3cfa3defce67160860cfe0ca4542622fc44429e9d6a22965e14ee3a108d71` |
| `src/DataAccess/DependencyInjection.cs` | 95 | R: 1–95; no separate finding; runtime limitations apply | `12cf00e1b18cb8ee388cac48477b5f4b98a24432a7a3b3e90d74415887a9493c` |
| `src/DataAccess/IdentityEntities/ApplicationUser.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `e92c48d9ad176b3507a0ce9feb22b900e5abe76737028cb82508615830b5f8a1` |
| `src/DataAccess/Persistence/AccountEmailLease.cs` | 41 | R: 1–41; no separate finding; runtime limitations apply | `4abe3bbf33a048932f2d49811a1764c72ed46b538219ec6a8a66120e896630cd` |
| `src/DataAccess/Persistence/Accounting/ManagedStorageIntegrity.cs` | 60 | R: 1–60; no separate finding; runtime limitations apply | `9a77496a52e3ed0d30ff475886753b734b512757832c6ac18a1b90c1a4271c76` |
| `src/DataAccess/Persistence/Accounting/ManagedStorageLedger.cs` | 11 | R: 1–11; no separate finding; runtime limitations apply | `b81ca20f69d28b5707ec99a4e513fa24f60c811a559a102156d470012bddf299` |
| `src/DataAccess/Persistence/Accounting/PublicEvidenceCacheEntry.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `7843c194c2ebddf629b3b1cd925bb51d97af498f98abf259d950e73f16531498` |
| `src/DataAccess/Persistence/Accounting/RecommendedIdentity.cs` | 8 | R: 1–8; no separate finding; runtime limitations apply | `c3812df09bea373a419b3f488efd355c93c4e51ed0252f7f9feb7fc2340375b1` |
| `src/DataAccess/Persistence/AppDbContext.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `c76454d29497d4b98cb1f0641a966ffe88e9b0b2eb7f2207a7168d3d4495fc47` |
| `src/DataAccess/Persistence/Configurations/AlbumConfiguration.cs` | 36 | R: 1–36; no separate finding; runtime limitations apply | `cbaa3a4207968674e84de36dc12464010bdce88a7b27f96a456df26084f02a18` |
| `src/DataAccess/Persistence/Configurations/ArtistConfiguration.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `fecc51ff867a4f7b333b737259506286e8ce4e84e3dde2491d39cb1dbb6caf5d` |
| `src/DataAccess/Persistence/Configurations/CatalogSearchConfiguration.cs` | 75 | R: 1–75; no separate finding; runtime limitations apply | `48c5e123373d3067c44438e02cadde235d7e9be323e9cdde7ff42aeae1ea1b30` |
| `src/DataAccess/Persistence/Configurations/DiscoveryConfiguration.cs` | 80 | R: 1–80; no separate finding; runtime limitations apply | `72fc55fe41165566cfe775f369955c98595446a0e1ba976d0ecc723e8c3a594b` |
| `src/DataAccess/Persistence/Configurations/ManagedStorageLedgerConfiguration.cs` | 18 | R: 1–18; no separate finding; runtime limitations apply | `a0ded9ceb41d564916007069c9d7772318fcf070fb96f35ce75744c419d5a8e1` |
| `src/DataAccess/Persistence/Configurations/PublicEvidenceCacheConfiguration.cs` | 25 | R: 1–25; no separate finding; runtime limitations apply | `a62d74c1fc9ddad4e489ceb68a8c9c1cef13bb3e5ba79daf6a53b40c59abcaae` |
| `src/DataAccess/Persistence/Configurations/RecentRecommendationConfiguration.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `12e2fecf56958447c0b1e6a3027c337aef64b836cc39417f0ec5ce874ee68e12` |
| `src/DataAccess/Persistence/Configurations/RecommendedIdentityConfiguration.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `b552574f8eaf7269f7642f59893957d631fa3d7a05c70afae7fab4937a4ae348` |
| `src/DataAccess/Persistence/Configurations/TasteSignalConfiguration.cs` | 61 | R: 1–61; no separate finding; runtime limitations apply | `fd7aad3d470c54d0d1a1f55c669a45e4a275f4ce3f01e5a341414d6e929beac0` |
| `src/DataAccess/Persistence/Configurations/TrackConfiguration.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `6796fdc0b350de4bd3d213b01b3fa7a7dce03c96de8ee6338498d6c6e9aa9040` |
| `src/DataAccess/Persistence/Configurations/UserConfiguration.cs` | 71 | R: 1–71; no separate finding; runtime limitations apply | `f3b60d4cb154c8fd59e00c985c13c5c7e0cb764eaaa8b36a361b06b6f1622c0e` |
| `src/DataAccess/Persistence/Configurations/UserMusicConnectionConfiguration.cs` | 48 | R: 1–48; no separate finding; runtime limitations apply | `b49ee96dafd7138d709482b979ee4c9f1d6efeaef5b17593c6052f154aaafafb` |
| `src/DataAccess/Persistence/Configurations/WeeklyDigestConfiguration.cs` | 70 | R: 1–70; no separate finding; runtime limitations apply | `9151b3e81e8d66929a0d75cb9e9e801106469fb88ecef87723f1bacfb1eb0788` |
| `src/DataAccess/Persistence/Configurations/WeeklyRecommendationConfiguration.cs` | 72 | R: 1–72; no separate finding; runtime limitations apply | `2d85b4e9b5668198da28681516df930bbea3daa34de62a18e5402465c88c223d` |
| `src/DataAccess/Persistence/LocalUnsubscribeProtection.cs` | 39 | R: 1–39; no separate finding; runtime limitations apply | `1b1d5b316bd6fb3602da55c952149bac131ed66caa491830c908245e51bb5e03` |
| `src/DataAccess/Persistence/Migrations/20260930194436_InitialCreate.Designer.cs` | 472 | G: 1–472; no separate finding; runtime limitations apply | `e84e836387b4f229d796f1ceb29fe077e1eb5c101faf4b4c396b7f2a5eefef98` |
| `src/DataAccess/Persistence/Migrations/20260930194436_InitialCreate.cs` | 329 | R: 1–329; no separate finding; runtime limitations apply | `e551a4a487e8d6006a0eff1b8c2bd968ec3e5a67441d5a170df8d80cb4135025` |
| `src/DataAccess/Persistence/Migrations/20261002203611_AddRatingToWeeklyRecommendation.Designer.cs` | 822 | G: 1–822; no separate finding; runtime limitations apply | `20974f05141c5f9d8e1923a32d39fdd7a2ea9299b99402d4fbf208172de70536` |
| `src/DataAccess/Persistence/Migrations/20261002203611_AddRatingToWeeklyRecommendation.cs` | 528 | R: 1–528; no separate finding; runtime limitations apply | `290f7de425741071c96bdf6da4003872051ad844338542772927e64270952694` |
| `src/DataAccess/Persistence/Migrations/20261010205812_AddEmailUnsubscribedAtUtc.Designer.cs` | 825 | G: 1–825; no separate finding; runtime limitations apply | `9bbcb4636c4f4f0ac8501fe3ce7795533c5beb4b1363575f050fab74389f1c2a` |
| `src/DataAccess/Persistence/Migrations/20261010205812_AddEmailUnsubscribedAtUtc.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `e3a43ab7d1b3a802bcf0a5cd4a0b0d940c3f7436428dc73e7b042ddd7f237b07` |
| `src/DataAccess/Persistence/Migrations/20261016092843_AddListenerProfilesAndDiscoveryBatches.Designer.cs` | 1044 | G: 1–1044; no separate finding; runtime limitations apply | `094ec42ecea8fd9be9159bde30cf67c6c0f52292a92b4a3a5eebaff205826ac5` |
| `src/DataAccess/Persistence/Migrations/20261016092843_AddListenerProfilesAndDiscoveryBatches.cs` | 212 | R: 1–212; no separate finding; runtime limitations apply | `14165465b9b38257265e75d44cc435258334ca87c8bea97fd0ddd63f6913091c` |
| `src/DataAccess/Persistence/Migrations/20261016100239_GeneralizeDiscoveryOwnership.Designer.cs` | 1083 | G: 1–1083; no separate finding; runtime limitations apply | `ab2bcc1715704d24eda4f642fd97d238672ec0769b1b3151c843080907301d03` |
| `src/DataAccess/Persistence/Migrations/20261016100239_GeneralizeDiscoveryOwnership.cs` | 260 | R: 1–260; no separate finding; runtime limitations apply | `7541633a296d3f4ec5525f17b686561ea013900022d36695059a75f9dc59bc93` |
| `src/DataAccess/Persistence/Migrations/20261016101423_AddGuestSessions.Designer.cs` | 1113 | G: 1–1113; no separate finding; runtime limitations apply | `1b1ae1bb83e3ce8186a1b31b582e24ff7fcb859472721fb57b22c2910d6acf66` |
| `src/DataAccess/Persistence/Migrations/20261016101423_AddGuestSessions.cs` | 60 | R: 1–60; no separate finding; runtime limitations apply | `3034c1ce5adece479011d4440d45bf6ca24cb00ad2842fa8c14e37391ac8bc95` |
| `src/DataAccess/Persistence/Migrations/20261016102144_RequireExplicitWeeklyEmailOptIn.Designer.cs` | 1118 | G: 1–1118; no separate finding; runtime limitations apply | `66e6eac673e9ab07bb48f2c8361b9ff8e269cdab410cc0f47850c8f3fd1eab91` |
| `src/DataAccess/Persistence/Migrations/20261016102144_RequireExplicitWeeklyEmailOptIn.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `3d031cd00512baaf99c55eff677c882b5c9edbc2293af0d37e7f9556c4056358` |
| `src/DataAccess/Persistence/Migrations/20261016130431_AddLocalDiscoveryCatalog.Designer.cs` | 1397 | G: 1–1397; no separate finding; runtime limitations apply | `f02fa312dbdd255a34aacd06b7efdb1d8ea625987d0c8272c589ab413b73168f` |
| `src/DataAccess/Persistence/Migrations/20261016130431_AddLocalDiscoveryCatalog.cs` | 246 | R: 1–246; no separate finding; runtime limitations apply | `9b31fc489cb256536608ad7ce6cbd04192a7af1881ab6cae3629f53cc4afc991` |
| `src/DataAccess/Persistence/Migrations/20261016133449_AddCanonicalTasteSelections.Designer.cs` | 1406 | G: 1–1406; no separate finding; runtime limitations apply | `ca25e9457d4f8de8c9d804d47a979687d2d3f6889e473acd56aea6bfb19ebb2c` |
| `src/DataAccess/Persistence/Migrations/20261016133449_AddCanonicalTasteSelections.cs` | 64 | R: 1–64; no separate finding; runtime limitations apply | `7628cf68b8924233c7e2939f4c6114bc698d8a98fe5c5fda637a60ef045f2963` |
| `src/DataAccess/Persistence/Migrations/20261017124050_AddManagedStorageLedger.Designer.cs` | 1431 | G: 1–1431; no separate finding; runtime limitations apply | `03d0964af7d0acd49a829218b612aa3ab40e63d823bde9053392a745fc507f6e` |
| `src/DataAccess/Persistence/Migrations/20261017124050_AddManagedStorageLedger.cs` | 88 | R: 1–88; no separate finding; runtime limitations apply | `7ccfeb02bc1e687ddd241bff77cc4ed47ec3d17d7e8e5dbdddc818dd96279035` |
| `src/DataAccess/Persistence/Migrations/20261017130938_AddSharedDiscoveryEvidenceAndHistory.Designer.cs` | 1527 | G: 1–1527; no separate finding; runtime limitations apply | `b0a4ba4e847a75443d3e94395f8c9d577fee64874af034ff58c290dc72f3203e` |
| `src/DataAccess/Persistence/Migrations/20261017130938_AddSharedDiscoveryEvidenceAndHistory.cs` | 204 | R: 1–204; no separate finding; runtime limitations apply | `3790bf5f7765a90f99d27317a644153e85d9b0562b05e498e64a5b12bbe70eea` |
| `src/DataAccess/Persistence/Migrations/20261018195528_IndexCatalogExpiryMaintenance.Designer.cs` | 1529 | G: 1–1529; no separate finding; runtime limitations apply | `0dd7e2599e3a3b997131e03e5b5bfe2f80648283433e14c4e9b1da84f3e0b995` |
| `src/DataAccess/Persistence/Migrations/20261018195528_IndexCatalogExpiryMaintenance.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `5ae9284ba037893b034fc18410288eb3f6374999c633a0063b8ef4e5aeafe768` |
| `src/DataAccess/Persistence/Migrations/20261019083146_AddOptionalTasteOnboarding.Designer.cs` | 1575 | G: 1–1575; no separate finding; runtime limitations apply | `655559c21223a9b64174e396c74cfa436d29ed856b4032f4ca44409dac31bad8` |
| `src/DataAccess/Persistence/Migrations/20261019083146_AddOptionalTasteOnboarding.cs` | 190 | R: 1–190; no separate finding; runtime limitations apply | `01f931455013547741cd310a71e7bef1ef8cd25bff159f374804422e0a1758af` |
| `src/DataAccess/Persistence/Migrations/20261019084129_StoreRecordingReleaseHints.Designer.cs` | 1581 | G: 1–1581; no separate finding; runtime limitations apply | `b77a6999145354d00ae68e13832fa51855782c93a93c1afd293294e21a71d5da` |
| `src/DataAccess/Persistence/Migrations/20261019084129_StoreRecordingReleaseHints.cs` | 110 | R: 1–110; no separate finding; runtime limitations apply | `80e54f079497a259d7a1dbeef81da83d9db71374e026cb804aba23ef598f8d21` |
| `src/DataAccess/Persistence/Migrations/AppDbContextModelSnapshot.cs` | 1578 | G: 1–1578; no separate finding; runtime limitations apply | `4cb4d00c8921fdd02767fb1e9ba6c1ada4ff0387813ae4de984649c4c675a39f` |
| `src/DataAccess/Persistence/Repositories/AccountProfileReader.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `494346d48370d0c8e0e46a20d894f20e0fd8fd1a378907645d1b7e418ed67365` |
| `src/DataAccess/Persistence/Repositories/CatalogImportStore.cs` | 131 | R: 1–131; no separate finding; runtime limitations apply | `79eb3284141fd5ca92679e99d95df92b61ba3a58e3809fbb21c73846358bb0cd` |
| `src/DataAccess/Persistence/Repositories/CatalogReader.cs` | 97 | R: 1–97; P11 | `5d44a276ee85c722258746162fae4f7964c1e899a631be9898f581ed0fe5a28c` |
| `src/DataAccess/Persistence/Repositories/DigestGenerationStore.cs` | 167 | R: 1–167; P09 | `0b7dc36ebc6bcd6232d0787dbe71664f192b99f06aa101871a5d316812f21afd` |
| `src/DataAccess/Persistence/Repositories/DiscoveryBatchStore.cs` | 140 | R: 1–140; no separate finding; runtime limitations apply | `3242303e06499446c6160415edba57ec0af56641e5819889e94ad3f643c0cb65` |
| `src/DataAccess/Persistence/Repositories/DiscoveryJobStore.cs` | 128 | R: 1–128; P07 | `a0936fb80175be7f6d205404ba787ecc55fdc9584aa0e864a4808961f873ea11` |
| `src/DataAccess/Persistence/Repositories/DiscoveryProfileReader.cs` | 61 | R: 1–61; P06 | `430aafe6009bf9ea89537a26b4b65c9fc3add57edbf3c9b3215c96e99dc3768b` |
| `src/DataAccess/Persistence/Repositories/DiscoveryReader.cs` | 50 | R: 1–50; no separate finding; runtime limitations apply | `9d8b42f55281b79ab5c9f538248e606bcb56dd5dce2d752f8226043f31d94fcb` |
| `src/DataAccess/Persistence/Repositories/DiscoveryTasteStore.cs` | 46 | R: 1–46; no separate finding; runtime limitations apply | `fc546f195abaa7441163a9232a31a6d0fc3aa8ae8448d80f53784e63d80e4c69` |
| `src/DataAccess/Persistence/Repositories/DiscoveryWriteCoordinator.cs` | 99 | R: 1–99; P01 | `07c4e41787418457e848d0e8a088a93da1567137fb719e5ab506060977c64d51` |
| `src/DataAccess/Persistence/Repositories/GuestProfileStore.cs` | 119 | R: 1–119; no separate finding; runtime limitations apply | `7f605ae39ad8cce68157c2844a21244ec6b9bf2e3677f30d7c916cdf6b1e4a54` |
| `src/DataAccess/Persistence/Repositories/ListenerProfileRepository.cs` | 25 | R: 1–25; no separate finding; runtime limitations apply | `c9c90ca9e89b0aa6d0ede3ff81589f163f78223ddb9e4823db44ffaf70915bac` |
| `src/DataAccess/Persistence/Repositories/LocalDigestCaptureStore.cs` | 82 | R: 1–82; no separate finding; runtime limitations apply | `ae8e3f16aeec226c0ac76b9792b905df015a05d3a4d61ac1b3e6f40a01fdcbf0` |
| `src/DataAccess/Persistence/Repositories/PublicEvidenceStore.cs` | 100 | R: 1–100; P12 | `d183ba393e7548a2517a78ca335e1ca8c766119949046b0d7dd101ae3cbafb82` |
| `src/DataAccess/Persistence/Repositories/RecentRecommendations.cs` | 24 | R: 1–24; no separate finding; runtime limitations apply | `a504a2a7925fabba04fdab4dbf5ea74c519476b1dc2d939212f452f8f0941afb` |
| `src/DataAccess/Persistence/Repositories/TasteSignalRepository.cs` | 59 | R: 1–59; no separate finding; runtime limitations apply | `13f9c330d4f2ad6cbf2eccc4ba4dda80af4aa9026d70efff2557299d7f4c529b` |
| `src/DataAccess/Persistence/Repositories/TrackRepository.cs` | 43 | R: 1–43; no separate finding; runtime limitations apply | `264556d0043c4e760f3b0bf052e4e25cea79076a9bf08b73773b6fb47acbd60e` |
| `src/DataAccess/Persistence/Repositories/UnsubscribeStore.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `01db95eb2cde835654a209988fffd81fcebb7614a3c9c737faecc7e61e058c2e` |
| `src/DataAccess/Persistence/Repositories/UserRepository.cs` | 61 | R: 1–61; no separate finding; runtime limitations apply | `33e486f8c5a615b151d8256bb40703310d3314c0d3cc23fd0b9515a75215836d` |
| `src/DataAccess/Persistence/Repositories/WeeklyDigestRepository.cs` | 90 | R: 1–90; no separate finding; runtime limitations apply | `978a2db87803797f85771ee7d25d4a42ebc0b83c781638c53ceb92a385e3fbda` |
| `src/DataAccess/Persistence/Repositories/WeeklyScheduleStore.cs` | 174 | R: 1–174; no separate finding; runtime limitations apply | `7995c2ffcf7395e2072ad3dcafb34c4bb3e5c0a3d2931d1cf31562f9c6d1444d` |
| `src/DataAccess/packages.lock.json` | 317 | L: 1–317; no separate finding; runtime limitations apply | `dff6812eba220e37066a2b54fad0953a3d4adebfbfc84d91ed2932126e62f3a3` |
| `src/Domain/Catalog/Album.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `bbd4b7290076502fb82d00b0215cd675ee5258dbdd5af187e3519cb7ace783f4` |
| `src/Domain/Catalog/Artist.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `2e6c37dcef566d8fba8f3bf4d95b7065233e7ec18cf1fe45e33ea929207eff99` |
| `src/Domain/Catalog/CatalogPublication.cs` | 65 | R: 1–65; no separate finding; runtime limitations apply | `cbcea0c47461a68fb087b397c60c9f56a7fe8f4b7421c47eccdc7c4b6757add9` |
| `src/Domain/Catalog/CatalogRecord.cs` | 63 | R: 1–63; no separate finding; runtime limitations apply | `fdb12d83646f12d2d42c85eaecb612a61a16a6668e19ec0308b11d28fed91f84` |
| `src/Domain/Catalog/CatalogSearch.cs` | 62 | R: 1–62; no separate finding; runtime limitations apply | `531c1d9f224e9b792933d8b38ad00c5a08c633fec028a20fa0082563afafde27` |
| `src/Domain/Catalog/Tag.cs` | 49 | R: 1–49; no separate finding; runtime limitations apply | `756e5bcafc0440ec13aa04c4c1a839d902a197efb8565e1dc819bee592fbbc50` |
| `src/Domain/Catalog/Track.cs` | 73 | R: 1–73; no separate finding; runtime limitations apply | `5dc0da114c534dcd35e49f3ffef7f087c71bd665db5eedf2c2f28727448b981a` |
| `src/Domain/Catalog/WeightedTag.cs` | 36 | R: 1–36; no separate finding; runtime limitations apply | `fd0528f73d2a10806ba1d0fd094bd391e9cc0ab49352de89d4465ededb792e41` |
| `src/Domain/Catalog/WeightedTagVector.cs` | 190 | R: 1–190; no separate finding; runtime limitations apply | `4ba386f3d6814d291e415065987ca22906e28f2ef86621e256b54c332796402f` |
| `src/Domain/Common/AuditableEntity.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `562bf867179f2354e2bb740879ed4f14023099e739002ff065480d6c2fe14f04` |
| `src/Domain/Common/BaseEntity.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `63cce9fecf6c02f797ec2a81e29f7d59b43570689b64db550ed739012756b38f` |
| `src/Domain/Common/IAuditableEntity.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `4e14cd763bcf55c3b8b48a8726b852a1cfd4b116185da32f3301bba09a3da114` |
| `src/Domain/Digest/IsoWeek.cs` | 87 | R: 1–87; no separate finding; runtime limitations apply | `d16650c0b46f2276e4734c9c2424b0550ae17a8be07699d0ef78f1cf0536bb73` |
| `src/Domain/Digest/User.cs` | 110 | R: 1–110; no separate finding; runtime limitations apply | `b8cec121452958e04612decb4dcae9acb7300e26ada1656afbd94c0561c36ed4` |
| `src/Domain/Digest/UserMusicConnection.cs` | 76 | R: 1–76; no separate finding; runtime limitations apply | `960def66127c185db82eb3643eb89a0b6eb87b5252083428c3d9ed2c4cffcf29` |
| `src/Domain/Digest/WeeklyDigest.cs` | 128 | R: 1–128; no separate finding; runtime limitations apply | `6a1690d2e9c83bea7a9a8117dd3797cf4e4c689341f7a24f2e44cff8fc4ec36f` |
| `src/Domain/Digest/WeeklyRecommendation.cs` | 91 | R: 1–91; no separate finding; runtime limitations apply | `a98b5d106afb3daae9891c7912091fb29523d6a88a1df103827e90c7ee0c4065` |
| `src/Domain/Digest/WeeklySchedulePolicy.cs` | 52 | R: 1–52; no separate finding; runtime limitations apply | `86148e245ca4554f4edab1d6e79282a966a3b8b3146c4412c8e5f78d0808aa93` |
| `src/Domain/Discovery/DiscoveryBatch.cs` | 47 | R: 1–47; no separate finding; runtime limitations apply | `0ee520db8845c5e84aa2b5d06f7b15cece9990af8d4056e4695fb53fba1fd772` |
| `src/Domain/Discovery/DiscoveryJob.cs` | 127 | R: 1–127; no separate finding; runtime limitations apply | `1c8931519cbc2a37a9d0792136ace16d473a462c73e5b05314392a91fcffca09` |
| `src/Domain/Discovery/DiscoveryRules.cs` | 17 | R: 1–17; no separate finding; runtime limitations apply | `28af5d182e1e4418e2fc7a6d457911bd49ec67a5f59417ae5adbf8d10ddd15a0` |
| `src/Domain/Discovery/DiscoverySelectionPolicy.cs` | 46 | R: 1–46; no separate finding; runtime limitations apply | `d9ae227359d7d6500693eb29a4859a1b3e9f0617a724dca5ed67ad3dac2ac284` |
| `src/Domain/Discovery/GuestSession.cs` | 18 | R: 1–18; no separate finding; runtime limitations apply | `544fca3fc0689caa70355d6faa0d713c71b0e3857da8d4feb9ea9108c55c7590` |
| `src/Domain/Discovery/ListenerProfile.cs` | 79 | R: 1–79; no separate finding; runtime limitations apply | `98d19803dd005bff37c32a02dad6174fc84e79ac08ea595f9974b5c612d32b79` |
| `src/Domain/Discovery/RecentRecommendation.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `8a239d6bb1a42d91ac15d2af928b39166ab7b842fd00c591de7071b3a9aca8d1` |
| `src/Domain/Discovery/TasteOnboarding.cs` | 30 | R: 1–30; no separate finding; runtime limitations apply | `5c153f34f221620e89a2d5d3e6816c788d9338ed5123b7a4b083a7522693de26` |
| `src/Domain/Domain.csproj` | 8 | R: 1–8; no separate finding; runtime limitations apply | `542180b8c1da3f49e1db4d866d77bdabfd1587f249586f399ba9b34aa0d07a02` |
| `src/Domain/Enums/DigestDeliveryDay.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `778dcda257cded5963a07b5de36593188209e72b6e9a9c47720a05252ed4dd03` |
| `src/Domain/Enums/DigestStatus.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `1ed10acab1522cc434bb77f3f7672a7ad222c4318feda9026235c3069e6ee00c` |
| `src/Domain/Enums/MusicServiceType.cs` | 11 | R: 1–11; no separate finding; runtime limitations apply | `7057808bd82e4372a08d93ce4ccd00cecfc414df786388f17037700cc87156e6` |
| `src/Domain/Enums/TasteSignalSource.cs` | 18 | R: 1–18; no separate finding; runtime limitations apply | `982328dbc5fb97bc1a292a510c2e6f0a5ff17df58ef3dd86e41e9424359cf892` |
| `src/Domain/Enums/TasteTargetType.cs` | 12 | R: 1–12; no separate finding; runtime limitations apply | `489fb8b9f40e5536fe3c7e26a420ef059a8dffec2c02c92678eb28725a893e7a` |
| `src/Domain/Enums/UserFeedback.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `f0b87dcc8587fc28813e2bd6fa06edd9c9628da96220dfba0f287b0bfa53916b` |
| `src/Domain/Scoring/BaselineAScorer.cs` | 185 | R: 1–185; P08, P10 | `cd309d199ef49858f0725c8d83ee486cc1d89c9fd90f9fecc8349a5036caf1bd` |
| `src/Domain/Scoring/CandidateTrack.cs` | 54 | R: 1–54; no separate finding; runtime limitations apply | `e1cc4085caa881b868449c90f88358bb8b681b4354dca2f413527a8f7170da64` |
| `src/Domain/Scoring/MatchedTagContribution.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `dfd32e24cc64853c23d9446447ac073c7608e279637670625b4331a4adc24f38` |
| `src/Domain/Scoring/RecommendationScorer.cs` | 122 | R: 1–122; no separate finding; runtime limitations apply | `76bf1d49d42c2ee467303e3c512c20827c839cb4dd7eba35746862bd90cd151a` |
| `src/Domain/Scoring/ScoreBreakdown.cs` | 122 | R: 1–122; no separate finding; runtime limitations apply | `89db00a8c2a9d55fece50a2782ac8111a9763d3fd122af5b4d5060a5a5d4b797` |
| `src/Domain/Scoring/ScoreSnapshot.cs` | 96 | R: 1–96; no separate finding; runtime limitations apply | `e30ab605e5d4b49d0d68e36ee1f0bc045427c89f5bfc5cd678833e36cecca32c` |
| `src/Domain/Scoring/ScoredCandidate.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `334edd851dbd1098b981657db70acf15d184f6a237d499a40b106b8ce860dd4b` |
| `src/Domain/Scoring/ScoringParameters.cs` | 38 | R: 1–38; no separate finding; runtime limitations apply | `a775dfb0b5f68ab81e5607a8d002cc1aab65301e53e4fa576976b35ec565ab5d` |
| `src/Domain/Taste/DiscoveryTasteMaterializer.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `77a0105592d60edf0421e20baf92fa1356a57a18d69f8f5c3a2fa9cbcc88752f` |
| `src/Domain/Taste/EffectiveTasteInputs.cs` | 75 | R: 1–75; no separate finding; runtime limitations apply | `859871676cb6c7612036d50b1b91c66017b8b3591c53aaf7305c1d31d00ecad5` |
| `src/Domain/Taste/TasteSignal.cs` | 102 | R: 1–102; no separate finding; runtime limitations apply | `000e4418055f3255ac00c693da6a4e72b1ed1f90499c54b292f6f440febed520` |
| `src/Domain/Taste/TasteVectorMaterializer.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `ca96d6ff08166f92af39b459e5fd5bae140c50bc29f2deea22152f87a61b3cf0` |
| `src/Domain/Taste/UserTasteProfile.cs` | 72 | R: 1–72; no separate finding; runtime limitations apply | `494930d3ce9a09232cc7320d7dc140a183a8d22ae5dc207cfd72ca3e94ab1349` |
| `src/Domain/packages.lock.json` | 6 | R: 1–6; no separate finding; runtime limitations apply | `03eeadc5ef377c17f787ab65f41fb4c8a9c936bb7f7f4171111fdeec8a81cb46` |
| `src/Infrastructure/Caching/DiscoveryMemoryCaches.cs` | 43 | R: 1–43; no separate finding; runtime limitations apply | `b080e81c573911638aaf9aa1710909c0efbd609a6a544b4ee861fbfeec21b8c0` |
| `src/Infrastructure/Catalog/CatalogArtifactReader.cs` | 95 | R: 1–95; no separate finding; runtime limitations apply | `1310f00d475549260b69cdaf091cf9cc209330c3a918534317333f327e17703b` |
| `src/Infrastructure/Catalog/MusicBrainzCatalogLookupSource.cs` | 72 | R: 1–72; no separate finding; runtime limitations apply | `154175d5aa50fbba8feac61e5ca15eac37a9ce12f443e87efaefe52b81faf98a` |
| `src/Infrastructure/Catalog/PublicRecordingCatalog.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `a6c4faa0a7961c02d9e8bb9e7937abd166ca827582de855bc0ffd1ea1cdd4578` |
| `src/Infrastructure/DependencyInjection.cs` | 142 | R: 1–142; no separate finding; runtime limitations apply | `e30bd79bdb1d6a746bc86ba8ad22fb12af44fecfc0bf9518271a2e5d2b15c1a0` |
| `src/Infrastructure/Discovery/PersistentDiscoveryPreparation.cs` | 24 | R: 1–24; P14 | `c8902b2c8457587cb8617ad389f50a0204041bea4b266c0ea019a0aeb2044904` |
| `src/Infrastructure/Discovery/PublicEvidenceCodec.cs` | 75 | R: 1–75; P14 | `4d16f0c91c4d890ccd3e25728addd3ab68b9d333c6cfbe9cbc85bcd4e1b17325` |
| `src/Infrastructure/Discovery/SharedCatalogLookup.cs` | 36 | R: 1–36; no separate finding; runtime limitations apply | `906be3bb20c3609de504f511419a96cf326bdd0d48f32f15d2d00fab74a585e1` |
| `src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs` | 128 | R: 1–128; P02, P05, P14 | `04163775402d9b341d8e9c6f46a3d79f86b4a776c390ff943e7597bef7099559` |
| `src/Infrastructure/Discovery/SharedPublicEvidence.cs` | 73 | R: 1–73; no separate finding; runtime limitations apply | `2803be8d2a2e1c7e037379f768cd2b451a04c20f629e088403f1e9277444a927` |
| `src/Infrastructure/Discovery/SharedSeedPreparation.cs` | 76 | R: 1–76; no separate finding; runtime limitations apply | `440e7a92679ddb560e70e14d9861c02c920fedfa6ac9e973feb25b9522451646` |
| `src/Infrastructure/Email/DigestEmailRenderer.cs` | 105 | R: 1–105; no separate finding; runtime limitations apply | `e2e5d6ae3e83760d128739cdc1100f2240ba2b8bfefa245d7b6f2b3adcf07d32` |
| `src/Infrastructure/Email/LocalEmailArchive.cs` | 108 | R: 1–108; P13 | `91cc6354589fe51932ac26239bc50116e10975545b6ffaa815c58cf77e277594` |
| `src/Infrastructure/Email/LocalEmailLegacyConversion.cs` | 134 | R: 1–134; no separate finding; runtime limitations apply | `fb7e7ee0fa9a62e2f74a2f528f681df4422515fe258de599135fec58edd166c7` |
| `src/Infrastructure/Email/LocalEmailMessageSerializer.cs` | 182 | R: 1–182; no separate finding; runtime limitations apply | `d101bde541fe1f2d028ef401f2c5e83dda3c6519fdfc3b7b68e4797943a85d5e` |
| `src/Infrastructure/Email/LocalEmailOptions.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `0720d41c5f89c93b0c1f26cae5115c70f556db7fad756164d5b7a64e73fb1833` |
| `src/Infrastructure/Email/LocalEmailPaths.cs` | 92 | R: 1–92; no separate finding; runtime limitations apply | `a4047a78dafcca0f0aa74eea1fff2bc9af5c9fd4083e38253c0dbe8315782350` |
| `src/Infrastructure/Email/LocalEmailSink.cs` | 112 | R: 1–112; no separate finding; runtime limitations apply | `f3e6ffc4320bd4c427847abcb45bd9404ef274af1536e9e164a4dc18afd8ff5f` |
| `src/Infrastructure/Email/UnsubscribeTokenService.cs` | 22 | R: 1–22; no separate finding; runtime limitations apply | `5a02a518eeb9695c36d400b389bf006021393d03c2b681d811f1646d1dea7b51` |
| `src/Infrastructure/Infrastructure.csproj` | 22 | R: 1–22; no separate finding; runtime limitations apply | `84c56f14c8e9355750cb825d299e39636314d0f98df402af2b237d2776b107bd` |
| `src/Infrastructure/RecommendationSources/LastFm/BatchTagCachingLastFmClient.cs` | 33 | R: 1–33; no separate finding; runtime limitations apply | `ca774d902fcc71b5b357fcbc9ce2316ed0301a25bdc680f1291cdde90b5c39d5` |
| `src/Infrastructure/RecommendationSources/LastFm/Fixtures/LastFmFixtureProvider.cs` | 279 | R: 1–279; no separate finding; runtime limitations apply | `f0aba8c3a28be148020f0f147d19476f1ac1c81b4eb1f5cf75ec716a30cfb100` |
| `src/Infrastructure/RecommendationSources/LastFm/ILastFmApiClient.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `c53260b2c0256efa07bcecb65b4a68bdca12d5356c8304a83724b27311535b9d` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmApiClient.cs` | 115 | R: 1–115; no separate finding; runtime limitations apply | `8e965c10283b0c024c7928c2ab6d547d767c8644cb3fc81091aa2ef5638e0e5e` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmCandidateHydrator.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `759132c796e727942875a62fe28e5cf680f470f3ef581fbb12d88d0f5bb7a86d` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmEvidenceParser.cs` | 129 | R: 1–129; no separate finding; runtime limitations apply | `0b06853d0bf4d518cfe156e5b9858900edac0182f869a4b75ddce6e61d60fece` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmOptions.cs` | 78 | R: 1–78; no separate finding; runtime limitations apply | `4a80fc397a76f3fd0b9d9351c36dc22e40a750a2e8293510f7f1c471b8201827` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmRateLimiter.cs` | 83 | R: 1–83; no separate finding; runtime limitations apply | `860df1cd6e2f1e4ca2a2cb11627d4227f811bbc522b5e8458c4da11aec59a3b4` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmRecommendationSource.cs` | 112 | R: 1–112; no separate finding; runtime limitations apply | `7d01f091735d43adccd335dbb083e4f73f540618806294d9c5ef643a3ef8a888` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmTagEvidence.cs` | 45 | R: 1–45; no separate finding; runtime limitations apply | `b68265b468c8a14b0f3d2c47cea208fd5e48640d3f503062e9037bf2de33034d` |
| `src/Infrastructure/RecommendationSources/LastFm/LocalRecordedInputLoader.cs` | 156 | R: 1–156; no separate finding; runtime limitations apply | `ad4cbcccc0d87dbaea6fff32d0b3a7ebe23cfa062c6ff019ee753c16e4758d33` |
| `src/Infrastructure/RecommendationSources/LastFm/Models/LastFmResponse.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `89c9257dd08fd9877e4c7b8fb99d3e2d40c651cb090174a484bb1f8d1f4fc6a1` |
| `src/Infrastructure/RecommendationSources/LastFm/Models/LastFmResponses.cs` | 37 | R: 1–37; no separate finding; runtime limitations apply | `79a7a8c5ee58b46223bddba91a68cf3933497133000dcafd0545aba46e067356` |
| `src/Infrastructure/RecommendationSources/LastFm/RecordedLastFmApiClient.cs` | 67 | R: 1–67; no separate finding; runtime limitations apply | `938c7154486374ec598d021df5a30b2d44be5f8782d938035d51d1eae40b9e07` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/FixtureListenBrainzApiClient.cs` | 96 | R: 1–96; no separate finding; runtime limitations apply | `06488ed912623342ea8fb3fc7b47bfc0e03ad0d657543036ea6abea73232a9c4` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzApiClient.cs` | 96 | R: 1–96; P02, P03 | `bffd2aa68433fdd72c5b05c271db0b3e799ad402577b79bbb0b3847192b4c34f` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzCanonicalSeeds.cs` | 52 | R: 1–52; no separate finding; runtime limitations apply | `847c20fc0579990602df63954a385c2b149f9123cf2bfeefef6dcbff4ddddfa8` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzContracts.cs` | 122 | R: 1–122; P03 | `6d51d245f4e8a466d7f1373a4df241156a0c69ed72e92b2dd4fe8b42b15277a4` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzEvidenceParser.cs` | 117 | R: 1–117; no separate finding; runtime limitations apply | `12e922c1970ab49ee0f70df4509c70583580ab5a8ff6228b857e7b39dba0bf56` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzPublicCatalog.cs` | 53 | R: 1–53; no separate finding; runtime limitations apply | `606956863c866f47a2da6b6313af24fa6518de5535b2b6a04a8f602b3b3a646a` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecommendationProvider.cs` | 157 | R: 1–157; P16 | `2063ea79777b50bd337d93c45899bc835bfed1fe8e697a0e8d7ffff1a72fd1c7` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecordingSeeds.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `605c394ece10462ff9b13677effa41f979706c41425002affdb1064458870bc4` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/MetaBrainzRequestGate.cs` | 61 | R: 1–61; P02 | `bfcdb4364c6c9dbff733de27d052add69b6c80a1708665a30f79d3ef19bf27db` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/RecordedListenBrainzApiClient.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `b8ac89c9f79bdaa311d7b910739a029451f9b73f496bae58200a37f6c3aa71ed` |
| `src/Infrastructure/RecommendationSources/RecommendationProviderResolver.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `a69737c918f5ecc36b9e4e4c2e30a8267483a56195836f855c0592af975bd540` |
| `src/Infrastructure/Storage/CoordinatedGenerationStorage.cs` | 136 | R: 1–136; P01 | `dbc4eb13040d613c6b42f9ab2db647f9f4b19d908cdd8b4ca3c93616e82563a9` |
| `src/Infrastructure/Storage/LocalGenerationStorage.cs` | 265 | R: 1–265; P01 | `d12c00eb9d23a94f277a390cb2d1c0c3ee974e918dc8ad03eeb255463a0e5a07` |
| `src/Infrastructure/packages.lock.json` | 93 | L: 1–93; no separate finding; runtime limitations apply | `bcd921812cf5ef9b4857e8ccad97cee42df58aeccdd8abccb94e7eba05e14d19` |
| `src/Presentation/Background/DiscoveryInventoryService.cs` | 45 | R: 1–45; no separate finding; runtime limitations apply | `898c4ce132e96a7d669467f9f6fe2e8f7678823c1f14e081ce717023f9e89f79` |
| `src/Presentation/Background/DiscoveryJobService.cs` | 115 | R: 1–115; P01, P07 | `ec92cc9ad153a8feacd7078acf9b2b3928003ea68ab05fd256537dff6bd3e2c0` |
| `src/Presentation/Controllers/ApiControllerBase.cs` | 23 | R: 1–23; no separate finding; runtime limitations apply | `ee66037e8389f951ceef584af8527f4b7f73f311e269792d757516d5bbde7ef9` |
| `src/Presentation/Controllers/AuthController.cs` | 270 | R: 1–270; P15 | `ba110eb178afc72fc97bb2adefc8541e5f459526e62e6b7db1b9a0485b36641e` |
| `src/Presentation/Controllers/DigestsController.cs` | 79 | R: 1–79; no separate finding; runtime limitations apply | `e1c2962180a9886e3a48678af6efbdc14cde578ba63830de33b5c8210641a4c3` |
| `src/Presentation/Controllers/ExportController.cs` | 55 | R: 1–55; no separate finding; runtime limitations apply | `188aa5cac5f42abaa3271d3bd75b22a49d115095cc49f6437a29a8c5911ab3d7` |
| `src/Presentation/Controllers/SubscribersController.cs` | 100 | R: 1–100; no separate finding; runtime limitations apply | `b7cb4c71eb48b562eb487acd5fe3d9472978705cfa932b2492968eb26840e1c8` |
| `src/Presentation/Controllers/TasteController.cs` | 52 | R: 1–52; no separate finding; runtime limitations apply | `7aa6020511c264c3abe51690a7e4443f03eaeff488b8c5113aba52e9ab199465` |
| `src/Presentation/Controllers/UnsubscribeController.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `d7710781fac2b0ec90e20510cbdc301aa0a8cc38db16a7bbdea2bf8761dd9ae5` |
| `src/Presentation/DependencyInjection.cs` | 168 | R: 1–168; P17 | `6d8db0a46cc10247fe446bc49fae3b7f84a639d052803d641ce9347dc60178a8` |
| `src/Presentation/Development/ManualTestEndpoints.cs` | 106 | R: 1–106; no separate finding; runtime limitations apply | `fd25eda9b1873c0e6389cd34cb4041bf1dcdc322dd57a553cbe6f4a996675088` |
| `src/Presentation/Discovery/CatalogController.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `969c6bc49ac3aa3ca6f3936de72d20c1c8f6f87c19ed398343a70039acf2de17` |
| `src/Presentation/Discovery/DiscoveryController.cs` | 76 | R: 1–76; no separate finding; runtime limitations apply | `46e92938ee521221202793039a0620793c394159170f2a8a2c214de2fa7798e3` |
| `src/Presentation/Discovery/GuestController.cs` | 96 | R: 1–96; no separate finding; runtime limitations apply | `30e7d3e5a0089b34f67acec12f61b30f4ebef98904899981e77b7360c76e1904` |
| `src/Presentation/Discovery/ListenerContext.cs` | 100 | R: 1–100; no separate finding; runtime limitations apply | `d3d718aba6b59c7ec2625f7818602082a8f14628ba4ece60df0112f43a11577e` |
| `src/Presentation/Discovery/RecentRecommendationsController.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `5d3947622502ad9c88a207f583048dcb775e2480fcd3291be21c5d6a17b7950a` |
| `src/Presentation/Discovery/TasteOnboardingController.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `fcf515a12719797323c46dd6b0775e0fa8813ce18ab2f9508416f550179a4149` |
| `src/Presentation/Filters/ApiExceptionFilterAttribute.cs` | 198 | R: 1–198; no separate finding; runtime limitations apply | `1bd26a26e8dc269710a1794fcffaf76e2269b5724ca5c2d3368172b18db4da37` |
| `src/Presentation/Filters/DiscoveryWriteFilter.cs` | 29 | R: 1–29; P15 | `16c5c14c0e26239e937e133f6d24601597515013253d9627640c7ae436f390e4` |
| `src/Presentation/Models/AuthModels.cs` | 32 | R: 1–32; no separate finding; runtime limitations apply | `465f07a78c7c72fe76481f02276fc7629e1c8cf87aa903172d46e7fa244665ab` |
| `src/Presentation/Options/JwtOptions.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `7cb8ca613b075260bf172598719d882e8cf0d0afc4af6cbe817e026a06fa9a93` |
| `src/Presentation/Presentation.csproj` | 26 | R: 1–26; no separate finding; runtime limitations apply | `2e83e8d1040c85e8848df722ee5998a537555c6e405a5d4cf23cfcc08443a1a0` |
| `src/Presentation/Program.cs` | 173 | R: 1–173; no separate finding; runtime limitations apply | `b9a23d47c2f36dd23f358d6ffba146c8409bae8f76b1ea75403dc8ca4a5de995` |
| `src/Presentation/Properties/launchSettings.json` | 37 | R: 1–37; no separate finding; runtime limitations apply | `7ac25744e0f8481d564ae0bc2cb3256a4666344730de07317fcdb208157758f5` |
| `src/Presentation/Services/AuthService.cs` | 212 | R: 1–212; P15 | `a1fa497ebafab77f71de16595dc21b585db62dd7e02c45ba6ead6ef61feb449b` |
| `src/Presentation/Services/CurrentUserService.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `16dde0a6328388146890e25b8c7e8ca1be87e1fafd67a9193c411f7afd7452db` |
| `src/Presentation/Services/JwtService.cs` | 105 | R: 1–105; no separate finding; runtime limitations apply | `0079aa565c4f55306ac591aea2299605926cd5d58f654f7a71763f3b3ced9065` |
| `src/Presentation/appsettings.Development.json` | 10 | R: 1–10; no separate finding; runtime limitations apply | `0c0cdcebab7e552a5cf1bd392a47756f42857ed39f7adb0e63461ad9647e693d` |
| `src/Presentation/appsettings.json` | 33 | R: 1–33; no separate finding; runtime limitations apply | `2fd16c99307467397a6856253e9978042762ec0e5f45cd90b3d8380adf1c389d` |
| `src/Presentation/packages.lock.json` | 524 | L: 1–524; no separate finding; runtime limitations apply | `f9f2d3dda7e0396c53caf4fbc7756940e9329c894bff7e7cdce5ab4c9aac9d71` |
| `src/Presentation/wwwroot/app.js` | 617 | R: 1–617; no separate finding; runtime limitations apply | `17fdad6831781457134f32543905e5ea3a72807c5ad68456094ea7a5ce78bfb5` |
| `src/Presentation/wwwroot/discovery.js` | 521 | R: 1–521; P04 | `923aa530b64562d9fbd284968ce5226de25968fd57316022044cc19eea3179cd` |
| `src/Presentation/wwwroot/index.html` | 179 | R: 1–179; no separate finding; runtime limitations apply | `f1a941b9a09fff6781df20331f184502429f1c7e6122f2d05dd185ed3cd712be` |
| `src/Presentation/wwwroot/manual-test/index.html` | 218 | R: 1–218; no separate finding; runtime limitations apply | `a042d549ebc96f898d457dca222970d6f99f93264214a764559634630822fe33` |
| `src/Presentation/wwwroot/manual-test/manual-test.css` | 229 | R: 1–229; no separate finding; runtime limitations apply | `345f3174aee636e254bf85b63b159d02ed01ecb75408320a32588f37ec01bc47` |
| `src/Presentation/wwwroot/manual-test/manual-test.js` | 574 | R: 1–574; no separate finding; runtime limitations apply | `b5dd7ddba2ed9b29a7f832517e34c93c7bb4b8f01d45299ce4f897ff5d9dbd90` |
| `src/Presentation/wwwroot/styles.css` | 1021 | R: 1–1021; no separate finding; runtime limitations apply | `e5fd74f1765948fdd4de5c5706606e242e32f3779373a96e85e8418cde4c9525` |
| `src/Worker/LocalDigestWorkerRunner.cs` | 54 | R: 1–54; no separate finding; runtime limitations apply | `c59881ab858d168065a00d75bf388ecaa82329f63d8c5d092449087daea2af5b` |
| `src/Worker/LocalScheduleOptions.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `752e2524821c03a5ba71b3e1e20d5751f90dd7ab204b304d48d96c30eeb9cd27` |
| `src/Worker/LocalScheduledWorker.cs` | 45 | R: 1–45; no separate finding; runtime limitations apply | `8d21ecb2980f7aa6ce2cfe9f7ffb75cad9e579d7f83bc76ee7f0f76eb30da867` |
| `src/Worker/LocalWorkerOptions.cs` | 23 | R: 1–23; no separate finding; runtime limitations apply | `ba99ff9285a16ceb138940b67e7b3e8de194c511ba71a3d9a304fb0da3934924` |
| `src/Worker/Program.cs` | 55 | R: 1–55; no separate finding; runtime limitations apply | `6dfb7c9e738968187eb5aabbbc75a102a1c12c622038d699294f4cb2cdd6f66a` |
| `src/Worker/Worker.csproj` | 20 | R: 1–20; no separate finding; runtime limitations apply | `c45c72e3cef8b1f0ff57208ad3e557b969fbe065ae20efbf97e1bc933c923975` |
| `src/Worker/appsettings.Development.json` | 1 | R: 1–1; no separate finding; runtime limitations apply | `ca3d163bab055381827226140568f3bef7eaac187cebd76878e0b63e9e442356` |
| `src/Worker/packages.lock.json` | 516 | L: 1–516; no separate finding; runtime limitations apply | `bdecae993d033c14c1fa3d5702dbe8ad0335c4b318064dad8d9e5206898c926c` |
| `tests/Application.Tests/Application.Tests.csproj` | 26 | R: 1–26; no separate finding; runtime limitations apply | `4e2bf85e488a3b45314b821de75cb74ed9a362e8b0a05ebe06bb94d9a81a72f6` |
| `tests/Application.Tests/Common/ApplicationBoundaryTests.cs` | 21 | R: 1–21; no separate finding; runtime limitations apply | `9fa5acca17b48c42a3f11ba302d219fb04e89f4eab4291d91b483898ae36583e` |
| `tests/Application.Tests/Common/Behaviors/ValidationBehaviorTests.cs` | 82 | R: 1–82; no separate finding; runtime limitations apply | `8e977cae84e162b5f81dc05e41e0897427c7f783011b6017253c09176424ae20` |
| `tests/Application.Tests/Common/Behaviors/ValidationCancellationTests.cs` | 34 | R: 1–34; no separate finding; runtime limitations apply | `b3cf508b0e397fc43d48df212a46c41094ee872aed469769130ebe3f36d9dde1` |
| `tests/Application.Tests/Common/EvaluationMetrics.cs` | 145 | R: 1–145; no separate finding; runtime limitations apply | `3602abed1fb994e45c5f6ed7d18dd2a5a0242fbf56da542eb11e0afc4ee4ce10` |
| `tests/Application.Tests/Common/EvaluationMetricsTests.cs` | 121 | R: 1–121; no separate finding; runtime limitations apply | `efa9729be00f700421f6c723169eb0b8de677aebd7cd98dbc27f163dfcd6d250` |
| `tests/Application.Tests/Common/Exceptions/ExceptionTests.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `61a9dc5c8d9990ca3bef93eaa74413eee622241495f15455aef8cb54bdcec475` |
| `tests/Application.Tests/Common/FixtureEvaluationHarness.cs` | 167 | R: 1–167; no separate finding; runtime limitations apply | `a8c2c841b63d86d9b240da0967d3144ecd91b2695bc1d836a26ac62459d19081` |
| `tests/Application.Tests/Common/FixtureEvaluationHarnessTests.cs` | 125 | R: 1–125; no separate finding; runtime limitations apply | `08cb5ce75430c20c42def2752fe0ba828ff0ea51194a6ac4095af1a3102ecdf9` |
| `tests/Application.Tests/Common/Mappings/MappingTests.cs` | 111 | R: 1–111; no separate finding; runtime limitations apply | `72ea3e34517af20e5c6925065464076cdd0e2cec8b7a41a8a63662d5918888a0` |
| `tests/Application.Tests/Common/Phase2ProfileMembership.cs` | 50 | R: 1–50; no separate finding; runtime limitations apply | `b732dae087eb07f445083906a61cf2db23e50110bd1850f152bd2f909b24fec0` |
| `tests/Application.Tests/Common/PilotEvaluation.cs` | 140 | R: 1–140; no separate finding; runtime limitations apply | `cbe770703fda8aa4298d29f4344f70413a01c75d6a2b2f24664bb5aee52d590e` |
| `tests/Application.Tests/Common/PilotScoring.cs` | 145 | R: 1–145; no separate finding; runtime limitations apply | `9fe8cede4a1df15d87c9df2a607727e743c7e1d0252191544bfdb238111a9524` |
| `tests/Application.Tests/Common/PilotScoringTests.cs` | 157 | R: 1–157; no separate finding; runtime limitations apply | `d799f098cb3202aa668abd7674951567288582c20eb32677e3d1b3595a3ef577` |
| `tests/Application.Tests/Common/ProfileLockTests.cs` | 85 | R: 1–85; no separate finding; runtime limitations apply | `d12a0feb564aad5e9b158657c9e56ed80aa66b6cf7df47e737f9a563a5eadc15` |
| `tests/Application.Tests/Features/Digests/DigestAndFeedbackTests.cs` | 238 | R: 1–238; no separate finding; runtime limitations apply | `2201a082edaef8331d90434b9854ca5da7ea8521c21469492fc8ea137acafeca` |
| `tests/Application.Tests/Features/Digests/GenerateDigestTests.cs` | 314 | R: 1–314; no separate finding; runtime limitations apply | `a5761eca943892a4b513df7165196a63b071b01e111c8ed17d525ee9c14757df` |
| `tests/Application.Tests/Features/Discovery/DiscoveryRuntimeOptionsTests.cs` | 21 | R: 1–21; no separate finding; runtime limitations apply | `88c20e1712288f6c008ed754bed0a43a41230be8955eebed32dc5470985e1f01` |
| `tests/Application.Tests/Features/Discovery/DiscoveryTasteTests.cs` | 153 | R: 1–153; no separate finding; runtime limitations apply | `74f0352d34eddde2cb2dede7589975271d387a62c6cda5248534f97df8a2c099` |
| `tests/Application.Tests/Features/Discovery/GuestActivityTests.cs` | 58 | R: 1–58; no separate finding; runtime limitations apply | `230c0ad5ee6dd65f8ad056fe167ebe171b2ec37809587fea96aec99a03909a60` |
| `tests/Application.Tests/Features/Discovery/PrepareDiscoveryBatchTests.cs` | 86 | R: 1–86; no separate finding; runtime limitations apply | `5cbebab27fba38acf35ab00ac6439ad61639a346149228de8f1660921eef21bd` |
| `tests/Application.Tests/Features/Discovery/TasteVocabularyTests.cs` | 31 | R: 1–31; no separate finding; runtime limitations apply | `efa16ce67b0f78add8457da1d52b632f5d365f9bf3b4dcd6e54ec25cafedde3c` |
| `tests/Application.Tests/Features/Export/GetUserDataExportTests.cs` | 219 | R: 1–219; no separate finding; runtime limitations apply | `ff70ac501497cb91c341b85f01f7f5efe0f4a1995170eb34c57a064449c7636d` |
| `tests/Application.Tests/Features/RunScheduledTickTests.cs` | 117 | R: 1–117; no separate finding; runtime limitations apply | `2a914dc64bddec7a2aa44a35b13ef91f200d1617bfa9d34830c7724adc03f692` |
| `tests/Application.Tests/Features/Subscribers/RegisterSubscriberTests.cs` | 131 | R: 1–131; no separate finding; runtime limitations apply | `47d13f2162ce8db8d119af220cc55c0757eaefc9c257a6ae944b5493e66865e7` |
| `tests/Application.Tests/Features/Subscribers/SubscriberQueryAndCommandTests.cs` | 80 | R: 1–80; no separate finding; runtime limitations apply | `59b3e4bdb353c651838259767e04b295e50de8d50c0e2de797b5d3492a04356d` |
| `tests/Application.Tests/Features/Taste/GetUserTasteSignalsTests.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `fcde465998d83b39036e52da4dc6f8109d2958c4ded276076b331d0d54d8bcb7` |
| `tests/Application.Tests/Features/Taste/SeedTasteProfileTests.cs` | 161 | R: 1–161; no separate finding; runtime limitations apply | `f2b7e33659812f19f0f3c64071ed266683f3a973a130b0c8f9374b2c2e4157a3` |
| `tests/Application.Tests/packages.lock.json` | 251 | L: 1–251; no separate finding; runtime limitations apply | `292cfc2489baa7c7ee1e6a1b10d826a7a023c8f6df997a65762c70393fefc425` |
| `tests/DataAccess.Tests/AppDbContextModelTests.cs` | 136 | R: 1–136; no separate finding; runtime limitations apply | `a8afa814cf60abb02ec3af59a101dff608e9446c47297e06f30fa5bf22e7426b` |
| `tests/DataAccess.Tests/ArchitectureTests.cs` | 31 | R: 1–31; no separate finding; runtime limitations apply | `e8c29359bba0caf56cc77fd717cacb90dd07c7c8e53343a0c2787b7cb9ad9719` |
| `tests/DataAccess.Tests/AsyncRepositoryTests.cs` | 133 | R: 1–133; no separate finding; runtime limitations apply | `01f69fed1b68a401aa4c4025994019d73d74505b7daa4df84073f56dc2458c42` |
| `tests/DataAccess.Tests/AuditTimestampTests.cs` | 68 | R: 1–68; no separate finding; runtime limitations apply | `e410678983d228a5fe4e7d64901a8f066cc1dd4f0976671d04c1a7dfc831d03b` |
| `tests/DataAccess.Tests/CatalogPostgresTests.cs` | 287 | R: 1–287; no separate finding; runtime limitations apply | `660a55e71b1acaaec2e8b31c681f550e2c7bd4896755fe4ae9b4f4565d825ea5` |
| `tests/DataAccess.Tests/ComprehensiveRepositoryTests.cs` | 135 | R: 1–135; no separate finding; runtime limitations apply | `8c53b1b143f0895073764621c7a11b8fa97115fa3bc8624370f7dd9662a5493e` |
| `tests/DataAccess.Tests/DataAccess.Tests.csproj` | 28 | R: 1–28; no separate finding; runtime limitations apply | `bab99e528d036df6881f19418d8f29fc339dfefc7d4188e6da0aa6834e989524` |
| `tests/DataAccess.Tests/DataContextAuditAndSoftDeleteTests.cs` | 117 | R: 1–117; no separate finding; runtime limitations apply | `91eba2a265ab2738d434f9c61e5789b8198f8e330e83b0705bdbd95659558d1b` |
| `tests/DataAccess.Tests/DiscoveryCoordinationPostgresTests.cs` | 706 | R: 1–706; no separate finding; runtime limitations apply | `23e182d7bbe1c17d1fb99590a4b50b956f7cef2d811bd962000d812450949a7c` |
| `tests/DataAccess.Tests/DiscoveryMigrationTests.cs` | 383 | R: 1–383; no separate finding; runtime limitations apply | `800f73645e7f57a7c41d449be39ad2af71f2319a0d62096e2b039ac383dd5301` |
| `tests/DataAccess.Tests/LocalScheduleOptionsTests.cs` | 59 | R: 1–59; no separate finding; runtime limitations apply | `7ac46237ad4c0ba0ed943ae3be34550063b3e7abc8e4ed74d401ae8f80a208f5` |
| `tests/DataAccess.Tests/LocalScheduledWorkerTests.cs` | 57 | R: 1–57; no separate finding; runtime limitations apply | `d063c48b72ede9ef78ed673945a7d253621c99bf7811f59523b52b7e08de19ec` |
| `tests/DataAccess.Tests/LocalUnsubscribePostgresTests.cs` | 92 | R: 1–92; no separate finding; runtime limitations apply | `cee5ca6c68606ccbcd082f4d225e7ff284026d467d63aa4ae536440ca2252001` |
| `tests/DataAccess.Tests/LocalWorkerOptionsTests.cs` | 34 | R: 1–34; no separate finding; runtime limitations apply | `8b731dfeb40db43451b73e2825e37256aed7c2f0c7329f5bb468093b4566e0aa` |
| `tests/DataAccess.Tests/LocalWorkerPostgresTests.cs` | 245 | E: 1–245; no separate finding; runtime limitations apply | `e7acb582e5b724a2840b7b69908208081558f7d6b35f42f2da6d74f2d3cf9c65` |
| `tests/DataAccess.Tests/OptionalTasteOnboardingPostgresTests.cs` | 118 | E: 1–118; no separate finding; runtime limitations apply | `603f5b63092534dbd3081f06848e3d7b505346a87c8c40b70aedd97edd37f7c3` |
| `tests/DataAccess.Tests/Phase6PostgresTests.cs` | 435 | E: 1–435; no separate finding; runtime limitations apply | `c999256db85dcc12de56119cc48f17c6a3a8fb84f1b6d24d909f6ee929de8746` |
| `tests/DataAccess.Tests/Phase8PrivacyInventoryTests.cs` | 105 | E: 1–105; no separate finding; runtime limitations apply | `c1a027f709e0ffc415d002287bb8fb2f9a53da71c8adb9ef4b6734f0788c1835` |
| `tests/DataAccess.Tests/Phase8ScheduledWorkerPostgresTests.cs` | 588 | E: 1–588; no separate finding; runtime limitations apply | `e333f704eeb2dae06d51510a8f1389dd54fe0e98c665f5e4520d4b9cccb18794` |
| `tests/DataAccess.Tests/PopulatedLegacyMigrationTests.cs` | 89 | E: 1–89; no separate finding; runtime limitations apply | `c6607ca5acd603c0027892572d971fcfdfa78a1557dae195f94d869742faba5f` |
| `tests/DataAccess.Tests/PrReviewRegressionPostgresTests.cs` | 89 | E: 1–89; no separate finding; runtime limitations apply | `6e1f3a69ade310c62be407fca4a999ae9ad4a073413905eea79f46da3c25aa9f` |
| `tests/DataAccess.Tests/RecordingCatalogPostgresTests.cs` | 45 | E: 1–45; no separate finding; runtime limitations apply | `b0a7ae21ab1aed5ac56a251cdd7bb101eb5d2cc65a5e2c2908291a3df9763594` |
| `tests/DataAccess.Tests/RepositoryTests.cs` | 120 | E: 1–120; no separate finding; runtime limitations apply | `cbcff325523788df79679bfeb1f87d75761da5de060b924121e4793f63605cde` |
| `tests/DataAccess.Tests/UnitOfWorkTests.cs` | 33 | E: 1–33; no separate finding; runtime limitations apply | `62a3afb87550b9643c8d1b2353b44b47098ccda97e76af22d62c83c88099203f` |
| `tests/DataAccess.Tests/packages.lock.json` | 622 | L: 1–622; no separate finding; runtime limitations apply | `199caf7b1ddddfec996e996e7998d05a1e64adcfa4378ac69e73b87ae35a2284` |
| `tests/Domain.Tests/Architecture/OnionArchitectureTests.cs` | 33 | R: 1–33; no separate finding; runtime limitations apply | `d0a6fc65a373385c0b80bbbbb8b6c1c6222ef50e2e36c2cd95ebf9bb08fab5bf` |
| `tests/Domain.Tests/Catalog/CatalogTextTests.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `e3a293ddc3900fbaaf519f8ede66372851c937a387efc21b27b6cea60b870154` |
| `tests/Domain.Tests/Catalog/TrackAndArtistTests.cs` | 86 | R: 1–86; no separate finding; runtime limitations apply | `8bd9a02998645baef01fc25c343c958abf8f59508f8d3859dc70b7f6a40f3f3a` |
| `tests/Domain.Tests/Catalog/WeightedTagVectorTests.cs` | 117 | R: 1–117; no separate finding; runtime limitations apply | `fa17a706d964686365661090b5ac074e6b10f0afc15d01a555561cc860b26d19` |
| `tests/Domain.Tests/Digest/IsoWeekExtendedTests.cs` | 69 | R: 1–69; no separate finding; runtime limitations apply | `c2e9ab296086dbe0fe9b581eb047d457b94cca68cf25d8ad2acd09affcad1730` |
| `tests/Domain.Tests/Digest/LocalEmailLifecycleTests.cs` | 37 | R: 1–37; no separate finding; runtime limitations apply | `a8a31f75f4e338c00305c1e2478e07886707c86f4aa1cae0cbc730f4002601b3` |
| `tests/Domain.Tests/Digest/UserAndConnectionTests.cs` | 113 | R: 1–113; no separate finding; runtime limitations apply | `d2e145891e6be171737445cb5bfc121e7742b01e82d519163bccbd92c8273541` |
| `tests/Domain.Tests/Digest/WeeklyDigestTests.cs` | 113 | R: 1–113; no separate finding; runtime limitations apply | `3ab0ae9e1e8ed9ed550406a7d2f309a4414861e34dba0e0fb5a638f225fa6195` |
| `tests/Domain.Tests/Discovery/DiscoveryFoundationTests.cs` | 86 | R: 1–86; no separate finding; runtime limitations apply | `a7b682fe62d2cf1ce28a8568414ec116d6124c57189928eb3d861216d3de5f77` |
| `tests/Domain.Tests/Discovery/DiscoveryOwnershipTests.cs` | 49 | R: 1–49; no separate finding; runtime limitations apply | `62000ef128324f8c8df54b77aa7964c3ba5de91d40f0d075cb13c285ee308294` |
| `tests/Domain.Tests/Discovery/OptionalTastePolicyTests.cs` | 118 | R: 1–118; no separate finding; runtime limitations apply | `f57201f64b7429c06b1c4d37af95e6716e339cbc6b9748c5fcdd6c93a7471255` |
| `tests/Domain.Tests/DiscoveryJobLeaseTests.cs` | 54 | R: 1–54; no separate finding; runtime limitations apply | `ec44de9fdd456f1fefce3bb80fe456c983e64e2f9170b6bc2872f89d585329da` |
| `tests/Domain.Tests/DiscoveryTasteMaterializerTests.cs` | 70 | R: 1–70; no separate finding; runtime limitations apply | `32e8e27f9e626b1d30ea0f28651a4ee44704da5b1761c45f6cc5da79afe1a464` |
| `tests/Domain.Tests/Domain.Tests.csproj` | 23 | R: 1–23; no separate finding; runtime limitations apply | `ae865900393e51196c763686e0ff2e4fd924b46e9bb847071f6512aa1af4b58b` |
| `tests/Domain.Tests/Fixtures/BenchmarkSeedFixtures.cs` | 224 | R: 1–224; no separate finding; runtime limitations apply | `d106a6520fe6aa34d0c59db4b65e2628c0ca26c98065953d2a6dca82714edb54` |
| `tests/Domain.Tests/Recommendation/BaselineAScorerTests.cs` | 99 | R: 1–99; no separate finding; runtime limitations apply | `bea9b203877478bf63df9c471e739403205978db6a6f468ac01e6c6227689ca7` |
| `tests/Domain.Tests/Recommendation/CandidateTrackAndParamsTests.cs` | 66 | R: 1–66; no separate finding; runtime limitations apply | `97376c9abcafe7aff8c03bd571ba69ffcb06c42f7b9e4fd3f62f67e12938311d` |
| `tests/Domain.Tests/Recommendation/RecommendationScorerTests.cs` | 240 | R: 1–240; no separate finding; runtime limitations apply | `e87693e05db9afbf97052248510bbce3173c12bfddd95fb9f35e1bc05d26f08f` |
| `tests/Domain.Tests/Taste/TasteSignalTests.cs` | 45 | R: 1–45; no separate finding; runtime limitations apply | `d0437edf225b30e6d712e24e99d4b9f1ee8695fe5301d8fbc60b49ee32a6513d` |
| `tests/Domain.Tests/Taste/TasteVectorMaterializerTests.cs` | 59 | R: 1–59; no separate finding; runtime limitations apply | `0b411ffd8c98d85b083146f1e448d534a1ceb2db222bd420dc9f39dfaf0024ef` |
| `tests/Domain.Tests/Taste/UserTasteProfileExtendedTests.cs` | 68 | R: 1–68; no separate finding; runtime limitations apply | `be513f300cfc742db54c37c7173e0f07d86d56a4b8a925d3f3704aae81e7bfd5` |
| `tests/Domain.Tests/WeeklySchedulePolicyTests.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `0b7a085f593bb90dbd3e4b345a12db18c05e98752582db293f927f0678912be1` |
| `tests/Domain.Tests/packages.lock.json` | 96 | L: 1–96; no separate finding; runtime limitations apply | `750ea787eae2018a001eee8360a22ab00e2ae9ab4237b9069db60596d54c8998` |
| `tests/Infrastructure.Tests/Caching/DiscoveryMemoryCacheTests.cs` | 35 | E: 1–35; no separate finding; runtime limitations apply | `b88c028938fba99bf301634504ac12b497678802ffe5865ee0397607922e9b59` |
| `tests/Infrastructure.Tests/Caching/PersistentDiscoveryPreparationTests.cs` | 168 | E: 1–168; no separate finding; runtime limitations apply | `ba5192028ecb41ec283376e9a4994c3de460339df83f472d89b767f4408080dc` |
| `tests/Infrastructure.Tests/Caching/PublicEvidenceCodecTests.cs` | 50 | E: 1–50; no separate finding; runtime limitations apply | `5558228bc9a703cf1fa96714995b6b71130ecbf25ab02b4ccd3094c0a2da8cf0` |
| `tests/Infrastructure.Tests/Caching/SharedDiscoveryPreparationTests.cs` | 135 | E: 1–135; no separate finding; runtime limitations apply | `a0e7133009be42d23263bbd98dbc0b80b7506e9a4ca2d5d51039bef512d3c43c` |
| `tests/Infrastructure.Tests/Catalog/CatalogArtifactTests.cs` | 101 | E: 1–101; no separate finding; runtime limitations apply | `ce568c6b1855d9b19d15bb43f98eafeb57c4ebfd7603e8d5d9c167fa1523add1` |
| `tests/Infrastructure.Tests/Fakes/EvidenceFixtures.cs` | 31 | E: 1–31; no separate finding; runtime limitations apply | `4830bdc4b13f6af1dbf6df4e78108152b110eda106c225e7239423052e326405` |
| `tests/Infrastructure.Tests/Fakes/MockHttpMessageHandler.cs` | 43 | E: 1–43; no separate finding; runtime limitations apply | `60aad55c2d03d52a9de43bbc3c38757e8729b1c2bb345316a571d5924c39e6b7` |
| `tests/Infrastructure.Tests/GenerationStorageTests.cs` | 141 | E: 1–141; no separate finding; runtime limitations apply | `19110cd1aa8e97386b2e3874469bbb97e61765281963aef7bfdee32ba3a8c898` |
| `tests/Infrastructure.Tests/Infrastructure.Tests.csproj` | 35 | E: 1–35; no separate finding; runtime limitations apply | `6c381b03879637fe9f81a8fc0f900d1d928cc4b875d8e0f4656b6544a0ab2584` |
| `tests/Infrastructure.Tests/ListenBrainzProviderTests.cs` | 178 | E: 1–178; no separate finding; runtime limitations apply | `02c26ce21a338de82ad28d221db4336604d608470e46d9b25d312e93e03f504e` |
| `tests/Infrastructure.Tests/ListenBrainzRecordedInputTests.cs` | 55 | E: 1–55; no separate finding; runtime limitations apply | `a7d9936b4cf697e642e14b43f19fadd53f338df250ebdafc998cf15781a28f01` |
| `tests/Infrastructure.Tests/ListenBrainzTransportTests.cs` | 224 | E: 1–224; no separate finding; runtime limitations apply | `f218a6709cbdf4b8fab51c15bc3625825ed71f80106493fa38fd7c3c51b262a3` |
| `tests/Infrastructure.Tests/LiveRecordingCatalogTests.cs` | 63 | E: 1–63; no separate finding; runtime limitations apply | `9d7ccba01a4001d4a02b05523f2cd72d1497fb34f0b0a6d45059f8d1c087d80a` |
| `tests/Infrastructure.Tests/LocalEmailTests.cs` | 429 | E: 1–429; no separate finding; runtime limitations apply | `aa1a82fab182f79651b5de438c7baaeffbe1dd78f81f218c2d6fae10427d2ed9` |
| `tests/Infrastructure.Tests/LocalRecordedInputTests.cs` | 60 | E: 1–60; no separate finding; runtime limitations apply | `d464b997e46fe5f3a204794ee9c0d3a20574d5be61abd0b6f744c81e6571a7b4` |
| `tests/Infrastructure.Tests/RecommendationProviderTests.cs` | 83 | E: 1–83; no separate finding; runtime limitations apply | `8d482455710f4f791e84920b4cf060c01a24de387d7e2caffae61786a45bda29` |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmApiClientTests.cs` | 221 | E: 1–221; no separate finding; runtime limitations apply | `7f5a0a7d6cea21f2bbf418b84452553b7288e381a39fc7082184ca8821474ddd` |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmCandidateHydratorTests.cs` | 78 | E: 1–78; no separate finding; runtime limitations apply | `c436c0a72bf2d105b08b6650f4e1e0154b794869c620c74fa89da5f2e8aef9b8` |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmRateLimiterTests.cs` | 51 | E: 1–51; no separate finding; runtime limitations apply | `5a78004eabdaa3de3ab215bca6f404813daf32805a65897fde76eaf62f1e2d15` |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmRecommendationSourceTests.cs` | 97 | E: 1–97; no separate finding; runtime limitations apply | `807961e0f38ff84bfe15604d27c2382df268d8b7eef374567702d32ab9fd3cdc` |
| `tests/Infrastructure.Tests/RecommendationSources/Phase3EvidenceTests.cs` | 345 | E: 1–345; no separate finding; runtime limitations apply | `059c74b53648a4433d67f37b331e1ab323294f94f266fa0fd0eee4b11c33c09b` |
| `tests/Infrastructure.Tests/RecommendationSources/Phase3IngestionTests.cs` | 71 | E: 1–71; no separate finding; runtime limitations apply | `7e88218535d5aa368e3e3d5afd69c94cca0245d64112eadedf51c779b8226007` |
| `tests/Infrastructure.Tests/RecommendationSources/PilotReplay.cs` | 167 | E: 1–167; no separate finding; runtime limitations apply | `fedacb788312271b54ab1a0d56efb30dcd7db44315d8f16fdd06b0299c6c9ef8` |
| `tests/Infrastructure.Tests/RecommendationSources/PilotReplayTests.cs` | 164 | E: 1–164; no separate finding; runtime limitations apply | `7999a0607f29042fc0bb0090e2b0199aa66713a1358dda8eda47b9bef1a5ea19` |
| `tests/Infrastructure.Tests/RecommendationSources/RecordingDiscoveryTests.cs` | 90 | E: 1–90; P16 | `ce811514796b4e29aa08bd62559fbece57c55f93f61edbd91b3206fb500109ce` |
| `tests/Infrastructure.Tests/Spikes/FounderSeedCoverageSpikeTests.cs` | 45 | E: 1–45; no separate finding; runtime limitations apply | `484c4b189832f2186858c1db949cdfc25f969185f3b6242d2051f5262cd73cb1` |
| `tests/Infrastructure.Tests/Storage/CoordinatedStorageTests.cs` | 110 | E: 1–110; no separate finding; runtime limitations apply | `b838aabe2015cfc2961da13fd3523c834e6ab21333688d35e1d477e4c6f0ae4f` |
| `tests/Infrastructure.Tests/packages.lock.json` | 345 | L: 1–345; no separate finding; runtime limitations apply | `d7b23101046d74c9c75fd56e15806642a52fbca44013290f8e095d45993be733` |
| `tests/Presentation.Tests/ApiExceptionFilterAttributeTests.cs` | 121 | E: 1–121; no separate finding; runtime limitations apply | `93abdd1ba8fcf4a9b27acdd90b9d36b9a1da2e207745f3f73ae03905ebad46e2` |
| `tests/Presentation.Tests/Controllers/AuthControllerTests.cs` | 247 | E: 1–247; no separate finding; runtime limitations apply | `f64764142aaf958658590713e73d1eef7c91373a4df035e32a9eb514858583e6` |
| `tests/Presentation.Tests/Controllers/DigestsControllerTests.cs` | 111 | E: 1–111; no separate finding; runtime limitations apply | `67012b92c74c51ba13365dea4013090ea22835d1e9dc5831092229a4c2e7b494` |
| `tests/Presentation.Tests/Controllers/ExportControllerTests.cs` | 108 | E: 1–108; no separate finding; runtime limitations apply | `8d6dfa96f154c8e34757349dc2bb24f25b2e3887043890e0d0167eb862c25ee2` |
| `tests/Presentation.Tests/Controllers/SubscribersControllerTests.cs` | 132 | E: 1–132; no separate finding; runtime limitations apply | `fd7c3aa1e7b3f78753d33258a55d67106517450ae3a860435baad27e4ad88c7f` |
| `tests/Presentation.Tests/Controllers/TasteControllerTests.cs` | 75 | E: 1–75; no separate finding; runtime limitations apply | `7124e917fbab8e694c1cb88d57adaa116d13feb20e774022efa8e72f74a410e7` |
| `tests/Presentation.Tests/CurrentUserServiceTests.cs` | 72 | E: 1–72; no separate finding; runtime limitations apply | `5105fef2b62490157e49d88976188f88c42494a92de05c0a93b052406f42bb53` |
| `tests/Presentation.Tests/DiscoveryInventoryServiceTests.cs` | 58 | E: 1–58; no separate finding; runtime limitations apply | `ccf26e06598205a189838b687ec31841a51780a80b56e0879b02f0196852068c` |
| `tests/Presentation.Tests/DiscoveryJobServiceTests.cs` | 123 | E: 1–123; no separate finding; runtime limitations apply | `fb3499b0e288af9d564c53be4e3cb53b056c0ac6a50cda0ee7eb360e225ea419` |
| `tests/Presentation.Tests/DiscoveryWriteFilterTests.cs` | 78 | E: 1–78; no separate finding; runtime limitations apply | `16eed5f9ad9731223afa60781f2962bd791d56825d7b45a7ebfdd0a3015f21eb` |
| `tests/Presentation.Tests/GuestDiscoveryApiTests.cs` | 223 | E: 1–223; no separate finding; runtime limitations apply | `04ad341fa991156a03d36a1fb4a134c071b121b34f2e1030cd85b8e209d3189d` |
| `tests/Presentation.Tests/JwtServiceTests.cs` | 82 | E: 1–82; no separate finding; runtime limitations apply | `aa67dc37561a89c663ccd74ea2a6cd1810701a0addb7e33cd2575ab416ad7a32` |
| `tests/Presentation.Tests/ListenerContextTests.cs` | 96 | E: 1–96; no separate finding; runtime limitations apply | `0c94b14f3c7f24b15f2dcc43a6ec214315e5d078a9e582f4f3bbd0ffb8fd38dd` |
| `tests/Presentation.Tests/ManualTestEnvironmentTests.cs` | 123 | E: 1–123; no separate finding; runtime limitations apply | `188aa0f569d02aa2f76057d63ba32c554e25692c96822cb648b61e7c56c52dc9` |
| `tests/Presentation.Tests/Phase1SecurityTests.cs` | 358 | E: 1–358; no separate finding; runtime limitations apply | `62c665b35d4e6ff971d26644947466cbce6fd7e335e77870f50e476ff32ecfac` |
| `tests/Presentation.Tests/Phase8PrivacyApiTests.cs` | 492 | E: 1–492; no separate finding; runtime limitations apply | `d39c09e19ae00710756eb7f3b4fa55cf133c2fa5ed280f84eea2cee28aad9d68` |
| `tests/Presentation.Tests/Phase9SwaggerTests.cs` | 55 | E: 1–55; no separate finding; runtime limitations apply | `fa9e3d45329d08839db7342b97a13fca48c84b2f30bb55f0bd59172699c12bb8` |
| `tests/Presentation.Tests/Presentation.Tests.csproj` | 28 | E: 1–28; no separate finding; runtime limitations apply | `2c1131bfebed09b81b65fdf376953f151be03f45a7fedcebab4ffec0cad4e605` |
| `tests/Presentation.Tests/UnsubscribeRouteTests.cs` | 98 | E: 1–98; no separate finding; runtime limitations apply | `41f31b37f6dd39aeec78b40f0975b3aee8c3966cb592bde9f4fb0cecae870dfa` |
| `tests/Presentation.Tests/packages.lock.json` | 877 | L: 1–877; no separate finding; runtime limitations apply | `c639e72440e47f5ddb7cbdf5f5893608d4279849181003397a00afd2f8765f55` |
