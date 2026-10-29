# Storage design experiments

These launch-only probes support the root [design proposal](../../STORAGE_INTEGRITY_DESIGN_PROPOSAL.md). They are not referenced by any production project and do not replace storage, accounting or authentication code.

Use the repository SDK and a Release build:

```bash
dotnet restore diagnostics/proposals/StorageDesignProbe.csproj --locked-mode
dotnet build diagnostics/proposals/StorageDesignProbe.csproj -c Release --no-restore
dotnet diagnostics/proposals/bin/Release/net10.0/StorageDesignProbe.dll storage-alternatives
dotnet diagnostics/proposals/bin/Release/net10.0/StorageDesignProbe.dll auth-boundary
```

The scan experiment creates generated files in uniquely named temporary directories and deletes only those directories afterward. Allow roughly 220 GiB of logical reads across all repetitions, plus initial fixture writes. It rotates three variants, discards a warmup, measures 20 samples per shape/variant and compares complete category revisions against the current production scan primitive at every checkpoint. It also flips a byte without changing file length or final modification time. It measures allocation traffic, not retained heap size. The buffered/parallel implementation is experimental and lacks the full production validation/error/concurrency matrix.

Run on an otherwise idle host, preserve runtime/GC settings and report storage/cache conditions. The checked-in results are local observations; the initial scan run overlapped other development work and is not a target-VPS acceptance result. Production ordinary workloads and profiling runs must be measured separately.

The authentication probe creates generated credentials in memory and measures the installed Identity hasher. It never connects to a database, modifies an account, lowers production hashing settings or accepts a real password/hash. Its results cannot establish lockout/concurrency equivalence of a proposed authentication adapter.

`artwork-persist` and `artwork-restore` are separate-process persistence checks.
They require explicit `LINER_PROBE_POSTGRES` and `LINER_PROBE_ROOT` environment
variables, accept only loopback database `liner_design_artwork` and an explicitly
named `liner-artwork-probe-*` scratch directory, and never accept production
settings. Prepare that database as a disposable migrated fixture. Run persist,
allow the process to exit, then run restore with the same settings. Restoration
uses a fresh memory cache and throws on any attempted provider access. The
persist step explicitly establishes its own test inventory; it is not a product
recovery shortcut. Drop only the owned database/directory after the check. Never
print or commit the connection environment.

The PostgreSQL experiment requires the explicitly named remediation-owned disposable container:

```bash
python3 diagnostics/proposals/ledger-probe.py \
  --container liner-audit-REPLACE_WITH_OWNED_TEST_CONTAINER \
  --output /tmp/liner-ledger-proposal.json
```

It creates a new `liner_design_20261029` database, checks an insert-only accounting primitive including rollback, and drops that database in `finally`. An existing database with that name causes refusal. It does not alter the application database, install a migration or prove full trigger equivalence. No connection string or password is accepted or printed. Never point it at a production container.

`measurements.json` contains sanitized numeric results and explicit limitations. Required acceptance tests and the proposed migration/rollback steps are in the design document.
