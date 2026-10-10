# Phase 7 implementation evidence and reassessment

Verified locally on 2026-10-11 on `Prism`, after approval of the
[design](phase-7-review.md), [implementation plan](phase-7-plan.md) and sequential
execution. Developer confirmation remains pending. **Mechanics only, no catalog conclusions.**

## Per-approval implementation status

| Approved item or remaining boundary | Status, evidence and reason |
|---|---|
| Reconcile authorized Phase 6 follow-ups | Implemented: status/cutoff and Development manual QA brought into this checkout in `3209336`; 341 tests passed. |
| Working local unsubscribe | Implemented: anonymous confirmation GET is read-only; form POST is idempotent, retaining the first UTC opt-out. Actual Playwright MCP confirmation and profile reload verified it. |
| One opt-out column and preserved existing accounts | Implemented: only nullable `Users.EmailUnsubscribedAtUtc` added; populated PostgreSQL migration verifies preservation. |
| API/export consequences | Implemented: subscriber field, export 2.1, sanitized owned messages/issues and honest deletion result; existing numeric status values preserved. |
| Isolated persisted unsubscribe capabilities | Implemented: dedicated Data Protection application/purpose uses existing key table; restart/shared-key and cryptographic Identity isolation checks pass. No token ledger or Identity/JWT root change. |
| Explicit offline Worker | Implemented: one-shot reconcile/generate/capture for exact account/week/origin; missing arguments exit 2; no idle/scheduled loop. |
| Reuse Phase 6 generation and persisted selections | Implemented: existing MediatR generation, persisted DTO capture, stored rank/explanations. No rescoring or provider call during capture. |
| Synthetic demonstration | Implemented: fabricated accounts and fixture seeds in a disposable local database. Actual separate Worker processes generated/captured empty and five-pick lists. |
| Exact approved recorded-input adapter | Implemented and verified with fabricated manifests: immutable replay, hash/expiry/use/account/week/containment bounds, credential/held-out rejection. No real input used because suitable current local-use permission was not provided. |
| HTML/plaintext and 0/1/3/5 lists | Implemented: UTF-8 MIME alternatives, actual list size, complete stored explanations, attribution and ordinary links. 0/5 inspected from actual Worker files; all four sizes covered by tests. |
| No automatic third-party requests/tracking | Implemented: inline CSS/system fonts, no images/scripts/remote fonts/resources/players; renderer and browser checks pass. |
| Bounded private local capture | Implemented: 100,000 UTF-8 bytes per alternative, 400,000 total MIME, no truncation, explicit ignored accounted sink, private Unix files, no-overwrite atomic publication. |
| Durable receipt and distinct local lifecycle | Implemented: protected ownership/body receipt and versioned render fingerprint; `LocalCaptured = 4`, `SentAt` remains null. Existing Sent numeric value remains 2. |
| Retry/restart/cancellation/concurrency | Implemented: bounded local I/O/fresh SQL attempts, non-replayed publication transaction, PostgreSQL locks, repeated capture and failure-window recovery checks. Separate-process repeat preserved bytes. |
| Inventory/headroom/partial accounting | Implemented: explicit reconciliation, exclusive artifact lease, revisions/freshness, three-partial reservation and owned-write tracking. Browser-created report invalidated inventory and caused a stop, then explicit reconciliation allowed progress. |
| Local copy visibility/deletion | Implemented: sanitized export, bounded ownership validation, removal after committed account deletion, explicit incomplete result/operator retry. Real deletion removed each account's copies and denied access afterward. |
| Runnable local QA path | Implemented: [launcher and procedure](phase-7-local-run.md), private MIME inspection, confirmation, cleanup retry. Actual launcher started, served QA/health, ran cleanup and exited removing its own temporary roots. |
| Mandatory reassessment | Implemented below. Explicit proceed/narrow/revise decision still required before later phases. |
| Popularity setting/migration/settings/export | Deferred under the earlier instruction to wait; excluded from Phase 7. V1 remains popularity-neutral. |
| Real storage capacity | Not decided: 1,000,000,000-byte stop is the temporary unresolved placeholder. Hosting/capacity decision remains build-order step 9. |
| Recommendation quality/personal fit | Not done: Phase 5 bypass remains provisional baseline A; synthetic mechanics cannot establish quality. |
| Live acquisition, imported histories and real recording demonstration | Not done: no applicable exact run/local-use approval; no live calls or history import in this work. |
| Phase 8a broad privacy/export/deletion and backup audit | Deferred: separate approval needed. Configured local copy cleanup does not prove all backups/copies are erased. |
| Phase 8b scheduling/external delivery | Deferred: separate approval needed. No scheduler, SMTP/provider sending or external delivery claim. |
| Phase 9 deployment/release/public-page approval | Deferred: separate approval needed; no push, provisioning or deployment. |
| Developer confirmation of implemented slice | Pending: local automation/browser evidence does not replace the developer's confirmation. |

