# Production beta engineering evidence

Local checks on 2026-11-01. No infrastructure was purchased, public service
deployed, changes pushed or release published. Developer acceptance remains
separate. See the [approved design](../superpowers/specs/2026-11-01-production-beta-design.md),
[plan](../superpowers/plans/2026-11-01-production-beta.md) and
[operating procedure](production-beta.md).

## Implemented scope

The original tracked tree at `972d536` failed the asset check with
`missing_release_asset`: HTML referenced ignored React outputs. Docker now builds
React and license notices from locked, tracked inputs. The checker follows
referenced scripts, styles, images and fonts. Byte-identical builds have not been
established.

Production rejects development/synthetic settings, unsafe host/origin/proxy
configuration and absent storage admission. Only Caddy publishes ports. Runtime
services are non-root with read-only roots, bounded tmpfs, resource limits and
rotating logs. Authentication keys persist separately. The runtime database role
has guarded DML privileges; schema administration uses the operator role.

Capacity defaults to 1,000,000,000 bytes and is configurable through a writer-agreed
policy. Verification, first establishment, policy transition and restore are
separate operator actions. Existing hashes, accounting, leases, ownership,
publication recovery and external-change detection remain enforced. Ordinary
startup never adopts unexplained content.

Backup serializes lifecycle operations, stops writers, verifies integrity and
captures PostgreSQL, managed files, keys, reconstruction settings and exact images.
Restic encrypts and verifies the repository. Independent repository credentials
are excluded; retention and scratch cleanup are bounded. Restore requires empty
target volumes and validates the complete payload manifest.

Logical restore changes physical database identities and can rewrite equivalent
schema representations. Runtime OID/file-node checks remain strict. Backup derives
the portable restore proof by restoring the captured dump's schema into a temporary
database. Explicit restore verifies that proof, logical counters, the original
witness and artifact hashes before establishing a new physical witness. No regex
normalization or automatic schema adoption was introduced.

Weekly Production generation is an explicit bounded live-provider command. Automatic
all-account scheduling and mail delivery are excluded. Operational probes report
readiness, queue age, expired leases, paused jobs, recent background failures and
backup state. Systemd templates invoke backup/status; delivered notifications
still need infrastructure configuration.

## Verification

| Check | Local evidence |
|---|---|
| Backend | Five projects, **909 passed**, zero failures/skips; disposable PostgreSQL and checked TRX results |
| Operator/checkers | **14 operator tests**, **11 production-check tests**, resolved production/acceptance topology passed |
| Framework/model | SDK **10.0.403**, matching Microsoft **10.0.14** packages/tool, locked restore; EF reports no pending model changes |
| NuGet | Eleven projects including transitive dependencies: zero reported advisories |
| HTTPS/proxy | Exact asset bytes compared through local-CA HTTPS; private routes, probes, forwarding, Host and Origin tested |
| Live browser | Five picks, explanations, feedback/history, exports, registration preserving taste, login and disposable account deletion; no page exceptions |
| Weekly | Five live-provider picks; repeat returned the same digest ID |
| Recovery | **29 tables** compared; password login, three taste signals, pre-backup protected token, export and restart integrity passed |
| Real-pick recovery | Five weekly recommendations and five stored breakdowns retained in a separate matched restore |
| Compatible upgrade | Verified pre-upgrade recovery set, no pending migrations, storage verification and new-image readiness passed |

These include checks performed in stages; the final artifact record below identifies
the candidate covered by final acceptance. HTTP-only test factories stub storage
startup explicitly; real startup, database roles and recovery are tested separately
with PostgreSQL and Docker. No release-image fixture bypass was added.

## Findings caught during verification

NuGet-only auditing missed the bundled framework. Trivy **0.76.1** found **two
critical and ten high** advisories in the 10.0.12 web image. The new security gate
reproduced that rejection. Official Microsoft metadata and NuGet indexes verified
stable runtime/framework **10.0.14** and SDK **10.0.403**; pins and locks were updated.
The patched web and Worker images had zero high/critical findings.

