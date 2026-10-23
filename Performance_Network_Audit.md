# Performance and network audit: execution checkpoint and plan

Date: 2026-10-24 (Asia/Baku)
Repository baseline: `3ae263c73dc1f16510a8033481b51a14cf349e16`
Audit execution baseline: `a930ebb87246a4709492ae06a543f996f85baac0` (approved plan commit; product sources unchanged).
Status: **Audit in progress. First source pass: 153/486 files, 7,620/63,157 numbered lines. Isolated storage/scoring/merge measurements and browser polling reproduction completed; exhaustive review, SQL/network attribution and retained-memory analysis remain unfinished.**

## Scope and success criteria

The developer requested a line-by-line performance and network review, using appropriate skills and MCP servers, especially Rider, dotTrace and dotMemory. They first selected **plan first, with initial findings**, then approved execution and instructed scanning every line. This document now records the first execution checkpoint and retains the approved methodology. Scope remains report only: no product fixes, project dependency updates or production configuration changes. Workload helpers and private evidence are outside Git under `/tmp/liner-performance-audit-20261024/`; no live provider requests or persistent application database mutations were made by these audit workloads.

The audit must account for every tracked first-party source/configuration line in its inventory, follow reachable call paths, and record every substantiated performance issue here. A search-pattern match is a lead, not a completed review. A file is complete only after all its numbered lines have been read, relevant callers/callees checked, and its disposition recorded. The ledger distinguishes a complete **source read** from completed end-to-end reconciliation: **no file is yet marked fully reviewed**. The first source read covers all Domain and Application files plus eight traced persistence/storage/runner/frontend files. The remaining 333 files and 55,537 lines have not received their complete source read.

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

Continue the remaining DataAccess implementation/migrations, then Infrastructure, Presentation, Worker, tests, scripts and configuration. Reconcile already-read files against implementations, endpoints and tests before promoting their disposition. Application-level W0–W8 workloads, SQL plans/counts, network/transfer attribution, concurrency/cold-cache sweeps and retained-memory analysis remain outstanding beyond these named subworkloads. No live-provider performance, production readiness, exhaustive clean bill or developer-confirmed completion is established.

Workflow observations: verify capture and licensed analysis separately; distinguish source reading from call-path/runtime reconciliation; freeze helper builds before aggregation; record tmpfs versus physical storage; use intercepted HTTP and a virtual clock to reproduce retry policy. These stay here so the pre-existing unrelated skill-observation log is preserved.

## Findings register

All priorities below are **provisional investigation priorities**, not measured production severities. No hypothetical optimization saving is claimed. `Structural` means work visible in source; `conditional` means cost requires the stated trigger. Measurements apply only to their named isolated workloads and units above. This audit remains unfinished.

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

