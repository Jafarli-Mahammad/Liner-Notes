# .NET diagnostics toolkit

These Bash scripts use the repository's .NET 10 SDK and open-source .NET diagnostics tools. Run them from any directory; they resolve paths relative to the repository. Artifacts go under `diagnostics/results/`, which is gitignored. Existing results are never removed.

## Repository setup and health

The solution targets `net10.0`, uses xUnit with `Microsoft.NET.Test.Sdk`, central package management, lock files, and VSTest. Coverlet MSBuild is centrally pinned. `.NET 10` MTP support is explicitly disabled for these tests because Coverlet's MSBuild integration uses VSTest. The local tool manifest pins ReportGenerator; global tool installations are also supported. No BenchmarkDotNet project exists.

```bash
dotnet tool restore                 # optional if global ReportGenerator is available
diagnostics/health.sh
```

Health reports SDK and tool versions and Linux diagnostic-access notes. Attachment generally requires the same OS user as the target. Linux ptrace policy or container boundaries can restrict collection. SOS may require first-time setup for `dotnet-dump`.

## Coverage

```bash
diagnostics/coverage.sh
diagnostics/coverage.sh --project 'tests/Domain.Tests/Domain.Tests.csproj'
diagnostics/coverage.sh --configuration Debug --timeout 300
```

The script runs each test project separately with Coverlet line and branch coverage. Default timeout is 300 seconds per project; set `--timeout` to a positive number of seconds. Output includes per-project Cobertura files, a combined Cobertura report at `diagnostics/results/coverage-<UTC>/report/Cobertura.xml`, HTML at `.../report/index.html`, and a short `Summary.txt`. `--no-build` requires that configuration to have already been built.

## CPU profiling

```bash
diagnostics/profile.sh --pid 12345 --duration 30
diagnostics/profile.sh --pid 12345 --duration 90 --name checkout-before
diagnostics/profile.sh --launch --duration 30 -- dotnet run --project src/Presentation/Presentation.csproj
```

