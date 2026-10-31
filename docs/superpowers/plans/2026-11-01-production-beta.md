# Production beta implementation plan

**Goal:** Produce and verify a complete v0.5.0 beta artifact with secure Production operation, consistent backup/recovery and bounded resource use.

**Architecture:** Single Linux host, Compose, Caddy, one web host and private PostgreSQL. Reuse database jobs and the existing storage publication/admission protocol. Keep mail transport and automatic weekly onsite scheduling outside the approved beta boundary; provide explicit production weekly generation through the operator.

**Tech stack:** .NET 10, EF Core/Npgsql, React/Vite, Docker Compose, Python standard-library operator automation, restic, systemd and Playwright. Verify official stable releases, image digests and compatibility before selecting new pins; retain MediatR 12.5.0.

**Estimated time:** Multi-session engineering work. Steps below are small authoring/check actions; image builds, PostgreSQL suites, crash tests and restore rehearsals can take substantially longer than five minutes. Do not infer completion time from the number of checklist entries.

**Approved design:** `docs/superpowers/specs/2026-11-01-production-beta-design.md`.

## Ownership, decisions and prerequisites

One integrator executes sequentially in the current checkout. No project issue tracker is configured for this work; this file is the task/decision record. Do not fabricate tracker claims or start parallel writers. Preserve the pre-existing local commits and never push.

| Decision | Owner and evidence | Resolution | Unblocks |
|---|---|---|---|
| Host architecture | Developer; approved design | Single-host Compose, existing background services | Tasks 1–3 |
| Backup consistency | Developer; approved maintenance windows | Stop all declared writers; matched logical dump/files/keys; encrypted repository | Tasks 5–7 |
| Capacity contract | Integrator; current witness/root binding and quota tests | Validated common policy, mismatches stop writes, explicit verified policy transition | Task 4 |
| Weekly behavior | Developer; written-design approval | Explicit operator generation; preserve email scheduler eligibility and capture semantics | Task 8 |
| New tool versions | Integrator; official releases, checksums/digests and compatibility | Resolve and record before installing or pinning in Task 1 | Tasks 1, 2, 6 |
| Infrastructure | Developer, later | Domain, VPS, off-host repository and independent recovery-key custody remain deployment checks | Public deployment only |

Prerequisites:

- [ ] Record HEAD, clean/unrelated changes and available disk; use only uniquely named disposable databases/volumes for tests.
- [ ] Verify Docker/Compose, SDK, Node/npm, browser executable and restic availability using their actual help/version output. Do not attach diagnostics to a discovered live PID.
- [ ] Read current `AGENTS.md`, `MEMORY.md`, `PROGRESS.md` and diagnostics instructions; preserve existing provider, scoring, security and storage contracts.
- [ ] Locate official stable release metadata for new build/proxy/backup tools, verify compatibility, and record chosen pins. Do not bulk-upgrade unrelated dependencies.
- [ ] Obtain implementation-plan approval and execution-method selection before changing product code.

## Dependency and commit order

`1 → 2 → 3`; `4 → 5 → 6 → 7`; `3 + 5 → 8`; `3 + 5 + 6 + 8 → 9`; all tasks feed `10 → 11`.

Execute sequentially. Commit groups: artifact (1); Production configuration/topology (2–3); capacity and verification (4–5); backup/recovery (6–7); Worker/operations (8–9); integrated acceptance/docs (10–11). A group is committed only after its relevant checks pass. Do not commit failing verification without asking the developer.

## Task 1: Build the complete frontend in the release image

Files: modify `Dockerfile`, `.dockerignore`, `.github/workflows/ci.yml`; create `scripts/checks/production_artifact.py` and `scripts/checks/test_production_artifact.py`. Reuse `src/Presentation/ClientApp/package-lock.json`, `scripts/licenses.mjs` and `scripts/publish-local.mjs`; modify those only if their current publication semantics prevent deterministic packaging.