Location: [DiscoveryProfileReader.cs:32](src/DataAccess/Persistence/Repositories/DiscoveryProfileReader.cs#L32), with the surrounding account export/serialization path still to be traced in the full audit.

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

## Pending checks that are not confirmed issues

- The host sets API `no-store` and static assets `no-cache` in [Program.cs:133](src/Presentation/Program.cs#L133). No application response-compression middleware was found in the initial search. Actual compression and cache behavior may be handled by a reverse proxy. Measure negotiated encoding, transfer size, ETag/304 and deployment headers first; do not report absent compression or complete repeated downloads as established facts.
- `ListenBrainzRecommendationProvider.HydrateCandidatesBatchAsync` repeatedly searches `responses.Values.FirstOrDefault` for recording metadata inside its candidate loop (lines 123–138). Candidate count is capped at 400 and artist metadata is already batched in chunks of 100. This is a CPU lead, not yet evidence that indexing it would matter.
- `DiscoveryReader.ReadAsync` makes several sequential queries and reloads the profile's onboarding field after querying its revision (lines 15–32). Count round trips and inspect consistency needs before proposing consolidation. Do not parallelize against a shared DbContext.
- Domain and Application now have a complete numbered source read, including score/vector work. Their full implementation/test reconciliation remains pending. Remaining assets, catalog matching/indexes, scheduled worker infrastructure, Last.fm pacing/cache paths, data minimization cleanup, email encryption and the rest of the inventory still need their full review. Absence from this issue register is not a clean bill of health.

## Required finding format for the full audit

For each issue append: stable ID; provisional/measured priority; status (`structural`, `conditional`, `measured`, `disproved`, `accepted tradeoff`); exact baseline file/line/symbol; reachable caller and route/job; trigger and input scale; evidence paths and tool settings; call/query/request counts; cost in correctly labeled units; reproduction steps; confidence and uncertainty; possible remediation and constraints; verification that would accept/reject it; developer disposition. Disproved leads stay documented with their disproof, rather than silently disappearing. Merge duplicate root causes and cross-reference their affected workloads.

Completion requires all ledger entries reviewed or explicitly outside scope with reasons, every proposed workload executed or explicitly blocked, every measured finding tied to reproducible evidence, and remaining uncertainty listed. Separate static review coverage, runtime scenario coverage, provider evidence, deployment evidence and developer confirmation. Source coverage alone does not establish runtime coverage or production readiness.

## Verification of this planning deliverable

Only this Markdown document is intended for the task commit. Validate that every referenced file and starting line exists, the full inventory matches the frozen checkout plus any explicitly recorded dirty baseline, all initial finding IDs are unique, and no file is marked fully reviewed without evidence. Review the Git diff for accidental product/configuration changes. No build or test suite is needed for a documentation-only planning artifact; no application behavior has been changed or runtime performance verified. Commit locally after these document checks; do not push.

## Task-observer note

A useful reusable profiling preflight is: enumerate actual MCP tools, confirm the IDE's active project, probe installed capture-tool help, verify runtime/platform support, and only then describe an executable capture plan. Tool registration, installed filenames, allocation totals and retained-object analysis are distinct evidence. This observation is recorded here without modifying the pre-existing observation log or installed skills.

## Coverage ledger

The generated inventory below covers tracked text source, tests, helper scripts, workflow files and root build/runtime configuration. Documentation, private/ignored state, binaries and external dependencies are excluded from the line count; generated migrations and lockfiles remain included and must be classified during review. Counts include blank/comment lines. Every entry starts `Pending`; citations above establish selected-line inspection only. The later audit must add reviewed ranges and dispositions per file, and reconcile new/deleted files before claiming complete coverage.

<!-- COVERAGE_LEDGER -->

| Area | Files | Lines |
| --- | ---: | ---: |
| .github | 2 | 125 |
| Root build/config | 12 | 555 |
| scripts | 30 | 3,607 |
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
| **Total** | **486** | **63,157** |

| File | Lines | Review status |
| --- | ---: | --- |
| `.github/workflows/ci.yml` | 97 | Pending |
| `.github/workflows/qodana.yml` | 28 | Pending |
| `Directory.Build.props` | 9 | Pending |
| `Directory.Packages.props` | 64 | Pending |
| `Dockerfile` | 50 | Pending |
| `Liner Notes.csproj` | 39 | Pending |
| `Liner Notes.sln` | 197 | Pending |
| `Liner Notes.slnLaunch` | 11 | Pending |
| `Program.cs` | 3 | Pending |
| `compose.ide.yaml` | 6 | Pending |
| `compose.yaml` | 132 | Pending |
| `dotnet-tools.json` | 13 | Pending |
| `global.json` | 7 | Pending |
| `qodana.yaml` | 24 | Pending |
| `scripts/acquisition_guard.py` | 97 | Pending |
| `scripts/catalog/catalog_probe.py` | 471 | Pending |
| `scripts/catalog/test_catalog_probe.py` | 175 | Pending |
| `scripts/checks/phase9_compose.py` | 215 | Pending |
| `scripts/checks/phase9_test_results.py` | 55 | Pending |
| `scripts/checks/renewal_docs.py` | 13 | Pending |
| `scripts/checks/secret_defaults.py` | 44 | Pending |
| `scripts/checks/test_phase9_helpers.py` | 77 | Pending |
| `scripts/git-qa-reviewer.py` | 26 | Pending |
| `scripts/git-security-reviewer.py` | 111 | Pending |
| `scripts/guest_discovery_demo.cs` | 83 | Pending |
| `scripts/listenbrainz_smoke.cs` | 39 | Pending |
| `scripts/phase4_pilot.py` | 431 | Pending |
| `scripts/phase4_replay.cs` | 38 | Pending |
| `scripts/phase4_weights.json` | 9 | Pending |
| `scripts/phase7_cleanup_local_email.cs` | 38 | Pending |
| `scripts/phase7_local_email.cs` | 74 | Pending |
| `scripts/phase8_repair_registration_slot.cs` | 57 | Pending |
| `scripts/phase8_schedule_fixture.cs` | 50 | Pending |
| `scripts/phase9_init_local.py` | 34 | Pending |
| `scripts/record_lastfm.py` | 497 | Pending |
| `scripts/run-schemathesis.sh` | 7 | Pending |
| `scripts/run-zap-scan.sh` | 5 | Pending |
| `scripts/run_live.sh` | 16 | Pending |
| `scripts/run_manual_qa.sh` | 47 | Pending |
| `scripts/run_phase7_local_qa.sh` | 98 | Pending |
| `scripts/setup_local_live.cs` | 126 | Pending |
| `scripts/test_phase2_recorder.py` | 376 | Pending |
| `scripts/test_phase4_pilot.py` | 243 | Pending |
| `scripts/validate_manual_qa_connection.cs` | 55 | Pending |
| `src/Application/Application.csproj` | 19 | Read 1–19; reconciliation pending |
| `src/Application/Common/Behaviors/ValidationBehavior.cs` | 47 | Read 1–47; reconciliation pending |
| `src/Application/Common/Exceptions/NotFoundException.cs` | 16 | Read 1–16; reconciliation pending |
| `src/Application/Common/Exceptions/ValidationException.cs` | 36 | Read 1–36; reconciliation pending |
| `src/Application/Common/Interfaces/Catalog/ICatalogMaintenance.cs` | 7 | Read 1–7; reconciliation pending |
| `src/Application/Common/Interfaces/Catalog/ICatalogStore.cs` | 63 | Read 1–63; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/ICoordinatedGenerationStorage.cs` | 19 | Read 1–19; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryBatchStore.cs` | 19 | Read 1–19; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryJobStore.cs` | 21 | Read 1–21; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryProfileReader.cs` | 8 | Read 1–8; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryReader.cs` | 7 | Read 1–7; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryTasteStore.cs` | 8 | Read 1–8; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/IGuestProfileStore.cs` | 25 | Read 1–25; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/IManagedStorageIntegrity.cs` | 3 | Read 1–3; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/IPublicEvidenceStore.cs` | 18 | Read 1–18; reconciliation pending |
| `src/Application/Common/Interfaces/Discovery/IRecentRecommendations.cs` | 9 | Read 1–9; reconciliation pending |
| `src/Application/Common/Interfaces/IAccountProfileReader.cs` | 9 | Read 1–9; reconciliation pending |
| `src/Application/Common/Interfaces/ILocalDigestCaptureStore.cs` | 20 | Read 1–20; reconciliation pending |
| `src/Application/Common/Interfaces/ILocalEmail.cs` | 44 | Read 1–44; reconciliation pending |
| `src/Application/Common/Interfaces/ILocalEmailCapture.cs` | 37 | Read 1–37; reconciliation pending |
| `src/Application/Common/Interfaces/IUnitOfWork.cs` | 10 | Read 1–10; reconciliation pending |
| `src/Application/Common/Interfaces/IWeeklyScheduleStore.cs` | 28 | Read 1–28; reconciliation pending |
| `src/Application/Common/Interfaces/Recommendation/ICandidateHydrator.cs` | 29 | Read 1–29; reconciliation pending |
| `src/Application/Common/Interfaces/Recommendation/IDigestGenerationStore.cs` | 56 | Read 1–56; reconciliation pending |
| `src/Application/Common/Interfaces/Recommendation/IDiscoveryPreparation.cs` | 28 | Read 1–28; reconciliation pending |
| `src/Application/Common/Interfaces/Recommendation/IRecommendationProviderResolver.cs` | 10 | Read 1–10; reconciliation pending |
| `src/Application/Common/Interfaces/Recommendation/IRecommendationSource.cs` | 39 | Read 1–39; reconciliation pending |
| `src/Application/Common/Interfaces/Recommendation/IRecordingRecommendationSource.cs` | 13 | Read 1–13; reconciliation pending |
| `src/Application/Common/Interfaces/Repositories/IAsyncRepository.cs` | 39 | Read 1–39; reconciliation pending |
| `src/Application/Common/Interfaces/Repositories/IListenerProfileRepository.cs` | 7 | Read 1–7; reconciliation pending |
| `src/Application/Common/Interfaces/Repositories/ITasteSignalRepository.cs` | 12 | Read 1–12; reconciliation pending |
| `src/Application/Common/Interfaces/Repositories/ITrackRepository.cs` | 11 | Read 1–11; reconciliation pending |
| `src/Application/Common/Interfaces/Repositories/IUserRepository.cs` | 13 | Read 1–13; reconciliation pending |
| `src/Application/Common/Interfaces/Repositories/IWeeklyDigestRepository.cs` | 17 | Read 1–17; reconciliation pending |
| `src/Application/Common/Mappings/MappingExtensions.cs` | 95 | Read 1–95; reconciliation pending |
| `src/Application/Common/Models/PagedResult.cs` | 16 | Read 1–16; reconciliation pending |
| `src/Application/Common/Models/Recommendation/GenerationConfiguration.cs` | 60 | Read 1–60; reconciliation pending |
| `src/Application/Common/Models/Recommendation/GenerationEvidenceMapping.cs` | 28 | Read 1–28; reconciliation pending |
| `src/Application/Common/Models/Recommendation/IngestionEvidence.cs` | 76 | Read 1–76; reconciliation pending |
| `src/Application/Common/Models/Recommendation/RawCandidateTrack.cs` | 41 | Read 1–41; reconciliation pending |
| `src/Application/DTOs/Digests/ScoreBreakdownDto.cs` | 28 | Read 1–28; reconciliation pending |
| `src/Application/DTOs/Digests/TrackDto.cs` | 16 | Read 1–16; reconciliation pending |
| `src/Application/DTOs/Digests/WeeklyDigestDto.cs` | 26 | Read 1–26; reconciliation pending |
| `src/Application/DTOs/Digests/WeeklyRecommendationDto.cs` | 28 | Read 1–28; reconciliation pending |
| `src/Application/DTOs/Export/AccountExportDto.cs` | 19 | Read 1–19; reconciliation pending |
| `src/Application/DTOs/Export/DiscoveryExportDto.cs` | 26 | Read 1–26; reconciliation pending |
| `src/Application/DTOs/Export/UserDataExportDto.cs` | 38 | Read 1–38; reconciliation pending |
| `src/Application/DTOs/Subscribers/SubscriberDto.cs` | 23 | Read 1–23; reconciliation pending |
| `src/Application/DTOs/Taste/SeedItemDto.cs` | 17 | Read 1–17; reconciliation pending |
| `src/Application/DTOs/Taste/TasteSignalDto.cs` | 27 | Read 1–27; reconciliation pending |
| `src/Application/DependencyInjection.cs` | 39 | Read 1–39; reconciliation pending |
| `src/Application/Features/Catalog/CatalogLookupJobs.cs` | 53 | Read 1–53; reconciliation pending |
| `src/Application/Features/Catalog/ImportCatalog.cs` | 75 | Read 1–75; reconciliation pending |
| `src/Application/Features/Catalog/LookupCatalog.cs` | 31 | Read 1–31; reconciliation pending |
| `src/Application/Features/Catalog/RunCatalogLookup.cs` | 70 | Read 1–70; reconciliation pending |
| `src/Application/Features/Catalog/SearchCatalog.cs` | 13 | Read 1–13; reconciliation pending |
| `src/Application/Features/Digests/Commands/CaptureLocalDigest/CaptureLocalDigestCommand.cs` | 58 | Read 1–58; reconciliation pending |
| `src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommand.cs` | 10 | Read 1–10; reconciliation pending |
| `src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs` | 190 | Read 1–190; reconciliation pending |
| `src/Application/Features/Digests/Commands/RecordFeedback/RecordRecommendationFeedbackCommand.cs` | 14 | Read 1–14; reconciliation pending |
| `src/Application/Features/Digests/Commands/RecordFeedback/RecordRecommendationFeedbackCommandHandler.cs` | 137 | Read 1–137; reconciliation pending |
| `src/Application/Features/Digests/Commands/RecordFeedback/RecordRecommendationFeedbackCommandValidator.cs` | 34 | Read 1–34; reconciliation pending |
| `src/Application/Features/Digests/Commands/RunScheduledTick/RunScheduledTickCommand.cs` | 81 | Read 1–81; reconciliation pending |
| `src/Application/Features/Digests/Queries/GetLatestDigest/GetLatestDigestQuery.cs` | 9 | Read 1–9; reconciliation pending |
| `src/Application/Features/Digests/Queries/GetLatestDigest/GetLatestDigestQueryHandler.cs` | 29 | Read 1–29; reconciliation pending |
| `src/Application/Features/Discovery/CleanupPublicDiscovery.cs` | 15 | Read 1–15; reconciliation pending |
| `src/Application/Features/Discovery/DiscoveryRuntimeOptions.cs` | 21 | Read 1–21; reconciliation pending |
| `src/Application/Features/Discovery/DiscoveryViews.cs` | 22 | Read 1–22; reconciliation pending |
| `src/Application/Features/Discovery/GuestLifecycle.cs` | 52 | Read 1–52; reconciliation pending |
| `src/Application/Features/Discovery/MaintainDiscoveryStorage.cs` | 13 | Read 1–13; reconciliation pending |
| `src/Application/Features/Discovery/MaterializeDiscoveryTaste.cs` | 146 | Read 1–146; reconciliation pending |
| `src/Application/Features/Discovery/PrepareDiscoveryBatch.cs` | 150 | Read 1–150; reconciliation pending |
| `src/Application/Features/Discovery/RecordDiscoveryFeedback.cs` | 20 | Read 1–20; reconciliation pending |
| `src/Application/Features/Discovery/RunDiscoveryJob.cs` | 30 | Read 1–30; reconciliation pending |
| `src/Application/Features/Discovery/SaveTasteOnboarding.cs` | 33 | Read 1–33; reconciliation pending |
| `src/Application/Features/Discovery/SelectDiscoveryTaste.cs` | 79 | Read 1–79; reconciliation pending |
| `src/Application/Features/Discovery/SubmitDiscovery.cs` | 65 | Read 1–65; reconciliation pending |
| `src/Application/Features/Discovery/TasteVocabulary.cs` | 51 | Read 1–51; reconciliation pending |
| `src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQuery.cs` | 9 | Read 1–9; reconciliation pending |
| `src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQueryHandler.cs` | 96 | Read 1–96; reconciliation pending |
| `src/Application/Features/Subscribers/Commands/DeleteUserAccount/DeleteUserAccountCommand.cs` | 8 | Read 1–8; reconciliation pending |
| `src/Application/Features/Subscribers/Commands/DeleteUserAccount/DeleteUserAccountCommandHandler.cs` | 34 | Read 1–34; reconciliation pending |
| `src/Application/Features/Subscribers/Commands/RegisterSubscriber/RegisterSubscriberCommand.cs` | 17 | Read 1–17; reconciliation pending |
| `src/Application/Features/Subscribers/Commands/RegisterSubscriber/RegisterSubscriberCommandHandler.cs` | 64 | Read 1–64; reconciliation pending |
| `src/Application/Features/Subscribers/Commands/RegisterSubscriber/RegisterSubscriberCommandValidator.cs` | 27 | Read 1–27; reconciliation pending |
| `src/Application/Features/Subscribers/Commands/UnsubscribeUser/UnsubscribeUserCommand.cs` | 13 | Read 1–13; reconciliation pending |
| `src/Application/Features/Subscribers/Queries/GetSubscriberProfile/GetSubscriberProfileQuery.cs` | 9 | Read 1–9; reconciliation pending |
| `src/Application/Features/Subscribers/Queries/GetSubscriberProfile/GetSubscriberProfileQueryHandler.cs` | 27 | Read 1–27; reconciliation pending |
| `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommand.cs` | 12 | Read 1–12; reconciliation pending |
| `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommandHandler.cs` | 75 | Read 1–75; reconciliation pending |
| `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommandValidator.cs` | 41 | Read 1–41; reconciliation pending |
| `src/Application/Features/Taste/Queries/GetUserTasteSignals/GetUserTasteSignalsQuery.cs` | 9 | Read 1–9; reconciliation pending |
| `src/Application/Features/Taste/Queries/GetUserTasteSignals/GetUserTasteSignalsQueryHandler.cs` | 27 | Read 1–27; reconciliation pending |
| `src/Application/Services/IAuthService.cs` | 19 | Read 1–19; reconciliation pending |
| `src/Application/Services/ICurrentUserService.cs` | 11 | Read 1–11; reconciliation pending |
| `src/Application/Services/IJwtService.cs` | 13 | Read 1–13; reconciliation pending |
| `src/Application/Services/ValidatorInterceptor.cs` | 3 | Read 1–3; reconciliation pending |
| `src/Application/packages.lock.json` | 47 | Read 1–47; reconciliation pending |
| `src/DataAccess/Core/AsyncRepository.cs` | 146 | Pending |
| `src/DataAccess/Core/UnitOfWork.cs` | 49 | Pending |
| `src/DataAccess/DapperPagedRepositoryBase.cs` | 90 | Pending |
| `src/DataAccess/DataAccess.csproj` | 31 | Pending |
| `src/DataAccess/DataContexts/DataContext.cs` | 146 | Pending |
| `src/DataAccess/DependencyInjection.cs` | 95 | Pending |
| `src/DataAccess/IdentityEntities/ApplicationUser.cs` | 19 | Pending |
| `src/DataAccess/Persistence/AccountEmailLease.cs` | 41 | Pending |
| `src/DataAccess/Persistence/Accounting/ManagedStorageIntegrity.cs` | 60 | Pending |
| `src/DataAccess/Persistence/Accounting/ManagedStorageLedger.cs` | 11 | Pending |
| `src/DataAccess/Persistence/Accounting/PublicEvidenceCacheEntry.cs` | 20 | Pending |
| `src/DataAccess/Persistence/Accounting/RecommendedIdentity.cs` | 8 | Pending |
| `src/DataAccess/Persistence/AppDbContext.cs` | 26 | Pending |
| `src/DataAccess/Persistence/Configurations/AlbumConfiguration.cs` | 36 | Pending |
| `src/DataAccess/Persistence/Configurations/ArtistConfiguration.cs` | 29 | Pending |
| `src/DataAccess/Persistence/Configurations/CatalogSearchConfiguration.cs` | 75 | Pending |
| `src/DataAccess/Persistence/Configurations/DiscoveryConfiguration.cs` | 80 | Pending |
| `src/DataAccess/Persistence/Configurations/ManagedStorageLedgerConfiguration.cs` | 18 | Pending |
| `src/DataAccess/Persistence/Configurations/PublicEvidenceCacheConfiguration.cs` | 25 | Pending |
| `src/DataAccess/Persistence/Configurations/RecentRecommendationConfiguration.cs` | 19 | Pending |
| `src/DataAccess/Persistence/Configurations/RecommendedIdentityConfiguration.cs` | 15 | Pending |
| `src/DataAccess/Persistence/Configurations/TasteSignalConfiguration.cs` | 61 | Pending |
| `src/DataAccess/Persistence/Configurations/TrackConfiguration.cs` | 51 | Pending |
| `src/DataAccess/Persistence/Configurations/UserConfiguration.cs` | 71 | Pending |
| `src/DataAccess/Persistence/Configurations/UserMusicConnectionConfiguration.cs` | 48 | Pending |
| `src/DataAccess/Persistence/Configurations/WeeklyDigestConfiguration.cs` | 70 | Pending |
| `src/DataAccess/Persistence/Configurations/WeeklyRecommendationConfiguration.cs` | 72 | Pending |
| `src/DataAccess/Persistence/LocalUnsubscribeProtection.cs` | 39 | Pending |
| `src/DataAccess/Persistence/Migrations/20260930194436_InitialCreate.Designer.cs` | 472 | Pending |
| `src/DataAccess/Persistence/Migrations/20260930194436_InitialCreate.cs` | 329 | Pending |
| `src/DataAccess/Persistence/Migrations/20261002203611_AddRatingToWeeklyRecommendation.Designer.cs` | 822 | Pending |
| `src/DataAccess/Persistence/Migrations/20261002203611_AddRatingToWeeklyRecommendation.cs` | 528 | Pending |
| `src/DataAccess/Persistence/Migrations/20261010205812_AddEmailUnsubscribedAtUtc.Designer.cs` | 825 | Pending |
| `src/DataAccess/Persistence/Migrations/20261010205812_AddEmailUnsubscribedAtUtc.cs` | 29 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016092843_AddListenerProfilesAndDiscoveryBatches.Designer.cs` | 1,044 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016092843_AddListenerProfilesAndDiscoveryBatches.cs` | 212 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016100239_GeneralizeDiscoveryOwnership.Designer.cs` | 1,083 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016100239_GeneralizeDiscoveryOwnership.cs` | 260 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016101423_AddGuestSessions.Designer.cs` | 1,113 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016101423_AddGuestSessions.cs` | 60 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016102144_RequireExplicitWeeklyEmailOptIn.Designer.cs` | 1,118 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016102144_RequireExplicitWeeklyEmailOptIn.cs` | 29 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016130431_AddLocalDiscoveryCatalog.Designer.cs` | 1,397 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016130431_AddLocalDiscoveryCatalog.cs` | 246 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016133449_AddCanonicalTasteSelections.Designer.cs` | 1,406 | Pending |
| `src/DataAccess/Persistence/Migrations/20261016133449_AddCanonicalTasteSelections.cs` | 64 | Pending |
| `src/DataAccess/Persistence/Migrations/20261017124050_AddManagedStorageLedger.Designer.cs` | 1,431 | Pending |
| `src/DataAccess/Persistence/Migrations/20261017124050_AddManagedStorageLedger.cs` | 88 | Pending |
| `src/DataAccess/Persistence/Migrations/20261017130938_AddSharedDiscoveryEvidenceAndHistory.Designer.cs` | 1,527 | Pending |
| `src/DataAccess/Persistence/Migrations/20261017130938_AddSharedDiscoveryEvidenceAndHistory.cs` | 204 | Pending |
| `src/DataAccess/Persistence/Migrations/20261018195528_IndexCatalogExpiryMaintenance.Designer.cs` | 1,529 | Pending |
| `src/DataAccess/Persistence/Migrations/20261018195528_IndexCatalogExpiryMaintenance.cs` | 27 | Pending |
| `src/DataAccess/Persistence/Migrations/20261019083146_AddOptionalTasteOnboarding.Designer.cs` | 1,575 | Pending |
| `src/DataAccess/Persistence/Migrations/20261019083146_AddOptionalTasteOnboarding.cs` | 190 | Pending |
| `src/DataAccess/Persistence/Migrations/20261019084129_StoreRecordingReleaseHints.Designer.cs` | 1,581 | Pending |
| `src/DataAccess/Persistence/Migrations/20261019084129_StoreRecordingReleaseHints.cs` | 110 | Pending |
| `src/DataAccess/Persistence/Migrations/AppDbContextModelSnapshot.cs` | 1,578 | Pending |
| `src/DataAccess/Persistence/Repositories/AccountProfileReader.cs` | 20 | Pending |
| `src/DataAccess/Persistence/Repositories/CatalogImportStore.cs` | 131 | Pending |
| `src/DataAccess/Persistence/Repositories/CatalogReader.cs` | 97 | Read 1–97; reconciliation pending |
| `src/DataAccess/Persistence/Repositories/DigestGenerationStore.cs` | 167 | Read 1–167; reconciliation pending |
| `src/DataAccess/Persistence/Repositories/DiscoveryBatchStore.cs` | 140 | Read 1–140; reconciliation pending |
| `src/DataAccess/Persistence/Repositories/DiscoveryJobStore.cs` | 128 | Pending |
| `src/DataAccess/Persistence/Repositories/DiscoveryProfileReader.cs` | 61 | Pending |
| `src/DataAccess/Persistence/Repositories/DiscoveryReader.cs` | 50 | Pending |
| `src/DataAccess/Persistence/Repositories/DiscoveryTasteStore.cs` | 46 | Pending |
| `src/DataAccess/Persistence/Repositories/DiscoveryWriteCoordinator.cs` | 99 | Read 1–99; reconciliation pending |
| `src/DataAccess/Persistence/Repositories/GuestProfileStore.cs` | 119 | Pending |
| `src/DataAccess/Persistence/Repositories/ListenerProfileRepository.cs` | 25 | Pending |
| `src/DataAccess/Persistence/Repositories/LocalDigestCaptureStore.cs` | 82 | Pending |
| `src/DataAccess/Persistence/Repositories/PublicEvidenceStore.cs` | 100 | Pending |
| `src/DataAccess/Persistence/Repositories/RecentRecommendations.cs` | 24 | Pending |
| `src/DataAccess/Persistence/Repositories/TasteSignalRepository.cs` | 59 | Pending |
| `src/DataAccess/Persistence/Repositories/TrackRepository.cs` | 43 | Pending |
| `src/DataAccess/Persistence/Repositories/UnsubscribeStore.cs` | 26 | Pending |
| `src/DataAccess/Persistence/Repositories/UserRepository.cs` | 61 | Pending |
| `src/DataAccess/Persistence/Repositories/WeeklyDigestRepository.cs` | 90 | Pending |
| `src/DataAccess/Persistence/Repositories/WeeklyScheduleStore.cs` | 174 | Pending |
| `src/DataAccess/packages.lock.json` | 317 | Pending |
| `src/Domain/Catalog/Album.cs` | 42 | Read 1–42; reconciliation pending |
| `src/Domain/Catalog/Artist.cs` | 42 | Read 1–42; reconciliation pending |
| `src/Domain/Catalog/CatalogPublication.cs` | 65 | Read 1–65; reconciliation pending |
| `src/Domain/Catalog/CatalogRecord.cs` | 63 | Read 1–63; reconciliation pending |
| `src/Domain/Catalog/CatalogSearch.cs` | 62 | Read 1–62; reconciliation pending |
| `src/Domain/Catalog/Tag.cs` | 49 | Read 1–49; reconciliation pending |
| `src/Domain/Catalog/Track.cs` | 73 | Read 1–73; reconciliation pending |
| `src/Domain/Catalog/WeightedTag.cs` | 36 | Read 1–36; reconciliation pending |
| `src/Domain/Catalog/WeightedTagVector.cs` | 190 | Read 1–190; reconciliation pending |
| `src/Domain/Common/AuditableEntity.cs` | 15 | Read 1–15; reconciliation pending |
| `src/Domain/Common/BaseEntity.cs` | 19 | Read 1–19; reconciliation pending |
| `src/Domain/Common/IAuditableEntity.cs` | 15 | Read 1–15; reconciliation pending |
| `src/Domain/Digest/IsoWeek.cs` | 87 | Read 1–87; reconciliation pending |
| `src/Domain/Digest/User.cs` | 110 | Read 1–110; reconciliation pending |
| `src/Domain/Digest/UserMusicConnection.cs` | 76 | Read 1–76; reconciliation pending |
| `src/Domain/Digest/WeeklyDigest.cs` | 128 | Read 1–128; reconciliation pending |
| `src/Domain/Digest/WeeklyRecommendation.cs` | 91 | Read 1–91; reconciliation pending |
| `src/Domain/Digest/WeeklySchedulePolicy.cs` | 52 | Read 1–52; reconciliation pending |
| `src/Domain/Discovery/DiscoveryBatch.cs` | 47 | Read 1–47; reconciliation pending |
| `src/Domain/Discovery/DiscoveryJob.cs` | 127 | Read 1–127; reconciliation pending |
| `src/Domain/Discovery/DiscoveryRules.cs` | 17 | Read 1–17; reconciliation pending |
| `src/Domain/Discovery/DiscoverySelectionPolicy.cs` | 46 | Read 1–46; reconciliation pending |
| `src/Domain/Discovery/GuestSession.cs` | 18 | Read 1–18; reconciliation pending |
| `src/Domain/Discovery/ListenerProfile.cs` | 79 | Read 1–79; reconciliation pending |
| `src/Domain/Discovery/RecentRecommendation.cs` | 26 | Read 1–26; reconciliation pending |
| `src/Domain/Discovery/TasteOnboarding.cs` | 30 | Read 1–30; reconciliation pending |
| `src/Domain/Domain.csproj` | 8 | Read 1–8; reconciliation pending |
| `src/Domain/Enums/DigestDeliveryDay.cs` | 15 | Read 1–15; reconciliation pending |
| `src/Domain/Enums/DigestStatus.cs` | 13 | Read 1–13; reconciliation pending |
| `src/Domain/Enums/MusicServiceType.cs` | 11 | Read 1–11; reconciliation pending |
| `src/Domain/Enums/TasteSignalSource.cs` | 18 | Read 1–18; reconciliation pending |
| `src/Domain/Enums/TasteTargetType.cs` | 12 | Read 1–12; reconciliation pending |
| `src/Domain/Enums/UserFeedback.cs` | 15 | Read 1–15; reconciliation pending |
| `src/Domain/Scoring/BaselineAScorer.cs` | 185 | Read 1–185; reconciliation pending |
| `src/Domain/Scoring/CandidateTrack.cs` | 54 | Read 1–54; reconciliation pending |
| `src/Domain/Scoring/MatchedTagContribution.cs` | 15 | Read 1–15; reconciliation pending |
| `src/Domain/Scoring/RecommendationScorer.cs` | 122 | Read 1–122; reconciliation pending |
| `src/Domain/Scoring/ScoreBreakdown.cs` | 122 | Read 1–122; reconciliation pending |
| `src/Domain/Scoring/ScoreSnapshot.cs` | 96 | Read 1–96; reconciliation pending |
| `src/Domain/Scoring/ScoredCandidate.cs` | 13 | Read 1–13; reconciliation pending |
| `src/Domain/Scoring/ScoringParameters.cs` | 38 | Read 1–38; reconciliation pending |
| `src/Domain/Taste/DiscoveryTasteMaterializer.cs` | 42 | Read 1–42; reconciliation pending |
| `src/Domain/Taste/EffectiveTasteInputs.cs` | 75 | Read 1–75; reconciliation pending |
| `src/Domain/Taste/TasteSignal.cs` | 102 | Read 1–102; reconciliation pending |
| `src/Domain/Taste/TasteVectorMaterializer.cs` | 29 | Read 1–29; reconciliation pending |
| `src/Domain/Taste/UserTasteProfile.cs` | 72 | Read 1–72; reconciliation pending |
| `src/Domain/packages.lock.json` | 6 | Read 1–6; reconciliation pending |
| `src/Infrastructure/Caching/DiscoveryMemoryCaches.cs` | 43 | Pending |
| `src/Infrastructure/Catalog/CatalogArtifactReader.cs` | 95 | Pending |
| `src/Infrastructure/Catalog/MusicBrainzCatalogLookupSource.cs` | 72 | Pending |
| `src/Infrastructure/Catalog/PublicRecordingCatalog.cs` | 44 | Pending |
| `src/Infrastructure/DependencyInjection.cs` | 142 | Pending |
| `src/Infrastructure/Discovery/PersistentDiscoveryPreparation.cs` | 24 | Pending |
| `src/Infrastructure/Discovery/PublicEvidenceCodec.cs` | 75 | Pending |
| `src/Infrastructure/Discovery/SharedCatalogLookup.cs` | 36 | Pending |
| `src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs` | 128 | Pending |
| `src/Infrastructure/Discovery/SharedPublicEvidence.cs` | 73 | Pending |
| `src/Infrastructure/Discovery/SharedSeedPreparation.cs` | 76 | Pending |
| `src/Infrastructure/Email/DigestEmailRenderer.cs` | 105 | Pending |
| `src/Infrastructure/Email/LocalEmailArchive.cs` | 108 | Pending |
| `src/Infrastructure/Email/LocalEmailLegacyConversion.cs` | 134 | Pending |
| `src/Infrastructure/Email/LocalEmailMessageSerializer.cs` | 182 | Pending |
| `src/Infrastructure/Email/LocalEmailOptions.cs` | 42 | Pending |
| `src/Infrastructure/Email/LocalEmailPaths.cs` | 92 | Pending |
| `src/Infrastructure/Email/LocalEmailSink.cs` | 112 | Pending |
| `src/Infrastructure/Email/UnsubscribeTokenService.cs` | 22 | Pending |
| `src/Infrastructure/Infrastructure.csproj` | 22 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/BatchTagCachingLastFmClient.cs` | 33 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/Fixtures/LastFmFixtureProvider.cs` | 279 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/ILastFmApiClient.cs` | 42 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmApiClient.cs` | 115 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmCandidateHydrator.cs` | 51 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmEvidenceParser.cs` | 129 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmOptions.cs` | 78 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmRateLimiter.cs` | 83 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmRecommendationSource.cs` | 112 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmTagEvidence.cs` | 45 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/LocalRecordedInputLoader.cs` | 156 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/Models/LastFmResponse.cs` | 20 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/Models/LastFmResponses.cs` | 37 | Pending |
| `src/Infrastructure/RecommendationSources/LastFm/RecordedLastFmApiClient.cs` | 67 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/FixtureListenBrainzApiClient.cs` | 96 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzApiClient.cs` | 96 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzCanonicalSeeds.cs` | 52 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzContracts.cs` | 122 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzEvidenceParser.cs` | 117 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzPublicCatalog.cs` | 53 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecommendationProvider.cs` | 157 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecordingSeeds.cs` | 44 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/MetaBrainzRequestGate.cs` | 61 | Pending |
| `src/Infrastructure/RecommendationSources/ListenBrainz/RecordedListenBrainzApiClient.cs` | 44 | Pending |
| `src/Infrastructure/RecommendationSources/RecommendationProviderResolver.cs` | 15 | Pending |
| `src/Infrastructure/Storage/CoordinatedGenerationStorage.cs` | 136 | Read 1–136; reconciliation pending |
| `src/Infrastructure/Storage/LocalGenerationStorage.cs` | 265 | Read 1–265; reconciliation pending |
| `src/Infrastructure/packages.lock.json` | 93 | Pending |
| `src/Presentation/Background/DiscoveryInventoryService.cs` | 45 | Pending |
| `src/Presentation/Background/DiscoveryJobService.cs` | 115 | Read 1–115; reconciliation pending |
| `src/Presentation/Controllers/ApiControllerBase.cs` | 23 | Pending |
| `src/Presentation/Controllers/AuthController.cs` | 270 | Pending |
| `src/Presentation/Controllers/DigestsController.cs` | 79 | Pending |
| `src/Presentation/Controllers/ExportController.cs` | 55 | Pending |
| `src/Presentation/Controllers/SubscribersController.cs` | 100 | Pending |
| `src/Presentation/Controllers/TasteController.cs` | 52 | Pending |
| `src/Presentation/Controllers/UnsubscribeController.cs` | 42 | Pending |
| `src/Presentation/DependencyInjection.cs` | 168 | Pending |
| `src/Presentation/Development/ManualTestEndpoints.cs` | 106 | Pending |
| `src/Presentation/Discovery/CatalogController.cs` | 42 | Pending |
| `src/Presentation/Discovery/DiscoveryController.cs` | 76 | Pending |
| `src/Presentation/Discovery/GuestController.cs` | 96 | Pending |
| `src/Presentation/Discovery/ListenerContext.cs` | 100 | Pending |
| `src/Presentation/Discovery/RecentRecommendationsController.cs` | 19 | Pending |
| `src/Presentation/Discovery/TasteOnboardingController.cs` | 29 | Pending |
| `src/Presentation/Filters/ApiExceptionFilterAttribute.cs` | 198 | Pending |
| `src/Presentation/Filters/DiscoveryWriteFilter.cs` | 29 | Pending |
| `src/Presentation/Models/AuthModels.cs` | 32 | Pending |
| `src/Presentation/Options/JwtOptions.cs` | 15 | Pending |
| `src/Presentation/Presentation.csproj` | 26 | Pending |
| `src/Presentation/Program.cs` | 173 | Pending |
| `src/Presentation/Properties/launchSettings.json` | 37 | Pending |
| `src/Presentation/Services/AuthService.cs` | 212 | Pending |
| `src/Presentation/Services/CurrentUserService.cs` | 51 | Pending |
| `src/Presentation/Services/JwtService.cs` | 105 | Pending |
| `src/Presentation/appsettings.Development.json` | 10 | Pending |
| `src/Presentation/appsettings.json` | 33 | Pending |
| `src/Presentation/packages.lock.json` | 524 | Pending |
| `src/Presentation/wwwroot/app.js` | 617 | Pending |
| `src/Presentation/wwwroot/discovery.js` | 521 | Read 1–521; reconciliation pending |
| `src/Presentation/wwwroot/index.html` | 179 | Pending |
| `src/Presentation/wwwroot/manual-test/index.html` | 218 | Pending |
| `src/Presentation/wwwroot/manual-test/manual-test.css` | 229 | Pending |
| `src/Presentation/wwwroot/manual-test/manual-test.js` | 574 | Pending |
| `src/Presentation/wwwroot/styles.css` | 1,021 | Pending |
| `src/Worker/LocalDigestWorkerRunner.cs` | 54 | Pending |
| `src/Worker/LocalScheduleOptions.cs` | 29 | Pending |
| `src/Worker/LocalScheduledWorker.cs` | 45 | Pending |
| `src/Worker/LocalWorkerOptions.cs` | 23 | Pending |
| `src/Worker/Program.cs` | 55 | Pending |
| `src/Worker/Worker.csproj` | 20 | Pending |
| `src/Worker/appsettings.Development.json` | 1 | Pending |
| `src/Worker/packages.lock.json` | 516 | Pending |
| `tests/Application.Tests/Application.Tests.csproj` | 26 | Pending |
| `tests/Application.Tests/Common/ApplicationBoundaryTests.cs` | 21 | Pending |
| `tests/Application.Tests/Common/Behaviors/ValidationBehaviorTests.cs` | 82 | Pending |
| `tests/Application.Tests/Common/Behaviors/ValidationCancellationTests.cs` | 34 | Pending |
| `tests/Application.Tests/Common/EvaluationMetrics.cs` | 145 | Pending |
| `tests/Application.Tests/Common/EvaluationMetricsTests.cs` | 121 | Pending |
| `tests/Application.Tests/Common/Exceptions/ExceptionTests.cs` | 51 | Pending |
| `tests/Application.Tests/Common/FixtureEvaluationHarness.cs` | 167 | Pending |
| `tests/Application.Tests/Common/FixtureEvaluationHarnessTests.cs` | 125 | Pending |
| `tests/Application.Tests/Common/Mappings/MappingTests.cs` | 111 | Pending |
| `tests/Application.Tests/Common/Phase2ProfileMembership.cs` | 50 | Pending |
| `tests/Application.Tests/Common/PilotEvaluation.cs` | 140 | Pending |
| `tests/Application.Tests/Common/PilotScoring.cs` | 145 | Pending |
| `tests/Application.Tests/Common/PilotScoringTests.cs` | 157 | Pending |
| `tests/Application.Tests/Common/ProfileLockTests.cs` | 85 | Pending |
| `tests/Application.Tests/Features/Digests/DigestAndFeedbackTests.cs` | 238 | Pending |
| `tests/Application.Tests/Features/Digests/GenerateDigestTests.cs` | 314 | Pending |
| `tests/Application.Tests/Features/Discovery/DiscoveryRuntimeOptionsTests.cs` | 21 | Pending |
| `tests/Application.Tests/Features/Discovery/DiscoveryTasteTests.cs` | 153 | Pending |
| `tests/Application.Tests/Features/Discovery/GuestActivityTests.cs` | 58 | Pending |
| `tests/Application.Tests/Features/Discovery/PrepareDiscoveryBatchTests.cs` | 86 | Pending |
| `tests/Application.Tests/Features/Discovery/TasteVocabularyTests.cs` | 31 | Pending |
| `tests/Application.Tests/Features/Export/GetUserDataExportTests.cs` | 219 | Pending |
| `tests/Application.Tests/Features/RunScheduledTickTests.cs` | 117 | Pending |
| `tests/Application.Tests/Features/Subscribers/RegisterSubscriberTests.cs` | 131 | Pending |
| `tests/Application.Tests/Features/Subscribers/SubscriberQueryAndCommandTests.cs` | 80 | Pending |
| `tests/Application.Tests/Features/Taste/GetUserTasteSignalsTests.cs` | 29 | Pending |
| `tests/Application.Tests/Features/Taste/SeedTasteProfileTests.cs` | 161 | Pending |
| `tests/Application.Tests/packages.lock.json` | 251 | Pending |
| `tests/DataAccess.Tests/AppDbContextModelTests.cs` | 136 | Pending |
| `tests/DataAccess.Tests/ArchitectureTests.cs` | 31 | Pending |
| `tests/DataAccess.Tests/AsyncRepositoryTests.cs` | 133 | Pending |
| `tests/DataAccess.Tests/AuditTimestampTests.cs` | 68 | Pending |
| `tests/DataAccess.Tests/CatalogPostgresTests.cs` | 287 | Pending |
| `tests/DataAccess.Tests/ComprehensiveRepositoryTests.cs` | 135 | Pending |
| `tests/DataAccess.Tests/DataAccess.Tests.csproj` | 28 | Pending |
| `tests/DataAccess.Tests/DataContextAuditAndSoftDeleteTests.cs` | 117 | Pending |
| `tests/DataAccess.Tests/DiscoveryCoordinationPostgresTests.cs` | 706 | Pending |
| `tests/DataAccess.Tests/DiscoveryMigrationTests.cs` | 383 | Pending |
| `tests/DataAccess.Tests/LocalScheduleOptionsTests.cs` | 59 | Pending |
| `tests/DataAccess.Tests/LocalScheduledWorkerTests.cs` | 57 | Pending |
| `tests/DataAccess.Tests/LocalUnsubscribePostgresTests.cs` | 92 | Pending |
| `tests/DataAccess.Tests/LocalWorkerOptionsTests.cs` | 34 | Pending |
| `tests/DataAccess.Tests/LocalWorkerPostgresTests.cs` | 245 | Pending |
| `tests/DataAccess.Tests/OptionalTasteOnboardingPostgresTests.cs` | 118 | Pending |
| `tests/DataAccess.Tests/Phase6PostgresTests.cs` | 435 | Pending |
| `tests/DataAccess.Tests/Phase8PrivacyInventoryTests.cs` | 105 | Pending |
| `tests/DataAccess.Tests/Phase8ScheduledWorkerPostgresTests.cs` | 588 | Pending |
| `tests/DataAccess.Tests/PopulatedLegacyMigrationTests.cs` | 89 | Pending |
| `tests/DataAccess.Tests/PrReviewRegressionPostgresTests.cs` | 89 | Pending |
| `tests/DataAccess.Tests/RecordingCatalogPostgresTests.cs` | 45 | Pending |
| `tests/DataAccess.Tests/RepositoryTests.cs` | 120 | Pending |
| `tests/DataAccess.Tests/UnitOfWorkTests.cs` | 33 | Pending |
| `tests/DataAccess.Tests/packages.lock.json` | 622 | Pending |
| `tests/Domain.Tests/Architecture/OnionArchitectureTests.cs` | 33 | Pending |
| `tests/Domain.Tests/Catalog/CatalogTextTests.cs` | 29 | Pending |
| `tests/Domain.Tests/Catalog/TrackAndArtistTests.cs` | 86 | Pending |
| `tests/Domain.Tests/Catalog/WeightedTagVectorTests.cs` | 117 | Pending |
| `tests/Domain.Tests/Digest/IsoWeekExtendedTests.cs` | 69 | Pending |
| `tests/Domain.Tests/Digest/LocalEmailLifecycleTests.cs` | 37 | Pending |
| `tests/Domain.Tests/Digest/UserAndConnectionTests.cs` | 113 | Pending |
| `tests/Domain.Tests/Digest/WeeklyDigestTests.cs` | 113 | Pending |
| `tests/Domain.Tests/Discovery/DiscoveryFoundationTests.cs` | 86 | Pending |
| `tests/Domain.Tests/Discovery/DiscoveryOwnershipTests.cs` | 49 | Pending |
| `tests/Domain.Tests/Discovery/OptionalTastePolicyTests.cs` | 118 | Pending |
| `tests/Domain.Tests/DiscoveryJobLeaseTests.cs` | 54 | Pending |
| `tests/Domain.Tests/DiscoveryTasteMaterializerTests.cs` | 70 | Pending |
| `tests/Domain.Tests/Domain.Tests.csproj` | 23 | Pending |
| `tests/Domain.Tests/Fixtures/BenchmarkSeedFixtures.cs` | 224 | Pending |
| `tests/Domain.Tests/Recommendation/BaselineAScorerTests.cs` | 99 | Pending |
| `tests/Domain.Tests/Recommendation/CandidateTrackAndParamsTests.cs` | 66 | Pending |
| `tests/Domain.Tests/Recommendation/RecommendationScorerTests.cs` | 240 | Pending |
| `tests/Domain.Tests/Taste/TasteSignalTests.cs` | 45 | Pending |
| `tests/Domain.Tests/Taste/TasteVectorMaterializerTests.cs` | 59 | Pending |
| `tests/Domain.Tests/Taste/UserTasteProfileExtendedTests.cs` | 68 | Pending |
| `tests/Domain.Tests/WeeklySchedulePolicyTests.cs` | 44 | Pending |
| `tests/Domain.Tests/packages.lock.json` | 96 | Pending |
| `tests/Infrastructure.Tests/Caching/DiscoveryMemoryCacheTests.cs` | 35 | Pending |
| `tests/Infrastructure.Tests/Caching/PersistentDiscoveryPreparationTests.cs` | 168 | Pending |
| `tests/Infrastructure.Tests/Caching/PublicEvidenceCodecTests.cs` | 50 | Pending |
| `tests/Infrastructure.Tests/Caching/SharedDiscoveryPreparationTests.cs` | 135 | Pending |
| `tests/Infrastructure.Tests/Catalog/CatalogArtifactTests.cs` | 101 | Pending |
| `tests/Infrastructure.Tests/Fakes/EvidenceFixtures.cs` | 31 | Pending |
| `tests/Infrastructure.Tests/Fakes/MockHttpMessageHandler.cs` | 43 | Pending |
| `tests/Infrastructure.Tests/GenerationStorageTests.cs` | 141 | Pending |
| `tests/Infrastructure.Tests/Infrastructure.Tests.csproj` | 35 | Pending |
| `tests/Infrastructure.Tests/ListenBrainzProviderTests.cs` | 178 | Pending |
| `tests/Infrastructure.Tests/ListenBrainzRecordedInputTests.cs` | 55 | Pending |
| `tests/Infrastructure.Tests/ListenBrainzTransportTests.cs` | 224 | Pending |
| `tests/Infrastructure.Tests/LiveRecordingCatalogTests.cs` | 63 | Pending |
| `tests/Infrastructure.Tests/LocalEmailTests.cs` | 429 | Pending |
| `tests/Infrastructure.Tests/LocalRecordedInputTests.cs` | 60 | Pending |
| `tests/Infrastructure.Tests/RecommendationProviderTests.cs` | 83 | Pending |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmApiClientTests.cs` | 221 | Pending |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmCandidateHydratorTests.cs` | 78 | Pending |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmRateLimiterTests.cs` | 51 | Pending |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmRecommendationSourceTests.cs` | 97 | Pending |
| `tests/Infrastructure.Tests/RecommendationSources/Phase3EvidenceTests.cs` | 345 | Pending |
| `tests/Infrastructure.Tests/RecommendationSources/Phase3IngestionTests.cs` | 71 | Pending |
| `tests/Infrastructure.Tests/RecommendationSources/PilotReplay.cs` | 167 | Pending |
| `tests/Infrastructure.Tests/RecommendationSources/PilotReplayTests.cs` | 164 | Pending |
| `tests/Infrastructure.Tests/RecommendationSources/RecordingDiscoveryTests.cs` | 90 | Pending |
| `tests/Infrastructure.Tests/Spikes/FounderSeedCoverageSpikeTests.cs` | 45 | Pending |
| `tests/Infrastructure.Tests/Storage/CoordinatedStorageTests.cs` | 110 | Pending |
| `tests/Infrastructure.Tests/packages.lock.json` | 345 | Pending |
| `tests/Presentation.Tests/ApiExceptionFilterAttributeTests.cs` | 121 | Pending |
| `tests/Presentation.Tests/Controllers/AuthControllerTests.cs` | 247 | Pending |
| `tests/Presentation.Tests/Controllers/DigestsControllerTests.cs` | 111 | Pending |
| `tests/Presentation.Tests/Controllers/ExportControllerTests.cs` | 108 | Pending |
| `tests/Presentation.Tests/Controllers/SubscribersControllerTests.cs` | 132 | Pending |
| `tests/Presentation.Tests/Controllers/TasteControllerTests.cs` | 75 | Pending |
| `tests/Presentation.Tests/CurrentUserServiceTests.cs` | 72 | Pending |
| `tests/Presentation.Tests/DiscoveryInventoryServiceTests.cs` | 58 | Pending |
| `tests/Presentation.Tests/DiscoveryJobServiceTests.cs` | 123 | Pending |
| `tests/Presentation.Tests/DiscoveryWriteFilterTests.cs` | 78 | Pending |
| `tests/Presentation.Tests/GuestDiscoveryApiTests.cs` | 223 | Pending |
| `tests/Presentation.Tests/JwtServiceTests.cs` | 82 | Pending |
| `tests/Presentation.Tests/ListenerContextTests.cs` | 96 | Pending |
| `tests/Presentation.Tests/ManualTestEnvironmentTests.cs` | 123 | Pending |
| `tests/Presentation.Tests/Phase1SecurityTests.cs` | 358 | Pending |
| `tests/Presentation.Tests/Phase8PrivacyApiTests.cs` | 492 | Pending |
| `tests/Presentation.Tests/Phase9SwaggerTests.cs` | 55 | Pending |
| `tests/Presentation.Tests/Presentation.Tests.csproj` | 28 | Pending |
| `tests/Presentation.Tests/UnsubscribeRouteTests.cs` | 98 | Pending |
| `tests/Presentation.Tests/packages.lock.json` | 877 | Pending |
