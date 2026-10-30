# Disposable storage and authentication measurements

This console harness measures actual coordinated login, digest persistence and exact-snapshot export against disposable PostgreSQL databases. It does not contact music providers, measure recommendation quality, use production accounts, or attach diagnostics to an existing application. It is intentionally separate from the solution and BenchmarkDotNet wrapper.

Build with the repository SDK and existing locked dependencies:

```bash
dotnet restore diagnostics/storage-benchmarks/StorageBenchmarks.csproj --locked-mode
dotnet build diagnostics/storage-benchmarks/StorageBenchmarks.csproj -c Release --no-restore
```

Supply `LINER_PHASE1_POSTGRES` privately with an administrator connection to an **owned disposable loopback PostgreSQL instance**. Never print or commit it. The harness creates a unique `liner_storage_bench_*` database and removes it in `finally`; an existing database is never adopted. Set `LINER_STORAGE_PROBE_ROOT` to a disposable directory on the filesystem being measured, with at least 2 GB available. The generated 900 MB fixture must not compete with personal/production data or unexpectedly fill a small tmpfs.

```bash
LINER_BENCH_SHAPE=900m LINER_BENCH_SAMPLES=20 \
  dotnet diagnostics/storage-benchmarks/bin/Release/net10.0/StorageBenchmarks.dll --disposable-only
```

Shapes: `empty`, `100m`, `900m`, `900m-1000`. Each run warms compilation, pooled connections and filesystem page cache, then measures 20 sequential logins, 20 persistence operations, and three waves of eight mixed operations. Sample count accepts 4–100; mixed wave count rounds upward to full waves. JSON output includes successful/failing attempts, stage durations, SQL execution counts, bytes scanned, process CPU/allocation and peak working set. SQL counts exclude transaction-control traffic and server-internal trigger work; they are not exact network round trips. Timed runs should execute sequentially without builds, tests or profilers alongside them.

For matched comparisons, copy exactly the same harness into a disposable baseline checkout, build it against that checkout's assemblies, alternate version order, retain failures, and compare the complete serialized recommendation fixtures separately. This harness's five synthetic precomputed picks do not constitute listening-quality evaluation. `measurements-20261030.json` contains sanitized local distributions and their limitations; the root [implementation record](../../STORAGE_AUTH_REDESIGN_IMPLEMENTATION.md) explains the findings.

The storage component's five-checkpoint comparison lives in `diagnostics/proposals/StorageProtocolProbe.cs`. `storage-protocol` runs all four shapes with 20 samples after warmup. `LINER_PROBE_KERNEL_LEASES=false` exercises mandatory full-read fallback. Use the same explicit disposable filesystem root for all variants.

Optional browser verification mode uses `LINER_BENCH_SERVE_URL=http://127.0.0.1:PORT` (port above 1023). It initializes only this run's disposable fixtures and invokes the real Presentation composition root with background provider jobs disabled and storage admission enabled. `LINER_BENCH_VERIFY_OPERATOR=true` first exercises the explicit stopped-writer establishment command. Browser fixture accounts exist only in this disposable database. Gracefully stop the launched process to run cleanup; after process termination, inspect and remove only its recorded owned resources. Never use this mode as a deployment configuration.