1. Write an artifact check that parses published `index.html`, resolves every local script/style reference, rejects missing/empty assets and verifies required font licenses. Unit-test valid output and missing JS/CSS/license cases using temporary directories.
2. Run `python3 -m unittest discover -s scripts/checks -p 'test_production_artifact.py'`. Establish the original clean-checkout image failure with the new check; a missing check itself is not proof of the original defect.
3. Add a pinned frontend stage with the concrete build sequence below. Exclude host-generated `wwwroot/home-assets` from input; explicitly copy stage output into the .NET publish output before forming the web image. Preserve other tracked legacy assets and the existing default web target.

```dockerfile
# Within the pinned Node stage after copying package.json/package-lock.json:
RUN npm ci
# After copying the tracked ClientApp source and font/license inputs:
RUN npm run build
```

4. Build from a temporary `git archive HEAD` tree plus the reviewed task diff, never the developer working directory's ignored outputs. Verify the image filesystem with the artifact check; launch and request every referenced JS/CSS asset and reject SPA HTML masquerading as JS. Record the image ID and input fingerprints.
5. Run frontend typecheck/build and the targeted artifact tests; wire equivalent clean-artifact verification into CI, then commit only Task 1 files.

## Task 2: Define fail-closed Production configuration and persistent keys

Files: create `src/Presentation/Production/ProductionConfiguration.cs`, `tests/Presentation.Tests/ProductionConfigurationTests.cs`; modify `src/Presentation/Program.cs`, `DependencyInjection.cs`, and `.gitignore` only for private production paths.

1. Write parameterized tests rejecting wildcard/missing hosts, non-HTTPS or mismatched origins, untrusted/invalid proxy configuration, fixture generation, local live-settings loading, local email capture, weak/missing signing configuration, and missing storage-admission settings. Include valid single-origin and paused-consumer configurations.
2. Run `/home/mahammadjafarli/.dotnet/dotnet test tests/Presentation.Tests/Presentation.Tests.csproj --filter FullyQualifiedName~ProductionConfigurationTests -m:1`. Observe the new boundary failing before implementation.
3. Implement one host-boundary validator. Reuse existing JWT/Origin/provider option validators; never put deployment settings into Domain. Invoke before loading development-only local settings or starting background consumers. Keep offline operator inspection independent of live-provider reachability.
4. Configure the default ASP.NET keyring on a private durable path with a stable discriminator; preserve existing unsubscribe/capture purposes and stored key compatibility. Test restart persistence of antiforgery protection; retain existing auth/token tests.
5. Run the new tests plus `Phase1SecurityTests`, `ListenerContextTests`, `Phase9SwaggerTests` and `ManualTestEnvironmentTests`. Existing Development behavior must still pass.

## Task 3: Add private Production topology and local HTTPS acceptance

Files: create `deploy/compose.production.yaml`, `deploy/compose.acceptance.yaml`, `deploy/Caddyfile`, `deploy/init-database.sql`, `scripts/checks/production_topology.py`, and `tests/Presentation.Tests/ProductionDatabaseRoleTests.cs`; modify Docker operator targets as needed without changing the local stack contract.

1. Add topology assertions: only proxy ports published; no public database/Kestrel port; no Development/local email settings; bounded memory/CPU/logs; durable state/key/proxy volumes; restart behavior declared; operator services not continuously running.
2. Add a disposable PostgreSQL role test proving the runtime role can authenticate, persist through accounting/publication triggers and recover jobs but cannot create/drop schema objects. Migrations use a separate role. Never rely on the runtime postgres superuser.
3. Implement the Compose/Caddy configuration with explicit public origin, allowed Host, fixed trusted proxy identity and private service networks. Keep egress available for approved providers. Mount operator settings/secrets privately and avoid expanded `compose config` output in evidence.
4. Add loopback-only HTTPS acceptance with a local CA, using the same web image and middleware. Test legitimate requests, HTTP redirection, forged Host/Origin and forwarding headers, CSRF rejection, secure cookies and hidden development routes. Trust the test CA; do not disable browser certificate verification globally.
5. Run `python3 scripts/checks/production_topology.py --check` and the targeted role/security tests. Commit Tasks 2–3 after both pass.

## Task 4: Bind configurable capacity to the storage contract

