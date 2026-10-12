# Phase 9 implementation plan

Approved continuation: 2026-10-12. The developer approved the written design,
required latest stable project packages/tools, and instructed work to proceed.
MediatR 12.5.0 is a separately approved licensing exception. Redis is excluded.
This is the concrete sequential plan authorized by that instruction; it creates
no permission for a remote push, release, live recording or external email.

## Revised version policy

Check official stable feeds, migrate APIs as needed, verify compatibility, then
freeze exact SDK/package/tool/image/action versions. Record the feed snapshot and
exception in `phase-9-versions.json`. Upgrade EF runtime/design/tool together.
New source/test migrations are not generated to hide an unexpected model change;
inspect any such difference first. Database verification uses disposable storage
and tests both fresh migrations and a populated legacy upgrade.

## Sequential implementation and verification

1. **Dependency modernization and build inputs.** Upgrade direct and transitive package
   versions centrally to the verified stable releases; update Swagger/OpenAPI and other
   changed APIs. Pin the SDK and local EF tool, create package locks, verify
   locked restore/Release build and current full PostgreSQL suite. Commit the
   upgrade and its compatibility evidence separately.
2. **Container targets and Compose.** Build non-root web/Worker images plus
   one-shot migration/private-volume targets. Pin stable image digests. Provide
   local credentials generated without overwrite or stdout exposure. Compose
   exposes only loopback web, uses dedicated database/state volumes and leaves
   Worker opt-in with its original Development/loopback/Synthetic boundaries.
   Exercise startup ordering, migration failure/idempotency, synthetic user
   lifecycle, encrypted capture, restart, unsubscribe and deletion. Commit this
   packaging/runbook work after verification.
3. **CI and release preparation.** Pin latest stable GitHub action revisions;
   provide healthy disposable PostgreSQL, locked restore/Release build, full .NET
   checks, existing fake-input Python/repository checks and strict TRX validation.
   Add a container smoke job. Document missing Last.fm written public-page
   approval and production operational decisions; update status and changelog.
   Run final local verification, create a focused local commit and report exact
   passed/failed/skipped counts and limitations.

## Verification rules

- No existing test is removed or weakened to make the upgrade pass.
- Fresh database and populated migration checks must pass without new product
  schema or export contracts. Inspect EF 10 model differences if discovered.
- Run the existing scorer/benchmark tests in the full suite; no scorer change.
- Preserve application retry, cancellation, encryption-key persistence, storage
  reconciliation and account-deletion boundaries.
- A task-owned local PostgreSQL instance is necessary for zero skipped checks;
  other databases and containers are left alone.
- Documentation and CI distinguish local equivalent checks from an actual
  remote Actions execution. GitHub runs remain unverified until an authorized push.
- Record exact image/SDK/server versions and immutable pins; no claim of
  byte-identical output or validated production capacity.
- Stop for a concrete decision only if an unsupported contract/schema change or
  licensing issue prevents the authorized upgrade; continue independent work.

## Execution status

- Dependency modernization locally verified: 472 passed, zero failures/skips.
- Swagger replacement: NSwag 14.7.1 verified after the developer selected investigation; document, security and Production hiding checks pass.
- Containers/Compose: pending implementation.
- CI/release documentation: pending implementation.
- Developer confirmation and public release: pending, independent of test passes.

See [approved design with version correction](phase-9-review.md).
