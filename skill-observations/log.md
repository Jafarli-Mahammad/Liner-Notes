# Skill Observation Log

Status: OPEN = not yet actioned; ACTIONED = updated; DECLINED = not pursued.

## 2026-10-16

### Observation 020: Preserve writer coverage and validation scope when replacing scans

**Status:** OPEN
**Date:** 2026-10-17
**Session context:** Replaced repeated history hashing with PostgreSQL accounting.
**Skill:** performance-reviewer
**Type:** open-source
**Phase/Area:** Database integrity and compatibility
**Issue:** A broader mutation counter caused a legacy guard to reject scheduler bookkeeping that its original revision intentionally excluded. Schema-only test creation also omits custom accounting triggers.
**Suggested improvement:** Inventory all writers, including raw SQL, cascade deletion and TRUNCATE. Preserve existing guard scope explicitly while maintaining a complete counter for new admission. Run generated migrations in integration tests and keep an independent reconciliation check against actual rows.
**Principle:** Faster accounting must preserve both mutation coverage and the meaning of the validation boundary.

### Observation 019: Separate cache admission units from clocks and working sets

**Status:** OPEN
**Date:** 2026-10-17
**Session context:** Bounded upstream and prepared-evidence caching.
**Skill:** performance-reviewer
**Type:** open-source
**Phase/Area:** Cache accounting and reproducible verification
**Issue:** Count-based cache charges ignored payload size. Absolute expirations from a fake clock were interpreted by a cache using the real clock, preventing reuse in deterministic tests. Scoped response dictionaries were separate retained working sets.
**Suggested improvement:** Audit every writer when changing cache units; distinguish byte estimates, transient working sets and measured RSS. Use a consistent clock or translate policy deadlines to relative lifetimes while independently validating source expiry.
**Principle:** A cache budget and freshness policy are effective only when all writers and clocks use compatible semantics.

### Observation 018: Bind resumed imports to consumed data, not only a cursor

**Status:** OPEN
**Date:** 2026-10-16
**Skill:** Data import integrity candidate
**Type:** open-source
**Issue:** A hash before a multi-pass import does not bind later reads or already committed chunks if the source changes or a read is interrupted. A saved cursor alone could skip mismatching staged rows on restart.
**Suggested improvement:** Verify the bytes actually consumed before publication, fingerprint normalized staged rows, and compare committed fingerprints against the approved artifact during resume. Keep publication atomic and the prior searchable version available after failure.
**Principle:** Resumability requires integrity evidence for both the input stream and the durable prefix.

### Observation 017: Keep fixture stabilization before the tested persistence boundary

**Status:** OPEN
**Date:** 2026-10-16
**Skill:** performance-reviewer / PostgreSQL integration testing candidate
**Type:** open-source
**Issue:** Freshly migrated disposable PostgreSQL databases can change physical size after an explicit inventory read. A fail-closed generation guard correctly stopped setup with database_inventory_changed, making unrelated capture fault tests intermittent.
**Suggested improvement:** First report the safe stop code. Permit bounded explicit setup reconciliation only after proving the managed-row revision is unchanged, no digest exists and no email artifact has been published. Never reconcile automatically after the fault/publication boundary or weaken the production guard.
**Principle:** Test setup can stabilize a real external measurement without erasing the invariant the test is intended to prove.

### Observation 016: Check what benchmark memory counters actually include
**Status:** OPEN
**Date:** 2026-10-16
**Skill:** performance-reviewer / brainstorming
**Type:** open-source
**Phase/Area:** Resource measurement
**Issue:** A subprocess benchmark's getrusage maximum included a large value inherited across exec, while its executable-specific high-water memory was much smaller. Container peak also included earlier tests and reclaimable cache, and SQL timing excluded API/network work.
**Suggested improvement:** Record metric scope and execution history. Compare process-specific current/high-water counters with inherited resource accounting, and distinguish container working set, whole-run peak, pressure events and OOM. Label SQL-only latency and synthetic corpus evidence separately from application capacity.
**Principle:** Match the accounting boundary and workload behind each measurement to the claim it supports.

