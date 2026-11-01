# Production foundations for v0.5.0 beta

Date: 2026-11-01. Status: approved by the developer on 2026-11-01; implementation-plan approval remains separate.
Baseline: Prism at `6425d52`. This document is not implementation evidence.

## Goal and agreed constraints

Produce a complete production image and a locally rehearsed operating procedure for a small Linux VPS. Preserve current product behavior, provider boundaries, persisted explanations, ownership, privacy and storage-integrity guarantees. Email transport, public deployment, purchases, pushes and release publication are excluded. The VPS and domain are not selected.

The developer approved the recommended single-host approach and short maintenance windows. Daily backups imply a provisional recovery-point target of 24 hours; recovery within a few hours is a target to measure, not a promise. Only a completed, usable off-host backup counts toward that recovery point.

## Architecture and alternatives

Use Docker Compose with one reverse proxy, one ASP.NET web host and private PostgreSQL. Reuse the existing in-host discovery, artwork, cleanup and publication-recovery services. Retain a separate bounded Worker/operator entry point. Host systemd timers invoke maintenance and health/backup checks; no Redis, broker, orchestration platform or general-purpose scheduler is required.

Caddy is the proposed TLS proxy. It avoids a separate certificate-renewal service. Nginx plus an ACME client remains feasible but adds certificate lifecycle configuration. Keep the proxy container replaceable: application configuration specifies the public origin and explicit trusted proxy addresses rather than depending on a proxy vendor.

Prefer stopping application writers for a matched logical database/files backup. An online cross-resource snapshot protocol adds unnecessary coordination risk for this beta. Cold PostgreSQL volume backups are possible, but couple restoration more tightly to server versions and physical layout. They are not the primary restore contract.

Use restic as an operator tool for encrypted backup storage, retention and verification, rather than designing archive cryptography. No remote service is provisioned. Tests use a disposable local repository; actual off-host credentials, availability and recovery-key custody remain deployment acceptance requirements.

## Release artifact

Add a pinned Node frontend build stage using the committed npm lock and `npm ci`. Typecheck and build React, collect required license files, and publish its outputs into the web image. Do not consume generated assets from the developer checkout. Keep SDK, Node, build tools and private operator material out of the web runtime image.

Define one release-build path used by local verification and CI. Build from an exported tracked tree with no ignored assets, credentials or developer dependencies. Record source revision, lock fingerprints, image identity and actual tool versions. Reproducible means the declared pinned inputs produce a complete release; do not claim byte-identical images without a separate comparison.

Preserve the existing local Compose workflow. Add a separate Production topology and a loopback-only acceptance topology using the same web image. Tests must identify the image being exercised, not silently rebuild or replace it after verification.

## Production configuration and exposure

Reject unsafe Production combinations at startup with bounded, nonsecret reasons: synthetic/fixture generation, local live-settings loading, local email capture, absent public HTTPS origin, wildcard host acceptance, missing signing/provider credentials where required, unconfigured storage admission and invalid trusted-proxy configuration. Maintenance commands use their own explicit configuration contract and do not need live-provider availability merely to inspect or recover storage.

Publish only the reverse proxy's HTTP/HTTPS ports. PostgreSQL and Kestrel remain on private networks. Trust only the configured proxy hop; never accept arbitrary forwarded headers. Validate the public Host and mutation Origin, preserve secure cookies/CSRF checks and existing CSP, and keep development pages and Swagger unavailable. Test forged Host, Origin and forwarding headers through the actual proxy.

Use private, ignored operator configuration and secret files mounted read-only where feasible. Do not print expanded configuration or put credentials in command arguments. Run web/Worker as non-root. Give the application a database role with the privileges its existing accounting and publication protocol actually needs; keep schema migration/administration privileges in a separate operator role. Test this role separation against real PostgreSQL.

Persist the default ASP.NET Data Protection keyring separately from ephemeral container storage, retaining its application discriminator. Preserve the existing separate database-backed unsubscribe/capture protector. Protect both in backup/recovery even though delivery is disabled. Do not merge key purposes or change existing token compatibility.

## Storage policy and integrity

Replace the fixed 1,000,000,000-byte cutoff with a validated effective policy, retaining the existing value as the compatibility default. All writers must agree on the policy through the database-anchored storage contract. A policy mismatch must fail closed before writes. A budget change requires stopped writers, integrity verification and explicit policy establishment; a new environment variable alone must not authorize a changed policy.