Files: modify `src/Infrastructure/Storage/LocalGenerationStorage.cs`, `CoordinatedGenerationStorage.cs`, `src/Infrastructure/DependencyInjection.cs`, and affected storage tests; create `tests/DataAccess.Tests/StoragePolicyPostgresTests.cs`.

1. Add tests at below/equal/above cutoff, reservation rollback, concurrent writers and differing writer budgets. Preserve existing exact row/accounting and filesystem headroom tests.
2. Define the complete options contract for the new value:

```csharp
// GenerationStorageOptions; retain the existing default for compatibility.
public long StopBytes { get; set; } = 1_000_000_000;
```

Validate before admission: positive bounded value, checked arithmetic, and no write at or beyond the cutoff. All production writers must bind the same effective capacity to the database witness. A changed value must not be silently accepted by an existing witness.

3. Introduce a versioned policy fingerprint incorporating canonical roots and the effective cutoff. Keep the existing database column/schema if its constraints support the versioned hash; add a migration only if required for an explicit representation. Inventory validity and schema/table coverage checks remain unchanged.
4. Update every admission/reclamation/publication quota comparison to use the validated effective value. Preserve the legacy constant as a compatibility default if existing fixture references require it, never as an alternative runtime limit. Reject an incompatible policy before writes.
5. Run targeted Infrastructure storage suites and `StoragePolicyPostgresTests`, `StorageRecoveryPostgresTests`, `ManagedPublicationCrashPostgresTests`, and accounting tests with a real disposable PostgreSQL connection. Record skipped PostgreSQL checks as unverified, not passing.

## Task 5: Add operator verification and explicit policy transition

Files: create `src/Worker/StorageOperator.cs`, `tests/DataAccess.Tests/StorageOperatorPostgresTests.cs`; modify `src/Worker/Program.cs`, `scripts/reconcile_managed_storage.cs` and storage internals only as required for verified establishment.

1. Specify exact operator verbs: `verify-storage` never adopts state; `establish-storage` requires stopped writers; `change-storage-policy` verifies the old trusted policy/artifact baseline before establishing the explicitly requested new policy. Configuration is supplied by private mounted file/environment, not secrets in arguments.
2. Test missing schema/witness, changed/missing/additional artifacts, wrong roots, invalid ledger, mismatched policy and active publications. Every refusal must leave data, ledger and witness unchanged. Test that explicit verified legacy-policy transition preserves user rows and content hashes.
3. Implement verification through `IManagedStorageIntegrity`, `IDigestGenerationStore`, existing coordinated leases and trusted-witness artifact verification. Do not implement `verify` by calling an adopting reconcile method. Avoid nested advisory-lock acquisition/deadlock.
4. Require explicit expected baseline for transitions/restores. Reuse `ReconcileVerifiedAsync` where its contract fits; add a narrow verified operator path where ordinary runtime policy rejection would prevent inspecting the old policy. No general runtime mismatch bypass.
5. Run operator tests, storage startup child-process tests, crash recovery and original admission suites. Commit Tasks 4–5 only after these pass.

## Task 6: Capture consistent backups and encrypt repository storage

Files: create `scripts/ops/production.py`, `scripts/ops/test_production.py`, `deploy/systemd/linernotes-backup.service`, `deploy/systemd/linernotes-backup.timer`; add an operator container target/tool pin if needed.

1. Define a standard-library CLI with explicit `backup`, `restore`, `upgrade`, `status` and `resume` subcommands. Use subprocess argument arrays, bounded timeouts, a host lifecycle lock and a durable maintenance marker. Never log private subprocess output or use `shell=True`.
2. Unit-test competing operations, malformed config, staging nested in managed roots, symlinks/unsafe paths, insufficient scratch headroom, stop/dump/copy/upload failures, interrupted publication and restart refusal during incomplete upgrades. Fixtures must own their paths/processes.
3. Implement stopped-writer capture: freeze timer-triggered work; stop all declared writers; verify storage; reject pending publications; obtain custom-format `pg_dump`; copy matching managed files/inventory/default keys and reconstruction settings; record hashes, schema/policy/image/PostgreSQL identity. Use private permissions and atomic complete-set publication outside managed roots.
4. Validate full dump readability, not only archive listing. Resume only the verified original safe image after complete capture; keep immutable staging for restic upload. Emit bounded status separately for capture and encrypted repository completion. Failed upload must not replace last-good backup freshness.
5. Invoke pinned restic with repository credentials/password from private files. Establish bounded scratch and repository retention; do not delete last-good recovery evidence after a failed operation. Run `python3 -m unittest discover -s scripts/ops -p 'test_*.py'` and disposable integration capture tests.