### Observation 015: Check safety prerequisites at every implementation boundary
**Status:** OPEN
**Date:** 2026-10-16
**Skill:** brainstorming / writing-plans
**Type:** open-source
**Phase/Area:** Implementation planning
**Issue:** A staged plan can introduce job submission before its consumer or add managed writes before the later accounting stage. The final architecture can look safe while intermediate commits accept work that cannot complete or bypass protection.
**Suggested improvement:** Check each commit's enabled behavior against its prerequisites. Introduce shared contracts/schema before consumers, keep incomplete submission routes disabled, and use existing conservative safeguards until improved coordination is verified. Require privacy coverage when each storage class appears.
**Principle:** Every intermediate runnable state must preserve ownership, resource admission, and progress guarantees.

### Observation 014: Inspect ownership and batch identity before adding guest discovery
**Status:** OPEN
**Date:** 2026-10-16
**Skill:** brainstorming
**Type:** open-source
**Phase/Area:** Architectural planning
**Issue:** Adding guest access and repeated discovery to a scheduled recommendation product changes more than its signup interface. Existing taste records may require an authenticated subscriber, while batch uniqueness may allow only one result per calendar period. A fast local search also leaves candidate acquisition and generation locking on the critical path.
**Suggested improvement:** Before proposing reuse, trace profile ownership, batch uniqueness, feedback ownership, privacy operations, candidate readiness, and lock scope. Specify guest-to-account transfer and repeated-batch identity explicitly. Measure prepared and uncached generation separately from autocomplete.
**Principle:** Validate the complete path from visitor identity to persisted results before treating a new discovery flow as a UI change.

## 2026-10-15

### Observation 013: Validate redundant metadata identities against live envelopes
**Status:** OPEN
**Date:** 2026-10-16
**Skill:** test-cases / brainstorming
**Type:** open-source
**Phase/Area:** Provider contract verification
**Issue:** An official example repeated an artist ID inside every tag, while a live artist-scoped response omitted that redundant field. Fixture tests alone accepted an unnecessarily strict parser that discarded usable tags.
**Suggested improvement:** Validate the enclosing entity identity first, test both redundant-ID and envelope-scoped shapes, reject conflicting supplied IDs, and use a bounded catalog smoke without recording account data before judging adapter coverage.
**Principle:** Infer context only from a verified enclosing entity; do not confuse absent redundant fields with absent evidence.

### Observation 012: Trace supporting evidence services in provider migrations
**Status:** OPEN
**Skill:** brainstorming
**Type:** open-source
**Phase/Area:** Architectural planning
**Issue:** A discovery abstraction can appear swappable while seed tags, hydration, attribution and allowed generation origins still depend on the original integration.
**Suggested improvement:** Trace the complete generation path; distinguish adapter support, offline replay, live generation, default rollout and future user preference.
**Principle:** Verify every dependency needed for a usable provider, not just discovery.

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

### Observation 006: Packaging must preserve local execution boundaries

**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Docker, Compose and CI inspection for an existing local batch application.
**Skill:** New skill candidate: release reproducibility
**Type:** open-source
**Phase/Area:** Packaging preflight
**Issue:** A web Dockerfile omitted shared target-framework properties before restore; Compose lacked its database, and CI could skip database checks. The batch executable deliberately rejected production mode and non-loopback databases, so a generic production topology would not represent implemented behavior.
**Suggested improvement:** Inspect shared build inputs, test skip conditions, executable guards, migration context and storage/key ownership before proposing deployment files. Package a supported local topology and distinguish reproducibility from production delivery and external approval.
**Principle:** Packaging should make existing runtime and verification contracts executable; containerizing a process does not establish a supported production workflow.

### Observation 007: Reproducibility pins follow the approved version policy