Attach requires an explicit PID; the script never discovers or attaches to a process automatically. Durations are whole seconds, 13600. Launch mode records from application startup and forwards child IO. Each run retains the original `.nettrace` plus a `.speedscope.json` profile. Open the latter at [speedscope.app](https://www.speedscope.app). For a CLI hotspot report, run the printed `dotnet-trace report topN -n 20 <trace>` command.

Sampling shows where CPU samples landed. It does not establish request latency or prove a regression by itself.

## Memory diagnostics

```bash
diagnostics/memory.sh monitor --pid 12345 --duration 60 --interval 2
diagnostics/memory.sh gcdump --pid 12345 --name checkout --timeout 60
diagnostics/memory.sh dump --pid 12345 --name checkout --yes
diagnostics/memory.sh analyze diagnostics/results/full-checkout.dmp --command 'dumpheap -stat'
```

`monitor` writes System.Runtime counters to a text artifact, including managed heap size, allocation rate, GC counts and process working set when exposed by the runtime. Managed heap size counts managed objects. Process memory/working set also includes native allocations, runtime/JIT, stacks and mapped pages. Allocation rate is allocation traffic, not retained growth. Suspected leaks need repeated measurements under a stable workload and evidence that objects remain rooted unexpectedly.

`gcdump` captures the managed heap and writes a type summary. It forces a full Gen 2 GC and can pause the process. A full dump may be very large, pause the target, and contain credentials, request data, or other secrets. Review authorization and storage before using `--yes`; never share or commit raw dumps or traces without sanitizing and approval. SOS commands include `dumpheap -stat`, `dumpheap -type TypeName`, `gcroot ADDRESS`, `clrstack`, and `dumpobj ADDRESS`.

PerfView is Windows-only. Fedora memory analysis here is primarily command-line based with counters, gcdump/SOS, dotnet-dump and Speedscope. dotnet-dump focuses on managed state; native-memory analysis may need platform-specific tools.

## Benchmarks

```bash
diagnostics/benchmark.sh
diagnostics/benchmark.sh --project benchmarks/LinerNotes.Benchmarks/LinerNotes.Benchmarks.csproj --list
diagnostics/benchmark.sh --project benchmarks/LinerNotes.Benchmarks/LinerNotes.Benchmarks.csproj --filter '*Scorer*'
```

There is currently no BenchmarkDotNet project. With none found, the script exits 3 and offers this optional isolated setup; it does not modify the repository:

```bash
dotnet new console -n LinerNotes.Benchmarks -o benchmarks/LinerNotes.Benchmarks
dotnet add benchmarks/LinerNotes.Benchmarks package BenchmarkDotNet
# Add a ProjectReference to only the production project being measured.
# Add a [MemoryDiagnoser] benchmark class, then run diagnostics/benchmark.sh --project <csproj>.
```

Results and raw BenchmarkDotNet reports go under `diagnostics/results/benchmarks/`. Save a baseline run before the change. Keep machine, SDK/runtime, power state, workload, inputs, filter and settings comparable. Compare the same reported statistic; do not claim an improvement or regression from unmatched runs.

## Rider on Fedora

Use Rider's **External Tools** settings so the launch actions remain user-local and compatible with different Rider versions:

1. Open **File | Settings | Tools | External Tools** (or press **Ctrl+Alt+S**, then select **Tools | External Tools**) and click **Add**.
2. Set **Program** to the script path. Rider accepts project macros; use `$ProjectFileDir$/diagnostics/coverage.sh`. The scripts are executable Bash files.
3. Set **Working directory** to `$ProjectFileDir$`. Enable **Open console for tool output**.
4. Add these entries:

| Name | Program | Arguments |
|---|---|---|
| Diagnostics: coverage | `$ProjectFileDir$/diagnostics/coverage.sh` | *(empty)* |
| Diagnostics: CPU profile | `$ProjectFileDir$/diagnostics/profile.sh` | `--pid 12345 --duration 30` |
| Diagnostics: memory monitor | `$ProjectFileDir$/diagnostics/memory.sh` | `monitor --pid 12345 --duration 60` |
| Diagnostics: benchmarks | `$ProjectFileDir$/diagnostics/benchmark.sh` | `--filter '*'` |

Replace sample PID `12345` with a PID you selected and may inspect. Add separate CPU entries for commonly used durations or names. Launch them from **Tools | External Tools**. Optional shortcuts are under **Settings | Keymap | External Tools**. Rider displays output in its tool window and artifacts remain in the results folder. These tools do not appear inside JetBrains' licensed dotTrace, dotCover, or dotMemory interfaces.

Rider's External Tools configuration is local and is not committed with this repository.

## Agent guidance

Codex reads `AGENTS.md`, Claude Code reads `CLAUDE.md`, and Antigravity CLI reads `GEMINI.md`. All point to this guide. Ordinary changes should use targeted tests, not expensive diagnostics by default. Run coverage for coverage questions, CPU profiling for CPU attribution, memory tools for allocation or retention investigations, and benchmarks for performance comparisons. Never kill production processes, attach without an explicit user-provided PID, collect sensitive dumps without authorization, or silently alter application behavior for profiling.

For before/after comparisons, use distinct names such as `feature-before` and `feature-after`, the same representative workload and environment, and report SDK/tool versions, duration and artifact paths. State observations separately from causal claims; one run does not prove an optimization.

## Exit behavior and troubleshooting

Scripts return nonzero for invalid arguments, missing tools, failed tests or collection, and absent benchmark projects (exit 3). A successful invocation confirms tool execution and artifact generation, not production health. Linux collection may fail due to user identity, disabled runtime diagnostics, namespaces or ptrace policy. See Microsoft's [.NET diagnostics documentation](https://learn.microsoft.com/dotnet/core/diagnostics/) and the [Coverlet project](https://github.com/coverlet-coverage/coverlet).