## Task 7: Rehearse restoration and upgrade failure recovery

Files: extend `scripts/ops/production.py`, `scripts/ops/test_production.py`; create `scripts/checks/production_recovery.py`, `tests/DataAccess.Tests/ProductionRestorePostgresTests.cs`.

1. Create a disposable source with two accounts, taste/feedback/history, packed score receipts, managed artifacts and keys. Capture its row fingerprints, artifact hashes and successful protected-token behavior without recording private payloads.
2. Restore the restic snapshot to a fresh target and validate manifest membership, hashes and safe paths. Restore PostgreSQL with error-stop behavior and verify schema, ledger and logical row fingerprints before explicit physical-inventory establishment. Reject nonempty targets; never run automatic destructive replacement.
3. Launch the exact source image with jobs paused and admission enabled; test ownership, explanation decoding, exports and subsequent admitted writes. Restore key material and prove existing protected material remains decryptable. Restart and repeat the critical reads.
4. Exercise truncated dump, artifact corruption, mixed backup generations, missing keys, insufficient space, interrupted restore, pending publication and wrong policy. Assert original source/backup is preserved and an unsafe target never becomes ready. Rehearse failed migration/startup and recovery using the matched pre-upgrade set.
5. Run `python3 scripts/checks/production_recovery.py --disposable` plus targeted restore/storage suites. Record actual capture downtime and restore duration. Commit Tasks 6–7 when the complete round trip and failure checks pass.

## Task 8: Enable bounded mail-free production weekly generation

Files: modify `src/Worker/Program.cs`, `LocalWorkerOptions.cs`, `LocalDigestWorkerRunner.cs` or add a narrowly scoped production runner; create `tests/DataAccess.Tests/ProductionWorkerPostgresTests.cs`.

1. Test Production accepts only an explicit bounded generation request for one user/week with live-enabled approved provider configuration and storage admission. Reject capture, legacy schedule, Recorded/Synthetic production generation, absent user/week and unsafe config.
2. Preserve Development CLI behavior and local email-scheduler tests. Do not change `WeeklyScheduleStore` eligibility, `EmailConfirmed`, email consent or schedule advancement. Generation does not claim delivery.
3. Reuse `GenerateDigestCommand` and current provider resolver, shared preparation, receipts and admission. Run required storage startup/recovery before one-shot generation; building a generic host without starting hosted services is insufficient. Bound execution time and emit only approved result fields.
4. Test repeat user/week idempotency, cancellation/retry, concurrent web/Worker admission, mismatched policy, shared upstream pacing and correct final receipt decoding. Use disposable fixtures for deterministic behavior and separately label real-source verification.
5. Run new Worker tests plus existing `LocalWorkerOptionsTests`, `LocalWorkerPostgresTests`, `LocalScheduleOptionsTests`, `LocalScheduledWorkerTests` and `Phase8ScheduledWorkerPostgresTests`.

## Task 9: Expose bounded operational health without private data

Files: create `src/Presentation/Production/OperationalHealth.cs`, `tests/Presentation.Tests/OperationalHealthTests.cs`; modify `Program.cs`, existing background consumers and `StorageRecoveryService.cs`; extend operator status and add `deploy/systemd/linernotes-status.service`/`.timer`.

