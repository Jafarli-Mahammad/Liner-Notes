# Guest discovery: local synthetic run

This exercises the onsite guest flow against a new disposable loopback database.
It supplies synthetic catalog entries and synthetic candidate evidence. It does
not establish real catalog coverage, recommendation quality or VPS capacity.

## Private configuration

Use the pinned .NET SDK and a local PostgreSQL instance. Supply runtime settings
through private environment configuration; do not commit credentials or generated
state. The preparation script reads environment variables, not IDE User Secrets.

| Setting | Value or requirement |
|---|---|
| `ConnectionStrings__DefaultConnection` | Private connection to a **new** database named `liner_guest_demo_` followed by 12 lowercase hexadecimal characters |
| `LINER_PHASE1_POSTGRES` | Private administrative connection to the same loopback PostgreSQL host/port |
| `Jwt__SecretKey` | Private randomly generated signing key of at least 32 bytes |
| `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT` | `Development` for loopback HTTP |
| `Generation__Origin` | `Synthetic` |
| `Generation__LiveEnabled` | `false` |
| `ListenBrainz__Mode`, `LastFm__Mode` | `FixtureOnly` |
| `ListenBrainz__LiveUseApproved` | `false` |
| `GuestDiscovery__Enabled` | `true` |
| `DiscoveryRuntime__RunJobs` | `true` |
| `LocalEmail__Enabled` | `false` |
| `GenerationStorage__InventoryPath` | New absolute path in a private state directory, outside artifact roots |
| `GenerationStorage__LeasePath` | Separate absolute path outside artifact roots |
| `GenerationStorage__DatabaseOverheadBytesPerPick` | `1048576` for this fixture run |
| `DiscoveryStorage__FilesystemPaths__0` | Existing directory on the relevant filesystem; include additional indices for separate database/WAL/artifact volumes |
| `DiscoveryStorage__MinimumFreeBytes` | Keep the default `536870912`, or explicitly use the supported `67108864` fixture minimum |

Create six empty private artifact directories. Configure each as index `0` of
`GenerationStorage__ArtifactRoots__<category>`, where category is `recordings`,
`cache`, `reports`, `copies`, `backups`, or `partials`. Do not overlap roots or put
the lease/inventory inside them. All applicable filesystem paths must be included
in free-space checks. The managed 1 GB stop is separate from whole-host capacity.

Weekly generation, schedule updates and unsubscribe writes use discovery admission
when `DiscoveryRuntime__RunJobs=true`; use that same setting for a development
Worker sharing this inventory. Concurrent local email capture remains blocked in
both hosts. External delivery is outside this local synthetic run.

## Prepare and run

From the repository root, with that private environment loaded:

```bash
dotnet run --file scripts/guest_discovery_demo.cs -- --prepare-synthetic
dotnet run --project src/Presentation --no-launch-profile -- --urls http://127.0.0.1:5092
```

Use `--file`: the legacy project at the repository root takes precedence over a
bare source-file argument. Preparation creates only a new database, applies its
migrations, verifies accounting, reconciles its new inventory and imports 100
synthetic artists and 20 synthetic albums. It refuses a preexisting inventory or
an arbitrary database name. It does not reset or migrate a retained database.

Open [the local guest demo](http://127.0.0.1:5092). Choose **Try as a guest**, select
artists/albums/styles, and request music. Search typing uses the local catalog.
**Search more online** explicitly schedules the fixture lookup adapter in this
synthetic run. Demo results and catalog matches are labelled accordingly.

Returning shows saved results. **Discover more** is explicit and observes the
configured generation cooldown. The small fixture has five shared candidate
tracks: after those have been shown, an empty batch is expected. This demonstrates
suppression and honest coverage reporting, not discovery breadth.

## Browser verification

Use an existing stable Playwright installation and compatible installed Chromium.
They are development tools, not application dependencies. Set
`LINER_PLAYWRIGHT_MODULE` to the module path when it is outside normal Node lookup,
and `LINER_CHROMIUM_PATH` when an explicit executable is needed. Optionally set
`LINER_GUEST_DEMO_URL` and `LINER_GUEST_DEMO_OUTPUT` (default output is under `/tmp`).

```bash
node scripts/checks/guest_discovery_browser.cjs --approve-synthetic
```

The script checks the server's synthetic evidence mode before generation, blocks
external browser requests and exercises stale search responses, artist/album
selection and lookup, onsite generation, return visits, feedback, export,
registration preserving the same profile, explicit existing-account import,
cooldown, sparse results and private-data deletion. Credentials are generated at
runtime and excluded from output. The report records timings as local fixture
evidence only; it does not benchmark live provider latency.

Stop the demo process after use. Remove only the disposable database/state created
for this run when no further browser or load checks need them. State under `/tmp`
may disappear between sessions.

## Routine operation and recovery

Keep the guest feature and consumer enabled together. Local search reads only the
catalog; returning to results does not submit another batch. An empty result is
reported honestly. Source or storage failures leave saved data readable when the
database itself remains available. Use each returned job ID with the owned
`/api/discovery/jobs/{id}` route to inspect bounded progress/error codes. Never log
guest credentials, lease tokens, private payloads or exports.

Known inventory is renewed once per minute. The same cycle reclaims up to 50
expired guest profiles, 100 expired public evidence entries, 100 expired catalog
records and ten empty publications. Guest inactivity expiry is seven days;
activity renewal is limited to once per hour. Catalog cleanup preserves the head
and staged imports; saved favorite evidence does not depend on a public row
remaining present. Canonical artist/album identities can remain for private
references. Physical PostgreSQL pages and WAL need their own operating budget.

An unknown revision, interrupted inventory publication or failed space check stops
new writes. Do not zero counters or auto-establish a baseline. Stop writers,
investigate the mismatch, verify accounting and explicitly reconcile using the
existing [storage procedure](guest-discovery-storage.md). Apply migrations with
writers stopped and a verified database/key backup; restore the matching database,
keys and artifacts together if recovery is needed. The synthetic preparation
script handles only a new disposable database and cannot repair retained state.

Use the [capacity report](guest-discovery-capacity.md) for the measured limits and
load driver. Its server memory caps are a tested starting profile, not deployment
approval. Reserve host space for the OS, logs, WAL, backups and staging; include
all relevant volumes in free-space checks. Use stable published PostgreSQL ports
when testing restarts, and keep runtime state/key mounts persistent.

Real acquisition needs its approved source manifest and the existing provider
[configuration and approval gates](listenbrainz-provider-runbook.md). No real
bulk source is enabled by the synthetic script. Weekly generation sharing this
inventory must enable discovery admission. Both hosts reject concurrent optional
local email capture; the onsite workflow works with that feature disabled.
