# Phase 9 local Compose runbook

This packages the existing **Development, fixture-only** slice. It does not
provide production scheduling, external delivery or public-release permission.

The default fixture provider is now ListenBrainz; Last.fm remains available by
explicit configuration. See the [provider runbook](listenbrainz-provider-runbook.md)
for origin/usage gates, private token configuration and rollback.
Supported verification platform: Linux amd64. Other platforms are unverified.
Docker Compose 5.6.0 was verified; all images and tools are pinned in
[the version snapshot](phase-9-versions.json). No Redis service is used.

## Start an isolated local stack

From the repository root:

```bash
python3 scripts/phase9_init_local.py
docker compose --env-file .env.phase9.local --profile local-worker --profile local-operator build
docker compose --env-file .env.phase9.local up -d web
```

The generator creates an ignored private file and refuses overwrite. Preserve
that file for a retained stack; changing its database password does not rotate
credentials in an already initialized PostgreSQL volume. Do not print expanded
Compose configuration. Use `config --quiet` to validate it.

Open `http://127.0.0.1:8080/manual-test` for the existing QA interface or
`http://127.0.0.1:8080/swagger` for Development API documentation. Register a
disposable account using the existing form/API and select known fixture artists
such as Jakuzi, Son Feci Bisiklet and M.O.O.N. Unknown fixture seeds can correctly
yield an empty digest. These fixtures establish mechanics, not musical quality.
The health URL is `http://127.0.0.1:8080/health`.

Only loopback web is published. PostgreSQL is reachable inside Compose and has
no host port. Its health gates the one-shot migration service; migration success
and private-volume initialization gate web startup. Migrations use the existing
AppDbContext and schema. PostgreSQL 18 uses `/var/lib/postgresql` as its volume
mount root; do not attach a PostgreSQL 16 data directory to it. This new local
stack has separate named volumes, not an automatic upgrade of another database.

## Run the existing Worker explicitly

Set the disposable account GUID and its canonical current ISO week:

```bash
phase9_user_id=YOUR_DISPOSABLE_ACCOUNT_GUID
phase9_week=$(date -u +%G-W%V)
docker compose --env-file .env.phase9.local run --rm worker --action reconcile --user "$phase9_user_id" --week "$phase9_week" --origin Synthetic
docker compose --env-file .env.phase9.local run --rm worker --action generate --user "$phase9_user_id" --week "$phase9_week" --origin Synthetic
docker compose --env-file .env.phase9.local run --rm worker --action reconcile --user "$phase9_user_id" --week "$phase9_week" --origin Synthetic
docker compose --env-file .env.phase9.local run --rm worker --action capture --user "$phase9_user_id" --week "$phase9_week" --origin Synthetic
```

Worker shares the local database's network namespace so its connection remains
loopback, preserving its existing guard. Web and Worker share private paths under
`/app/local-state`, including encrypted copies and the inventory/lease. A changed
inventory remains a stop; inspect the reason and perform an explicit reconcile
before a new attempt. Capture results distinguish `local_captured` from
`already_captured`; neither means an external message was sent.

For an enrolled fixture account, scheduling remains an explicitly bounded action:

```bash
docker compose --env-file .env.phase9.local run --rm worker --action schedule --origin Synthetic --Schedule:Users:0 "$phase9_user_id"
```

It returns a run result containing `Ticks`, each containing `Jobs`. It does not
automatically enroll users or run indefinitely. The [Phase 8b guide](phase-8b-local-run.md)
explains UTC slots and the fixture helper; the automated Compose check prepares
only its own fabricated account's due slot. There is no production cron/timer.

## Inspect, unsubscribe and delete

The operator profile runs the existing helper from a read-only source mount
in a dedicated SDK image as the same non-root APP_UID used by web and Worker.
Its source, NuGet, and CLI cache paths are writable by that user:

```bash
docker compose --env-file .env.phase9.local run --rm operator --action inspect --user "$phase9_user_id" --week "$phase9_week"
docker compose --env-file .env.phase9.local run --rm operator --action unsubscribe-link --user "$phase9_user_id" --week "$phase9_week"
```

Inspection prints sanitized text. The second action prints a private capability
for local confirmation; keep it out of chat, logs and commits. GET displays a
confirmation page; POST opts the account out. Later capture is suppressed.
Authenticated export is now version 2.5, adding discovery metadata to sanitized copies and stored scores.
Account deletion invalidates access and removes owned configured capture files.
Database keys and shared catalog entries remain under their documented boundaries.

## Verification and stopping

The automated check creates a randomly named project, private credentials and
fabricated account, exercises the lifecycle and removes only its own volumes:

```bash
python3 scripts/checks/phase9_compose.py --build
```

For a retained manual stack, normal shutdown preserves data:

```bash
docker compose --env-file .env.phase9.local down
```

Only when explicitly discarding that disposable local stack:

```bash
docker compose --env-file .env.phase9.local down --volumes --remove-orphans
```

Do not run destructive cleanup against another project. Retain the same database,
keys, state volume and private environment file for restart testing. The current
1 GB storage stop is temporary; no hosting-capacity decision is established here.

For host development, install **SDK 10.0.401** first; `global.json` deliberately
disables roll-forward. Then restore tools and dependencies in locked mode.
The task's downloaded SDK under `/tmp` is evidence, not a persistent host install.

See [implementation evidence](phase-9-evidence.md) and [release gates](phase-9-release-checklist.md).