**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Developer corrected the release design's proposal to retain EF Core 9 after EF Core 10 was stable, and approved proceeding with latest stable packages/tools.
**Skill:** New skill candidate: release reproducibility
**Type:** open-source
**Phase/Area:** Dependency selection
**Issue:** Preserving an existing dependency graph was proposed as a default despite a newer stable framework release. Reproducibility and version freshness were treated as competing requirements.
**Suggested improvement:** Verify stable releases and compatibility first, upgrade under the authorized version policy, then freeze exact versions and lock files. Distinguish a verified compatibility exception from retaining an old version without investigation.
**Principle:** Reproducibility means pinning a deliberately chosen, verified graph; it does not require keeping an outdated graph.

### Observation 008: Latest transitive majors need executable compatibility checks

**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Stable dependency modernization and API documentation compatibility.
**Skill:** New skill candidate: release reproducibility
**Type:** open-source
**Phase/Area:** Dependency compatibility
**Issue:** A restore/build accepted a newer transitive OpenAPI major, but document generation failed at runtime with MissingMethodException. A replacement generator passed the same route/security check. A framework upgrade also changed an implicit constraint naming convention despite unchanged entities.
**Suggested improvement:** Inspect generated schema diffs, preserve intended explicit names, and verify actual generated documents and security references after changing package majors. Do not equate successful dependency resolution or compilation with runtime compatibility.
**Principle:** Stable packages can form an incompatible graph; prove the contracts the application actually uses.

### Observation 009: Required database checks need an explicit result gate

**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Local reproduction of the PostgreSQL-backed CI suite.
**Skill:** New skill candidate: release reproducibility
**Type:** open-source
**Phase/Area:** Verification completeness
**Issue:** A successful test command could contain skipped database checks when its environment setting was omitted. A complete rerun passed, but exit status alone could have hidden the missing coverage.
**Suggested improvement:** Supply a disposable healthy database and validate results for every required project, positive test counts and zero failed or skipped outcomes. Exercise the validator with missing and non-passing result files.
**Principle:** A passing command establishes only the checks that actually ran.

### Observation 010: Verify approved exceptions against the actual package graph

**Status:** OPEN
**Date:** 2026-10-12
**Session context:** Dependency and credential findings in a pull-request review.
**Skill:** New skill candidate: release reproducibility
**Type:** open-source
**Phase/Area:** Review and dependency approval consistency
**Issue:** Documentation described an approved licensing exception while the central manifest and restored lock files selected the newer release. A credential scan also flagged private environment interpolation alongside literal fixture passwords.
**Suggested improvement:** Compare central versions, all affected lock entries and version evidence with approval decisions after restore. Verify flagged credentials against current use before changing configuration or rotating anything; distinguish literals from required private interpolation.
**Principle:** Approval records and scan labels need verification against the executable configuration.

### Observation 011: Keep progress status concise and evidence-linked

**Status:** OPEN
**Date:** 2026-10-13
**Session context:** Pruned the project progress tracker after several backend phases had accumulated implementation evidence.
**Skill:** New skill candidate: project status documentation
**Type:** open-source
**Phase/Area:** Status maintenance
**Issue:** The progress file repeated completed implementation details across several sections, contained stale historical claims and malformed fragments, and made open gates hard to identify.
**Suggested improvement:** Keep the tracker focused on current implementation status, concrete remaining decisions and execution gates. Retain detailed verification and historical rationale in linked evidence documents; check claims against the current checkout before pruning.
**Principle:** A progress tracker should answer what is done and what remains; evidence documents should carry the detailed record.

### 021 — Schema changes and exact public payload checksums

Adding a column changes PostgreSQL to_jsonb(row) storage-accounting estimates
without firing row triggers. Verify the old accounting baseline before schema
DDL, then explicitly rebase after DDL/backfill. Public JSON payloads with exact
byte checksums need TEXT storage; JSONB normalizes representation. Test decoded
immutable collections by sequence contents and normalize timestamp fixtures to
PostgreSQL microsecond precision. Keep these checks in migration/cache tests.

