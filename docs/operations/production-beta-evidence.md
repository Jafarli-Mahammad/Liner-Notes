# Production beta engineering evidence

This record distinguishes local implementation checks from production acceptance.
The approved scope and execution order are in the [design](../superpowers/specs/2026-11-01-production-beta-design.md) and [plan](../superpowers/plans/2026-11-01-production-beta.md).

## Release packaging

The original tracked tree at `972d536` failed the new artifact check with `missing_release_asset`: tracked HTML referenced ignored React outputs. The new validator's four tests cover complete output, missing/empty assets/licenses, unsafe references and HTML returned instead of JavaScript.

The Docker frontend stage uses Node 26.13.0, verified against the [official release index](https://nodejs.org/dist/index.json), and the official `node:26.13.0-bookworm-slim` manifest digest `sha256:bc0a2afd3b74ce12222fc8bab99271e66257f021078f5f4a5d687a77d280c1d9`. It runs locked npm installation, TypeScript checking, Vite build and the existing runtime-license collector. Generated host assets are excluded from build input.

Local checks on 2026-11-01:

- `python3 -m unittest discover -s scripts/checks -p test_production_artifact.py`: four passing tests.
- `python3 scripts/checks/production_artifact.py --build --tree 4e098def02fa9318db545d21418941df1fc0a950 --tag linernotes-production-beta:packaging`: clean Git-tree image build and complete asset/license verification passed. The staged tree contains the reviewed packaging changes; it has no ignored developer outputs.
- The same image ran in a disposable loopback container with provider jobs disabled and no database use. `production_artifact.py --image ... --url ...` fetched HTML and both assets and compared their SHA-256 hashes to the image contents; all passed. This was packaging verification under Development, not Production acceptance.

Production topology, recovery and live-provider acceptance remain separate plan tasks; packaging alone does not establish readiness.

## Production host and topology

- 45 targeted Presentation tests passed with a disposable PostgreSQL fixture, including Production settings, transport/authentication/CSRF, hidden development routes and file-backed key decryption after service-provider restart.
- The restricted-role PostgreSQL test passed: guarded guest creation and full managed-accounting verification succeed with DML privileges; schema creation is rejected. This reuses the existing DataAccess coordination fixture rather than duplicating database setup in Presentation tests.
- Three topology tests passed against resolved Compose JSON: only proxy ports are public, acceptance is loopback-only, runtime containers are hardened, and services have resource/log limits. The configuration is not a provisioning or host-firewall test.
- Production routing-only WebApplicationFactory fixtures explicitly replace storage startup; real startup, witness and role coverage remain separate PostgreSQL tests. No runtime fixture/synthetic bypass was added.
- Caddy 2.11.7 and restic 0.19.1 were verified against their official GitHub latest-release metadata on 2026-11-01. Caddy's official Alpine image is pinned by manifest digest. Actual TLS and final-image browser checks are later acceptance tasks.
- The pinned Caddy container accepted `deploy/Caddyfile` with `caddy validate`. No public certificate was requested and no public service was started.

## Storage policy and operator verification

Capacity now defaults to the existing 1,000,000,000 bytes but is configurable. The versioned witness fingerprint includes capacity, reservation overhead, artifact roots and inventory/lease paths. Ordinary writers refuse legacy or mismatched fingerprints. A verified explicit stopped-writer transition is required; startup never adopts unexplained changes.

`verify-storage` verifies ledger/table coverage and the trusted artifact baseline without publishing a new witness. `establish-storage` refuses an existing witness; `change-storage-policy` requires the previous policy and matching artifacts; `restore-storage` also requires the expected snapshot inventory. The old establishment script no longer silently re-establishes an existing witness.

Local verification: 29 targeted Infrastructure storage checks and 60 PostgreSQL coordination/accounting/publication/security checks passed. The legacy-policy upgrade test retains the existing guest profile. Tests cover mismatched writer capacity, unchanged witness during verification, unexplained artifacts, invalid restore manifests, explicit quota transitions and transactional rollback when witness establishment exceeds quota. Another 27 host/startup/security checks passed, and the file-based establishment helper built successfully with its supported CLI invocation. These are disposable local fixtures; complete backup/restore and final-image checks remain later tasks.
