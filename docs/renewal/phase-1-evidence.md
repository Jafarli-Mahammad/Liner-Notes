# Phase 1 evidence

## Baseline

2026-10-06, `Prism` at `10d97f9`, initially clean. SDK 10.0.112 / runtime 10.0.12.
`dotnet test 'Liner Notes.sln' --no-restore --filter 'FullyQualifiedName!~FounderSeedCoverageSpikeTests'`
passed; counts are recorded in [baseline summary](evidence/baseline.txt).
Sandbox IPC initially failed; rerun outside sandbox was approved. No live API calls made.
The excluded test can read local credentials and writes hard-coded coverage conclusions;
its removal/replacement requires approval. Filtering this run does not certify that test.

## 1A

Commit-set `renewal-1a`. `python3 scripts/checks/renewal_docs.py` failed on personal notes
before the rewrite ([red](evidence/1a-red.txt)), then passed ([green](evidence/1a-green.txt)).
Reconciled README, progress, agent guidance and specification precedence. Removed personal taste
notes and obsolete pillar; replaced the unsupported coverage report with a provenance qualification.
No scorer/provider changes. Developer confirmation pending.

## 1B

Commit-set `renewal-1b`. `python3 scripts/checks/secret_defaults.py` reproduces the tracked
credential fallback ([red](evidence/1b-red.txt)), then passes ([green](evidence/1b-green.txt)).
Removed JSON and DataAccess fallback credentials; missing connection configuration fails startup.
Added placeholder/environment guidance and ignore rules. [Rotation](local-configuration.md) remains
an operator action, not claimed performed. No credential values were copied into evidence.

The developer subsequently requested default passwords for now. Presentation and Worker now carry
an explicit `postgres` password for loopback-only Development. Production configuration remains
blank, and the known Development signing key is rejected outside Development. Application accounts
still choose their own passwords; external credential rotation has not been performed.

## 1C–1I

1C stores only a hash of each refresh token with expiry and Identity security stamp, redeems it
with a compare-and-swap update, and rejects legacy plaintext or expired tokens. 1D uses Identity
lockout after five failed logins for 15 minutes and a 10/minute IP auth limiter. 1E wraps Identity,
subscriber and seed creation in one retried database transaction. 1F physically deletes owned
records and credentials together and rejects access tokens for deleted accounts. 1G locks the
recommendation row, replaces its feedback signals atomically, and rejects contradictory ratings;
familiarity is independent of rating. 1H keeps browser tokens in memory, coalesces refreshes,
guards logout races, removes inline event handlers, and clears private data on logout. 1I sets
explicit CORS, security headers, HTTPS/HSTS in production, size limits, and no-store API responses.

The offline solution suite passed on 2026-10-07 with 202 tests (Domain 77, Application 47,
Infrastructure 14, DataAccess 26, Presentation 38), including local PostgreSQL security tests.
The live Last.fm coverage spike remained excluded; no live calls were made. The PostgreSQL tests
use a disposable database with `EnsureCreated`, so they do not validate a production migration.
`node scripts/checks/phase1_browser.cjs`, `python3 scripts/checks/secret_defaults.py`, and
`python3 scripts/checks/renewal_docs.py` passed. Developer runtime confirmation is pending.

## Greptile follow-up

Fixed stale login/registration response races, feedback failure messaging, rating highlight clearing,
and rating a familiar track now preserves its familiarity feedback. Configured forwarded scheme/client
IP handling for explicitly trusted proxy IPs before HTTPS redirection and rate limiting. Updated the
existing rating migration to add its Identity FK as PostgreSQL `NOT VALID`, preserving legacy domain
rows during upgrade while enforcing the FK for new/changed rows. A local PostgreSQL migration test
upgrades a populated InitialCreate database and verifies both preservation and future enforcement.

The FK remains unvalidated for pre-existing domain users without Identity rows. This avoids inventing
credentials or deleting user data; account reconciliation and FK validation remain a separate follow-up.
`node scripts/checks/phase1_browser.cjs` covers auth response races, feedback payload/UI behavior and
HTTP failure reporting. The proxy integration test confirms a trusted forwarded HTTPS request avoids
redirection. All 202 offline tests passed; no live Last.fm calls were made.

## 1J

The live NuGet audit found AutoMapper 13.0.1 (direct in Application, transitive elsewhere) and
System.Security.Cryptography.Xml 9.0.2 (transitive through DataProtection) with high-severity advisories.
Audit output is local at `/tmp/liner-dependency-audit.json`.

Narrow remediation implemented after the developer directed Phase 1 to continue:
- Remove Application's AutoMapper 13.0.1 reference. Reuse the existing explicit typed DTO projections
  in `Common/Mappings/MappingExtensions.cs`, adjust five handlers and DI, and preserve mapping/export
  value assertions. No export shape or field changes.
- Add an explicit System.Security.Cryptography.Xml 10.0.12 reference to DataAccess, the common
  DataProtection consumer, lifting its transitive Pkcs dependency to 10.0.12 as well.
- Kept existing target frameworks, EF packages and all other dependencies unchanged. Restore,
  compilation and the offline suite passed. The repeat NuGet audit returned no vulnerable packages.
  NU1510 warns that the direct XML package will not be pruned; this pin is retained to lift the
  vulnerable transitive version until a broader dependency update is approved.

Sources checked during this work: [AutoMapper advisory](https://github.com/advisories/GHSA-rvv3-g6hj-g44x),
[XML cryptography advisory](https://github.com/advisories/GHSA-6588-8gv4-xfgh), and
[XML cryptography 10.0.12 package](https://www.nuget.org/packages/System.Security.Cryptography.Xml/10.0.12).
This avoids adopting a different AutoMapper licensing arrangement. No broader dependency change
was made.

## 1K

Removed 816 generated `bin/obj` paths from the Git index while leaving working copies available.
Existing `.gitignore` rules already exclude them. Committed locally as `4f4d24a`.
Unrelated working-tree changes to Git hooks and reviewer scripts were left untouched.