### Observation 024: Interfaces must hide implementation dependencies
**Status:** OPEN
**Date:** 2026-10-18
**Skill:** ponytail; architecture review
**Type:** open-source
**Issue:** An interface exposed vendor collection and transaction types despite having no application consumers. A provider migration also prompted a correction about excessive coupling.
**Suggested improvement:** Audit public contract types and implementation references before extending workflows; remove unused vendor-shaped contracts and test the application assembly boundary.
**Principle:** Polymorphism supports substitution only when contracts hide implementation details.

### Observation 025: Verify original content before repairing compressed output
**Status:** OPEN
**Date:** 2026-10-19
**Skill:** task-observer; coding workflows
**Type:** open-source
**Issue:** Tool-output compression removed words and generic type syntax, making valid source and documentation appear truncated. That presentation can prompt unnecessary or incorrect repairs.
**Suggested improvement:** Retrieve original output or inspect small exact slices before editing. Distinguish a tool presentation failure from file corruption. Recreate disposable test environments from pinned inputs when they expire, and verify dynamically assigned ports again after restarts.
**Principle:** Work from verified source and recoverable test inputs; summaries are navigation aids, not mutation authority.

### Observation 026: Trace selection limits through generation and evidence storage

**Status:** OPEN
**Date:** 2026-10-19
**Session context:** Planning optional onboarding with unlimited selected taste tags.
**Skill:** brainstorming
**Type:** open-source
**Phase/Area:** Existing-system requirements and feasibility
**Issue:** Removing a visible selection limit would still leave server validation, materialization count limits, acquisition windows and explanation snapshot budgets restricting the feature. Raising one limit would not satisfy the requirement.
**Suggested improvement:** When a requirement changes cardinality, trace collection limits from UI through persistence, computation, external requests and stored evidence. Specify which selections are retained and how bounded processing represents them before estimating implementation.
**Principle:** Product cardinality and per-operation resource budgets need separate contracts; changing one must not silently redefine the other.
**Reference file:** docs/superpowers/specs/2026-10-19-optional-taste-onboarding-design.md


### Observation 027: Trace new entity kinds through validation and custom SQL

**Status:** OPEN
**Date:** 2026-10-19
**Skill:** efcore-patterns; modern-csharp-coding-standards
**Type:** open-source
**Phase/Area:** Schema and lifecycle verification
**Issue:** Extending an enum and import/search paths still left a Domain selection allowlist and a migration-only database CHECK constraint rejecting the new kind. Existing tests passed until the browser exercised saving an actual selected entity.
**Suggested improvement:** Search all entity-kind guards, hand-written migration constraints, persistence readers, exports and immutable snapshots. Add an integration test that selects, saves and reads the new kind through the complete application path, including an upgrade from the old schema.
**Principle:** Enum extension is an end-to-end data contract change; model-generated migrations do not discover custom SQL constraints.


### Observation 028: Reproduce the PR merge before changing CI

**Status:** OPEN
**Date:** 2026-10-19
**Session context:** Diagnosing a GitHub restore/Compose failure that passed on the source branch.
**Skill:** ponytail; task-observer
**Type:** open-source
**Phase/Area:** CI failure triage
**Issue:** Source-head restore and container checks passed, while the PR merge inherited a malformed dependency lock from its target branch. NU1510 warnings in the excerpt distracted from the actual NU1004 failure.
**Suggested improvement:** Confirm which revision each job checks out, reproduce that merge in a disposable tree, and check lock-file JSON for duplicate keys before weakening locked restore or removing dependencies.
**Principle:** A passing branch-head check does not validate the code that a merge-based PR workflow builds.

### Observation 029: Verify PR refs separately after rewriting a branch

