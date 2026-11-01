# Operating the v0.5.0 beta

This is a single Linux host deployment: Caddy, the web host and PostgreSQL,
with short stopped-writer maintenance windows. Mail delivery is excluded.
Read [the evidence](production-beta-evidence.md) before treating a check as
verified. The VPS, domain, off-host repository and alert destination still need
to be selected. Nothing in this procedure publishes a release automatically.

## Build the release

Use Docker, Python 3.12+ and Git. Run from a clean checkout of the reviewed
revision. The build exports a Git tree, installs locked frontend dependencies,
builds React and .NET, and validates the bundled assets and notices. Ignored
developer outputs are not inputs.

```sh
python3 scripts/ops/build_release.py --output release-images.json
python3 -m unittest discover -s scripts/ops -p 'test_*.py'
python3 -m unittest discover -s scripts/checks -p 'test_production*.py'
```

Keep the resulting source-tree identity and four image IDs with the release.
Run `production_security.py` against web, Worker and migration-tool image IDs;
it rejects high/critical findings and checks the scanned image configuration.
The scanner uses private disk-backed scratch and never mounts application data
or the Docker socket. CI runs the same gate. Rebuild and repeat acceptance after
security patches rather than reusing an earlier candidate's results.
The build pins declared inputs; byte-for-byte identical image output has not
been established. The migration image contains the SDK and source and is an
operator tool; the web runtime does not contain the SDK or Node.

## Prepare a new host

Use a supported Linux distribution with current security updates, Docker Engine
and Compose v2-compatible CLI. Use SSH keys, restrict administrative access and
configure the host firewall to allow only SSH and public TCP 80/443. Database
and Kestrel ports must remain unpublished. Docker membership grants root-level
host control; restrict the `linernotes` operator account accordingly.

Choose a private Docker /28 subnet which does not overlap host or VPN routes.
Do not treat synthetic benchmarks as a VPS sizing result. The Compose resource
limits are initial bounds; check the selected machine can accommodate their
combined peaks, OS memory, PostgreSQL/WAL, images, backup scratch and free disk.
The managed-storage quota is only part of physical disk consumption.

Copy the reviewed checkout to `/opt/linernotes` and verified images to the host.
Run `init_production.py --help` with a new absolute directory, the real hostname
and the four image IDs from `release-images.json`. Example shape:

```sh
python3 scripts/ops/init_production.py --directory /srv/linernotes \
  --site music.example.org --network 172.30.85.0/28 \
  --web-image IMAGE_ID --worker-image IMAGE_ID \
  --tools-image IMAGE_ID --volume-image IMAGE_ID
```

The initializer refuses an existing directory. Private parent directories are
0700. Mounted settings are readable by non-root containers inside those private
directories; do not expose or recursively make the parent directory public.
Do not print resolved Compose configuration or copy credentials into tickets.
Jobs and provider-use approval start disabled. Enable provider use only after
reviewing applicable terms; `--approve-provider-use` records an operator decision,
not evidence of permission. It does not enable processing by itself.

Use the generated private `deployment.env` and production Compose file for
initial provisioning. Start only `db`, wait for its authenticated health check,
run `volume-init`, run `migrate`, then run the Worker `establish-storage` action
with operator settings and `--writers-stopped true`. The integration rehearsal
in `scripts/checks/production_recovery.py` shows these exact invocations through
the shared `Operations` wrapper. Establishment is for a genuinely new database;
it refuses an existing witness. Start `web` and `proxy` only after verification.
There is no ordinary-startup migration or automatic baseline adoption.

For a genuinely new installation, the exact sequence through the shared wrapper
is below. It is not an upgrade or repair procedure:

```sh
python3 - <<'PY'
import json, sys
from pathlib import Path
sys.path.insert(0, str(Path('scripts/ops').resolve()))
from production import Operations, lifecycle
o = Operations(json.loads(Path('/srv/linernotes/operations.json').read_text()))
with lifecycle(o.state):
    o.command('up', '-d', '--wait', '--wait-timeout', '90', 'db')
    o.command('run', '--rm', '--no-deps', 'volume-init')
    o.command('run', '--rm', '--no-deps', 'migrate')
    o.worker('establish-storage', operator=True)
    o.worker('verify-storage', operator=True)
    o.command('up', '-d', 'web', 'proxy')
PY
```

Configure DNS, check IPv4/IPv6 routing, and allow Caddy to obtain public TLS.
Verify HTTPS redirect, certificate renewal, host validation and mutation-origin
checks from outside the VPS. The acceptance override uses a local CA and loopback
port; never install that override as public production configuration.

## Operate and monitor

All lifecycle commands use the generated configuration:

```sh
python3 scripts/ops/production.py --configuration /srv/linernotes/operations.json status
python3 scripts/ops/production.py --configuration /srv/linernotes/operations.json backup
```

`status` returns a nonzero exit code for degraded operation. Public
`/health/live` and `/health/ready` expose minimal responses. Detailed `/ops/status`
is loopback-only and denied by the proxy. The operator reads it inside the web
container. It reports storage, database, disk headroom, background observations,
queue age/expired leases, paused jobs, recent failures and backup age/location.
Checks do not contact providers or repeatedly hash the complete artifact tree.
Keep logs private; bounded error codes avoid provider payloads and user data.