Preserve exact row/artifact/reservation accounting, SHA-256 verification, publication/recovery states, external-change detection, leases, root-path binding and privacy deletion guarantees. Preserve separately enforced physical disk headroom. Keep stable in-container managed paths so moving host volumes does not unnecessarily change the trusted root identity.

Bound container memory/CPU, log rotation, caches, work queues and operator staging. Physical capacity must include PostgreSQL/WAL, runtime keys, images, logs, backup scratch space and the backup repository where local. Check disk headroom before creating a backup or running migrations. Keep backup staging and repositories outside managed artifact roots to avoid unaccounted recursive writes.

Retain existing guest expiry and public-cache retention. Do not delete registered users' taste/history to create space. Measure large-history/export memory separately from short discovery requests. Local synthetic tests establish bounded mechanics, not real-provider capacity or latency.

## Upgrade and backup protocol

Operator commands share a host-side lifecycle lock. The supported topology has one declared set of writers; maintenance must stop that set and prevent timer-triggered jobs from starting during the operation. Refuse overlapping operations. A crash must leave an explicit maintenance/failure state and must not silently restart an unsafe partially upgraded host.

For a backup:

1. Enter maintenance, drain/stop every web, Worker and maintenance writer, and inspect unresolved publication operations. Complete supported recovery or fail with instructions; do not discard pending reservations.
2. Verify current database accounting, trusted inventory and artifact hashes. Do not reconcile unexplained content into a backup baseline.
3. Create a private custom-format PostgreSQL dump, matching managed files and inventory, runtime key material, and the configuration needed to reconstruct paths/policy. Record the release image and PostgreSQL version. Include cryptographic content manifests without exposing private data in logs.
4. Validate the completed staging set and publish it atomically. Restart only the known-safe original services. Upload the immutable staged set to the encrypted repository after capture so remote upload time does not extend the maintenance window.
5. Record capture, repository and verification outcomes separately. A partial dump, partial upload or failed verification cannot replace the last known-good backup. Retain failed-operation status and bound scratch retention.

For an upgrade, verify a usable pre-upgrade recovery set, stop writers, apply explicit migrations with the operator role, verify the expected schema/accounting/artifact baseline, and establish the approved inventory/policy before starting the new image. Never auto-migrate or adopt filesystem content in ordinary startup. Roll back by a documented compatible migration procedure or a matched recovery set, not by running an old writer against a new guarded schema.

## Restore protocol and evidence

Restore into new isolated volumes/database by default. Reject nonempty targets unless a separately explicit destructive action has been authorized; normal verification never touches the developer's live database or files.

Verify the encrypted repository and restored manifest; reject missing, changed or unexpected payloads and unsafe paths/links. Restore the dump with error-stop behavior, preserve matching keys/files, and verify all managed tables and trigger coverage before trusting the result. Logical restoration can change physical database size, so compare logical accounting and artifact hashes first, then explicitly establish the restored physical inventory while writers remain stopped. This is a controlled restore operation, not automatic repair.

Start the exact recovery image with provider acquisition initially disabled and storage admission enabled. Check reads, ownership, explanation decoding, authentication, exports and subsequent admitted writes. Then exercise background recovery and permitted generation. Compare row counts and content fingerprints without writing personal data into evidence logs. Record backup size, capture duration, downtime and restore duration.

Failure tests cover interrupted capture/upload, corrupt or mismatched files/dump, missing keys, insufficient space, wrong policy/roots, pending publications, concurrent maintenance attempts and interrupted restoration. Retain the source and last known-good backup on failure.

## Background processing and beta boundary

Make existing discovery/artwork/cleanup/publication-recovery consumers work under Production with the same leases, attempts, deadlines, pacing and storage admission. Do not introduce an alternative job queue.

This foundation design proposes a production-safe, bounded operator command for generating an explicit user's weekly digest without email capture, preserving user/week idempotency, complete receipts and current provider/storage validation. Keep existing Development-only local capture and its scheduler semantics intact.

Automatic weekly onsite generation is a separate product decision because the current scheduler selects confirmed, email-opted-in users and advances only after capture. Do not repurpose email consent, mark email confirmed or silently widen that selection. If automatic onsite generation is selected for v0.5.0, extend the design with a separately reviewed eligibility/opt-in and scheduling contract before implementing that part. The operator-command boundary is proposed for approval, not an assertion that the full background-processing objective is already satisfied.

