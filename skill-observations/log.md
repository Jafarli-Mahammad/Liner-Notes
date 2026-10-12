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
