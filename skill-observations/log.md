# Skill Observation Log

Status: OPEN = not yet actioned; ACTIONED = updated; DECLINED = not pursued.

## 2026-10-12

### Observation 001: Preflight transitive workflow skills before starting
**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Approved multi-phase development workflow.
**Skill:** brainstorming / writing-plans
**Type:** open-source
**Phase/Area:** Skill dependency preflight
**Issue:** The architectural workflow required planning and execution skills that were absent from the installed catalog. Resolving them only at the handoff delayed implementation after design approval.
**Suggested improvement:** Check required transitive skill files when selecting the architectural path; identify an authoritative readable fallback before asking for design review.
**Principle:** Verify workflow prerequisites before spending the user's time on staged approvals. Preserve already selected execution order across handoffs.

### Observation 002: Privacy tests need value and failure-boundary proof
**Status:** OPEN
**Date:** 2026-10-12
**Session context:** End-to-end privacy verification and fresh review.
**Skill:** test-cases / executing-plans
**Type:** open-source
**Phase/Area:** Verification completeness
**Issue:** A complete column list and passing scalar tests initially missed populated nested snapshots, a later-page HTTP failure, and the before-operation baseline for another account's encrypted copy. Fresh review found the gaps despite a green suite.
**Suggested improvement:** Include an explicit checklist for every persisted subtree, partial-progress failures, and before/after ownership baselines; validate added regression checks with temporary wrong mutations when the underlying production behavior was already correct.
**Principle:** Schema coverage and pass counts do not establish complete privacy behavior; test the stored values and exact transaction/filesystem failure boundaries.

### Observation 003: Honor standing authorization for routine commands
**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Developer explicitly authorized the routine build/database verification command families.
**Skill:** executing-plans
**Type:** open-source
**Phase/Area:** Approval handling
**Issue:** Repeated command escalation questions during ordinary local verification created workflow friction even though the development and test operations were already authorized.
**Suggested improvement:** Carry standing command authorization across steps/turns, distinguish required sandbox mechanics from a new permission request, and reserve human review for concrete architectural decisions or unapproved external effects.
**Principle:** An approved execution scope should enable routine verification without repeated authorization prompts.

### Observation 004: Crash barriers must establish backend rollback
**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Local PostgreSQL process/restart verification.
**Skill:** test-cases / executing-plans
**Type:** open-source
**Phase/Area:** Deterministic crash testing
**Issue:** Killing a client while its backend waits in a controlled SQL trigger does not establish rollback. Releasing the barrier can let an autocommit update finish after client death; first-use database allocations can also invalidate a conservative inventory fixture.
**Suggested improvement:** Identify the exact test-owned backend/database, terminate and wait for its rollback before releasing a crash barrier, and provision keys/first-use row paths before inventory measurement. Permit only documented bounded operator reconciliation for the exact changed-inventory reason; keep product guards intact.
**Principle:** A crash test must prove the server-side durable boundary, rather than infer it from client process exit. Prepare operational side effects before measuring storage.

### Observation 005: Packaging must preserve local execution boundaries

**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Docker, Compose and CI inspection for an existing local batch application.
**Skill:** New skill candidate: release reproducibility
**Type:** open-source
**Phase/Area:** Packaging preflight
**Issue:** A web Dockerfile omitted shared target-framework properties before restore; Compose lacked its database, and CI could skip database checks. The batch executable deliberately rejected production mode and non-loopback databases, so a generic production topology would not represent implemented behavior.
**Suggested improvement:** Inspect shared build inputs, test skip conditions, executable guards, migration context and storage/key ownership before proposing deployment files. Package a supported local topology and distinguish reproducibility from production delivery and external approval.
**Principle:** Packaging should make existing runtime and verification contracts executable; containerizing a process does not establish a supported production workflow.

### Observation 006: Reproducibility pins follow the approved version policy

**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Developer corrected the release design's proposal to retain EF Core 9 after EF Core 10 was stable, and approved proceeding with latest stable packages/tools.
**Skill:** New skill candidate: release reproducibility
**Type:** open-source
**Phase/Area:** Dependency selection
**Issue:** Preserving an existing dependency graph was proposed as a default despite a newer stable framework release. Reproducibility and version freshness were treated as competing requirements.
**Suggested improvement:** Verify stable releases and compatibility first, upgrade under the authorized version policy, then freeze exact versions and lock files. Distinguish a verified compatibility exception from retaining an old version without investigation.
**Principle:** Reproducibility means pinning a deliberately chosen, verified graph; it does not require keeping an outdated graph.

### Observation 007: Latest transitive majors need executable compatibility checks

**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Stable dependency modernization and API documentation compatibility.
**Skill:** New skill candidate: release reproducibility
**Type:** open-source
**Phase/Area:** Dependency compatibility
**Issue:** A restore/build accepted a newer transitive OpenAPI major, but document generation failed at runtime with MissingMethodException. A replacement generator passed the same route/security check. A framework upgrade also changed an implicit constraint naming convention despite unchanged entities.
**Suggested improvement:** Inspect generated schema diffs, preserve intended explicit names, and verify actual generated documents and security references after changing package majors. Do not equate successful dependency resolution or compilation with runtime compatibility.
**Principle:** Stable packages can form an incompatible graph; prove the contracts the application actually uses.

### Observation 008: Required database checks need an explicit result gate

**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Local reproduction of the PostgreSQL-backed CI suite.
**Skill:** New skill candidate: release reproducibility
**Type:** open-source
**Phase/Area:** Verification completeness
**Issue:** A successful test command could contain skipped database checks when its environment setting was omitted. A complete rerun passed, but exit status alone could have hidden the missing coverage.
**Suggested improvement:** Supply a disposable healthy database and validate results for every required project, positive test counts and zero failed or skipped outcomes. Exercise the validator with missing and non-passing result files.
**Principle:** A passing command establishes only the checks that actually ran.