1. Test process liveness separately from database/storage readiness and background degradation. Upstream failure must not cause liveness failure or hide saved history. Public health bodies expose no identities, paths, secrets or provider payloads.
2. Implement cached bounded operational state for startup/admission failures, worker heartbeats, oldest ready work, cleanup/recovery progress, disk headroom and backup age/outcome. Reuse current diagnostic instruments and bounded failure codes. Never contact upstream or hash all artifacts on every health probe.
3. Keep diagnostic detail private: proxy denies operator endpoints, and operator status uses private access. Preserve the existing `/health` compatibility where callers need it; add explicit liveness/readiness semantics with tests.
4. Connect consumer/recovery outcomes to health state and structured logs. Set bounds on status payloads, sampling frequency and log retention. Status timer returns nonzero for stale/failed backup or degraded storage; actual external alert delivery is a deployment configuration requirement.
5. Run health tests and affected background/storage-recovery suites. Commit Tasks 8–9 after Worker and health checks pass.

## Task 10: Verify the exact image through HTTPS and real sources

Files: create `scripts/checks/production_acceptance.py`, `src/Presentation/ClientApp/tests/production.spec.ts`; modify CI and Playwright configuration only to support the verified environment without replacing image behavior.

1. Build the final image from tracked inputs and record its immutable ID. Run topology/security checks and the complete deterministic backend/frontend regression suites. PostgreSQL tests must actually execute; report skips explicitly.
2. Launch that image behind the acceptance proxy with Production configuration, test-CA trust and isolated persistence. Test asset loading, security headers, auth, guest handoff, ownership, feedback/history, explanation rendering, export/deletion, restart persistence and readiness failure states.
3. Deterministic Application/Infrastructure tests may use injected fixtures. Exact production-image acceptance must not add a runtime synthetic bypass or replace source adapters. Where live credentials are unavailable, test unavailable behavior and report source-dependent journeys unverified.
4. With authorized live access, run a bounded public catalog smoke and one disposable-account browser generation. Correlate the submitted operation/job and persisted new picks; reload and restart to prove persistence. Verify valid artwork or genuine unavailable fallback. Never use old cards as proof of new generation; never alter scoring/evidence or store provider tokens/bodies in evidence.
5. Run the recovery rehearsal against the final image. If a fix changes the image, rebuild and repeat the affected image/browser/recovery acceptance; report which evidence belongs to which image.

## Task 11: Security review, evidence and operator handoff

Files: create `docs/operations/production-beta.md`, `docs/operations/production-beta-evidence.md`; update `CHANGELOG.md`, `PROGRESS.md`, `docs/renewal/phase-9-release-checklist.md` and applicable CI/runbook links.

1. Run locked restores, dependency advisory/secret checks and relevant HTTP/security regression tests. Validate findings against exploitable paths. Explain removal-versus-upgrade before any dependency removal; do not weaken failing gates.
2. Audit active/persisted provider provenance and public attribution/export paths against official ListenBrainz/MusicBrainz/Last.fm documentation. Keep permission decisions separate from technical smoke results. Do not contact providers or assert legal clearance.
3. Document clean build, secret/key setup, initial database/role establishment, explicit policy establishment, start/status, backup/restore, upgrade/rollback and maintenance recovery commands. Give each procedure one canonical home. Include off-host key custody and failure instructions; do not embed real settings or credentials.
4. Record revision/image identities, commands, versions, fixture/live origins, pass/fail/skip counts, measured backup/restore timings and residual limitations. List DNS/public TLS, VPS firewall/durability/resources, off-host transfer, external alerts and provider permissions as deployment-dependent until verified there.
5. Run `git diff --check`, documentation/link checks and only the additional regression justified by final changes. Audit all nonignored files, commit only task files, and report local commits plus remaining developer/deployment acceptance. Do not push, tag or publish v0.5.0.

## Execution and stop conditions

Recommended execution: sequentially in this session with one integrator. An alternative is a fresh session using this plan and approved design; no external provider orchestration or delegated agents are assumed.

Stop the affected implementation slice for design review if it requires weakening external-change detection, admitting partial recommendation batches, deleting user inputs/history, changing scoring or email consent, replacing the storage protocol, adding distributed infrastructure or changing the approved backup consistency model. Continue independent authorized tasks where possible.

Missing local Docker/browser/tool access or live-provider credentials must be reported with the exact affected checks. Never substitute a synthetic run for final live/artifact evidence, bypass tests or call unexecuted checks passing. The project is not production-confirmed until the developer accepts it and deployment-specific checks are completed.