## Operational visibility

Separate process liveness, storage/database readiness and background-service degradation. Readiness must not make upstream HTTP requests or perform expensive full-artifact scans per probe. Use validated startup state and bounded cached operational results. An upstream outage must not cause endless container restarts or make saved history unavailable.

Expose minimal public health responses. Keep diagnostic detail private to the operator network/command. Report bounded codes for storage admission/integrity stops, queue age/failures, last successful cleanup/recovery, disk pressure, backup freshness and verification failures. Retain operation-correlated structured logs without secrets, provider bodies, raw searches or high-cardinality personal metric labels. Provide host timer exit statuses and understandable diagnostic commands; no observability server is required for beta.

## Providers and licensing

Use the existing provider abstraction and ListenBrainz production path. Preserve shared per-IP pacing, HTTP cache restrictions, response-size limits, request budgets, deadline behavior and valid contactable User-Agent. Test web/Worker coordination. Preserve Last.fm availability behind its adapter without enabling its live/public use as a fallback.

Document active data origins, required attribution and applicable provider conditions. Last.fm public-page permission is conditional on actually serving Last.fm-derived data; audit retained/published provenance rather than assuming provider selection removes that obligation. MusicBrainz tag licensing requires separate attention from core metadata licensing. Code configuration flags do not constitute external approval. Do not contact providers or claim legal clearance.

Run bounded live read-only smoke checks and an exact operation-to-persistence browser journey only using available authorized credentials and permitted source access. Keep these results distinct from deterministic fixture tests, recommendation-quality evaluation and production capacity. Do not modify scoring, evidence requirements or benchmark policy.

## Verification and reviewable delivery

Implementation should use focused commits for: complete artifact; Production topology/security; storage policy/operator verification; consistent backups and restore rehearsal; operational/background integration; and final artifact acceptance/documentation. Concrete execution tasks and ordering belong in the implementation plan after written-design approval.

Reuse targeted Domain/Application/DataAccess/Infrastructure/Presentation suites, disposable PostgreSQL, existing browser tests and storage crash/recovery cases. Add meaningful tests at the new production boundaries, including clean-checkout packaging, runtime role permissions, startup refusal cases, proxy headers and secure mutation behavior, quota agreement, backup/restore failure paths and Worker idempotency. Run broader regression and container/browser acceptance once the integrated changes justify it.

Test the final image through local HTTPS using a test CA, with the same proxy and Production middleware as deployment. Verify frontend asset loading, registration/login, guest handoff, discovery/artwork, feedback, history, complete explanations, export/deletion, restart recovery and cross-user isolation. Rebuild after fixes and repeat affected artifact checks. Include dependency/secret/security checks and verify stable releases/compatibility before changing pins; retain the approved MediatR licensing exception.

Update CHANGELOG, PROGRESS and a canonical concise operator runbook. Record exact commands, revisions/artifact identities, fixture/live origin, pass/fail/skip counts and limitations. A locally passing image is not developer acceptance or deployment verification.

## Deployment-dependent acceptance

Actual DNS, publicly trusted certificates/renewal, host firewall and Docker exposure, filesystem primitives/durability, VPS memory/disk headroom, off-host backup transfer and independent key custody, measured restore time on replacement hardware, real-provider egress behavior and required permissions remain explicit deployment checks. No local test may mark them satisfied.

## Official references consulted

- PostgreSQL SQL dumps: https://www.postgresql.org/docs/18/backup-dump.html
- PostgreSQL filesystem backups: https://www.postgresql.org/docs/18/backup-file.html
- ASP.NET proxy trust: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0
- Caddy proxy and HTTPS: https://caddyserver.com/docs/caddyfile/directives/reverse_proxy and https://caddyserver.com/docs/automatic-https
- Docker firewall behavior: https://docs.docker.com/engine/network/packet-filtering-firewalls/
- restic backup/restore: https://restic.readthedocs.io/en/stable/040_backup.html and https://restic.readthedocs.io/en/stable/050_restore.html
- ListenBrainz API requirements: https://listenbrainz.readthedocs.io/en/latest/users/api/index.html
- MusicBrainz licensing: https://musicbrainz.org/doc/MusicBrainz_Database
- Last.fm terms: https://www.last.fm/api/tos