Review and install the supplied systemd backup/status service and timer templates
after adjusting the operator user and paths. Connect failed units to an actual
notification destination and test delivery. Merely enabling a timer does not
establish alerting. Also monitor host disk/inodes, memory/OOM, Docker health,
PostgreSQL/WAL growth and certificate expiry externally. Log rotation and tmpfs
limits are in Compose. Retention does not delete registered users' history.

When provider permission and deployment checks pass, set `LINER_RUN_JOBS=true`
in the private environment and recreate web. Generation, artwork and retention
reuse the existing services. Weekly onsite generation is an explicit bounded
operator action, without mail:

```sh
python3 scripts/ops/production.py --configuration /srv/linernotes/operations.json \
  generate --user USER_UUID --week 2026-W45
```

It uses the live source and the existing generation handler. A host lifecycle
lock prevents overlap with backup/restore/upgrade. Automatic all-account weekly
scheduling is not part of this beta operating contract. Paused jobs are visible
as degraded operation rather than silently presented as working.

## Backup and recovery

Use an encrypted **off-host** restic repository. Configure its endpoint and
credentials in private `restic.env`, and remove `local_repository` from
`operations.json` when selecting a remote repository. A local repository is for
rehearsal and is reported as degraded. Run `init-repository` only for a new
repository. Keep the restic password, remote credentials and access instructions
in independent custody; these credentials are deliberately excluded from the
backup. Losing them makes the encrypted backup unrecoverable.

Backup serializes lifecycle operations, stops application writers, refuses stray
runtime-role sessions, verifies storage and accounting, captures a custom-format
PostgreSQL dump, managed files, keys, reconstruction settings and exact images.
It derives the restore schema proof by restoring the captured schema into a
temporary database, then drops that database. This handles PostgreSQL's logical
restore representation changes without weakening ordinary runtime identity
checks. Scratch capacity must accommodate these copies and the temporary schema.
The web resumes after safe capture; upload and full repository data verification
follow. Only a successful verified snapshot updates the last-good record.
Retention is seven daily, four weekly and three monthly snapshots.

Daily backups imply a provisional 24-hour recovery point only while a usable
off-host snapshot completes daily. Recovery time must be measured on the actual
host and repository; the local fixture timing is not a production promise.
Failed attempts retain bounded status and maintenance markers. Investigate disk,
credentials, network and repository errors; do not replace the last-good snapshot
or clear an unsafe marker to make the dashboard green.

Restore into a **new project and empty volumes**. Initialize a fresh target with
the intended domain/network, independently supply the repository credentials,
and select an explicit snapshot ID:

```sh
python3 scripts/ops/production.py --configuration NEW_TARGET/operations.json \
  restore --snapshot SNAPSHOT_ID
python3 scripts/ops/production.py --configuration NEW_TARGET/operations.json resume
```

Restore authenticates the repository, checks the complete content manifest,
loads captured images and restores matching secrets, dump, files and keyring.
It verifies schema, logical inventory and hashes before explicitly establishing
the restored physical witness. Pending publication or unexplained content fails
closed. Nonempty target projects/volumes are rejected; there is no force flag.
Verify login, existing protected tokens, taste, owned recommendations, persisted
explanations, export and subsequent admitted writes before switching traffic.
Keep provider acquisition paused until recovery acceptance is complete.

Rehearse with disposable volumes locally:

```sh
python3 scripts/checks/production_recovery.py --disposable --images service-images.json
```

`service-images.json` maps `web`, `worker`, `migrate`, `volume-init` to the exact
four image IDs. The harness owns and removes only its generated projects. Never
point it at the developer's database or actual user data.

## Upgrade and storage policy changes

`upgrade --images service-images.json` requires four images stamped with the same
known source tree, captures a verified recovery set, stops writers and checks
pending migrations before startup. The automated beta path supports compatible
releases with no pending schema migrations. A schema-changing release requires
a separately reviewed migration/witness transition and recovery rehearsal; it
is intentionally blocked rather than adopted automatically. No old writer is
automatically restarted against a changed guarded schema.

Storage budget/path changes similarly need stopped writers, the previous policy
and the explicit `change-storage-policy` operator action. Changing only an
environment value causes a policy mismatch and fails closed. Ordinary runtime
and `verify-storage` never repair unexplained data. Use `resume` only when the
operation recorded a safe-to-resume state; unsafe partial restore/upgrade requires
investigation or recovery into a fresh target.

## Release decision

The owner must accept the tested beta, real provider terms/use, selected host
headroom, off-host recovery, independent key custody, public TLS/firewall and
working failure notification before public launch. Last.fm is conditional on
actually enabling its adapter; the current Live catalog uses ListenBrainz and
MusicBrainz. Runtime dependency notices are bundled; provider metadata and cover
art rights are not granted by this deployment tooling. Preserve project AGPL
source obligations and review upstream terms at the time of launch.

Provider references checked on 2026-11-01: [ListenBrainz API rate limits and identification](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html),
[ListenBrainz terms](https://listenbrainz.org/terms-of-service/),
[MusicBrainz API limits](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting),
[MusicBrainz core/supplementary data licenses](https://musicbrainz.org/doc/About/Data_License)
and [Cover Art Archive copyright notice](https://beta.coverartarchive.org/).
The shared provider gate spaces calls by at least 1.05 seconds and honors bounded
backoff headers. API access does not establish blanket redistribution rights.

Keep operating configuration, key material and repository credentials on durable
storage. `/tmp` is unsuitable: a host restart can remove it even when Docker
volumes and images survive. Run local browser acceptance after Docker network
creation/removal has finished; host network changes can abort Chromium requests.
