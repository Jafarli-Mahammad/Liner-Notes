# Guest discovery: local resource evidence

Measured on 2026-10-19 (Asia/Baku). Application changes are locally verified;
developer confirmation and deployment verification remain pending.

## Runtime and scope

The Release website ran as UID 1654 in a read-only .NET 10.0.12 container,
limited to 768 MiB. PostgreSQL 18.6 was limited to 512 MiB with 64 MiB shared
buffers and 4 MiB work memory. Both containers shared CPU core 0 on an Intel
Core i5-14500HX. Their combined memory limits were 1.25 GiB. This constrains the
application and database; it does not reproduce the kernel and other services
of an entire 2 GiB VPS or the speed of an older Xeon.

The catalog began with 100 synthetic artists and 20 synthetic albums. Browser
lookup added three records. The final load used 50 persisted guest profiles,
five generation clients, and 5/20/50 concurrent search clients. Forty-five
profiles had no accumulated history. Catalog search is public and identical
for anonymous and signed-in visitors. This is a small-catalog resource baseline.
The earlier scaled SQL probes in the evidence ledger are separate measurements.

## Observations

The final warm run used four repeated prefix/typo queries with no HTTP failures:

| Concurrent search clients | Requests | p50 | p95 | p99 | Maximum |
|---|---:|---:|---:|---:|---:|
| 5 | 100 | 17.8 ms | 27.6 ms | 29.2 ms | 30.5 ms |
| 20 | 200 | 63.1 ms | 123.3 ms | 130.7 ms | 134.6 ms |
| 50 | 500 | 168.9 ms | 322.8 ms | 351.0 ms | 372.0 ms |

Earlier runs reached 240 ms p95 at 20 clients and 531 ms at 50. The provisional
100 ms search target passed at normal load and was missed during bursts. These
figures include loopback HTTP and JSON handling, exclude browser debounce, and
are observations rather than a supported throughput guarantee. Diverse cold
queries against a real catalog still need measurement.

Five style-seeded requests in the final run completed in 2.11–2.27 seconds
(observed p95 2.27 seconds, including polling and queueing). A browser artist/album
first batch took 4.389 seconds. In a separate process-recovery run, five unfinished
requests survived a forced website restart and completed in 5.05–5.55 seconds.
That test establishes queued-work recovery; lease-expiry/fencing behavior is
covered by PostgreSQL regressions. Provider and queue delays were not separately
instrumented, and synthetic adapters do not establish live-provider latency.

During the recovery run, cgroup peaks were 109 MiB for the website and 182 MiB
for PostgreSQL. Later browser/load and catalog-regression activity increased
observed peaks to 193 MiB and 280 MiB. The website process RSS high-water mark
was 281 MiB, including mapped pages whose cgroup accounting can belong elsewhere.
Neither container recorded an OOM or OOM-kill event. Load generators and Chromium
ran outside the server containers. The later database peak includes additional
disposable-database regression work and is a conservative mixed-workload sample.

After browser and load cleanup, SQL counts for private profiles, jobs,
recommendations and subscribers were all zero. The database retained 123 public
catalog records. Public evidence held 16 entries (296,722 serialized bytes) at an
earlier checkpoint; a later bounded maintenance pass left two entries (1,268 bytes).
Catalog relations and their indexes used 827,392 bytes in this fixture.

## Disk budget

The pinned runtime images occupied 339,383,196 bytes (.NET) and 433,118,553 bytes
(PostgreSQL); published application files occupied 15,677,244 bytes. One measured
cluster footprint was 102,118,827 bytes, including 67,108,864 bytes of WAL; the
demo database itself was 10,835,647 bytes. These are fixture observations, not
maximum sizes. SDK/build images, the OS, backups, logs, source artifacts and
longer-lived private history must be budgeted separately. Build on a development
machine or CI rather than retaining SDK/build caches on a small VPS.

Keep at least 20% of the chosen filesystem free: for example, set
`DiscoveryStorage__MinimumFreeBytes` to 2147483648 for a 10 GiB budget, and
3221225472 for 15 GiB, after confirming every relevant database/WAL/artifact
mount is represented. The fixture used the explicit 64 MiB test minimum.
The managed 1 GB write stop remains separate from whole-host capacity. No actual
host disk was filled or resized. Low-space, quota and stale-inventory stops
were exercised through the storage regressions.

The application cache admission budget remains 64 MiB in aggregate, and durable
public evidence is limited to 256 MiB/5,000 entries. Active catalog coverage,
WAL/log rotation, backups and production storage sizing need deployment-specific
decisions. Bounded cleanup frees logical space; PostgreSQL may retain pages.

## Reproduction and acceptance

Use the [synthetic runbook](guest-discovery-local-run.md). With its server running
and Node 22 available, run:

```bash
node scripts/checks/guest_discovery_load.cjs --approve-synthetic
```

The driver refuses non-loopback origins and non-synthetic evidence before writes.
It creates 50 profiles, measures search, submits five idempotent batches, reads
results, and deletes its guests. Reports default to `/tmp/liner-guest-load`;
`LINER_GUEST_LOAD_OUTPUT` selects another private output directory.
Allow a guest-start rate-limit window between runs: two immediate 50-profile
runs exceed the default 60 new guests per minute from one address.

For a controlled crash check, set `LINER_GUEST_LOAD_RESTART_MARKER` to a new file.
After the driver creates it, restart only the disposable website, wait for its
synthetic overview to respond, then create `<marker>.resumed`. The driver retains
its private cookies in memory and resumes polling. The host must provide the
restart; the driver never controls Docker or a retained database. Use stable
published database ports: Docker can reassign an automatically selected port
after a container restart. One discarded run failed for this setup error.

Final verification: 648 .NET checks passed with zero failures/skips; seven
catalog integration checks additionally passed after strengthening retained
private-evidence assertions; all 12 browser checks passed against the constrained
Release host. Browser and load fixtures were removed. No live catalog was imported,
no quality evaluation ran, and no external email was sent. Concurrent optional
local email capture remains explicitly blocked in both composition roots.

Developer review: open the synthetic demo, select artists/albums/styles, generate
and return to results, then check feedback, export and registration/import. Record
confirmation separately from this local evidence.