Focused implementation commits: `3209336` (prerequisites), `c822271` (unsubscribe/account
boundaries), `c97fe0e` (render/sink/archive), `c9f4c4e` (Worker/recorded gate/recovery),
followed by the evidence/QA commit containing this document and final safeguards.
The first three Phase 7 milestones passed 348 full-suite, 86 focused Infrastructure,
then 385 full-suite checks respectively; the final integrated suite below supersedes them.

## Final automated verification

`dotnet test 'Liner Notes.sln' --no-restore --logger 'console;verbosity=quiet'`, with an
explicit disposable loopback PostgreSQL administrator connection provided through
`LINER_PHASE1_POSTGRES`, passed **386 / 0 failed / 0 skipped**:

| Project | Passed |
|---|---:|
| Domain.Tests | 105 |
| Application.Tests | 86 |
| Infrastructure.Tests | 97 |
| DataAccess.Tests | 51 |
| Presentation.Tests | 47 |

The tests create/drop isolated databases. They cover populated migration/key persistence,
UTF-8/escaping/header and URL rejection, fingerprints/feedback, actual files, safe export,
ownership cleanup, path/symlink/byte limits, partial storage accounting, cancellation and
locks. Existing benchmark seed checks passed; Phase 7 makes no scorer-policy changes.
See [local email tests](../../tests/Infrastructure.Tests/LocalEmailTests.cs),
[recorded input checks](../../tests/Infrastructure.Tests/LocalRecordedInputTests.cs),
[Worker PostgreSQL checks](../../tests/DataAccess.Tests/LocalWorkerPostgresTests.cs),
[opt-out migration/key checks](../../tests/DataAccess.Tests/LocalUnsubscribePostgresTests.cs)
and [route/logging checks](../../tests/Presentation.Tests/UnsubscribeRouteTests.cs).

Additional checks: `renewal_docs.py`, `secret_defaults.py`, Bash syntax for both QA
launchers, JavaScript syntax for main/manual QA clients, and `git diff --check` passed.
Rider MCP returned no errors for the changed sink and Worker runner; earlier focused
controller/store diagnostics also returned no errors. Its batch lint response returned no
file entries, so that response does not establish coverage. Codebase MCP navigation found
the Phase 7 documents, with new C# coverage incomplete; source reads and `rg` supplied the
implementation evidence. Package-pruning NU1510 and EF CLI 9.0.0/runtime 9.0.2 warnings
remain; no dependency removal/upgrade was authorized or performed.

## Actual process and browser evidence

The disposable PostgreSQL 16 server was bound only to loopback. Presentation used a private
temporary configuration and Development/local-email settings. The ordinary registration,
seed, profile, export and deletion APIs were used; no existing user database or real
recordings were read. The demo produced these exact UTF-8 sizes:

| Synthetic list | Plaintext bytes | HTML bytes | Serialized MIME bytes |
|---|---:|---:|---:|
| 0 picks | 570 | 1,120 | 3,834 |
| 5 picks | 2,892 | 5,223 | 12,649 |

For each, actual sequential reconcile → generate → reconcile → capture ran through Worker
composition. A subsequent separate Worker process returned `already_captured`; SHA-256
comparison showed identical completed bytes. Five-pick MIME contained every persisted
explanation, no automatic remote resource and file mode `0600`. Export 2.1 reported the
owned sanitized copy. No capability, credential or recipient was retained in tracked
evidence. These two fabricated examples do not establish a size distribution or capacity.

Native Playwright MCP could not start because system Chrome was absent; its Chrome installer
required unavailable sudo. The existing browser cache removed by installer GC was restored.
A temporary isolated Playwright MCP server with a cached Chromium executable exercised
the real route: GET showed confirmation and left the profile opt-out null; browser form
submission showed **Unsubscribed**, and the reloaded profile contained the UTC opt-out.
The browser page had no script or remote image. Subsequent local capture stopped with
`email_unsubscribed`; account/taste access remained intact. Temporary tooling did not change
project dependencies or persistent MCP configuration.

Deleting the five-pick account returned 204 and removed its message while the other account's
message remained. Deleting the other account later removed that copy and invalidated protected
access. A third throwaway account verified the operator helper: `account_still_exists` while
active, then `cleanup_complete` after deletion. The actual launcher applied/upheld migrations,
served Healthy and the redirected QA page with HTTP 200, ran its cleanup menu, then quit.
Both domain and Identity account tables were empty after all three deletions. Temporary
processes, demo artifacts and the disposable PostgreSQL container were cleaned up.

## File/SQL failure windows and ownership