**Status:** OPEN
**Date:** 2026-10-19
**Session context:** Replacing a PR branch history to remove flagged historical commits.
**Skill:** task-observer
**Type:** open-source
**Phase/Area:** GitHub PR history and security checks
**Issue:** Force-updating the source branch did not update the closed PR's recorded head SHA or clear its failed GitGuardian check. A new PR scan was not created by pushing a branch, and this environment had SSH push access but no GitHub API write credential.
**Suggested improvement:** After a history rewrite, verify both the remote branch SHA and the PR head/state through GitHub's API. Treat a clean branch history, an open PR, and passing secret-scanning checks as separate states; confirm API write access before promising PR creation or incident resolution.
**Principle:** Branch refs, PR refs, and security incidents have independent lifecycle state.

### Observation 030: Validate diagnostic flags against installed tools

**Status:** OPEN
**Date:** 2026-10-24
**Session context:** Building reusable Fedora .NET diagnostic scripts.
**Skill:** task-observer; diagnostics scripting
**Type:** open-source
**Phase/Area:** CLI compatibility and bounded collection
**Issue:** A remembered duration option for dotnet-counters was absent from the installed version, which would have made the memory monitor fail after launch.
**Suggested improvement:** Inspect each installed tool's help output before composing commands. Use an external timeout with graceful interruption when the tool does not provide a duration option, and verify one representative invocation before documenting it.
**Principle:** Diagnostic automation should bind flags and shutdown behavior to the actual installed CLI contract.

### Observation 031: Keep README setup links canonical

**Status:** OPEN
**Date:** 2026-10-27
**Session context:** Updating the project, developer and frontend READMEs against the current checkout.
**Skill:** task-observer; ai-slop-cleaner
**Type:** open-source
**Phase/Area:** Documentation maintenance
**Issue:** The README repeated long local setup instructions already maintained in runbooks, and the developer map mixed stale implementation notes with current entry points.
**Suggested improvement:** Give each README one clear audience, link to the canonical setup procedure, and verify source paths plus local links before finishing.
**Principle:** Keep orientation in READMEs and procedures in one maintained runbook.

### Observation 032: Preserve useful diagrams during a prose rewrite

**Status:** OPEN
**Date:** 2026-10-27
**Session context:** Restoring the product-flow, architecture, data and source-graph visuals after a README rewrite removed them.
**Skill:** task-observer; ai-slop-cleaner
**Type:** open-source
**Phase/Area:** Documentation cleanup
**Issue:** A text-focused cleanup removed diagrams that helped readers understand the product loop, source layout and code relationships.
**Suggested improvement:** Inventory existing visual assets and diagrams before rewriting documentation; keep the useful ones, update their labels and limits, and verify the rendered references.
**Principle:** Simplify prose without discarding visuals that carry useful project context.

### Observation 033: Trace readiness separately from response latency

**Status:** OPEN
**Date:** 2026-10-27
**Session context:** Investigating slow background discovery and cover loading.
**Skill:** performance-reviewer
**Type:** open-source
**Phase/Area:** Asynchronous preparation and browser retries
**Issue:** A cached image endpoint can respond in milliseconds while background acquisition takes minutes; finite browser retries can expire before preparation finishes. A fast warm probe alone would miss the user-visible failure.
**Suggested improvement:** Trace enqueue-to-ready time, cache state, and browser retry horizon separately from HTTP response duration. Test cold, warm, delayed, and terminal-error states before assigning a cause.
**Principle:** Measure the time until useful content is visible, including work outside the request that serves it.

### Observation 034: Check CSP before changing image loading

**Status:** OPEN
**Date:** 2026-10-27
**Session context:** Adding explicit image readiness and asynchronous retry recovery.
**Skill:** performance-reviewer; frontend verification
**Type:** open-source
**Phase/Area:** Image delivery and security policy
**Issue:** A blob URL image-loading approach passed typechecking but failed in the real host because its CSP excluded blob images. Contract tests running against the real host caught the incompatibility.
**Suggested improvement:** Read the host's CSP before choosing blob/data/direct URL delivery. Prefer an existing permitted delivery path where possible, and verify image decoding under actual response headers.
**Principle:** Loading-state improvements must be tested with the application's security policy active.
