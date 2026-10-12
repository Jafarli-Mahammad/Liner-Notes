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

### Observation 005: Bounded schedulers need liveness checks across repeated ticks
**Status:** OPEN
**Date:** 2026-10-12
**Skill:** test-cases
**Type:** open-source
**Phase/Area:** Scheduling regression verification
**Issue:** Single-account failure checks cannot establish fairness when stopped jobs retain their due slots.
**Suggested improvement:** Test a persistent failure alongside a healthy later account with a limit of one, repeated scopes, an unchanged clock, and both invalid-state and missing-receipt failures. Preserve required operator reconciliation during end-to-end capture checks.
**Principle:** Verify bounded progress across ticks as well as safety within each job.