The SDK image also contained Ubuntu `libbrotli1` **1.1.0-2build2**, affected by
**CVE-2026-111497**. The fixed **1.1.0-2ubuntu0.1** package is installed from the
signed Ubuntu **20261101T000000Z** snapshot. HTTPS and by-hash fetching address an
inconsistent HTTP mirror index without disabling verification. No public exploit
path was established; the library was upgraded rather than removed. Sources:
[Microsoft releases](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json),
[Ubuntu advisory](https://ubuntu.com/security/CVE-2026-111497),
[Ubuntu snapshots](https://snapshot.ubuntu.com/).

Managed erasure previously returned incomplete whenever delivery was disabled.
It now validates copy roots independently of delivery settings. Tests cover owned
file deletion, the other account's retention, idempotency and rejection of shared
root permissions. Unverifiable files and unmanaged legacy cleanup still fail closed.

## Deployment-dependent and unverified

- Off-host upload/recovery and independent credential custody; rehearsals used local repositories.
- Actual VPS capacity, disk/WAL headroom and recovery speed. Fixture timings are not capacity or production latency claims.
- DNS, public certificate issuance/renewal, IPv6/firewall exposure, installed timers and delivered failure notifications.
- Schema-changing upgrades: these require a separately reviewed migration/witness transition and recovery rehearsal; automation refuses them.
- Provider permission and metadata/artwork rights. A live response or approval flag is not blanket permission. Last.fm obligations apply if its adapter is enabled. MediatR 12.5.0 retains its approved licensing exception.
- Remote GitHub Actions execution and developer acceptance. No push was authorized.

The host reboot cleared earlier `/tmp` fixtures/logs; persistent private fixtures
were recreated and checks repeated. Docker network changes caused host Chromium
`ERR_NETWORK_CHANGED`; browser acceptance runs after lifecycle operations finish.
Existing pruning and Microsoft.StringTools framework warnings remain visible;
tests, model consistency and actual migrations passed. No unrelated dependency
removal was performed.

## Final exact-image acceptance

Tested source tree: `03c0e4c65ebc4e2caf397942f99897ce616ad33b`.
This is the exact tree of local implementation commit `8851dcb`.
All four images carry that revision and `0.5.0-beta` labels. The image set was
built from an exported Git tree, without ignored developer assets or private input.

| Image | Immutable local image ID |
|---|---|
| Web | `sha256:840978bc6c652e4b18d58bc8697caf3b1e88c13fe1579b4039860ac5705b4271` |
| Worker | `sha256:a30d32c1f639649c212a48af569ffedc45616721c1fe847a2a73d361dfe81b05` |
| Migration tools | `sha256:04149c72e9a2b4740337ff800ab343df54f49f027261eaa14b1fc190b8295270` |
| Volume initializer | `sha256:8c443b8bcc961ce3bdfb8adf703c876ad4e6da05ff243de9963ce432f9779475` |

- Trivy scanned the exact web, Worker and migration-tool archives: **zero high
  and zero critical findings** in each. The initializer shares the runtime base;
  it was exercised by provisioning/recovery rather than separately scanned.
- The exact web image served all seven compared HTML/assets byte-for-byte through
  HTTPS. Four private routes were denied; health, forwarding, Host and Origin
  checks passed. The real-provider journey and Production browser smoke both passed.
  The real journey produced five picks, no page exceptions, successful account
  deletion and **no operator-cleanup warning**.
- Production weekly generation for a second week produced another five picks;
  repeating the command returned the same digest ID. A matched backup/restore
  compared 29 tables and retained **ten recommendations and ten stored breakdowns**.
  Authentication, pre-backup protected token, export and restart integrity passed.
  A second restore into the nonempty target was refused without changing data.
- The automated clean fixture recorded 13.1 seconds of backup capture, 25.6 seconds
  through verified repository completion, and 28.6 seconds for restore acceptance.
  Live-history recovery/acceptance took 36.2 seconds. These small local fixtures
  establish recovery mechanics, not production capacity or recovery-time guarantees.
- The compatible-upgrade command took a verified backup, checked migrations and
  integrity, and reached readiness with this exact image set.

Sanitized reports, TRX files, private disposable configuration and encrypted local
repositories are under `~/.local/state/liner-production-beta-20261101/`. They are
not source inputs, public artifacts or an off-host backup. Credentials are not
included in this document.

To rebuild the tested source:

```sh
python3 scripts/ops/build_release.py --tree 8851dcb --output release-images.json
```

Repeat artifact/security/recovery acceptance for the new image identities. The
following documentation commit does not alter the tested application, deployment
or operator code.
