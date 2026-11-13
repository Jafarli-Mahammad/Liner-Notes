# Run the actual website locally

From the repository root:

```bash
bash scripts/run_live.sh
```

Open **http://localhost:5080/**. Keep the terminal running; Ctrl+C stops the host.
After the first setup, the Presentation project's **Liner Notes** IDE launch
profile starts the same live application.

## First start

The launcher uses the SDK pinned in `global.json`. If the system `dotnet` cannot
resolve that SDK, it checks `$HOME/.dotnet/dotnet`. `LINER_DOTNET` can select a
different installed executable.

PostgreSQL must already be running on loopback. Presentation User Secrets must
contain `ConnectionStrings:DefaultConnection`, `Jwt:SecretKey` and
`ListenBrainz:Token`. See [private local configuration](local-configuration.md).
The initializer reuses those private credentials to create a separate persistent
database, `liner_notes_live`. It applies migrations only when creating that new
database and establishes the verified managed-storage inventory. It never copies
synthetic data or the previous account/database contents into the live database.

Settings, inventory and artifact directories live under `.linernotes/live/`.
The directory is excluded from Git and Docker build contexts. On Unix, its
directories have owner-only access and the settings file is created with mode
0600. Provider tokens and connection strings are not printed or committed.
Existing User Secrets remain unchanged.

The normal profile explicitly loads that private live configuration. It selects
`Generation:Origin=Live`, `Generation:LiveEnabled=true`, ListenBrainz `LiveOnly`,
and both guest discovery and the durable job consumer. The profile fails if the
live configuration is incomplete. Its settings override previously inherited
fixture environment values. It does not select a fixture provider on failure.

## Real music and persistence

Autocomplete searches the stored catalog. **Search more online** explicitly
queries MusicBrainz when an artist, album or track is missing. Requesting
discoveries obtains real candidates from ListenBrainz and stores deterministic
score explanations and feedback. Nothing is generated merely by viewing a page.

Public recording metadata acquired while preparing candidates is imported through
the existing coordinated catalog importer before private scoring. The projection
contains canonical recording/artist identities and names, excludes supplementary
tags and user/taste/ranking fields, and expires with the provider's shared cache
permission. Completed picks can therefore appear in the recipient-free Recent
recommendations feed without requiring a synthetic or preloaded track catalog.
ListenBrainz remains the default; Last.fm remains available behind its existing
provider abstraction and existing live-use restrictions.

The client respects one-request-per-second pacing, response backoff, bounded
request/response limits and configured cache budgets. Shared-cache freshness uses
`s-maxage` before `max-age`; no-store, no-cache and private responses remain
ineligible for shared retention. See the [ListenBrainz API rules](https://listenbrainz.readthedocs.io/en/latest/users/api/),
[MusicBrainz API rules](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting),
[core/supplementary data licenses](https://musicbrainz.org/doc/About/Data_License)
and [HTTP shared-cache semantics](https://httpwg.org/specs/rfc9111.html#cache-response-directive.s-maxage).

## Retained data and scope

Restarting the launcher reuses the database and matching inventory. For an
explicit setup check, with writers stopped, run the pinned SDK's
`dotnet run --file scripts/setup_local_live.cs --no-cache -- --initialize`.
An existing database without its matching settings/inventory is rejected;
the initializer does not establish a fresh baseline over unknown retained data.
Pending schema upgrades also require a separate migration with writers stopped
and a verified backup.

This is the real application with live data on this machine. The host uses the
Development environment for loopback HTTP and IDE integration. Public deployment,
TLS/domain configuration and external email delivery are separate operations.
Manual QA and synthetic verification remain explicit, separate tool profiles;
their databases and inventories must not be shared with this live runtime.