| Window | Verified recovery behavior |
|---|---|
| Before creating a partial | No completed message/state; later eligible capture can retry. |
| Mid-write or after partial flush | Partial remains counted; bounded retry uses a fresh owned path. |
| After completed publication, before SQL state commit | Validated receipt survives; rerun marks LocalCaptured without another completed file. |
| Cancellation after publication | Artifact remains recoverable; leases release; no false Sent claim. |
| SQL commit succeeds, response/attempt subsequently fails | Rerun validates captured receipt and returns already captured. |
| Concurrent participating captures | Account/artifact/row locks serialize; one completed message. |
| Opt-out before final row lock | Capture stops before publication. |
| Opt-out/deletion racing a held row/account lock | Operations wait; captured-before-opt-out state is coherent, deletion then removes owned copies. |
| Captured receipt missing/conflicting/truncated | Integrity stop; no replacement or fabricated success. |

Lock order is account advisory lease → artifact file lease → final PostgreSQL Users/digest
row transaction. Generation's ownership lease is released before capture. The capture store
uses its own fresh DataContext without automatic EF transaction replay; no file publication
is placed inside the retrying UnitOfWork callback. Unsubscribe's atomic row update and account
deletion coordinate with the same ownership/row boundary. SQL and files still do not share a
transaction: recovery validates the protected receipt, current render input and body checksum.
These are process/fault/cancellation checks, not proof of power-loss durability or external
email exactly-once delivery. Nonparticipating/distributed writers remain unverified.

## Storage and privacy reassessment

One explicit inventory after both messages measured database **8,821,783 bytes**, reports
**138 bytes**, copies **16,483 bytes**, and recordings/cache/backups/partials **0 bytes**:
total **8,838,404 bytes**. The generated MIME byte sum exactly matches `copies`. An additional
browser report made that inventory stale and capture stopped; reconciliation was explicit.
The test launcher reserves 1 MB of database overhead per pick as a conservative fixture
setting, not a calibrated production estimate.

The sink reserves three possible MIME-sized partials under the temporary 1 GB stop; it
validates database/artifact revisions and hashes of its own writes. Overflow or changed
unowned artifacts stops before publication. Failed partials remain accounted. Existing
receipt reconciliation/inspection and opt-out/deletion continue without creation headroom.
No expiry, retention or automatic partial removal was adopted. Production p95/max histories,
backup budget, filesystem limits, enrollment and supported capacity remain **not verified**.

Messages contain recipient/taste data and an unsubscribe capability. Newly created Unix
files are private; Windows ACLs and encryption at rest were not established. Export removes
capabilities/security internals and reports missing/corrupt copies. Cleanup occurs after
SQL deletion, outside automatic SQL retry; unvalidated files remain with an incomplete issue,
and the separate operator helper can retry after account deletion. This covers the configured
sink only. Shared catalog, backups and copies outside it retain the Phase 8a audit boundary.
Hosting request-query logging is suppressed while local email is enabled; a regression test
checks the capability query is absent. No external proxy/client logging guarantee is claimed.

## Terms, evidence quality, cost and complexity reassessment

The [authoritative references refreshed for the approved review](phase-7-review.md#authoritative-references-refreshed-2026-10-11)
cover Last.fm endpoints/terms, Generic Host, Data Protection and MIME alternatives.
Last.fm artist tags describe artists, not track-specific musical characteristics; the
pipeline retains that evidence scope. Its upstream candidate ordering/coverage can bias
what is available even with popularity-neutral scoring. No new similarity algorithm, formula
superiority, personal fit or catalog quality is claimed from fixtures.

Text-only email attribution acceptability, territorial/consent questions and written
public-page approval remain **not verified**. No provider endpoint, rate or pricing
integration was added. Hosting/email cost, scheduling resources and worker uptime are not
decided; no purchase or external service was used. Baseline A remains provisional after the
explicit Phase 5 bypass, and real evaluation is still open.

Complexity now includes authenticated file receipts, persisted protection keys, artifact
inventory revisions, two lease types and a non-replayed SQL transaction. This is justified
for the approved local recovery/privacy boundary, but external delivery would require its
own delivery model and failure analysis. A separate full privacy audit and delivery approval
remain necessary; passing local tests does not establish production readiness.

## Required next decision

The approved Phase 7 implementation is **locally verified, developer confirmation pending**.
Use [the runnable QA procedure](phase-7-local-run.md) to confirm the implemented behavior.
Before a later phase, explicitly choose **proceed, narrow or revise** based on the reassessment
above. Phase 8a privacy verification and Phase 8b scheduling/delivery have separate approvals;
Phase 9 release/deployment remains unapproved. The popularity setting stays deferred and the
storage threshold stays temporary/unresolved. Nothing starts automatically.
