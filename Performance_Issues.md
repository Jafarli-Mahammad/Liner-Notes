# Liner Notes — Performance Issues

Consolidated on 2026-10-27 from the repository based on `64d6b7d`, including the
documentation pointers added with this consolidation. This file contains the
current issue register followed by the complete performance reports, design,
evidence, operational guides and project handoff snapshots. Source paths, inclusion
scope, lengths and SHA-256 hashes are recorded below.

## Read this first

The initial repository-wide audit is complete: its historical ledger accounts for
495 files and 63,948 lines. The later targeted storage scan is also complete within
its stated scope. **Remediation is not complete.** G0/T01, G1a–G1c and the G1d0–G1d4
foundations have local verification; incremental admission, the scan-identified
gaps and G2–G7 remain unfinished. Production mutation admission retains full content
hashing. No actual VPS, real-provider workload, long-running recovery or real-email
delivery acceptance is established.

The register in this opening section describes the latest recorded state. The
appendices preserve historical reports in full, including old failures, pending
approval statements, earlier test counts and proposals that were later superseded.
For example, the approved plan's original P20 row predates the completed G1c writer
integration. Those historical rows are not new statements that G1c is unimplemented.
The complete plan, joint P01/P19/P20 design, D1–D4 and narrow privacy-reclamation
exception are already approved. **The S01 signed web-maintenance policy is still
undecided**; the latest request to consolidate documentation does not approve it.

This is a documentation consolidation. It adds no runtime behavior, migration,
dependency, new performance measurement or new acceptance result. Existing tests
and metrics retain their original dates, workload boundaries and limitations.

## Contents

- [Complete finding register](#complete-finding-register)
- [Additional storage findings](#additional-storage-findings)
- [Completed work and verification](#completed-work-and-verification)
- [Important measurements and their limits](#important-measurements-and-their-limits)
- [Approved contracts and remaining decision](#approved-contracts-and-remaining-decision)
- [Remaining implementation and verification](#remaining-implementation-and-verification)
- [Operational and external acceptance requirements](#operational-and-external-acceptance-requirements)
- [Complete source reports and handoff snapshots](#complete-source-reports-and-handoff-snapshots)

## Complete finding register

“Locally addressed” means the recorded implementation and local regression evidence
exist. It does not mean the developer confirmed the deployed behavior. Partial
findings retain their unfinished work. Conditional findings are not promoted into
normal-path bottlenecks without evidence. Accepted tradeoffs retain remeasurement
triggers rather than acquiring speculative rewrites.

| ID | Problem | Latest disposition | Remaining action / group |
| --- | --- | --- | --- |
| P01 | Repeated complete artifact hashing inside serialized write admission | Partial: authenticated recovery, exact receipt anchoring, immutable manifests, verified consumption, bounded strict read cache and internal metadata scanner exist. Production still fully hashes. | Finish S01–S03/S05 coverage, startup/periodic audit trust and measured opt-in incremental admission; G1d. |
| P02 | Duplicate metadata acquisition around concurrent cache misses | Partial: post-pacing cache reuse exists; some overlap controls bypassed real pacing. | Measure reachable same-process/distinct-pool and cross-process overlap before justified bounded coalescing; G6. |
| P03 | Response buffering, copied arrays and owning JSON allocations | Partial: one response-sized copy was removed; bounded buffering and owning JSON remain. | Compare supported bounded ownership/buffer alternatives without lifetime/hash regressions; G6. Retain justified residual costs explicitly. |
| P04 | Polling retries ignored longer server Retry-After | Locally addressed with delay parsing, timer-safe waits and cancellation regression evidence. | Preserve Node and actual-browser regression gates; G7. |
| P05 | Unrelated cold pools can share a long-held stripe gate | Accepted measured tradeoff: synthetic collisions exist, but no actionable normal-lane bottleneck was established. | Reopen only after acquisition concurrency changes or normal traffic shows material attributable queueing; G7. |
| P06 | Complete exports build large graphs and repeat preview/download work | Unresolved; bounded complete staged-export design approved. | Consistent complete snapshot, atomic private chunks, one builder, bounded disk use, reuse and owner reauthorization; G2. |
| P07 | Empty queues poll SQL every second | Accepted measured responsiveness tradeoff at current host/lane count. | Reopen for multiple hosts, measured idle pressure or a changed wake-latency requirement; G7. |
| P08 | Losing candidates still allocate explanation snapshots/contributions | Partial: batch invariants are reused. | Separate score/select/explain only with identical validation, deterministic ordering and selected stored explanations; G5. |
| P09 | Weekly generation materializes and fingerprints all feedback history repeatedly | Unresolved; transactional revision direction approved. | Exact input revisions plus required-data projections, all writer coverage and reconciliation; G4. |
| P10 | Alias grouping repeatedly rebuilt identities in a nested scan | Locally addressed through indexed alias merging. | Preserve transitive identities, evidence preference and exclusions; G7. |
| P11 | Catalog selections fan out into sequential eligible-item SQL reads | Locally addressed and PostgreSQL verified through batched eligible reads. | Preserve requested order, duplicates/missing IDs, expiry and cancellation; G7. |
| P12 | Evidence publication recomputes cache-wide SUM/COUNT quotas | Unresolved; transactional exact-counter direction approved. | Exact quota totals for publication/update/delete/expiry/eviction/retry and migrated reconciliation; G4. |
| P13 | Owner export/delete scans and decrypts unrelated accounts' archive copies | Unresolved as a performance issue; current bytes/ownership checks and coordinated deletion are retained. | Authenticated owner index/layout, safe legacy migration and complete export/deletion; G2. |
| P14 | Fresh prepared pools are encoded then decoded into another graph | Unresolved; duplicate graph cost measured. | Reuse only the exact validated acquired object actually published; preserve stored-hit decoding and lifetime/provenance bounds; G5. |
| P15 | Password verification holds shared admission needed by unrelated writes | Unresolved; actual login/write contention measured. | Move pure verification outside admission, retaining atomic guarded lockout/session/security-stamp finalization and revocation; G3. |
| P16 | Artist metadata is acquired before sufficient direct recording tags are selected | Unresolved; redundant dispatches measured and sufficiency direction approved. | Skip unused artist acquisition only for valid sufficient direct tags, record `not_requested`, preserve fallback/evidence semantics; G6. |
| P17 | Authenticated requests perform two sequential account-existence checks | Locally addressed and PostgreSQL verified through one combined eligibility query. | Retain both Identity and Domain checks and immediate revocation; G7. |
| P18 | Uncompressed upstream/public-static transfers waste bytes | Unresolved; compression opportunity measured on a bounded sample. | Supported negotiation/decompression with decoded cap and provenance; selected proxy compresses public static assets only; G6 and external acceptance. |
| P19 | Per-row managed-ledger updates cause trigger, WAL and lock overhead | Locally implemented in G1b: statement transition-table accounting retains the exact formula and revision semantics. Bulk gains measured; small/cold costs remain. | Preserve all SQL/migration/accounting guards; remeasure contention and remaining costs with G4/G7 and actual deployment workloads. |
| P20 | Scheduled capture stopped after generation committed without inventory publication | G1c locally remediates coordinated generation/capture/key/schedule/failure/unsubscribe/deletion publication. Offline uninterrupted cohorts pass. | Keep the original unattributed injected post-commit stop open; combined recovery/concurrency/soak and realistic delivery acceptance remain; G7/external acceptance. |
| T01 | Diagnostics wrapper incompatible with installed CLI and result handling | Locally addressed in G0: supported capture, output creation/conversion/report arguments, child status and global fallback. | Recheck installed CLI help when tooling changes; no application speedup follows from a tooling fix. |

## Additional storage findings

The targeted scan reviewed expanded search candidates in 22 file-I/O files
(159 matching lines) and 28 SQL-access files (119 matching lines), then traced
callers and delegated framework/operator paths. These are search candidate counts,
not percentages of test coverage or a proof against arbitrary external writers.

| ID | Finding | Evidence and current boundary | Required disposition |
| --- | --- | --- | --- |
| S01 | Development HTTP reconciliation exposes an operator trust reset without an operator policy | Route requires an active authenticated account but directly calls re-enrollment without the CLI's database admission/independent ledger verification. Production does not map it. Offline reconciliation accepts changed hashes after strict admission rejects them; no authenticated HTTP exploit was run. | Decide CLI-only signed maintenance versus a reviewed privileged web maintenance protocol before changing this endpoint. |
| S02 | Pacing directory may overlap enrolled managed storage | An 18-byte operational pacing write inside an enrolled cache root makes the next strict admission stop. Default/Compose layouts are separate; arbitrary overlap is not rejected. No HTTP request was sent. | Enforce disjoint managed/control/manifest paths before activation; preserve pacing, limits and accounting. |
| S03 | Metadata category stats hold the consumed-reader cache gate | Deliberate 10,000-file overlap changes median-of-process-median reads from 5.407 to 133.268 ms. The scanner is inactive in production admission. | Move stat work outside the reader gate using a proven bounded design; retain delayed-holder/eviction/GC/cancellation proofs. |
| S04 | A mutable third-pass catalog stream reaches append callbacks before final hash failure | 250 modified rows reach a fake append store; publication remains blocked. Current host callers own private MemoryStreams; no normal-path exploit or SQL commit was demonstrated. | Preserve exclusive private input; define verified immutable staging before supporting mutable/operator file streams. Do not rewrite a safe normal path speculatively. |
| S05 | Startup proof and 30/60-second audit enforcement are not implemented | Full hashing remains active. The current one-minute, RunJobs-only maintenance service is not the approved incremental auditor, and persisted timestamps alone cannot prove restart trust. | Implement/test process-local startup audit, monotonic freshness, expiry, cross-process invalidation, cancellation/failure and publication fencing before shortcuts. |

Other recorded discoveries and limitations are retained in full in the evidence:

- A filesystem-only rollback of a valid control checkpoint while SQL retained a
  newer receipt was reproduced; exact independent SQL receipt anchoring now rejects
  it. Ambiguous old checkpoints need explicit verified maintenance.
- A bounded physical-database-growth admission stop was reproduced and corrected.
  This does not explain every older intermittent stopped outcome.
- The original injected post-SQL-commit stopped outcome remains unattributed.
  Eleven fresh-process recovery observations pass; passing repeats do not erase
  the original failure. Failure messages now include the stopped reason without
  weakening expected status or ciphertext-count assertions.
- A separate `email_sink_tracked` trial was traced to sandbox-dependent visibility
  of an empty `/tmp/.git` marker. Private ignored mode-0700 TMPDIR isolates it in
  final tests. The Git-exclusion security guard remains, and this mechanism is not
  asserted to explain the older post-commit stop.
- Initial pooled-buffer and cache-every-part trials include regressions. They are
  retained; later accepted local gains do not convert earlier trials into passes.
- Frozen and rebuilt DLL hashes differ when informational Git metadata changes.
  Source hashes and all 2,155 managed method bodies/signatures/locals/exception
  regions match in the scan comparison; both binary provenances remain explicit.
- Real-browser cache/transfer checks, licensed tooling availability, representative
  live providers and actual VPS behavior have limits recorded in the original audit.

## Completed work and verification

Counts below are recorded stage results, not tests newly executed while creating
this document. Do not sum different stages into a fictitious distinct-test count.
The latest 848-check integration gate combines the full run with the final affected
project result after two test-only additions. The later scan ran targeted checks.

| Stage | Local commit/checkpoint | Recorded verification / outcome |
| --- | --- | --- |
| Initial fixes | `0ad4054`, `93a36e6`, `ea11724` | P04/P10/P11/P17 fixed; partial P02/P03/P08 work retained. Original audit carries the exact controls. |
| Plan | `f8263fd` | Complete sequential design, mandatory joint P01/P19/P20 review and D1–D4 approved. |
| G0 | `db8ae78` | Five diagnostics regressions, shell/help/health and real task-launched capture/conversion/status propagation. |
| G1a | `8438408` | Authenticated checkpoint/pending operation/exact SQL recovery; 716 checks. |
| G1b | `95ee516` | Exact statement accounting, strict trigger/schema guard and prepared query; 725 checks, plus minimum-version PostgreSQL verification. |
| G1c foundations/correction | `7f92f4e`, `51645ad` | Capture/recovery/privacy foundation and continuous protection-key proof; intermediate failures retained in evidence. |
| G1c completed writer integration | `383083b` | 769 checks; 163 DataAccess checks on PostgreSQL 16.15; three 111-recipient offline probes with exact accounting and no reconciliation between cohorts. |
| G1d0 | `bd26135` | Shared bounded full-hash buffers; 770 checks; matched allocation/CPU/latency gains with full reads unchanged. |
| G1d1 | `540feb2` | Immutable manifests, charged metadata, durable enrollment/retirement and exact receipt anchor; 814 checks, 171 PostgreSQL 16.15 DataAccess checks. |
| G1d2 | `21a9d2e` | Consumed archive/recording buffer proof before parsing/decryption; 823 checks; strict full scans retained. |
| G1d3 | `ecba945` | Bounded generation cache, on-disk and consumed-buffer hashes retained; 834 checks, 171 PostgreSQL 16.15 DataAccess checks. |
| G1d4 | `862af96` | Internal metadata scanner; 848-check combined gate. Same-size/time corruption deliberately passes metadata-only checks but full admission and consumed verification reject it. No activation. |
| Targeted scan | `64d6b7d` | 154 distinct targeted checks: Infrastructure 116, DataAccess 29, Presentation 9; 11 separate fresh-process recovery observations; offline boundary/contention probes. No production change. |

The full evidence preserves migrations, failed trials, independently recomputed
accounting, ciphertext/manifest hashes, concurrency/failure tests, source/binary
identities and reproduction recipes. A local pass establishes only its tested scope.

## Important measurements and their limits

These selected results orient the reader. Every original table, sample qualifier,
CPU/memory/database/network observation and missed target is included in the
complete source appendices; this table does not replace them.

| Scope | Recorded result | Qualification |
| --- | --- | --- |
| Original P01 storage-only control | Five boundaries: 472.40 ms for 100 MiB; 2,202.93 ms for 512 MiB; approximately 2.68 GB of application reads in the latter. | Warm-cache storage component, excluding SQL/wait/network; not VPS disk traffic. |
| G1b 1,000-row INSERT | Client 66.838 → 21.202 ms; backend CPU 4,700 → 1,450 ms over 75 bulk statements plus controls; WAL 639,408 → 547,224 bytes. | Three matched process/database pairs. Small/cold statement costs and serialization/ledger contention remain. |
| G1d0 10,000-file five-pass control | Allocation 535.26 → 94.03 MB; wall 1,205.24 → 888.08 ms; CPU 3,410.94 → 1,782.77 ms. | Three matched pairs. Full payload traffic remains; this is not incremental verification. |
| G1d3 consumed reads | About 78% less allocation at 10,000 entries; 25-reader control improves, but sequential latency/CPU are mixed and sometimes regress. | Every on-disk part and consumed buffer still hashed; one SQL-state request per read remains. |
| G1d4 five isolated scanner validations | 512 MiB wall 1,518.694 → 14.564 ms; 10,000-file/100 MiB wall 1,087.495 → 403.537 ms. | Internal metadata helper, not production admission. 10,000-file improvement is 62.9%, below the proposed 80% latency target. |
| G1d4 regressions | Empty scanner wall 9.209 → 15.280 ms; empty allocation 0.631 → 1.129 MB. | Configuration/checkpoint-helper costs included; do not claim universal savings or activate on populated-only gains. |
| S03 reader contention | Median 5.407 → 133.268 ms; median process p95 6.371 → 167.737 ms; direct gate waits 70.666–188.073 ms. | Three fresh 100-read modes, bursts of 25 deliberately aligned with inactive metadata walks. SQL-state stub; not production-frequency tails. |
| Original P06 export stress | 10,000 recommendations: 52,958,047 JSON bytes, 439,553,520 bytes mapping/serialization allocation; four exporters peak around 1.21 GB. | Synthetic component process peaks; complete exports must not truncate history. |
| Original P13 archive stress | One owned-copy export among 10,000 shared files: about 1,342.37 ms / 1,317.96 MB allocation. | Separate scope from SQL export mapping and full deployed memory. |
| P15 contention | Warmed unrelated write median approximately 54 → 189 ms during login. | Local actual-route contention; secure finalization/lockout checks cannot be removed. |
| P16 direct tags | 200 direct-tag recordings dispatch 20 radio + 20 recording metadata + two artist metadata requests. | Target removes two unnecessary artist requests while retaining fallback. Paced queue budget is not measured live-job latency. |
| P18 compression sample | Public response 5,877 identity / 1,806 gzip bytes; cold static CSS/JS total 89,152 bytes. | One bounded sample; no site-wide or production latency saving established. |
| Final offline local delivery | All 111 recipients advance; repeat ticks empty; exact 5,440,279 logical SQL bytes; 111 ciphertexts / 1,567,875 bytes; six parts / 8,976 bytes. | Local encrypted capture, no `SentAt`. Functional probe overlaps test-only verification, so its elapsed outputs are not matched performance. |

## Approved contracts and remaining decision

The complete joint design and alternatives follow in the source reports. Core
requirements remain binding:

- Keep Onion/provider boundaries, deterministic scoring and stored explanations.
- Authenticate storage independently of mutable roots and SQL state; SHA-256 the
  actual consumed bytes before parsing/decrypting. Never use mtime as content proof.
- Keep exact transaction/receipt/revision/accounting semantics, original-ciphertext
  recovery, consent/ownership checks, key protection and crash-safe publication.
- Use canonical owner → global database admission → filesystem lease → ledger/rows
  ordering where needed; provider/render/crypto preparation stays outside shared
  write admission. Never wait for an owner while holding a later global lease.
- Preserve authentication, lockout/session/revocation and complete export/deletion.
- Preserve compatibility, explicit migrations/backfills and stopped, verified
  maintenance; no silent adoption of unknown rows/files or weakened trigger checks.

Already approved decisions:

| Decision | Approved contract |
| --- | --- |
| D1 | Registered immutable artifacts, independent authentication, verified consumption, startup/recovery full scans, refresh at 30 seconds and maximum completed-full-audit age of 60 seconds. Stop uncertain/failed/expired mutations. Strict mode stays available; incremental mode is not yet enabled. |
| D2 | Complete consistent export snapshot, authenticated owner archive view, private atomic staging, 10-minute reuse, 256 MiB aggregate staging, one builder and 60-second build cap. Private plaintext staging fallback approved. Bound immutable chunks to 8 MiB without truncating history. |
| D3 | Additive bounded operational recovery/accounting/index metadata with exact charging, privacy inventory, lifecycle, migration and reconciliation. No raw account identifiers in opaque artifact/control metadata. |
| D4 | Valid nonempty direct recording tags may make artist acquisition `not_requested`; retain actual failure statuses and fallback. Negotiate upstream compression with a 1,000,000-byte decoded cap. Public-static proxy compression only; no blanket private-response compression. |
| Privacy exception | Independently trustworthy SQL allows narrow owned SQL/credential reclamation despite unrelated artifact corruption. Preserve the prior expected manifest as `Reconciled=false`; ordinary generation/publication remains blocked until full verification restores trust. Lost keys/bad MAC/unknown SQL/pending uncertainty still reject. |

**Pending S01 decision:** recommended CLI-only re-enrollment when signed storage is
configured, retaining unsigned local QA. The alternative is a new explicit operator
web-maintenance authorization/quiescence contract covering both hosts, admission
and ledger verification. Do not invent that role or web protocol, or infer approval
from a request to consolidate these reports. Production currently does not map the
Development helper, and no anonymous access or authenticated HTTP exploit is claimed.

## Remaining implementation and verification

| Order / dependency | Work still required | Acceptance still needed |
| --- | --- | --- |
| G1d caller closure | Resolve S01 policy, reject pacing/root overlap, preserve private catalog-input contract, remove metadata/reader gate contention within the cache bound. | Policy/auth/CLI/legacy compatibility, reader concurrency, cancellation/eviction/retention and fail-closed byte/namespace tests. |
| G1d trust and activation | Process-local startup full audit, 30-second refresh and monotonic 60-second completed-audit deadline in both hosts, including jobs off. | Restart with persisted fresh stamp, wall rollback, late/cancelled/failed audit, cross-process invalidation, quarantine and publication fencing. Metadata-only work never refreshes full-audit trust. |
| G1d performance | Compare real DB-only admission at original shapes and changed payloads; retain full recovery. | Target ≥90% less application read traffic and ≥80% lower storage-phase median latency; include startup/audit pauses, fixed-workload total CPU/I/O, memory and worst admission wait. Current isolated 10,000-file target is missed. |
| G2 after G1 | Complete atomic staged exports and owner-indexed archives, safe legacy migration. | Every stored private section, unknown score fields, consistent snapshot, owner isolation, delete/cancel/crash behavior, quotas/headroom, download authorization and measured RAM/disk/fsync/SQL costs. |
| G3 after G1 | Narrow authentication admission. | Pure password work outside shared admission; atomic lockout/session/security-stamp finalization, no stale acceptance or revocation regression; matched login/write contention. |
| G4 after G1/P19 | Transactional feedback/input revisions, required data projections and exact public-cache quotas. | All EF/raw/bulk/expiry/eviction/deletion/retry paths, backfill/reconciliation, exact rows/counters and measured plans/WAL/lock/trigger impact. |
| G5 | Scorer snapshot allocation and fresh graph reuse. | Benchmark seeds, complete deterministic selected-output/explanation equivalence, validated identity/ownership/lifetime, measured CPU/allocation. |
| G6 | Sufficient recording evidence, negotiated upstream/static compression, justified response buffering and metadata coalescing. | Request counts, encoded/decoded bytes, hashes, limits/cache/rate rules, fallback/provenance/cancellation; real browser caching separate from intercepted fixtures. |
| G7 after implemented groups | Preserved fixes, combined concurrency/load, migration/recovery/soak and final dispositions. | Reproduce/investigate the old post-commit stop without weakened assertions; bounded local soak is not production stability. Preserve P04/P10/P11/P17 and accepted P05/P07 triggers. |

G5/G6 may proceed independently when a G1 decision blocks only its dependent work,
under the approved plan. No live provider, external sending, public deployment,
infrastructure purchase or push is authorized by that plan or this document task.

## Operational and external acceptance requirements

- Verify selected VPS CPU/RAM, filesystem/fsync/storage latency, PostgreSQL/WAL/
  vacuum/backup behavior, complete storage/headroom inventory and resource pressure.
  The recorded 2 vCPU / 2 GiB / at least 25 GiB SSD estimate is not measured capacity.
- Verify the selected reverse proxy's TLS, ETag/Vary/304, encoding behavior and
  public-static compression boundary without compressing private dynamic responses.
- Obtain explicit authorization for current real provider endpoints/terms/quotas,
  representative cold/warm latency, cache headers and failures. Historical captured
  public samples do not authorize new traffic.
- Verify representative histories, concurrency, large exports, sustained queue/disk
  pressure and long-running recovery on the actual host.
- Select/authorize a real mail transport and verify realistic opt-out, idempotency
  and delivery behavior. Offline encrypted capture is not inbox delivery and cannot
  establish external exactly-once semantics.
- Stop writers, verify a matched backup/accounting baseline and resolve pending
  operations before migration/enrollment. New-format rollback generally keeps the
  new strict binary, or needs reviewed conversion/matched database+artifact+key
  backup. Removing configuration or issuing a raw Down migration is not a safe
  downgrade. Never recreate deleted keys or recapture published historical receipts.
- Recheck actual installed tools/versions before reuse. Use the pinned private SDK,
  appropriate diagnostics and task-owned private fixtures; never attach without an
  authorized PID or collect sensitive dumps by default. Freeze comparable binaries
  and keep timing processes separate from builds/tests/profilers.
- Keep raw email, keys, recordings, databases, HAR, dumps and large traces private
  and ignored. This consolidation embeds human-readable reports and reproduction
  guidance, not those sensitive/large artifacts. Evidence locations can be absent
  in another checkout; preservation notes do not replace fresh comparable runs.

## Complete source reports and handoff snapshots

The following appendices contain full source text, with heading depth and local
links adjusted for this root document. Full Markdown source SHA-256 and byte length
refer to the original source, not its rendered adaptation. The MEMORY handoff is
explicitly a performance-only excerpt; unrelated user preferences are not copied.
PROGRESS, CHANGELOG and renewal contracts are included in full for chronology and
binding context; their unrelated project entries are not new performance findings.

The current register above takes precedence over obsolete implementation/approval
status prose preserved in dated historical snapshots. Original files remain in
place. If they change later, this dated consolidation must be regenerated/reconciled;
it is not a live include. No source is deleted or its older failure evidence hidden.

| Appendix | Source / included report | Inclusion |
| --- | --- | --- |
| [A01](#source-audit) | `Performance_Network_Audit.md` — Original audit, runtime investigations and complete coverage ledger | Full source |
| [A02](#source-plan) | `docs/renewal/performance-remediation-plan.md` — Approved sequential plan and joint P01/P19/P20 design | Full source |
| [A03](#source-evidence) | `docs/renewal/performance-remediation-evidence.md` — All implementation stages, measurements, failures and verification | Full source |
| [A04](#source-capture) | `docs/renewal/performance-capture-integration-notes.md` — Historical capture integration notes and later qualifications | Full source |
| [A05](#source-manifest) | `docs/renewal/performance-manifest-integration.md` — Manifest, receipt, consumption, cache and metadata integration | Full source |
| [A06](#source-scan) | `docs/renewal/storage-boundary-scan.md` — Complete S01–S05 scan, matrix, probes, pending policy and remaining checks | Full source |
| [A07](#source-privacy) | `docs/renewal/privacy-reclamation-integrity-review.md` — Approved narrow privacy-reclamation exception and tests | Full source |
| [A08](#source-storage) | `docs/renewal/guest-discovery-storage.md` — Storage, accounting, coordination and recovery contracts | Full source |
| [A09](#source-contracts) | `docs/renewal/contracts.md` — Full binding renewal contract context | Full source |
| [A10](#source-diagnostics) | `diagnostics/README.md` — Diagnostics operations, tooling limits and reproducibility | Full source |
| [A11](#source-accounting-control) | `diagnostics/benchmarks/ManagedStorageAccounting/README.md` — Exact SQL accounting controls | Full source |
| [A12](#source-inventory-control) | `diagnostics/benchmarks/ArtifactInventory/README.md` — Full versus metadata storage controls | Full source |
| [A13](#source-consumption-control) | `diagnostics/benchmarks/ArtifactConsumption/README.md` — Consumed-read controls | Full source |
| [A14](#source-boundary-control) | `diagnostics/benchmarks/StorageBoundaryScan/README.md` — Offline boundary and reader-contention probes | Full source |
| [A15](#source-progress) | `PROGRESS.md` — Full current project progress and historical context | Full source |
| [A16](#source-changelog) | `CHANGELOG.md` — Full recorded change chronology | Full source |
| [A17](#source-handoff) | `MEMORY.md` — Performance handoff excerpt only | Lines 84–97 |

Snapshots below preserve statements as originally recorded. Their presence does not approve a pending proposal or reclassify a failed trial.

---

<a id="source-audit"></a>

## Appendix A01 — Original audit, runtime investigations and complete coverage ledger

Original: [Performance_Network_Audit.md](Performance_Network_Audit.md). Full source text. Original source: 237,131 UTF-8 bytes, 1,343 lines.

Source SHA-256: `4946988cb406b2a7af8fb75c93d861d560e205df94d94b903de4c0e6e9fedcb1`.

<!-- BEGIN SOURCE SNAPSHOT: Performance_Network_Audit.md -->

<a id="source-audit--performance-and-network-audit-static-review-and-profiling-plan"></a>

### Performance and network audit: static review and profiling plan

Initial audit date: 2026-10-24; latest runtime continuation: 2026-10-27 (Asia/Baku)
Repository baseline: `3ae263c73dc1f16510a8033481b51a14cf349e16`
Audit execution baseline: `a930ebb87246a4709492ae06a543f996f85baac0` (approved plan commit; product sources unchanged).
Continuation baseline: `eac7d74b3d6f4b21841a6d4ad4ad3f2be0a6419e`.
Status: **Static baseline: 495/495 files, 63,948 lines accounted for. Four findings addressed and three partially addressed by earlier commits. The 2026-10-25 runtime audit below adds CPU/allocation/heap, SQL, contention, network, browser and resource-limit evidence, including new P18–P20 and tooling T01. Actual VPS and representative live-discovery acceptance remain unverified. The comprehensive remediation plan and D1–D4 are now approved for sequential implementation (2026-10-25); see the current implementation checkpoint below. Developer confirmation remains pending.**


<a id="source-audit--approved-implementation-checkpoint--2026-10-27"></a>

#### Approved implementation checkpoint — 2026-10-27

The [targeted storage-boundary scan](#source-scan) is now
documented with a reader/writer matrix and reproducible offline probes. Confirmed
items include Development HTTP re-enrollment policy, deliberately overlapping
pacing configuration, and cache-gate reader contention in the inactive metadata
scanner. Catalog mutable-stream behavior is qualified against current private
in-memory callers. Eleven fresh-process recovery checks pass without attributing
the old intermittent stop. This scan changes no production behavior and does not
close P01/P19/P20 or the outstanding activation/deployment gates.

G1d4 adds an internal metadata snapshot scanner, exercised only by tests and
diagnostics. Final local verification passes **848/848 checks** and the offline
Node/TRX gates. Production mutation admission retains full content hashing.
Matched scanner controls reduce populated-fixture read traffic and CPU, but
10,000-file latency improves only 63% and empty scans regress; this does not meet
complete P01 activation acceptance. Strict consumed-read allocation changes by
less than 0.2% after the shared-cache visitor refactor; latency/CPU remain mixed.
One offline 1/10/100 delivery probe advances all 111 recipients with exact SQL
accounting and manifest verification. Startup audit trust, periodic 30/60-second
refresh/deadline enforcement, reader contention and remaining writer coverage
still precede incremental activation. G2–G7 and external acceptance remain open.
See [G1d4 evidence](#source-evidence--g1d4--internal-metadata-snapshot-foundation).

G1c writer integration is **implemented and locally verified**: all 769 checks
pass, plus all 163 DataAccess checks on PostgreSQL 16.15. Historical conversion,
recovery, ownership isolation, quiet-period unsubscribe and signed writer fences
are covered. Both composition roots require an independent integrity key for
combined discovery/local capture. The approved
[privacy exception](#source-privacy) preserves
owned SQL reclamation while uncertain artifacts block ordinary delivery.

Three final offline 1/10/100-recipient probes advanced all 333 recipients without
intermediate reconciliation; repeated ticks were empty. Each produced 111 encrypted
receipts, no externally sent digest, and independently exact SQL accounting. Median
finite-process times were 4.53/7.03/32.15 seconds including startup and the tick
interval. An earlier replay exposed an intermittent stop; a deterministic bounded
physical-growth failure was corrected and the original failure evidence retained.
These measurements demonstrate local delivery correctness; they do not establish a
latency improvement, live-provider health or production throughput. P01 incremental
auditing (G1d), dependent G2–G7 groups and deployment acceptance remain outstanding.
Detailed evidence, resources and limits are in the
[implementation evidence](#source-evidence).

G1d0's bounded full-scan hashing step passes the latest **770/770 tests**, both
offline Node regressions and the five-project TRX gate. It needs no migration and
does not activate incremental verification. See the detailed evidence for CPU,
allocation, host variance, empty/single-file costs and unchanged application reads.

G1d1's strict persistence/receipt-anchor stage passes **814/814 checks** and all
**171 DataAccess checks on PostgreSQL 16.15**. The new opt-in format charges live
and retired immutable metadata and recovers exact known operations. A reproduced
filesystem-only rollback of a control-only operation is now rejected by the SQL
receipt anchor. One manifest-enabled 1/10/100 probe advances all 111 accounts with
exact accounting; this is local functional evidence, not a production speed claim.
Full scans remain; verified consumption and incremental cache/audit admission are
the subsequent stages. [Integration and rollout](#source-manifest).

G1d2's verified-consumption stage is locally verified on 2026-10-27:
**823/823 checks**, both offline Node checks and the TRX gate pass, plus all
**171 DataAccess checks on PostgreSQL 16.15**. Published email and local recording
buffers are checked against authenticated expected hashes and the independent SQL
receipt before parsing/decryption. Pending recovery retains its signed-plan proof.
The uncached proof adds measurable CPU, allocation and metadata reads; no speedup
is claimed. A final offline probe advances all 111 recipients with exact independent
accounting and manifest/receipt verification. A retained PostgreSQL 16 failure
reported `email_sink_tracked`; private ignored test roots isolate the observed
sandbox `/tmp/.git` marker interference. The older post-commit stop remains
unattributed. Bounded cache, periodic deadline/auditor integration, complete
reader/writer coverage, incremental admission, G2–G7 and deployment acceptance are
still pending. See the detailed evidence for resource costs and limitations.

G1d3's bounded strict consumed-read cache is locally verified (2026-10-27):
**834/834 checks**, both offline Node checks, the TRX gate and all **171 DataAccess
checks on PostgreSQL 16.15** pass. Matched frozen controls show about **78% lower
allocation** for 10,000-entry reads; 25-reader CPU/batch latency improves, while
sequential results are mixed and include regressions. Metadata read traffic and
SQL-state requests are unchanged. The cache retains at most 48,000,096 bytes of
large live parts for one authenticated generation and still hashes the on-disk
part and consumed buffer. Tiny-part caching was rejected after measurement. The
final offline delivery probe advances all 111 recipients with independently exact
accounting and hashes. Full scans and all remaining incremental activation gates
remain; this does not close P01 or establish production performance. See the
evidence and integration guide for bounds, controls and limitations.

Earlier verified prerequisite checkpoints (activation statements below are historical):

The developer approved the [complete plan](#source-plan), including the mandatory joint P01/P19/P20 design and D1–D4. Earlier report-only and pending-approval statements below describe historical checkpoints.

T01 is addressed and locally verified: supported managed thread-time collection,
directory creation, converter filenames, report argument order and child exit status;
the related global ReportGenerator fallback recursion is also fixed. Five offline
regressions and actual task-launched capture/conversion/report checks passed. This
restores diagnostic tooling, not application performance. No production behavior has
changed at this checkpoint. [Evidence](#source-evidence).

G1a adds the authenticated storage/recovery foundation and bounded operational
receipt migration. All 716 Release tests passed with no failures/skips; both Node
checks and the TRX gate passed. Full hashing remains and signing is not activated
for the general runtime. This is a prerequisite, not an independent resolution of
P01/P19/P20 or a measured speedup. See the implementation evidence for scope,
matched controls and deployment limitations.

G1b implements statement-level DML accounting with exact existing byte/revision
semantics, strict trigger/function fingerprints and one prepared inventory query.
All 725 tests passed; all 143 DataAccess tests also passed PostgreSQL 16.15.
Three matched PostgreSQL 18.6 comparison pairs show bulk INSERT latency
66.838 → 21.202 ms, backend CPU 4,700 → 1,450 ms across 75 bulk statements,
statement WAL 639,408 → 547,224 B, and warm inventory reads 8.336 → 6.431 ms.
Single-row and first-use costs increased; the [evidence](#source-evidence)
records those costs, allocations, measurement boundaries, host variance and rollout.
P19's batching stage is locally verified; the joint protocol remains incomplete
until writer integration and incremental activation pass their acceptance checks.


<a id="source-audit--authorized-remediation--2026-10-24"></a>

#### Authorized remediation — 2026-10-24

Planning update (2026-10-25): the [comprehensive remediation plan](#source-plan)
maps every finding to a work group, accepted tradeoff or conditional investigation.
It includes the mandatory joint P01/P19/P20 design, concrete unresolved contracts,
migration/rollback and before/after acceptance requirements. This is a review
artifact, not implementation approval or new application performance evidence.
The original checkout's 150 manifest-listed continuation artifacts were found and
their lengths/SHA-256 hashes verified during preparation; none were copied into Git.

The developer subsequently authorized bounded fixes, leaving large design/policy decisions for later review. The static findings, line references and hashes below describe baseline `fa42ae4` and are retained as historical audit evidence; they no longer describe every current source hash.

| Finding | Current disposition |
| --- | --- |
| P01 | Direction approved; measured high-cost integrity architecture; implementation awaits review |
| P02 | Partially addressed: post-pacing cache reuse; in-flight coalescing remains open |
| P03 | Partially addressed: one full-body copy removed; bounded buffering/owning JSON retained |
| P04 | Addressed and regression-verified: server retry delay honored |
| P05 | Component contention measured; no current normal-lane bottleneck established |
| P06 | Staged bounded export direction approved; memory/serialization measured; implementation awaits review |
| P07 | Idle SQL measured; low-cost responsiveness tradeoff, lower priority |
| P08 | Partially addressed: invariant preparation reused; complete snapshots still built per candidate |
| P09 | Transactional revision direction approved; history costs measured; design/implementation await review |
| P10 | Addressed and regression-verified: indexed alias lookup in both merges |
| P11 | Addressed and PostgreSQL-verified: batched, eligibility-filtered item reads |
| P12 | Transactional totals direction approved; aggregate plans/costs measured; migration design awaits review |
| P13 | Per-owner archive direction approved; cross-owner cost measured; safe migration awaits review |
| P14 | Duplicate graph allocation measured; fresh-object reuse awaits design review |
| P15 | Narrower-boundary direction approved; actual login/write contention measured; security design awaits review |
| P16 | Sufficient recording tags direction approved; redundant artist calls measured; provenance design awaits review |
| P17 | Addressed and PostgreSQL-verified: one eligibility query, both checks retained |
| P18 | New: measured compression opportunity; proxy and decoded-limit design awaits review |
| P19 | New: measured managed-ledger trigger/lock cost; accounting design awaits review |
| P20 | New audit disposition: legacy scheduled capture guard blocks uninterrupted throughput |
| T01 | Tooling: configured CPU wrapper incompatible with installed CLI; direct captures succeeded |

First implementation group: P04 now honors numeric and HTTP-date `Retry-After` values, falls back to five seconds for missing/invalid headers, resumes ordinary two-second polling after success, and preserves tab/session cancellation. Long delays are split into timer-safe intervals without early HTTP retries. P02 is **partially addressed**: the ListenBrainz client rechecks shared memory after provider pacing and only consumes request budget when dispatching; overlapping in-flight acquisitions still need a separate coalescing design. P03 is **partially addressed**: parsing uses the existing bounded stream buffer, removing `ToArray`; complete-body buffering and the owning JSON clone remain for integrity/lifetime safety.

Verification: the new polling test failed on the original premature retry and passed after the change; the new transport concurrency test failed on duplicate dispatch and passed after the change. All 201 Infrastructure tests passed with zero skips; both dependency-free Node browser checks passed. VSTest required permission for its local socket outside the sandbox. No live provider calls were used. Larger decisions and subsequent implementation groups are recorded here as work proceeds; developer confirmation remains pending.

Second implementation group: P08's repeated taste sorting/magnitude and configuration hashing are prepared once per scoring enumeration, with separate cached hashes for the two existing formula versions. The onsite preparation and weekly ranking paths use this batch operation. Cached taste/policy dictionaries are read-only; scalar scoring and replay remain available. Every candidate still receives its complete snapshot, so P08 is **partially addressed**, with no snapshot/schema or scoring-policy change. P10's onsite and legacy weekly merges now find matching groups through an ordinal alias index and retain group preference, transitive identity, provenance and exclusion behavior. Duplicate-heavy group merging can still copy growing groups; no claim of universally linear complexity is made.

Verification: configuration-enumeration regression failed at 100 enumerations before the change and passes afterward; full Domain suite passed 150 tests, including benchmark seeds and replay. Application suite passed 139 tests, including transitive alias bridges, recording-evidence preference and legacy whole-group exclusions. A new fixture expectation was corrected to match `RawCandidateTrack`'s existing canonical path sorting; production path sorting was preserved.

Fresh isolated Release comparison on .NET 10.0.12: 400 distinct candidates, ten tags each, 271 taste tags, five selected picks, empty score evidence, fixed batch identity. Sixty warm-ups precede 25 synchronous samples; allocation uses `GC.GetAllocatedBytesForCurrentThread` and includes measurement overhead. Merge timing includes reflection invocation of the actual private method. Helpers/logs are under `/tmp/liner-perf-remediation/`; these are temporary local artifacts, not permanent capture storage.

| Operation | Before median allocated bytes | After median allocated bytes | Before median ms | After median ms | Output contract |
| --- | ---: | ---: | ---: | ---: | --- |
| Batch ranking | 13,068,520 | 2,940,728 | 13.7313 | 9.1366 | Same serialized selected-breakdown hash |
| Onsite merge | 43,254,096 | 1,431,024 | 10.4940 | 1.1732 | Same ordered track/tag/path hash |

Ranking SHA-256: `f29b93cf21bf901b9d21858ea70170d2f2ffe219f907d33d5b7b7792d952dcc0`; merge SHA-256: `e63f61168c44552ef22895379b10013214ebcd2d947d746d3e797d72e37d6323`. Hashes omit random transient track IDs. These fresh measurements use a different fixture/configuration from the older audit and must not be compared numerically with its table. Shared-host timing is indicative, not an end-to-end latency or retained-memory claim; real evidence graphs and duplicate distributions vary.

Third implementation group: P11 adds a provider-independent batched catalog read, folds publication eligibility into the same SELECT, preserves requested order/duplicates and omits unavailable IDs. Taste selection and completed lookup mapping use it. Retained private favorite fallback and owner checks remain; tag evidence is parsed once per row. P17 combines Identity and subscriber eligibility in one SQL command, retaining both soft-delete conditions and per-request revocation checks. No schema migration, dependency update, provider endpoint or pacing-policy change was made.

PostgreSQL command interceptors reproduced two SELECTs for one eligible item and two eligibility commands for a valid bearer token before the changes. Afterward: ten catalog items plus a duplicate/missing ID require one SELECT; an empty batch requires none; cancelled reads propagate cancellation; expired items are excluded. Authenticated requests require one eligibility command, and hiding either the Identity or Domain user immediately returns 401. Complete export, private ownership, guest flow, legacy adapters, model/migration consistency and process recovery remain covered by the full regression suite.

Final local verification: 702 Release tests passed, zero failures/skips (Domain 150, Application 139, DataAccess 131, Infrastructure 201, Presentation 81). The existing TRX gate verifies all five projects. Both `node scripts/checks/discovery_polling.cjs` and `node scripts/checks/phase1_browser.cjs` passed; `git diff --check` passed. Database tests used fresh task-owned PostgreSQL containers and disposable databases; their containers were removed afterward. Existing live data/settings were not used. Existing package-pruning and unrelated test-analyzer warnings remain; no dependency removal was attempted.


<a id="source-audit--original-decision-briefing--superseded-by-the-approved-directions-below"></a>

##### Original decision briefing — superseded by the approved directions below

- **P01 — Artifact integrity and write latency.** Each ordinary discovery write currently makes five full passes over configured artifact roots. Each pass recursively enumerates files and reads them to calculate SHA-256. The passes protect tamper detection, cross-process coordination, and fail-closed storage accounting, but they extend the shared write lease and sometimes the database transaction; heartbeats and unrelated writes can wait behind them. The choice is between keeping all five integrity checks, accepting their cost, or defining a trusted-writer contract: for example, only this process may change artifacts and every mutation must advance a durable generation/version. Under that contract, some repeated hashes could be replaced by version checks, while startup/recovery or periodic audits still hash content. That option is faster only if every writer (including crashes, operators, and other processes) obeys the contract; otherwise a changed file could escape detection. **Decision:** retain strict rehashing, or authorize design of a versioned/exclusive-writer integrity contract? Do not reduce checks before that contract is specified.
- **P06/P13 — Complete data export and shared email archive.** The account data export promises the user's full stored history. Its reader loads batches, jobs, recommendations/tracks, taste signals, aliases and mapped DTOs into overlapping in-memory arrays. Separately, when the local email archive is enabled, exporting one account scans the shared copies directory, reads and decrypts each copy before checking its owner, then searches that account's digests for matches. Cost therefore grows with both the exporting account's history and, for archive copies, other accounts' retained mail. Keeping the current design preserves complete, failure-atomic export behavior and the existing archive layout. A bounded redesign could stream or page database history and stage output privately before publishing the completed export; it could also store/archive by owner or add an indexed owner manifest, with a migration path for existing shared-directory copies. Direct streaming could leave a partial export if a later read/decrypt/serialization fails, while staging requires temporary storage, cleanup and disk-space limits. Per-owner storage must retain authentication before reads/deletes and safely migrate old copies. **Decision:** keep current semantics/layout and address only after realistic scale measurements, or authorize a design for staged bounded exports plus owner-indexed archive access and legacy-copy migration? No history should be omitted to save memory.
- **P09/P12 — Database work under shared write coordination.** Weekly generation materializes and fingerprints all historical feedback to establish the revision/snapshot it is processing. Public-evidence publication also calculates cache-wide byte and row totals before each quota decision, then may sort/select eviction victims. These database operations extend the shared mutation's lock/transaction duration; the cost rises with feedback history or cache entries. One possible redesign maintains revision and quota totals transactionally so each operation reads a small counter, and narrows locks around unrelated work. That introduces a correctness contract: every insert/update/delete/expiry/eviction path must update counters exactly once, including retries, concurrent processes and crash recovery. A missed update could grant excess quota or hide new feedback. **Decision:** keep deriving totals from source rows, or authorize a schema/transaction design for maintained counters after query-plan and contention measurements? This should be scoped separately for feedback revision (P09) and evidence quota (P12); a database migration is part of the latter design if counters are chosen.
- **P05/P15 — Who must wait for whom.** Prepared-pool acquisition uses 32 semaphore stripes; distinct cold pool keys that map to one stripe serialize during upstream discovery and hydration. Separately, when background jobs are enabled, login runs inside the global write admission used by discovery changes and heartbeats, so password-hash verification can hold that admission even for failed logins. The first may cause avoidable waits between unrelated pools; the second may delay unrelated writes during CPU-heavy password verification. Their real impact depends on concurrent reachability and traffic, so changing synchronization before measuring could add complexity without helping. Options for pool acquisition include retaining stripes or coalescing/waiting by exact pool key while allowing unrelated keys to proceed. For login, admission might cover only the lockout/session mutations, but then failure counts, lockout rules and successful session storage need a defined atomicity and cross-process concurrency model. **Decision:** first authorize a bounded contention measurement for both paths, then decide whether to redesign either boundary? Any redesign must keep provider pacing, password hashing, lockout/session guarantees and managed-write accounting intact.
- **P16 — Evidence required for recording-tag recommendations.** Hydration currently fetches and prepares artist metadata for candidates before checking whether the recording itself already has usable tags. For candidates with direct recording tags, the chosen score vector comes from those recording tags, so the artist request may add latency and network traffic without changing the selected vector. However, artist responses also provide supporting evidence and may report missing/gap information; dropping that request could change explanation or evidence-completeness behavior even when the score stays the same. Keeping it preserves current evidence behavior and cost. Skipping it for direct-tag candidates would reduce requests, but requires deciding what the stored recommendation should say about supporting artist evidence and whether a recording-only explanation is sufficient. **Decision:** require artist supporting evidence/gap reporting for every recording pick, or allow recording tags alone when present and represent artist evidence as not requested/not applicable? No acquisition was skipped pending that policy choice.

At this remediation checkpoint P07/P14, retained memory and deployment/provider profiling remained outstanding. The 2026-10-25 continuation below supersedes those investigation statuses and resolves the five direction questions. The older dotTrace/dotMemory limitations do not invalidate the subsequently captured free-tool evidence. Unrelated workflow, security-report and skill-observation changes remain excluded from task commits.


<a id="source-audit--approved-directions-and-audit-continuation--2026-10-25"></a>

#### Approved directions and audit continuation — 2026-10-25

These directions resolve the earlier preference questions; **they do not authorize implementation**. Existing completed remediations remain intact. The remaining audit must finish, its findings and prioritized remediation plan must be reviewed, and explicit implementation approval must follow before product changes.

- **P01:** Retain SHA-256 and the existing five passes. Investigate immutable artifacts, independently integrity-protected versioned manifests and incremental hashing. Full checks apply at startup, recovery, periodically and when integrity is uncertain. Define an external-process/administrator modification detection window, fail-closed behavior and the threat model, including limits against fully privileged host administrators. Preserve cross-process atomicity, storage accounting and crash recovery. A manifest alone is insufficient. The objective is ordinary cost proportional primarily to changed data; record this as outstanding in `PROGRESS.md`.
- **P06/P13:** Design bounded-batch staged streaming into private temporary storage, publish only complete exports, retain all history and failure atomicity, and use per-owner email archives with safe legacy migration. Address authorization, cancellation, partial failures, cleanup and disk exhaustion.
- **P09/P12:** Favor transactionally maintained revision/quota counters where appropriate, after measuring current costs. Cover concurrency, updates/deletes, expiry/eviction/retries, safe migrations, reconciliation/recovery and protection against quota bypass or stale revisions.
- **P05/P15:** Measure stripe and authentication admission contention first; selectively design per-key coordination or narrower critical sections where justified. Preserve password hashing, atomic lockout/session behavior, concurrency security, provider pacing, managed accounting and cross-process correctness.
- **P16:** Use sufficient recording tags without unnecessary artist requests. Define sufficiency, preserve deterministic scoring, provenance and explanations, distinguish not-requested from requested-but-unavailable evidence, and fetch additional evidence when needed.

Continue from the static audit and completed-fix checkpoint. Investigate all remaining/partial findings and new profiling discoveries. Use controlled representative workloads when live credentials, terms, rate limits or authorization do not permit live requests. Final reporting must distinguish measured and expected improvements, identify residual decisions and migration/security/correctness dependencies, and wait for implementation authorization.


<a id="source-audit--runtime-investigation-and-final-audit-disposition--2026-10-25"></a>

#### Runtime investigation and final audit disposition — 2026-10-25

**Boundary:** this continuation records decisions, measurements and an optimization plan. It changes no product behavior and implements none of the approved architectural directions. The four completed fixes and three partial remediations above remain intact. The 495-file ledger is historical static coverage, not a claim that every runtime path was exercised.

**Environment:** source checkpoint `f5c4c15`; Fedora kernel `7.2.11-200.fc44.x86_64`, 20 logical CPUs, approximately 31 GiB RAM; SDK 10.0.401/runtime 10.0.12. Evidence is under `diagnostics/results/audit-20261025/` on the repository's Btrfs filesystem, rather than volatile `/tmp`. PostgreSQL 18.6 ran in task-owned loopback-only disposable containers/databases. The API/browser hosts used random loopback ports, synthetic provider bundles and separate private storage. No existing website, live database or external email delivery was used. Two separately labelled, paced public metadata requests are the only live-provider measurements in this continuation.


<a id="source-audit--tools-and-measurement-limits"></a>

##### Tools and measurement limits

The configured health check passed. Installed `dotnet-trace`, `dotnet-counters`, `dotnet-dump` and `dotnet-gcdump` are version `10.0.750501`. Rider's OpenTelemetry `get_services` returned no services; licensed dotTrace analysis and a dotMemory MCP interface remain unavailable as previously recorded. These are no longer blockers to the CPU/managed-memory investigations: the free tools produced usable method stacks, allocation stacks, counters and three managed heap graphs.

`dotnet-monitor` was not found in PATH/global tools, and there is no BenchmarkDotNet project or cached BenchmarkDotNet package. No dependency was added to the application or measurement helper. Repeatable controls use `Stopwatch`, process CPU, GC counters, managed allocation counters, three fresh processes and explicit input sizes. They are not BenchmarkDotNet results or statistical confidence intervals. Coverlet was not run: this is a performance investigation, not a new coverage claim. `dotnet-dump` was available but a full process dump was unnecessary for the bounded-cache question; no native dominator/root or universal leak-free claim follows.

CPU and allocation traces launch the synthetic workload under the collector; heap collection is restricted to the audit-launched process's private socket. No existing application PID was attached. `dotnet-gcdump` forces collections and warns that this .NET 10 target uses lossy rather than .NET 11 Block buffering; all three graphs completed, but the forced collections are not normal request-pause measurements. Microsoft's documentation distinguishes [sampled thread time and allocation profiles](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace), [GC-induced heap graphs](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump), and [diagnostic port modes](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostic-port).

**T01 — Configured profiling wrapper compatibility.** `diagnostics/profile.sh:45` requests `cpu-sampling` through `collect`; the installed tool rejects it with exit 3 because that profile belongs to `collect-linux`. Its line 42 also contains a literal `\nmkdir` instead of a separate directory-creation command, and its printed report example puts the arguments in the wrong order. The profile rejection was reproduced; the other two defects are source findings. The audit used supported direct CLI commands. Tooling repair is proposed separately; the installed scripts were not changed.

Timings exclude seed construction unless stated. Query counts come from actual EF command interceptors or isolated host logs; host counts can include background probes. SQL command durations exclude some result consumption/materialization. CPU baselines, storage runs and query measurements were run sequentially. Short-run tiered-JIT results varied materially, so a separate, explicitly labelled `DOTNET_TieredCompilation=0` control was added for resource comparison; actual host and profiler workloads retained their normal runtime settings. Medians of operation medians are not end-to-end SLOs. No source, schema, dependency, concurrency or security configuration was changed in the product.


<a id="source-audit--cpu-allocation-retention-and-p08p14"></a>

##### CPU, allocation, retention and P08/P14

The 400-candidate compute fixture has 271 taste tags, ten candidate tags, one synthetic provenance path per candidate, five selected picks and empty scoring evidence. It is a bounded component/stress fixture, not a claim about typical user taste or full real-provider evidence graphs. Each repeatable operation has 60 warm-ups and 100 samples; three fresh processes provide the comparison. In the tiering-disabled native control, ranking takes a median 14.3168 ms and allocates 3,024,112 bytes; merging takes 1.0287 ms and allocates 1,431,024 bytes. These are current baselines, not new measured improvements over the earlier commits.

A separate 20-second default-runtime thread-sampling trace, analyzed over seconds 3–20 on the active compute thread, attributes 65.69% of sampled time to `BaselineAScorer.ScoreCore`, 21.68% to `PublicEvidenceCodec.DecodePool`, and 11.86% to `Encode`. This synchronous compute workload supports method-level hotspot attribution. The percentages are sampled thread time, not exact instrumented CPU per call; all-thread totals would misleadingly include the finalizer's waiting time.

**P08 remains partial:** batch invariants are already reused, but full candidate snapshots/contribution objects are still built before selecting five picks. Allocation stacks identify `ScoreCore` and tag/contribution collections as substantial sources. A future score-first/select/snapshot design could avoid much losing-candidate explanation work, but must preserve validation, deterministic ordering, formula-version hashes, replay and selected breakdowns. No saving for that future split has been measured; benchmark seeds and full serialized-output equivalence are required before accepting it.

**P14 is now measured:** a 400-candidate public-pool document is approximately 483 KB (482,395–483,197 UTF-8 bytes across clock-stamped controls). The tiering-disabled native encode/decode round trip takes 6.2028 ms and allocates 4,054,424 bytes; decode alone allocates 2,478,208 bytes. The fresh persistent adapter's encode-then-decode constructs another graph even when acquisition already produced one. Reusing the acquired object could avoid the measured decode allocation on an eligible fresh path; it does not remove the encoding needed for persistence. This is an expected opportunity, not a verified post-change result. The design must prove the returned persisted document is the same validated acquisition; stored-hit decoding, origin/expiry/byte limits and checksum/provenance checks remain necessary.

A separate default-runtime `gc-verbose` capture attributes approximately 2.29 GB of sampled allocation traffic to strings, 1.34 GB to string/double dictionary-entry arrays and 0.66 GB to byte arrays over its 20-second tight loop. Nearest application allocation stacks include `ScoreCore`, `DecodePool`, `Encode`, weighted-vector construction and ordering. These are allocation-event estimates, not retained sizes. That trace observed 1,370 suspend/resume intervals totaling 728 ms, maximum 3.66 ms. In a separate counter run, after excluding its first three observations, the median allocation rate was 480.7 MB/s, GC pause time 0.0415 s/s, Gen 2 collections 20/s and working set 93.6 MB. The workload deliberately saturates roughly one CPU core; these rates must not be presented as ordinary production traffic.

**Retention control:** 400 successive unique 400-candidate pool constructions exercise the actual bounded prepared cache. Warm and post-quiescence heap graphs contain 15 pools, 6,000 tracks and 6,000 hydrated candidates, at 24,125,777 and 24,379,259 managed heap bytes. After explicit cache compaction/eviction, those application object types disappear and the graph falls to 7,185,048 bytes. The cache's charged estimate was approximately 29 MB against its 32 MiB limit. This demonstrates bounded retention and releasability in this scenario; it establishes neither a cache leak nor universal absence of leaks. Remaining serializer/array-pool residency is not itself evidence of a leak.

Evidence: `cpu-final.nettrace`, `cpu-final.speedscope.json`, `cpu-attribution.json`, `allocation.nettrace`, `allocation-analysis.log`, `runtime-counters-final.csv`, `warm.gcdump`, `stable.gcdump`, `released.gcdump`, corresponding `*-heap.txt`, `memory-hold.jsonl`, `compute-repeat-*.log`, `jit-control-*.json`; captured helper/binary version in `captured-v1/`.


<a id="source-audit--storage-admission-and-p01"></a>

##### Storage admission and P01

The actual storage lease/reservation/validation/verification/publication sequence was measured without a database mutation. It makes the same five artifact passes as the write coordinator. Five samples per shape used private files on Btrfs with fsync on fixture creation:

| Artifacts | Files | Median storage-only time | Median allocated bytes | Application read bytes per write |
| --- | ---: | ---: | ---: | ---: |
| Empty | 0 | 4.02 ms | 153,304 | approximately 4 KB of control data |
| 100 MiB | 1 | 472.40 ms | 187,624 | 524,289,053 |
| 512 MiB | 1 | 2,202.93 ms | 182,120 | 2,684,355,596 |
| approximately 100 MiB | 1,000 | 907.42 ms | 51,799,664 | 524,291,162 |

`/proc/self/io` reported zero physical read bytes in these warm-page-cache runs. The read column is application read traffic, not SSD traffic; cold physical I/O, VPS storage latency and fsync behavior remain target-dependent. This also excludes SQL, transaction and admission wait. The results nevertheless establish that an otherwise tiny write can spend seconds processing unchanged artifacts, and many files add allocation/open/enumeration costs. Filesystem metadata alone was not substituted for content validation.

P01 therefore has measured high priority. The approved immutable/incremental/independently protected manifest direction should target work proportional primarily to changed data. A one-file ordinary mutation's expected reduction from five complete passes cannot be quantified before that architecture exists; startup/recovery/periodic full scans still cost collection size. Its design must specify the external-change detection window, trusted writers, manifest/key trust boundary, fail-closed recovery and limitations against a fully privileged host administrator. Preserve the current five passes until approved and verified. Evidence: `storage.jsonl`, `harness/Files.cs`.


<a id="source-audit--sql-exports-and-p06p09p11p12p19"></a>

##### SQL, exports and P06/P09/P11/P12/P19

Actual repository reads used migrated PostgreSQL and synthetic histories containing persisted score snapshots, recommendations/tracks and batches. Each read has two warm-ups and ten samples (quota aggregates have 30). No user data was omitted and no indexes/triggers were disabled.

| Operation | Records | Median elapsed | Allocated bytes/operation | SELECTs/operation |
| --- | ---: | ---: | ---: | ---: |
| Weekly feedback inputs/revision | 1,000 | 148.80 ms | 22,557,165 | 3 |
| Weekly feedback inputs/revision | 10,000 | 550.06 ms | 230,379,348 | 3 |
| Complete discovery export mapping | 1,000 | 142.49 ms | 24,552,196 | 7 |
| Complete discovery export mapping | 10,000 | 587.69 ms | 242,737,282 | 7 |
| Discovery page, 10,000-record owner | 10,000 | 0.98 ms | 82,982 | 5 |
| Public-evidence SUM + COUNT | 100 entries | 1.12 ms | 13,309 | 2 |
| Public-evidence SUM + COUNT | 10,000 entries | 4.19 ms | 13,284 | 2 |
| Public-evidence SUM + COUNT | 50,000 entries | 13.94 ms | 13,278 | 2 |

The histories are declared stress sizes, not estimates of real customer usage. Feedback's three SQL commands averaged approximately 56 ms at 10,000 records; much of its 550 ms elapsed and 230 MB allocation comes from loading/parsing/fingerprinting full objects rather than solely database execution. A revision counter alone does not eliminate data still needed by taste, familiarity or exclusions; the design must project/query those requirements separately and preserve their behavior. The maintained-counter direction needs all raw/EF update paths, retries and cross-process writers covered, with transactional reconciliation.

**P06:** 10,000 exported recommendations produce 52,958,047 JSON bytes. A fresh-process mapping-plus-serialization stress control allocates 439,553,520 bytes and peaks at 401,797,120 process bytes for one export. Four concurrent exports, each with its own DbContext, produce 211,832,188 output bytes, allocate 1,742,752,000 bytes and peak at 1,207,468,032 process bytes. These are exporter component process peaks, not a full deployed stack or a universal account maximum. Staged bounded output should reduce live graph/output overlap; its future peak is unmeasured. Define a consistent export snapshot, authorization recheck, private temporary disk budget, cancellation/cleanup and atomic publication before implementing it.

The account data screen fetches the complete export for preview and `downloadExport` fetches it again (`app.js:409`, `app.js:443`). This repeats the export operation for a preview/download journey; the measured 52.96 MB component body would be incurred twice if unchanged. That is an extrapolation from the operation and verified call path, not a captured 106 MB authenticated journey. Staged export reuse needs an explicit freshness, ownership and expiry contract; no historical data may be capped to avoid the cost.

**P12:** the 50,000-entry aggregate plan reads 50,000 rows in a sequential scan, 1,471 shared-hit blocks, no disk reads, approximately 12.52 ms database execution. This is warm-cache evidence, not a disk-bound claim. Maintained totals could remove this cache-wide scan from publication, but must account for leased/null-payload entries, replacement, expiry, eviction, failed publication and configuration shrink. Do not turn a payload-byte quota into an unprotected approximate counter.

**P19 — Managed-accounting trigger cost (new measured finding).** Location: [managed ledger trigger](src/DataAccess/Persistence/Migrations/20261017124050_AddManagedStorageLedger.cs#L32), reached by public-evidence/import mutations. `liner_storage_delta` updates the shared ledger and serializes row state for each affected row. An isolated 1,000-row public-evidence insert with `EXPLAIN ANALYZE` took 92.36 ms, including 81.39 ms in 1,000 trigger calls; its ModifyTable node reported 572,376 WAL bytes. A 50,000-row fixture insert took approximately 17.98 s. These insert shapes are stress controls, not the current bounded import's measured total cost. Two real connections updating the ledger row show 204 ms wait after a deliberately injected 200 ms hold; this proves contention mechanics, not an observed production wait distribution. Any counter/import design must consider this write/lock cost rather than assuming all counters are free. Explore transactionally equivalent batching only after preserving every-row coverage, core/full revisions, rollback, external writers and tamper detection. This needs accounting-design review; disabling triggers is not an optimization proposal.

**Index lead resolved for the measured shape:** recent recommendations over 10,000 catalog/recent rows use the recent primary-key scan and an index-only catalog lookup. The captured plan has 21 recent rows, 21 index probes, 90 shared-hit blocks, no disk reads and 0.58 ms execution; the repository's 30-sample median is 3.53 ms. No missing-index finding is established for this dataset. Catalog search returns its bounded ten-row page at 8.80 ms median using two reads. Multiple publications/lookup-heavy catalogs and cold disks can produce different plans; there is no justification here for a speculative index migration. P11's earlier batched-read fix remains intact.

Evidence: `db.jsonl`, `catalog.jsonl`, `quota-plan-{100,10000,50000}.json`, `recent-plan.json`, `trigger-insert-plan.json`, `export-peak-{1,4}.jsonl` and helper sources. Query-plan JSON and timing artifacts contain synthetic data only.


<a id="source-audit--p13--owner-archive-cost"></a>

##### P13 — Owner archive cost

The real serializer/protection/archive reader was exercised with authenticated encrypted copies and real owner checks. One account owns one copy; all other copies belong to distinct synthetic owners. No ownership or cryptographic check was bypassed.

| Shared archive | Owned copies | Median read/export time | Allocated bytes/operation |
| --- | ---: | ---: | ---: |
| 1,000 files, 7.21 MB encrypted | 1 | 310.51 ms | 132,087,448 |
| 10,000 files, 72.13 MB encrypted | 1 | 1,342.37 ms | 1,317,963,507 |

This confirms that the shared layout makes one owner's operation pay for other owners' reads/decryption/MIME parsing. It is allocation churn, not a 1.32 GB retained-object or leak claim. Per-owner storage/indexing should make that operation primarily depend on owned copies, but no post-change savings have been measured. A safe migration must authenticate existing receipts, retain encrypted originals until verified publication, handle interruption and prevent owner/path spoofing. Sanitized export and deletion must keep their existing authorization/ownership guarantees. Evidence: `archive.jsonl`, `harness/Files.cs`.


<a id="source-audit--concurrency-queueing-and-p05p07p15"></a>

##### Concurrency, queueing and P05/P07/P15

**P05:** the actual preparation component with an injected 150 ms synthetic source delay coalesces 20 identical requests into one source acquisition. Two distinct keys on one stripe take 306.54 ms with source concurrency one; two keys on different stripes take 153.25 ms with concurrency two. A 33-key burst takes 464.46 ms, maximum source concurrency 23. These controls prove the collision mechanism and its conditional cost.

However, the normal web job host has one acquisition lane. Both ten-job endpoint bursts below completed with maximum one Running job observed; same-pool versus distinct-pool completion times were similar. The audited normal route does not establish semaphore-stripe contention as the current end-to-end bottleneck. Keep the bounded design unless a wider concurrently reachable acquisition path justifies finer locking; per-key coordination must also bound key memory and preserve coalescing/cancellation. Do not infer that replacing stripes alone will speed the present queue.

**P15:** actual HTTP login and guest writes were tested with secure hashing retained. Six matched pairs per mode were run; discard each mode's cold first pair. With job admission enabled, the last-five guest-write medians are 53.64 ms alone and 188.59 ms when launched 20 ms after a valid login. With authentication admission disabled, corresponding medians are 58.27 and 57.66 ms. Known wrong-password and unknown-account requests still returned 401 in separate controls. This isolates a meaningful shared-admission penalty for unrelated writes; it does not attribute the entire difference to hashing alone. A narrower authentication section must preserve account eligibility, concurrent failure counters, lockout, session/token state and cross-process atomicity. The expected benefit is overlapping unrelated work, not weakening password verification. No implementation comparison exists.

**P07:** a quiescent host issues zero SQL commands in ten seconds with jobs disabled and 30 with jobs enabled: roughly three readiness probes per second per web host, before minute-level maintenance. A warmed empty probe takes 0.88 ms median in the isolated database. That is approximately 259,200 probes/day if continuously idle, a derived count rather than a day-long capture. It is currently a small measured cost in exchange for roughly one-second poll responsiveness. Adaptive polling/notifications are lower priority; their expected idle reduction must be weighed against wake latency, durable queue recovery and cross-process notification correctness.

**Actual distinct-owner HTTP controls:** 25 separate guest sessions were created; all 144 read requests succeeded. At 1/10/25 concurrent owners, median overview latency was 16.39/20.04/23.94 ms, with observed p95 17.06/40.10/42.48 ms. Requests normally perform one guest-resolution query plus the reader's five overview reads; the ten-owner sample also includes three background probes. Two ten-job generation bursts completed all jobs: same-pool observed median/max completion 4.75/5.30 s; distinct-pool 4.77/5.72 s, measured by 250 ms polling. These are synthetic-provider, empty-artifact, loopback results at the declared dataset size, not live-provider throughput or an internet-user SLO. The host reached approximately 252 MiB resident memory after those journeys on this 20-core development machine.

Evidence: `pools.jsonl`, `gates.jsonl`, `auth-repeat-results.json`, `auth-repeat-host.log`, `host-results.json`, `multiuser-results.json`, `multiuser-host.log`.


<a id="source-audit--network-provider-evidence-and-p02p03p04p16p18"></a>

##### Network, provider evidence and P02/P03/P04/P16/P18

The controlled server uses real loopback TCP and the production metadata client's request/parse/cache logic. Five cacheable reads cause one request; five `no-store` reads cause five requests over one connection. A twenty-way overlap with pacing deliberately bypassed produces twenty requests and twenty connections; that is an upper-bound race control, not the normal production gate's behavior. Two audit-launched processes with separate response caches and one real shared pacing directory each fetch the same metadata once: two requests remain necessary in that process-local-cache setup. Six parallel calls to the real pacing gate are granted at approximately 0/1.05/2.10/3.15/4.20/5.25 seconds. P02 is consequently partial and conditional on reachable overlap/process boundaries; current pacing and the post-pacing cache fix must remain intact.

**P03 transport allocation control:** with two warm-ups and 30 samples through a non-network message handler, valid no-store JSON bodies of 10,011 / 100,011 / 900,011 bytes allocate 47,452 / 367,187 / 3,002,446 bytes per parse, at medians 0.166 / 0.563 / 1.450 ms. The returned JSON remains readable after HTTP response disposal. This isolates current bounded buffering, buffer growth, hashing and owning-JSON work; it excludes socket transfer and provider hydration and is not a retained-size measurement. Further allocation reduction must retain that independent lifetime and the one-million-byte decoded response limit. No additional saving was implemented or measured. Evidence: `response-memory.jsonl`, `harness/ResponseMemory.cs`.

The controls also confirm: 429 responses register the supplied 60-second defer signal; no internal HTTP retry loop runs; cancellation propagates; oversized response bodies are rejected; forced unnegotiated gzip is not parsed as JSON by the current identity-only transport. Oversize server byte counters describe intended application writes and must not be treated as received TCP bytes. The remaining P03 body buffering/owning clone has a real byte cap and lifetime purpose; no retained-body leak was established. Further stream/JSON ownership changes need separate validity, hash and cancellation controls. P04's previously verified Retry-After browser fix remains intact.

**P16:** real provider hydration over synthetic metadata returns recording-scoped evidence when direct tags exist, but still issues artist requests first. At 200 distinct recordings/artists it dispatches 20 radio, 20 recording-metadata and two artist-metadata responses, totaling 121,042 synthetic payload bytes; warmed hydration allocates about 2.00 MB. The two artist responses are unnecessary for the selected recording vector under the approved direction, but their supporting/gap semantics still need an explicit representation. Under saturated 1.05-second pacing, avoiding two dispatch slots could save approximately 2.1 seconds of queue budget; this is a conditional estimate, not a measured live-job speedup. Without direct tags the same fixture correctly selects artist evidence. The bounded `responses.Values.FirstOrDefault` lead remains part of hydration's measured aggregate, without isolated proof that indexing it is worth a separate change.

**Live spot check:** public metadata terms were checked against the official [metadata endpoint](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html) and [HTTPS/User-Agent/rate-limit requirements](https://listenbrainz.readthedocs.io/en/latest/users/api/). Two unauthenticated requests for the documentation's public recording ID used the application's shared live pacing directory; no personal endpoint, token or retry was used. Both returned 200; rate-limit remaining was 29 then 28. The identity response body was 5,877 bytes; negotiated gzip was 1,806 bytes and decoded to the identical SHA-256 content: **69.3% fewer response-body transfer bytes for this one sample**. Total times were 446 ms and 91 ms, but the second request reused a connection; these timings do not isolate a compression latency improvement. Headers included `public, max-age=0, s-maxage=120`, so cache eligibility has an explicit server-provided shared lifetime. This is a two-response spot check, not representative live discovery latency, an authenticated quota benchmark or permission for load testing.

**P18 — Compression configuration (new conditional network optimization).** The configured `HttpClientHandler` does not advertise/decode compression (`src/Infrastructure/DependencyInjection.cs:103`); the current upstream path therefore leaves the demonstrated body-byte saving unused. Direct Kestrel also returns the static assets uncompressed despite `Accept-Encoding: gzip, br`. The three CSS/JS bodies total 89,152 bytes. Compression could reduce cold transfers, but deployed proxy behavior is not established because no VPS exists. Proposed scope: negotiate supported upstream compression and configure compression for public static assets at the selected proxy/host. Retain decoded-size limits, payload/provenance hashes, cancellation and cache directives; do not apply private-token-bearing dynamic compression indiscriminately. Only the sampled provider byte reduction is measured; site-wide bandwidth and latency gains are unknown.

**Browser:** cached Chromium/Playwright exercised the real isolated host without API interception. Desktop cold/warm load was 73/36 ms; throttled mobile (150 ms latency, 200 kB/s down, 4× CPU slowdown) was 841/375 ms. No page errors or long tasks were observed in that route. Static warm transfers fell to about 900 bytes of revalidation overhead across the three CSS/JS files, rather than repeated full-body downloads; direct ETag requests returned 304. An initial routing-enabled browser run was discarded because [Playwright routing disables HTTP cache](https://playwright.dev/docs/api/class-browsercontext#browser-context-route). The browser clock is simulated mobile throttling on this host, not a physical mobile device or deployed CDN/proxy measurement.

Evidence: `network.jsonl`, `live-spot.jsonl`, `hydration.jsonl`, `browser-results.json`, `host-recheck.json`, associated helper/log files. The first generation control's cache-row count exceeded its configured quota; it was corrected to the intended 50,000-entry test envelope and the isolated generation then completed. That mismatch is not counted as a product defect.


<a id="source-audit--p20--legacy-scheduled-capture-stops-on-a-fresh-uninterrupted-tick"></a>

##### P20 — Legacy scheduled capture stops on a fresh uninterrupted tick

Current disposition (2026-10-26): G1c repairs the locally reproduced writer integration failure. Three final fresh offline probes each advance all 111 recipients with exact accounting and empty repeat ticks. The measurements below describe the original failure controls. Production scheduling, external delivery and long-running deployment acceptance remain open.

The actual Worker CLI, production migrations, deterministic scoring, database persistence and encrypted local-email configuration were exercised for 1, 10 and 100 due synthetic recipients. Both the larger history database and a separate small database were investigated. In the small control the finite ticks take 3.02/3.37/4.58 seconds and stop 1/10/100 jobs respectively with `database_inventory_changed`; no mail files are published. **These are failure/guard timings, not successful delivery throughput.** An initial malformed schedule fixture was discarded and corrected to the scheduler's exact weekday/hour requirement before these controls.

Locations: [scheduler](src/Application/Features/Digests/Commands/RunScheduledTick/RunScheduledTickCommand.cs#L58), [legacy persistence return](src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs#L135), [capture lease](src/Infrastructure/Email/LocalEmailSink.cs#L41), [inventory validation](src/Infrastructure/Storage/LocalGenerationStorage.cs#L133). Trace the operation across these components: generation commits a digest and changes core accounting, but that branch does not publish the committed storage inventory before immediate capture checks it. The persisted digest and newer core revision remain, while capture rejects the older inventory. Existing `Phase8ScheduledWorkerPostgresTests` explicitly reconcile/retry this guarded flow; the stop therefore confirms an existing operator-recovery dependency, rather than disproving the prior passing test suite. A separate selection probe changes only full revisions and leaves core revision/bytes unchanged, ruling out the earlier assumption that selection itself explained the core mismatch.

Current configuration also rejects scheduled local capture with `DiscoveryRuntime.RunJobs=true`; turning that flag on is not an available bypass. P20 is an operational throughput blocker for unattended weekly delivery and must be included in the accounting/writer integration design. Preserve fail-closed protection, durable receipts, idempotency, restart recovery and ownership. The proposed optimization is a verified known-write inventory/publication protocol across generation, capture and scheduling, not automatic blind reconciliation. Restored successful unattended throughput is expected but unmeasured. This requires explicit implementation/design review alongside P01 and existing local-capture integration work. Successful 1/10/100 delivery capacity cannot be certified from the present path without changing that behavior, which is outside this audit's authorization.

Evidence: `worker-results.json`, `worker-summary.log`, `schedule-{1,10,100}.log`, `worker-probe.jsonl`, private small/large fixture definitions, and the cited production/test call paths.


<a id="source-audit--minimum-working-vps-estimate"></a>

##### Minimum working VPS estimate

The developer confirmed no VPS has been bought. A **practical initial estimate is 2 shared vCPUs, 2 GiB RAM and at least 25 GiB SSD/NVMe**, running prebuilt Release artifacts with the web job host and PostgreSQL on the same VPS. For several concurrent large exports before staged streaming is implemented, **4 GiB RAM is the more defensible budget**. This is a sizing estimate for a small initial deployment, not a tested user-capacity promise, a purchase recommendation for a named vendor, or evidence that the scheduled-delivery blocker is solved.

The owned PostgreSQL container reported 115.3 MiB idle after the fixture runs; this is a Docker working-usage observation, not a future memory reservation. The measured web scenario uses roughly 252 MiB, while one large exporter component peaks around 383 MiB and four around 1.12 GiB before adding the rest of the web host, PostgreSQL and OS. Therefore a successful 512 MiB application-only container control does not establish that a complete 512 MiB or 1 GiB VPS is suitable. PostgreSQL's shared buffers, per-operation work memory and connection concurrency need their own budget; [its resource guidance](https://www.postgresql.org/docs/18/runtime-config-resource.html) explicitly distinguishes those costs. Likewise a [.NET container heap limit](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector) is not a whole-process or whole-VPS reservation.

Local resource controls used the installed ASP.NET 10.0.12 image, readonly application binaries, no network, 1 CPU/512 MiB and 2 CPUs/1 GiB, three fresh processes each. With tiered compilation disabled only in the benchmark control, ranking medians were 14.30/14.50 ms and encode/decode 7.20/6.39 ms; their median allocated-byte counts matched. Default-tiered short-run results differed much more due to warm-up/runtime effects and are retained as such, not used to claim a stable CPU scaling factor. These controls show the bounded compute workload can run in those envelopes, not full-stack capacity. Extra CPUs do not remove shared admission, queue policy or provider pacing.

Provisioning should leave room beyond managed data for PostgreSQL WAL/vacuum, private export staging, logs, container images and backup handling. The application's 1,000,000,000-byte managed stop is a separate accounting policy, not the VPS disk capacity. Builds, coverage and diagnostic collection should occur off this minimum runtime budget. Actual VPS disk latency, noisy-neighbor CPU behavior, reverse-proxy compression/TLS, internet latency, backup load and OOM/recovery behavior need a post-selection acceptance run. No VPS was provisioned or deployed.


<a id="source-audit--prioritized-plan-for-review--no-implementation-authorization"></a>

##### Prioritized plan for review — no implementation authorization

| Order | Scope and measured reason | Proposed work and expected benefit | Dependencies, risks and acceptance |
| --- | --- | --- | --- |
| 1 | P01/P20 and writer/accounting integration: seconds per ordinary write at 512 MiB; scheduled capture stops | Design changed-data integrity/manifests and known-write atomic publication/recovery together; expect less unchanged-data processing and uninterrupted valid workflow | Approve threat model/detection window; preserve tamper/privileged-admin limits, cross-process fencing and receipts; migration/reconciliation plan; failing guard controls must remain safe and successful full ticks must be proven |
| 2 | P06/P13: hundreds of MB/export, over 1 GB allocation for an unrelated-owner archive scan | Bounded staged complete exports, owner-indexed archives and reuse of a completed authorized export for download | Snapshot/freshness policy, temporary disk quota/TTL, disk exhaustion and cancellation, owner-safe migration and atomic publication; measure peak RAM plus bytes and compare complete exported data |
| 3 | P15: warmed unrelated write median 54 → 189 ms during login | Narrow admission around atomic auth/session mutations while retaining secure verification | Concurrency/security design, lockout/session/revocation regressions, no authentication bypass, cross-process tests; rerun matched contention controls |
| 4 | P09/P12/P19: history fingerprint allocation, cache-wide aggregate, ledger trigger cost | Transactional revision/quota totals plus required-data projections; inspect accounting batching without weakening coverage | Schema/backfill/reconciliation; update/delete/expiry/eviction/retry and all writers; avoid replacing scans with another hot-row bottleneck; before/after plans, WAL and wait measurements |
| 5 | P08/P14: snapshot/contribution and duplicate graph construction | Separate ranking from losing-candidate snapshots where equivalent; reuse validated fresh prepared objects | Preserve all validation, evidence, deterministic ties/formula hashes and replay; benchmark seeds, exact selected-output comparisons, byte caps and lifetime tests |
| 6 | P16/P18/P02: avoidable evidence dispatches and measured compression opportunity; conditional metadata overlap | Implement the approved recording-evidence direction; negotiate upstream/static compression; add coalescing only for proven reachable overlap | Define not-requested versus unavailable and sufficient-evidence fallback; preserve pacing, shared cache lifetime, decoded caps/provenance, failure and cancellation semantics; measure actual requests/encoded bytes |
| 7 | P05/P07 and overview-query lead | Retain bounded stripes/current polling initially unless wider acquisition concurrency or a queue-latency target justifies change; consider repeated overview-read consolidation if RTT becomes material | No present normal-route stripe bottleneck or missing recent index was established; do not add semaphore maps, notifications or migrations speculatively |

P18 needs a deployment compression-boundary choice; P19/P20 require reviewed accounting/writer integration. T01 is routine tool compatibility work. P05/P07 do not justify a redesign under the currently measured normal route without changed concurrency/latency requirements.

Implementation needs a reviewed design and separate explicit authorization. Approved directions resolve the five preference questions; the design still needs concrete acceptance values/contracts: P01 detection window and trust anchor, export snapshot/freshness/temp-disk policy, counter ownership/reconciliation, auth atomicity, and recording-evidence status semantics. This table supplies an implementation ordering, not permission to execute it. Expected benefits above are hypotheses constrained by measured baselines; no future speedup, capacity or savings is represented as achieved.


<a id="source-audit--coverage-reconciliation-and-remaining-evidence-limits"></a>

##### Coverage reconciliation and remaining evidence limits

| Workload | Current disposition |
| --- | --- |
| W0 idle | Real isolated host, jobs on/off, SQL rate; runtime counters in separate compute scenario |
| W1 guest/onboarding/catalog | Actual bootstrap/start/selection/generation, bounded catalog queries, 25 distinct owners; complete UI coverage is not claimed |
| W2 provider lookup/HTTP | Controlled TCP cache/overlap/error/cancellation/size checks, real pacing and cross-process cache boundary; two public metadata spot checks |
| W3 generation | Current scorer/merge/codec, history/feedback scales, actual successful job-to-result path |
| W4 concurrency | Component stripe controls, real login/write controls, 25-owner reads and two ten-job host bursts; no multi-host live-provider load test |
| W5 history/export | Real repository sizes, SQL plans, serialization and 1/4 concurrent large exports; authenticated preview/download duplication traced, not a captured large browser journey |
| W6 scheduled capture | 1/10/100 real CLI guard stops investigated at both scales; successful unattended throughput blocked by P20; no bypass/fix |
| W7 storage/maintenance | Actual five-pass storage sequence on Btrfs at 0/100/512 MiB and many-file shape, ledger wait/trigger controls; physical cold-disk measurements not fabricated |
| W8 browser/transfer | Real loopback host, cold/warm ETag, throttled mobile, compression negotiation; no real VPS/proxy/CDN or physical mobile claim |

The remaining external acceptance work is representative authenticated/provider discovery where valid credentials and quotas permit, and the actual chosen VPS/proxy/storage envelope. Public radio/generation internet latency, long-duration real traffic, native-memory roots, and every possible error/state distribution were not exhaustively measured. No finite audit can prove every millisecond/byte is optimal. All original unresolved/partial findings now have a measured, conditional, accepted-tradeoff or explicitly blocked disposition; structural concerns were not dismissed for being small today.

The audit's reproducible evidence is the gitignored local directory plus a SHA-256/length manifest. Raw traces, heaps, fixture data, account IDs and private settings are not committed. `verification-status.json` records passed evidence/consistency checks, including 20 successful burst jobs and 111 investigated scheduled guard stops. Task-owned PostgreSQL containers/volumes and private connection files are removed after verification; retained artifacts do not depend on those secrets. The prior 702-test remediation result is historical; it was not rerun or relabelled as a new runtime coverage result during this report-only work.

Workflow observations: verify script compatibility against installed tool help; distinguish profiler thread time from CPU; retain captured binary/source versions; isolate timing runs; check that interception has not disabled browser caching; match fixture quotas to runtime options; distinguish deliberate fail-closed recovery from successful throughput. These observations are kept here to preserve the unrelated pre-existing skill-observation log.



<a id="source-audit--scope-and-success-criteria"></a>

#### Scope and success criteria

The developer requested a line-by-line performance and network review, using appropriate skills and MCP servers, especially Rider, dotTrace and dotMemory. They first selected **plan first, with initial findings**, then approved execution and instructed scanning every line. This document records the completed static pass, preserves the earlier isolated measurements and retains the profiling plan. The original audit was report only; subsequent authorized product fixes are documented above. No project dependency updates or production configuration changes were made. Earlier workload helpers and private evidence were under `/tmp/liner-performance-audit-20261024/`; that directory is absent during this continuation, so those raw captures could not be reinspected. No new live provider requests or live application database mutations were made; remediation tests used disposable databases.

The inventory covers first-party application code, migrations, tests, scripts, workflows and build/runtime configuration. A search-pattern match is a lead, not a completed review. All handwritten `src/` files received a complete numbered source read, with reported issues traced through their implementations and callers. Repeated test/tooling lines use exact references to already-read lines while every novel line is inspected in file order. Generated schema files use unchanged-block equality plus review of every changed declaration; lockfiles use JSON parsing and exact baseline comparison plus changed-line review. These are distinct methods, not a claim that all 63,948 lines were independently reread verbatim. The ledger records the method and content hash per file. Static review does not establish runtime coverage or clear a file of all performance defects.

The goal is to identify attributable CPU time, latency, allocation, retained memory, disk I/O, SQL work and network traffic. No profiler can guarantee attribution of every millisecond or byte: sampling, instrumentation overhead, scheduler behavior, native memory, TLS, network transport and external services limit precision. Record uncertainty, profiler overhead and residual/unattributed work explicitly. Do not invent savings.

Preserve provider abstraction, deterministic scoring and persisted explanations, complete data export, guest access, privacy boundaries, mandatory provider pacing and fail-closed storage accounting. Existing source changes in `.github/workflows/qodana.yml`, `skill-observations/log.md`, and the untracked `Security_Scan.md` belong to other work and are excluded from this task's edits and commit.


<a id="source-audit--approach-considered"></a>

#### Approach considered

| Approach | Benefit | Limitation |
| --- | --- | --- |
| Static review alone | Exhaustive source coverage; detects repeated work and complexity without running the site | Cannot establish actual latency, retention, execution frequency or wire bytes |
| Profiling alone | Identifies costly executed paths under a defined workload | Misses unexercised paths and can mistake mandatory waits for waste |
| **Recommended: tracked source review plus separate CPU, memory, SQL and network measurements** | Connects code-level causes to reproducible costs and exposes coverage gaps | Requires a controlled environment, workload definitions and working capture tools |

Execution skills: `performance-reviewer` for evidence-based triage and `task-observer` for workflow observations. No brainstorming skill was used during audit execution. Product implementation remains outside scope.


<a id="source-audit--verified-tool-access-and-capture-gaps"></a>

#### Verified tool access and capture gaps

This table and B1 describe the original planning preflight. The execution checkpoint below supersedes capture/browser availability and records the new license and browser-launch blockers.

| Capability | Current evidence | Plan |
| --- | --- | --- |
| Rider MCP connection | `get_solution_projects(rootFolder=...)` succeeded and returned Domain, Application, DataAccess, Infrastructure, Presentation, Worker and test projects | Use the explicit repository root on each Rider call |
| Rider launch discovery | `get_run_configurations` succeeded; configurations include `Presentation: Liner Notes`, `Presentation: Manual QA`, `Presentation: Swagger` and `Worker` | Select the exact existing configuration; verify its target checkout, build mode, process and data before execution |
| Launch overrides | Returned configurations report `supportsDynamicLaunchOverrides=false` | Do not assume that MCP can override their environment or arguments |
| dotTrace MCP analysis | Tools listed below are exposed | Use them once a valid snapshot exists; analysis-tool presence does not establish recording access |
| dotTrace recording via MCP | No recording/start/attach profiling tool was found in the exposed Rider tool names/descriptions | Capture through supported Rider UI or a verified local CLI, then analyze through MCP |
| dotMemory via MCP | No dotMemory MCP tool was found in this session's tool registry | Use a verified local profiler or Rider memory UI; retained-object analysis access remains to be established |
| Local profiler files | Rider's `tools/profiler` contains `dotTrace.sh`, `dotMemory.sh`, `dottrace` and `dotmemory` | Presence is insufficient: see blocker B1 |
| Existing evidence | No `.dtp` or `.dmw` file was found in the repository inventory | No snapshot has been opened or analyzed; this does not assert that none exists elsewhere |
| Project memory | Codex-Mem `build_context` was queried for performance/profiling context; repository `MEMORY.md` and `PROGRESS.md` were read | Prefer current code over older checkout notes; the active frontend is `wwwroot/*.js`, with no React package manifest in this inventory |

Exposed dotTrace MCP tools, by suffix under `mcp__rider__`:

- `dotTraceGetSnapshotInfo`
- `dotTraceGetSnapshotFilters`
- `dotTraceGetCallTree`
- `dotTraceGetTimeline`
- `dotTraceGetTimelineEvents`
- `dotTraceOpenReport`

**B1 — Planning-pass local CLI capture was not verified usable (execution update below).** Running the installed `dotTrace.sh --help` failed because its referenced sibling `runtime-dotnet.sh` is missing. Invoking the newer `dottrace --help` failed with permission denied; `ls -l` shows both newer wrappers lack execute permission. Reading both legacy launchers confirms they reference the same missing runtime script. No install, permission change or tool repair was attempted. Establish whether Rider UI capture works, or prepare a compatible standalone CLI distribution, before scheduling runtime capture. This is a tooling blocker, not an application performance finding.

**B2 — Linux profiling has platform limits.** JetBrains documents that line-by-line profiling is unavailable for .NET Core applications on Linux/macOS. The project pins SDK `10.0.401`; validate profiler support for its exact runtime and OS before capture. Use supported sampling/timeline/tracing modes and source inspection. Even where line instrumentation is supported, JetBrains warns its high overhead distorts call times. [CLI platform limitations](https://www.jetbrains.com/help/profiler/Performance_Profiling__Profiling_Using_the_Command_Line.html), [profiling configuration guidance](https://www.jetbrains.com/help/rider/Profiling_Configurations.html).


<a id="source-audit--proposed-audit-sequence-and-effort"></a>

#### Proposed audit sequence and effort

Provisional effort is **6–10 focused working days**, not a runtime guarantee: environment/tool preflight 0.5–1 day; source review 3–5 days; workload captures and attribution 1.5–3 days; reconciliation/report verification 0.5–1 day. The inventory currently contains 486 tracked text files and 63,157 lines, including generated migrations, lockfiles and test fixtures. Capture-tool setup and realistic dataset preparation can extend the estimate. Re-estimate after the first complete layer review.

| Stage | Work and output | Completion check |
| --- | --- | --- |
| 0. Freeze scope and environment | Recheck HEAD and dirty paths; regenerate inventory; record host CPU/RAM/OS, SDK/runtime, profiler version, process command, configuration, database scale and deployment topology | The measured checkout and workload are identifiable; unrelated changes remain untouched |
| 1. Establish ordinary baselines | Use a Release build with matching symbols and no debugger; warm JIT separately; record idle and representative journeys without profiling | Timing distributions and resource counters exist before introducing profiler overhead |
| 2. Read every file | Review the ledger in dependency order: Domain, Application, DataAccess, Infrastructure, Presentation, Worker, then tests/scripts/build/deployment; follow hot paths across layers | Every inventoried line has an explicit reviewed disposition, with generated/declarative content distinguished |
| 3. CPU and waits | Capture supported dotTrace sampling/timeline profiles of the same workloads; trace inclusive time to expensive descendants; separate queue, locks, SQL, upstream pacing and remote service waits | Hotspots and waits have call paths, workload IDs and evidence references |
| 4. Allocation and retention | In separate runs, capture dotMemory baseline/burst/post-quiescence snapshots; analyze allocation stacks, dominators, roots, cache residency, LOH and object deltas | Retention claims have live-object/root evidence; allocation churn is not mislabeled a leak |
| 5. SQL, disk and network | Correlate query counts/plans, artifact read bytes, HTTP requests, payload sizes and browser waterfalls | Duplicate work and wire cost are attributed to specific operations and deployments |
| 6. Reconcile findings | Resolve overlapping causes, disprove false positives, rank costs under the observed workload, and list unmeasurable gaps | Every finding has evidence status and a reproducible validation procedure |
| 7. Deliver report | Review this document, its ledger and evidence references; verify no product changes; make a focused local report commit after verification | Developer reviews the report before any remediation or claim of developer-confirmed completion |

Suggested local documentation commits during a later audit: (1) reviewed source ledger and static findings, (2) CPU/SQL/network evidence and attribution, (3) memory evidence, reconciled priorities and final coverage. Do not commit raw profiles, HAR files, database contents or other sensitive capture artifacts. No fixes or pushes are included.


<a id="source-audit--measurement-protocol"></a>

#### Measurement protocol

1. Use disposable test accounts and isolated database/storage roots. Record bounded dataset sizes and hashes of nonsecret workload inputs. Do not run write-heavy scenarios against the developer's persistent live database by default. Its normal IDE profile selects real providers; do not accidentally use it as an offline load-test target.
2. Measure ordinary execution first, then separate CPU and memory runs. Start with at least five comparable timing runs after warm-up, report median/range, and add repetitions when variance matters. Report p95/p99 only with enough operation samples and the sample count. Record a monotonic timeline and workload boundaries.
3. Vary one dimension at a time: cold/warm cache, one/many distinct keys, small/large history, artifact bytes/file counts, request concurrency and queue depth. Proposed load points are 1, 5 and 20 concurrent local clients; adjust to host capacity. Do not send that load to external providers.
4. Separate offline/replayed mechanics from live provider performance. Reuse existing immutable replay clients for controlled source behavior where appropriate; explicitly label that evidence. Real provider captures require current terms/rate-limit review and a bounded call budget. Never bypass the request gate, refresh private data indiscriminately, or turn cache tests into upstream load tests.
5. Preserve raw private captures outside Git; save sanitized aggregates and precise evidence paths in the report. HTTP headers/bodies, SQL parameters, memory dumps, HAR and profiler output can contain cookies, tokens, email addresses, taste data or query text. Do not print launch environment values or private settings into this report.
6. For time attribution, separate wall-clock critical path from summed thread time. Overlapping spans and parent/child call-tree values must not be added together. Break down CPU, GC, queueing, lock contention, storage validation, DB round trips, network setup, provider pacing and remote response latency. Mandatory pacing is a constraint, not automatically wasted time.
7. For bytes, distinguish allocation rate, live managed heap, native memory, process RSS, disk reads/writes, serialized/decompressed body size, compressed HTTP transfer size and actual transport bytes. Cache size estimates are not measured RSS. HTTP payload sizes do not include TLS/TCP overhead; only attribute transport bytes when a suitable scoped observation is available.


<a id="source-audit--workload-matrix"></a>

##### Workload matrix

| ID | Scenario | Variations and observations |
| --- | --- | --- |
| W0 | Idle web host and worker | Jobs enabled/disabled; idle SQL rate, CPU wakeups, memory baseline, periodic maintenance |
| W1 | Guest entry, optional onboarding, local catalog search | First/repeat visit; empty/skipped/full taste; rapid typing, aborted searches, long result pages; SQL counts, DOM work, waterfall |
| W2 | Upstream catalog lookup | Same/distinct keys, existing public evidence, 429/503/slow response, cancellation; provider calls and pacing, cache misses and job polls |
| W3 | Discovery generation | Prepared/unprepared pool, repeated/overlapping seeds, bounded maximum candidates, feedback/history sizes; queue wait through persisted results |
| W4 | Concurrent discovery | Identical keys, overlapping metadata, distinct keys sharing a stripe, separate host processes; coalescing, contention and duplicate bytes |
| W5 | History, recent picks and JSON export | Empty/typical/large histories; query plans, materialized rows, peak memory, time to first/last byte; preserve complete export |
| W6 | Scheduled digest and local email capture | 1/10/100 due recipients in an isolated environment, pending recovery and failures; per-user SQL/upstream/disk work and serial bottlenecks |
| W7 | Storage admission, heartbeat and maintenance | Empty/100 MB/near-limit artifacts; many small versus few large files; query and full-file hash counts per mutation; never weaken integrity checks |
| W8 | Browser transfer and rendering | Cold/warm cache, compressed/uncompressed negotiation, realistic mobile throttling, background/foreground tab; bytes, revalidations, long tasks and abandoned polling |


<a id="source-audit--rider-and-dottrace-analysis-procedure"></a>

##### Rider and dotTrace analysis procedure

After capture, call `dotTraceGetSnapshotInfo` with the absolute snapshot path and repository `rootFolder` first. Preserve snapshot type, duration/time range and active filters in the evidence record. Then:

1. For **timeline** profiles, discover available filters with `dotTraceGetSnapshotFilters`; inspect timeline phases and use `dotTraceGetTimelineEvents` grouped summaries (`maxEvents=0`) for SQL, HTTP, file I/O and contention. Verify which event providers actually produced data; absent SQL/HTTP events do not prove absence of that work.
2. Drill into call trees with `dotTraceGetCallTree`, preserving returned node IDs as strings. Its default own-time threshold is 1%; set `minOwnTimePercent=0` when examining inclusive-time wrappers or many individually small calls. Explicitly record thresholds and truncation; repeatedly expand unexplained branches.
3. Filter by numeric thread IDs from snapshot filters and isolate the scenario time window. Do not pass friendly thread names as numeric IDs. Separate running/waiting and event units: time-related payloads use nanoseconds; memory payloads use bytes.
4. For **performance/sampling/tracing** profiles, use the call tree. Do not interpret ignored timeline filters or time-window arguments as successful scoping; those profiles do not provide the same timeline information.
5. Link each result to the source baseline, route/job, input scale, call count, own/inclusive time and relevant SQL/network evidence. Use `dotTraceOpenReport` only after reading its required report schema; the project-root Markdown remains the authoritative issue list.

Rider's documented capture workflow is selecting the run configuration and supported profiling configuration, starting a profile session, reproducing the scenario and saving the snapshot. The MCP run-configuration tool is an ordinary launcher, not proof of a profiled run. [Rider profiling workflow](https://www.jetbrains.com/help/rider/Profiling_Applications.html).


<a id="source-audit--dotmemory-procedure"></a>

##### dotMemory procedure

Establish a working CLI or UI and exact .NET 10 compatibility first. A documented CLI capability is attaching by PID or taking a `.dmw` snapshot using `get-snapshot`; verify syntax with the actual working installed tool before executing it. [JetBrains dotMemory CLI documentation](https://www.jetbrains.com/help/dotmemory/Working_with_dotMemory_Command-Line_Profiler.html).

For each of W3, W5 and W7, capture (A) warmed idle state, (B) immediately after a bounded workload, and (C) after completion, cancellation, disposal and a defined cache-expiry/quiescence period. Compare repeated equal-size bursts to distinguish expected bounded caches from sustained retention. Inspect response JSON, byte arrays, `MemoryStream`, hydrated pools, EF tracking, score snapshots and exports. Keep forced-GC experiments separate and label their perturbation. Managed snapshots alone do not explain native memory or RSS. If retained-object/dominator analysis cannot be accessed from this environment, leave that part explicitly blocked rather than substituting dotTrace allocation totals.


<a id="source-audit--sql-network-and-browser-procedure"></a>

##### SQL, network and browser procedure

- Count SQL commands per journey/job using available profiler events or an isolated DB diagnostic session. Analyze representative SELECTs with `EXPLAIN (ANALYZE, BUFFERS)` on the isolated database; `ANALYZE` executes the query. Check cardinality estimates, returned rows, index use, sequential scans, joins, sorts, locks, transaction duration and round-trip counts. Do not parallelize queries on one EF `DbContext` as a proposed shortcut.
- Trace outbound calls by provider/method and normalized nonsecret request identity. Record cache hits/misses, duplicates, pool reuse, DNS/connect/TLS timing where observable, response headers, status, retries, cancellation and compressed/decompressed bytes. Confirm actual HTTP handler defaults and deployment behavior before flagging missing connection pooling or compression.
- Capture browser journeys using browser/network tools and DevTools/HAR. The planning pass ran no browser measurement; the execution checkpoint records the controlled polling reproduction and MCP launch failure. Complete API waterfalls, search debounce/abort, main-thread work, repeated DOM rebuilding and asset transfers remain outstanding.
- Inspect deployed proxy/static asset headers separately from Kestrel. `Cache-Control: no-cache` permits conditional caching/revalidation; it does not mean every asset body is downloaded again. Private API `no-store` is intentional. Check Brotli/gzip negotiation and ETag/304 behavior before assigning an issue to the application.


<a id="source-audit--execution-checkpoint--2026-10-24"></a>

#### Execution checkpoint — 2026-10-24


<a id="source-audit--coverage-and-environment"></a>

##### Coverage and environment

The regenerated inventory matches the approved plan: 486 files, 63,157 lines, no line-count differences. SHA-256 was frozen for every inventoried file; all 486 hashes still matched after the workloads. Existing unrelated work is excluded from edits. Product source and runtime configuration are unchanged.

| Area | Complete numbered source read | Disposition |
| --- | --- | --- |
| Domain | 47 files / 2,668 lines | Pure types, vector operations, both scorers, selection, feedback grouping, schedules and bounded evidence read; scoring callers traced into Application. Whole-layer reconciliation remains pending. |
| Application | 98 files / 3,412 lines | All handlers, interfaces, mappings, DTOs, configuration and lockfile read. Persistence/provider/endpoint and test reconciliation remains pending. |
| Traced implementation | 8 files / 1,540 lines | `DigestGenerationStore`, `DiscoveryBatchStore`, `DiscoveryWriteCoordinator`, `CatalogReader`, both storage files, `DiscoveryJobService`, all 521 lines of `discovery.js`. |
| Remaining | 333 files / 55,537 lines | Pending complete source read, including remaining DataAccess, Infrastructure, Presentation, Worker, tests, scripts and root/deployment files. |

The ledger's `Read 1–N` means every numbered line was read, **not** that the file is cleared or all paths exercised. No file is promoted to fully reviewed yet. P01/P04/P08/P10 have isolated measurements; other findings remain structural or conditional.

Host: Linux `7.2.11-200.fc44.x86_64`, Intel Core i5-14500HX, 20 logical CPUs, approximately 31.1 GiB RAM. This shared development host had uncontrolled background activity. SDK `/home/mahammadjafarli/.dotnet/dotnet` `10.0.401`; measured runtime `.NET 10.0.12`. Helpers reference the actual Application/Infrastructure projects, use optimized Release builds and portable symbols, and run without a debugger. An initial parallel MSBuild failed without diagnostics; rebuilding with `-m:1 --disable-build-servers` succeeded with zero warnings/errors. No application issue is inferred from that tooling failure.


<a id="source-audit--tool-preflight-updates"></a>

##### Tool preflight updates

- **B1 partially resolved: dedicated dotTrace capture works.** Official stable `JetBrains.dotTrace.GlobalTools` `2026.2.3.1` was verified and installed into the task-private tool directory. Sampling captured the isolated scorer on this exact runtime. A sandbox host-connection permission error required authorized unsandboxed execution. Product processes were not attached or restarted. Rider's bundled wrapper remains unusable as described above. [Official CLI distribution and capture](https://www.jetbrains.com/help/profiler/Performance_Profiling__Profiling_Using_the_Command_Line.html).
- **B3: Rider dotTrace analysis is license-blocked.** `dotTraceGetSnapshotInfo` with the explicit repository root and captured path returned: `dotTrace is not licensed. Activate a dotTrace / dotUltimate license to use these tools.` Snapshot pieces exist, but no call tree, hotspot attribution, timeline or own/inclusive method times were obtained. No license activation was attempted.
- Standalone dotTrace/dotMemory UI distributions exist under JetBrains Toolbox, but UI presence is not capture/analysis evidence. No dotMemory MCP tool or verified usable memory-analysis path was established. No `.dmw`, dominator/root, post-quiescence live-object or leak result exists. A failed lookup of a guessed global-tool package is not proof that the official memory CLI is unavailable. [Official memory capture distribution](https://www.jetbrains.com/help/dotmemory/Working_with_dotMemory_Command-Line_Profiler.html).
- **B4: Playwright MCP Chrome is missing.** Its launch failed before running the test because `/opt/google/chrome/chrome` was absent. The reproduction used cached Playwright `1.64.1` and Chromium `155.0.8059.12` through the Node API. This is browser evidence, not a successful MCP browser run. A sandbox socket error required authorized unsandboxed Chromium execution. No global browser installation or project package change was made.


<a id="source-audit--w7-s-actual-storage-phases-with-isolated-artifacts"></a>

##### W7-S: Actual storage phases with isolated artifacts

`StorageAudit` calls the actual `LocalGenerationStorage` implementation in the coordinator's five-phase sequence: acquire coordinated lease, reserve, validate, verify stored bytes, publish committed inventory. It supplies a fixed accounting state and makes **no database connection or mutation**. Six category directories and deterministic zero-filled artifact files are private to this task. Reconciliation and one complete warm-up precede five ordinary samples per shape. Artifact construction/deletion are outside measurement.

**`/tmp` is tmpfs here.** These are warm, memory-backed filesystem results. `/proc/self/io` `rchar` deltas measure application read characters, not storage-device/network bytes. Physical `read_bytes` was zero in every sample. Small extra reads cover inventory and process-counter data. Wall time includes validation, hashing, async execution and inventory publication. Process CPU totals cover all helper threads; they must not be added to wall time.

| Artifact bytes | Files | Samples | Median wall ms (range) | Median process CPU ms | Median allocated bytes | Application read characters per operation |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 0 | 5 | 1.479 (1.205–2.686) | 1.663 | 89,112 | 869–3,365 |
| 100,000,000 | 1 | 5 | 453.742 (344.902–563.416) | 946.629 | 145,616 | 500,000,889–500,000,891 |
| 100,000,000 | 1,000 | 5 | 677.829 (412.721–905.112) | 1,454.673 | 50,875,488 | 500,006,011–500,007,036 |
| 900,000,000 | 1 | 5 | 3,847.635 (3,296.892–5,004.048) | 8,435.898 | 143,152 | 4,500,000,895–4,500,000,900 |

Process-wide allocation uses `GC.GetTotalAllocatedBytes(precise: true)` deltas and includes runtime work. The roughly 5× payload read relationship corroborates the source trace. Many-file runs also expose allocation cost. Physical-disk latency, SQL/transaction/advisory-lock wait, concurrent admission and heartbeat timeout were not measured. Near-limit publication itself took 644–1,060 ms; its five-second deadline was not exceeded. This measures P01's storage phases, not the total application write-path cost. Integrity checks remain unchanged.


<a id="source-audit--w3-s-pure-scorer-and-candidate-merge"></a>

##### W3-S: Pure scorer and candidate merge

`ScorerAudit` uses actual `GenerationConfiguration.Scoring()` and `BaselineAScorer.RankForBatch`: ten candidate tags, taste vectors of 20 or 271 tags, distinct tracks/artists, empty evidence and no exclusions. Each shape receives at least 50 warm-up calls and one second of warm-up, then five measured ranks in each of five independent processes (25 samples). Allocation uses `GC.GetAllocatedBytesForCurrentThread`; scoring is synchronous. Selecting five finite scores is asserted. Selected-breakdown serialization occurs **after** timing/allocation measurement.

| Taste tags | Candidates | Samples | Median wall ms (range) | Allocated bytes per rank | JSON bytes for 5 selected breakdowns |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 20 | 10 | 25 | 0.499 (0.409–1.587) | 137,584–146,224 | 21,561 |
| 20 | 100 | 25 | 0.608 (0.535–1.155) | 1,338,104 | 21,577 |
| 20 | 400 | 25 | 3.429 (2.371–11.845) | 5,349,704 | 21,593 |
| 271 | 10 | 25 | 0.884 (0.334–1.327) | 375,824 | 21,076 |
| 271 | 100 | 25 | 3.861 (3.276–10.732) | 3,750,104 | 21,092 |
| 271 | 400 | 25 | 38.146 (14.275–47.488) | 14,997,704 | 21,108 |

Wide ranges and small operations limit latency comparisons; warm-up does not establish that tiering or host noise disappeared. Allocation supports P08 but does not attribute every byte to invariant setup. Selected JSON is serialized payload with empty evidence, not persisted column size, full recommendation payload, retained memory or wire traffic. No policy tuning or recommendation-quality evaluation occurred.

Five ordinary control processes each executed 500 warmed C=400/T=271 ranks: median 9,267.943 ms, range 8,417.096–10,985.032 ms. One matching dotTrace sampling run measured 9,617.793 ms inside the workload; capture lifecycle was 13.599 seconds. The profiled result lies inside ordinary variance, so a reliable overhead percentage cannot be inferred. Earlier short-warm-up/mixed-build experiments were excluded. B3 prevented opening the snapshot or attributing CPU to individual methods.

`MergeAudit` invokes actual private `PrepareDiscoveryBatch.Merge` through reflection, using distinct generated text identities and valid offline provenance. Construction/warm-up are outside measurement; reflection overhead is included. Each shape receives at least 20 calls and one second of warm-up, then five samples in each of five independent processes. Output count is asserted unchanged.

| Distinct candidates | Samples | Median wall ms (range) | Allocated bytes per merge |
| ---: | ---: | ---: | ---: |
| 10 | 25 | 0.024 (0.015–0.052) | 55,000 |
| 100 | 25 | 1.788 (0.663–2.669) | 3,139,904–3,140,152 |
| 400 | 25 | 19.101 (10.102–38.489) | 47,133,544–47,133,792 |

These generated shapes measure isolated costs; they are not demo recommendations or real-provider evidence. Callers establish use in live-capable discovery, but these runs exclude provider pacing, SQL, real evidence graphs, persistence and HTTP serialization. Do not add medians to estimate an end-to-end latency.


<a id="source-audit--w2-p-controlled-frontend-retry-reproduction"></a>

##### W2-P: Controlled frontend retry reproduction

The exact unchanged `discovery.js` ran in Chromium with a minimal DOM, ordinary `fetch`, and routed session/overview/job responses. Every request under a reserved `.invalid` audit origin was intercepted; unrelated URLs were aborted and service workers blocked. `clock.install`, `pauseAt` and `runFor` controlled Date/timers. This measures timer policy and attempted HTTP requests, not elapsed transport time or live backend throughput. [Playwright clock semantics](https://playwright.dev/docs/clock).

With a running job and `429` plus `Retry-After: 60`, attempted poll timestamps in milliseconds were **0, 5,000, 10,000, 15,000, 20,000, 25,000, 30,000, 35,000, 40,000, 45,000, 50,000, 55,000, 60,000**. A controlled completed response arrived at 60 seconds. This establishes 11 premature retries before the first requested delay elapsed; subsequent rejections also each requested another 60 seconds. No more polls occurred during 65 virtual seconds after completion. In a fresh active-job scenario, leaving immediately after the first rejection prevented further attempts for 65 virtual seconds. No page errors occurred. Missing/malformed/date-form retry headers and complete UI journeys remain untested.


<a id="source-audit--evidence-reproduction-and-outstanding-work"></a>

##### Evidence, reproduction and outstanding work

Private root: `/tmp/liner-performance-audit-20261024/`, **volatile tmpfs rather than durable archive**. `evidence-manifest.json` records SHA-256/byte lengths for helper sources, build/capture logs, selected samples/aggregates and snapshot pieces. `inventory.json` and `coverage.json` retain frozen source hashes and read ranges. The committed ledger preserves source-read state if private files disappear. No raw snapshot, HAR, DB contents or private settings are committed.

| Evidence | Private paths under that root |
| --- | --- |
| Scorer | `ScorerAudit/{ScorerAudit.csproj,Program.cs}`, `scorer-steady-baseline-{1..5}.json`, `scorer-steady-control-{1..5}.json`, `scorer-aggregate.json`, `scorer-steady-sampling.log`, `scorer-steady-sampling.dtp*` |
| Storage | `StorageAudit/{StorageAudit.csproj,Program.cs}`, `storage-baseline.json`, `storage-aggregate.json`; generated artifacts deleted after measurement |
| Merge | `MergeAudit/{MergeAudit.csproj,Program.cs}`, `merge-baseline-{1..5}.json`, `merge-aggregate.json` |
| Browser | `polling-audit.cjs`, `polling-evidence.json`, `polling-error.log`; cached executable paths are recorded inside the helper |
| Tool setup | `dottrace-help.txt`, `dottrace-start-help.txt`, `dottrace-install.log`, `dottrace-versions.json`, `*-build.log` |

Rebuild helpers with the full pinned SDK path and `build <private-project-path> -c Release -o <private-output-path> -m:1 --disable-build-servers`. Run `dotnet scorer-bin/ScorerAudit.dll` five times for shape samples, and five times with `--profile` for 500-rank controls. Run the storage and merge DLLs similarly without overlapping measured CPU workloads. Browser command: `node /tmp/liner-performance-audit-20261024/polling-audit.cjs`. Input construction rules, assertions and bounds are above and in the private helper sources.

Sampling command after ordinary controls:

```sh
/tmp/liner-performance-audit-20261024/tools/dottrace start --framework=NetCore --profiling-type=Sampling --save-to=/tmp/liner-performance-audit-20261024/scorer-steady-sampling.dtp --propagate-exit-code --no-check-for-updates -- /home/mahammadjafarli/.dotnet/dotnet /tmp/liner-performance-audit-20261024/scorer-bin/ScorerAudit.dll --profile
```

This was the first execution checkpoint. The continuation below supersedes its source-coverage totals. Application-level W0–W8 workloads, SQL plans/counts, network/transfer attribution, concurrency/cold-cache sweeps and retained-memory analysis remain outstanding beyond these named subworkloads. No live-provider performance, production readiness, exhaustive clean bill or developer-confirmed completion is established.

Workflow observations: verify capture and licensed analysis separately; distinguish source reading from call-path/runtime reconciliation; freeze helper builds before aggregation; record tmpfs versus physical storage; use intercepted HTTP and a virtual clock to reproduce retry policy. These stay here so the pre-existing unrelated skill-observation log is preserved.


<a id="source-audit--static-continuation--2026-10-24"></a>

#### Static continuation — 2026-10-24

Reviewed the remaining handwritten DataAccess, Infrastructure, Presentation and Worker sources, migration operations/SQL, tests and supporting scripts/configuration. Cross-checked the reported paths against dependency injection, controller/filter placement, provider bundles and regression-test contracts. The original checkpoint's 153 source-read entries were carried forward after checking their frozen hashes. All inventoried source hashes remained unchanged during this continuation; only this report was edited by the task.

| Review method | Files | Accounted lines |
| --- | ---: | ---: |
| Complete numbered source reads, including prior checkpoint | 362 | 28,911 |
| Ordered novel-line inspection with exact references to previously read test/tooling lines | 109 | 12,895 |
| Generated schema: exact unchanged blocks plus every changed declaration/index/relationship | 15 | 18,501 |
| Lockfile JSON parsing, exact baseline comparison and changed-line inspection | 9 | 3,641 |
| **Total** | **495** | **63,948** |

This includes nine additions to the earlier inventory: `.config/dotnet-tools.json`, `.dockerignore`, `.env.example`, both `.githooks` files and four `.cjs` browser/load checks (791 lines). Documentation, IDE/agent state, private runtime settings, ignored recordings, binaries and dependency internals remain outside this source count. No new executable source was found outside the expanded inventory in the tracked C#/JS/CJS/Python/shell/build/workflow paths. Source hashes and method dispositions are preserved below; temporary comparison maps and scan helpers are under `/tmp/liner-audit-continuation/` and are not permanent evidence storage.

Safeguards reconciled during the review:

- Global EF no-tracking and split queries are configured. Local catalog search batches result mapping; P11 concerns individual selection/lookup reads.
- Shared pool/public-evidence leases and tests cover same-key reuse. P02 concerns overlapping lower-level metadata requests across distinct pool scopes, not a claim that identical pools always duplicate work.
- Active discovery bounds feedback history; P09 is qualified to weekly generation's input/fingerprint path. The finite synthetic scheduler is not evidence of production scheduling capacity.
- Guest activity has an hour threshold before write admission. Private API `no-store`, complete exports, password hashing, account revocation, archive ownership verification and storage-integrity checks remain required constraints.
- Direct recording-tag tests support P16's path; origin checks and supporting-response policy still require preservation. Test source inspection is not a fresh passing test run.

Rider MCP was consulted again with the explicit project root: project discovery succeeded; project problems returned zero cached diagnostics. That is not a fresh performance inspection or profiler result. Earlier dotTrace analysis was license-blocked; no dotMemory analysis interface or retained-object evidence was established. The earlier raw evidence directory is absent, so its committed measurements remain historical results with the limitations above. This continuation adds static evidence only: no new timings, allocation totals, SQL plans or network-byte measurements.

Remaining runtime work is the W0–W8 plan: establish ordinary application baselines, capture usable licensed CPU/wait and memory evidence, collect SQL/lock/trigger plans and counts, then browser/provider wire-byte evidence and concurrency/cold-cache sweeps. Run approved live-provider scenarios only with their configured use gates and a bounded workload. Developer confirmation and deployment verification remain outstanding.

Workflow observation: inventory by tracked paths as well as extension, because hidden hooks and `.cjs` checks were omitted initially. Treat truncated/compressed source output as unread until recovered in bounded chunks. Compare generated blocks exactly and label that method explicitly; file/line totals are not proof of runtime attribution. These notes stay here to preserve the unrelated existing observation log.


<a id="source-audit--findings-register"></a>

#### Findings register

All priorities below are **provisional investigation priorities**, not measured production severities. No hypothetical optimization saving is claimed. `Structural` means work visible in source; `conditional` means cost requires the stated trigger. Measurements apply only to their named isolated workloads and units above. This register describes the historical baseline; the 2026-10-25 investigation above supersedes its runtime-status statements.

The finding narratives below describe the original audit baseline. The remediation table above is the current disposition; historical "no fix applied" statements must be read in that baseline context.

| ID | Priority | Evidence status | Concern | Primary validation |
| --- | --- | --- | --- | --- |
| P01 | High | Measured storage phases; DB contention pending | Multiple full-artifact hashing passes per coordinated write | W7-S: five full reads; near-limit median 3.85 s on tmpfs |
| P02 | Medium | Conditional | Duplicate upstream metadata requests after concurrent cache misses | W4: count same-key outbound calls across distinct pools |
| P03 | Medium | Structural; cost unmeasured | Full-response buffering, extra array copy and JSON clone | W2/W3: allocation stacks and body-size sweep |
| P04 | Medium | Reproduced in controlled browser | Generation polling ignores the longer server retry delay | W2-P: 5-second attempts despite Retry-After 60 |
| P05 | Medium | Structural contention; cost unmeasured | Unrelated cold pools serialize on 32 stripe gates | W4: same-stripe versus different-stripe keys |
| P06 | Medium | Structural; cost unmeasured | Full discovery export is materialized in memory | W5: large valid history, peak live memory and allocation |
| P07 | Low | Structural; cost unmeasured | Idle discovery lanes poll the database every second | W0: count SQL at idle per host |
| P08 | Medium | Measured pure scorer allocation; CPU attribution pending | Invariant setup and snapshots repeated before selecting five picks | W3-S: 15.0 MB allocated at C=400/T=271 |
| P09 | Medium | Structural; scale-dependent cost unmeasured | Weekly generation reads/hashes full feedback history twice | W3/W6: history sweep and SQL/lock evidence |
| P10 | Medium | Measured isolated merge; end-to-end cost pending | Quadratic alias grouping rebuilds identities repeatedly | W3-S: 47.1 MB allocated at 400 distinct candidates |
| P11 | Medium | Structural query fan-out; runtime count pending | Sequential item fetching for catalog selection/completed lookup | W1/W2: 2N eligible-item SELECTs plus surrounding queries |
| P12 | Medium | Structural; cost unmeasured | Cache-wide quota aggregates on each public-evidence publication | W3/W7: cardinality sweep, plans and admission time |
| P13 | Medium, local capture | Structural; cost unmeasured | Account export/deletion scans and decrypts the shared email archive | W5/W6: fixed owned history, growing unrelated copies |
| P14 | Medium | Structural; cost unmeasured | Cold pools serialized and immediately decoded into another object graph | W3: cold/persistent/memory allocation comparison |
| P15 | High | Structural; concurrency impact unmeasured | Login password verification holds global write admission | W4: authentication plus unrelated writes |
| P16 | Medium | Structural; avoidability conditional | Artist metadata acquired and parsed before direct recording tags are selected | W2/W3: request count and allocations with valid direct tags |
| P17 | Low | Structural; cost unmeasured | Two sequential account-existence SQL checks on authenticated requests | W1/W2: SQL round trips under job polling |


<a id="source-audit--p01--repeated-complete-artifact-scans-inside-serialized-write-admission"></a>

##### P01 — Repeated complete artifact scans inside serialized write admission

Current disposition (2026-10-26): authenticated writer integration is locally verified. G1d0 buffer reuse is locally verified with unchanged fingerprints and full content checks. Three paired controls reduce 10,000-file allocations 535.26 → 94.03 MB and median latency 1,205.24 → 888.08 ms; full payload read traffic remains unchanged. Immutable manifests, incremental validation and the approved audit deadline remain pending; P01 is not closed. The baseline below remains historical evidence.

Locations: [LocalGenerationStorage.cs:139](src/Infrastructure/Storage/LocalGenerationStorage.cs#L139), [CoordinatedGenerationStorage.cs:94](src/Infrastructure/Storage/CoordinatedGenerationStorage.cs#L94), [DiscoveryWriteCoordinator.cs:51](src/DataAccess/Persistence/Repositories/DiscoveryWriteCoordinator.cs#L51), [DiscoveryJobService.cs:97](src/Presentation/Background/DiscoveryJobService.cs#L97).

`ValidateArtifactsAsync` calls `MeasureAsync`, which recursively enumerates configured artifact roots and opens/hashes each file with SHA-256 (lines 148–193). A successful ordinary `DiscoveryWriteCoordinator.ExecuteAsync` invokes artifact validation during acquisition, reservation, pre-write validation, post-write verification and committed-inventory publication: **five complete passes**, excluding caller-level work. The batch/storage lease surrounds this work; some passes also occur inside the DB transaction. Heartbeat renewal uses the same coordinator for a small mutation.

If managed artifacts contain A bytes across F files, this path performs approximately O(5A) application-level content reads and O(5F) file work per write. OS page cache can reduce physical reads but not all hashing/enumeration cost. Near the configured storage cap, the amount of revalidation can dwarf the mutation. Mandatory integrity validation is intentional: the issue to investigate is repeated total work and lock duration, not whether checks should be removed.

Execution update: W7-S above reproduced the five passes and measured ordinary storage-phase time, process CPU, allocation and application read counts at 0/100 MB/near-limit artifacts, including a many-file case. Physical-disk behavior, method-level profile attribution, transaction time, lease contention and heartbeat latency remain outstanding. Preserve fail-closed tamper and cross-process guarantees. No fix applied.


<a id="source-audit--p02--cache-miss-is-checked-before-pacing-without-a-second-lookup"></a>

##### P02 — Cache miss is checked before pacing, without a second lookup

Locations: [ListenBrainzApiClient.cs:25](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzApiClient.cs#L25), [MetaBrainzRequestGate.cs:43](src/Infrastructure/RecommendationSources/ListenBrainz/MetaBrainzRequestGate.cs#L43), [SharedDiscoveryPreparation.cs:49](src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs#L49).

The metadata response cache is checked once before awaiting the cross-process request gate. Different pool acquisitions can miss the same metadata key, wait for pacing, then each send HTTP even if an earlier request has populated the cache. Pool-level stripe gating and persistent public-evidence leases reduce some duplication but do not themselves coalesce every shared metadata key across different pools/processes. The gate enforces spacing, not cache-key deduplication.

For C overlapping acquisitions of an otherwise cacheable metadata key, up to C requests are possible instead of one, conditional on timing, process cache boundaries and the acquisition paths actually running concurrently. Each duplicate consumes provider quota and body bytes; the gate's configured 1.05-second spacing can increase queue time. Reproduce with distinct pools sharing metadata and a bounded deterministic upstream stand-in; separately test same-process and cross-process behavior. Count requests and cache state before proposing coalescing or a second lookup. Do not relax mandatory pacing. No fix applied.


<a id="source-audit--p03--extra-response-sized-allocations-during-metadata-parsing"></a>

##### P03 — Extra response-sized allocations during metadata parsing

Locations: [ListenBrainzApiClient.cs:49](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzApiClient.cs#L49), [ListenBrainzContracts.cs:103](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzContracts.cs#L103).

The bounded response is accumulated in a growing `MemoryStream`; `output.ToArray()` creates a separate full-body array; `JsonDocument.Parse` then parses it and `RootElement.Clone()` preserves an independently owned JSON value. The temporary body, copied array and parsed/retained representation overlap during processing. The response cap is 1,000,000 bytes, which limits individual exposure but does not eliminate churn across many responses.

One explicit avoidable-looking copy allocates N additional bytes for an N-byte body, in addition to stream capacity and JSON storage. Exact peak memory and JSON internal allocation behavior need profiling; this is not a leak claim. Capture allocation stacks for 10 KB/100 KB/near-limit valid bodies, compare LOH/GC activity and peak memory, and include cancellation/error responses. Any future approach must retain response byte caps, provenance hashing and independent JSON lifetime. No fix applied.


<a id="source-audit--p04--generation-poll-retry-loop-disregards-retry-after"></a>

##### P04 — Generation poll retry loop disregards Retry-After

Locations: [discovery.js:45](src/Presentation/wwwroot/discovery.js#L45), [discovery.js:125](src/Presentation/wwwroot/discovery.js#L125).

The shared request helper reads and clamps `Retry-After` and, on 429, stores a deadline in `page.nextRequestAtUtc`. The generation polling error branch nevertheless schedules its next request after a fixed 5,000 ms. The stored deadline feeds the generation button's cooldown, not this polling schedule. A server asking the client to wait 60 seconds can therefore receive repeated premature polls, roughly twelve polling attempts per minute ignoring response time, with rejected traffic and repeated error handling.

Execution update: W2-P above reproduced five-second attempted requests during controlled `429`/`Retry-After: 60`, then verified completion and active-view cancellation. Absent/malformed/date-form headers and complete journeys remain outstanding. Existing two-second success polling is a separate policy choice whose cost should be measured, not automatically called a defect. No fix applied.


<a id="source-audit--p05--unrelated-cold-pool-keys-share-a-long-held-stripe-gate"></a>

##### P05 — Unrelated cold pool keys share a long-held stripe gate

Location: [SharedDiscoveryPreparation.cs:15](src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs#L15), especially lines 49–60 and the surrounding acquisition/hydration body.

Prepared-pool acquisition uses 32 semaphores selected from the first hash byte modulo stripe count. It holds the selected semaphore across upstream discovery, hydration and publication preparation, with a 45-second linked deadline started **after** acquiring the gate. Different keys mapping to one stripe serialize despite not being the same pool. At enough distinct cold keys, collisions are unavoidable; even two chosen keys can reproduce the condition.

The synchronization is bounded and may be an acceptable tradeoff. Profile paired same-stripe/different-stripe cold requests and a 33-distinct-key offline burst. Attribute semaphore wait independently from mandatory provider pacing, public-evidence leases and the job runner's intentionally limited acquisition concurrency. Verify concurrent reachability before elevating this lead; a single active acquisition lane may mask it. No fix applied.


<a id="source-audit--p06--export-holds-all-discovery-history-and-mapped-results-simultaneously"></a>

##### P06 — Export holds all discovery history and mapped results simultaneously

Location: [DiscoveryProfileReader.cs:32](src/DataAccess/Persistence/Repositories/DiscoveryProfileReader.cs#L32); reconciled with `GetUserDataExportQueryHandler`, `ExportController`, `app.js` and `discovery.js` during the continuation.

The reader separately materializes all profile batches, jobs, recommendations with tracks, taste signals and recommended identity aliases using `ToArrayAsync`, then constructs DTO arrays. JSON fields are parsed into retained elements. There is no history pagination/bound within this complete-export reader. Work and live memory grow with the profile's stored history; some entity arrays overlap the mapped output arrays.

Full export is a privacy requirement. This is a scale-dependent memory/latency concern, not a recommendation to omit data or cap the export. Measure realistic and maximum plausible account histories, time to first/last byte, allocated bytes, peak live memory and concurrent exports. Trace the complete response pipeline before estimating serialization overhead or recommending a snapshot-consistent streaming design. No fix applied.


<a id="source-audit--p07--empty-job-queues-still-cause-recurring-sql-round-trips"></a>

##### P07 — Empty job queues still cause recurring SQL round trips

Locations: [DiscoveryJobService.cs:12](src/Presentation/Background/DiscoveryJobService.cs#L12), [DiscoveryJobStore.cs:9](src/DataAccess/Persistence/Repositories/DiscoveryJobStore.cs#L9).

When enabled, the host runs a prepared lane and an acquisition lane. At idle, the prepared lane checks generation readiness once and the acquisition lane checks lookup and generation readiness, then each sleeps one second. `HasReadyAsync` is a database `AnyAsync`, so an idle instance performs approximately three readiness SQL commands per loop interval plus execution time, independent of useful jobs. This scales with enabled web-host instances.

These indexed existence checks may be cheap and the one-second responsiveness may be intentional. Measure idle commands/sec, query plans, DB network traffic and CPU before treating the polling policy as worth changing. Keep it separate from expensive coordinated write admission: no-ready checks do not themselves invoke that coordinator. No fix applied.


<a id="source-audit--p08--repeated-batch-invariant-scorer-work-before-selecting-five-picks"></a>

##### P08 — Repeated batch-invariant scorer work before selecting five picks

Status: **structural duplication plus measured whole-scorer allocation; method CPU attribution pending**. Locations: [BaselineAScorer.cs:59](src/Domain/Scoring/BaselineAScorer.cs#L59), [BaselineAScorer.cs:85](src/Domain/Scoring/BaselineAScorer.cs#L85), [BaselineAScorer.cs:156](src/Domain/Scoring/BaselineAScorer.cs#L156), [PrepareDiscoveryBatch.cs:76](src/Application/Features/Discovery/PrepareDiscoveryBatch.cs#L76), [GenerateDigestCommandHandler.cs:102](src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs#L102).

Every candidate's `ScoreCore` validates configuration, recreates a sorted taste dictionary through an intermediate dictionary, recalculates taste magnitude, and reads `configuration.Sha256`, which validates, orders/formats policy and hashes it again. It constructs a complete score snapshot before ranking keeps at most five picks. Large tastes still incur full sorting even when persisted taste is trimmed to overlaps. At C candidates and T taste tags, the repeated invariant taste preparation alone has O(C × T log T) work; configuration hashing also repeats C times. Candidate vectors, overlap calculation and output explanations still require candidate-specific work.

Reachability: `RunDiscoveryJob.ExecuteAsync` calls shared preparation; `PrepareDiscoveryBatch` calls `ScoreForBatch` or weekly `Score`, both using `ScoreCore`. The older weekly branch calls `Rank`, which also scores before selection. `GenerationConfiguration` caps candidates at 400 and picks at five. W3-S measured actual `RankForBatch` (same core) at 400 candidates/271 taste tags: 14,997,704 allocated bytes per rank, median 38.146 ms with 14.275–47.488 ms range. Five selected breakdowns serialized to 21,108 bytes with empty evidence. This is allocation churn, not retained memory or a proven leak. Full real-evidence generation may have a different cost distribution.

Future remediation to evaluate: prepare immutable batch-invariant state once, and investigate delaying full snapshot construction until selected scores are known. Preserve exact deterministic scores/order, configuration hashes, selected explanation evidence, unknown JSON fields and snapshot limits; sanity-check existing scorer/benchmark fixtures. The measurement does not establish what fraction would be saved. No fix applied.


<a id="source-audit--p09--weekly-generation-materializes-and-fingerprints-all-historical-feedback"></a>

##### P09 — Weekly generation materializes and fingerprints all historical feedback

Status: **structural scale-dependent work, runtime SQL/lock cost unmeasured**. Locations: [DigestGenerationStore.cs:38](src/DataAccess/Persistence/Repositories/DigestGenerationStore.cs#L38), [DigestGenerationStore.cs:123](src/DataAccess/Persistence/Repositories/DigestGenerationStore.cs#L123), [GenerateDigestCommandHandler.cs:38](src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs#L38).

`ReadInputsAsync` fetches all profile taste signals and all feedback-bearing recommendations with tracks, then serializes revision input to a complete UTF-8 array and hashes it. No feedback window exists in this method. Generation calls it before choosing the shared path; `PersistCoreAsync` calls it again while holding `SHARE ROW EXCLUSIVE` table locks on Users, TasteSignals, WeeklyRecommendations, WeeklyDigests and Tracks. Therefore the shared runtime's bounded preparation does not eliminate these two full-history loads/fingerprints. Cost grows with stored feedback/signals; materialization/JSON conversion and the lock-holding portion need measurement. A non-existing digest that reaches persistence is the trigger; existing-digest fast returns do not take this path.

Important false-positive boundary: active discovery's `DiscoveryBatchStore.ReadInputsAsync` uses `Take(feedbackWindow)`, default 20, validated maximum 40. `EffectiveTasteInputs.Build` has nested alias-group scans, but it must **not** be reported as unbounded history in that active discovery path. The older weekly branch can feed it the unbounded history from `DigestGenerationStore`; with shared jobs enabled, this branch is not used. Weekly full-history reads/fingerprints still occur before and during persistence in the shared branch.

Validate W3/W6 with isolated increasing histories, SQL command/row/payload counts, allocation/peak live memory and lock duration; include idempotent existing-digest controls. Consider a transactional revision/projection approach only after proving consistency with feedback and account mutations. Never truncate exports or silently change feedback policy to optimize this. No fix applied.


<a id="source-audit--p10--candidate-deduplication-repeatedly-reconstructs-aliases-in-a-nested-scan"></a>

##### P10 — Candidate deduplication repeatedly reconstructs aliases in a nested scan

Status: **structural quadratic growth plus measured isolated allocation/time**. Locations: [PrepareDiscoveryBatch.cs:126](src/Application/Features/Discovery/PrepareDiscoveryBatch.cs#L126), [GenerateDigestCommandHandler.cs:170](src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs#L170), [BaselineAScorer.cs:181](src/Domain/Scoring/BaselineAScorer.cs#L181).

The shared preparation merge visits every existing group and its members for each new candidate. Each comparison rebuilds the existing track's aliases and a LINQ intersection. With N distinct one-member groups this performs N(N−1)/2 comparisons: 79,800 at the configured maximum of 400. The weekly legacy merge similarly reconstructs tracks and aliases inside its comparison loop. Transitive grouping is required, but these particular allocations are repeated work.

W3-S exercised the actual shared private merge, not a reimplementation: 100 distinct candidates allocated about 3.14 MB; 400 allocated about 47.13 MB. At 400, observed median was 19.101 ms, range 10.102–38.489 ms, across 25 samples. Reflection overhead is included. The test uses distinct generated text aliases with valid offline provenance; real duplicate/MBID distributions can differ. It does not measure the older merge or whole generation, and no CPU call tree or retention result was obtained.

Evaluate precomputed aliases and an identity-to-group index/union approach in future remediation, preserving transitive MBID/text identity merging, provenance aggregation, deterministic preferred-record selection, exclusion behavior and output ordering. Test bridge-alias cases as well as all-distinct/duplicate-heavy shapes before any optimization claim. No fix applied.


<a id="source-audit--p11--catalog-item-loops-fan-out-into-sequential-database-reads"></a>

##### P11 — Catalog item loops fan out into sequential database reads

Status: **structural query fan-out, actual SQL count/latency unmeasured**. Locations: [SelectDiscoveryTaste.cs:40](src/Application/Features/Discovery/SelectDiscoveryTaste.cs#L40), [CatalogLookupJobs.cs:44](src/Application/Features/Catalog/CatalogLookupJobs.cs#L44), [CatalogReader.cs:74](src/DataAccess/Persistence/Repositories/CatalogReader.cs#L74).

Saving onboarding/taste resolves selected catalog IDs one at a time. Reading a completed provider-lookup result similarly calls `catalog.GetAsync` for each of at most ten IDs. The concrete reader issues an eligibility SELECT and then a records/publication SELECT for each eligible ID. Structurally, N eligible items require 2N item SELECTs in sequence, plus the surrounding profile/job work; a ten-result completion therefore has 20 item SELECTs before surrounding work. Expired/missing items can take only the first query. Execution-strategy retries and connection/transaction overhead are not included in this source count.

`SearchAsync` already batches its selected result IDs; typing local search is a separate path and should not inherit this finding. No upstream calls occur in `CatalogReader`. The impact depends on round-trip latency, selected-item count and how often a completed lookup is read. Trace actual SQL in W1/W2 before assigning latency. A future batch reader could reuse existing mapping while preserving per-item eligibility, order, expiry and retained selection fallback; do not parallelize against the shared DbContext. No fix applied.


<a id="source-audit--p12--public-evidence-publication-scans-cache-wide-quota-totals"></a>

##### P12 — Public evidence publication scans cache-wide quota totals

Status: **structural database work; latency and query plans unmeasured**. Priority: medium. Location: [PublicEvidenceStore.cs:48](src/DataAccess/Persistence/Repositories/PublicEvidenceStore.cs#L48); caller: `SharedPublicEvidence.GetAsync` publication through coordinated write admission.

Every successful publication computes `SumAsync(StoredBytes)` and `CountAsync` over all `PublicEvidenceCache` entries before quota decisions. The current indexes cover key and expiry, without a maintained aggregate for these totals. At E entries, each publication requests cache-wide aggregate work, even when its own payload is small; the configured maximum permits 50,000 entries. Eviction additionally selects/sorts up to 100 victims. This happens inside the global admitted mutation, so aggregate time also extends its lock/transaction lifetime. It is separate from P01's filesystem scans.

Measure increasing cache cardinality and payload size with SQL plans/buffers, publication latency and admission contention. A future maintained total would need the same transactional and cross-process quota guarantees. Bounded cache size is acknowledged; no measured bottleneck or savings is claimed. No fix applied.


<a id="source-audit--p13--account-archive-export-reads-and-decrypts-other-accounts-email-copies"></a>

##### P13 — Account archive export reads and decrypts other accounts' email copies

Status: **structural cross-account work; runtime cost unmeasured**. Priority: medium when local capture is enabled. Locations: [LocalEmailArchive.cs:29](src/Infrastructure/Email/LocalEmailArchive.cs#L29), [GetUserDataExportQueryHandler.cs:90](src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQueryHandler.cs#L90).

An enabled account export enumerates the shared copy directory, reads each encrypted file and parses/decrypts its MIME and receipt **before** checking `Receipt.UserId`. The global directory limit is 10,000 entries; one account's export therefore scales with everybody's retained copies, not only its own. For each owned copy, another linear search over that account's digest list runs. Deletion similarly scans/authenticates the shared archive while holding the artifact lease. Authentication before deleting an unknown file is required; this finding concerns the shared layout/access path, not removing ownership checks.

The ordinary live guest host rejects concurrent local email capture, so this is an optional legacy/local-capture path, not an assertion about every live page. Measure fixed own history while growing other accounts' copies, recording bytes read, decrypt/parse allocations and lease time. A future authenticated ownership index or per-owner layout must preserve legacy-copy discovery, complete export and safe deletion. No fix applied.


<a id="source-audit--p14--freshly-acquired-prepared-pools-are-serialized-and-decoded-immediately"></a>

##### P14 — Freshly acquired prepared pools are serialized and decoded immediately

Status: **structural repeated representation work; cost unmeasured**. Priority: medium. Locations: [PersistentDiscoveryPreparation.cs:13](src/Infrastructure/Discovery/PersistentDiscoveryPreparation.cs#L13), [PublicEvidenceCodec.cs:22](src/Infrastructure/Discovery/PublicEvidenceCodec.cs#L22), [SharedDiscoveryPreparation.cs:88](src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs#L88).

On a cold pool acquisition the inner preparation already constructs hydrated candidates and serializes them for size/hash accounting, then caches that prepared object when eligible. The persistent adapter encodes that object into another JSON representation for public evidence and unconditionally decodes the returned document into new tracks, vectors and candidate arrays, replacing the memory entry with the decoded pool. The fresh path therefore constructs two object graphs and multiple serialized representations of the same bounded pool. Persistent-hit decoding is necessary to restore stored evidence; immediate decoding of a just-produced object is the additional work to measure.

`SharedSeedPreparation` also caches a serialized document and decodes it on each memory hit; this is the same representation tradeoff at seed scale. Profile separate cold, persistent-hit and memory-hit cases up to 400 candidates, including checksum and provenance validation. Any future shortcut must validate equivalent evidence and must not bypass persisted-document integrity boundaries. No fix applied.


<a id="source-audit--p15--login-verification-holds-global-write-admission"></a>

##### P15 — Login verification holds global write admission

Status: **structural critical-section expansion; concurrent effect unmeasured**. Priority: high. Locations: [DiscoveryWriteFilter.cs:12](src/Presentation/Filters/DiscoveryWriteFilter.cs#L12), [AuthController.cs:21](src/Presentation/Controllers/AuthController.cs#L21), [AuthService.cs:140](src/Presentation/Services/AuthService.cs#L140).

With `DiscoveryRuntime.RunJobs=true`, the filter admits every non-GET action on `AuthController` before executing it. The login action consequently performs account lookup, password-hash verification, failure-count updates and successful-session storage inside the globally coordinated transaction/lease. Even a rejected login follows the admitted path. Required password hashing and lockout accounting can therefore hold admission needed by unrelated taste, discovery and heartbeat writes. P01's artifact revalidation adds to this cost, but the broad placement of CPU-heavy authentication inside admission is a separate concern.

Validate successful, wrong-password and unknown-account requests alongside unrelated discovery writes, measuring admission duration and failed/busy responses as well as latency. Future design must retain serialized accounting and atomic lockout/session updates; do not weaken password hashing or security checks to improve timing. No fix applied.


<a id="source-audit--p16--artist-metadata-is-fetched-before-direct-recording-tags-are-selected"></a>

##### P16 — Artist metadata is fetched before direct recording tags are selected

Status: **structural acquisition and parsing; avoidability conditional on evidence requirements and cache misses**. Priority: medium. Location: [ListenBrainzRecommendationProvider.cs:115](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecommendationProvider.cs#L115); regression boundary: [RecordingDiscoveryTests.cs:12](tests/Infrastructure.Tests/RecommendationSources/RecordingDiscoveryTests.cs#L12).

Hydration gathers every candidate's uncached artist IDs, requests metadata in chunks of 100, and builds artist tag vectors per candidate. Only afterward does it inspect direct recording tags and select those when valid. In that case the selected vector and tag observations come from the recording, yet artist acquisition/parsing has already happened. The existing recording test explicitly supports candidates with direct tags and empty artist tags. At A uncached distinct artist IDs, hydration requests ceil(A/100) artist-metadata responses even when every candidate has usable direct tags; HTTP/public cache hits can avoid wire traffic, and calls are not one per candidate.

Artist responses also remain in `SupportingResponses`, their origins are checked, and artist gaps are carried forward. Whether every such response is mandatory is a policy question; a change must preserve required provenance and gap semantics. Measure a controlled direct-tag-rich batch against an artist-fallback batch, recording cold/warm request counts, bytes and parsing allocations. Missing direct tags still require the approved fallback behavior. No live request, runtime saving or fix is claimed.


<a id="source-audit--p17--authentication-adds-two-sequential-existence-queries-per-request"></a>

##### P17 — Authentication adds two sequential existence queries per request

Status: **structural round trips; cost unmeasured**. Priority: low. Location: [DependencyInjection.cs:129](src/Presentation/DependencyInjection.cs#L129).

For an otherwise valid bearer token, `OnTokenValidated` awaits an Identity user existence query and then a Domain user existence query before allowing the endpoint to execute. Each authenticated job poll or API read therefore carries these two checks in addition to its endpoint queries. Missing/invalid accounts short-circuit. Both checks use IDs and may be cheap; actual latency depends on database placement and polling volume.

Immediate account-revocation semantics are intentional and tested. Investigate a single database-side eligibility query only after measuring command count and duration; do not introduce stale authorization caching or remove either eligibility condition. Keep this separate from P07's background queue polling and P11's item retrieval. No fix applied.


<a id="source-audit--pending-checks-that-are-not-confirmed-issues"></a>

#### Pending checks that are not confirmed issues

- The host sets API `no-store` and static assets `no-cache` in [Program.cs:133](src/Presentation/Program.cs#L133). No application response-compression middleware was found in the initial search. Actual compression and cache behavior may be handled by a reverse proxy. Measure negotiated encoding, transfer size, ETag/304 and deployment headers first; do not report absent compression or complete repeated downloads as established facts.
- `ListenBrainzRecommendationProvider.HydrateCandidatesBatchAsync` repeatedly searches `responses.Values.FirstOrDefault` for recording metadata inside its candidate loop (lines 123–138). Candidate count is capped at 400 and artist metadata is already batched in chunks of 100. This is a CPU lead, not yet evidence that indexing it would matter.
- `DiscoveryReader.ReadAsync` makes several sequential queries and reloads the profile's onboarding field after querying its revision (lines 15–32). Count round trips and inspect consistency needs before proposing consolidation. Do not parallelize against a shared DbContext.
- `RecentRecommendations.ReadAsync` checks catalog eligibility by canonical recording identity, while the catalog's unique index leads with publication ID. Inspect the actual join/EXISTS plan at representative catalog sizes before calling this a missing-index bottleneck; the planner can choose set-based work.
- Managed-storage row triggers serialize old/new rows with `to_jsonb` and update a shared ledger row. This is required accounting and a possible write-amplification/lock cost during large imports; measure trigger time and WAL on an isolated database before proposing a transactionally equivalent aggregate strategy.
- The account data screen previews a complete JSON export, then download can request another complete export. This may repeat SQL, serialization and bytes for a preview-then-download journey (P06). Measure the journey and establish freshness/privacy requirements before recommending reuse.
- All inventory entries now have a static disposition. None establishes a clean bill of health for paths that were not exercised, deployed proxy behavior, retained objects or live-provider performance.


<a id="source-audit--required-finding-format-for-the-full-audit"></a>

#### Required finding format for the full audit

For each issue append: stable ID; provisional/measured priority; status (`structural`, `conditional`, `measured`, `disproved`, `accepted tradeoff`); exact baseline file/line/symbol; reachable caller and route/job; trigger and input scale; evidence paths and tool settings; call/query/request counts; cost in correctly labeled units; reproduction steps; confidence and uncertainty; possible remediation and constraints; verification that would accept/reject it; developer disposition. Disproved leads stay documented with their disproof, rather than silently disappearing. Merge duplicate root causes and cross-reference their affected workloads.

Completion requires all ledger entries reviewed or explicitly outside scope with reasons, every proposed workload executed or explicitly blocked, every measured finding tied to reproducible evidence, and remaining uncertainty listed. Separate static review coverage, runtime scenario coverage, provider evidence, deployment evidence and developer confirmation. Source coverage alone does not establish runtime coverage or production readiness.


<a id="source-audit--verification-of-this-report"></a>

#### Verification of this report

Only this Markdown document is intended for the task commit. Validate that every referenced file and starting line exists, the full inventory matches the frozen checkout plus the recorded dirty baseline, finding IDs are unique, every entry has a static disposition, and aggregate totals match the ledger. Review the Git diff for accidental product/configuration changes. No new build or application test run is required for this documentation-only continuation; no application behavior was changed or new runtime performance verified. Commit locally after these document checks; do not push.

Continuation verification passed: `git diff --check`; 495 ledger entries totaling 63,948 lines; all 495 frozen SHA-256 hashes and line counts unchanged; 42 local source links/starting lines resolve; P01–P17 appear exactly once as finding sections; tracked code-path inventory reconciliation found no omitted C#/JS/CJS/MJS/Python/shell/SQL/project/solution/build/workflow/hook files. The unrelated workflow, observation-log and security-report changes were preserved and excluded from staging. Application tests were inspected, not rerun. Developer confirmation remains pending.


<a id="source-audit--task-observer-note"></a>

#### Task-observer note

A useful reusable profiling preflight is: enumerate actual MCP tools, confirm the IDE's active project, probe installed capture-tool help, verify runtime/platform support, and only then describe an executable capture plan. Tool registration, installed filenames, allocation totals and retained-object analysis are distinct evidence. This observation is recorded here without modifying the pre-existing observation log or installed skills.


<a id="source-audit--coverage-ledger"></a>

#### Coverage ledger

The inventory covers tracked text source, tests, helper scripts, workflow files and root build/runtime configuration, including the nine continuation additions. Documentation, private/ignored state, IDE/agent state, binaries and external dependency internals are excluded. Counts include blank/comment lines. `R` records a complete numbered source read; `E` records ordered inspection of novel test/tooling lines with exact previously read line references; `G` records generated-schema unchanged-block comparison and changed-declaration review; `L` records JSON lockfile comparison and changed-line review. Each disposition covers lines 1–N; it is a static review status, not proof of execution, zero defects or developer-confirmed completion. SHA-256 identifies the reviewed content, including the pre-existing dirty Qodana workflow.

<!-- COVERAGE_LEDGER -->

| Area | Files | Lines |
| --- | ---: | ---: |
| .config | 1 | 11 |
| .githooks | 2 | 103 |
| .github | 2 | 125 |
| Root build/config | 14 | 591 |
| scripts | 34 | 4,248 |
| src/Application | 98 | 3,412 |
| src/DataAccess | 79 | 24,387 |
| src/Domain | 47 | 2,668 |
| src/Infrastructure | 48 | 4,225 |
| src/Presentation | 37 | 6,221 |
| src/Worker | 8 | 743 |
| tests/Application.Tests | 29 | 3,349 |
| tests/DataAccess.Tests | 25 | 4,755 |
| tests/Domain.Tests | 23 | 1,938 |
| tests/Infrastructure.Tests | 28 | 3,615 |
| tests/Presentation.Tests | 20 | 3,557 |
| **Total** | **495** | **63,948** |

| File | Lines | Static disposition | SHA-256 |
| --- | ---: | --- | --- |
| `.config/dotnet-tools.json` | 11 | E: 1–11; no separate finding; runtime limitations apply | `8fff2d5078c590b5233a87dc47cea2f10f7fea99894bc6e5c02c03d2bdc7b8d4` |
| `.dockerignore` | 30 | E: 1–30; no separate finding; runtime limitations apply | `491bca91b2b29666ce52be19063be7c4fc36be2ea4507d11b87ac1447bfc5184` |
| `.env.example` | 6 | E: 1–6; no separate finding; runtime limitations apply | `4528e400d0f2722ef11cf9eb57c5443219082dc8343f938b4698c0e4dbb2a591` |
| `.githooks/pre-commit` | 37 | E: 1–37; no separate finding; runtime limitations apply | `b30d72cf80465539b57f8f71cf18148b02392f3c29a9bb76dbadd711fef37531` |
| `.githooks/pre-push` | 66 | E: 1–66; no separate finding; runtime limitations apply | `555f09fd589fee92ee60a436bd2ea20cbc97ba123c671455ec3dd8a87499ad59` |
| `.github/workflows/ci.yml` | 97 | E: 1–97; no separate finding; runtime limitations apply | `099ad828e5c9d6c652a13edd96dea31ed5efb8094a3122fd39797c46efc5274a` |
| `.github/workflows/qodana.yml` | 28 | E: 1–28; no separate finding; runtime limitations apply | `c3dc05d9245cc281c19652e49ae9c765e427f20973c776cfb7da089544f03d03` |
| `Directory.Build.props` | 9 | E: 1–9; no separate finding; runtime limitations apply | `73118fbef773f142f1c4e6c30e639c3cd6f8a934b02cac98c2982b3c4bc882c1` |
| `Directory.Packages.props` | 64 | E: 1–64; no separate finding; runtime limitations apply | `6ceff15c7e790a1d7b8dbbbb03d39a8562c4c84abe4384ee211a5a898a013bac` |
| `Dockerfile` | 50 | E: 1–50; no separate finding; runtime limitations apply | `3bb642b4fb882ca4117e787832eedea5cd87fcc8c2a0e64883b58731cd39c9db` |
| `Liner Notes.csproj` | 39 | E: 1–39; no separate finding; runtime limitations apply | `0821381aec6c91ebea4064b5cc048be8d7a9876664730637dc7972421eee3954` |
| `Liner Notes.sln` | 197 | E: 1–197; no separate finding; runtime limitations apply | `7a27825c78650b9c296b0a42ea1aba5bf1d65fed7d11a12034a5c3eccc3057d2` |
| `Liner Notes.slnLaunch` | 11 | E: 1–11; no separate finding; runtime limitations apply | `bde7a77aa1dc8d4f6bd44dff21359069abe0ca1c26cd9cd6aa2383fc1ec4c2a5` |
| `Program.cs` | 3 | E: 1–3; no separate finding; runtime limitations apply | `8b19830b25f98c46c4879557c4c3f9aed8bd772dff4d821a84d074af69bbca80` |
| `compose.ide.yaml` | 6 | E: 1–6; no separate finding; runtime limitations apply | `38850b904ee107bd648c8fd30a87b21669b210d522a1952279ac7533a1bf9ab4` |
| `compose.yaml` | 132 | E: 1–132; no separate finding; runtime limitations apply | `20829b8b528f94b2d437185d34303883b8c867262729380a8b4666c84327e3d9` |
| `dotnet-tools.json` | 13 | E: 1–13; no separate finding; runtime limitations apply | `12af4a55d97fd3fe6d9db3377ddb36b28c33281f0218033610da89f323f52ef8` |
| `global.json` | 7 | E: 1–7; no separate finding; runtime limitations apply | `4e7d5022b2a5a8d02bb951bb264a4c8613eabc332572a0af5f9743d23f23d84e` |
| `qodana.yaml` | 24 | E: 1–24; no separate finding; runtime limitations apply | `116ee1e33afd314880a6617e832d6024ced8a7ea10a4a8da4bb33622d8fdf066` |
| `scripts/acquisition_guard.py` | 97 | E: 1–97; no separate finding; runtime limitations apply | `178c91d20669c20bcc5d627bc5f9792302107aa29dc4bd1a923e5387c6552b29` |
| `scripts/catalog/catalog_probe.py` | 471 | E: 1–471; no separate finding; runtime limitations apply | `910bb8cb731ea4819801ed5cfc2206fc63d065d9764d2b1c3a4c3cdfa6bdb24f` |
| `scripts/catalog/test_catalog_probe.py` | 175 | E: 1–175; no separate finding; runtime limitations apply | `459e0f3cbcd3912425bf5ceae73045bc509ce8fd483173e29907b71858564461` |
| `scripts/checks/guest_discovery_browser.cjs` | 213 | E: 1–213; no separate finding; runtime limitations apply | `f8892f0ff69e30e55b325c847f9dea70f689154f8b9d0df031c5e080c69e33b0` |
| `scripts/checks/guest_discovery_load.cjs` | 111 | E: 1–111; no separate finding; runtime limitations apply | `310c34cde6364bbe03ef8e753b21f520c6d2dd6eeed4dd5487abdaa127816881` |
| `scripts/checks/optional_taste_browser.cjs` | 126 | E: 1–126; no separate finding; runtime limitations apply | `9d68bd4fc5f1591cf926f7bf14bf09bec845af4fb9159fe7a529f4e601ddf6f2` |
| `scripts/checks/phase1_browser.cjs` | 191 | E: 1–191; no separate finding; runtime limitations apply | `fc808218d52980e608df5e7a2cdbf341c01ec78a25490117045e53ffba49b8fe` |
| `scripts/checks/phase9_compose.py` | 215 | E: 1–215; no separate finding; runtime limitations apply | `01600de888934a66a01427c7adf1d88de9a0e8a9cdeaf22563038db9b90a505e` |
| `scripts/checks/phase9_test_results.py` | 55 | E: 1–55; no separate finding; runtime limitations apply | `f5fcd56a8dc012eb05c0bc062052dfe6b5fd99e9b0d49ff2bf02a870d4535966` |
| `scripts/checks/renewal_docs.py` | 13 | E: 1–13; no separate finding; runtime limitations apply | `543c2c5f2601a1fc56c295714c2494ef0ed068b50c6de3a97225d7a0c0937987` |
| `scripts/checks/secret_defaults.py` | 44 | E: 1–44; no separate finding; runtime limitations apply | `0595243f4a3be0597f7248f74ff3e933f0103f23ac321e47c1b4067e2be51b17` |
| `scripts/checks/test_phase9_helpers.py` | 77 | E: 1–77; no separate finding; runtime limitations apply | `d6cff3efc8f2b767391c43f7f759fa0c8e2476264d30888e2bf1463d0e74736a` |
| `scripts/git-qa-reviewer.py` | 26 | E: 1–26; no separate finding; runtime limitations apply | `26a205c5c640b04312ef19440db30ef69f3f69a03921f30404bf5e763d6ad284` |
| `scripts/git-security-reviewer.py` | 111 | E: 1–111; no separate finding; runtime limitations apply | `8da97f99f909475ff2fb1654e2ae75185923c4b98511daf942d1ed69df717728` |
| `scripts/guest_discovery_demo.cs` | 83 | E: 1–83; no separate finding; runtime limitations apply | `917366f80f8e5b650fba733c03b419b41aa03e71bc0c20e796bf1c9a2c8e9fcb` |
| `scripts/listenbrainz_smoke.cs` | 39 | E: 1–39; no separate finding; runtime limitations apply | `549e9bdaf4816a6584f71eded68b2c736482b62d9ebd0e9f9c39cd7194470435` |
| `scripts/phase4_pilot.py` | 431 | E: 1–431; no separate finding; runtime limitations apply | `1ba220dd69a27c6fd601904a486ec5695c563063ec4578f237cd2eb55f30a857` |
| `scripts/phase4_replay.cs` | 38 | E: 1–38; no separate finding; runtime limitations apply | `41e0ce957c051c1b8a94597e187a2e4d19ccd0e2b75805b1fed7eb1f4b983496` |
| `scripts/phase4_weights.json` | 9 | E: 1–9; no separate finding; runtime limitations apply | `85c3f261815d689a2f212913e39510c9cabc93326d81e383e27cbf272e864a47` |
| `scripts/phase7_cleanup_local_email.cs` | 38 | E: 1–38; no separate finding; runtime limitations apply | `fe42cd8a2e147511ec3fb7f976a7fa9ee486dc371777ddef6d1642b82fd0601a` |
| `scripts/phase7_local_email.cs` | 74 | E: 1–74; no separate finding; runtime limitations apply | `d853daaadd6a80e163e26b1cb9d0786804a63aa6b55158071fe8e7556346bec5` |
| `scripts/phase8_repair_registration_slot.cs` | 57 | E: 1–57; no separate finding; runtime limitations apply | `8be76f468822f4383fe995c4055d46d9ee63e6da63208e779e50433d75bd35d1` |
| `scripts/phase8_schedule_fixture.cs` | 50 | E: 1–50; no separate finding; runtime limitations apply | `1510abdb46b3b18ad72549b986e11264433e2e9457437b95d2347de4906a8db1` |
| `scripts/phase9_init_local.py` | 34 | E: 1–34; no separate finding; runtime limitations apply | `13ce898fb4ca76dea138e8ad9bd9d1297d3f3c7beec93a1b68f374dc72ee2e52` |
| `scripts/record_lastfm.py` | 497 | E: 1–497; no separate finding; runtime limitations apply | `37892936a8dc3bb9f9d481c2f3e8af2647a5462ce0908c9b897fc83872a0d5c0` |
| `scripts/run-schemathesis.sh` | 7 | E: 1–7; no separate finding; runtime limitations apply | `7ffcc0c442e149d37f33bee58dc26e951a21a2491ad19616e98941b2752387df` |
| `scripts/run-zap-scan.sh` | 5 | E: 1–5; no separate finding; runtime limitations apply | `7d01b49aef275a70b238aef8ff02f936875aafc7392c0c5983e503e4245475d3` |
| `scripts/run_live.sh` | 16 | E: 1–16; no separate finding; runtime limitations apply | `e34376f0fdfde106c44adac2191a6ddb9b1e84ff66e6562fa6a6e0d5c4099ead` |
| `scripts/run_manual_qa.sh` | 47 | E: 1–47; no separate finding; runtime limitations apply | `7c7e3d5523d4f606c966988ca14ad02e67a320fa073324384a0de18607672efe` |
| `scripts/run_phase7_local_qa.sh` | 98 | E: 1–98; no separate finding; runtime limitations apply | `c30ec271f61cbad595906ffdc193c9533d6b4c1dfb17d6b071bf489d298c1582` |
| `scripts/setup_local_live.cs` | 126 | E: 1–126; no separate finding; runtime limitations apply | `53df51e1df5660b6a82425d462b564559d26ee180516dcce1fc86fc363aa6baa` |
| `scripts/test_phase2_recorder.py` | 376 | E: 1–376; no separate finding; runtime limitations apply | `eed50a8f07cd326af8cda08f0e22c12903f1d0b1f1873a0f3a711ef7ae8f2f31` |
| `scripts/test_phase4_pilot.py` | 243 | E: 1–243; no separate finding; runtime limitations apply | `edb616db67f02432affabab3930ffaf082b7be0569e24757a6c35a9bc0971198` |
| `scripts/validate_manual_qa_connection.cs` | 55 | E: 1–55; no separate finding; runtime limitations apply | `affc0c757e80af93faf49c8d7ec2370a699b05d6de63561ab55747c5430e511a` |
| `src/Application/Application.csproj` | 19 | R: 1–19; no separate finding; runtime limitations apply | `cb67ed348b70e895da62fa3590305022dd39d73b92b754f3723e03dc3fa04511` |
| `src/Application/Common/Behaviors/ValidationBehavior.cs` | 47 | R: 1–47; no separate finding; runtime limitations apply | `9f164865e55d78b0c0722a46b1fc376401312ebc375d3aa54ed036a4cfa331ce` |
| `src/Application/Common/Exceptions/NotFoundException.cs` | 16 | R: 1–16; no separate finding; runtime limitations apply | `04318a0c8f8cefef81b4dfb1bad7f20725b3814f799fa1a5976580fc1c7e5e7f` |
| `src/Application/Common/Exceptions/ValidationException.cs` | 36 | R: 1–36; no separate finding; runtime limitations apply | `e3b0d319d9c4b4e608fa40f0dc4909f79faaea4882ce4cc132110071eb10330a` |
| `src/Application/Common/Interfaces/Catalog/ICatalogMaintenance.cs` | 7 | R: 1–7; no separate finding; runtime limitations apply | `38da8ffffb54a729a5fc629405c931e3ed76e989649d096139fcf3b073770ee9` |
| `src/Application/Common/Interfaces/Catalog/ICatalogStore.cs` | 63 | R: 1–63; no separate finding; runtime limitations apply | `f85de283c6dc9b6928306c1b4a47fec31cc5d1b792e5a7af15596eb4e37b8197` |
| `src/Application/Common/Interfaces/Discovery/ICoordinatedGenerationStorage.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `7238ce59223992dde7918a0dad868bdb82795e081a0d346a552322431b350702` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryBatchStore.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `fc01ab238f1d82797541830a857ae69594bc0824594820ca2c20806abeccc8d0` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryJobStore.cs` | 21 | R: 1–21; no separate finding; runtime limitations apply | `d014e8e89fad25c8be37cc795e7f0c85c084f38ada352ca8d96f18582bde3b53` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryProfileReader.cs` | 8 | R: 1–8; no separate finding; runtime limitations apply | `5ab7a8530e9105061686cff47163f231c276e231f34296053ef2cd3f9316c770` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryReader.cs` | 7 | R: 1–7; no separate finding; runtime limitations apply | `0d928f29f1800508644c5d37c1647e6bd9cf260d0f37acb9eb0860f86f380a68` |
| `src/Application/Common/Interfaces/Discovery/IDiscoveryTasteStore.cs` | 8 | R: 1–8; no separate finding; runtime limitations apply | `8f2e809d211f135ebf62287a515e496c14cb3bd5815b1c5e277aa188da4b4541` |
| `src/Application/Common/Interfaces/Discovery/IGuestProfileStore.cs` | 25 | R: 1–25; no separate finding; runtime limitations apply | `33f11464b0062741155840fc7bf2430ae353290bd727fef028ef3342c906ea62` |
| `src/Application/Common/Interfaces/Discovery/IManagedStorageIntegrity.cs` | 3 | R: 1–3; no separate finding; runtime limitations apply | `79016258a4a58cc1dec1ebbe450ada35e2dae85e931ab1e28c62c01467323e76` |
| `src/Application/Common/Interfaces/Discovery/IPublicEvidenceStore.cs` | 18 | R: 1–18; no separate finding; runtime limitations apply | `49545e08335bd33924be571b70d9e7703bb21e8587a4dabffd0c84ce698cdbd4` |
| `src/Application/Common/Interfaces/Discovery/IRecentRecommendations.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `30262e35d2903f6c74fd38bec334e896456331f41ff53218171e11064a8a4ec9` |
| `src/Application/Common/Interfaces/IAccountProfileReader.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `fa7f0a8bd202898634c7c28c331bb025b794b0cdd80509f9d34487c184177cfa` |
| `src/Application/Common/Interfaces/ILocalDigestCaptureStore.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `46d3a149d85145e19d1faf8d36d093c603ee5291fc3b4ba2eacf660f94910306` |
| `src/Application/Common/Interfaces/ILocalEmail.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `2bb61653051743d0fb22f0cb5d6c3bc1a7b84b2e858cda1ecc925dc679dc185f` |
| `src/Application/Common/Interfaces/ILocalEmailCapture.cs` | 37 | R: 1–37; no separate finding; runtime limitations apply | `0f9a1a491cbd1c62be6f9af6799b8eea912ce290640689fd2a7e289fb0cbe075` |
| `src/Application/Common/Interfaces/IUnitOfWork.cs` | 10 | R: 1–10; no separate finding; runtime limitations apply | `05dde484e56807a50deb867dfca63c304380c5e254300ebf63f4fecf13132608` |
| `src/Application/Common/Interfaces/IWeeklyScheduleStore.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `b75a4fb864535b151c6159bad5775d9b8eedc36286be64dba164b22995b26911` |
| `src/Application/Common/Interfaces/Recommendation/ICandidateHydrator.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `785bd7047fd6f10ef99c67e7344d0ecde2d14a4017138f0325baed131ed3c2ca` |
| `src/Application/Common/Interfaces/Recommendation/IDigestGenerationStore.cs` | 56 | R: 1–56; no separate finding; runtime limitations apply | `aa257bc19fc7dd13a44293479fcf000e384fbaadd248c834b2d988a4f00b399f` |
| `src/Application/Common/Interfaces/Recommendation/IDiscoveryPreparation.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `4ac239b9a4759e3931d11035b6a5a511371979bac1e8393cb66cca1e588fb313` |
| `src/Application/Common/Interfaces/Recommendation/IRecommendationProviderResolver.cs` | 10 | R: 1–10; no separate finding; runtime limitations apply | `d01413de66e466a9eaba8eeba7e9b3d77269b8a27e123173bedf7e3717245d8a` |
| `src/Application/Common/Interfaces/Recommendation/IRecommendationSource.cs` | 39 | R: 1–39; no separate finding; runtime limitations apply | `6f55ad9452746707dc728cbe4249cb4d3e9364824a202a7393956b8e7342bdd3` |
| `src/Application/Common/Interfaces/Recommendation/IRecordingRecommendationSource.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `78f5f65960ed40cde884ac84ea7868d5112ce94e9e59c19bd40131b292474be9` |
| `src/Application/Common/Interfaces/Repositories/IAsyncRepository.cs` | 39 | R: 1–39; no separate finding; runtime limitations apply | `a3e2ece916910895c4c70c0bea06b769ff9dd7120ae0654c465cfc36deaa54c4` |
| `src/Application/Common/Interfaces/Repositories/IListenerProfileRepository.cs` | 7 | R: 1–7; no separate finding; runtime limitations apply | `18a183032be7b41e12cdab00a713a6e6eeaaf4e66e021eb99606418cf85f9f74` |
| `src/Application/Common/Interfaces/Repositories/ITasteSignalRepository.cs` | 12 | R: 1–12; no separate finding; runtime limitations apply | `adfa6bda64b09be2b2b8e35c6987c213d3067669efe240f3a61faa3d0b7ef6a2` |
| `src/Application/Common/Interfaces/Repositories/ITrackRepository.cs` | 11 | R: 1–11; no separate finding; runtime limitations apply | `780ea3e89f81704c45ab5599793c7687ceff0ddb36557c2d66b963e92b158adf` |
| `src/Application/Common/Interfaces/Repositories/IUserRepository.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `cfdd3749fb54f0bba3d9f160a301c0fb856a9878c73c5438257f4b72dfa44df5` |
| `src/Application/Common/Interfaces/Repositories/IWeeklyDigestRepository.cs` | 17 | R: 1–17; no separate finding; runtime limitations apply | `6e1c7a7f7dd0fea141df25b251135bafb8c753e9447c1924aada27c1ed10ed4a` |
| `src/Application/Common/Mappings/MappingExtensions.cs` | 95 | R: 1–95; no separate finding; runtime limitations apply | `7dea546ab7cbbac4a5ccbe753605bb1086babf19b272f9c1b6ac8bd2922825b2` |
| `src/Application/Common/Models/PagedResult.cs` | 16 | R: 1–16; no separate finding; runtime limitations apply | `4f34e06673d15f9dea5ed16b195a5288d90d688bf595bae90adf47dc60893dfd` |
| `src/Application/Common/Models/Recommendation/GenerationConfiguration.cs` | 60 | R: 1–60; no separate finding; runtime limitations apply | `c6520277286fdce60d98f3555295838f85132f6799e797ebf91ae2b2751d3456` |
| `src/Application/Common/Models/Recommendation/GenerationEvidenceMapping.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `2d40af02922f9f27db678721889b459c2ba8090ccfae569615541d64725267dc` |
| `src/Application/Common/Models/Recommendation/IngestionEvidence.cs` | 76 | R: 1–76; no separate finding; runtime limitations apply | `b1ec882cf8b53be48dca2821ba065cac8172cd06d65b0ceceec7e05618c9510f` |
| `src/Application/Common/Models/Recommendation/RawCandidateTrack.cs` | 41 | R: 1–41; no separate finding; runtime limitations apply | `03f26905bdf5c51a82317961454ae0e59791a46106083287c07e571e8a16624f` |
| `src/Application/DTOs/Digests/ScoreBreakdownDto.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `3dcef0d28caac561e54faca692283e4777fc1d89ab094dda7dcabd36fcfd95f3` |
| `src/Application/DTOs/Digests/TrackDto.cs` | 16 | R: 1–16; no separate finding; runtime limitations apply | `759f297427e9fac7c8b305824594ae8b52565fb933e870d3411c2790bd71e3c4` |
| `src/Application/DTOs/Digests/WeeklyDigestDto.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `d309ae79856245babfc788f6de7fc8eee7c9a762fbd0ea8196c1d44fa5faf8e6` |
| `src/Application/DTOs/Digests/WeeklyRecommendationDto.cs` | 28 | R: 1–28; no separate finding; runtime limitations apply | `7f9f07e1d87bc791aa94b5148523f1a308099a8b7b745531ac844ca66da30f81` |
| `src/Application/DTOs/Export/AccountExportDto.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `b749331f31aeba4b97e392ab5e8adbd091bce6de4b353db83de75f9f396c9760` |
| `src/Application/DTOs/Export/DiscoveryExportDto.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `45585dd65f77bdf88c686ddf7398f49e34ca343fbd4f9325a5da9a7541a22648` |
| `src/Application/DTOs/Export/UserDataExportDto.cs` | 38 | R: 1–38; no separate finding; runtime limitations apply | `7362cc27a811d511317cdc6ed302a5c2ad4a78e766cb48a009d46990736c8935` |
| `src/Application/DTOs/Subscribers/SubscriberDto.cs` | 23 | R: 1–23; no separate finding; runtime limitations apply | `cefe48889fee3b49cb864c47cd5583d49f43196472ba1735a4a7b4e3c8e010b9` |
| `src/Application/DTOs/Taste/SeedItemDto.cs` | 17 | R: 1–17; no separate finding; runtime limitations apply | `f0e9821cf3024b31eaecb8e762032050f943a3133e66480d3f92c0c34e4c6361` |
| `src/Application/DTOs/Taste/TasteSignalDto.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `f460413ad44e1d4d7469c748022dcbbea8dfb50f40467b192e24cfc2212752cb` |
| `src/Application/DependencyInjection.cs` | 39 | R: 1–39; no separate finding; runtime limitations apply | `502c0b5aa8be891afab767709b0cfdfd44e77552580d2e31beb391acb072a3b7` |
| `src/Application/Features/Catalog/CatalogLookupJobs.cs` | 53 | R: 1–53; P11 | `2badada147c3b7d269dbf2066e2a3eff58597573ba20c5f24dcdf5864f6852d4` |
| `src/Application/Features/Catalog/ImportCatalog.cs` | 75 | R: 1–75; no separate finding; runtime limitations apply | `87cc38dfb5d7cb45ed312f986cf23fec05e99146ecdd1b1586f50f53db0e8517` |
| `src/Application/Features/Catalog/LookupCatalog.cs` | 31 | R: 1–31; no separate finding; runtime limitations apply | `22270c7683968763fe4887920cebd14f506e0e8200b7b9500d01728c89d9bfae` |
| `src/Application/Features/Catalog/RunCatalogLookup.cs` | 70 | R: 1–70; no separate finding; runtime limitations apply | `edd72df6f18fff405eccb112d55b52a7dca1c298757a55f4b5680f448e12c617` |
| `src/Application/Features/Catalog/SearchCatalog.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `95cc17716c07a25dc8a4e5c719dea6e69fb79e9401b69e394a0a3eba14cc6d81` |
| `src/Application/Features/Digests/Commands/CaptureLocalDigest/CaptureLocalDigestCommand.cs` | 58 | R: 1–58; no separate finding; runtime limitations apply | `6c4141a3a56158260bb29fca793e41bd2f095dfcd1fd8bc925e730a56bd74b02` |
| `src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommand.cs` | 10 | R: 1–10; no separate finding; runtime limitations apply | `46ad5926c15fd15f67d6087afbcbc298dc8335204704712170d8639dac521c07` |
| `src/Application/Features/Digests/Commands/GenerateDigest/GenerateDigestCommandHandler.cs` | 190 | R: 1–190; P08, P09, P10 | `ade5864506f5b637a73a34890de6f5a81ca32f0b01f9392a590ce08e3eb1d3df` |
| `src/Application/Features/Digests/Commands/RecordFeedback/RecordRecommendationFeedbackCommand.cs` | 14 | R: 1–14; no separate finding; runtime limitations apply | `fc39f231944dc2f7c1429c6f1b9c91cfb2badccab140539b9012eadb97b6d12c` |
| `src/Application/Features/Digests/Commands/RecordFeedback/RecordRecommendationFeedbackCommandHandler.cs` | 137 | R: 1–137; no separate finding; runtime limitations apply | `f54e8d0a225d5625b7c948a4c3c833d75a1a65fd42bcb9cc6cb72eefbd386b77` |
| `src/Application/Features/Digests/Commands/RecordFeedback/RecordRecommendationFeedbackCommandValidator.cs` | 34 | R: 1–34; no separate finding; runtime limitations apply | `08c65d64b37fc44bd84aace4e2877111748e4d400b737ddd8c5e6187b4856682` |
| `src/Application/Features/Digests/Commands/RunScheduledTick/RunScheduledTickCommand.cs` | 81 | R: 1–81; no separate finding; runtime limitations apply | `db6700eb9ae9ad712c3d59c8b008875c323514cd7b45a550fbb50822a85eada0` |
| `src/Application/Features/Digests/Queries/GetLatestDigest/GetLatestDigestQuery.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `1b791f82a64bc5583d76248135dec7188ee959b61f1d70d3ce2c5efbf914bf0d` |
| `src/Application/Features/Digests/Queries/GetLatestDigest/GetLatestDigestQueryHandler.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `c141df4ac0af1ff17014520a0207bbd1d6e984494987511e17bfc95d900f6fc9` |
| `src/Application/Features/Discovery/CleanupPublicDiscovery.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `fdf06d94277163891c78847985c65288d9f001761fe06cbe9bdec9e1d43e4596` |
| `src/Application/Features/Discovery/DiscoveryRuntimeOptions.cs` | 21 | R: 1–21; no separate finding; runtime limitations apply | `1dd1baedc68b9063bf23c7b1aa41327ec5a2024732aae388f1e91dc8b2199d38` |
| `src/Application/Features/Discovery/DiscoveryViews.cs` | 22 | R: 1–22; no separate finding; runtime limitations apply | `2e8988f1abdeaea3f87f157843af974e6bd6966b1d673cd488110028d589819d` |
| `src/Application/Features/Discovery/GuestLifecycle.cs` | 52 | R: 1–52; no separate finding; runtime limitations apply | `890697060fb8c72c01d770c143073c6e645b47640520cdfa8cd404d67ac76243` |
| `src/Application/Features/Discovery/MaintainDiscoveryStorage.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `57294bca9e922143d1440ccc4f82581d10ee090b452de9abf1a1926f1dfb8e95` |
| `src/Application/Features/Discovery/MaterializeDiscoveryTaste.cs` | 146 | R: 1–146; no separate finding; runtime limitations apply | `72eaf911c57a795c06b5bb8c54041f4682d980a96d9d0e3cb8ccfad47d15c15b` |
| `src/Application/Features/Discovery/PrepareDiscoveryBatch.cs` | 150 | R: 1–150; P08, P10 | `22b033be58de9794113937ddbc970162d745c104856e9c89fb3176765d8117b9` |
| `src/Application/Features/Discovery/RecordDiscoveryFeedback.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `3b65dff805c9765e85b08c3f15d5ed47a2f40d119c4b15ab41cbd524ab77d3e6` |
| `src/Application/Features/Discovery/RunDiscoveryJob.cs` | 30 | R: 1–30; no separate finding; runtime limitations apply | `835c75358588be49d170a9937b037051f74731d2630b3f785022f289bde4a783` |
| `src/Application/Features/Discovery/SaveTasteOnboarding.cs` | 33 | R: 1–33; no separate finding; runtime limitations apply | `c81a3d856e45d53ab90ef7b9620e798a53e159636224fcf853d52b46b6f89ae0` |
| `src/Application/Features/Discovery/SelectDiscoveryTaste.cs` | 79 | R: 1–79; P11 | `68ffb28ab0a76a569fdd27ed3897d7c307c67f8c736f84acda27b5f78ba9c4fc` |
| `src/Application/Features/Discovery/SubmitDiscovery.cs` | 65 | R: 1–65; no separate finding; runtime limitations apply | `3632358ee016c8527dd20e5119e800d50e7bf8230d11ddd27922a8bc98d72b75` |
| `src/Application/Features/Discovery/TasteVocabulary.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `5a2557847eea918bbae55e9086a02e0d2394222618de88a924a581c45b7a5db3` |
| `src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQuery.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `b982f6a8a42f2c0cf376be223bf9af26a72329a8dc272c026574610e26e611c3` |
| `src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQueryHandler.cs` | 96 | R: 1–96; P13 | `a574e7a478f478e0a21c2329dac24d96a0f72d6a0cb0f32045510b1c484c0120` |
| `src/Application/Features/Subscribers/Commands/DeleteUserAccount/DeleteUserAccountCommand.cs` | 8 | R: 1–8; no separate finding; runtime limitations apply | `ca64eaac95b53880585c1e6f8749a365794012e3f6a1fd8bb2e5a782764f5106` |
| `src/Application/Features/Subscribers/Commands/DeleteUserAccount/DeleteUserAccountCommandHandler.cs` | 34 | R: 1–34; no separate finding; runtime limitations apply | `21de73969a8cec32ce5f43d5266531bab98a802dbd14e1fb7ad91a8a0c92f216` |
| `src/Application/Features/Subscribers/Commands/RegisterSubscriber/RegisterSubscriberCommand.cs` | 17 | R: 1–17; no separate finding; runtime limitations apply | `3c3c07a278c8bea8a0ad4c71c38649de04391b80f03fffd4231796acdcfd3c63` |
| `src/Application/Features/Subscribers/Commands/RegisterSubscriber/RegisterSubscriberCommandHandler.cs` | 64 | R: 1–64; no separate finding; runtime limitations apply | `aabdac5872a9a31ed2f146674c313b3284a08cf437336aff992b54cd14c785fa` |
| `src/Application/Features/Subscribers/Commands/RegisterSubscriber/RegisterSubscriberCommandValidator.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `7850d090732a5067871b0067df8d2c9a6918e2069be21bd55c7cb09d85fb8349` |
| `src/Application/Features/Subscribers/Commands/UnsubscribeUser/UnsubscribeUserCommand.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `bb4cfc00e3bcf5baa25f9c0042cb6edd88bdfe334cab3ecce33e8eefe911650b` |
| `src/Application/Features/Subscribers/Queries/GetSubscriberProfile/GetSubscriberProfileQuery.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `d06aed9c359e1ca4118a9d85d72cd6a248c33e1750ebdd1b11b74c7450856c3e` |
| `src/Application/Features/Subscribers/Queries/GetSubscriberProfile/GetSubscriberProfileQueryHandler.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `4cb5ee73c6cb9222d52f1f20718c4fa962b2efc195c9c8aa4bef4683259e2c46` |
| `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommand.cs` | 12 | R: 1–12; no separate finding; runtime limitations apply | `b6606820f227c069519c25cc5a5890f82f6bdb855ddbda33d82b165e770976dd` |
| `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommandHandler.cs` | 75 | R: 1–75; no separate finding; runtime limitations apply | `76d455a3320a4a59d4db6e663e1f81adfba50325a64362cf5fda9caf6d52fe35` |
| `src/Application/Features/Taste/Commands/SeedTasteProfile/SeedTasteProfileCommandValidator.cs` | 41 | R: 1–41; no separate finding; runtime limitations apply | `4432fb0b57065d327d65b5de5b98b1d8097408f7f62a7bb801f9e7c91ba97ddf` |
| `src/Application/Features/Taste/Queries/GetUserTasteSignals/GetUserTasteSignalsQuery.cs` | 9 | R: 1–9; no separate finding; runtime limitations apply | `cf677a3321ad3076c47295fea527c93c6da1abd7f9a64beb8005e3cff3f9df3b` |
| `src/Application/Features/Taste/Queries/GetUserTasteSignals/GetUserTasteSignalsQueryHandler.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `57910076272b03f2626826a7392d520e566e901b81b099417e65f21a5e6d5984` |
| `src/Application/Services/IAuthService.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `54b8d6fd57ba863de2bc463e04b75622ed405d1e394e5f72313bdbf380bbb9f7` |
| `src/Application/Services/ICurrentUserService.cs` | 11 | R: 1–11; no separate finding; runtime limitations apply | `4739d61ad42d20978f1a5c2a1e61ef18488cae24cc6626bf147aea1d1bb84885` |
| `src/Application/Services/IJwtService.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `9fd69fccbff48d23accdb8cdca3d7eef83f423b40200c527a5b6ef79be53f76f` |
| `src/Application/Services/ValidatorInterceptor.cs` | 3 | R: 1–3; no separate finding; runtime limitations apply | `2c1cb22cd7556791b989d021eab40514312e573c5a0538aab064ca082bdcdaf0` |
| `src/Application/packages.lock.json` | 47 | R: 1–47; no separate finding; runtime limitations apply | `c9865699dc1c96c42abacf325fa2cd30f567b2385a956691589fc804c0c4046f` |
| `src/DataAccess/Core/AsyncRepository.cs` | 146 | R: 1–146; no separate finding; runtime limitations apply | `17913c9dafd44b4b589a28847c9bace86abe4f32ce4a2368c74f2e307385367a` |
| `src/DataAccess/Core/UnitOfWork.cs` | 49 | R: 1–49; no separate finding; runtime limitations apply | `15acd21d538002f8f22109492d0909cb5f543a14085dc67fe789513dedbb3dd2` |
| `src/DataAccess/DapperPagedRepositoryBase.cs` | 90 | R: 1–90; no separate finding; runtime limitations apply | `904c0bc165f03556c5575c62a09894b023c76f45b97d4e6e79306244b67b0e80` |
| `src/DataAccess/DataAccess.csproj` | 31 | R: 1–31; no separate finding; runtime limitations apply | `b605293a510f1385c9132a8be8df9adf3b926e22af2b40fae5590c03e7de5ce4` |
| `src/DataAccess/DataContexts/DataContext.cs` | 146 | R: 1–146; no separate finding; runtime limitations apply | `9ee3cfa3defce67160860cfe0ca4542622fc44429e9d6a22965e14ee3a108d71` |
| `src/DataAccess/DependencyInjection.cs` | 95 | R: 1–95; no separate finding; runtime limitations apply | `12cf00e1b18cb8ee388cac48477b5f4b98a24432a7a3b3e90d74415887a9493c` |
| `src/DataAccess/IdentityEntities/ApplicationUser.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `e92c48d9ad176b3507a0ce9feb22b900e5abe76737028cb82508615830b5f8a1` |
| `src/DataAccess/Persistence/AccountEmailLease.cs` | 41 | R: 1–41; no separate finding; runtime limitations apply | `4abe3bbf33a048932f2d49811a1764c72ed46b538219ec6a8a66120e896630cd` |
| `src/DataAccess/Persistence/Accounting/ManagedStorageIntegrity.cs` | 60 | R: 1–60; no separate finding; runtime limitations apply | `9a77496a52e3ed0d30ff475886753b734b512757832c6ac18a1b90c1a4271c76` |
| `src/DataAccess/Persistence/Accounting/ManagedStorageLedger.cs` | 11 | R: 1–11; no separate finding; runtime limitations apply | `b81ca20f69d28b5707ec99a4e513fa24f60c811a559a102156d470012bddf299` |
| `src/DataAccess/Persistence/Accounting/PublicEvidenceCacheEntry.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `7843c194c2ebddf629b3b1cd925bb51d97af498f98abf259d950e73f16531498` |
| `src/DataAccess/Persistence/Accounting/RecommendedIdentity.cs` | 8 | R: 1–8; no separate finding; runtime limitations apply | `c3812df09bea373a419b3f488efd355c93c4e51ed0252f7f9feb7fc2340375b1` |
| `src/DataAccess/Persistence/AppDbContext.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `c76454d29497d4b98cb1f0641a966ffe88e9b0b2eb7f2207a7168d3d4495fc47` |
| `src/DataAccess/Persistence/Configurations/AlbumConfiguration.cs` | 36 | R: 1–36; no separate finding; runtime limitations apply | `cbaa3a4207968674e84de36dc12464010bdce88a7b27f96a456df26084f02a18` |
| `src/DataAccess/Persistence/Configurations/ArtistConfiguration.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `fecc51ff867a4f7b333b737259506286e8ce4e84e3dde2491d39cb1dbb6caf5d` |
| `src/DataAccess/Persistence/Configurations/CatalogSearchConfiguration.cs` | 75 | R: 1–75; no separate finding; runtime limitations apply | `48c5e123373d3067c44438e02cadde235d7e9be323e9cdde7ff42aeae1ea1b30` |
| `src/DataAccess/Persistence/Configurations/DiscoveryConfiguration.cs` | 80 | R: 1–80; no separate finding; runtime limitations apply | `72fc55fe41165566cfe775f369955c98595446a0e1ba976d0ecc723e8c3a594b` |
| `src/DataAccess/Persistence/Configurations/ManagedStorageLedgerConfiguration.cs` | 18 | R: 1–18; no separate finding; runtime limitations apply | `a0ded9ceb41d564916007069c9d7772318fcf070fb96f35ce75744c419d5a8e1` |
| `src/DataAccess/Persistence/Configurations/PublicEvidenceCacheConfiguration.cs` | 25 | R: 1–25; no separate finding; runtime limitations apply | `a62d74c1fc9ddad4e489ceb68a8c9c1cef13bb3e5ba79daf6a53b40c59abcaae` |
| `src/DataAccess/Persistence/Configurations/RecentRecommendationConfiguration.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `12e2fecf56958447c0b1e6a3027c337aef64b836cc39417f0ec5ce874ee68e12` |
| `src/DataAccess/Persistence/Configurations/RecommendedIdentityConfiguration.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `b552574f8eaf7269f7642f59893957d631fa3d7a05c70afae7fab4937a4ae348` |
| `src/DataAccess/Persistence/Configurations/TasteSignalConfiguration.cs` | 61 | R: 1–61; no separate finding; runtime limitations apply | `fd7aad3d470c54d0d1a1f55c669a45e4a275f4ce3f01e5a341414d6e929beac0` |
| `src/DataAccess/Persistence/Configurations/TrackConfiguration.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `6796fdc0b350de4bd3d213b01b3fa7a7dce03c96de8ee6338498d6c6e9aa9040` |
| `src/DataAccess/Persistence/Configurations/UserConfiguration.cs` | 71 | R: 1–71; no separate finding; runtime limitations apply | `f3b60d4cb154c8fd59e00c985c13c5c7e0cb764eaaa8b36a361b06b6f1622c0e` |
| `src/DataAccess/Persistence/Configurations/UserMusicConnectionConfiguration.cs` | 48 | R: 1–48; no separate finding; runtime limitations apply | `b49ee96dafd7138d709482b979ee4c9f1d6efeaef5b17593c6052f154aaafafb` |
| `src/DataAccess/Persistence/Configurations/WeeklyDigestConfiguration.cs` | 70 | R: 1–70; no separate finding; runtime limitations apply | `9151b3e81e8d66929a0d75cb9e9e801106469fb88ecef87723f1bacfb1eb0788` |
| `src/DataAccess/Persistence/Configurations/WeeklyRecommendationConfiguration.cs` | 72 | R: 1–72; no separate finding; runtime limitations apply | `2d85b4e9b5668198da28681516df930bbea3daa34de62a18e5402465c88c223d` |
| `src/DataAccess/Persistence/LocalUnsubscribeProtection.cs` | 39 | R: 1–39; no separate finding; runtime limitations apply | `1b1d5b316bd6fb3602da55c952149bac131ed66caa491830c908245e51bb5e03` |
| `src/DataAccess/Persistence/Migrations/20260930194436_InitialCreate.Designer.cs` | 472 | G: 1–472; no separate finding; runtime limitations apply | `e84e836387b4f229d796f1ceb29fe077e1eb5c101faf4b4c396b7f2a5eefef98` |
| `src/DataAccess/Persistence/Migrations/20260930194436_InitialCreate.cs` | 329 | R: 1–329; no separate finding; runtime limitations apply | `e551a4a487e8d6006a0eff1b8c2bd968ec3e5a67441d5a170df8d80cb4135025` |
| `src/DataAccess/Persistence/Migrations/20261002203611_AddRatingToWeeklyRecommendation.Designer.cs` | 822 | G: 1–822; no separate finding; runtime limitations apply | `20974f05141c5f9d8e1923a32d39fdd7a2ea9299b99402d4fbf208172de70536` |
| `src/DataAccess/Persistence/Migrations/20261002203611_AddRatingToWeeklyRecommendation.cs` | 528 | R: 1–528; no separate finding; runtime limitations apply | `290f7de425741071c96bdf6da4003872051ad844338542772927e64270952694` |
| `src/DataAccess/Persistence/Migrations/20261010205812_AddEmailUnsubscribedAtUtc.Designer.cs` | 825 | G: 1–825; no separate finding; runtime limitations apply | `9bbcb4636c4f4f0ac8501fe3ce7795533c5beb4b1363575f050fab74389f1c2a` |
| `src/DataAccess/Persistence/Migrations/20261010205812_AddEmailUnsubscribedAtUtc.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `e3a43ab7d1b3a802bcf0a5cd4a0b0d940c3f7436428dc73e7b042ddd7f237b07` |
| `src/DataAccess/Persistence/Migrations/20261016092843_AddListenerProfilesAndDiscoveryBatches.Designer.cs` | 1044 | G: 1–1044; no separate finding; runtime limitations apply | `094ec42ecea8fd9be9159bde30cf67c6c0f52292a92b4a3a5eebaff205826ac5` |
| `src/DataAccess/Persistence/Migrations/20261016092843_AddListenerProfilesAndDiscoveryBatches.cs` | 212 | R: 1–212; no separate finding; runtime limitations apply | `14165465b9b38257265e75d44cc435258334ca87c8bea97fd0ddd63f6913091c` |
| `src/DataAccess/Persistence/Migrations/20261016100239_GeneralizeDiscoveryOwnership.Designer.cs` | 1083 | G: 1–1083; no separate finding; runtime limitations apply | `ab2bcc1715704d24eda4f642fd97d238672ec0769b1b3151c843080907301d03` |
| `src/DataAccess/Persistence/Migrations/20261016100239_GeneralizeDiscoveryOwnership.cs` | 260 | R: 1–260; no separate finding; runtime limitations apply | `7541633a296d3f4ec5525f17b686561ea013900022d36695059a75f9dc59bc93` |
| `src/DataAccess/Persistence/Migrations/20261016101423_AddGuestSessions.Designer.cs` | 1113 | G: 1–1113; no separate finding; runtime limitations apply | `1b1ae1bb83e3ce8186a1b31b582e24ff7fcb859472721fb57b22c2910d6acf66` |
| `src/DataAccess/Persistence/Migrations/20261016101423_AddGuestSessions.cs` | 60 | R: 1–60; no separate finding; runtime limitations apply | `3034c1ce5adece479011d4440d45bf6ca24cb00ad2842fa8c14e37391ac8bc95` |
| `src/DataAccess/Persistence/Migrations/20261016102144_RequireExplicitWeeklyEmailOptIn.Designer.cs` | 1118 | G: 1–1118; no separate finding; runtime limitations apply | `66e6eac673e9ab07bb48f2c8361b9ff8e269cdab410cc0f47850c8f3fd1eab91` |
| `src/DataAccess/Persistence/Migrations/20261016102144_RequireExplicitWeeklyEmailOptIn.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `3d031cd00512baaf99c55eff677c882b5c9edbc2293af0d37e7f9556c4056358` |
| `src/DataAccess/Persistence/Migrations/20261016130431_AddLocalDiscoveryCatalog.Designer.cs` | 1397 | G: 1–1397; no separate finding; runtime limitations apply | `f02fa312dbdd255a34aacd06b7efdb1d8ea625987d0c8272c589ab413b73168f` |
| `src/DataAccess/Persistence/Migrations/20261016130431_AddLocalDiscoveryCatalog.cs` | 246 | R: 1–246; no separate finding; runtime limitations apply | `9b31fc489cb256536608ad7ce6cbd04192a7af1881ab6cae3629f53cc4afc991` |
| `src/DataAccess/Persistence/Migrations/20261016133449_AddCanonicalTasteSelections.Designer.cs` | 1406 | G: 1–1406; no separate finding; runtime limitations apply | `ca25e9457d4f8de8c9d804d47a979687d2d3f6889e473acd56aea6bfb19ebb2c` |
| `src/DataAccess/Persistence/Migrations/20261016133449_AddCanonicalTasteSelections.cs` | 64 | R: 1–64; no separate finding; runtime limitations apply | `7628cf68b8924233c7e2939f4c6114bc698d8a98fe5c5fda637a60ef045f2963` |
| `src/DataAccess/Persistence/Migrations/20261017124050_AddManagedStorageLedger.Designer.cs` | 1431 | G: 1–1431; no separate finding; runtime limitations apply | `03d0964af7d0acd49a829218b612aa3ab40e63d823bde9053392a745fc507f6e` |
| `src/DataAccess/Persistence/Migrations/20261017124050_AddManagedStorageLedger.cs` | 88 | R: 1–88; no separate finding; runtime limitations apply | `7ccfeb02bc1e687ddd241bff77cc4ed47ec3d17d7e8e5dbdddc818dd96279035` |
| `src/DataAccess/Persistence/Migrations/20261017130938_AddSharedDiscoveryEvidenceAndHistory.Designer.cs` | 1527 | G: 1–1527; no separate finding; runtime limitations apply | `b0a4ba4e847a75443d3e94395f8c9d577fee64874af034ff58c290dc72f3203e` |
| `src/DataAccess/Persistence/Migrations/20261017130938_AddSharedDiscoveryEvidenceAndHistory.cs` | 204 | R: 1–204; no separate finding; runtime limitations apply | `3790bf5f7765a90f99d27317a644153e85d9b0562b05e498e64a5b12bbe70eea` |
| `src/DataAccess/Persistence/Migrations/20261018195528_IndexCatalogExpiryMaintenance.Designer.cs` | 1529 | G: 1–1529; no separate finding; runtime limitations apply | `0dd7e2599e3a3b997131e03e5b5bfe2f80648283433e14c4e9b1da84f3e0b995` |
| `src/DataAccess/Persistence/Migrations/20261018195528_IndexCatalogExpiryMaintenance.cs` | 27 | R: 1–27; no separate finding; runtime limitations apply | `5ae9284ba037893b034fc18410288eb3f6374999c633a0063b8ef4e5aeafe768` |
| `src/DataAccess/Persistence/Migrations/20261019083146_AddOptionalTasteOnboarding.Designer.cs` | 1575 | G: 1–1575; no separate finding; runtime limitations apply | `655559c21223a9b64174e396c74cfa436d29ed856b4032f4ca44409dac31bad8` |
| `src/DataAccess/Persistence/Migrations/20261019083146_AddOptionalTasteOnboarding.cs` | 190 | R: 1–190; no separate finding; runtime limitations apply | `01f931455013547741cd310a71e7bef1ef8cd25bff159f374804422e0a1758af` |
| `src/DataAccess/Persistence/Migrations/20261019084129_StoreRecordingReleaseHints.Designer.cs` | 1581 | G: 1–1581; no separate finding; runtime limitations apply | `b77a6999145354d00ae68e13832fa51855782c93a93c1afd293294e21a71d5da` |
| `src/DataAccess/Persistence/Migrations/20261019084129_StoreRecordingReleaseHints.cs` | 110 | R: 1–110; no separate finding; runtime limitations apply | `80e54f079497a259d7a1dbeef81da83d9db71374e026cb804aba23ef598f8d21` |
| `src/DataAccess/Persistence/Migrations/AppDbContextModelSnapshot.cs` | 1578 | G: 1–1578; no separate finding; runtime limitations apply | `4cb4d00c8921fdd02767fb1e9ba6c1ada4ff0387813ae4de984649c4c675a39f` |
| `src/DataAccess/Persistence/Repositories/AccountProfileReader.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `494346d48370d0c8e0e46a20d894f20e0fd8fd1a378907645d1b7e418ed67365` |
| `src/DataAccess/Persistence/Repositories/CatalogImportStore.cs` | 131 | R: 1–131; no separate finding; runtime limitations apply | `79eb3284141fd5ca92679e99d95df92b61ba3a58e3809fbb21c73846358bb0cd` |
| `src/DataAccess/Persistence/Repositories/CatalogReader.cs` | 97 | R: 1–97; P11 | `5d44a276ee85c722258746162fae4f7964c1e899a631be9898f581ed0fe5a28c` |
| `src/DataAccess/Persistence/Repositories/DigestGenerationStore.cs` | 167 | R: 1–167; P09 | `0b7dc36ebc6bcd6232d0787dbe71664f192b99f06aa101871a5d316812f21afd` |
| `src/DataAccess/Persistence/Repositories/DiscoveryBatchStore.cs` | 140 | R: 1–140; no separate finding; runtime limitations apply | `3242303e06499446c6160415edba57ec0af56641e5819889e94ad3f643c0cb65` |
| `src/DataAccess/Persistence/Repositories/DiscoveryJobStore.cs` | 128 | R: 1–128; P07 | `a0936fb80175be7f6d205404ba787ecc55fdc9584aa0e864a4808961f873ea11` |
| `src/DataAccess/Persistence/Repositories/DiscoveryProfileReader.cs` | 61 | R: 1–61; P06 | `430aafe6009bf9ea89537a26b4b65c9fc3add57edbf3c9b3215c96e99dc3768b` |
| `src/DataAccess/Persistence/Repositories/DiscoveryReader.cs` | 50 | R: 1–50; no separate finding; runtime limitations apply | `9d8b42f55281b79ab5c9f538248e606bcb56dd5dce2d752f8226043f31d94fcb` |
| `src/DataAccess/Persistence/Repositories/DiscoveryTasteStore.cs` | 46 | R: 1–46; no separate finding; runtime limitations apply | `fc546f195abaa7441163a9232a31a6d0fc3aa8ae8448d80f53784e63d80e4c69` |
| `src/DataAccess/Persistence/Repositories/DiscoveryWriteCoordinator.cs` | 99 | R: 1–99; P01 | `07c4e41787418457e848d0e8a088a93da1567137fb719e5ab506060977c64d51` |
| `src/DataAccess/Persistence/Repositories/GuestProfileStore.cs` | 119 | R: 1–119; no separate finding; runtime limitations apply | `7f605ae39ad8cce68157c2844a21244ec6b9bf2e3677f30d7c916cdf6b1e4a54` |
| `src/DataAccess/Persistence/Repositories/ListenerProfileRepository.cs` | 25 | R: 1–25; no separate finding; runtime limitations apply | `c9c90ca9e89b0aa6d0ede3ff81589f163f78223ddb9e4823db44ffaf70915bac` |
| `src/DataAccess/Persistence/Repositories/LocalDigestCaptureStore.cs` | 82 | R: 1–82; no separate finding; runtime limitations apply | `ae8e3f16aeec226c0ac76b9792b905df015a05d3a4d61ac1b3e6f40a01fdcbf0` |
| `src/DataAccess/Persistence/Repositories/PublicEvidenceStore.cs` | 100 | R: 1–100; P12 | `d183ba393e7548a2517a78ca335e1ca8c766119949046b0d7dd101ae3cbafb82` |
| `src/DataAccess/Persistence/Repositories/RecentRecommendations.cs` | 24 | R: 1–24; no separate finding; runtime limitations apply | `a504a2a7925fabba04fdab4dbf5ea74c519476b1dc2d939212f452f8f0941afb` |
| `src/DataAccess/Persistence/Repositories/TasteSignalRepository.cs` | 59 | R: 1–59; no separate finding; runtime limitations apply | `13f9c330d4f2ad6cbf2eccc4ba4dda80af4aa9026d70efff2557299d7f4c529b` |
| `src/DataAccess/Persistence/Repositories/TrackRepository.cs` | 43 | R: 1–43; no separate finding; runtime limitations apply | `264556d0043c4e760f3b0bf052e4e25cea79076a9bf08b73773b6fb47acbd60e` |
| `src/DataAccess/Persistence/Repositories/UnsubscribeStore.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `01db95eb2cde835654a209988fffd81fcebb7614a3c9c737faecc7e61e058c2e` |
| `src/DataAccess/Persistence/Repositories/UserRepository.cs` | 61 | R: 1–61; no separate finding; runtime limitations apply | `33e486f8c5a615b151d8256bb40703310d3314c0d3cc23fd0b9515a75215836d` |
| `src/DataAccess/Persistence/Repositories/WeeklyDigestRepository.cs` | 90 | R: 1–90; no separate finding; runtime limitations apply | `978a2db87803797f85771ee7d25d4a42ebc0b83c781638c53ceb92a385e3fbda` |
| `src/DataAccess/Persistence/Repositories/WeeklyScheduleStore.cs` | 174 | R: 1–174; no separate finding; runtime limitations apply | `7995c2ffcf7395e2072ad3dcafb34c4bb3e5c0a3d2931d1cf31562f9c6d1444d` |
| `src/DataAccess/packages.lock.json` | 317 | L: 1–317; no separate finding; runtime limitations apply | `dff6812eba220e37066a2b54fad0953a3d4adebfbfc84d91ed2932126e62f3a3` |
| `src/Domain/Catalog/Album.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `bbd4b7290076502fb82d00b0215cd675ee5258dbdd5af187e3519cb7ace783f4` |
| `src/Domain/Catalog/Artist.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `2e6c37dcef566d8fba8f3bf4d95b7065233e7ec18cf1fe45e33ea929207eff99` |
| `src/Domain/Catalog/CatalogPublication.cs` | 65 | R: 1–65; no separate finding; runtime limitations apply | `cbcea0c47461a68fb087b397c60c9f56a7fe8f4b7421c47eccdc7c4b6757add9` |
| `src/Domain/Catalog/CatalogRecord.cs` | 63 | R: 1–63; no separate finding; runtime limitations apply | `fdb12d83646f12d2d42c85eaecb612a61a16a6668e19ec0308b11d28fed91f84` |
| `src/Domain/Catalog/CatalogSearch.cs` | 62 | R: 1–62; no separate finding; runtime limitations apply | `531c1d9f224e9b792933d8b38ad00c5a08c633fec028a20fa0082563afafde27` |
| `src/Domain/Catalog/Tag.cs` | 49 | R: 1–49; no separate finding; runtime limitations apply | `756e5bcafc0440ec13aa04c4c1a839d902a197efb8565e1dc819bee592fbbc50` |
| `src/Domain/Catalog/Track.cs` | 73 | R: 1–73; no separate finding; runtime limitations apply | `5dc0da114c534dcd35e49f3ffef7f087c71bd665db5eedf2c2f28727448b981a` |
| `src/Domain/Catalog/WeightedTag.cs` | 36 | R: 1–36; no separate finding; runtime limitations apply | `fd0528f73d2a10806ba1d0fd094bd391e9cc0ab49352de89d4465ededb792e41` |
| `src/Domain/Catalog/WeightedTagVector.cs` | 190 | R: 1–190; no separate finding; runtime limitations apply | `4ba386f3d6814d291e415065987ca22906e28f2ef86621e256b54c332796402f` |
| `src/Domain/Common/AuditableEntity.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `562bf867179f2354e2bb740879ed4f14023099e739002ff065480d6c2fe14f04` |
| `src/Domain/Common/BaseEntity.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `63cce9fecf6c02f797ec2a81e29f7d59b43570689b64db550ed739012756b38f` |
| `src/Domain/Common/IAuditableEntity.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `4e14cd763bcf55c3b8b48a8726b852a1cfd4b116185da32f3301bba09a3da114` |
| `src/Domain/Digest/IsoWeek.cs` | 87 | R: 1–87; no separate finding; runtime limitations apply | `d16650c0b46f2276e4734c9c2424b0550ae17a8be07699d0ef78f1cf0536bb73` |
| `src/Domain/Digest/User.cs` | 110 | R: 1–110; no separate finding; runtime limitations apply | `b8cec121452958e04612decb4dcae9acb7300e26ada1656afbd94c0561c36ed4` |
| `src/Domain/Digest/UserMusicConnection.cs` | 76 | R: 1–76; no separate finding; runtime limitations apply | `960def66127c185db82eb3643eb89a0b6eb87b5252083428c3d9ed2c4cffcf29` |
| `src/Domain/Digest/WeeklyDigest.cs` | 128 | R: 1–128; no separate finding; runtime limitations apply | `6a1690d2e9c83bea7a9a8117dd3797cf4e4c689341f7a24f2e44cff8fc4ec36f` |
| `src/Domain/Digest/WeeklyRecommendation.cs` | 91 | R: 1–91; no separate finding; runtime limitations apply | `a98b5d106afb3daae9891c7912091fb29523d6a88a1df103827e90c7ee0c4065` |
| `src/Domain/Digest/WeeklySchedulePolicy.cs` | 52 | R: 1–52; no separate finding; runtime limitations apply | `86148e245ca4554f4edab1d6e79282a966a3b8b3146c4412c8e5f78d0808aa93` |
| `src/Domain/Discovery/DiscoveryBatch.cs` | 47 | R: 1–47; no separate finding; runtime limitations apply | `0ee520db8845c5e84aa2b5d06f7b15cece9990af8d4056e4695fb53fba1fd772` |
| `src/Domain/Discovery/DiscoveryJob.cs` | 127 | R: 1–127; no separate finding; runtime limitations apply | `1c8931519cbc2a37a9d0792136ace16d473a462c73e5b05314392a91fcffca09` |
| `src/Domain/Discovery/DiscoveryRules.cs` | 17 | R: 1–17; no separate finding; runtime limitations apply | `28af5d182e1e4418e2fc7a6d457911bd49ec67a5f59417ae5adbf8d10ddd15a0` |
| `src/Domain/Discovery/DiscoverySelectionPolicy.cs` | 46 | R: 1–46; no separate finding; runtime limitations apply | `d9ae227359d7d6500693eb29a4859a1b3e9f0617a724dca5ed67ad3dac2ac284` |
| `src/Domain/Discovery/GuestSession.cs` | 18 | R: 1–18; no separate finding; runtime limitations apply | `544fca3fc0689caa70355d6faa0d713c71b0e3857da8d4feb9ea9108c55c7590` |
| `src/Domain/Discovery/ListenerProfile.cs` | 79 | R: 1–79; no separate finding; runtime limitations apply | `98d19803dd005bff37c32a02dad6174fc84e79ac08ea595f9974b5c612d32b79` |
| `src/Domain/Discovery/RecentRecommendation.cs` | 26 | R: 1–26; no separate finding; runtime limitations apply | `8a239d6bb1a42d91ac15d2af928b39166ab7b842fd00c591de7071b3a9aca8d1` |
| `src/Domain/Discovery/TasteOnboarding.cs` | 30 | R: 1–30; no separate finding; runtime limitations apply | `5c153f34f221620e89a2d5d3e6816c788d9338ed5123b7a4b083a7522693de26` |
| `src/Domain/Domain.csproj` | 8 | R: 1–8; no separate finding; runtime limitations apply | `542180b8c1da3f49e1db4d866d77bdabfd1587f249586f399ba9b34aa0d07a02` |
| `src/Domain/Enums/DigestDeliveryDay.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `778dcda257cded5963a07b5de36593188209e72b6e9a9c47720a05252ed4dd03` |
| `src/Domain/Enums/DigestStatus.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `1ed10acab1522cc434bb77f3f7672a7ad222c4318feda9026235c3069e6ee00c` |
| `src/Domain/Enums/MusicServiceType.cs` | 11 | R: 1–11; no separate finding; runtime limitations apply | `7057808bd82e4372a08d93ce4ccd00cecfc414df786388f17037700cc87156e6` |
| `src/Domain/Enums/TasteSignalSource.cs` | 18 | R: 1–18; no separate finding; runtime limitations apply | `982328dbc5fb97bc1a292a510c2e6f0a5ff17df58ef3dd86e41e9424359cf892` |
| `src/Domain/Enums/TasteTargetType.cs` | 12 | R: 1–12; no separate finding; runtime limitations apply | `489fb8b9f40e5536fe3c7e26a420ef059a8dffec2c02c92678eb28725a893e7a` |
| `src/Domain/Enums/UserFeedback.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `f0b87dcc8587fc28813e2bd6fa06edd9c9628da96220dfba0f287b0bfa53916b` |
| `src/Domain/Scoring/BaselineAScorer.cs` | 185 | R: 1–185; P08, P10 | `cd309d199ef49858f0725c8d83ee486cc1d89c9fd90f9fecc8349a5036caf1bd` |
| `src/Domain/Scoring/CandidateTrack.cs` | 54 | R: 1–54; no separate finding; runtime limitations apply | `e1cc4085caa881b868449c90f88358bb8b681b4354dca2f413527a8f7170da64` |
| `src/Domain/Scoring/MatchedTagContribution.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `dfd32e24cc64853c23d9446447ac073c7608e279637670625b4331a4adc24f38` |
| `src/Domain/Scoring/RecommendationScorer.cs` | 122 | R: 1–122; no separate finding; runtime limitations apply | `76bf1d49d42c2ee467303e3c512c20827c839cb4dd7eba35746862bd90cd151a` |
| `src/Domain/Scoring/ScoreBreakdown.cs` | 122 | R: 1–122; no separate finding; runtime limitations apply | `89db00a8c2a9d55fece50a2782ac8111a9763d3fd122af5b4d5060a5a5d4b797` |
| `src/Domain/Scoring/ScoreSnapshot.cs` | 96 | R: 1–96; no separate finding; runtime limitations apply | `e30ab605e5d4b49d0d68e36ee1f0bc045427c89f5bfc5cd678833e36cecca32c` |
| `src/Domain/Scoring/ScoredCandidate.cs` | 13 | R: 1–13; no separate finding; runtime limitations apply | `334edd851dbd1098b981657db70acf15d184f6a237d499a40b106b8ce860dd4b` |
| `src/Domain/Scoring/ScoringParameters.cs` | 38 | R: 1–38; no separate finding; runtime limitations apply | `a775dfb0b5f68ab81e5607a8d002cc1aab65301e53e4fa576976b35ec565ab5d` |
| `src/Domain/Taste/DiscoveryTasteMaterializer.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `77a0105592d60edf0421e20baf92fa1356a57a18d69f8f5c3a2fa9cbcc88752f` |
| `src/Domain/Taste/EffectiveTasteInputs.cs` | 75 | R: 1–75; no separate finding; runtime limitations apply | `859871676cb6c7612036d50b1b91c66017b8b3591c53aaf7305c1d31d00ecad5` |
| `src/Domain/Taste/TasteSignal.cs` | 102 | R: 1–102; no separate finding; runtime limitations apply | `000e4418055f3255ac00c693da6a4e72b1ed1f90499c54b292f6f440febed520` |
| `src/Domain/Taste/TasteVectorMaterializer.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `ca96d6ff08166f92af39b459e5fd5bae140c50bc29f2deea22152f87a61b3cf0` |
| `src/Domain/Taste/UserTasteProfile.cs` | 72 | R: 1–72; no separate finding; runtime limitations apply | `494930d3ce9a09232cc7320d7dc140a183a8d22ae5dc207cfd72ca3e94ab1349` |
| `src/Domain/packages.lock.json` | 6 | R: 1–6; no separate finding; runtime limitations apply | `03eeadc5ef377c17f787ab65f41fb4c8a9c936bb7f7f4171111fdeec8a81cb46` |
| `src/Infrastructure/Caching/DiscoveryMemoryCaches.cs` | 43 | R: 1–43; no separate finding; runtime limitations apply | `b080e81c573911638aaf9aa1710909c0efbd609a6a544b4ee861fbfeec21b8c0` |
| `src/Infrastructure/Catalog/CatalogArtifactReader.cs` | 95 | R: 1–95; no separate finding; runtime limitations apply | `1310f00d475549260b69cdaf091cf9cc209330c3a918534317333f327e17703b` |
| `src/Infrastructure/Catalog/MusicBrainzCatalogLookupSource.cs` | 72 | R: 1–72; no separate finding; runtime limitations apply | `154175d5aa50fbba8feac61e5ca15eac37a9ce12f443e87efaefe52b81faf98a` |
| `src/Infrastructure/Catalog/PublicRecordingCatalog.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `a6c4faa0a7961c02d9e8bb9e7937abd166ca827582de855bc0ffd1ea1cdd4578` |
| `src/Infrastructure/DependencyInjection.cs` | 142 | R: 1–142; no separate finding; runtime limitations apply | `e30bd79bdb1d6a746bc86ba8ad22fb12af44fecfc0bf9518271a2e5d2b15c1a0` |
| `src/Infrastructure/Discovery/PersistentDiscoveryPreparation.cs` | 24 | R: 1–24; P14 | `c8902b2c8457587cb8617ad389f50a0204041bea4b266c0ea019a0aeb2044904` |
| `src/Infrastructure/Discovery/PublicEvidenceCodec.cs` | 75 | R: 1–75; P14 | `4d16f0c91c4d890ccd3e25728addd3ab68b9d333c6cfbe9cbc85bcd4e1b17325` |
| `src/Infrastructure/Discovery/SharedCatalogLookup.cs` | 36 | R: 1–36; no separate finding; runtime limitations apply | `906be3bb20c3609de504f511419a96cf326bdd0d48f32f15d2d00fab74a585e1` |
| `src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs` | 128 | R: 1–128; P02, P05, P14 | `04163775402d9b341d8e9c6f46a3d79f86b4a776c390ff943e7597bef7099559` |
| `src/Infrastructure/Discovery/SharedPublicEvidence.cs` | 73 | R: 1–73; no separate finding; runtime limitations apply | `2803be8d2a2e1c7e037379f768cd2b451a04c20f629e088403f1e9277444a927` |
| `src/Infrastructure/Discovery/SharedSeedPreparation.cs` | 76 | R: 1–76; no separate finding; runtime limitations apply | `440e7a92679ddb560e70e14d9861c02c920fedfa6ac9e973feb25b9522451646` |
| `src/Infrastructure/Email/DigestEmailRenderer.cs` | 105 | R: 1–105; no separate finding; runtime limitations apply | `e2e5d6ae3e83760d128739cdc1100f2240ba2b8bfefa245d7b6f2b3adcf07d32` |
| `src/Infrastructure/Email/LocalEmailArchive.cs` | 108 | R: 1–108; P13 | `91cc6354589fe51932ac26239bc50116e10975545b6ffaa815c58cf77e277594` |
| `src/Infrastructure/Email/LocalEmailLegacyConversion.cs` | 134 | R: 1–134; no separate finding; runtime limitations apply | `fb7e7ee0fa9a62e2f74a2f528f681df4422515fe258de599135fec58edd166c7` |
| `src/Infrastructure/Email/LocalEmailMessageSerializer.cs` | 182 | R: 1–182; no separate finding; runtime limitations apply | `d101bde541fe1f2d028ef401f2c5e83dda3c6519fdfc3b7b68e4797943a85d5e` |
| `src/Infrastructure/Email/LocalEmailOptions.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `0720d41c5f89c93b0c1f26cae5115c70f556db7fad756164d5b7a64e73fb1833` |
| `src/Infrastructure/Email/LocalEmailPaths.cs` | 92 | R: 1–92; no separate finding; runtime limitations apply | `a4047a78dafcca0f0aa74eea1fff2bc9af5c9fd4083e38253c0dbe8315782350` |
| `src/Infrastructure/Email/LocalEmailSink.cs` | 112 | R: 1–112; no separate finding; runtime limitations apply | `f3e6ffc4320bd4c427847abcb45bd9404ef274af1536e9e164a4dc18afd8ff5f` |
| `src/Infrastructure/Email/UnsubscribeTokenService.cs` | 22 | R: 1–22; no separate finding; runtime limitations apply | `5a02a518eeb9695c36d400b389bf006021393d03c2b681d811f1646d1dea7b51` |
| `src/Infrastructure/Infrastructure.csproj` | 22 | R: 1–22; no separate finding; runtime limitations apply | `84c56f14c8e9355750cb825d299e39636314d0f98df402af2b237d2776b107bd` |
| `src/Infrastructure/RecommendationSources/LastFm/BatchTagCachingLastFmClient.cs` | 33 | R: 1–33; no separate finding; runtime limitations apply | `ca774d902fcc71b5b357fcbc9ce2316ed0301a25bdc680f1291cdde90b5c39d5` |
| `src/Infrastructure/RecommendationSources/LastFm/Fixtures/LastFmFixtureProvider.cs` | 279 | R: 1–279; no separate finding; runtime limitations apply | `f0aba8c3a28be148020f0f147d19476f1ac1c81b4eb1f5cf75ec716a30cfb100` |
| `src/Infrastructure/RecommendationSources/LastFm/ILastFmApiClient.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `c53260b2c0256efa07bcecb65b4a68bdca12d5356c8304a83724b27311535b9d` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmApiClient.cs` | 115 | R: 1–115; no separate finding; runtime limitations apply | `8e965c10283b0c024c7928c2ab6d547d767c8644cb3fc81091aa2ef5638e0e5e` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmCandidateHydrator.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `759132c796e727942875a62fe28e5cf680f470f3ef581fbb12d88d0f5bb7a86d` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmEvidenceParser.cs` | 129 | R: 1–129; no separate finding; runtime limitations apply | `0b06853d0bf4d518cfe156e5b9858900edac0182f869a4b75ddce6e61d60fece` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmOptions.cs` | 78 | R: 1–78; no separate finding; runtime limitations apply | `4a80fc397a76f3fd0b9d9351c36dc22e40a750a2e8293510f7f1c471b8201827` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmRateLimiter.cs` | 83 | R: 1–83; no separate finding; runtime limitations apply | `860df1cd6e2f1e4ca2a2cb11627d4227f811bbc522b5e8458c4da11aec59a3b4` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmRecommendationSource.cs` | 112 | R: 1–112; no separate finding; runtime limitations apply | `7d01f091735d43adccd335dbb083e4f73f540618806294d9c5ef643a3ef8a888` |
| `src/Infrastructure/RecommendationSources/LastFm/LastFmTagEvidence.cs` | 45 | R: 1–45; no separate finding; runtime limitations apply | `b68265b468c8a14b0f3d2c47cea208fd5e48640d3f503062e9037bf2de33034d` |
| `src/Infrastructure/RecommendationSources/LastFm/LocalRecordedInputLoader.cs` | 156 | R: 1–156; no separate finding; runtime limitations apply | `ad4cbcccc0d87dbaea6fff32d0b3a7ebe23cfa062c6ff019ee753c16e4758d33` |
| `src/Infrastructure/RecommendationSources/LastFm/Models/LastFmResponse.cs` | 20 | R: 1–20; no separate finding; runtime limitations apply | `89c9257dd08fd9877e4c7b8fb99d3e2d40c651cb090174a484bb1f8d1f4fc6a1` |
| `src/Infrastructure/RecommendationSources/LastFm/Models/LastFmResponses.cs` | 37 | R: 1–37; no separate finding; runtime limitations apply | `79a7a8c5ee58b46223bddba91a68cf3933497133000dcafd0545aba46e067356` |
| `src/Infrastructure/RecommendationSources/LastFm/RecordedLastFmApiClient.cs` | 67 | R: 1–67; no separate finding; runtime limitations apply | `938c7154486374ec598d021df5a30b2d44be5f8782d938035d51d1eae40b9e07` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/FixtureListenBrainzApiClient.cs` | 96 | R: 1–96; no separate finding; runtime limitations apply | `06488ed912623342ea8fb3fc7b47bfc0e03ad0d657543036ea6abea73232a9c4` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzApiClient.cs` | 96 | R: 1–96; P02, P03 | `bffd2aa68433fdd72c5b05c271db0b3e799ad402577b79bbb0b3847192b4c34f` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzCanonicalSeeds.cs` | 52 | R: 1–52; no separate finding; runtime limitations apply | `847c20fc0579990602df63954a385c2b149f9123cf2bfeefef6dcbff4ddddfa8` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzContracts.cs` | 122 | R: 1–122; P03 | `6d51d245f4e8a466d7f1373a4df241156a0c69ed72e92b2dd4fe8b42b15277a4` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzEvidenceParser.cs` | 117 | R: 1–117; no separate finding; runtime limitations apply | `12e922c1970ab49ee0f70df4509c70583580ab5a8ff6228b857e7b39dba0bf56` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzPublicCatalog.cs` | 53 | R: 1–53; no separate finding; runtime limitations apply | `606956863c866f47a2da6b6313af24fa6518de5535b2b6a04a8f602b3b3a646a` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecommendationProvider.cs` | 157 | R: 1–157; P16 | `2063ea79777b50bd337d93c45899bc835bfed1fe8e697a0e8d7ffff1a72fd1c7` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecordingSeeds.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `605c394ece10462ff9b13677effa41f979706c41425002affdb1064458870bc4` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/MetaBrainzRequestGate.cs` | 61 | R: 1–61; P02 | `bfcdb4364c6c9dbff733de27d052add69b6c80a1708665a30f79d3ef19bf27db` |
| `src/Infrastructure/RecommendationSources/ListenBrainz/RecordedListenBrainzApiClient.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `b8ac89c9f79bdaa311d7b910739a029451f9b73f496bae58200a37f6c3aa71ed` |
| `src/Infrastructure/RecommendationSources/RecommendationProviderResolver.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `a69737c918f5ecc36b9e4e4c2e30a8267483a56195836f855c0592af975bd540` |
| `src/Infrastructure/Storage/CoordinatedGenerationStorage.cs` | 136 | R: 1–136; P01 | `dbc4eb13040d613c6b42f9ab2db647f9f4b19d908cdd8b4ca3c93616e82563a9` |
| `src/Infrastructure/Storage/LocalGenerationStorage.cs` | 265 | R: 1–265; P01 | `d12c00eb9d23a94f277a390cb2d1c0c3ee974e918dc8ad03eeb255463a0e5a07` |
| `src/Infrastructure/packages.lock.json` | 93 | L: 1–93; no separate finding; runtime limitations apply | `bcd921812cf5ef9b4857e8ccad97cee42df58aeccdd8abccb94e7eba05e14d19` |
| `src/Presentation/Background/DiscoveryInventoryService.cs` | 45 | R: 1–45; no separate finding; runtime limitations apply | `898c4ce132e96a7d669467f9f6fe2e8f7678823c1f14e081ce717023f9e89f79` |
| `src/Presentation/Background/DiscoveryJobService.cs` | 115 | R: 1–115; P01, P07 | `ec92cc9ad153a8feacd7078acf9b2b3928003ea68ab05fd256537dff6bd3e2c0` |
| `src/Presentation/Controllers/ApiControllerBase.cs` | 23 | R: 1–23; no separate finding; runtime limitations apply | `ee66037e8389f951ceef584af8527f4b7f73f311e269792d757516d5bbde7ef9` |
| `src/Presentation/Controllers/AuthController.cs` | 270 | R: 1–270; P15 | `ba110eb178afc72fc97bb2adefc8541e5f459526e62e6b7db1b9a0485b36641e` |
| `src/Presentation/Controllers/DigestsController.cs` | 79 | R: 1–79; no separate finding; runtime limitations apply | `e1c2962180a9886e3a48678af6efbdc14cde578ba63830de33b5c8210641a4c3` |
| `src/Presentation/Controllers/ExportController.cs` | 55 | R: 1–55; no separate finding; runtime limitations apply | `188aa5cac5f42abaa3271d3bd75b22a49d115095cc49f6437a29a8c5911ab3d7` |
| `src/Presentation/Controllers/SubscribersController.cs` | 100 | R: 1–100; no separate finding; runtime limitations apply | `b7cb4c71eb48b562eb487acd5fe3d9472978705cfa932b2492968eb26840e1c8` |
| `src/Presentation/Controllers/TasteController.cs` | 52 | R: 1–52; no separate finding; runtime limitations apply | `7aa6020511c264c3abe51690a7e4443f03eaeff488b8c5113aba52e9ab199465` |
| `src/Presentation/Controllers/UnsubscribeController.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `d7710781fac2b0ec90e20510cbdc301aa0a8cc38db16a7bbdea2bf8761dd9ae5` |
| `src/Presentation/DependencyInjection.cs` | 168 | R: 1–168; P17 | `6d8db0a46cc10247fe446bc49fae3b7f84a639d052803d641ce9347dc60178a8` |
| `src/Presentation/Development/ManualTestEndpoints.cs` | 106 | R: 1–106; no separate finding; runtime limitations apply | `fd25eda9b1873c0e6389cd34cb4041bf1dcdc322dd57a553cbe6f4a996675088` |
| `src/Presentation/Discovery/CatalogController.cs` | 42 | R: 1–42; no separate finding; runtime limitations apply | `969c6bc49ac3aa3ca6f3936de72d20c1c8f6f87c19ed398343a70039acf2de17` |
| `src/Presentation/Discovery/DiscoveryController.cs` | 76 | R: 1–76; no separate finding; runtime limitations apply | `46e92938ee521221202793039a0620793c394159170f2a8a2c214de2fa7798e3` |
| `src/Presentation/Discovery/GuestController.cs` | 96 | R: 1–96; no separate finding; runtime limitations apply | `30e7d3e5a0089b34f67acec12f61b30f4ebef98904899981e77b7360c76e1904` |
| `src/Presentation/Discovery/ListenerContext.cs` | 100 | R: 1–100; no separate finding; runtime limitations apply | `d3d718aba6b59c7ec2625f7818602082a8f14628ba4ece60df0112f43a11577e` |
| `src/Presentation/Discovery/RecentRecommendationsController.cs` | 19 | R: 1–19; no separate finding; runtime limitations apply | `5d3947622502ad9c88a207f583048dcb775e2480fcd3291be21c5d6a17b7950a` |
| `src/Presentation/Discovery/TasteOnboardingController.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `fcf515a12719797323c46dd6b0775e0fa8813ce18ab2f9508416f550179a4149` |
| `src/Presentation/Filters/ApiExceptionFilterAttribute.cs` | 198 | R: 1–198; no separate finding; runtime limitations apply | `1bd26a26e8dc269710a1794fcffaf76e2269b5724ca5c2d3368172b18db4da37` |
| `src/Presentation/Filters/DiscoveryWriteFilter.cs` | 29 | R: 1–29; P15 | `16c5c14c0e26239e937e133f6d24601597515013253d9627640c7ae436f390e4` |
| `src/Presentation/Models/AuthModels.cs` | 32 | R: 1–32; no separate finding; runtime limitations apply | `465f07a78c7c72fe76481f02276fc7629e1c8cf87aa903172d46e7fa244665ab` |
| `src/Presentation/Options/JwtOptions.cs` | 15 | R: 1–15; no separate finding; runtime limitations apply | `7cb8ca613b075260bf172598719d882e8cf0d0afc4af6cbe817e026a06fa9a93` |
| `src/Presentation/Presentation.csproj` | 26 | R: 1–26; no separate finding; runtime limitations apply | `2e83e8d1040c85e8848df722ee5998a537555c6e405a5d4cf23cfcc08443a1a0` |
| `src/Presentation/Program.cs` | 173 | R: 1–173; no separate finding; runtime limitations apply | `b9a23d47c2f36dd23f358d6ffba146c8409bae8f76b1ea75403dc8ca4a5de995` |
| `src/Presentation/Properties/launchSettings.json` | 37 | R: 1–37; no separate finding; runtime limitations apply | `7ac25744e0f8481d564ae0bc2cb3256a4666344730de07317fcdb208157758f5` |
| `src/Presentation/Services/AuthService.cs` | 212 | R: 1–212; P15 | `a1fa497ebafab77f71de16595dc21b585db62dd7e02c45ba6ead6ef61feb449b` |
| `src/Presentation/Services/CurrentUserService.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `16dde0a6328388146890e25b8c7e8ca1be87e1fafd67a9193c411f7afd7452db` |
| `src/Presentation/Services/JwtService.cs` | 105 | R: 1–105; no separate finding; runtime limitations apply | `0079aa565c4f55306ac591aea2299605926cd5d58f654f7a71763f3b3ced9065` |
| `src/Presentation/appsettings.Development.json` | 10 | R: 1–10; no separate finding; runtime limitations apply | `0c0cdcebab7e552a5cf1bd392a47756f42857ed39f7adb0e63461ad9647e693d` |
| `src/Presentation/appsettings.json` | 33 | R: 1–33; no separate finding; runtime limitations apply | `2fd16c99307467397a6856253e9978042762ec0e5f45cd90b3d8380adf1c389d` |
| `src/Presentation/packages.lock.json` | 524 | L: 1–524; no separate finding; runtime limitations apply | `f9f2d3dda7e0396c53caf4fbc7756940e9329c894bff7e7cdce5ab4c9aac9d71` |
| `src/Presentation/wwwroot/app.js` | 617 | R: 1–617; no separate finding; runtime limitations apply | `17fdad6831781457134f32543905e5ea3a72807c5ad68456094ea7a5ce78bfb5` |
| `src/Presentation/wwwroot/discovery.js` | 521 | R: 1–521; P04 | `923aa530b64562d9fbd284968ce5226de25968fd57316022044cc19eea3179cd` |
| `src/Presentation/wwwroot/index.html` | 179 | R: 1–179; no separate finding; runtime limitations apply | `f1a941b9a09fff6781df20331f184502429f1c7e6122f2d05dd185ed3cd712be` |
| `src/Presentation/wwwroot/manual-test/index.html` | 218 | R: 1–218; no separate finding; runtime limitations apply | `a042d549ebc96f898d457dca222970d6f99f93264214a764559634630822fe33` |
| `src/Presentation/wwwroot/manual-test/manual-test.css` | 229 | R: 1–229; no separate finding; runtime limitations apply | `345f3174aee636e254bf85b63b159d02ed01ecb75408320a32588f37ec01bc47` |
| `src/Presentation/wwwroot/manual-test/manual-test.js` | 574 | R: 1–574; no separate finding; runtime limitations apply | `b5dd7ddba2ed9b29a7f832517e34c93c7bb4b8f01d45299ce4f897ff5d9dbd90` |
| `src/Presentation/wwwroot/styles.css` | 1021 | R: 1–1021; no separate finding; runtime limitations apply | `e5fd74f1765948fdd4de5c5706606e242e32f3779373a96e85e8418cde4c9525` |
| `src/Worker/LocalDigestWorkerRunner.cs` | 54 | R: 1–54; no separate finding; runtime limitations apply | `c59881ab858d168065a00d75bf388ecaa82329f63d8c5d092449087daea2af5b` |
| `src/Worker/LocalScheduleOptions.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `752e2524821c03a5ba71b3e1e20d5751f90dd7ab204b304d48d96c30eeb9cd27` |
| `src/Worker/LocalScheduledWorker.cs` | 45 | R: 1–45; no separate finding; runtime limitations apply | `8d21ecb2980f7aa6ce2cfe9f7ffb75cad9e579d7f83bc76ee7f0f76eb30da867` |
| `src/Worker/LocalWorkerOptions.cs` | 23 | R: 1–23; no separate finding; runtime limitations apply | `ba99ff9285a16ceb138940b67e7b3e8de194c511ba71a3d9a304fb0da3934924` |
| `src/Worker/Program.cs` | 55 | R: 1–55; no separate finding; runtime limitations apply | `6dfb7c9e738968187eb5aabbbc75a102a1c12c622038d699294f4cb2cdd6f66a` |
| `src/Worker/Worker.csproj` | 20 | R: 1–20; no separate finding; runtime limitations apply | `c45c72e3cef8b1f0ff57208ad3e557b969fbe065ae20efbf97e1bc933c923975` |
| `src/Worker/appsettings.Development.json` | 1 | R: 1–1; no separate finding; runtime limitations apply | `ca3d163bab055381827226140568f3bef7eaac187cebd76878e0b63e9e442356` |
| `src/Worker/packages.lock.json` | 516 | L: 1–516; no separate finding; runtime limitations apply | `bdecae993d033c14c1fa3d5702dbe8ad0335c4b318064dad8d9e5206898c926c` |
| `tests/Application.Tests/Application.Tests.csproj` | 26 | R: 1–26; no separate finding; runtime limitations apply | `4e2bf85e488a3b45314b821de75cb74ed9a362e8b0a05ebe06bb94d9a81a72f6` |
| `tests/Application.Tests/Common/ApplicationBoundaryTests.cs` | 21 | R: 1–21; no separate finding; runtime limitations apply | `9fa5acca17b48c42a3f11ba302d219fb04e89f4eab4291d91b483898ae36583e` |
| `tests/Application.Tests/Common/Behaviors/ValidationBehaviorTests.cs` | 82 | R: 1–82; no separate finding; runtime limitations apply | `8e977cae84e162b5f81dc05e41e0897427c7f783011b6017253c09176424ae20` |
| `tests/Application.Tests/Common/Behaviors/ValidationCancellationTests.cs` | 34 | R: 1–34; no separate finding; runtime limitations apply | `b3cf508b0e397fc43d48df212a46c41094ee872aed469769130ebe3f36d9dde1` |
| `tests/Application.Tests/Common/EvaluationMetrics.cs` | 145 | R: 1–145; no separate finding; runtime limitations apply | `3602abed1fb994e45c5f6ed7d18dd2a5a0242fbf56da542eb11e0afc4ee4ce10` |
| `tests/Application.Tests/Common/EvaluationMetricsTests.cs` | 121 | R: 1–121; no separate finding; runtime limitations apply | `efa9729be00f700421f6c723169eb0b8de677aebd7cd98dbc27f163dfcd6d250` |
| `tests/Application.Tests/Common/Exceptions/ExceptionTests.cs` | 51 | R: 1–51; no separate finding; runtime limitations apply | `61a9dc5c8d9990ca3bef93eaa74413eee622241495f15455aef8cb54bdcec475` |
| `tests/Application.Tests/Common/FixtureEvaluationHarness.cs` | 167 | R: 1–167; no separate finding; runtime limitations apply | `a8c2c841b63d86d9b240da0967d3144ecd91b2695bc1d836a26ac62459d19081` |
| `tests/Application.Tests/Common/FixtureEvaluationHarnessTests.cs` | 125 | R: 1–125; no separate finding; runtime limitations apply | `08cb5ce75430c20c42def2752fe0ba828ff0ea51194a6ac4095af1a3102ecdf9` |
| `tests/Application.Tests/Common/Mappings/MappingTests.cs` | 111 | R: 1–111; no separate finding; runtime limitations apply | `72ea3e34517af20e5c6925065464076cdd0e2cec8b7a41a8a63662d5918888a0` |
| `tests/Application.Tests/Common/Phase2ProfileMembership.cs` | 50 | R: 1–50; no separate finding; runtime limitations apply | `b732dae087eb07f445083906a61cf2db23e50110bd1850f152bd2f909b24fec0` |
| `tests/Application.Tests/Common/PilotEvaluation.cs` | 140 | R: 1–140; no separate finding; runtime limitations apply | `cbe770703fda8aa4298d29f4344f70413a01c75d6a2b2f24664bb5aee52d590e` |
| `tests/Application.Tests/Common/PilotScoring.cs` | 145 | R: 1–145; no separate finding; runtime limitations apply | `9fe8cede4a1df15d87c9df2a607727e743c7e1d0252191544bfdb238111a9524` |
| `tests/Application.Tests/Common/PilotScoringTests.cs` | 157 | R: 1–157; no separate finding; runtime limitations apply | `d799f098cb3202aa668abd7674951567288582c20eb32677e3d1b3595a3ef577` |
| `tests/Application.Tests/Common/ProfileLockTests.cs` | 85 | R: 1–85; no separate finding; runtime limitations apply | `d12a0feb564aad5e9b158657c9e56ed80aa66b6cf7df47e737f9a563a5eadc15` |
| `tests/Application.Tests/Features/Digests/DigestAndFeedbackTests.cs` | 238 | R: 1–238; no separate finding; runtime limitations apply | `2201a082edaef8331d90434b9854ca5da7ea8521c21469492fc8ea137acafeca` |
| `tests/Application.Tests/Features/Digests/GenerateDigestTests.cs` | 314 | R: 1–314; no separate finding; runtime limitations apply | `a5761eca943892a4b513df7165196a63b071b01e111c8ed17d525ee9c14757df` |
| `tests/Application.Tests/Features/Discovery/DiscoveryRuntimeOptionsTests.cs` | 21 | R: 1–21; no separate finding; runtime limitations apply | `88c20e1712288f6c008ed754bed0a43a41230be8955eebed32dc5470985e1f01` |
| `tests/Application.Tests/Features/Discovery/DiscoveryTasteTests.cs` | 153 | R: 1–153; no separate finding; runtime limitations apply | `74f0352d34eddde2cb2dede7589975271d387a62c6cda5248534f97df8a2c099` |
| `tests/Application.Tests/Features/Discovery/GuestActivityTests.cs` | 58 | R: 1–58; no separate finding; runtime limitations apply | `230c0ad5ee6dd65f8ad056fe167ebe171b2ec37809587fea96aec99a03909a60` |
| `tests/Application.Tests/Features/Discovery/PrepareDiscoveryBatchTests.cs` | 86 | R: 1–86; no separate finding; runtime limitations apply | `5cbebab27fba38acf35ab00ac6439ad61639a346149228de8f1660921eef21bd` |
| `tests/Application.Tests/Features/Discovery/TasteVocabularyTests.cs` | 31 | R: 1–31; no separate finding; runtime limitations apply | `efa16ce67b0f78add8457da1d52b632f5d365f9bf3b4dcd6e54ec25cafedde3c` |
| `tests/Application.Tests/Features/Export/GetUserDataExportTests.cs` | 219 | R: 1–219; no separate finding; runtime limitations apply | `ff70ac501497cb91c341b85f01f7f5efe0f4a1995170eb34c57a064449c7636d` |
| `tests/Application.Tests/Features/RunScheduledTickTests.cs` | 117 | R: 1–117; no separate finding; runtime limitations apply | `2a914dc64bddec7a2aa44a35b13ef91f200d1617bfa9d34830c7724adc03f692` |
| `tests/Application.Tests/Features/Subscribers/RegisterSubscriberTests.cs` | 131 | R: 1–131; no separate finding; runtime limitations apply | `47d13f2162ce8db8d119af220cc55c0757eaefc9c257a6ae944b5493e66865e7` |
| `tests/Application.Tests/Features/Subscribers/SubscriberQueryAndCommandTests.cs` | 80 | R: 1–80; no separate finding; runtime limitations apply | `59b3e4bdb353c651838259767e04b295e50de8d50c0e2de797b5d3492a04356d` |
| `tests/Application.Tests/Features/Taste/GetUserTasteSignalsTests.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `fcde465998d83b39036e52da4dc6f8109d2958c4ded276076b331d0d54d8bcb7` |
| `tests/Application.Tests/Features/Taste/SeedTasteProfileTests.cs` | 161 | R: 1–161; no separate finding; runtime limitations apply | `f2b7e33659812f19f0f3c64071ed266683f3a973a130b0c8f9374b2c2e4157a3` |
| `tests/Application.Tests/packages.lock.json` | 251 | L: 1–251; no separate finding; runtime limitations apply | `292cfc2489baa7c7ee1e6a1b10d826a7a023c8f6df997a65762c70393fefc425` |
| `tests/DataAccess.Tests/AppDbContextModelTests.cs` | 136 | R: 1–136; no separate finding; runtime limitations apply | `a8afa814cf60abb02ec3af59a101dff608e9446c47297e06f30fa5bf22e7426b` |
| `tests/DataAccess.Tests/ArchitectureTests.cs` | 31 | R: 1–31; no separate finding; runtime limitations apply | `e8c29359bba0caf56cc77fd717cacb90dd07c7c8e53343a0c2787b7cb9ad9719` |
| `tests/DataAccess.Tests/AsyncRepositoryTests.cs` | 133 | R: 1–133; no separate finding; runtime limitations apply | `01f69fed1b68a401aa4c4025994019d73d74505b7daa4df84073f56dc2458c42` |
| `tests/DataAccess.Tests/AuditTimestampTests.cs` | 68 | R: 1–68; no separate finding; runtime limitations apply | `e410678983d228a5fe4e7d64901a8f066cc1dd4f0976671d04c1a7dfc831d03b` |
| `tests/DataAccess.Tests/CatalogPostgresTests.cs` | 287 | R: 1–287; no separate finding; runtime limitations apply | `660a55e71b1acaaec2e8b31c681f550e2c7bd4896755fe4ae9b4f4565d825ea5` |
| `tests/DataAccess.Tests/ComprehensiveRepositoryTests.cs` | 135 | R: 1–135; no separate finding; runtime limitations apply | `8c53b1b143f0895073764621c7a11b8fa97115fa3bc8624370f7dd9662a5493e` |
| `tests/DataAccess.Tests/DataAccess.Tests.csproj` | 28 | R: 1–28; no separate finding; runtime limitations apply | `bab99e528d036df6881f19418d8f29fc339dfefc7d4188e6da0aa6834e989524` |
| `tests/DataAccess.Tests/DataContextAuditAndSoftDeleteTests.cs` | 117 | R: 1–117; no separate finding; runtime limitations apply | `91eba2a265ab2738d434f9c61e5789b8198f8e330e83b0705bdbd95659558d1b` |
| `tests/DataAccess.Tests/DiscoveryCoordinationPostgresTests.cs` | 706 | R: 1–706; no separate finding; runtime limitations apply | `23e182d7bbe1c17d1fb99590a4b50b956f7cef2d811bd962000d812450949a7c` |
| `tests/DataAccess.Tests/DiscoveryMigrationTests.cs` | 383 | R: 1–383; no separate finding; runtime limitations apply | `800f73645e7f57a7c41d449be39ad2af71f2319a0d62096e2b039ac383dd5301` |
| `tests/DataAccess.Tests/LocalScheduleOptionsTests.cs` | 59 | R: 1–59; no separate finding; runtime limitations apply | `7ac46237ad4c0ba0ed943ae3be34550063b3e7abc8e4ed74d401ae8f80a208f5` |
| `tests/DataAccess.Tests/LocalScheduledWorkerTests.cs` | 57 | R: 1–57; no separate finding; runtime limitations apply | `d063c48b72ede9ef78ed673945a7d253621c99bf7811f59523b52b7e08de19ec` |
| `tests/DataAccess.Tests/LocalUnsubscribePostgresTests.cs` | 92 | R: 1–92; no separate finding; runtime limitations apply | `cee5ca6c68606ccbcd082f4d225e7ff284026d467d63aa4ae536440ca2252001` |
| `tests/DataAccess.Tests/LocalWorkerOptionsTests.cs` | 34 | R: 1–34; no separate finding; runtime limitations apply | `8b731dfeb40db43451b73e2825e37256aed7c2f0c7329f5bb468093b4566e0aa` |
| `tests/DataAccess.Tests/LocalWorkerPostgresTests.cs` | 245 | E: 1–245; no separate finding; runtime limitations apply | `e7acb582e5b724a2840b7b69908208081558f7d6b35f42f2da6d74f2d3cf9c65` |
| `tests/DataAccess.Tests/OptionalTasteOnboardingPostgresTests.cs` | 118 | E: 1–118; no separate finding; runtime limitations apply | `603f5b63092534dbd3081f06848e3d7b505346a87c8c40b70aedd97edd37f7c3` |
| `tests/DataAccess.Tests/Phase6PostgresTests.cs` | 435 | E: 1–435; no separate finding; runtime limitations apply | `c999256db85dcc12de56119cc48f17c6a3a8fb84f1b6d24d909f6ee929de8746` |
| `tests/DataAccess.Tests/Phase8PrivacyInventoryTests.cs` | 105 | E: 1–105; no separate finding; runtime limitations apply | `c1a027f709e0ffc415d002287bb8fb2f9a53da71c8adb9ef4b6734f0788c1835` |
| `tests/DataAccess.Tests/Phase8ScheduledWorkerPostgresTests.cs` | 588 | E: 1–588; no separate finding; runtime limitations apply | `e333f704eeb2dae06d51510a8f1389dd54fe0e98c665f5e4520d4b9cccb18794` |
| `tests/DataAccess.Tests/PopulatedLegacyMigrationTests.cs` | 89 | E: 1–89; no separate finding; runtime limitations apply | `c6607ca5acd603c0027892572d971fcfdfa78a1557dae195f94d869742faba5f` |
| `tests/DataAccess.Tests/PrReviewRegressionPostgresTests.cs` | 89 | E: 1–89; no separate finding; runtime limitations apply | `6e1f3a69ade310c62be407fca4a999ae9ad4a073413905eea79f46da3c25aa9f` |
| `tests/DataAccess.Tests/RecordingCatalogPostgresTests.cs` | 45 | E: 1–45; no separate finding; runtime limitations apply | `b0a7ae21ab1aed5ac56a251cdd7bb101eb5d2cc65a5e2c2908291a3df9763594` |
| `tests/DataAccess.Tests/RepositoryTests.cs` | 120 | E: 1–120; no separate finding; runtime limitations apply | `cbcff325523788df79679bfeb1f87d75761da5de060b924121e4793f63605cde` |
| `tests/DataAccess.Tests/UnitOfWorkTests.cs` | 33 | E: 1–33; no separate finding; runtime limitations apply | `62a3afb87550b9643c8d1b2353b44b47098ccda97e76af22d62c83c88099203f` |
| `tests/DataAccess.Tests/packages.lock.json` | 622 | L: 1–622; no separate finding; runtime limitations apply | `199caf7b1ddddfec996e996e7998d05a1e64adcfa4378ac69e73b87ae35a2284` |
| `tests/Domain.Tests/Architecture/OnionArchitectureTests.cs` | 33 | R: 1–33; no separate finding; runtime limitations apply | `d0a6fc65a373385c0b80bbbbb8b6c1c6222ef50e2e36c2cd95ebf9bb08fab5bf` |
| `tests/Domain.Tests/Catalog/CatalogTextTests.cs` | 29 | R: 1–29; no separate finding; runtime limitations apply | `e3a293ddc3900fbaaf519f8ede66372851c937a387efc21b27b6cea60b870154` |
| `tests/Domain.Tests/Catalog/TrackAndArtistTests.cs` | 86 | R: 1–86; no separate finding; runtime limitations apply | `8bd9a02998645baef01fc25c343c958abf8f59508f8d3859dc70b7f6a40f3f3a` |
| `tests/Domain.Tests/Catalog/WeightedTagVectorTests.cs` | 117 | R: 1–117; no separate finding; runtime limitations apply | `fa17a706d964686365661090b5ac074e6b10f0afc15d01a555561cc860b26d19` |
| `tests/Domain.Tests/Digest/IsoWeekExtendedTests.cs` | 69 | R: 1–69; no separate finding; runtime limitations apply | `c2e9ab296086dbe0fe9b581eb047d457b94cca68cf25d8ad2acd09affcad1730` |
| `tests/Domain.Tests/Digest/LocalEmailLifecycleTests.cs` | 37 | R: 1–37; no separate finding; runtime limitations apply | `a8a31f75f4e338c00305c1e2478e07886707c86f4aa1cae0cbc730f4002601b3` |
| `tests/Domain.Tests/Digest/UserAndConnectionTests.cs` | 113 | R: 1–113; no separate finding; runtime limitations apply | `d2e145891e6be171737445cb5bfc121e7742b01e82d519163bccbd92c8273541` |
| `tests/Domain.Tests/Digest/WeeklyDigestTests.cs` | 113 | R: 1–113; no separate finding; runtime limitations apply | `3ab0ae9e1e8ed9ed550406a7d2f309a4414861e34dba0e0fb5a638f225fa6195` |
| `tests/Domain.Tests/Discovery/DiscoveryFoundationTests.cs` | 86 | R: 1–86; no separate finding; runtime limitations apply | `a7b682fe62d2cf1ce28a8568414ec116d6124c57189928eb3d861216d3de5f77` |
| `tests/Domain.Tests/Discovery/DiscoveryOwnershipTests.cs` | 49 | R: 1–49; no separate finding; runtime limitations apply | `62000ef128324f8c8df54b77aa7964c3ba5de91d40f0d075cb13c285ee308294` |
| `tests/Domain.Tests/Discovery/OptionalTastePolicyTests.cs` | 118 | R: 1–118; no separate finding; runtime limitations apply | `f57201f64b7429c06b1c4d37af95e6716e339cbc6b9748c5fcdd6c93a7471255` |
| `tests/Domain.Tests/DiscoveryJobLeaseTests.cs` | 54 | R: 1–54; no separate finding; runtime limitations apply | `ec44de9fdd456f1fefce3bb80fe456c983e64e2f9170b6bc2872f89d585329da` |
| `tests/Domain.Tests/DiscoveryTasteMaterializerTests.cs` | 70 | R: 1–70; no separate finding; runtime limitations apply | `32e8e27f9e626b1d30ea0f28651a4ee44704da5b1761c45f6cc5da79afe1a464` |
| `tests/Domain.Tests/Domain.Tests.csproj` | 23 | R: 1–23; no separate finding; runtime limitations apply | `ae865900393e51196c763686e0ff2e4fd924b46e9bb847071f6512aa1af4b58b` |
| `tests/Domain.Tests/Fixtures/BenchmarkSeedFixtures.cs` | 224 | R: 1–224; no separate finding; runtime limitations apply | `d106a6520fe6aa34d0c59db4b65e2628c0ca26c98065953d2a6dca82714edb54` |
| `tests/Domain.Tests/Recommendation/BaselineAScorerTests.cs` | 99 | R: 1–99; no separate finding; runtime limitations apply | `bea9b203877478bf63df9c471e739403205978db6a6f468ac01e6c6227689ca7` |
| `tests/Domain.Tests/Recommendation/CandidateTrackAndParamsTests.cs` | 66 | R: 1–66; no separate finding; runtime limitations apply | `97376c9abcafe7aff8c03bd571ba69ffcb06c42f7b9e4fd3f62f67e12938311d` |
| `tests/Domain.Tests/Recommendation/RecommendationScorerTests.cs` | 240 | R: 1–240; no separate finding; runtime limitations apply | `e87693e05db9afbf97052248510bbce3173c12bfddd95fb9f35e1bc05d26f08f` |
| `tests/Domain.Tests/Taste/TasteSignalTests.cs` | 45 | R: 1–45; no separate finding; runtime limitations apply | `d0437edf225b30e6d712e24e99d4b9f1ee8695fe5301d8fbc60b49ee32a6513d` |
| `tests/Domain.Tests/Taste/TasteVectorMaterializerTests.cs` | 59 | R: 1–59; no separate finding; runtime limitations apply | `0b411ffd8c98d85b083146f1e448d534a1ceb2db222bd420dc9f39dfaf0024ef` |
| `tests/Domain.Tests/Taste/UserTasteProfileExtendedTests.cs` | 68 | R: 1–68; no separate finding; runtime limitations apply | `be513f300cfc742db54c37c7173e0f07d86d56a4b8a925d3f3704aae81e7bfd5` |
| `tests/Domain.Tests/WeeklySchedulePolicyTests.cs` | 44 | R: 1–44; no separate finding; runtime limitations apply | `0b7a085f593bb90dbd3e4b345a12db18c05e98752582db293f927f0678912be1` |
| `tests/Domain.Tests/packages.lock.json` | 96 | L: 1–96; no separate finding; runtime limitations apply | `750ea787eae2018a001eee8360a22ab00e2ae9ab4237b9069db60596d54c8998` |
| `tests/Infrastructure.Tests/Caching/DiscoveryMemoryCacheTests.cs` | 35 | E: 1–35; no separate finding; runtime limitations apply | `b88c028938fba99bf301634504ac12b497678802ffe5865ee0397607922e9b59` |
| `tests/Infrastructure.Tests/Caching/PersistentDiscoveryPreparationTests.cs` | 168 | E: 1–168; no separate finding; runtime limitations apply | `ba5192028ecb41ec283376e9a4994c3de460339df83f472d89b767f4408080dc` |
| `tests/Infrastructure.Tests/Caching/PublicEvidenceCodecTests.cs` | 50 | E: 1–50; no separate finding; runtime limitations apply | `5558228bc9a703cf1fa96714995b6b71130ecbf25ab02b4ccd3094c0a2da8cf0` |
| `tests/Infrastructure.Tests/Caching/SharedDiscoveryPreparationTests.cs` | 135 | E: 1–135; no separate finding; runtime limitations apply | `a0e7133009be42d23263bbd98dbc0b80b7506e9a4ca2d5d51039bef512d3c43c` |
| `tests/Infrastructure.Tests/Catalog/CatalogArtifactTests.cs` | 101 | E: 1–101; no separate finding; runtime limitations apply | `ce568c6b1855d9b19d15bb43f98eafeb57c4ebfd7603e8d5d9c167fa1523add1` |
| `tests/Infrastructure.Tests/Fakes/EvidenceFixtures.cs` | 31 | E: 1–31; no separate finding; runtime limitations apply | `4830bdc4b13f6af1dbf6df4e78108152b110eda106c225e7239423052e326405` |
| `tests/Infrastructure.Tests/Fakes/MockHttpMessageHandler.cs` | 43 | E: 1–43; no separate finding; runtime limitations apply | `60aad55c2d03d52a9de43bbc3c38757e8729b1c2bb345316a571d5924c39e6b7` |
| `tests/Infrastructure.Tests/GenerationStorageTests.cs` | 141 | E: 1–141; no separate finding; runtime limitations apply | `19110cd1aa8e97386b2e3874469bbb97e61765281963aef7bfdee32ba3a8c898` |
| `tests/Infrastructure.Tests/Infrastructure.Tests.csproj` | 35 | E: 1–35; no separate finding; runtime limitations apply | `6c381b03879637fe9f81a8fc0f900d1d928cc4b875d8e0f4656b6544a0ab2584` |
| `tests/Infrastructure.Tests/ListenBrainzProviderTests.cs` | 178 | E: 1–178; no separate finding; runtime limitations apply | `02c26ce21a338de82ad28d221db4336604d608470e46d9b25d312e93e03f504e` |
| `tests/Infrastructure.Tests/ListenBrainzRecordedInputTests.cs` | 55 | E: 1–55; no separate finding; runtime limitations apply | `a7d9936b4cf697e642e14b43f19fadd53f338df250ebdafc998cf15781a28f01` |
| `tests/Infrastructure.Tests/ListenBrainzTransportTests.cs` | 224 | E: 1–224; no separate finding; runtime limitations apply | `f218a6709cbdf4b8fab51c15bc3625825ed71f80106493fa38fd7c3c51b262a3` |
| `tests/Infrastructure.Tests/LiveRecordingCatalogTests.cs` | 63 | E: 1–63; no separate finding; runtime limitations apply | `9d7ccba01a4001d4a02b05523f2cd72d1497fb34f0b0a6d45059f8d1c087d80a` |
| `tests/Infrastructure.Tests/LocalEmailTests.cs` | 429 | E: 1–429; no separate finding; runtime limitations apply | `aa1a82fab182f79651b5de438c7baaeffbe1dd78f81f218c2d6fae10427d2ed9` |
| `tests/Infrastructure.Tests/LocalRecordedInputTests.cs` | 60 | E: 1–60; no separate finding; runtime limitations apply | `d464b997e46fe5f3a204794ee9c0d3a20574d5be61abd0b6f744c81e6571a7b4` |
| `tests/Infrastructure.Tests/RecommendationProviderTests.cs` | 83 | E: 1–83; no separate finding; runtime limitations apply | `8d482455710f4f791e84920b4cf060c01a24de387d7e2caffae61786a45bda29` |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmApiClientTests.cs` | 221 | E: 1–221; no separate finding; runtime limitations apply | `7f5a0a7d6cea21f2bbf418b84452553b7288e381a39fc7082184ca8821474ddd` |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmCandidateHydratorTests.cs` | 78 | E: 1–78; no separate finding; runtime limitations apply | `c436c0a72bf2d105b08b6650f4e1e0154b794869c620c74fa89da5f2e8aef9b8` |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmRateLimiterTests.cs` | 51 | E: 1–51; no separate finding; runtime limitations apply | `5a78004eabdaa3de3ab215bca6f404813daf32805a65897fde76eaf62f1e2d15` |
| `tests/Infrastructure.Tests/RecommendationSources/LastFmRecommendationSourceTests.cs` | 97 | E: 1–97; no separate finding; runtime limitations apply | `807961e0f38ff84bfe15604d27c2382df268d8b7eef374567702d32ab9fd3cdc` |
| `tests/Infrastructure.Tests/RecommendationSources/Phase3EvidenceTests.cs` | 345 | E: 1–345; no separate finding; runtime limitations apply | `059c74b53648a4433d67f37b331e1ab323294f94f266fa0fd0eee4b11c33c09b` |
| `tests/Infrastructure.Tests/RecommendationSources/Phase3IngestionTests.cs` | 71 | E: 1–71; no separate finding; runtime limitations apply | `7e88218535d5aa368e3e3d5afd69c94cca0245d64112eadedf51c779b8226007` |
| `tests/Infrastructure.Tests/RecommendationSources/PilotReplay.cs` | 167 | E: 1–167; no separate finding; runtime limitations apply | `fedacb788312271b54ab1a0d56efb30dcd7db44315d8f16fdd06b0299c6c9ef8` |
| `tests/Infrastructure.Tests/RecommendationSources/PilotReplayTests.cs` | 164 | E: 1–164; no separate finding; runtime limitations apply | `7999a0607f29042fc0bb0090e2b0199aa66713a1358dda8eda47b9bef1a5ea19` |
| `tests/Infrastructure.Tests/RecommendationSources/RecordingDiscoveryTests.cs` | 90 | E: 1–90; P16 | `ce811514796b4e29aa08bd62559fbece57c55f93f61edbd91b3206fb500109ce` |
| `tests/Infrastructure.Tests/Spikes/FounderSeedCoverageSpikeTests.cs` | 45 | E: 1–45; no separate finding; runtime limitations apply | `484c4b189832f2186858c1db949cdfc25f969185f3b6242d2051f5262cd73cb1` |
| `tests/Infrastructure.Tests/Storage/CoordinatedStorageTests.cs` | 110 | E: 1–110; no separate finding; runtime limitations apply | `b838aabe2015cfc2961da13fd3523c834e6ab21333688d35e1d477e4c6f0ae4f` |
| `tests/Infrastructure.Tests/packages.lock.json` | 345 | L: 1–345; no separate finding; runtime limitations apply | `d7b23101046d74c9c75fd56e15806642a52fbca44013290f8e095d45993be733` |
| `tests/Presentation.Tests/ApiExceptionFilterAttributeTests.cs` | 121 | E: 1–121; no separate finding; runtime limitations apply | `93abdd1ba8fcf4a9b27acdd90b9d36b9a1da2e207745f3f73ae03905ebad46e2` |
| `tests/Presentation.Tests/Controllers/AuthControllerTests.cs` | 247 | E: 1–247; no separate finding; runtime limitations apply | `f64764142aaf958658590713e73d1eef7c91373a4df035e32a9eb514858583e6` |
| `tests/Presentation.Tests/Controllers/DigestsControllerTests.cs` | 111 | E: 1–111; no separate finding; runtime limitations apply | `67012b92c74c51ba13365dea4013090ea22835d1e9dc5831092229a4c2e7b494` |
| `tests/Presentation.Tests/Controllers/ExportControllerTests.cs` | 108 | E: 1–108; no separate finding; runtime limitations apply | `8d6dfa96f154c8e34757349dc2bb24f25b2e3887043890e0d0167eb862c25ee2` |
| `tests/Presentation.Tests/Controllers/SubscribersControllerTests.cs` | 132 | E: 1–132; no separate finding; runtime limitations apply | `fd7c3aa1e7b3f78753d33258a55d67106517450ae3a860435baad27e4ad88c7f` |
| `tests/Presentation.Tests/Controllers/TasteControllerTests.cs` | 75 | E: 1–75; no separate finding; runtime limitations apply | `7124e917fbab8e694c1cb88d57adaa116d13feb20e774022efa8e72f74a410e7` |
| `tests/Presentation.Tests/CurrentUserServiceTests.cs` | 72 | E: 1–72; no separate finding; runtime limitations apply | `5105fef2b62490157e49d88976188f88c42494a92de05c0a93b052406f42bb53` |
| `tests/Presentation.Tests/DiscoveryInventoryServiceTests.cs` | 58 | E: 1–58; no separate finding; runtime limitations apply | `ccf26e06598205a189838b687ec31841a51780a80b56e0879b02f0196852068c` |
| `tests/Presentation.Tests/DiscoveryJobServiceTests.cs` | 123 | E: 1–123; no separate finding; runtime limitations apply | `fb3499b0e288af9d564c53be4e3cb53b056c0ac6a50cda0ee7eb360e225ea419` |
| `tests/Presentation.Tests/DiscoveryWriteFilterTests.cs` | 78 | E: 1–78; no separate finding; runtime limitations apply | `16eed5f9ad9731223afa60781f2962bd791d56825d7b45a7ebfdd0a3015f21eb` |
| `tests/Presentation.Tests/GuestDiscoveryApiTests.cs` | 223 | E: 1–223; no separate finding; runtime limitations apply | `04ad341fa991156a03d36a1fb4a134c071b121b34f2e1030cd85b8e209d3189d` |
| `tests/Presentation.Tests/JwtServiceTests.cs` | 82 | E: 1–82; no separate finding; runtime limitations apply | `aa67dc37561a89c663ccd74ea2a6cd1810701a0addb7e33cd2575ab416ad7a32` |
| `tests/Presentation.Tests/ListenerContextTests.cs` | 96 | E: 1–96; no separate finding; runtime limitations apply | `0c94b14f3c7f24b15f2dcc43a6ec214315e5d078a9e582f4f3bbd0ffb8fd38dd` |
| `tests/Presentation.Tests/ManualTestEnvironmentTests.cs` | 123 | E: 1–123; no separate finding; runtime limitations apply | `188aa0f569d02aa2f76057d63ba32c554e25692c96822cb648b61e7c56c52dc9` |
| `tests/Presentation.Tests/Phase1SecurityTests.cs` | 358 | E: 1–358; no separate finding; runtime limitations apply | `62c665b35d4e6ff971d26644947466cbce6fd7e335e77870f50e476ff32ecfac` |
| `tests/Presentation.Tests/Phase8PrivacyApiTests.cs` | 492 | E: 1–492; no separate finding; runtime limitations apply | `d39c09e19ae00710756eb7f3b4fa55cf133c2fa5ed280f84eea2cee28aad9d68` |
| `tests/Presentation.Tests/Phase9SwaggerTests.cs` | 55 | E: 1–55; no separate finding; runtime limitations apply | `fa9e3d45329d08839db7342b97a13fca48c84b2f30bb55f0bd59172699c12bb8` |
| `tests/Presentation.Tests/Presentation.Tests.csproj` | 28 | E: 1–28; no separate finding; runtime limitations apply | `2c1131bfebed09b81b65fdf376953f151be03f45a7fedcebab4ffec0cad4e605` |
| `tests/Presentation.Tests/UnsubscribeRouteTests.cs` | 98 | E: 1–98; no separate finding; runtime limitations apply | `41f31b37f6dd39aeec78b40f0975b3aee8c3966cb592bde9f4fb0cecae870dfa` |
| `tests/Presentation.Tests/packages.lock.json` | 877 | L: 1–877; no separate finding; runtime limitations apply | `c639e72440e47f5ddb7cbdf5f5893608d4279849181003397a00afd2f8765f55` |
<!-- END SOURCE SNAPSHOT: Performance_Network_Audit.md -->

---

<a id="source-plan"></a>

## Appendix A02 — Approved sequential plan and joint P01/P19/P20 design

Original: [docs/renewal/performance-remediation-plan.md](docs/renewal/performance-remediation-plan.md). Full source text. Original source: 60,481 UTF-8 bytes, 307 lines.

Source SHA-256: `45a5c8914edb933ac88e1a1c9d9143234f805c1d70811799062657fc59861831`.

<!-- BEGIN SOURCE SNAPSHOT: docs/renewal/performance-remediation-plan.md -->

<a id="source-plan--performance-remediation-design-and-implementation-plan"></a>

### Performance remediation design and implementation plan

Date: 2026-10-25. Review baseline: `fb2b4d7`.
Status: **Design and sequential implementation approved by the developer on 2026-10-25, including D1–D4 and the private plaintext staging fallback.** See [implementation evidence](#source-evidence) for completed stages; preparation measurements below remain historical baselines.

This document covers P01–P20 and T01 from [the audit](#source-audit). Implementation is sequential, with focused local commits after relevant verification. Approval must explicitly settle the security/privacy choices in the decision table below before dependent behavior changes. A failure to meet an acceptance criterion is reported, not relabelled as a successful optimization. Developer confirmation and deployment acceptance remain separate from local verification.


<a id="source-plan--preparation-and-current-evidence"></a>

#### Preparation and current evidence

Read the repository instructions, MEMORY and PROGRESS, the complete audit including its historical sections and coverage ledger, the V1 structure/infrastructure specifications, renewal contracts, approved guest-discovery and optional-onboarding designs, storage contract, privacy inventory and scheduling contracts. Newer approved contracts supersede the older five-layer, soft-deletion, import and numeric-provider-limit examples. The active frontend is `wwwroot/*.js` in this checkout.

Inspected the current storage coordinator, ledger migration/integrity verifier, weekly generation/capture/scheduling paths, account deletion, authentication, export readers/archive/UI, scorer, persistent pool codec/cache, metadata transport/hydration and their regression tests. Inspected central package pins and tool manifests. No dependency additions, removals or updates are needed for the proposed initial approach; MediatR 12.5.0 remains the licensing exception. Verify releases/compatibility before any later proposed dependency change.

The worktree was clean at preparation. Historical raw evidence is absent from this worktree but available under `/home/mahammadjafarli/source/repos/Liner Notes/diagnostics/results/audit-20261025/`. All **150 manifest-listed files, 54,112,129 bytes, matched their recorded lengths and SHA-256 hashes** during this review. The manifest names source checkpoint `f5c4c15`. This verifies artifact preservation, not a fresh rerun of the measurements. Older `/tmp` evidence is not a durable baseline; use the preserved continuation harnesses and regenerate comparable controls before implementation.

The installed SDK works through `/home/mahammadjafarli/.dotnet`; the default `/usr/bin/dotnet` does not resolve pinned SDK 10.0.401. With the private SDK first in PATH, the diagnostics health check passes. Trace/counters/dump/gcdump are 10.0.750501; ReportGenerator is 5.5.11. `dotnet-monitor`, ShellCheck and a repository BenchmarkDotNet project are unavailable. Coverlet 10.1.0 is pinned in the test setup. Their absence does not block measured component comparisons. No profiler was attached and no dump was collected in this review.

Codex-Mem was consulted for prior decisions; the audit's current approved directions match the current request. Relevant skills used: task-observer, brainstorming, performance-reviewer and ponytail. MCP tool discovery confirms Rider analysis and browser tools are exposed, but does not establish licensed analysis or a working browser session. Prefer the proven free CLI path; verify browser launch again when needed. No live-provider request was made.


<a id="source-plan--disposition-of-every-finding"></a>

#### Disposition of every finding

“Present” below means verified in current source and supported by the cited audit evidence. Historical passing tests are not claimed as newly executed.

| Finding | Current disposition | Planned resolution |
| --- | --- | --- |
| P01 | Open: five whole-artifact passes per admitted write | Joint design G1; strict behavior retained until its replacement contract is approved and verified |
| P02 | Partial: post-pacing cache recheck present; overlap/process boundaries remain | G6 overlap controls; bounded coalescing only if normal reachable overlap justifies it |
| P03 | Partial: response `ToArray` copy removed; bounded buffer and owning JSON remain | G6 measured buffer/ownership optimization; retain necessary bounded ownership if no justified saving |
| P04 | Implemented, historical regression verification | Preserve Retry-After parsing, timer-safe long waits and cancellation; rerun Node/browser regressions |
| P05 | Measured stripe collisions; no normal-lane bottleneck established | Accepted current tradeoff, with explicit remeasurement trigger |
| P06 | Open: complete export graphs and repeated preview/download work | G2 bounded, privately staged complete export and reuse policy |
| P07 | Measured inexpensive idle polling | Accepted current tradeoff, with explicit remeasurement trigger |
| P08 | Partial: batch invariants reused; snapshots still built for losing candidates | G5 score/select/explain separation where exact behavior is preserved |
| P09 | Open: weekly full-feedback materialization/fingerprint repeated | G4 transactional input revision plus required-data projections |
| P10 | Implemented alias index, historical regression verification | Preserve transitive identities, preferred recording evidence and exclusions |
| P11 | Implemented batched eligible catalog reads | Preserve ordering, duplicate/missing IDs, cancellation and expiry checks |
| P12 | Open: cache-wide SUM/COUNT for publication | G4 exact transactional quota counters |
| P13 | Open: owner export/delete scans unrelated encrypted mail | G2 authenticated owner layout/index and resumable legacy migration |
| P14 | Open: fresh pool encoded then decoded into another graph | G5 reuse the validated acquired object only when publication returns that exact document |
| P15 | Open: secure authentication holds unrelated-write admission | G3 pure password verification outside admission; atomic guarded finalization |
| P16 | Open: artist requests precede sufficient direct recording tags | G6 direct-tag sufficiency and explicit evidence-acquisition status |
| P17 | Implemented combined account eligibility query | Preserve both Identity and Domain checks and immediate revocation |
| P18 | Open: upstream compression opportunity; deployment static boundary unresolved | G6 negotiated upstream decompression and public-static compression only |
| P19 | G1b implemented and locally verified: statement aggregation preserves exact accounting | Bulk SQL/CPU/WAL gains measured; cold/small-statement costs and external workload acceptance documented |
| P20 | Open: uninterrupted scheduled capture stops on stale inventory | Joint G1 design; known-write publication and authenticated recovery across all phases |
| T01 | G0 implemented and locally verified | Installed CLI compatibility, child status, converter/report and global fallback verified |


<a id="source-plan--approved-decisions"></a>

#### Approved decisions

The developer explicitly approved D1–D4 with this plan. These contracts govern implementation; do not request their approval again. If a new material contract becomes unresolved, stop only its dependent work and continue independent approved groups.

| Decision | Recommended contract | Alternative / consequence |
| --- | --- | --- |
| D1: artifact trust and external-edit detection | Approve incremental mode for registered immutable artifacts, an independently held authentication key, SHA-256 verification on consumption, startup/recovery full checks and a **60-second maximum age of a completed full audit**. Start refresh at 30 seconds; stop new managed mutations when the audit expires, fails or is uncertain. Strict five-pass mode remains available. | Retain five full content passes for immediate pre-write detection of arbitrary external edits. This preserves the present stronger temporal check but also retains its measured cost. Filesystem notifications/mtime alone are insufficient. |
| D2: export snapshot, temporary retention and disk privacy | One consistent database snapshot plus authenticated owned archive view; one completed snapshot per owner, reusable for **10 minutes**, timestamped explicitly. Private 0700 directory/0600 files outside webroot, preferably on an encrypted volume. **256 MiB aggregate staging allowance**, also subject to managed stop and filesystem headroom; one cross-process builder initially. Private plaintext JSON staging needs explicit acceptance if no encrypted volume is available. | Stream directly and accept partial JSON on late failure, or retain whole-memory export. Neither meets the preferred bounded, complete-publication design. No history may be silently truncated to fit staging. |
| D3: schema and operational metadata | Approve additive versioned checkpoint/commit-receipt fields, exact counters and owner-index/export metadata as specified below, including reviewed privacy inventory and migration/backfill. Preserve private history retention; retain operational recovery information only until safe checkpoint/cleanup. | Retain current schemas/expensive derivation. No counter or journal is introduced without accounting, lifecycle and migration coverage. |
| D4: evidence and compression boundary | Nonempty valid recording-scoped tags suffice under the current parser; record artist metadata as `not_requested` when skipped. Preserve actual failures as unavailable. Negotiate supported upstream compression. Prefer selected reverse proxy for public static CSS/JS compression; test locally, deploy only separately authorized. | Require artist supporting evidence, retaining extra calls; or use public-static-only host middleware if the eventual proxy cannot compress. Private dynamic API/auth/export responses remain outside blanket compression. |

D1 is a security-contract change: even five checks do not provide instantaneous detection between checks, but incremental mode deliberately allows unchanged, unconsumed external edits to wait for the audit. No mode can prove authenticity after a fully privileged administrator compromises the process, key, database and filesystem together, or coherently restores all trusted state. An independently protected external anchor would be a different architecture and is not proposed.

The 60-second audit deadline and export limits are proposed local defaults, not verified VPS capacity. If a target cannot finish full verification within the deadline, fail closed and report the availability/cost tradeoff; do not silently extend the window.


<a id="source-plan--dependencies-effort-and-commit-boundaries"></a>

#### Dependencies, effort and commit boundaries

```text
G0 tooling + reproducible baseline
  -> G1 joint P01/P19/P20: protocol -> trigger batching -> scheduled integration -> incremental mode
       -> G2 exports/owner archives
       -> G3 authentication boundary
       -> G4 input revisions/public quotas (uses G1 accounting contract)
  -> G5 scorer/pool reconstruction
  -> G6 HTTP/evidence/compression
all implemented groups -> G7 combined/resource/recovery verification and final dispositions
```

G5/G6 can be implemented independently if a G1 decision is pending; the default sequence above follows audit priority. Archive, export, authentication and new counter writers must use G1's protocol once introduced. No newly introduced feature is enabled before its writer coverage and recovery checks pass.

Planning allowance: **15–25 focused engineering days**, excluding developer reviews, target-VPS work and live-provider acceptance. Approximate components: G0 0.5–1; G1 5–8; G2 3–4; G3 1–2; G4 2–3; G5 1–2; G6 1.5–3; G7 1–2. These overlap in verification effort; re-estimate after G1's protocol tests. Storage/recovery and migration uncertainty dominate, not small C# edits.

Each stage below ends with its targeted tests, matched measurements, audit/progress updates and a focused local commit. Separate migrations/backfills from activation where practical, while keeping every intermediate state runnable and guarded. Never disable a trigger to make a benchmark pass. User-visible behavior updates CHANGELOG; documentation-only planning does not imply a shipped optimization.


<a id="source-plan--common-measurement-and-acceptance-protocol"></a>

#### Common measurement and acceptance protocol

- Freeze commit, SDK/runtime, package locks, harness source/input hashes, filesystem, PostgreSQL version/configuration, provider mode, limits and host/container resources. Use disposable synthetic databases/private roots and blocked outbound HTTP. Do not start the normal live IDE profile for load tests.
- Before each change, rerun the original component and actual route/CLI controls on current HEAD. Afterward rerun the same inputs, warm-up and resource limits. Keep default-tiered host tests separate from explicitly tiering-disabled compute controls. Run timing experiments sequentially without profiler overhead; collect traces/counters separately.
- Compute comparisons: three fresh processes, 60 warm-ups and 100 operations each where appropriate. Storage/file scenarios: at least five samples per shape, warm/cold cache clearly separated. Auth/read/concurrency journeys: at least 100 operations for useful p95 reporting; do not label five-sample tails reliable p95. Record median, range, sample count and run-to-run variance.
- For every optimization record wall latency, allocated bytes and CPU; also RSS/live heap/GC where material. Record SQL count/plans/buffers, trigger calls, WAL, lock waits and transaction duration for database changes; artifact read/write bytes and fsync time for storage; dispatched HTTP counts, encoded/decoded body bytes and cache behavior for network work. Mark genuinely inapplicable metrics explicitly.
- Correctness/security are hard gates. Performance acceptance requires a reproducible improvement in the targeted metric, not just passing tests. Proposed numeric budgets below are review targets. If missed, investigate or document an explicitly accepted limitation; do not claim the target was achieved.
- Preserve sanitized summaries and nonsecret reproduction commands/input generators in Git. Keep raw profiles, mail, HAR, keys, tokens, database contents and large artifacts gitignored. Record original and measured build hashes. Do not depend on volatile `/tmp` for lasting evidence.
- Relevant suites include Domain benchmark seeds/replay; Application boundary/feedback/export/scheduling; migrated PostgreSQL accounting/coordination/privacy/scheduling; Infrastructure storage/email/cache/transport; Presentation authentication/privacy/export; offline Node and actual browser journeys. Run targeted checks per commit, full solution and existing TRX gate at integration. No skipped PostgreSQL tests may be represented as verified.


<a id="source-plan--g0--diagnostics-and-reproducible-controls-t01"></a>

#### G0 — Diagnostics and reproducible controls (T01)

Root cause: `profile.sh` contains a literal `\nmkdir`, asks `collect` for a Linux-kernel-only profile, prints `report topN` in the wrong argument order and assumes a Speedscope output filename that must be checked against the converter. Installed help confirms `dotnet-sampled-thread-time` belongs to `collect`, while `cpu-sampling` belongs to `collect-linux`.

Repair the existing scripts, not the product. Use the supported managed sampled-thread profile by default and label its units honestly. Native kernel profiling is optional and requires separate access; never silently escalate to system-wide/root capture. Fix directory creation, converter output discovery, printed report command and SDK-selection documentation. Review the related memory/coverage wrappers against installed help; change only reproduced incompatibilities.

Verify shell syntax, invalid arguments, output collisions and paths with spaces. Launch a task-owned synthetic workload through the wrapper and prove it produces readable NetTrace, Speedscope and correctly invoked top-N output; verify child exit status. Preserve the explicit-PID policy for attachment. A launch-based capture avoids attaching to any existing process. Do not collect full sensitive dumps simply because the tool exists.

No migration. Commit: diagnostics repair/help plus smoke-check evidence. Acceptance: supported commands complete and declared artifacts exist; tool repair alone is not an application speedup.


<a id="source-plan--g1--joint-p01p19p20-architectural-design"></a>

#### G1 — Joint P01/P19/P20 architectural design


<a id="source-plan--shared-root-causes-and-measured-baseline"></a>

##### Shared root causes and measured baseline

P01's storage-only five passes take **4.02 ms empty, 472.40 ms at 100 MiB, 2,202.93 ms at 512 MiB**; approximately 100 MiB in 1,000 files takes **907.42 ms and 51.8 MB allocation**. The 512 MiB operation reads approximately **2.68 GB of application bytes**, with zero physical reads in the warm-cache control. SQL and queue waits are additional, not included in those times.

P19's 1,000-row evidence insert takes **92.36 ms**, including **81.39 ms in 1,000 trigger calls**, and reports **572,376 WAL bytes**. Row serialization remains necessary for exact existing logical-byte accounting. The avoidable candidate is updating the shared ledger for every row; fewer ledger updates do not imply zero contention.

P20's actual finite Worker stops **all 1/10/100 due recipients**, with `database_inventory_changed`, after generation commits without publishing its new core inventory. The measured **3.02/3.37/4.58 seconds are failure timings**. Capture/status changes, failure recording, scheduling, unsubscribe, archive writes/deletion and key writes must all participate; fixing only the generation return leaves later stale states. The current combined local-capture/job-host guard must remain until integration is proved.


<a id="source-plan--chosen-approach-and-alternatives"></a>

##### Chosen approach and alternatives

Extend existing PostgreSQL admission and private filesystem storage with **authenticated versioned checkpoints, one bounded recoverable operation at a time, immutable published artifacts and exact database accounting**. Keep the five logical validation boundaries (acquire, reserve, before mutation, verify result, publish). First run this protocol with full SHA-256 scans at those boundaries. Only a later verified stage may replace ordinary unchanged-content scans with authenticated manifest/generation validation under D1.

This is one storage protocol, not an independent P20 bypass or an EF-only accounting shortcut. Alternatives considered: keep full scanning and operator reconciliation (safe but retains the costs and unattended-delivery problem); trust mtime/watchers or auto-reconcile any new revision (rejected: cannot distinguish tampering from authorized work); move everything to a new object store/queue (unnecessary service and migration complexity for one VPS).


<a id="source-plan--invariants"></a>

##### Invariants

1. Every admitted mutation has a fresh authenticated starting checkpoint, matching database epoch/schema/core/full revision and artifact generation. An unexplained mismatch stops writes. No ordinary path establishes trust by measuring whatever happens to exist.
2. SHA-256 remains the content identity. Authentication covers checkpoint/manifest version, storage-instance ID/epoch, generation, preceding checkpoint hash, categorized canonical paths, lengths, hashes and database accounting state. A hash stored beside mutable data alone is not authentication.
3. Use a dedicated versioned HMAC-SHA-256 key supplied outside all mutable artifact/control roots, never generated automatically to replace a missing key. Root-only tampering cannot forge a checkpoint. Unknown/lost keys stop operation; rotation is explicit, with authenticated old/new key IDs and verified migration. Do not reuse public hashes or expose keys through exports/logs.
4. Logical accounting equals the existing sum of UTF-8 `to_jsonb(row)` size plus 512 bytes for every covered row, with the existing core subset preserved. Full/core revisions remain monotonically advanced for the same affected rows, including no-op value updates. `max(logical accounting, pg_database_size)` and volume headroom remain admission inputs.
5. Reserve and charge artifacts, temporary files, manifest generations and recovery metadata, including old/new overlap and retries. Never double-charge a file once promoted; never release a reservation while its bytes still exist. Expired/stale state may permit verified space reclamation, never unverified growth.
6. SQL atomicity and filesystem atomic rename are distinct. Recovery must identify whether SQL committed and which exact files belong to that operation. Unknown outcomes are resolved by reading committed state, never replaying a file-producing transaction blindly.
7. One digest and one authenticated local receipt per account/week; `LocalCaptured` is not external sending and `SentAt` stays null. Schedule advances exactly one approved slot only after receipt, captured status and inventory publication are valid. Current eligibility/unsubscribe/deletion checks remain authoritative at capture time.
8. Ownership is derived from authenticated receipts and authorized server-side account/profile resolution. Paths/manifests never supply independent authorization. No recipient association enters the public recent feed.


<a id="source-plan--control-state-and-normal-database-writes"></a>

##### Control state and normal database writes

Add bounded versioned commit-receipt fields to the existing operational ledger (or a singleton companion if EF mapping requires it): storage epoch, operation ID/kind, predecessor checkpoint hash, intent hash, exact committed core/full revisions and totals, intended artifact-generation hash and authentication tag. There is at most one unpublished operation because the existing global lease prevents another admitted mutation until publication or verified recovery.

Avoid a recursively accounted journal: the bounded operational control record is explicitly treated like the existing ledger, excluded from row-delta triggers, covered by schema/authentication checks and a separately charged control-state allowance. Cap the serialized receipt and pending intent at 64 KiB each; reserve their combined allowance in addition to actual manifest/staging bytes and verify the bound in schema/serialization tests. Larger artifact plans must be split into bounded operations. This is a proposed extension of the accounting contract requiring D3; private payload/history does not belong in this record. Any owner identifiers required for recovery must be minimized, included in deletion handling and removed when checkpointed. Retain only the last committed receipt plus the bounded pending operation, not an indefinite event log.

Under the admission leases, persist a signed, flushed intent describing the prior checkpoint, bounded reservation and allowed mutation/artifact plan. In one database transaction, fence the ledger, validate base state, apply the mutation, verify resulting accounting/headroom and store the authenticated exact post-commit receipt. The callback is finished before sealing; no further managed mutation is permitted between sealing and commit. Verify committed state still matches the sealed result before publication, including any deferred database effects; a mismatch stops rather than being silently re-signed. File/control work is not automatically replayed by EF's execution strategy.

After successful SQL commit, publish immutable manifest data and atomically replace the signed checkpoint, flushing file contents and the containing directory as required by the supported filesystem. Keep the lease until completion. Caller disconnect cannot cancel bounded post-commit bookkeeping; an exceeded deadline produces a recoverable pending state. A subsequent writer may recover only the exact authenticated predecessor/receipt chain with matching schema, revision, totals and artifact hashes. An arbitrary newer database revision still fails closed.

Physical database growth without a row revision is charged conservatively under the unchanged trusted logical/schema state, as the existing coordinated path allows; it does not cause an endless reconcile loop in the legacy path. Above-cutoff growth blocks new growth while allowing verified bounded reclamation. Fsync/crash-durability behavior must be proved on the target storage; rename alone is insufficient evidence of power-loss durability.


<a id="source-plan--artifacts-audits-and-trust-boundary"></a>

##### Artifacts, audits and trust boundary

All application writers register a bounded mutation plan: recordings, reports, caches, encrypted copies, backups, partials, export staging, manifests and protection-key artifacts. Operator-managed roots must either remain strict or be explicitly imported under a verified maintenance operation. An uncovered writer prevents activating incremental mode. Key stores that legitimately mutate need coordinated creation/rotation; simply removing them from accounting is not a fix. The independently supplied integrity-authentication key is an explicit secret/configuration boundary; its managed backup/rotation artifacts still require an inventory policy. Review that policy during enrollment instead of accidentally including the trust anchor in the roots it authenticates.

Published data files are immutable; replacements use new staged files and authenticated generation publication. Paths are canonical, private, non-symlinked and confined to configured disjoint roots. Hash changed/new content and verify declared sizes before publication. Hash every consumed file against its authenticated entry before parsing/decrypting/serving it. Retain existing count/byte/path limits, cancellation and fail-closed error behavior.

At each ordinary validation boundary, still enumerate current paths and stat lengths/attributes against the authenticated manifest, rejecting unexpected additions, missing files, changed sizes or symlinks. This keeps unknown extra bytes from bypassing accounting while content audits are deferred. Metadata is not accepted as proof of unchanged content: same-size edits remain subject to on-consumption hashing and the D1 deadline. Enumeration remains O(file count), and must be included in the many-file measurement rather than hidden inside a purported constant-cost check.

Start with one immutable manifest per category plus a small authenticated root. Cache parsed immutable metadata only under the existing bounded memory budget and signed generation key. Ordinary writes do not reopen unchanged payloads; manifest metadata work can still scale with entry count. **Do not claim universal O(changed files)**. Introduce manifest paging only if the many-file comparison shows the simpler form fails the agreed budget.

Full verification includes unexpected additions/deletions, same-size/same-mtime content edits, control tampering and all configured categories. Perform it at startup, after uncertain recovery, before trusting a migrated baseline and periodically under D1. Audits compare with authenticated expected state; they never bless external edits. Metadata/watch notifications may invalidate trust early, but cannot substitute for full checks. To avoid races, audit a pinned immutable generation and fence publication; the initial implementation may retain the global lease for the full scan and report that periodic latency spike separately.

With D1, a full-audit completion deadline is an admission requirement. A failed/overdue scan or clock rollback invalidates incremental trust across processes; persisted wall-clock timestamps alone do not establish freshness after restart. Restart always audits before enabling writes. Unexpected discovered changes quarantine the affected state and stop managed mutations. Reads that consume artifacts still verify content. This preserves detection with an explicitly different detection window; it is not equivalent to rehashing all roots before every mutation.


<a id="source-plan--p19-trigger-batching-within-the-same-contract"></a>

##### P19 trigger batching within the same contract

Replace per-row ledger updates, where supported and measured worthwhile, with separate INSERT/UPDATE/DELETE statement triggers using PostgreSQL transition tables. Compute exactly the old/new byte sums and row deltas, then update the ledger once per affected statement. Advance full/core revision by affected-row count to preserve revision meaning; zero affected rows do not invent row revisions. UPDATE must cover both old and new images without pairing by a mutable key. Preserve the explicit pre-TRUNCATE accounting path and its existing revision rule.

All raw SQL, EF bulk updates/deletes, cascades, upserts, retries, multi-statement imports, rolled-back statements and schema changes remain covered. Validate actual PostgreSQL 16+ transition-table limitations and cascade/upsert behavior in migration tests. Keep row accounting for any unsupported shape rather than creating a coverage gap. Update schema/trigger verification to recognize and validate the new definitions and operational exceptions; do not just stop checking the old trigger names. A cross-process ledger fence remains required.

This stage removes repeated ledger writes/WAL and lock reacquisition, not the O(rows) serialization needed by the approved exact accounting formula. Do not change that formula or add per-table ledgers unless matched measurements establish that the remaining cost requires another reviewed design.


<a id="source-plan--p20-capturescheduling-protocol"></a>

##### P20 capture/scheduling protocol

Route both legacy and discovery weekly persistence through the known-write publication protocol regardless of the job-host flag. Generation publishes its committed state before capture starts. Scheduler initialization/advance, unsubscribe, failure recording and capture-state commits use the same protocol. No provider I/O occurs in the shared mutation transaction.

Capture retains its account ownership lease and authenticated receipt. Prepare a bounded encrypted partial, then under the mutation/fencing rules recheck subscriber/digest/opt-out and input fingerprint. Flush and authenticate the partial, publish the final immutable receipt without overwrite, commit captured state plus its exact operation receipt and publish inventory. A published file is never assumed rolled back because SQL failed. Recovery authenticates that exact receipt and current eligibility; it either finalizes the authorized state once or reports a safe stop/owned orphan requiring cleanup. It never sends externally or recreates a deleted account.

Schedule advancement is a separate short idempotent mutation after capture publication. A crash before advancement retries the original due slot and receipt, not a new message or a skipped week. Keep finite tick/backlog limits and the existing scheduler lease. Enable `RunJobs` plus local capture only after both composition roots pass combined concurrency, key-write and recovery tests; retain the guard until then.

Canonical lock order for participating paths: optional scheduler/export-capacity lease, account-email ownership lease when needed, global database admission lease, filesystem mutation lease, transaction ledger fence, account/profile row, digest/job row, ordered cache keys. Paths not needing an earlier lease skip it; none acquires it after a later lease. Rework legacy capture's file/row order and verify deletion/unsubscribe/import/maintenance against this order. No global write holder may wait for the scheduler/export-capacity lease. Use deterministic ordering for multi-owner work and fault tests for deadlocks and stale leases.


<a id="source-plan--failurerecovery-acceptance"></a>

##### Failure/recovery acceptance

| Failure or race | Required outcome and test |
| --- | --- |
| SQL rollback/deadlock/serialization retry | Rows, counters and commit receipt roll back together; no trusted inventory advance; only explicitly owned partials may remain |
| SQL commit succeeds, checkpoint replace fails or process dies | Exact authenticated committed receipt permits bounded recovery without operator baseline reset; tampered or unmatched state stops |
| Connection lost during COMMIT | Query operation identity on a new context to distinguish committed from rolled back; no duplicate mutation/file publication |
| File published, capture SQL not committed | Authenticate owner/week/body/fingerprint and pending plan; recheck eligibility before one finalization; preserve receipt or report safe owned cleanup |
| Capture committed, schedule not advanced | Original receipt reused; one conditional seven-day advance; no additional mail copy |
| Two hosts/processes and stale lease holder | Only current fenced operation publishes; one slot/receipt; stale completion rejected |
| Unsubscribe/delete/password/session change during work | No post-opt-out capture, no account resurrection; auth/session checks remain effective; private deletion includes pending artifacts |
| External SQL or file edit, trigger disable, manifest rewrite/rollback | Unauthenticated revision/checkpoint transition fails; consumed file hash fails immediately; other file edits detected by approved full-audit deadline |
| Missing key, clock rollback, lost audit/watch signal, scan cancellation | Incremental trust invalidated; full verified recovery or explicit stop, never silent trust regeneration |
| Disk full/ENOSPC/permission denial/fsync or rename failure | No complete export/receipt falsely advertised; SQL rollback or known pending recovery; reservations cover remaining bytes |
| Physical DB growth and concurrent reservation/eviction | Charge actual observed size; prevent over-admission; no blind reconciliation or unlimited retries |
| Ledger overflow/underflow, cascade/upsert/TRUNCATE, migration interruption | Constraints hold, exact independent recomputation matches, or transaction/migration stops safely |


<a id="source-plan--migrations-rollout-and-rollback"></a>

##### Migrations, rollout and rollback

Stop all writers for initial migration, take/validate a backup, verify existing ledger independently against covered rows, hash every existing artifact and authenticate the initial manifest/checkpoint. Do not repair mismatched counters automatically. Preserve old content, receipt ownership, IDs, snapshots and keys. Initial enrollment establishes trust only through this explicit verified maintenance step.

Use additive versioned control schema first; migrate triggers atomically with independent backfill/reconciliation and populated legacy fixtures. Update privacy/schema inventories and both composition roots. Old writers cannot understand new epochs/trigger contracts: prevent mixed-version operation. New binaries initially use full scans; incremental mode is a later activation after writer coverage, tamper and failure tests pass.

Rollback before activation can stop writers and revert additive unused state. After new-format writes, prefer disabling incremental mode in the new binary while keeping authenticated publication/recovery. Returning to an old binary requires a documented stopped-writer format conversion plus complete reconciliation, or restoring a verified pre-upgrade backup together with its matching artifacts/keys; a raw Down migration is not a safe data-preserving downgrade. Recovery/format conversion must never recapture an already published receipt.


<a id="source-plan--independently-verifiable-implementation-stages-and-acceptance"></a>

##### Independently verifiable implementation stages and acceptance

1. **G1a protocol foundation:** versioned trust/control state, explicit migration and known-write SQL publication with full scans unchanged. Tests prove all prior fail-closed conditions and commit/publication fault recovery. No incremental shortcut or trigger change yet.
2. **G1b P19 accounting:** transition-table trigger migration and verifier/backfill. Exact totals/revision semantics on generated mixed mutation sequences; one ledger update per eligible statement; lower trigger cost/WAL than matched baseline without material single-row regression. Report remaining serialization and contention.
3. **G1c P20 integration:** generation, capture, failures, key management, schedule/unsubscribe/deletion writers and both hosts. Fresh 1/10/100 ticks must complete without reconcile/retry scaffolding, produce exactly one authenticated receipt/account/week, and advance one slot. Repeated ticks and process crashes must not duplicate or skip. Compare successful throughput separately from the old failure timings.
4. **G1d P01 incremental activation:** only after D1 and complete writer enrollment. For unchanged 100/512 MiB payloads between required audits, zero payload rehash bytes on ordinary DB-only writes; target **at least 90% less application read traffic and 80% lower storage-phase median latency** in matched warm controls. Changed payloads are fully hashed. Report periodic/startup scan time, total CPU/read bytes over a fixed workload, metadata allocations and worst admission pause; merely moving all work to a background task does not establish overall efficiency.

Each stage has a focused migration/implementation/evidence commit as needed. Nothing in G1 is implemented before the complete joint design is approved. G1c may restore safe unattended delivery while G1d remains disabled if immediate arbitrary-edit detection is retained.


<a id="source-plan--g2--complete-bounded-exports-and-owner-archives-p06p13"></a>

#### G2 — Complete bounded exports and owner archives (P06/P13)

Baseline: 10,000 recommendations map in **587.69 ms / 242.74 MB allocated**, generating **52,958,047 JSON bytes**. Mapping plus serialization allocates **439.55 MB** and peaks at **401.80 MB process working set**; four exporters peak at **1,207.47 MB**. The shared 10,000-file archive takes **1,342.37 ms / 1,317.96 MB allocation** to export one owned copy. These are different measurement scopes and must stay separate.

Use bounded keyset pages/projections for every exported section and incremental JSON writing into private staged output. Preserve existing version 2.5 data shape, explicit security exclusions, unknown score fields, disabled/incomplete archive statuses and all account/guest history. Keep legacy endpoints compatible by returning the completed JSON byte stream as their existing JSON/download response. Do not use `ToArray` across complete history or parallel queries on one DbContext.

Build database sections in one read-only repeatable-read snapshot with deterministic composite cursors; include deleted history where the current contract does. Use the account ownership lease for a stable owned archive view against capture/deletion, without holding global admission while reading/serializing the export. Consume immutable authenticated archive copies and cap build duration initially at 60 seconds. Snapshot isolation/long transactions can retain database versions: measure transaction duration and vacuum impact. If consistency or deadline fails, discard the stage and return an error; never publish a partial complete export.

To fit G1 without inventing an untracked growing file or holding its lease throughout serialization, flush bounded JSON chunks of at most 8 MiB through separate short G1 operations, with their own reservation, hash, fsync and inventory publication. Use separate write scopes from the read-only snapshot context. Stored chunks are immutable and immediately charged; an incomplete export descriptor is never downloadable. Atomically mark the ordered chunk descriptor complete only after all sections and the complete-stream hash/length validate. Downloads concatenate those exact chunks into the unchanged JSON format, checking each before use, without copying into a second full output file. This stays below the existing 64 MiB per-operation reservation ceiling and charges disk growth before publication. Backpressure the serializer while a chunk is being committed. Report the chunk/fsync/metadata and snapshot-transaction cost: a bounded-memory gain can increase disk work, especially while strict full scans remain enabled.

Proposed UI/API extension: create an authenticated export snapshot returning a small timestamp/size/section-count descriptor, then download the same completed snapshot by opaque ID. Keep the old fresh-export routes for clients. The account page shows the descriptor and a bounded preview, with full data available in the complete download, avoiding browser parsing/rendering of 53 MB just to preview it. No database history is omitted. An explicit refresh requests a new snapshot; reuse is timestamped, never silently described as current. Same guest ownership rules apply to guest exports; no account required.

Every descriptor/download rechecks current account/guest eligibility and server-side owner. The opaque ID is not a bearer capability. Expiry, deletion, guest expiry/attachment/import, cancellation, failed builds and restart cleanup revoke/remove relevant staged files and metadata. Logout/session changes clear browser descriptors. TTL cleanup runs at startup and in bounded maintenance; a crash cannot leave uncharged private copies indefinitely. D2 explicitly approves the new disk-retention/privacy boundary.

Each chunk/complete operation fences the still-active build token and owner; an expired or revoked builder cannot recreate deleted metadata. Atomic publication means a failed build is never advertised as a complete export. A later network disconnect, disk error or tampering during download can still interrupt transfer; the complete length/hash identify a truncated download, and the server must abort rather than continue with invalid bytes. Do not claim that staging makes network delivery atomic.

Move local mail to a server-derived per-owner directory/index. Lookup still authenticates encrypted receipt owner/digest/week/fingerprint before read or delete. Owner index metadata is integrity-protected and registered in G1; a directory name alone is insufficient. Convert legacy shared copies once in bounded resumable batches: authenticate source, stage/verify destination, publish index, then remove source. Retain the verified original until publication; charge temporary duplication. Conflicts, unreadable or foreign receipts remain explicit issues, never silently discarded. After migration, absence from an owner index must not trigger an unbounded global scan. Keep legacy recovery/operator access explicit during transition.

Affected: Application export interfaces/DTO orchestration; DataAccess export projections and owned stage metadata; Infrastructure archive/path/staging; Presentation export routes and data screen. Add owner/stage metadata migration only as required; update complete privacy inventory, account/guest deletion and storage enrollment. No new export queue/service or framework dependency is planned.

Acceptance: parsed complete JSON matches baseline contents/ordering at a frozen snapshot; old/new migration round trips preserve every record and unknown field. With one builder and 10,000-history fixture, target **incremental exporter working memory under 64 MiB above the warmed process baseline** and bounded growth when history doubles. Report total allocation and stage/disk I/O as well as peak RSS. Keep four simultaneous requests as a queue/backpressure test, not a comparison pretending four builders ran. For a one-copy owner after archive migration, unrelated copies cause **zero decrypts/body reads**. Injection tests cover short writes, disk exhaustion, cancellation, corruption, index/path spoofing, interruption at every migration boundary, deletion/download races and owner isolation. Commits: archive migration, staged export/lifecycle, UI reuse plus browser/evidence.


<a id="source-plan--g3--narrow-authentication-admission-p15"></a>

#### G3 — Narrow authentication admission (P15)

Baseline unrelated guest-write medians: **53.64 ms alone / 188.59 ms overlapping valid login** with admission; control without auth admission **58.27/57.66 ms**. The control is attribution evidence, not a secure replacement implementation.

Split login into read-only verifier snapshot, CPU-heavy password verification outside global admission, then a short admitted transaction that reloads/locks the account and verifies password hash/security/concurrency stamps and current eligibility/lockout. Apply a failure increment, successful counter reset, any required password rehash and refresh-session write atomically. Issue success only after commit. Do not call a high-level verifier outside admission if it can persist a password rehash. Use the existing hasher's pure verification and defer writes. If the verifier state changes, retry bounded verification outside the lease or reject safely.

Concurrent attempts must not lose failures or bypass the threshold; a correct password arriving after another attempt locks the account must fail. Preserve refresh-token compare-and-swap, expiry/stamp checks, reset/revocation/deletion races and P17's immediate dual eligibility checks. Retain coordinated admission on all mutations; replacing the broad filter does not exempt register/reset/refresh/role or other AuthController writes. Unknown-account/error behavior and rate limiting remain unchanged.

Affected: `DiscoveryWriteFilter`, AuthController/AuthService and Identity-backed transactional operations. Prefer existing row locks/stamps without a new table. Test across two hosts/contexts, known/unknown/wrong passwords, threshold races, rehash, lockout/reset/delete and session rotation. Alternative account lock held while hashing still serializes a single account and expands lock duration; do not replace global contention with a long shared database transaction.

Acceptance: hasher execution is demonstrably outside global admission, all security races pass and matched overlapping guest-write median improves. Initial target: **overlap penalty no more than 25% above the alone median** on the isolated fixture, with CPU saturation reported separately. Do not expect faster password hashing. Record admission duration, auth latency/CPU, SQL/WAL and write p95. Commit implementation/security regressions, then matched contention evidence.


<a id="source-plan--g4--input-revisions-and-public-quotas-p09p12-depends-on-g1p19"></a>

#### G4 — Input revisions and public quotas (P09/P12; depends on G1/P19)

Baseline weekly inputs at 10,000 feedback records: **550.06 ms / 230.38 MB / three SELECTs**, with roughly 56 ms in SQL. At 50,000 public-cache entries, SUM/COUNT cost **13.94 ms** and the aggregate plan scans 50,000 rows/1,471 shared-hit blocks. A revision counter alone does not eliminate historical inputs actually needed by taste/familiarity/exclusions.

Add an exact per-profile generation-input revision, advanced transactionally for all relevant taste/onboarding/feedback/identity changes. Define dependencies from the current fingerprint fields, including raw SQL, account import/deletion, track identity and matched-tag inputs. Avoid revision updates for unrelated recommendation reads. Replace the second full input load during persistence with a locked revision comparison. Project only required historical feedback/track/tag fields on the first read; preserve the existing all-history weekly semantics and bounded onsite semantics. Do not silently introduce a feedback window or an approximate revision.

Add exact public-cache row/payload-byte counters maintained in the same transaction by database-enforced mutation coverage. Count leased/null-payload rows and zero payload bytes correctly; handle replacement, failed/expired leases, renewal, eviction, expiry, release, cancellation, configuration shrink, deletion and retries. The quota lock is acquired in a consistent order with G1's ledger before per-key locks. No admission decision uses an approximate/in-memory total. If shrinking below actual usage cannot be resolved within bounded eligible eviction, reject new publication clearly.

Separate schema/backfill/reconciliation from consumers. Independently recompute counters under stopped/fenced writers; mismatches block activation and recovery does not silently overwrite them. New counters have reviewed operational/private ownership and export/deletion treatment. Trigger/counter cost must be measured together with P19: fewer scans are not a win if a new hot-row update makes total latency worse.

Affected: DataAccess generation/evidence stores and migrations; Application revision interfaces; all input/feedback/import/delete writers. Test generated mixed mutation sequences, concurrent publication at exact quota boundaries, stale generation, rollback/backend termination and populated migrations. Acceptance: constant-size revision validation without a second history graph; no SUM/COUNT cache-wide scan on ordinary publication; exact independently reconciled totals; lower matched input allocation and publication latency with no correctness regression. Remaining necessary O(history) data/alias work is measured and documented. Commits: input revision migration/consumer, quota migration/consumer, integrated database evidence.


<a id="source-plan--g5--scorer-and-fresh-prepared-pool-allocations-p08p14"></a>

#### G5 — Scorer and fresh prepared-pool allocations (P08/P14)

Current continuation control: **3,024,112 bytes / 14.3168 ms** per 400-candidate, 271-tag rank; native-tiering-disabled pool encode/decode **4,054,424 bytes / 6.2028 ms**, decode alone **2,478,208 bytes**. Sampled compute-thread attribution was 65.69% ScoreCore, 21.68% DecodePool and 11.86% Encode; these are sampled thread-time percentages, not exact CPU shares.

Keep batch-invariant reuse. Compute validated lightweight candidate scores, select with the exact existing ordinal ties/one-artist/exclusion/optional-policy behavior, then build full explanations for selected picks. Preserve floating-point operation ordering; a different summation can change ties. All candidates still receive required validation, including invalid losing candidates. Keep public scalar/full-scoring APIs intact where callers need all explanations. The current Application path calls `ScoreForBatch`/`Score` and then `DiscoverySelectionPolicy.Rank`, whose vocal preference reads `Snapshot.Evidence`; optimizing only `RankForBatch` would miss that hot path. Supply the lightweight selector with equivalent evidence references and numeric sort keys, and materialize selected snapshots afterward. Preserve optional-policy priority order exactly.

For a fresh acquired pool, retain the acquired immutable object only if encode validation and shared publication prove that the returned persisted document is precisely that object/document (key/version/origin/hash/expiry/size). A lease race returning another process's document still decodes and validates that document. Stored hits retain decode/checksum/provenance checks. Check transient random IDs and serialized round-trip differences before claiming object equivalence; retain required canonicalization without reconstructing the entire graph.

No migration or policy/weight change intended. Domain remains pure and EF/provider types stay out of Application. Acceptance: exact selected breakdown/order/configuration hashes, replay, unknown-field round trips, legacy formula/batch versions, benchmark seeds and sparse/tie/invalid input cases. Target at least **30% lower rank allocation** in the original fixture, and elimination of the measured duplicate decode on eligible fresh pool paths; no specific end-to-end speedup is promised. Measure cold/persistent/memory paths separately, with evidence-bearing fixtures as well as the original empty-evidence control. Measure the actual optional-policy preparation path in addition to the pure rank microbenchmark. Commits: scorer selection/explanations, fresh pool reuse, matched measurements.


<a id="source-plan--g6--http-recording-evidence-compression-and-response-ownership-p02p03p16p18"></a>

#### G6 — HTTP, recording evidence, compression and response ownership (P02/P03/P16/P18)

Baseline P16: 200 direct-tag recordings dispatch **20 radio + 20 recording-metadata + two artist-metadata requests**, 121,042 synthetic payload bytes and about 2.00 MB warmed hydration allocation. Avoiding two paced dispatch slots could save 2.1 seconds of saturated queue budget; that is not measured live-job latency.

P16 design: validate recording response identity/origin and parse tags first. Sufficient means the current normalized recording-scoped vector is nonempty, finite, positive and traceable to eligible response evidence; no new quality/tag-count threshold. For candidate hydration with sufficient direct tags, skip unused artist acquisition/parsing and record a versioned `not_requested` supporting-evidence status. It is neither success nor requested-but-unavailable. Fetch additional metadata when the current fallback is required; preserve track-favorite rules that prohibit silently substituting artist-level evidence. Historical snapshots remain readable. Test direct, missing, malformed, mixed-origin, mixed-batch and actual fallback paths; selected scores remain equivalent where artist evidence was unused. Expected acceptance on the original direct-tag fixture: **zero of those two artist requests**, fallback behavior intact.

P18 baseline: one previously captured public response is **5,877 bytes identity / 1,806 gzip**, identical decoded hash (69.3% smaller body); cold static CSS/JS total **89,152 bytes**. The sample does not establish site-wide savings or latency. Configure supported `HttpClientHandler` automatic decompression, preserving a **1,000,000-byte decoded cap**, timeout/cancellation and decoded-byte provenance hashes. Reject unsupported/malformed/stacked content encodings safely; never rely on compressed Content-Length as the decoded limit. Test compressed oversize bodies and chunked responses. Static compression belongs at the chosen proxy by default, with correct Vary/ETag/304 and no double compression; a host fallback must be restricted to public assets. Private dynamic responses retain no-store/security policy. Measure encoded and decoded bytes and CPU separately.

P03 baseline parsing: **47,452 / 367,187 / 3,002,446 allocated bytes** for 10,011/100,011/900,011-byte bodies; medians **0.166/0.563/1.450 ms**. Inspect a bounded buffer-growth improvement using supported length hints and pooling only if ownership/hash tests prove a real net saving. Returned JsonElement must survive response/buffer disposal. Do not introduce pooled-memory lifetime bugs or a disposable document contract across unrelated callers merely to delete Clone. If the owning representation remains justified, document that residual cost as an accepted bound after measuring alternatives.

P02: five cacheable sequential calls already dispatch once; five no-store calls dispatch five times. The twenty-way duplicate control bypassed pacing, while the actual gate/post-pacing recheck avoids that shape. First exercise real reachable same-process distinct-pool metadata overlap with production pacing and separate-process controls. Only if duplicates remain material, add a bounded, operation-scoped single-flight coordinator keyed by full provider/request/auth context, with waiter cancellation independent of the owner, failed-entry removal and no retained no-store response. Every actual dispatch still acquires the shared gate and request budget. Do not add a distributed cache or unbounded semaphore map to eliminate two necessary process-local-cache requests. If no normal overlap warrants a change, close the remaining P02 part as an explicitly measured limitation.

Affected: Infrastructure provider/transport/codec/evidence mappings, additive snapshot evidence status and focused Presentation/proxy configuration documentation. No provider API change or dependency expected; refresh official endpoint/terms/headers before coding and use synthetic servers/replay for all tests. No live calls authorized by this plan.

Acceptance: exact counts/hashes for representative fixture shapes; rate limits, origin/auth isolation, s-maxage/Age restrictions, no-store/no-cache/private, 429 defer, no hidden retries, caller cancellation, malformed/compressed-oversize bodies and independent JSON lifetime all pass. Capture actual browser cold/warm ETags/transfers without routing that disables cache. Report saved request/body bytes, allocation and CPU; gains remain synthetic/local until separately accepted live/deployment measurements. Commits: recording sufficiency/status, compression, justified buffer/coalescing changes separately.


<a id="source-plan--g7--preserved-fixes-tradeoffs-and-final-acceptance"></a>

#### G7 — Preserved fixes, tradeoffs and final acceptance

P04/P10/P11/P17 remain explicit regression gates throughout. Re-run Retry-After Node/browser checks; alias bridges, duplicate evidence and exclusions; batched catalog SQL counts/eligibility; and one combined eligibility SQL query rejecting either deleted Identity or Domain account. Do not broaden scope into speculative indexes, a new recommendation policy, cache service, queue or frontend framework.

Accept P05's bounded stripes under the single normal acquisition lane: same-stripe synthetic sources serialize at roughly **306.54 ms versus 153.25 ms**, but real same/distinct-pool ten-job bursts were similar. Reopen only when acquisition concurrency changes or measured normal traffic attributes material queue latency to collisions. Accept P07's approximately **three 0.88 ms readiness probes/second/host** in exchange for current wake responsiveness. Reopen for multiple-host deployment, measured idle resource pressure or a different wake-latency requirement. The overview-query/index leads remain documented with their existing satisfactory local plans, not speculative migrations.

Run combined discovery + login + export + scheduled capture + cleanup with two processes, realistic synthetic history and 0/100/512 MiB artifacts, plus resource-limited full-stack tests. Exercise expiry/restart/pending recovery and a bounded long-running synthetic soak, reporting duration, backlog, failures, RSS/heap, file/control-state growth and reconciliation results. Proposed local soak: 24 hours with periodic synthetic due slots, two forced restarts and bounded transient database/filesystem faults; accelerate slot dates through fixtures, not altered production policy. Do not present this as production stability.

Final report includes every P01–P20/T01 disposition, each implemented commit, matched before/after data, security/correctness evidence, migration/rollback/deployment requirements, missed targets and explicitly accepted limits. Update the audit's current-status section rather than overwriting historical evidence, PROGRESS, CHANGELOG for actual user-visible changes and affected storage/privacy/provider/operations documents.


<a id="source-plan--external-acceptance-still-required"></a>

#### External acceptance still required

- Selected VPS CPU/RAM and actual filesystem/fsync/disk latency, PostgreSQL resource/WAL/vacuum and backup behavior, full-stack storage inventory/headroom and resource pressure. The audit's **2 vCPU / 2 GiB / at least 25 GiB SSD** remains an estimate, not purchased or measured infrastructure.
- Selected reverse proxy's TLS/compression/cache headers, identity versus gzip/Brotli bytes, encoded ETag behavior and private-response compression exclusions.
- Explicitly authorized representative provider endpoints/workloads under current quotas/terms, actual cold/warm latency and failure distributions. Historical public spot checks are not authorization for new calls.
- Representative account/guest histories, concurrency, large export staging and sustained queue/disk pressure; long-running delivery/recovery on the actual host, not just synthetic local ticks.
- An approved external mail transport and realistic delivery/opt-out/idempotency assessment. This task can restore uninterrupted **encrypted local capture**; it cannot certify inbox delivery or safely infer external exactly-once semantics.

No infrastructure purchase, public deployment, push, external sending or live-provider load is included. Where these checks cannot yet run, record an external acceptance requirement rather than claim production performance is verified.


<a id="source-plan--authoritative-api-checks-for-the-proposed-design"></a>

#### Authoritative API checks for the proposed design

Checked during this review; implementation must still verify exact installed behavior:

- [PostgreSQL 16 CREATE TRIGGER](https://www.postgresql.org/docs/16/sql-createtrigger.html): statement triggers and transition-table restrictions; repeat tests on the supported minimum as well as the selected deployment version.
- [PostgreSQL locking](https://www.postgresql.org/docs/18/explicit-locking.html): shared row/advisory lock behavior and consistent ordering requirements.
- [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace): managed sampled-thread diagnostics versus Linux kernel capture; installed help is the immediate compatibility authority.
- [HttpClientHandler decompression](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclienthandler.automaticdecompression?view=net-10.0) and [ASP.NET compression](https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-10.0): negotiation/decompression behavior and private HTTPS response considerations.
- [ListenBrainz metadata](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html) and [API requirements](https://listenbrainz.readthedocs.io/en/latest/users/api/): recording metadata, HTTPS/User-Agent and rate-limit headers. Keep existing shared pacing; no new endpoint or usage permission is inferred.
<!-- END SOURCE SNAPSHOT: docs/renewal/performance-remediation-plan.md -->

---

<a id="source-evidence"></a>

## Appendix A03 — All implementation stages, measurements, failures and verification

Original: [docs/renewal/performance-remediation-evidence.md](docs/renewal/performance-remediation-evidence.md). Full source text. Original source: 76,135 UTF-8 bytes, 1,150 lines.

Source SHA-256: `d261b1e90fb751f7d5d3dd465f9a66c15d7aa84c72bb5faa105d82413bbb4a47`.

<!-- BEGIN SOURCE SNAPSHOT: docs/renewal/performance-remediation-evidence.md -->

<a id="source-evidence--approved-performance-remediation-evidence"></a>

### Approved performance remediation evidence

The developer approved the complete [plan](#source-plan), joint
P01/P19/P20 design and D1–D4, including the private plaintext export-staging fallback,
on 2026-10-25. Local verification is not production acceptance or developer confirmation.


<a id="source-evidence--g0--diagnostics-compatibility-t01"></a>

#### G0 — Diagnostics compatibility (T01)

Baseline `f8263fd`; installed SDK 10.0.401/runtime 10.0.12, diagnostics tools
10.0.750501, ReportGenerator 5.5.11, Fedora Linux. No dependency changed.

Before: two offline wrapper regression cases failed (launch artifacts and nonzero
child status). The original script also failed against the actual installed
collector with exit 3: `cpu-sampling` does not apply to `collect`.

After: the wrapper creates its output directory, uses supported managed sampled
thread time, passes child arguments/IO, verifies the converter's actual filename
and prints the correct report argument order. A valid trace is converted even
when the child exits nonzero; its exit status is preserved. The shared global
ReportGenerator fallback also recursively invoked its own shell function; a new
regression reproduced the failure and the fallback now resolves the executable.

Verification:

- `python3 scripts/checks/test_diagnostics.py`: five passing checks, including
  paths/arguments with spaces, invalid arguments, existing-output protection,
  child exit propagation and the global ReportGenerator fallback.
- `bash -n diagnostics/profile.sh diagnostics/common.sh diagnostics/memory.sh diagnostics/coverage.sh` passed.
- Health check passed using the pinned private SDK first in PATH.
- Actual collector launched a new synthetic net10.0 Release process; normal exit
  returned 0 and produced a 100,238-byte NetTrace plus a 32,860-byte Speedscope
  document. JSON parsing confirmed one profile, and the printed top-N command
  successfully read the trace.
- A second launched synthetic process returned 7; the wrapper returned 7 and
  retained readable NetTrace and Speedscope artifacts (68,886-byte JSON).

Task-owned raw evidence and the tiny smoke project are gitignored under
`diagnostics/results/remediation-20261025/`. The smoke program performs two seconds
of repeated `Math.Sqrt` operations and exits with its optional integer argument;
it references only the net10.0 framework. Reproduce with a Release console build:

```bash
export PATH=/home/mahammadjafarli/.dotnet:/home/mahammadjafarli/.dotnet/tools:$PATH
bash diagnostics/profile.sh --launch --duration 5 --name smoke -- dotnet /absolute/path/Smoke.dll
bash diagnostics/profile.sh --launch --duration 5 --name smoke-seven -- dotnet /absolute/path/Smoke.dll 7
dotnet-trace report /absolute/path/to/smoke.nettrace topN -n 5
```

EventPipe socket access required sandbox escalation for these task-launched
processes. No existing PID was attached, no full dump was collected and no live
provider or application database was accessed. The thread-time report includes
runtime waits, confirming why it must not be labelled exact CPU attribution.

Latency/allocation/database/network improvement: **not applicable to this tooling
repair**. It restores usable capture; it establishes no application speedup. The
remaining groups require matched before/after application measurements.


<a id="source-evidence--g1a--authenticated-recovery-foundation"></a>

#### G1a — Authenticated recovery foundation

Fresh baseline: 702 tests passed with no skips across the five projects. The first
Presentation run hit the shared host's 128-instance inotify limit and a transient
`email_sink_tracked` stop; a clean rerun passed all 81 tests. Subsequent test processes
set `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false` to avoid configuration watchers.
This is a test-host setting, not a product/runtime change or a performance workaround.

The new opt-in protocol authenticates inventory and pending/committed operation
metadata using an independently supplied key. It adds one nullable bounded database
receipt, stores it in the row transaction and recovers only its exact authenticated
result after interrupted inventory publication. Existing full content scans remain;
this stage is a security/recovery prerequisite, not an optimization claim. Normal
unsigned configuration remains compatible. Runtime activation of signing/incremental
verification remains gated on G1c writer coverage; the existing capture guard remains.

The initial five file-level regressions failed before implementation. New checks
cover exact commit recovery without reconciliation, actual database rollback,
current/forged receipt rejection, unsigned downgrade, same-revision/no-op recovery,
leftover intent cleanup, legacy enrollment, independent/private key requirements,
unaccounted control/lease bytes and unknown artifact changes. A publication-fault
adapter injects failure after a real SQL commit; new scopes recover the original
committed profile and admit a second profile, with exact independent ledger totals.
This is fresh-service recovery, not a claim of power-cut hardware testing.

Migration `20261024232949_AddAuthenticatedStorageCommit` was generated with the
pinned EF CLI and `--context AppDbContext`. It retains legacy rows and adds a UTF-8
byte-limit constraint. The schema privacy inventory explicitly excludes the new
non-personal operational metadata; no private export field is omitted. A fixed
320 KiB control allowance covers bounded operational metadata. Applying the migration
changes schema inventory and admission accounting: stop writers, verify backup/ledger
and reconcile explicitly; do not apply it to a live database from this task.

Final full suite: **716 passed, zero failures/skips** (Domain 150, Application 139,
DataAccess 134, Infrastructure 212, Presentation 81); the existing five-project TRX
gate passed. Both offline Node browser checks passed. Existing tests include populated
migration, ledger corruption/rollback, ownership/deletion and process-recovery cases;
new signed-protocol recovery is explicitly identified above. No package change or
live-provider request was made. Component comparison follows below.

Remaining: P19 statement-trigger batching, all legacy/capture/key writers and actual
uninterrupted ticks, mixed-version activation fencing, incremental manifests/audits,
key rotation, actual process/power-loss and target-filesystem acceptance. P01/P19/P20
are still open as complete findings; G1a does not resolve them independently.


<a id="source-evidence--matched-default-mode-storage-control"></a>

##### Matched default-mode storage control

The same preserved audit storage helper, with per-operation process CPU added,
ran sequentially against frozen before/after DLL sets. Five samples per shape;
fixture construction is excluded. This comparison covers **unsigned default-mode
compatibility**, not the opt-in signing overhead or a P01 optimization. Native
Btrfs warm-cache runs reported zero physical read bytes. Process CPU covers all
helper threads and is not added to wall time. Shared-host timing variation is
visible; no latency improvement is claimed.

| Shape | Before / after median wall ms | Before / after CPU ms | Before / after allocated bytes | Before / after application read bytes |
| --- | ---: | ---: | ---: | ---: |
| 0 MiB / 0 files | 2.73 / 2.46 | 2.67 / 2.17 | 143,832 / 142,680 | 988 / 1,008 |
| 100 MiB / 1 files | 502.67 / 474.90 | 1161.71 / 1169.80 | 206,408 / 200,968 | 524,289,028 / 524,289,048 |
| 512 MiB / 1 files | 2073.63 / 1856.77 | 4191.38 / 4390.82 | 189,352 / 198,432 | 2,684,355,590 / 2,684,355,610 |
| 100 MiB / 1000 files | 654.72 / 542.92 | 1339.98 / 1360.02 | 52,202,424 / 52,211,408 | 524,291,154 / 524,291,174 |

Five payload passes remain. The bounded inventory representation adds small
metadata allocation; no hashing reduction has been activated. Signed-mode latency,
fsync and allocation baselines must precede G1d activation. G1a's database change
adds one constant-size receipt UPDATE only for opted-in signed mutations, with no
cache-wide/history scan; this stage supplies correctness evidence, not a measured
SQL optimization. No HTTP requests or response-transfer bytes are involved in this
storage component control.

Raw control files are `before-storage-cpu.jsonl` and `g1a-storage-cpu.jsonl` under
`diagnostics/results/remediation-20261025/`. The directory also preserves the exact
harness, runner recipes, source/assembly hashes (`g1a-measured-builds.json`) and
frozen binaries. A framework-only comparison project references those DLLs directly,
so rebuilding the harness does not accidentally substitute current product sources
for the before version. Raw captures are gitignored; no sensitive payload is committed.


<a id="source-evidence--g1b--statement-level-accounting-and-strict-storage-guard"></a>

#### G1b — Statement-level accounting and strict storage guard

Baseline: frozen `8438408` product assemblies. P19 originally measured 1,000
row-trigger invocations and repeated writes to the shared ledger. Migration
`20261025000408_BatchManagedStorageAccounting` replaces DML row triggers with
separate INSERT/UPDATE/DELETE statement triggers and PostgreSQL transition tables.
It preserves `octet_length(to_jsonb(row)::text)+512`, the existing core subset,
affected-row revision increments, empty-statement behavior and pre-TRUNCATE rule.
It independently verifies populated counters under table locks before switching;
a mismatch aborts the migration rather than repairing the ledger.

The storage guard checks trigger event/timing, statement shape, transition aliases,
function/schema, arguments, enablement and absence of a conditional predicate.
Its SHA-256 fingerprint includes complete stored trigger/function catalog records,
including function source and security settings. Merely retaining a trigger name
cannot disguise changed behavior. Catalog records are aggregated once. The one
fixed SQL statement is explicitly prepared per physical connection, avoiding
repeated planning; results and catalog rows are never cached. See
[Npgsql's preparation contract](https://www.npgsql.org/doc/prepare.html).

Final matched measurements used PostgreSQL **18.6**, SDK 10.0.401/runtime 10.0.12,
three alternating before/after pairs, fresh migrated disposable databases,
identical synthetic evidence rows and no other task workload. Reported values
below are pooled sample medians. Each pair has three SQL warmups, 25 measured
zero/1,000-row statements, 100 one-row statements, and 200 warmed inventory reads.
The first inventory read is recorded separately. Inserts roll back between samples.
CPU uses the task-owned backend's Linux counters at 10-ms resolution. Allocation
is process-wide managed allocation around the awaited client call, not server memory.

| Control | Before | After |
| --- | ---: | ---: |
| 1,000-row INSERT client latency | 66.838 ms | 21.202 ms |
| Same statement PostgreSQL execution | 66.054 ms | 20.024 ms |
| Same statement trigger time / calls | 57.073 ms / 1,000 | 8.947 ms / 1 |
| Same statement client allocation | 6,776 B | 6,776 B |
| Same statement WAL insertion delta | 639,408 B | 547,224 B |
| Same statement plan WAL / shared buffer hits | 544,365 B / 4,611 | 544,365 B / 4,611 |
| Backend CPU, 75 bulk statements plus controls | 4,700 ms | 1,450 ms |
| One-row INSERT client latency / allocation | 1.083 ms / 6,728 B | 1.243 ms / 6,784 B |
| One-row PostgreSQL execution / trigger time | 0.324 ms / 0.189 ms | 0.395 ms / 0.246 ms |
| Backend CPU, 300 single-row statements plus controls | 280 ms | 340 ms |
| Zero-row INSERT latency / allocation | 0.666 ms / 6,432 B | 0.777 ms / 6,776 B |
| Warm inventory read latency / allocation | 8.336 ms / 1,904 B | 6.431 ms / 1,744 B |
| Backend CPU, 600 warm inventory reads | 4,010 ms | 2,850 ms |
| Client CPU, same warm-read loop | 2,703.894 ms | 1,893.958 ms |
| CachedPlan context total / used, one warmed backend | 12,288 B / 11,480 B | 612,464 B / 380,024 B |
| First inventory read, three cold samples | 32.041 ms / 99,784 B | 53.800 ms / 105,656 B |

The bulk fixture shows **68% lower client latency**, **69% less backend CPU** and
**14% less statement WAL**. Warm inventory reads are about **23% faster** with
**8% less client allocation** despite stronger coverage. This does not establish
an end-to-end workload gain. Per-pair bulk medians varied substantially on the
shared host: before 35.195/87.946/78.047 ms, after 21.572/19.822/21.432 ms. Warm-read
pair medians were 8.079/8.547/8.290 versus 3.194/6.479/6.819 ms. Preserve these
ranges rather than projecting one median to a VPS.

A separate memory-context probe on one warmed connection measured **600,176 B
more reserved** and **368,544 B more used** in `CachedPlan%` contexts. This is a
bounded per-connection cost, not backend RSS or the entire database working set.
The actual pool size and total memory budget still need representative VPS
acceptance; one fixed prepared shape does not make its resource cost zero.

Measured costs remain: a single-row statement adds roughly **0.071 ms database
execution** and **0.160 ms client latency** in the pooled fixture; zero-row
statements invoke one short trigger but write no ledger/WAL. Single-row WAL stays
616 B. First use of each physical connection has preparation/initialization cost
and is slower here; its three samples include JIT and are not an isolated estimate
of preparation. There is one prepared query shape per connection, with no result
cache or automatic preparation of arbitrary SQL. An initial unprepared strict
guard regressed; SQL attribution led to the prepared implementation. Intermediate
measurements remain preserved rather than substituted into the final comparison.

EXPLAIN's plan WAL/buffer counters omit AFTER-trigger work: unchanged plan counters
are not evidence of unchanged ledger WAL. Statement WAL above brackets execution
with cluster insertion LSN readings, excluding rollback; unrelated background WAL
can contaminate individual samples. Database byte-accounting invariants remain
exact. Transition tables still serialize every affected row and can spill under
large workloads; no lower peak server memory, physical disk IO, retained database
size or live HTTP/network improvement is claimed. No upstream requests were made.

Verification: **725 tests passed**, zero failures/skips, across Domain 150,
Application 139, DataAccess 143, Infrastructure 212 and Presentation 81. All
**143 DataAccess tests also passed PostgreSQL 16.15**. New regressions cover bulk
and no-op revisions, zero rows, Unicode/upsert, real rollback, core cascades,
TRUNCATE, populated downgrade/upgrade, independent totals, migration mismatch,
concurrent statements, overflow rollback, trigger/function tampering, fresh
catalog visibility on the same prepared connection, and one retained prepared
shape. Existing authenticated commit/publication recovery and privacy/schema
inventory regressions pass. Browser-script checks, the five-project TRX gate and EF no-pending-model check
also pass. The committed reusable harness builds and passes a fresh isolated
run with all five output groups. These are
transaction/fault-injection checks, not a hardware power-loss claim.

The small reusable [SQL harness](#source-accounting-control)
is committed; raw JSONL/TRX, frozen assemblies, captured harness and reproduction recipes,
source hashes and intermediate runs stay private and ignored under
`diagnostics/results/remediation-20261025/`. Final comparison inputs are
`trigger-{before,after}-prepared-pair{1,2,3}.jsonl`; verification directories are
`g1b-prepared-verification` and `g1b-prepared-postgres16`.

Rollout requires stopped writers, a matched database/artifact/key backup, migration,
independent accounting verification and explicit full reconciliation before
restarting compatible hosts. Down restores the old row-trigger contract without
altering counters/data; it is not permission for a live or mixed-binary downgrade.
The changed schema fingerprint deliberately invalidates old inventory checkpoints.
G1c writer integration and G1d incremental activation remain outstanding; this
stage does not activate signed runtime mode or repair scheduled delivery by itself.



<a id="source-evidence--g1c-continuation-signed-capture-and-removal-not-final-acceptance"></a>

#### G1c continuation: signed capture and removal, not final acceptance

The capture/removal foundation is locally verified. G1c and P20 remain open; G1d's incremental
mode is not activated. The developer approved the narrow exception for
[privacy reclamation under uncertain artifact integrity](#source-privacy).
Legacy conversion still uses its previous adapter path and needs integration.


<a id="source-evidence--reproduced-controls-and-actual-delivery-probe"></a>

##### Reproduced controls and actual delivery probe

Frozen G1b controls (`g1c-before`) stopped all 1/10/100 recipients with
`database_inventory_changed`, produced zero receipts, and took
2,671.72/1,919.73/3,225.78 ms. These are failure timings, not successful throughput.

The current frozen Worker (`g1c5-worker-bin`) used a fresh synthetic PostgreSQL
18.6 database, the same frozen seed helper, fixture-only providers, local encrypted
capture, and independent private integrity key/control directories. There was
**one initial enrollment across all cohorts**, with no reconciliation between
generation, capture, advancement or cohorts. Each process ran two ticks, with a
one-second configured interval. All first ticks advanced exactly their requested
recipient count; every second tick had no jobs. The source/assembly manifest binds
this evidence to the uncommitted working changes, not merely HEAD `95ee516`.

| Recipients | Two-tick process wall time | Process user CPU | Process system CPU | Peak RSS |
| --- | ---: | ---: | ---: | ---: |
| 1 | 3,272.74 ms | 1.93 s | 0.16 s | 167,904 KiB |
| 10 | 4,675.85 ms | 3.41 s | 0.40 s | 181,224 KiB |
| 100 | 18,652.28 ms | 18.64 s | 3.57 s | 208,272 KiB |

Wall times include startup and the repeated tick; these are single observations,
not percentiles or representative VPS capacity. CPU can exceed wall time because
the process uses multiple threads. GNU time supplied process CPU/RSS values. No
allocation profile or backend CPU attribution was collected for this probe, and
no allocation reduction is claimed. Fixture configuration disallowed live provider
work; this was not a fresh packet capture or real provider/email acceptance run.

Final verification found exactly 111 digests, all `LocalCaptured`, none with
`SentAt`, and 111 encrypted files totaling 1,567,875 bytes. Final database size was
12,621,503 bytes. An independent full scan of every managed table recomputed
`sum(octet_length(to_jsonb(row)::text)+512)` as **5,440,177 bytes**, exactly the
stored logical counter. No counter correction was performed. The fixed control
allowance remains additional to database/logical storage accounting.

Private artifacts are under `diagnostics/results/remediation-20261025/g1c5-after/`:
`results.json`, schedule/resource logs, fixture metadata and `source-manifest.json`.
The preserved replay source is `recipes/g1c_uninterrupted.py`; it refuses evidence
reuse, performs only initial enrollment, and retains the original ciphertexts.
The key, synthetic identifiers and raw logs are not committed. Before/after failure
and success measurements must not be presented as a speedup percentage.


<a id="source-evidence--correctness-and-integration-evidence"></a>

##### Correctness and integration evidence

- `g1c5-removal-final`: all 21 authenticated storage/capture/removal checks passed.
  Coverage includes exact commit publication, SQL rollback, immutable final/partial
  recovery, incomplete partial cleanup, removal before/after interruption and
  same-size tampering that is neither adopted nor deleted.
- `g1c5-removal-db-final`: all 23 focused PostgreSQL/Worker checks passed, including
  real process termination before capture commit and before schedule advancement,
  recovery of the original ciphertext, repeated/concurrent capture, deletion
  serialization, exact control-receipt accounting and the following writer.
- `g1c5-security-fixtures`: all nine authentication/security checks passed, including
  lockout/rate-limit behavior and rollback of failed registration/deletion.
- The sole-expired-key regression proves recovery's read-only scope creates no
  key; normal stock rotation remains supported outside a pending operation.
  Revocation and shared key-manager cache invalidation remain tested. A very long
  outage with only a framework-unusable expired key can require maintenance before
  pending encrypted recovery; no custom cryptographic/default-key override exists.

A prior full continuation scan ran 741 tests: 736 passed and five failed before
copy-removal integration and fixture corrections. That historical failure run is
preserved as `g1c4-full-scan`; it is not the final status. The final broad scan is
being retained separately as `g1c5-verification-scan`. Do not infer a complete pass
from focused results. The existing HTTP privacy test remains a gate for verification of the approved
exception; it must not be weakened to accept account-deletion blocking.


<a id="source-evidence--tooling-observation-and-deployment-limits"></a>

##### Tooling observation and deployment limits

MSBuild child evaluation/shared compilation on this host could fail without a
useful root compiler diagnostic. Explicit `BuildInParallel=false` and
`UseSharedCompilation=false`, with the pinned SDK first in PATH, allowed the
build/test scan to proceed. Package pruning and StringTools support warnings were
preserved; no SDK pin or dependency was changed to hide them.

This is local synthetic evidence. VPS CPU/RAM/storage durability, proxy settings,
real provider quotas/latency, scheduled external email and long-running stability
remain external acceptance requirements. No deployment or live-provider request
was performed. Do not activate general signed/incremental mode until all writer
paths, the privacy decision and final verification are resolved.



<a id="source-evidence--approved-privacy-exception-and-final-foundation-acceptance"></a>

##### Approved privacy exception and final foundation acceptance

The developer approved the narrow exception in the review document during this
continuation. It verifies exact SQL accounting independently before permitting
owned-data reclamation under uncertain artifact integrity. Its purpose-bound,
authenticated receipt cannot carry a file plan or positive reservation. Successful
SQL purge publishes an incomplete checkpoint retaining the original artifact
manifest and audit timestamp. Unknown SQL, a wrong signed ledger total, invalid
control trust or unsafe pending operation cannot enter the exception. Credential
failure rolls back owned SQL rows and retains every copy. Ordinary storage work
remains blocked until a full check matches the original authenticated manifest;
no unknown file is adopted. The legacy unsigned missing-inventory compatibility
case permits verified SQL purge but remains explicitly untrusted until enrollment.

`g1c6-final-acceptance` passed **752/752 tests**, zero failures or skips:
Domain 150, Application 140, DataAccess 155, Infrastructure 225, Presentation 82.
This includes real signed process/backend failure recovery, exact accounting,
ownership/concurrency, removal and approved privacy behavior. The final interface
is explicit because NSubstitute/Castle could not invoke its earlier default generic
method; the former 751/752 run is preserved rather than relabeled a pass.
Pinned builds use no new C# warnings. Existing package-pruning/StringTools warnings
remain documented. `git diff --check` passes.

No current privacy-exception latency or allocation improvement is claimed. This
stage restores transaction/privacy correctness and successful offline scheduled
operation; the recorded process resource probe predates the privacy exception and
remains bound to its own frozen binaries. Full artifact scans are intentionally
retained. G1d's reductions, remaining lifecycle integration, broad combined-root
activation and external production acceptance are still pending.


Compatibility acceptance (`g1c6-postgres16`) passed all **155 DataAccess tests on
PostgreSQL 16.15**, zero failures/skips. Four strengthened HTTP regressions
(`g1c6-privacy-exact-postconditions`) also passed independent full SQL-accounting
recomputation after ordinary/uncertain-artifact credential rollback and successful
ordinary/privacy deletion. No counter was reset. These extra postcondition asserts
were added after the full 752-test run; production sources were unchanged.

Foundation verification was completed in the continuation ending 2026-10-26
(Asia/Baku). General G1c acceptance remains open for legacy conversion and combined
composition-root activation; G1d and G2–G7 have not been implemented in this stage.



<a id="source-evidence--g1c-follow-up-key-state-change-during-preparation"></a>

##### G1c follow-up: key-state change during preparation

A deterministic regression invalidated the key epoch during rendering and issued
a fresh epoch afterward. The former implementation captured only the fresh token,
so it incorrectly returned a publication capability (`g1c7-key-proof-red`: expected
exception absent). The proof now starts before rendering/decryption/encryption and
is checked before returning prepared inputs, on admission and at publication.
Local key creation can legitimately invalidate it; the existing bounded retry
prepares again after that independent key transaction completes.

`g1c7-key-proof-green` passes the new infrastructure regression and all 23 affected
PostgreSQL/Worker scenarios. A new real-key test starts with no protection keys,
creates exactly one, enters the final capture transaction once, publishes one
receipt and independently verifies exact accounting. Existing keys, revocation,
recovery and scheduling semantics remain covered. This is a security correctness
fix, not a measured latency/allocation improvement. The earlier resource probe
retains its original frozen binary/source manifest.


<a id="source-evidence--g1c-final-writer-integration-and-reservation-correction--2026-10-26"></a>

##### G1c final writer integration and reservation correction — 2026-10-26

Historical conversion now publishes through its own signed file-plan purpose and a
control-only SQL commit. It preserves the historical receipt and message bodies,
does not change the digest status or delivery time, and removes plaintext/partial
sources only through verified journaled cleanup. Complete temporary ciphertext is
reused after a process rebuild. An existing target belonging to another owner is
retained and rejected. The conversion, capture and removal purposes cannot be
substituted for each other during recovery.

Bare signed adapters reject uncoordinated writes. The real web and Worker roots
permit combined discovery/local capture only with an explicitly configured independent
integrity key. Positive concurrent-writer tests and negative missing-key startup
tests cover both roots. Quiet-period unsubscribe performs a full known-state proof
before its atomic consent update. Both provider adapters remain fixture-only in
these checks; no live provider or external email is involved.

`g1c8-final-verified` passed 768/768 checks. Its saved probe completed all 111
recipients, but the synthetic SQL container did not survive a session restart.
The preserved replay `g1c8-final-replay` exposed an intermittent stop during the
100-recipient cohort: 20 advanced and 80 stopped on the first tick; 79 new captures
and one already-captured receipt advanced on the second tick. This run is a failure
of uninterrupted first-tick acceptance and is not relabeled as passing. Two
stack-hook attribution controls completed without reproducing the stop; their
timings are diagnostic controls, not the final uninstrumented measurements.

A separate deterministic regression (`g1c9-physical-growth-red`) established that
reservation validation rejected a 4 KiB physical database increase with an unchanged
managed SQL revision. PostgreSQL can extend internal visibility/free-space maps
without a managed-row change. The coordinated reservation now charges such growth
against its existing allowance and cutoff, preserving exact SQL-revision equality,
artifact verification and filesystem headroom checks. Growth beyond the reservation,
changed SQL revisions and insufficient free space still fail closed. The original
intermittent stop's stack was not captured, so this is a demonstrated failure
mechanism rather than conclusive attribution of that particular run.

After the correction, `g1c9-full-acceptance` passes **769/769**, zero failures or
skips: Domain 150, Application 140, DataAccess 163, Infrastructure 232,
Presentation 84. `g1c9-postgres16` passes all **163 DataAccess checks on PostgreSQL
16.15**. Existing NU1510/StringTools build warnings remain; no new C# warnings were
introduced. The final source and frozen Worker DLL hashes are recorded privately in
`g1c9-source-binary-manifest.json`; raw logs/TRX remain under the ignored results
directory. No allocation or latency improvement is claimed for this correctness
stage. Full content scans remain active; G1d must establish its own comparable
before/after measurements and security acceptance.


Final uninstrumented probes `g1c9-final-a/b/c` use the same frozen Worker and one
explicit initial enrollment per fresh synthetic database. All three advance exactly
1/10/100 recipients on their first ticks; second ticks are empty. Each has 111
`LocalCaptured` digests, zero non-null `SentAt` values and 111 ciphertext files
(1,567,875 bytes). Independent full-row recomputation matches the respective ledger
values 5,439,962 / 5,439,953 / 5,439,889 bytes; differences are synthetic timestamp/
identifier serialization lengths, not counter resets. Physical database sizes are
12,523,199 / 12,588,735 / 12,596,927 bytes.

| Recipients | Process wall ms, median [range] | User CPU s, median | System CPU s, median | Peak RSS KiB, median [range] |
|---|---:|---:|---:|---:|
| 1 | 4,526.85 [4,276.66–4,778.35] | 3.01 | 0.24 | 168,568 [168,524–177,080] |
| 10 | 7,034.59 [6,983.50–7,083.94] | 5.21 | 0.59 | 180,604 [180,360–180,980] |
| 100 | 32,152.56 [32,100.29–32,300.76] | 31.62 | 6.47 | 217,712 [215,516–218,528] |

Wall time includes process startup, both ticks and their one-second interval. CPU
can exceed wall time across threads. These are isolated local synthetic runs on
PostgreSQL 18.6, not representative VPS workloads. Compared with stopped pre-G1c
controls, the improvement is successful correct completion; failure wall times are
not a throughput baseline. Earlier successful checkpoints are different binaries
and are not used to claim an optimization. End-to-end allocation, network transfer
and real delivery were not measured here; no live requests were made. Full artifact
scans remain active and expensive. G1d must measure changed/unchanged content,
metadata allocation and periodic audit pauses before claiming its targeted gains.

Each final run preserves results, GNU-time resources, read-only SQL verification
and an evidence hash manifest excluding the synthetic key and database identifier.
`g1c9-final-resource-summary.json` preserves all resource samples. The database
container can be discarded without losing these checks. Private source/DLL hashes
bind the exact measured code; the subsequent commit changes documentation only.
The approved plan remains sequential: G1d, G2 exports/archive indexing, G3 auth
contention, G4 revisions/quotas, G5 scoring/pools, G6 HTTP/recording, G7 combined
resource and deployment acceptance. No external acceptance has been completed here.



<a id="source-evidence--g1d0-bounded-full-scan-hashing-incremental-mode-remains-pending"></a>

##### G1d0: bounded full-scan hashing (incremental mode remains pending)

A new dependency-free [artifact inventory control harness](#source-inventory-control)
references frozen Release assemblies rather than rebuilding a baseline. Its five
shapes cover empty storage, 100/512 MiB single files, and 100 MiB distributed across
1,000/10,000 files. Enrollment is separate; each process uses two warmups and five
measured acquire/reserve/pre-write/result/publish cycles. Exact fixture bytes are
checked. The first strict run measured 535,261,320 allocated bytes for five
10,000-file passes and approximately five times the payload in character reads.
This is application allocation traffic, not retained heap or peak memory.

The scanner previously constructed a buffered stream and hashing operation for
each file at every boundary. It now shares one rented 64 KiB buffer and one file
hasher across a complete inventory, disables the redundant stream buffer and resets
the hash between files. The buffer is cleared before returning it to the pool.
Reads are bounded by the measured file length; short reads, changed final stream
lengths and changed filesystem lengths fail. Cancellation is checked for every
entry, including empty files. The categorized path/length/mtime/SHA-256 fingerprint
format is unchanged. Every full content pass, symlink/path/count/byte limit and
control authentication remains. No migration, configuration activation, new package
or change to the storage-integrity detection window is required.

The initial pooled-buffer trial reduced allocations but regressed 10,000-file wall
time (median 995.61 → 1,326.39 ms across its 15 samples). Those trials remain as
`g1d0-before/after-b/c/d`; they are not relabeled as final. The final implementation
avoids an additional EOF read per file using the already measured length, then
checks both observed byte count and final lengths. A dedicated compatibility test
covers empty files before and after nonempty files, buffer boundaries and multiple
blocks against independently computed legacy SHA-256 fingerprints. A same-size,
same-mtime edit still stops admission. Existing cancellation, symlink, tamper,
recovery and storage tests pass in the focused 233-check infrastructure run.

The strict allocation step does not implement authenticated immutable manifest
parts, verified reads, periodic full-audit deadlines or incremental admission.
P01 remains open until the rest of approved G1d and its complete workload/security
acceptance pass. Metadata work remains proportional to file count. The full-scan
read-traffic target is intentionally unchanged at this stage; SQL and network costs
are outside this storage-only comparison. End-to-end delivery resource probes above
remain bound to their earlier G1c binaries.


Final comparison (`g1d0-final-before/after-a/b/c`) alternates three pairs of the
same frozen harness, SDK/runtime 10.0.12, workstation GC and 20 reported processors.
Each table value is the median of all 15 warm samples; all samples are retained.

| Payload / files | Wall ms before → after | Allocated MB before → after | Process CPU ms before → after |
|---|---:|---:|---:|
| empty / 0 | 3.73 → 4.01 | 0.2123 → 0.2133 | 3.35 → 3.53 |
| 100 MiB / 1 | 423.65 → 424.47 | 0.2696 → 0.2266 | 919.69 → 968.00 |
| 512 MiB / 1 | 1,749.67 → 1,553.70 | 0.2676 → 0.2245 | 3,822.05 → 2,981.78 |
| 100 MiB / 1,000 | 568.34 → 433.09 | 53.7562 → 9.6355 | 1,408.79 → 834.76 |
| 100 MiB / 10,000 | 1,205.24 → 888.08 | 535.2635 → 94.0265 | 3,410.94 → 1,782.77 |

For 10,000 files this is **82.4% less allocation, 26.3% lower median wall time and
47.7% less measured process CPU** in these local controls. The 1,000-file allocation
reduction is 82.1%. Empty storage adds about 960 allocated bytes and 0.28 ms at the
median; single-file 100 MiB wall time is effectively unchanged and CPU increases
5.3%. The change is not a universal latency improvement. Host variance is large:
10,000-file run medians are 1,012.69 / 2,158.57 / 1,205.24 ms before and 901.03 /
1,122.90 / 771.20 ms after. No confidence interval or VPS prediction is claimed.

All warm physical-read counters are zero. Application reads remain about 500 MiB
for 100 MiB payloads and 2,560 MiB for 512 MiB payloads: every validation boundary
still hashes every payload. These are warm-cache CPU/allocation controls, not disk
latency. Process CPU can exceed wall time across threads. The cumulative process
peak working-set medians at the final shape are 109,121,536 → 101,343,232 bytes;
previous shapes contribute to that high-water mark, so this is not an incremental
RSS or retained-memory improvement claim.

Explicit enrollment is also measured separately (three samples): at 512 MiB its
median is 904.35 → 651.03 ms; at 10,000 files, 771.07 → 524.80 ms and 214,260,632 →
37,895,304 allocated bytes. It performs two content scans and remains outside the
warm-cycle table. It is not a cold process or startup-recovery benchmark.

The final source/DLL manifest, identical harness hashes, raw JSONL and summary are
preserved privately as `g1d0-final-frozen-manifest.json`,
`g1d0-final-evidence-manifest.json` and `g1d0-final-summary.json`. The public harness
also passes its missing-frozen-assemblies and invalid-argument guards; all scratch
fixtures are removed after completion. No SQL rows, schema, provider/API calls or
network transfer are part of these storage-only measurements. Final full application
regression acceptance (`g1d0-final-acceptance`) passes **770/770**, zero failures or
skips: Domain 150, Application 140, DataAccess 163, Infrastructure 233 and
Presentation 84. The five-project TRX gate and both offline Node checks pass. No
new C# or analyzer warning is present. The measured after Infrastructure DLL hash
matches the final tested DLL. G1c remains bound to commit `383083b`; these resource
measurements cover the subsequent hashing-only change. Rollback needs no schema or
artifact-format conversion because the fingerprints and serialized controls are
unchanged. Production performance and power-loss durability remain unverified.



<a id="source-evidence--g1d1--strict-immutable-manifest-persistence-and-receipt-anchoring"></a>

##### G1d1 — strict immutable-manifest persistence and receipt anchoring

The [integration guide](#source-manifest) records the format,
accounting, exact publication/recovery, privacy, migration and rollback contracts.
The new directory is opt-in; no application configuration enables it by default.
Full content scans remain. This is a protocol prerequisite, not P01 incremental
activation or a claim of reduced application reads.

The new per-category parts contain opaque path/content hashes and stat data. Live
and retired parts are charged explicitly; retirement remains authorized and
recoverable after the ordinary intent is removed. A signed candidate in the
existing inventory temporary slot pins interrupted enrollment. Changed inputs,
unexpected metadata, invalid hashes or unsafe paths fail closed. Entry comparison
proves changes are confined to the signed copy plan. Metadata on a separate
filesystem must have its own reservation headroom. The approved privacy path can
preserve corrupt parts as untrusted while committing verified owned SQL reclamation;
restoration requires full agreement with the original manifest.

`g1d-control-replay-red` reproduced a filesystem-only rollback after a control-only
removal: the old initial signed checkpoint, payload/timestamp and metadata were
restored while the SQL receipt remained current. Admission incorrectly succeeded.
New checkpoints authenticate a domain-separated SHA-256 of the exact SQL receipt
(or null state). The regression now rejects that replay through both coordinated
admission and signed read-only preflight, with and without manifest parts. Legacy
post-commit checkpoints can upgrade only when unambiguous. An old null-operation
maintenance root with an existing receipt requires explicit verified maintenance
(`storage_commit_anchor_missing`); the application does not guess a new anchor.

The first Worker matrix caught a preflight compatibility error: fencing all legacy
acquisition also blocked the application's legitimate read-only generation probe.
The adapter now returns a coordinated read-only lease for signed inventories and
rejects reservations through it. A separate regression
(`g1d1-signed-preflight-red`) observed `database_inventory_changed` for a known 4 KiB
physical increase under unchanged SQL state. Coordinated preflight now charges that
physical growth, checks the exact receipt and preserves cutoff/space protections.
The green storage matrix passes. Pending owner recovery remains a bounded stopped
result, with no owner wait under the preflight leases.

An earlier broad run (`g1d1-full-acceptance`) stopped at 170/171 DataAccess checks:
the combined scheduled generation/export/opt-out/deletion case reported only
`stopped`. Its original reason was not captured. An isolated repeat passed; the
assertion now includes the bounded reason. The deterministic preflight failure is
a proven mechanism, not conclusive attribution of that particular stopped run.
The original failure evidence remains intact.

The first complete acceptance (`g1d1-final-full-scan`) passed **813/813**, zero failures or skips:
Domain 150, Application 140, DataAccess 171, Infrastructure 268, Presentation 84.
Both offline Node checks and the five-project TRX gate pass. All **171 DataAccess
checks also pass on PostgreSQL 16.15** (`g1d1-postgres16`). No new C# or analyzer
warnings were introduced; existing pruning/StringTools and the pre-existing
xUnit2031 warning remain. Focused matrices include 20 legacy/manifest Worker
capture, restart, concurrent discovery, export/deletion and historical conversion
cases, plus both actual child-process scheduler modes. Storage tests cover partial
enrollment, changed pinned inputs, unknown/corrupt metadata, receipt migration,
filesystem-only replay, interrupted/already-completed/corrupt retirement, separate
metadata-volume headroom and privacy quarantine/restoration.


<a id="source-evidence--storage-only-protocol-costs"></a>

###### Storage-only protocol costs

The identical frozen harness measures `bd26135` full scans, the new receipt-anchored
format without manifests, and the new strict manifest format. All use runtime
10.0.12, workstation GC, 20 reported processors, the same five shapes/two warmups
and five measured cycles. These are **one sequence of five-sample controls per
mode**, not alternating multi-pair optimization acceptance. Host variance is large;
do not attribute their latency ratios to the receipt field or claim a speedup.
The files are `g1d1-final-old-full.jsonl`, `g1d1-final-anchored-full.jsonl` and
`g1d1-final-manifest-full.jsonl`.

| Payload / files | Original full ms / allocated MB | Anchored full ms / allocated MB | Manifest full ms / allocated MB |
|---|---:|---:|---:|
| empty / 0 | 3.80 / 0.2133 | 3.56 / 0.2150 | 14.15 / 0.7584 |
| 100 MiB / 1 | 442.49 / 0.2260 | 585.31 / 0.2286 | 434.79 / 0.7724 |
| 512 MiB / 1 | 1,330.00 / 0.2245 | 1,766.53 / 0.2263 | 1,960.52 / 0.7701 |
| 100 MiB / 1,000 | 404.78 / 9.6355 | 682.38 / 9.6373 | 585.64 / 12.8362 |
| 100 MiB / 10,000 | 1,086.17 / 94.0274 | 1,130.70 / 94.0303 | 1,173.87 / 126.1282 |

For 10,000 files the corresponding measured process CPU medians are
2,195.59 / 2,264.79 / 2,323.19 ms. At 512 MiB they are
2,650.18 / 3,361.79 / 3,524.86 ms. Full manifests add allocation and metadata IO:
100 MiB/10,000 files reads 524,298,696 → 528,315,364 character bytes compared with
anchored full scans. The 512 MiB payload still causes about 2.684 GB of reads.
All physical-read counters are zero in these warm controls. There is no network or
SQL workload in this harness. Parts occupy 96 bytes empty, 176 bytes for either
single-file fixture, 80,096 bytes at 1,000 files and 800,096 bytes at 10,000 files;
the harness independently matches those values against actual files. Cumulative
peak working sets are retained in raw samples and are not incremental-memory
measurements. This added strict-mode work must be included in later incremental
comparisons, periodic audits and memory-budget checks.


<a id="source-evidence--actual-finite-worker-with-manifests"></a>

###### Actual finite Worker with manifests

`g1d1-delivery-after` uses the exact final frozen Worker, fixture-only providers and
one explicit initial enrollment across all three cohorts. All **111 recipients**
advance on their first ticks; repeated ticks are empty. There are 111 ciphertexts
(1,567,875 bytes), 111 `LocalCaptured` digests and zero non-null `SentAt` values.
Full independent SQL accounting matches **5,439,948 bytes**; physical database size
is **12,637,887 bytes**. The final six metadata parts hash correctly, contain 111
copy entries and occupy **8,976 separately charged bytes**. No retired or temporary
parts remain. No reconciliation occurred between recipients or cohorts.

| Recipients | Process wall ms | User / system CPU s | Peak RSS KiB |
|---|---:|---:|---:|
| 1 | 4,475.92 | 3.00 / 0.24 | 170,032 |
| 10 | 7,285.00 | 5.50 / 0.76 | 184,144 |
| 100 | 32,399.42 | 26.53 / 7.60 | 212,340 |

These are single local process runs including startup and the one-second tick
interval. End-to-end allocation and network transfer were not measured. They show
successful offline scheduled capture, not a throughput improvement or external
email delivery. The prior G1c trials use different frozen binaries and are not
represented as matched performance controls. No live requests, deployment or VPS
acceptance occurred. Sources, exact DLLs, raw JSONL, TRX, SQL verification and
metadata hash evidence remain in private ignored results. Verified consumption,
bounded shared caching, the approved 30/60-second audit policy and incremental
admission remain required before closing G1d/P01.


A final ownership regression (`g1d1-unknown-metadata-red`) found that a recovery
retry could delete an unplanned temporary file named after an unchanged metadata
part. No exception was thrown. The writer now validates directory membership before
writing, permits only the exact newly introduced part paths and never recreates a
missing unchanged part. The unknown temporary remains untouched. The final broad
scan (`g1d1-final-ownership-scan`) passes **814/814**, zero failures or skips:
Domain 150, Application 140, DataAccess 171, Infrastructure 269, Presentation 84.
Earlier 813-check controls remain bound to their own source/binaries. Final source,
DLL and repeated resource/delivery evidence use the `g1d1-verified-*` labels.


Final PostgreSQL 16 verification first recorded an intermittent failure in the
existing `FailureAfterSqlCommit_ReturnsVerifiedExistingReceiptOnFreshAttempt` case
(`g1d1-final-postgres16`: 170/171). Its assertion exposed only `stopped`, not the
reason. The Worker helper now records bounded stop reasons for diagnosis, without
logging payloads or credentials. The full retry
(`g1d1-postgres16-attribution`) passes all **171/171**, and three isolated repeats
of that exact fault case also pass (`g1d1-commit-recovery-repeat-1/2/3`). The failed
trial is preserved. The original cause remains untraced; a rare bounded stop during
injected post-commit failure remains an investigation item for G7. No assertion
was weakened, no rollback/recovery guarantee is claimed from the passing retry,
and this stage does not establish long-running or production delivery acceptance.


The ownership-hardened binary was re-frozen and remeasured separately
(`g1d1-ownership-verified-anchored-full.jsonl` and
`g1d1-ownership-verified-manifest-full.jsonl`). Five warm samples per shape give:

| Payload / files | Anchored full ms / allocated MB | Manifest full ms / allocated MB |
|---|---:|---:|
| empty / 0 | 3.43 / 0.2150 | 13.58 / 0.7584 |
| 100 MiB / 1 | 479.79 / 0.2280 | 373.10 / 0.7721 |
| 512 MiB / 1 | 1,341.16 / 0.2263 | 1,613.81 / 0.7700 |
| 100 MiB / 1,000 | 404.17 / 9.6373 | 891.73 / 12.8361 |
| 100 MiB / 10,000 | 1,399.91 / 94.0298 | 1,056.84 / 126.1280 |

At 10,000 files, CPU is 2,888.10 → 2,083.71 ms and character reads
524,297,672 → 528,315,363 bytes. At 512 MiB, CPU is 2,667.94 → 3,034.69 ms;
application reads remain about 2.684 GB. All physical-read counters are zero. These
single-sequence observations retain the same host-variance and protocol-overhead
limits; they are not evidence of incremental savings. Metadata accounting remains
96 / 176 / 176 / 80,096 / 800,096 bytes and is independently matched to files.

A fresh final Worker probe (`g1d1-ownership-delivery-after`) advances all **111**
recipients without intermediate reconciliation, preserves empty repeated ticks,
and leaves 111 ciphertexts (1,567,875 bytes), zero sent timestamps and exactly six
live parts (8,976 bytes, 111 copy entries), with no retired/temporary parts.
Independent SQL accounting matches **5,440,048 bytes**, physical database size is
**12,859,071 bytes**, and the signed checkpoint receipt anchor matches a SHA-256
computed directly in SQL. Finite 1/10/100 process times are
**4,675.55 / 7,181.52 / 32,854.02 ms**; user/system CPU is
3.94/0.29, 5.51/0.74 and 28.85/6.92 seconds, with peak RSS
177,752 / 183,376 / 216,468 KiB. These remain single synthetic offline process
runs; no performance percentage, retained-memory gain or production acceptance is
claimed. The final hashes are in `g1d1-ownership-verified-source-binary-manifest.json`;
SQL, manifest and ciphertext verification are retained with that probe.


<a id="source-evidence--g1d2--verified-consumption-still-full-scans-2026-10-27"></a>

##### G1d2 — verified consumption, still full scans (2026-10-27)

This is a correctness prerequisite for D1, not a completed P01 optimization.
Published email and local recording readers now hash the exact buffer consumed
against the authenticated manifest entry before parsing/decryption. The root's
independent exact SQL receipt/full revision is checked, and a second authenticated
root read rejects concurrent publication. The reader takes no mutation lease,
performs no recovery and never enrolls observed state. Signed pending file plans
continue to prove new/recovered ciphertext outside the predecessor manifest.
See `performance-manifest-integration.md` for legacy, privacy and rollout behavior.

Local verification:

- Baseline selected Infrastructure checks: **105/105** (`g1d2-baseline`). New
  targeted checks: **111/111**, followed by **277/277** Infrastructure checks and
  all **20** real Worker/capture/export/delete integration cases.
- Final isolated full scan: **823/823**, zero failures/skips (Domain 150,
  Application 140, DataAccess 171, Infrastructure 278, Presentation 84), both Node
  checks and the five-project TRX gate. No new C#/CA warnings; pre-existing package
  and xUnit warnings remain. Results: `g1d2-isolated-final-scan`.
- PostgreSQL **16.15**: all **171 DataAccess** and **278 Infrastructure** checks
  pass (`g1d2-isolated-postgres16`). No migration or dependency change.
- New regressions cover buffer tampering with an unchanged original on disk,
  same-size/same-time edits, unlisted paths, SQL receipt/revision mismatches,
  concurrent publication, non-adoption of pending commits, part corruption and
  configuration removal, cancellation and no SQL/mutation lease for legacy reads.
  An archive test uses a valid new encryption of identical plaintext to prove
  manifest rejection before export; intact copy verification after privacy SQL
  reclamation is proved not to restore global write trust.
  Final review added an explicit part-permission assertion to the existing
  downgrade case; all seven consumed-artifact cases pass again in
  `g1d2-final-permission-guard`. Production sources/binaries are unchanged from
  the full scan and frozen measurement bindings.

Retained failure and environment isolation:

`g1d2-postgres16` passed 170/171; both concurrent captures in the failed legacy
case stopped with **`email_sink_tracked`**, now present in TRX output. A sandboxed
read showed an empty read-only `/tmp/.git` marker while an unrestricted read
showed it absent. Concurrent namespace tooling can therefore trigger the existing
Git safety guard for `/tmp` fixtures. The private test runner now explicitly sets
`TMPDIR` to each run's mode-0700 ignored diagnostics directory; the final complete
PostgreSQL 16 and PostgreSQL 18 scans pass under that isolation. The production
Git-exclusion guard is retained, and the failed trial is not replaced or hidden.
This attributes a demonstrated guard mechanism; it does **not** establish that the
older injected post-commit `stopped` outcome had the same cause. That G7 item
remains open. An earlier 822-test scan preceded the last privacy regression and
is retained separately (`g1d2-final-scan`).

Reader cost control:

The committed `diagnostics/benchmarks/ArtifactConsumption` harness is compiled
once against frozen G1d1 assemblies and used with the frozen before/after builds.
Three alternating process pairs use 60 warm-up reads and 100 measured reads per
shape. Runtime is .NET **10.0.12**, workstation GC, 20 visible processors; no
concurrent tests, builds or profiler runs during the final comparison. SQL state
is supplied in memory: real query latency, database CPU/buffers and lock behavior
are **not measured** by this component control. The normal verifier adds exactly
one SQL-state request per consumed buffer. Fixture setup/enrollment is excluded
from per-read latency/CPU/allocation. All process physical-read counters are zero
in these warm-cache trials.

| Buffer / category entries | Median read ms, before → verified | Allocated bytes/read, before → verified | CPU ms/read, before → verified | Character reads bytes/read, before → verified |
| --- | ---: | ---: | ---: | ---: |
| 8 KiB / 1 | 0.1103 → 2.7331 | 15,170 → 221,938 | 0.227 → 3.837 | 8,194 → 12,166 |
| 8 KiB / 1,000 | 0.1121 → 4.3656 | 15,166 → 301,886 | 0.196 → 5.859 | 8,194 → 92,125 |
| 8 KiB / 10,000 | 0.1997 → 2.2621 | 15,166 → 1,022,561 | 0.276 → 4.155 | 8,194 → 812,868 |
| 1 MiB / 1 | 0.4471 → 2.1370 | 1,057,646 → 1,263,723 | 0.663 → 4.022 | 1,049,347 → 1,053,314 |

These are medians of the three process medians, with substantial host variance.
Verified process medians range 1.0667–3.1367, 2.2163–5.9973, 2.1193–4.5272 and
1.6679–4.0535 ms respectively; the non-monotonic file-count timings are not a
scaling claim. Raw samples/GC counters are retained. Process peak RSS includes
fixture enrollment and prior shapes: before ranges approximately 54–99 MiB,
after approximately 65–84 MiB; this does not establish retained-memory savings.
The known uncached costs are an added requirement for the next bounded-cache
stage, and the archive's cross-owner scan remains a G2 issue. No network requests
or transfers occur in this component test.

Final comparison: `g1d2-isolated-read-measurements`, including source, assembly and
harness SHA-256 bindings in `source-binary-manifest.json`. The earlier
`g1d2-read-measurements` trial potentially overlapped the short Node checks and
is retained but excluded from the table. Reproduction uses the public harness
README and private recipe `recipes/g1d2_consumption.py`; the latter refuses to
overwrite evidence or reuse a changed frozen build.

Finite scheduled-delivery regression:

`g1d2-delivery-after`, using the final frozen Worker, advances every **1/10/100**
recipient without intermediate reconciliation and leaves each repeat tick empty.
Independent SQL recomputation matches **5,440,140 logical bytes**, physical database
size **12,646,079 bytes**. There are exactly **111 LocalCaptured digests**, zero
sent timestamps and 111 ciphertexts totaling **1,567,875 bytes**. Six live manifest
parts total **8,976 bytes**, contain 111 copy entries and no retired/temporary
parts. An independent binary-entry reconstruction verifies every path/content
hash, length and modification tick; the SQL-computed receipt SHA-256 matches the
checkpoint anchor. Metadata is in the separately private
`g1d2-delivery-manifests` directory. Verification recipes and SHA-256 evidence
manifest are retained without committing private payloads or keys.

Finite process times are **5,429.59 / 8,786.06 / 32,496.67 ms**; user/system CPU is
3.81/0.34, 7.16/0.90 and 28.11/6.97 seconds; peak RSS is
169,200 / 184,828 / 213,756 KiB. These are single synthetic offline process runs,
including startup and the tick interval. They establish local delivery mechanics,
not latency improvement, real email delivery, power-loss durability, long-running
stability or target-VPS acceptance. Periodic auditing, bounded metadata caching,
complete writer/reader coverage and incremental admission remain outstanding.


<a id="source-evidence--g1d3--bounded-strict-consumed-read-metadata-cache-2026-10-27"></a>

##### G1d3 — bounded strict consumed-read metadata cache (2026-10-27)

Root cause and scope: G1d2 reconstructed and structurally validated an entire
category part on every consumed read. A 10,000-entry part is 800,016 bytes; its
repeated allocation adds LOH/GC pressure. A shared process-local cache now retains
only validated large live parts for one authenticated storage ID, checkpoint
hash/generation and metadata directory. The public format and all five mutation
validation boundaries are unchanged. This stage does not implement ordinary
mutation-admission metadata snapshots, the 30/60-second auditor/deadline, or
incremental activation.

The retained bound is **48,000,096 bytes** (six maximum-size parts), with at most
one large cold load in progress. Retained metadata plus that whole-part load is
56,000,112 bytes, below 64 MiB. Scratch buffers, caller data and runtime/request
memory are additional; this is not a whole-process memory guarantee. Cache hits
still hash the entire on-disk part using bounded pooled scratch, then the actual
consumed buffer. Root authentication, the independent current SQL receipt/revision,
private paths/permissions and the second-root publication fence remain. Cached
arrays are not returned to callers; only the matching entry value escapes the
cache gate. A forced-GC regression proves that a suspended warm verifier cannot
retain an evicted backing array. No application data, keys or account IDs are
stored in this cache; the existing opaque binary metadata is reused in memory.

The first frozen trial (`g1d3-read-measurements`) cached every part. Its large-part
allocation result was useful, but a one-entry part added about 6.6 KB/read. The
final policy retains parts only when larger than **65,536 bytes**; tiny reads use
the original bounded loader outside the gate. Exactly 819 entries occupy 65,536
bytes, and 820 entries cross the boundary; both are tested. Every read still
observes the authenticated generation so old large parts can be pruned even when
the current requested part is tiny. Old delayed generations cannot displace the
newest cached generation. No operator tuning parameter or deployment switch was
added. No schema, migration, dependency or persisted-format change is required.

Correctness/compatibility evidence:

- Before changes, selected read/archive regressions **8/8** pass (`g1d3-baseline`).
  Initial cache/storage/archive target **77/77** and initial five-project scan
  **829/829** pass; those precede the measured minimum-size refinement.
- Final scan **834/834**, zero failures/skips: Domain 150, Application 140,
  DataAccess 171, Infrastructure 289, Presentation 84. Both offline Node checks
  and the TRX gate pass (`g1d3-sized-final-scan`). No new C#/CA warnings; existing
  package/xUnit warnings remain. PostgreSQL **16.15** passes all **171 DataAccess**
  checks (`g1d3-sized-postgres16`).
- Cache regressions cover 25 concurrent callers sharing a single cold load while
  verifying each warm read, generation pruning/reuse, delayed older readers,
  different storage identities, failed loads, independent waiter cancellation,
  failed warm checks, the six-maximum-part bound, evicted-array collection, direct
  tiny-reader concurrency and the exact size threshold. Existing checkpoint,
  receipt-anchor, privacy, capture/removal/conversion and archive suites remain.
- A real warm large-part read rejects same-size/same-time metadata corruption.
  Final review extends that case to changed permissions and a missing metadata
  file; the focused cache/consumption regressions pass **18/18** again in
  `g1d3-final-read-guards`. Production code is unchanged from the final full scan
  and frozen comparisons.
- DI uses the shared singleton cache across scoped adapters. Direct callers retain
  a bounded instance cache. The existing four-argument constructor remains
  available so the identical old-compiled measurement harness can run against
  both frozen builds. Restart discards only memory; rollback to G1d2 needs no
  on-disk conversion. Do not downgrade past the earlier signed-format constraints.

Matched component measurements:

`g1d3-sized-read-measurements` compares frozen G1d2 and final G1d3 assemblies with
one identical compiled harness. Both run `PERF_CONSUMED_VERIFY=1`; neither omits
integrity checks. Each concurrency setting uses three alternating fresh-process
pairs, 60 serial warm-ups and 100 batches per shape: 100 reads at concurrency one,
2,500 reads at concurrency 25. .NET **10.0.12**, workstation GC and 20 visible
processors; no concurrent test/build/profiler runs. Hash bindings are in
`source-binary-manifest.json`. Raw results include every batch, process variation,
GC collections, process I/O counters and peak RSS. First-trial outputs and binaries
are retained separately; they are not substituted for the final table.

| Readers / buffer / entries | Median batch ms before → after | Allocated bytes/read before → after | CPU ms/read before → after |
| --- | ---: | ---: | ---: |
| 1 / 8 KiB / 1 | 1.4650 → 2.4224 | 220,705 → 221,261 | 1.9609 → 2.4773 |
| 1 / 8 KiB / 1,000 | 3.1419 → 3.4489 | 300,654 → 227,434 | 3.3715 → 4.6674 |
| 1 / 8 KiB / 10,000 | 2.6267 → 3.7374 | 1,021,328 → 227,503 | 4.0751 → 4.0912 |
| 1 / 1 MiB / 1 | 2.2787 → 4.1629 | 1,262,433 → 1,263,226 | 2.9324 → 5.0624 |
| 25 / 8 KiB / 1 | 10.4584 → 12.5989 | 220,948 → 221,451 | 4.8123 → 6.0713 |
| 25 / 8 KiB / 1,000 | 11.8269 → 11.6469 | 300,617 → 228,038 | 5.6419 → 5.8285 |
| 25 / 8 KiB / 10,000 | 15.8207 → 6.1381 | 1,023,015 → 227,323 | 6.1546 → 3.2496 |
| 25 / 1 MiB / 1 | 14.6334 → 15.4238 | 1,261,718 → 1,262,358 | 5.4528 → 5.6425 |

The measured target is allocation/GC reduction for large parts: approximately
**24%** less allocation at 1,000 entries and **78%** at 10,000. For 10,000 entries,
sequential generation-2 collection counts fall **25 → 0** per process; the
25-reader counts fall **103–148 → 0**. That concurrent shape records about 61%
lower median batch latency and 47% less CPU/read. These are local synthetic
controls, not an application throughput or production p95 claim.

Sequential latency and CPU are mixed and several medians regress; no universal
latency/CPU gain is established. Host/process variance is substantial, including
in direct tiny-part controls. The 10,000-entry 25-reader process medians range
12.6097–43.0029 ms before and 5.8797–6.6637 ms after; sequential 10,000-entry
medians range 2.1343–2.9663 ms before and 2.2825–4.1547 ms after. The serial result
therefore does not satisfy a latency improvement criterion. Tiny-part allocation
adds only approximately 0.1–0.3% in these controls after the size refinement.
Remaining fixed root-proof costs and strict metadata hashing remain limitations;
P01's broader mutation latency/read-traffic acceptance is still outstanding.

Character-read traffic is effectively unchanged: approximately 12,166 bytes/read
for the small single-entry shape, 92,100 for 1,000 entries, 812,100–812,900 for
10,000, and 1,053,300 for the sequential 1 MiB shape. Every physical-read counter
is zero in these warm-cache trials. Cached parts are still fully read/hashed;
there is no metadata-I/O reduction claim. Actual SQL latency/CPU is not measured:
state is supplied in memory. The harness asserts exactly **one state request per
read in both builds**. The production SQL implementation, accounting triggers,
transactions and WAL behavior are unchanged by this memory-only cache. No provider
or network requests/transfers occur in the component control.

RSS includes fixture enrollment and earlier shapes, so it does not isolate
retained-cache memory. Sequential process peaks span approximately 65–95 MiB.
For the 10,000-entry concurrent shape, peaks are 153–211 MiB before and 95–96 MiB
after; later 1 MiB shapes bring process peaks to about 221 MiB. This is not an
RSS cap or a universal retained-heap saving. The explicit cache bound and forced
collection tests establish its owned-reference policy separately.

Finite scheduled-delivery regression:

The final frozen Worker in `g1d3-delivery-after` advances all **111** recipients
across the 1/10/100 cohorts without intermediate reconciliation; repeat ticks
are empty. Independent SQL recomputation matches **5,440,005 logical bytes**;
physical size is **12,621,503 bytes**. There are 111 `LocalCaptured` digests, no
sent timestamps, and 111 ciphertexts totaling **1,567,875 bytes**. Six live parts
total **8,976 bytes** with 111 copy entries and no retired/temporary parts. Every
binary path/content hash, size and modification tick is reconstructed independently;
the SQL-computed receipt anchor matches the checkpoint. Metadata and proof files
are retained privately with `g1d3-delivery-manifests` and the existing verification
recipes; no private key or ciphertext is committed.

Finite 1/10/100 process times are **4,374.07 / 7,483.18 / 38,059.08 ms**; user/system
CPU is 2.86/0.24, 5.85/0.82 and 33.82/8.17 seconds; peak RSS is
168,836 / 183,384 / 219,768 KiB. These single offline runs establish continued local
delivery mechanics, not a scheduling speedup, long-running reliability, real email
sending or VPS acceptance. The older injected post-commit stopped result remains
unattributed. Periodic audit/deadline enforcement, complete reader/writer coverage,
mutation-admission caching, incremental activation and G2–G7 remain outstanding.



<a id="source-evidence--g1d4--internal-metadata-snapshot-foundation"></a>

#### G1d4 — Internal metadata snapshot foundation

Baseline `ecba945`; local verification on 2026-10-27. This stage prepares the
approved D1 path but **does not enable metadata admission**. Production writes,
recovery and privacy trust restoration retain full content scans. No migration,
new configuration switch, provider call or dependency change is introduced.

The existing five validation boundaries repeatedly open and hash all payloads.
The new internal scanner walks the same canonical roots and sorted directory
order, finds expected entries in each immutable category part, checks lengths,
modification ticks and counts, and reconstructs the existing fingerprint using
expected content hashes. Missing, extra, renamed or symlinked paths stop; aggregate
payload limits remain. Metadata namespace membership, permissions, sizes and hashes
remain checked. Pending recovery and unreconciled inventories stop without writing
control state. Neither accounting nor the completed-audit timestamp is refreshed.

A synchronous span visitor pins one cache part while traversing its category.
No byte array is exposed; the existing 48,000,096-byte retention bound and one
maximum-part in-flight allowance remain. Warm parts are still hashed on disk once
per category. Consumed reads use the same visitor and retain their independent SQL,
root and consumed-buffer checks. A failed visitor releases the gate and cannot
make a later read skip content verification.

An explicit regression changes payload bytes while restoring length and timestamp:
metadata comparison passes, but production admission and consumed-byte verification
reject the replacement. This is the expected limit of metadata checking. A future
caller must hold coordinated admission, validate configuration, authenticate the
root/SQL receipt and establish current process-local full-audit trust. The scanner
cannot establish that trust. Full-audit startup, 30-second refresh, 60-second expiry,
failed/uncertain audit handling and uncovered writer/reader integration remain
mandatory before activation. The category traversal holds the shared cache gate;
its effect on concurrent consumed readers also remains to be measured.


<a id="source-evidence--comparable-scanner-controls"></a>

##### Comparable scanner controls

The checked-in `ArtifactInventory` harness now supports
`PERF_ARTIFACT_MANIFESTS=1 PERF_ARTIFACT_VERIFICATION=full|metadata`. Both modes use
one frozen final Release build: two warmups and five measured cycles, each cycle
calling its actual helper five times on unchanged enrolled synthetic artifacts.
Three independent process pairs alternate full/metadata order. The original
`boundaries` mode is unchanged. Full/metadata controls exclude root authentication,
SQL state/receipt reads, transaction/lock admission, checkpoint publication,
periodic audits and recovery. The metadata helper additionally validates
configuration and hashes the supplied in-memory checkpoint; those costs remain in
the measurements. These are scanner costs, not complete storage-phase acceptance.

SDK 10.0.401/runtime 10.0.12, Linux, workstation GC, 20 reported processors, warm
filesystem cache. Medians below are the median of each process's five-sample
median; units are per **five validations**, MB means decimal megabytes.

| Payload/files | Wall ms full → metadata | CPU ms full → metadata | Allocated MB full → metadata | Application read MB full → metadata |
| --- | ---: | ---: | ---: | ---: |
| Empty | 9.209 → 15.280 | 11.846 → 18.253 | 0.631 → 1.129 | 0.000692 → 0.000692 |
| 100 MiB / 1 | 502.158 → 15.278 | 1027.258 → 18.377 | 0.645 → 1.140 | 524.289 → 0.001118 |
| 512 MiB / 1 | 1518.694 → 14.564 | 3028.032 → 16.382 | 0.644 → 1.140 | 2684.356 → 0.001122 |
| 100 MiB / 1,000 | 506.842 → 72.718 | 1004.244 → 115.456 | 13.110 → 10.551 | 524.690 → 0.401748 |
| 100 MiB / 10,000 | 1087.495 → 403.537 | 2141.440 → 414.094 | 130.398 → 94.562 | 528.313 → 4.010272 |

Populated fixtures read over 99% fewer application bytes; expected payload bytes
are not opened by the metadata scanner. This does not eliminate manifest I/O.
The 10,000-file fixture improves median wall time **62.9%**, CPU **80.7%** and
allocation **27.5%**. It misses the proposed 80% storage-latency target even before
adding actual admission/audit costs. Empty controls regress 65.9% in wall time and
79.1% in allocation; one-large-file allocation also increases. Do not activate or
claim complete P01 acceptance on the strength of these controls.

Physical read counters are zero in all warm samples. Peak working set is a
cumulative process high-water mark: median full/metadata is about 59.2/63.8 MB for
empty and 103.1/106.3 MB for 10,000 files; no general RSS saving is established.
Full-process median wall ranges are 990.3–1087.7 ms for 10,000 files versus
402.5–407.5 ms for metadata; the 1,000-file full control varies 378.9–863.3 ms.
No profiling, tests or builds run during these timed processes. Startup/full-audit
pauses and fixed-workload aggregate CPU/I/O remain unmeasured for incremental mode.

Raw samples, source/assembly/harness SHA-256 hashes, all per-process CPU/allocation/
read/RSS counters and the recipe are ignored under
`diagnostics/results/remediation-20261025/g1d4-scanner-measurements` and
`recipes/g1d4_scanners.py`; frozen binaries are `g1d4-final-assemblies`. Reproduce
with the public harness README, one frozen build and three alternating process
pairs. No payload, key, fixture account or large diagnostic is committed.


<a id="source-evidence--strict-read-regression-controls"></a>

##### Strict-read regression controls

The existing common `ArtifactConsumption` harness compares the prior G1d3 frozen
build with the final build, with verification enabled in both. Three alternating
process pairs, 60 serial warmups, 100 measured batches, concurrency 1 and 25, and
8 KiB/1 MiB consumed buffers retain identical SQL-state request counts (one per
read). The SQL reader is an in-memory stub: actual PostgreSQL latency/CPU is not
measured. Network request counts are zero; metadata read traffic is unchanged.

Across all eight shapes, allocated bytes per read change by less than 0.2%.
Sequential 8 KiB/one-entry median wall time changes 2.316 → 2.558 ms and CPU
2.729 → 3.013 ms; sequential 10,000-entry reads change 3.706 → 3.368 ms and
5.854 → 5.658 ms. The 25-reader 10,000-entry batch changes 10.630 → 9.902 ms,
CPU per read 5.194 → 5.133 ms and allocation 227,912 → 227,619 bytes. Other
latency/CPU results are mixed; this refactor establishes no universal speedup.
Warm physical reads remain zero. Cumulative RSS spans roughly 68–243 MB across
processes/shapes and includes retained fixture/runtime state, not just cache bytes.
Full results and hashes are `g1d4-consumption-measurements`, using
`g1d3-common-read-harness`, prior `g1d3-sized-final-assemblies` and matching final
`g1d4-consumption-assemblies`. These comparisons run after the scanner controls.


<a id="source-evidence--correctness-recovery-and-offline-delivery"></a>

##### Correctness, recovery and offline delivery

The initial 18 existing cache/consumed-read checks pass; the first attempt could
not start because the previous task database container was gone. That output is
retained. A fresh cached-image PostgreSQL 18.6 task container uses only an ephemeral
loopback port; the unrelated IDE database is untouched. No image pull occurred.

The full five-project run passes 846 checks. Two additional cold/warm visitor
failure-injection cases then pass with all **303 Infrastructure checks**; production
sources and frozen DLLs remain unchanged. Combining that final project TRX with
the other four unchanged projects yields **848/848**, zero failures/skips:
Domain 150, Application 140, DataAccess 171, Infrastructure 303, Presentation 84.
Both offline Node checks and the five-project TRX gate pass. Existing NU1510,
StringTools and xUnit2031 warnings remain; no new C#/CA warning is introduced.
No SQL or migration changes require a new PostgreSQL 16 comparison in this stage.
Raw logs are `g1d4-baseline*`, `g1d4-targeted`, `g1d4-full`,
`g1d4-final-infrastructure`, `g1d4-final-trx` and `g1d4-worker-build.log`.

New checks cover nested/multiple roots and canonical ordering; missing/extra/renamed
files; length/time changes; file/directory symlinks; unexpected metadata; warm-part
corruption; pending recovery; quarantine; cancellation and visitor failure. Existing
receipt-anchor, crash/recovery, concurrency, fail-closed privacy and strict cache
memory tests remain passing. No production behavior bypass is enabled.

A final frozen-Worker 1/10/100-recipient probe advances **111** recipients without
intermediate reconciliation; repeated ticks are empty, all 111 results are
`LocalCaptured`, and none has `SentAt`. Independent SQL recomputation matches
**5,440,279 logical bytes**; observed database size is **12,900,031 bytes**.
There are 111 encrypted copies (1,567,875 bytes), six live parts (8,976 bytes),
no retired/temporary parts, exact canonical entry lengths/times/content hashes and
an independently computed matching SQL receipt anchor. Evidence is
`g1d4-delivery-after` and `g1d4-delivery-manifests`; reproducible recipes remain
`g1c_uninterrupted.py`, `verify_manifest_probe.py` and `verify_uninterrupted.py`.
The finite-process elapsed outputs are retained (5.48/6.08/32.95 seconds), but this
functional probe overlaps the final test-only Infrastructure verification and is
**not a matched performance measurement**. No external email/provider request,
long-running stability, VPS performance or production acceptance is established.
The original untraced injected post-SQL-commit stop remains open for G7.



<a id="source-evidence--targeted-storage-readerwriter-scan--2026-10-27"></a>

#### Targeted storage reader/writer scan — 2026-10-27

The [boundary report](#source-scan) records the complete scoped matrix,
source search limits, S01–S05 findings, maintenance-policy options, offline probes
and remaining activation gates. Baseline is `862af96`; production sources and
behavior are unchanged. Targeted verification passes 154/154 distinct checks plus
11 separate fresh-process recovery observations; no full-suite rerun is claimed.
The old injected post-commit failure remains unattributed.

Three controlled 100-read process pairs show cache-gate contention from the inactive
10,000-file metadata walker: median-of-process-median request latency changes
5.407 to 133.268 ms, and median process p95 changes 6.371 to 167.737 ms. These are
deliberately aligned synthetic bursts with a SQL-state stub, not production tails
or an optimization claim. The report includes CPU, allocation, read traffic, RSS,
hashes, raw-evidence locations and reproducible commands. It also distinguishes
current private catalog streams from a mutable-stream append limitation and
reproduces a stopped result for deliberately overlapping pacing configuration.

Development HTTP re-enrollment's operator trust-reset policy requires a decision
before remediation; Production does not map that route. No live provider, external
email, existing-process attachment, public deployment or infrastructure purchase
was performed. No security boundary or accounting check is weakened.
<!-- END SOURCE SNAPSHOT: docs/renewal/performance-remediation-evidence.md -->

---

<a id="source-capture"></a>

## Appendix A04 — Historical capture integration notes and later qualifications

Original: [docs/renewal/performance-capture-integration-notes.md](docs/renewal/performance-capture-integration-notes.md). Full source text. Original source: 7,189 UTF-8 bytes, 106 lines.

Source SHA-256: `1abe61e177c989125e62a28a13762d035d4b9d947f2e5dd3c5fd90a40a8be7c0`.

<!-- BEGIN SOURCE SNAPSHOT: docs/renewal/performance-capture-integration-notes.md -->

<a id="source-capture--g1c-integration-notes--work-in-progress"></a>

### G1c integration notes — work in progress

This follows the approved joint P01/P19/P20 design. G1b is committed as `95ee516`.
The capture/removal foundation is locally verified; G1c is not a completed
resolution of P20.

Fresh frozen-G1b finite Worker controls reproduce 1/10/100 stopped recipients and
zero receipts. Failure times are 2,671.72 / 1,919.73 / 3,225.78 ms. They cannot be
compared to successful delivery throughput. Synthetic inputs, frozen Worker/helper
assemblies and logs are private under `diagnostics/results/remediation-20261025/`.
The durable replay recipe refuses to overwrite a cohort/evidence directory and
reuses frozen binaries. The task-owned PostgreSQL container was recreated after
the continuation; earlier G1b timings are not mixed with this host session.

Implemented so far:

- Legacy generation uses coordinated persistence even when job hosting is off,
  preserving the provider's original per-pick charge and recording expiry checks
  before/after persistence. Schedule and unsubscribe writers also publish their
  known mutations independently of the job-host flag.
- Reservation providers expose their full charge. Signed intents now bind that
  value, including configured row overhead, rather than only the caller estimate.
- Local email's existing database-backed Data Protection repository now stores
  keys through independent coordinated transactions. Exact XML/name repeats are
  idempotent. A synchronous framework callback is bridged only for key creation/
  rotation, with a bounded deadline; nested creation inside coordinated mutation
  stops instead of blocking or publishing a key in memory before its parent rolls
  back. Existing key storage, application discriminator and purposes are retained.
- Decryption first uses a non-generating ring sharing the writer's key manager and
  invalidation. The stock writer fallback preserves compatibility when the framework
  cannot select a sole expired key. Pending-operation recovery explicitly suppresses
  that fallback to avoid recursive key creation. No keys are deleted; Identity/JWT
  purpose configuration is unchanged.
- Explicit enrollment precedes key creation. Legacy/test constructors remain for
  existing-key compatibility verification; both runtime roots supply the
  coordinated repository.

The broad DataAccess run before key integration had 133/146 passing tests and 13
remaining failures. Several old fixtures mutate rows directly and need explicit
setup reconciliation before exercising admission. They also need the real
filesystem probe configuration, including its 64-MiB minimum. Runtime recovery
acceptance must never use those setup helpers between generation/capture/advance.
Other failures concern real capture ordering and assertions for the previously
stopped path. Do not change those assertions without proving the new behavior.
Three targeted key tests pass; full verification follows capture integration.

Remaining implementation under the approved design:

1. Split preparation (render/encrypt/read receipt) from acquisition of global and
   filesystem mutation leases. Keep all key creation and decryption outside those
   leases. Acquire scheduler (when needed), account ownership, global admission,
   filesystem lease, transaction fence, then user/digest rows in that order.
2. Extend the signed intent with bounded file plans. Persist opaque canonical-path
   hashes, byte/content bounds and an authenticated unplanned-artifact baseline,
   not account identifiers. Authorize publication only after eligibility and
   fingerprint revalidation. Record full observed artifact measurements before
   sealing the SQL result. Preserve old receipt authentication with null-omitting
   optional fields and compare normalized mutation values structurally.
3. Publish/fsync the immutable final receipt before marking captured, as approved.
   Commit capture state and its signed operation receipt together, then publish
   the checkpoint under the same lease. A committed result can finalize its exact
   artifact checkpoint. Confirmed rollback may clean only signed owned temporary
   paths. A published final receipt is retained and recovered, never discarded
   merely because SQL rolled back.
4. For a published receipt with rolled-back SQL, validate its signed file proof and
   decrypt outside global locks. Recover the original capture under its account
   lease, rechecking current ownership/eligibility/fingerprint. Generic admission
   must release global/filesystem locks before dispatching owner-specific recovery.
   Try ownership without waiting when a caller might already hold another owner;
   stop safely if busy. Scheduler readiness before selecting bounded batches
   prevents a pending owner's position from starving the queue. Recovery never
   generates recommendations, sends externally or recreates a deleted user.
5. Bring failure recording, conversion, archive deletion and HTTP mutation/deletion
   paths into the protocol. Pure authorized file cleanup can finish only its
   authenticated plan; it cannot refresh unexplained changes. Keep the existing
   combined job-host/local-capture guard until both roots and all writers pass.

Tests must cover partial/final publication faults, SQL rollback/unknown outcome,
checkpoint faults, repeated/restarted finite ticks, unsubscribe before final
admission, consent/ownership changes, account deletion, valid-key rollover and
revocation, independent counters, tampered files/control state, lock contention,
cross-process recovery and migration compatibility. Measure successful 1/10/100
throughput separately from the failure controls. P01 incremental activation remains
G1d; full scans are retained until its separate acceptance checks pass.


Latest continuation: signed capture, recovery, scheduling and removal are implemented
in the working changes; 23 focused PostgreSQL/Worker and 21 artifact tests pass.
The fresh 1/10/100-recipient probe advances all 111 accounts without intermediate
reconciliation. All repeated ticks are empty; original ciphertexts are retained.
See the evidence document for resource observations and qualification.

The earlier 133/146 and 13-failure descriptions above are historical. The current
full scan passes all 752 tests with zero failures/skips. The approved exception is documented in
[privacy reclamation review](#source-privacy); its implementation
and commit/rollback, counter-trust and HTTP regression checks pass.

G1c8 completes coordinated historical conversion, including signed purpose-bound
recovery and journaled plaintext cleanup. Ownership conflicts retain both inputs.
Both real composition roots accept combined discovery/local capture only with an
explicit independent integrity key; missing-key startup is covered by negative
checks. Quiet-period unsubscribe refreshes only a fully verified known inventory.
Bare signed writer adapters reject uncoordinated capture/conversion/removal.
The earlier 752-test and remaining-writer descriptions are historical checkpoints;
see the current evidence for final acceptance and resource measurements.
G1d incremental verification remains disabled and separate.
<!-- END SOURCE SNAPSHOT: docs/renewal/performance-capture-integration-notes.md -->

---

<a id="source-manifest"></a>

## Appendix A05 — Manifest, receipt, consumption, cache and metadata integration

Original: [docs/renewal/performance-manifest-integration.md](docs/renewal/performance-manifest-integration.md). Full source text. Original source: 15,826 UTF-8 bytes, 248 lines.

Source SHA-256: `28a03e1974b387666c11577dc8156e1a46f7d1ccdc49ed08ea590c65e5ecae43`.

<!-- BEGIN SOURCE SNAPSHOT: docs/renewal/performance-manifest-integration.md -->

<a id="source-manifest--authenticated-artifact-manifests--g1d-integration"></a>

### Authenticated artifact manifests — G1d integration

This implements strict persistence, verified consumption, bounded strict read
caching and an internal metadata snapshot scanner for the approved joint
P01/P19/P20 design. Full content scans remain enabled.
Periodic audits and incremental admission are subsequent stages; this document
does not authorize deployment or claim their acceptance.

The subsequent [storage-boundary scan](#source-scan) records the
remaining caller contracts, Development maintenance-policy decision, pacing
disjointness gap and measured snapshot/reader contention. It is verification
evidence, not incremental activation or a production behavior change.


<a id="source-manifest--metadata-snapshot-foundation--g1d4"></a>

#### Metadata snapshot foundation — G1d4

The internal snapshot scanner is exercised by tests and the isolated benchmark;
production admission does not call it. It traverses each category with the existing
canonical root/path ordering, rejects symlinks, unknown/missing/renamed files,
length or modification-time changes, and verifies file counts and aggregate bytes.
Using the authenticated expected content hashes, it reconstructs the existing
category fingerprint without opening payloads. It hashes each manifest part once
per category, including warm cached parts, checks the metadata namespace and
verifies remaining retired parts. Pending recovery and quarantined inventories
cannot use this path. It writes no checkpoint, receipt, accounting or audit time.

The bounded cache exposes a synchronous span visitor. A category traversal pins
only its current part while holding the cache gate; no backing array is returned
to the caller. Large-part retention and the one-in-flight limit are unchanged.
Small parts still bypass retention. Existing consumed reads use the same visitor
and keep their strict on-disk and consumed-buffer content checks.

This scan is **not content proof**. A test changes payload bytes while preserving
length and timestamps: the isolated metadata scan passes, while production full
admission and consumed-byte verification reject the replacement. Before a future
caller uses metadata admission, it must authenticate the root and exact SQL state,
hold coordinated admission and establish process-local startup/full-audit trust.
The approved 30-second refresh and 60-second completed-audit deadline, failed-audit
handling and remaining reader/writer coverage must be implemented and verified
first. A persisted audit timestamp alone cannot establish startup trust. Full
recovery and uncertain state continue to require content verification.
The category walk holds the shared large-part cache gate while reading filesystem
metadata. Its effect on concurrent consumed reads must be measured before admission
activation. Current scanner controls also miss the proposed 80% latency target for
10,000 files; do not present their lower read traffic as complete P01 acceptance.


<a id="source-manifest--format-and-accounting"></a>

#### Format and accounting

`GenerationStorage:ManifestDirectory` is optional. It must be an existing private
Linux directory, disjoint from the artifact and control roots and the independent
integrity key. No directory or replacement key is created automatically. Leaving
the option unset preserves the original artifact format. An inventory containing
manifests cannot silently fall back when the option is removed.

The signed checkpoint references one immutable binary part per category. Each
part has a 16-byte version/category/count header and 80-byte entries: canonical
path SHA-256, byte length, modification ticks and content SHA-256. Entries are
sorted by path hash. They contain no filenames, account identifiers, credentials,
recommendation data or message bodies. The existing categorized fingerprint format
is unchanged. Each category retains its 100,000-file limit; a part is at most
8,000,016 bytes. Parts are addressed by content SHA-256 and created with mode 0600
inside the private directory.

Live and retired part lengths are charged separately from payloads and the fixed
SQL/control allowance. They are not hidden in the existing 327,680-byte control
reservation. There are six live descriptors and at most twelve retired descriptors.
Temporary paths are bounded and authorized by a signed operation or enrollment
candidate. Directory membership, file sizes, permissions and hashes are checked;
an unexpected file is not adopted or removed by ordinary work.

The manifest records when full verification completed. At this stage that field
does not enable shortcuts or replace any validation boundary. A cold start, idle
refresh or ordinary mutation still performs full content checks.


<a id="source-manifest--publication-and-recovery"></a>

#### Publication and recovery

Application file changes retain the existing owner → database admission → file
lease → SQL transaction/ledger/row order. Rendering, encryption and provider calls
remain outside that mutation interval. Published payloads remain immutable.

For capture, conversion and removal, the scanner builds a candidate manifest after
verifying the authorized payload changes. Sorted entry comparison proves that only
the four signed copy-plan paths changed; unrelated entries and categories must
remain identical. A bounded signed intent records the exact before/after manifest
descriptors **before** any new metadata file is created. Complete metadata is
flushed and renamed before the exact SQL receipt is sealed. Recovery reuses the
same descriptors and ciphertext; it cannot replace them with newly observed state.

The published root initially retains obsolete part descriptors. Their bytes remain
charged until cleanup succeeds. After the intent is durably removed, that signed
retired list authorizes deletion of only those verified metadata files. Missing
retired files represent an interrupted authorized cleanup; a corrupt retired file
stops cleanup. Clearing the list creates another signed checkpoint generation under
the unchanged independently verified SQL receipt. It does not generate a digest or
replay a transaction. Cleanup can resume after a restart without guessing ownership.

New growth reserves the old/new overlap and checks the managed cutoff and physical
headroom, including the metadata filesystem. A verified copy removal may need a
bounded metadata rewrite while already at the cutoff. Its live artifact/manifest
total must decrease; temporary metadata remains charged, physical margin is still
required, and ordinary growth remains blocked until verified retirement completes.

Recovery checks the complete allowed metadata namespace before writing. Only parts
introduced by the signed change are writable; an unchanged part is verified rather
than recreated, and an unplanned similarly named temporary file is preserved.

Confirmed SQL rollback may remove only metadata introduced by its authenticated
plan, after the unchanged payload baseline is proved. Published ciphertext is
preserved for the existing owner-specific recovery path. Unknown SQL outcomes,
incorrect part hashes, changed sealed descriptors and unrelated files fail closed.
The account-privacy exception preserves the original manifest as untrusted and
does not use uncertainty to enroll files or run metadata cleanup.


<a id="source-manifest--receipt-anchoring-and-legacy-checkpoints"></a>

#### Receipt anchoring and legacy checkpoints

A new authenticated `Checkpoint.CommitReceiptSha256` binds each published root to
the exact independent SQL commit receipt, including a distinct null-receipt state.
This is needed for file/control-only operations: their row revision may be
unchanged. A deterministic attack fixture restored a valid initial checkpoint,
payload, timestamps and metadata while leaving the newer SQL removal receipt in
place. Before anchoring, admission accepted that rollback. The regression now
requires `storage_commit_conflict` before accepting restored filesystem state.

The additive field is omitted when null so old authenticators remain verifiable.
An unambiguous legacy post-commit checkpoint can be upgraded after exact receipt
and full artifact validation. An old checkpoint with no operation ID but an
existing SQL receipt is ambiguous; it stops with `storage_commit_anchor_missing`
until explicit verified maintenance. It is not automatically assigned a fresh
anchor. New manifest inventories require the anchor.


<a id="source-manifest--signed-preflight"></a>

#### Signed preflight

The legacy generation preflight uses coordinated validation for signed inventories.
It verifies the independent receipt and can conservatively publish known physical
database growth under unchanged SQL/schema state. Its returned lease is read-only:
reservations require the explicit coordinated write API. A pending owner-specific
recovery becomes a bounded stopped result; preflight does not hold leases while
waiting for another owner. This also closes the legacy preflight's demonstrated
false stop on a 4 KiB physical increase.


<a id="source-manifest--verified-consumption--g1d2"></a>

#### Verified consumption — G1d2

Published local-email reads (archive export, operator read, deletion inspection,
existing capture and historical conversion) and local recording input reads now
verify the byte buffer that will be parsed or decrypted. A valid replacement
encryption of the same message is rejected when its bytes differ from the enrolled
entry. Existing ownership, receipt, provider-recording approval and encryption
checks still run after verification.

The verifier authenticates the checkpoint, validates the private disjoint roots,
and matches its exact independent SQL receipt and full revision. It hashes and
validates the bounded category part, finds the canonical path hash, then compares
the consumed buffer's length and SHA-256 with that entry. It reads and authenticates
the root again to reject a concurrent publication with
`storage_read_generation_changed`. SQL/root disagreement produces a bounded stop;
the reader never publishes, recovers an operation, acquires the global write
lease, or adopts observed content. Missing enrollment, missing metadata, lost keys,
removed manifest configuration, changed part permissions, unknown paths and corrupt
parts fail closed. Removing both integrity/manifest options cannot downgrade an
existing signed root.

Read verification uses the signed expected content identity, without applying the
five-minute mutation-inventory age limit to idle archives. During approved privacy
reclamation, an intact original entry can still prove an owned copy's bytes even
when the global root is marked unreconciled; authoritative SQL ownership and the
current receipt remain required. This does not restore global trust or admit writes.
An unrelated corrupt category part is not read to prove an intact private copy.

Recovery and newly written staging have a different proof: their exact buffers
are checked against the signed pending file plan or the prepared expected bytes
before parsing/publication. They cannot require membership in the predecessor's
manifest, which intentionally excludes the new file. The existing plan-bound
recovery paths remain in place.

The verifier is scoped with the database adapter; the local recording loader is
also scoped. A manifest-enabled bare email/recording reader without its verifier
stops with `storage_artifact_verifier_unavailable`. Legacy formats retain their
reader-specific cryptographic/recording checks. No migration, package update or
incremental activation is included. Each verified read adds one SQL-state read,
two bounded root reads and a category-part read/hash. G1d3's strict cache below
removes repeated large-part allocation/structural validation. The existing archive
still scans other owners' copies until G2 supplies the approved owner index.

Full writer/reader coverage, including catalog import streaming and operational
pacing files, remains an activation gate for incremental admission. This stage
does not establish that gate or claim a throughput improvement. See the
`ArtifactConsumption` diagnostic harness for reproducible reader cost controls.


<a id="source-manifest--bounded-strict-read-cache--g1d3"></a>

#### Bounded strict read cache — G1d3

Both composition roots register one shared `ArtifactManifestCache`. It retains
validated binary parts larger than 64 KiB for one authenticated storage instance,
checkpoint generation/hash and metadata directory. A new generation prunes obsolete
parts and reuses only identical live descriptors. A delayed older reader cannot
replace the newer cached generation; another storage instance clears the cache.
Tiny parts use the original loader without retaining bytes or serializing their
file I/O. The minimum size follows the measured fixed cost of caching one-entry
parts; it is not a new operator tuning option.

At most six parts totaling **48,000,096 bytes** are retained. One large miss loads
at a time. Retained parts plus one maximum-sized in-flight part total 56,000,112
bytes, below 64 MiB. This is a metadata bound, not a process RSS limit: caller
buffers, runtime objects, pooled scratch buffers and concurrent requests also
consume memory. A forced-GC concurrency regression proves that a delayed warm
verification does not retain an evicted whole part after generation replacement.
Only the matched entry value escapes the cache gate; arrays are never returned
to callers. Miss failure/cancellation cannot publish partial metadata, and a
cancelled waiter cannot cancel the owner load.

Strict detection is retained. A warm hit streams and hashes the complete on-disk
part using bounded pooled scratch, checks its size, permissions and path, and
compares its SHA-256 with the authenticated descriptor before returning an entry.
The consumed payload is still hashed and both signed-root/SQL checks still run.
No content proof relies on timestamps. Warm hashing runs outside the cache gate;
this gate never acquires SQL, owner or global mutation leases. Full scans, recovery,
manifest publication/retirement and storage accounting continue using their full
verification paths. No disk format, migration, deployment setting or package changes.

This stage reduces allocation and repeated structural validation for large parts;
it does not remove metadata I/O or SQL-state queries and does not enable incremental
admission. Periodic full-audit freshness/deadline enforcement, uncovered reader/
writer integration and actual deployment acceptance remain required. Before/after
component results, fixed costs and host variance are recorded in the evidence.


<a id="source-manifest--maintenance-enrollment-and-rollback"></a>

#### Maintenance enrollment and rollback

Stop all writers, validate a matched backup and independently verify SQL accounting
before enabling the directory and invoking the existing explicit reconciliation
command. Resolve pending operations first. Enrollment performs full scans, checks
space for overlapping parts, and durably writes its signed candidate using the
already reserved inventory `.tmp` control slot. No sixth uncharged control document
is introduced. Parts are written before the live root is replaced.

An interrupted enrollment can resume from that authentic candidate only while the
SQL revision and measured artifact/part identities still match. Changed inputs,
an invalid candidate or unknown metadata stop the retry. After root publication,
the signed retired list handles interrupted cleanup. Repeated enrollment is not a
normal runtime recovery mechanism.

No database migration or package update is needed for this stage. Do not mix old
and new writers: old binaries cannot authenticate roots containing the new receipt
anchor or manifests. Keep the new binary in strict mode for rollback of incremental
behavior. Downgrading the on-disk format requires a stopped, reviewed conversion or
a matching pre-upgrade database/artifact/control/key backup. Do not remove the
directory setting or run an old binary against new checkpoints.

Process-interruption tests establish recovery mechanics. Power-loss behavior,
actual VPS storage performance, reverse-proxy behavior, real provider quotas and
long-running unattended delivery still require deployment acceptance.
<!-- END SOURCE SNAPSHOT: docs/renewal/performance-manifest-integration.md -->

---

<a id="source-scan"></a>

## Appendix A06 — Complete S01–S05 scan, matrix, probes, pending policy and remaining checks

Original: [docs/renewal/storage-boundary-scan.md](docs/renewal/storage-boundary-scan.md). Full source text. Original source: 22,290 UTF-8 bytes, 272 lines.

Source SHA-256: `eaf23c99ee87ef6a17a478a9c115ff788e4fd3677021ba9a15bd973cdcc1230d`.

<!-- BEGIN SOURCE SNAPSHOT: docs/renewal/storage-boundary-scan.md -->

<a id="source-scan--storage-readerwriter-scan--2026-10-27"></a>

### Storage reader/writer scan — 2026-10-27

Baseline `862af96`. This is a targeted continuation of the approved P01/P19/P20
review, not a replacement for the original repository-wide audit. Production
sources, migrations, configuration and behavior are unchanged. The new offline
probe and clearer failure messages in an existing recovery test support this scan.


<a id="source-scan--findings-and-disposition"></a>

#### Findings and disposition

| ID | Finding | Current disposition |
| --- | --- | --- |
| S01 | Development HTTP reconciliation exposes an operator trust reset to an ordinary authenticated account | Confirmed source-level authorization/coordination gap; production does not map the route. Maintenance-policy decision below precedes remediation. |
| S02 | Provider pacing can write inside a configured managed artifact root | Reproduced with deliberately overlapping configuration. Current strict admission stops; documented Compose layout is separate. Add a disjointness guard before incremental activation. |
| S03 | Metadata category walks hold the consumed-reader cache gate | Measured controlled-overlap reader delays. Scanner remains unactivated; resolve contention while preserving bounded memory before activation. |
| S04 | An arbitrary mutable catalog stream can reach append callbacks before the final consumed-hash failure | Reproduced with a fake store; publication remains blocked. Current host callers own private MemoryStreams. Preserve that contract; no normal-path exploit or performance bottleneck established. |
| S05 | Process-local startup proof and periodic 30/60-second full-audit enforcement are absent | Already-known implementation prerequisite, confirmed by source/test review. Current full hashing remains enabled; no incremental integrity-window acceptance is claimed. |

The older intermittent post-SQL-commit stop remains unattributed. One instrumented
run and ten fresh-process repeats pass; this does not explain the retained failure.


<a id="source-scan--s01--development-http-trust-reset-decision-required"></a>

#### S01 — Development HTTP trust reset: decision required

`src/Presentation/Program.cs:164` maps manual-test routes only in Development.
`ManualTestEndpoints.cs:24` uses `RequireAuthorization()` without a role or operator
policy. JWT validation still requires an active account; this is not an anonymous
endpoint. `ManualTestEndpoints.cs:68` reads SQL state and calls `ReconcileAsync`
directly. It does not acquire `IDigestGenerationStore.AcquireBatchAsync`, invoke
`IManagedStorageIntegrity.VerifyAsync`, or enforce stopped-writer maintenance.

In contrast, `LocalDigestWorkerRunner.cs:24` takes database admission and verifies
SQL integrity before explicit CLI reconciliation. `scripts/setup_local_live.cs`
also takes admission and verifies integrity for initial creation; retained state
uses known-state maintenance rather than blind re-enrollment.

Reconciliation deliberately enrolls newly observed hashes. The offline probe
changes a three-byte file while restoring its timestamp: strict admission stops
with `artifact_inventory_changed`, but explicit re-enrollment makes those changed
bytes pass consumed-byte verification. This demonstrates the operator primitive's
semantics, not an HTTP exploit. Together with the route call chain, it establishes
a trust-reset exposure when a Development host is reachable by an active account
and an artifact has changed. Production route absence and anonymous denial have
existing route tests; an authenticated end-to-end signed-storage HTTP reproduction
has not been performed. Do not claim production exposure or tampering was observed.

Choose the maintenance contract before changing this behavior:

1. **CLI-only for signed storage (recommended):** when an integrity key or manifest
   directory is configured, the HTTP helper returns a bounded maintenance-required
   result without reading SQL or re-enrolling. Keep unsigned local Manual QA
   behavior. Use the existing CLI with writers stopped, verified backup, database
   admission, independent ledger verification and full enrollment. This avoids
   introducing web-based privileged maintenance state.
2. **Keep signed web maintenance:** define explicit operator authorization and a
   maintenance/quiescence contract covering both hosts, then acquire database
   admission before the file lease, verify SQL accounting and perform full
   enrollment. Merely adding authentication or a local in-process flag is
   insufficient. This requires additional architecture and failure tests.

Required regression cases: an ordinary authenticated account cannot reset signed
trust or change the checkpoint; no database/file work happens before the policy
guard; anonymous requests remain denied; Production stays unmapped; unsigned
development behavior remains compatible; CLI SQL-integrity, lock order, pending
intent and enrollment-recovery tests remain passing. Do not infer approval for a
new operator role or web maintenance protocol from this report.


<a id="source-scan--readerwriter-coverage-matrix"></a>

#### Reader/writer coverage matrix

Initial searches found 140 file-I/O matching lines in 20 production files and 90
EF/transaction matching lines in 25 files. Expanded searches also include
`FileInfo`/`DirectoryInfo`, JSON configuration, XML key repositories, raw Npgsql
commands and Dapper: **159 matching lines / 22 files**, and **119 matching lines /
28 files** respectively, excluding generated migrations. These are search
candidates, including declarations and reads; they are not counts of distinct
writes or a numerical code-coverage claim. Callers and delegated framework paths
were then inspected. Raw lists, patterns and source SHA-256 hashes are retained in
`diagnostics/results/remediation-20261025/g1d-storage-reader-writer-scan`.

| Family and source | Integrity/accounting boundary | Lock/recovery disposition |
| --- | --- | --- |
| `LocalGenerationStorage`, `AuthenticatedStorage`, `CoordinatedGenerationStorage` | HMAC root/intent, exact independent SQL receipt/revision, full category scans, headroom | Runtime admission/publish/recovery remain strict. Explicit enrollment is a trust reset; S01 concerns its HTTP caller. |
| `ArtifactManifests`, `ArtifactManifestFormat` | Authenticated descriptors, separately charged live/retired metadata, private namespace and full part hashes | Signed before/after authority precedes writes; retirement/recovery cannot adopt unknown files. Full recovery remains required. |
| `ArtifactMetadataSnapshot`, `ArtifactManifestCache` | Metadata comparison cannot prove unchanged content; cached parts and consumed buffers still hashed | No production metadata-admission caller. Category gate contention is S03. |
| `ConsumedArtifacts`, `LocalEmailPaths`, `LocalRecordedInputLoader` | Bounded consumed buffers verified before parsing/decryption against root/SQL/part proof; recording approvals, expiry, hashes and secret rejection also remain | Ordinary reads do not recover/publish. Changed root/receipt or missing enrollment stops. Recording files must be enrolled in managed roots. |
| `LocalEmailSink`, capture command/store, `CoordinatedEmailStorage`, `CaptureArtifactPlan` | Render/encrypt outside shared admission; four signed owned paths, sealed byte proofs, exact SQL capture receipt and immutable rename | Owner → database admission → file lease → ledger/rows. Prepared/pending files use signed plan proof before parsing, not old-manifest membership. |
| `LocalEmailArtifactRecovery`, `CaptureArtifactPlan` | Original bounded ciphertext SHA and pending operation identity precede parse/reuse; eligibility/consent rechecked | Recovery releases shared admission before bounded owner `TryAcquire`; no re-render, account resurrection or duplicate capture. |
| `LocalEmailArchive`, `LocalEmailRemoval`, `CoordinatedEmailRemoval` | Verify copy bytes and ownership before removal; journaled control-only receipt and exact accounting; unverifiable copies retained/reported | Account deletion takes owner lease before SQL, commits SQL/Identity before filesystem cleanup. Archive scans remain O(all copies); owner indexing belongs to G2. |
| `LocalEmailLegacyConversion`, `CoordinatedLocalEmailConversion`, `CoordinatedEmailConversion` | Runtime uses journaled immutable conversion and source/final byte proofs; plaintext cleanup follows durable encrypted proof | Both roots inject coordinated adapters. Signed-mode guards reject bare legacy mutation paths; unsigned/test fallback is not incremental coverage. |
| `CoordinatedKeyRepository`, `LocalUnsubscribeProtection`, DI | Runtime keys live in SQL and are charged by triggers; exact key creation/rotation/revocation publication; key-state epoch checked through capture | Shared repository supplied by both roots; nested key creation stops. Legacy/test bare provider construction is not the runtime writer. |
| Generation, schedule, unsubscribe and failure stores | Row writes and storage-control receipts participate in coordinated publication even with job hosting disabled | Scheduler/owner/admission ordering retained; raw Npgsql scheduling updates use the coordinator in runtime DI. Separate advisory-lock commands do not persist artifact bytes. |
| Guest lifecycle, discovery jobs/batches/taste, public evidence and catalog SQL stores | Application workflows/coordinator and statement accounting cover EF/raw bulk paths; schema/revision checks remain | Provider I/O occurs outside the shared write interval. Cold public acquisition/caches are not filesystem payload writers. |
| Auth/taste/feedback/subscriber HTTP and export | Mutating account actions use write filter/coordinator or private reclamation; stored score explanations and ownership filters retained | Auth contention remains G3. Exports currently read SQL/history and authenticated archive copies; no staged export files exist yet (G2). |
| `CatalogArtifactReader`, `ImportCatalog`, lookup/recording catalog callers | Whole-artifact preflight plus consumed-stream hash; bounded chunk commits and final publication | Current sources are private MemoryStreams. Mutable third-pass append limitation is S04; no uploaded/file catalog endpoint found. |
| `MetaBrainzRequestGate`, `ListenBrainzOptions`, Compose | Two bounded mutable operational pacing files; size/symlink/format checks and fsync, outside signed artifact journaling | Documented separate pacing directory is compatible. Arbitrary configured overlap is not rejected; S02 must close before activation. |
| `Program`, development helpers, local bootstrap/recorder/cleanup scripts | Settings/static hosting are outside artifact accounting; recorder has a separate acquisition lease/budget, not the runtime HMAC journal | Manual acquisition/import/backup changes require stopped-writer, verified maintenance. Do not run the recorder concurrently against enrolled roots. Cleanup uses owner/coordinated services. S01 is the exceptional HTTP enrollment entry. |

No additional ordinary managed-file writer was identified in the reviewed runtime
callers. This is not a proof against arbitrary external SQL, reflection, plugins,
manual tools or future entry points. Their effects still require strict detection
or the approved bounded audit window after activation. No P04/P10/P11/P17 fix is
removed, and no speculative P05/P07 optimization is introduced.


<a id="source-scan--s02-and-s04--offline-boundary-results"></a>

#### S02 and S04 — Offline boundary results

The checked-in `StorageBoundaryScan` harness uses frozen Infrastructure/Application
assemblies and private disposable fixtures. There are no database, provider or
email requests. Its boundary mode reproduces:

- A pacing directory inside the enrolled cache root writes an 18-byte timestamp;
  the next strict admission stops with `artifact_inventory_changed`. Default
  pacing is under the temporary directory and Compose uses a separate
  `/app/local-state/upstream-pacing` sibling. Neither options validation nor runtime
  DI rejects arbitrary overlap. Validate disjoint managed/control/manifest paths
  and account for the bounded operational files separately; do not exempt arbitrary
  unknown files from manifest checks or adopt pacing mutations into trusted state.
- A synthetic 251-row stream changes on the importer's third pass. The first
  250-row append callback completes before the reader reports
  `catalog_consumed_artifact_hash_mismatch`; final publication is never called.
  This is an application/fake-store observation, not a SQL commit measurement.
  Existing PostgreSQL tests cover staging invisibility and rejection of mismatched
  resume rows. Keep current caller-owned buffers private. Before supporting operator
  file streams, define exclusive verified import or immutable staging; do not use
  the final hash as proof that no earlier staging callback occurred.

Neither result authorizes weakening accounting/security to avoid a stopped result.


<a id="source-scan--s05--audit-failure-coverage"></a>

#### S05 — Audit failure coverage

`ValidateArtifactsAsync` still calls the full content scanner. The metadata helper
has no production admission caller. `ValidateManifestState` rejects default/future
audit timestamps but does not implement a 60-second completed-audit deadline.
`PublishCommittedAsync` refreshes `FullAuditCompletedAtUtc` only after today's full
validation; when metadata admission exists, that unconditional assignment must be
split so stat-only work never refreshes content trust.

`DiscoveryInventoryService` runs once per minute and is registered only when
`DiscoveryRuntime:RunJobs` is true. The scheduled Worker registers only its finite
schedule service. Neither establishes the approved process-local startup proof,
30-second audit refresh, monotonic 60-second expiry or cross-process failed-audit
invalidation. These are implementation gaps, not features whose acceptance tests
can already pass. Full scans currently provide the stronger per-boundary check.

Existing tests cover full-mode stale/future inventory, unavailable filesystem,
same-size/time payload replacement, consumed bytes, permission/part corruption,
quarantine, cancellation, concurrent publication and pending commit recovery.
Still required for incremental mode: restart with a fresh persisted timestamp,
30/60-second transitions, wall rollback versus monotonic elapsed time, scan lasting
past expiry, failed/cancelled audit, two hosts, audit/publication fencing and retained
quarantine after partial audit success. Check both composition roots with jobs on
and off. Do not enable shortcuts before these exist and pass.



<a id="source-scan--s03--measured-reader-contention"></a>

#### S03 — Measured reader contention

The final public probe runs unchanged frozen production assemblies in three fresh
processes, reversing mode order in process two. Each mode has 100 verified reads
in four bursts of 25, after two scanner warmups and 60 read warmups. One mode has
no metadata walk; the other deliberately starts each burst during a 10,000-file
category walk. A diagnostic waiter directly measures the actual cache semaphore.
The fixture contains one 8,192-byte file and 9,999 one-byte files (18,191 payload
bytes); it isolates metadata contention, rather than repeating the earlier
100 MiB storage-phase workload.
There are 300 requests per mode across processes. Per-process p95 uses nearest
rank over that process's 100 requests; the table summarizes process statistics,
not a pooled production tail.

| Metric | No metadata scan | Deliberate scan overlap |
| --- | ---: | ---: |
| Median of process median request latency | 5.407 ms | 133.268 ms |
| Median of process p95 request latency | 6.371 ms | 167.737 ms |
| Process median latency range | 5.065–5.455 ms | 125.793–148.485 ms |
| Largest observed request latency | 7.596 ms | 190.355 ms |
| Observed cache waiter range | 0.0002–0.0080 ms | 70.666–188.073 ms |
| Combined process CPU, median per 100 reads | 662.464 ms | 1023.058 ms |
| Combined allocation, median per 100 reads | 21.247 MB | 98.570 MB |
| Application reads, median per 100 reads | 80.399 MB | 83.613 MB |
| SQL-state stub callbacks per 100 reads | 100 | 100 |
| Physical reads | 0 | 0 |

Overlap counters include four full metadata walks, observer work and readers;
this is not a per-reader allocation or CPU regression in deployed code. Individual
scans take 78.290–194.857 ms. Cumulative peak RSS ranges 89.1–103.0 MB without a
scan and 92.2–102.6 MB with overlap; earlier modes/fixture creation contribute.
MB is decimal. Runtime is 10.0.12, workstation GC, 20 reported processors; no
concurrent test/build/profiler ran during measurements. SQL is an in-memory state
stub; real database and network costs are absent. The deliberately aligned bursts
measure the consequence of overlap, not its production frequency or a selected VPS.

The cause is directly visible in `ArtifactManifestCache.InspectAsync`: the cache
semaphore remains held while the synchronous visitor performs every filesystem
stat in the category. Consumed readers need that same semaphore. This is a
confirmed design limitation of the inactive scanner, not a reason to remove
consumed-byte proof. Before activation, consider a separately bounded snapshot pin
or bounded entry copies so stats run outside the reader gate. Prove the retained
plus in-flight memory bound, cancellation, old-generation eviction/GC and failed
part behavior; moving arrays outside the gate without counting delayed holders
would invalidate G1d3's memory proof. No such redesign is implemented by this scan.

Initial characterization is retained in `g1d-storage-scan-probes`; its overlap p95
ranges 196.6–273.0 ms. Final `g1d-storage-scan-final` uses the same reader workload
and adds the explicit trust-reset boundary probe. It retains every raw request,
gate wait, scan duration and resource counter plus source/binary hashes. Variation
between trials is real and no universal production latency figure is claimed.


<a id="source-scan--recovery-investigation-and-reproducibility"></a>

#### Recovery investigation and reproducibility

The existing `FailureAfterSqlCommit_ReturnsVerifiedExistingReceiptOnFreshAttempt`
case still requires exactly `already_captured` and one ciphertext after the injected
post-commit fault and a fresh attempt. Its assertions now include the stopped reason
on failure. No acceptance condition, retry loop or production behavior is weakened.
One instrumented run and ten additional fresh-process runs pass against the isolated
PostgreSQL 18.6 container. The old failure is not reproduced or attributed. Preserve
it as a G7 investigation; passing repeats cannot erase it. Private mode-0700 TMPDIR
is set before process launch, as required by the separately proven sandbox Git-marker
interference. That interference is not asserted to explain the older failure.

Frozen Infrastructure SHA-256 is
`6185dc53f16a0e53cd67855a21de85426186a3f37716be06eb7f5b63c373fef6`;
final probe SHA-256 is
`b0107b1f637f9c83a6287ef181827deef1a95f10d504fcd7f6bb97ca62abcc87`.
The freshly rebuilt test Infrastructure DLL has a different full-file hash,
`c98c4a4e6ed7ebbde77c2fafbbcdc63be136c074f5593b7e6399ed12d8a14ef1`.
The frozen build records `ecba945` in its assembly informational version and the
test rebuild records `862af96`. An initial same-binary assertion therefore failed;
it was corrected by recording both provenances rather than calling the files
identical. Every production source hash matches the frozen source manifest, and
static PE comparison confirms all **2,155 managed method bodies**, signatures,
locals/stack flags and exception regions match. Comparison output is retained in
`g1d-scan-assembly-compare`; frozen benchmark binaries were not replaced or rebuilt
during measurements. This distinguishes matching executable code from matching DLL
file hashes.
Build uses SDK 10.0.401, no new package, frozen references and zero warnings/errors.
No existing process is attached, no heap dump is collected, and no live provider,
email, public deployment or unrelated IDE database is accessed.

Reproduce boundary/contention probes with
[the public harness instructions](#source-boundary-control).
Use one private scratch parent, three fresh processes with alternating mode order,
and no simultaneous builds/tests. Reproduce the recovery check with the pinned SDK,
task-owned `LINER_PHASE1_POSTGRES`, private TMPDIR and:

```bash
dotnet test tests/DataAccess.Tests/DataAccess.Tests.csproj -c Release -m:1   --filter FullyQualifiedName~FailureAfterSqlCommit_ReturnsVerifiedExistingReceiptOnFreshAttempt   --logger trx --results-directory /absolute/private/results/recovery-first
# Repeat with --no-build --no-restore in fresh processes and distinct result directories.
```

The raw recovery evidence is `g1d-scan-postcommit-instrumented` and
`g1d-scan-recovery-repeats`; source inventories are
`g1d-storage-reader-writer-scan`. Preserve failed/initial build output and all trial
history. The probe emits observations rather than treating conditional contract
limitations as passing security acceptance. A passing command does not close S01–S05.


<a id="source-scan--remaining-work-after-this-scan"></a>

#### Remaining work after this scan

Targeted verification passes **154/154 checks**, zero failures/skips: Infrastructure
116 (storage, manifest/consumed-byte and catalog guards), DataAccess 29 (capture,
keys, manifest/accounting recovery and catalog staging), Presentation 9 (development
authentication and production route absence). The 11 focused fresh-process recovery
passes above are separate repeated observations, not 11 additional distinct test
cases. The documentation check passes. The previous 848-check integration gate is
historical evidence; it was not rerun or relabeled as this scan's test run. No
optimization or production behavior change requires a before/after speedup claim.
Raw TRX/logs use `g1d-scan-targeted-infrastructure`, `g1d-scan-targeted-dataaccess`
and `g1d-scan-targeted-presentation`. Existing package/StringTools/xUnit2031 warnings
remain; the final standalone probe build has no warnings or errors.

The scoped runtime reader/writer trace and contention characterization are complete.
Before implementation, resolve S01's maintenance-policy decision. Then remediate the
confirmed configuration and cache-gate limitations, enforce private immutable catalog
input at any new file-import boundary, and finish startup/periodic audit trust and
failure tests. Keep full hashing until those gates and the original comparable
storage-phase performance targets pass. G2–G7, the untraced recovery cause and external
VPS/proxy/provider/real-email/long-running acceptance remain separate unfinished work.
<!-- END SOURCE SNAPSHOT: docs/renewal/storage-boundary-scan.md -->

---

<a id="source-privacy"></a>

## Appendix A07 — Approved narrow privacy-reclamation exception and tests

Original: [docs/renewal/privacy-reclamation-integrity-review.md](docs/renewal/privacy-reclamation-integrity-review.md). Full source text. Original source: 7,567 UTF-8 bytes, 128 lines.

Source SHA-256: `dd0f5d8b74931fc52332deaf017042dcdc06f9741cc8426825ece98acdce1937`.

<!-- BEGIN SOURCE SNAPSHOT: docs/renewal/privacy-reclamation-integrity-review.md -->

<a id="source-privacy--decision-required-account-deletion-when-archive-integrity-is-uncertain"></a>

### Decision required: account deletion when archive integrity is uncertain

Status: the developer approved the recommended narrow exception on 2026-10-25.
The locally verified G1c foundation implements this exception; general mode
activation and developer functional confirmation remain pending.
This extends the approved joint P01/P19/P20 design. Its ordinary capture, accounting
and recovery invariants remain binding.


<a id="source-privacy--reproduced-conflict"></a>

#### Reproduced conflict

`Phase8PrivacyApiTests.Delete_CopyIntegrityFailureRevokesAccessAndReportsIncompleteCleanup`
adds an unrelated, unauthenticated partial after two successful captures. The
existing privacy contract requires deleting the requesting account's owned SQL
rows and credentials, rejecting its capabilities, preserving the other account,
and reporting incomplete filesystem cleanup. The coordinated implementation stops
with HTTP 503 before SQL deletion because the artifact inventory changed.

The strict behavior preserves storage integrity but prevents account revocation
and purge while unrelated artifact trust is unavailable. Silently enrolling the
new file or bypassing accounting is unacceptable. This is a security/privacy
contract decision, not a benchmark optimization.


<a id="source-privacy--recommended-exception"></a>

#### Recommended exception

Permit a dedicated account-deletion reclamation operation when artifact integrity
is uncertain **only if database trust is independently intact**. Validate the
signed checkpoint, exact committed SQL receipt, managed ledger, schema and trigger
coverage. Keep account ownership, global admission, filesystem and SQL lock order.
Reject unknown SQL revisions, unresolvable pending operations, unauthenticated
control state and ledger discrepancies.

In the existing transaction, purge only the requesting owner's rows and credentials.
Require the exact logical byte count to be non-increasing, excluding the already
bounded control receipt. Roll back every SQL change if credential deletion fails.
Retain shared catalog data, shared protection keys and other accounts.

Commit the exact operation receipt with SQL. Publish a signed checkpoint recording
the new database state and the **previous authenticated artifact measurements**,
marked incomplete/untrusted. Do not adopt unknown artifact paths or contents and
do not advance the completed full-audit timestamp. Ordinary generation, capture,
export publication and unrelated managed mutations must reject this state.
Return account-deleted plus explicit incomplete-copy-cleanup status when filesystem
ownership/integrity cannot be proved. Never delete a corrupt or unrelated file by
guessing ownership.

Subsequent restoration must hash the entire known artifact inventory and prove
that it matches the prior authenticated manifest, together with the current exact
SQL receipt. Only that proof may restore complete trust automatically. Unknown
files or mismatched content continue to block ordinary work and require explicit
operator repair/enrollment. This verification is distinct from blindly reconciling
current filesystem contents.


<a id="source-privacy--failure-concurrency-and-recovery-rules"></a>

#### Failure, concurrency and recovery rules

- A failed SQL transaction leaves the previous account and checkpoint intact.
- SQL commit followed by process loss must recover the exact reclamation receipt
  into an incomplete checkpoint; it must never recover as a complete audit.
- A checkpoint rename followed by process loss must clear only its matching
  pending operation. The existing bounded authenticated recovery journal applies.
- Account ownership serializes capture, deletion and export revocation. Global
  admission and the ledger fence serialize accounting and all unrelated writers.
- A known copy-removal operation may complete its original signed plan. An unknown
  or owner-dependent pending operation must be resolved safely before deletion;
  no lock inversion or recursive key creation is permitted.
- Missing control trust, independent key access, SQL trust or safe ownership still
  fails closed. The exception authorizes no data growth or provider/email work.


<a id="source-privacy--migration-deployment-and-rollback"></a>

#### Migration, deployment and rollback

Prefer the existing incomplete-inventory representation and a bounded,
null-omitted operation-purpose marker in authenticated metadata. No new private
payload or plaintext owner identifier belongs in the control journal. Verify that
the receipt and its maximum serialization remain inside the approved allowances.
No application-data migration is expected; confirm this during implementation.

Keep the exception disabled until its tests pass. Older binaries must fail closed
on an incomplete checkpoint or unsupported authenticated receipt. Rollback must
retain committed SQL deletions and the incomplete checkpoint; it cannot recreate
deleted personal data. Resume with compatible code and verified repair, never by
removing the intent or resetting counters.


<a id="source-privacy--required-acceptance-tests"></a>

#### Required acceptance tests

1. The reproduced HTTP case revokes credentials/capabilities and purges all owned
   SQL data, preserves every other account/shared row, reports incomplete cleanup,
   and retains unknown/corrupt files.
2. New artifact creation, normal writes and delivery remain blocked after the
   exception; known content is never enrolled implicitly.
3. Independent recomputation proves exact accounting after success, rollback,
   credential failure and concurrent unrelated mutations.
4. Process/backend failure before SQL commit, after SQL commit, after checkpoint
   rename and before journal cleanup preserves the required incomplete state.
5. Same-size tampering, missing/extra files, symlinks, changed schema/triggers,
   bad MACs and unknown SQL commits cannot enter the exception or restore trust.
6. Capture/export/deletion races preserve ownership and revoke access; busy owner
   recovery never waits while holding global admission.
7. Full verification restores trust only after the original manifest matches
   exactly. Remaining discrepancies require operator action.
8. Measure reclamation latency, allocations, database work and filesystem scans
   against the existing deletion baseline; do not describe blocked failure timing
   as successful deletion throughput.


<a id="source-privacy--alternative"></a>

#### Alternative

Retain global fail-closed deletion blocking until an operator restores artifact
trust. This preserves the strict storage contract but explicitly changes the
existing privacy behavior and requires a documented account-deletion availability
and retention limitation. No implementation should silently choose between these
policies.



<a id="source-privacy--implementation-notes-and-compatibility"></a>

#### Implementation notes and compatibility

The implementation uses the existing `Reconciled=false` inventory state and a
null-omitted `PrivacyReclamation` purpose marker in the authenticated receipt.
No application-data migration or personal payload was added. Successful privacy
SQL commits preserve the previous artifact measurements and audit timestamp.
Ordinary signed-mode admission can restore trust only after a full content check
matches that exact manifest. Pending privacy operations recover into incomplete
state, and confirmed SQL rollback never authorizes filesystem changes.

The established legacy **unsigned** test contract also permits owned-data purge
when its inventory file is absent. That compatibility path independently verifies
the exact SQL ledger/schema, publishes only an incomplete inventory, and requires
explicit enrollment before later growth. It does not claim cryptographic recovery
or artifact trust. Signed mode still rejects a missing or invalid checkpoint,
missing independent key or unknown SQL receipt; the approved exception does not
relax those requirements.
<!-- END SOURCE SNAPSHOT: docs/renewal/privacy-reclamation-integrity-review.md -->

---

<a id="source-storage"></a>

## Appendix A08 — Storage, accounting, coordination and recovery contracts

Original: [docs/renewal/guest-discovery-storage.md](docs/renewal/guest-discovery-storage.md). Full source text. Original source: 14,662 UTF-8 bytes, 224 lines.

Source SHA-256: `d61d425d489fb0f7b8252de12ecb567fe8adc9ae161ce4adad0b2d827f405850`.

<!-- BEGIN SOURCE SNAPSHOT: docs/renewal/guest-discovery-storage.md -->

<a id="source-storage--discovery-storage-and-job-coordination"></a>

### Discovery storage and job coordination


<a id="source-storage--authenticated-recovery-foundation--2026-10-25"></a>

#### Authenticated recovery foundation — 2026-10-25

The approved performance remediation's G1a adds an opt-in authenticated checkpoint
and exact committed-operation receipt for coordinated database writes. This is the
protocol foundation, not activation of incremental verification or the combined
local-capture runtime. All full artifact checks remain. Complete writer integration
and deployment instructions must pass G1c before configuring this mode on a runtime.

Verification configuration uses `GenerationStorage:IntegrityKeyPath` and a stable
`IntegrityKeyId` (default `v1`). The key is exactly 32 externally supplied bytes,
owner-readable only, outside artifact roots and the separate private control
directory. No key is generated or replaced automatically. Control directories use
0700 and published control files 0600; symlinks, unexpected control entries and
oversized files fail closed. The authenticated durability adapter currently supports
Linux directory fsync. Key rotation is not yet implemented; do not change a deployed
key/ID or remove it to fall back to unsigned storage.

The additive migration supplies nullable `ManagedStorageLedger.CommitReceiptJson`,
bounded to 65,536 UTF-8 bytes by a database constraint. It contains only storage
instance/generation, random operation identity, predecessor checkpoint hash,
reservation, exact committed revision, key identifier and authentication tag. It
contains no account identifiers, email, taste payload or secret key. Like the existing
ledger, it is excluded from individual exports and row-delta triggers. Admission
adds a conservative **327,680-byte control allowance** to the larger of database
physical/logical bytes, covering the receipt and four bounded filesystem control
files, rather than silently omitting these bytes. A changed schema/charge requires
explicit operator reconciliation after migration, with writers stopped.

An ordinary signed write durably records its authenticated intent before SQL,
stores the authenticated result in the same SQL transaction as the row/ledger
changes, and publishes/flushed-renames its checkpoint after commit. A later writer
may recover that exact committed result only when the signed predecessor, intent,
receipt, schema/revision and full artifact hashes agree. Unexplained changes still
stop. SQL rollback and a crash after checkpoint rename but before intent deletion
have separate authenticated cleanup paths. Request cancellation cannot cancel the
bounded post-commit bookkeeping step. A bare new database revision never authorizes
recovery.

Initial legacy inventory enrollment is explicit and full-scanning. An already
signed inventory cannot be silently re-enrolled using a missing/different key.
Do not downgrade an enrolled instance to old binaries or apply a Down migration
while writers are active; follow the approved stopped-writer recovery/rollback plan.
See [current evidence](#source-evidence) for verified scope and
remaining activation work.

Status (2026-10-17): C7 mechanics are locally verified. The website runner and new
submission flow are not enabled yet. This document describes the guard contract,
not a measured VPS capacity or production-readiness claim.


<a id="source-storage--accounting"></a>

#### Accounting

`AddManagedStorageLedger` established row accounting and the initial backfill.
`BatchManagedStorageAccounting` replaces its DML row triggers with three AFTER
STATEMENT triggers using old/new transition tables; the BEFORE TRUNCATE trigger
retains its previous rule. Bulk statements update the shared ledger once per
affected table/event. Full/core revisions still advance by affected row count,
including no-op updates; empty statements do not advance revisions. Migration
verifies existing totals under table locks and fails on mismatch. Normal admission
reads counters and schema metadata rather than serializing private history.
Logical accounting charges the UTF-8 JSON row size plus a
provisional 512 bytes per row. This is a conservative admission estimate, not an
assertion about each row's actual index size.

Admission uses the larger of logical accounting and `pg_database_size`. Its schema
fingerprint uses SHA-256 over relation/column metadata and complete stored trigger/
function records, including code and security settings. It checks all four trigger
shapes and prepares one fixed query per physical connection without caching results. Uncovered or
disabled accounting triggers, missing counters, changed schema and unexpected row
mutations stop admission. Relation size can change without a row mutation; the
coordinated path charges the current observed size when the complete counter and
schema revision remain unchanged. It does not silently discard physical growth.

The ledger keeps both the historical weekly core revision and an all-write
revision. The old weekly/local-capture guard compares its original core scope;
physical-size, artifact and cutoff checks remain. New discovery admission compares
the complete revision. This preserves local Worker mechanics without ignoring
Identity, scheduler, catalog or other managed writes in the new path.

Global counters are operational metadata, not privately owned profile records.
They contain no user payload and are excluded from individual account exports.
Every ledger column is in the reviewed schema inventory. Private jobs remain
exported/deleted through their profile; credentials and lease tokens remain
excluded security internals. Terminal jobs clear their private input payload.


<a id="source-storage--short-managed-writes"></a>

#### Short managed writes

`DiscoveryWriteCoordinator` takes the shared database lease, filesystem lease and
reservation only around admission, transaction and inventory publication. Provider
I/O must happen outside that callback. The transaction fences the ledger, verifies
the starting revision, applies accepted input/job changes together, then checks
actual logical and physical growth. A failed check rolls everything back. The
all-write and schema revision are validated again before publication.

After commit, the inventory file is written, flushed and atomically replaced under
the same leases. A failed or interrupted publication leaves a revision mismatch;
new writes stop until explicit reconciliation. An HTTP disconnect does not cancel
the bounded bookkeeping step after a successful database commit.

The existing managed-write stop remains 1,000,000,000 bytes. It is **not** a 10 GB
filesystem size or a total VPS allowance. PostgreSQL WAL, other databases, images,
logs and host files are outside `pg_database_size`; filesystem headroom must also
be measured. `DiscoveryStorage:FilesystemPaths` must name readable paths on every
relevant database/WAL/artifact volume as seen by the application. An unavailable
volume fails closed. `MinimumFreeBytes` defaults to 512 MiB. Operator configuration
must describe the actual deployment mounts; the application cannot infer remote
database free space from its own root filesystem.

The inventory has a five-minute freshness requirement. The enabled runtime renews
only a known baseline every minute and reclaims expired guests and public evidence
in bounded passes. Account writes, weekly persistence, scheduler activity and
unsubscribe updates use coordinated admission. Provider acquisition occurs before
the short final write lease. Concurrent optional local email capture remains
blocked in both hosts. Public-cache quotas and coalescing are implemented; source
approval and real-catalog coverage remain independent gates. See the measured
[resource profile](docs/renewal/guest-discovery-capacity.md) and [local runbook](docs/renewal/guest-discovery-local-run.md).


<a id="source-storage--explicit-reconciliation"></a>

#### Explicit reconciliation

The local Worker's existing `reconcile` action now verifies ledger totals against
all covered rows before recording filesystem/database inventory. Verification
uses short database coordination and table locks; it is an explicit integrity
operation, not a page-view or per-job history scan. A mismatch stops without
silently repairing counters. Stop writers and investigate/restore from a verified
backup if corruption is reported. Do not manually zero counters to bypass a stop.

Apply migrations with writers stopped and a verified backup. Existing inventory
revisions become stale after the accounting migration and need explicit
reconciliation. `EnsureCreated` does not install the custom SQL triggers and is
not a substitute for migration. Future migrations that add a table must install
all three DML statement triggers and the TRUNCATE trigger, and extend the reviewed
inventory in the same change. Stop both hosts for this migration and any rollback;
verify a matched database/artifact/key backup first, then independently verify
accounting and explicitly reconcile the changed schema fingerprint. Down restores
row accounting with data/counters intact but does not make a mixed-binary or live
downgrade safe. General signed-mode activation still waits for G1c writer coverage.


<a id="source-storage--jobs"></a>

#### Jobs

Requests use owner/key uniqueness and content hashes for idempotency. The same key
with another intent conflicts. Queue capacity and cooldown are admission controls;
there is no configured user-count ceiling. At most one unfinished generation is
allowed per profile, with an additional store check for lookup work.

Workers claim eligible rows with `FOR UPDATE SKIP LOCKED`. Random lease tokens,
expiry, bounded attempts and retry dates survive process restarts. A replaced or
expired token cannot renew or complete. Compound operations lock the profile
before the job, matching deletion/import order. Completion rechecks the stored
lease and current profile revision; stale tracked objects are reloaded before
fencing. Terminal outcomes revoke leases and clear raw input payloads.

The runtime supplies the consumer, safe error classification, source fairness,
progress API and transactional publication. Routes remain opt-in; start the
consumer with valid source/storage configuration before accepting requests.



<a id="source-storage--capture-and-privacy-reclamation-continuation"></a>

#### Capture and privacy reclamation continuation

The locally verified G1c capture/removal foundation prepares rendering, encryption and receipt reads
outside global admission. Capture then fences the global inventory and SQL rows,
revalidates eligibility and the stored-content fingerprint, and publishes/fsyncs
the immutable encrypted file before committing capture state and its exact signed
receipt together. Recovery retains the original final ciphertext. A complete,
authenticated temporary file can be promoted; an incomplete owned temporary can
be cleaned. Unknown files are not adopted or removed.

Local protection keys are created/rotated through independent coordinated writes;
Identity/JWT protection purposes are unchanged. Recovery requires existing usable
keys and cannot recursively create a key while another storage operation is
pending. Keep the independent storage key, shared data-protection key store,
database and artifact backups matched across all compatible hosts. A very long
outage with only a framework-unusable expired data-protection key can require
operator maintenance; no custom expiration or cryptographic override is present.

The developer approved the narrow account-deletion exception documented in
[the privacy reclamation review](#source-privacy).
When artifact trust fails but signed database trust and independent exact SQL
accounting remain valid, account deletion may purge only owned SQL data and
credentials. It publishes a signed `Reconciled=false` checkpoint with the prior
artifact measurements and prior audit timestamp. It does not enroll unknown
contents. Normal mutations and delivery remain blocked. An unresolvable pending
operation, invalid signed checkpoint, missing independent key, unknown SQL revision
or accounting mismatch still prevents the exception.

An account-deleted response may report incomplete local-copy cleanup. Investigate
unknown/corrupt files and retry verified owned-copy removal; do not delete a file
by guessing its owner. After repair, only a full content comparison against the
prior authenticated artifact manifest may restore ordinary trust automatically.
A missing signed manifest is not repaired by the privacy exception. Legacy
unsigned missing-inventory compatibility permits verified SQL purge but requires
explicit enrollment before later growth and has no cryptographic recovery claim.

Archive removal now uses a bounded signed file plan and a SQL control-only commit.
After interruption it can complete only the original authorized removal; it cannot
recreate or delete unrelated content. Legacy conversion now uses a distinct signed
conversion plan and control-only SQL commit. It preserves historical receipt bodies,
retains source plaintext until the verified encrypted final is durable, and journals
source cleanup. Recovery reuses the original ciphertext; another owner's target is
never adopted or removed. Bare signed capture/conversion/removal adapters reject
uncoordinated writes. The combined job-host/local-capture roots require an explicit
independently held integrity key. Full content scans remain enabled; incremental
verification is a separate stage. No production deployment is implied.

Full inventory hashing now uses one bounded reusable buffer per scan, checks exact
observed lengths and clears its buffer before returning it to the pool. Existing
fingerprints and full content verification boundaries are unchanged. This memory
optimization needs no enrollment/migration and does not enable incremental mode;
operators still need the remaining manifest/audit work and deployment acceptance.

For the opt-in manifest format and receipt-anchor upgrade, follow the
[manifest integration and maintenance guide](#source-manifest).
Live and retired manifest bytes are charged outside the fixed control allowance.

G1d3 adds a process-local strict read cache for validated manifest parts larger
than 64 KiB. It retains only the current authenticated generation's live metadata,
at most 48,000,096 bytes, and still hashes the on-disk part and consumed buffer.
This is not a process RSS limit or incremental activation. The cache holds opaque
path/content hashes and sizes/times; it adds no persisted user records, copies or
SQL state. See the integration guide for lifetime, concurrency and memory bounds.
Unknown metadata remains untrusted, and removal of the configuration cannot
silently downgrade the format. An ambiguous legacy null-operation checkpoint with
an existing SQL receipt requires stopped-writer verification/reconciliation.
This stage retains full content scans and does not activate incremental admission.
<!-- END SOURCE SNAPSHOT: docs/renewal/guest-discovery-storage.md -->

---

<a id="source-contracts"></a>

## Appendix A09 — Full binding renewal contract context

Original: [docs/renewal/contracts.md](docs/renewal/contracts.md). Full source text. Original source: 44,122 UTF-8 bytes, 465 lines.

Source SHA-256: `c1bf5cfa4db88fee652c402bf04fd4d1f347099c9606f160ee7e16b999dfc3c1`.

<!-- BEGIN SOURCE SNAPSHOT: docs/renewal/contracts.md -->

<a id="source-contracts--renewal-contracts-and-pending-proposals"></a>

### Renewal contracts and pending proposals

The 2026-10-06 user-supplied renewal plan takes precedence over older specifications.
Phase 1 execution was authorized with unchanged PRs 1A–1K. On 2026-10-07, the developer requested
Phase 2 execution and explicitly approved its exact membership and offline legacy-test replacement;
see [Phase 2 design](docs/renewal/phase-2-design.md) and [evidence](docs/renewal/phase-2-evidence.md). The developer subsequently
requested Phase 3 execution; see [design](docs/renewal/phase-3-design.md) and [evidence](docs/renewal/phase-3-evidence.md).
The developer then requested Phase 4 implementation; its tooling is verified offline, with the
concrete live run still pending. See [Phase 4 evidence and procedure](docs/renewal/phase-4-evidence.md).
Phase 6 local work was requested on 2026-10-09, followed by an explicit provisional baseline-A
evidence bypass. Its concrete persistence/API/policy design was approved and implementation
explicitly requested on 2026-10-10; local verification is recorded in [Phase 6 evidence](docs/renewal/phase-6-evidence.md). Quality remains
unverified and real evaluation remains open. The developer requested Phase 7 local work on
2026-10-11 and selected working local unsubscribe with one opt-out column and matching
API/export changes. The developer then approved its [written design](docs/renewal/phase-7-review.md);
its [implementation plan](docs/renewal/phase-7-plan.md) and sequential execution were subsequently
approved. Local implementation is verified with developer confirmation pending; see
[Phase 7 evidence](docs/renewal/phase-7-evidence.md). Phases 5/8–9 and live run manifests remain separately
gated. On 2026-10-06, the developer explicitly
approved this PR 1A revised written plan, numeric evaluation thresholds, pilot adequacy bars and
flagged interpretations in response to the written-plan approval question. This is planning approval;
that approval alone did not authorize Phases 2–9, and concrete run manifests retain separate approval.
No recorder execution, live Last.fm calls, real scoring, project scaffolding or code changes form part
of this planning revision. Phase 1 work remains authorized under its existing separate-approval limits.
See [PROGRESS](#source-progress) for current decisions and gates.

Approval scope: the evaluation protocol, pilot bars, manifest format and evaluation-only formula
definitions below are approved for planning. Descriptions retained as proposals do not indicate
pending reapproval of those same values. Phase 2 exact profile membership and its offline legacy-test
replacement are now approved as documented above. Production formula adoption, retention, live
acquisition, broader schema/API/test-contract changes and individual run manifests still
require the decisions specified below. No implementation was confirmed by this planning approval.
The three reviewed interpretations are accepted: recorder-only calls apply to evaluation, with
production acquisition unresolved until Phase 6; tags sum to the tag subtotal and named components
sum to the final score; diversity/novelty are formula-selection tie-breakers while the evaluation
formulas retain their novelty component.


<a id="source-contracts--specification-reconciliation"></a>

#### Specification reconciliation

The six directories in PROGRESS replace older five-layer examples: EF/Identity/repositories belong
in DataAccess; provider clients/cache/email in Infrastructure; the host is Presentation, not Web/Api.
.NET 10 is targeted; EF Core 10 remains intended, while checked-in packages are 9.x.
Infrastructure specification §§1, 5.4, 6.2, 7.1, 8.2 still identify integration/import requirements,
but ListenBrainz and both providers' username/history imports are deferred. Spotify APIs are out of V1.
§13 deployment and §15 hardening remain requirements subject to approval; CI is not explicitly specified.
The prior soft-delete design is insufficient for physical account deletion.
User exports must cover taste/account data, not disclose hashes, refresh secrets or protection keys;
the exact versioned export boundary must be reviewed before changing that API.


<a id="source-contracts--revised-phase-order"></a>

#### Revised phase order

The order and written plan are approved; **not approved** below describes execution status.

Number mapping: old 2 -> new 2-5 (split), old 3 -> new 6, old 3A -> new 7,
old 4 -> new 5, old 5 -> new 8, old 6 -> new 9.

1. **Baseline and security — authorized, unchanged.** PRs 1A–1K retain their existing scopes,
   separate local commit sets, verification requirements and developer-confirmation requirement.
   This revision belongs to PR 1A; it does not authorize scorer, provider or test-contract changes.
2. **Harness mechanics — authorized; implementation verified locally, confirmation pending.** Fixture-only harness with an HTTP handler that rejects
   outbound requests, metric implementations and founder/development/held-out/synthetic profile
   sets. Lock held-out membership before any harness scoring, including synthetic mechanics runs.
   Write the separate recorder but do not run it live. Build the acquisition storage guard using fake
   inventory/response inputs. No Last.fm dependency is needed to complete these mechanics.
3. **Honest ingestion, in memory only — authorized; implementation verified locally, confirmation pending.** First verify the concrete design needs no
   database schema change; if it does, stop and ask. Remove invented tags, listener counts and
   match values. Preserve absent/invalid evidence explicitly, all discovery paths, attribution and
   candidate provenance (`live`, `recorded`, `synthetic`). Unknown seeds produce coverage gaps,
   never substitute fixture artists. Exercise this with offline responses only. No persistence.
4. **Pilot — tooling authorized/verified; live run pending.** Gate: developer approval of a concrete manifest (endpoints, exact
   seeds, request maximum, pacing, estimated bytes, absolute output directory), pilot adequacy bars
   and numeric scoring rules before the run. One small local recording, about 5 MB, covering all
   six founder clusters plus a few additional seeds. First report yield per seed, tag noise,
   missing popularity, coverage gaps and size against the preapproved bars below. Then compare
   A–D directionally and perform the developer's blind review. Nothing is tuned or promoted.
   Every table, chart, listening result and summary is labelled **pilot, directional**. Inadequate
   evidence is reported as such; no automatic extension, rerun or expanded budget.
5. **Real evaluation — not approved.** Gate: Last.fm reply reviewed for applicable restrictions,
   or the developer's explicit decision to proceed without one. A reply alone is not blanket
   permission. Obtain a second approved manifest for full development and held-out acquisition.
   Record first; compare scoring second; the developer chooses the formula last. Development-only
   tuning precedes configuration freeze. Include IDF corpus size, genre balance and sensitivity,
   and a defined, versioned, persisted rule for multiple match values in C. Evaluate held-out once
   using the predeclared batch below. If results motivate a change, retire the set to diagnostic use
   and ask for a fresh set. If the Last.fm gate takes too long, the developer chooses to wait longer
   or proceed to Phase 6 with baseline A without real evidence. The agent never chooses this escape.
   Waiving the reply to run evaluation and bypassing evaluation entirely are distinct decisions.
6. **Recommendation pipeline and persistence — design approved; locally verified, developer confirmation pending.**
   Provisional baseline A was explicitly approved under an evidence bypass on 2026-10-09;
   quality remains unverified and evaluation remains open. The concrete JSON/API, ordinal selection,
   feedback, no-automatic-deletion and local storage policy is approved. See
   [design](docs/renewal/phase-6-review.md) and [implementation evidence](docs/renewal/phase-6-evidence.md).
   Application orchestrates signals →
   materialization → discovery → hydration → pure ranking → persistence; provider details stay in
   Infrastructure. Use the formula chosen in Phase 5, or A on explicit evidence bypass. Resolve the
   hash/ordinal/representative-track selection contract, retention and categorical/numeric feedback
   conflicts. Present concrete schema changes and export consequences for approval before building.
   Verify that complete, versioned breakdowns are extensible JSON and that adding a formula needs
   no migration; JSONB mapping alone does not prove this. Include old/new payload round trips,
   unknown-field preservation, formula/configuration versions and replay from stored evidence.
7. **Worker-to-local-email slice — design and plan approved; implementation verified locally,
   developer confirmation pending.**
   See [approved source reassessment and contracts](docs/renewal/phase-7-review.md) and
   [implementation plan](docs/renewal/phase-7-plan.md). The developer selected
   working local unsubscribe, one opt-out column and matching API/export changes. Implemented
   Worker → offline generation → persisted digest → HTML/plaintext → local sink, including
   explanations, unsubscribe, short/empty lists, retries and recovery. Export 2.1 exposes the
   nullable `Users.EmailUnsubscribedAtUtc`, LocalCaptured lifecycle and sanitized owned copies/issues.
   Local file receipts authenticate ownership, render inputs and body integrity; deletion reports
   incomplete cleanup after SQL commit. No token/delivery table was added and SentAt stays null.
   Final suite: 386 passed, zero failures/skips, including PostgreSQL. Actual fixture process/browser
   checks and the mandatory reassessment are in [evidence](docs/renewal/phase-7-evidence.md); use the
   [local procedure](docs/renewal/phase-7-local-run.md) for developer confirmation.
   Reassessment covers end-to-end behavior, security, evidence, storage, terms,
   artist tags' suitability for track discovery, cost and complexity. The developer explicitly
   chooses proceed, narrow or revise before later phases; nothing starts automatically.
8. **Privacy and delivery — not approved; two separate approvals.** (a) Complete table/column
   inventory, export and physical deletion verification, including new Phase 6 data, shared catalog,
   backups and access invalidation. Phase 1F deletion fixes remain authorized and are not moved here.
   (b) Scheduling, unsubscribe enforcement at send time, cancellation and delivery recovery;
   verify database atomicity, idempotency and email failure windows separately. A local sink in
   Phase 7 does not authorize external sending. Approval of (a) does not approve (b), or vice versa.
9. **Reproducibility and release preparation — not approved.** Docker/Compose, CI and documented
   Last.fm written public-page approval. Release stays blocked until that approval is documented,
   even if the Phase 5 reply was waived. Hosting/email choices, purchases, provisioning, public
   deployment and external sending retain their separate approvals.


<a id="source-contracts--evaluation-boundaries-and-profile-locks"></a>

#### Evaluation boundaries and profile locks

- Evaluation code depends on Application and Domain; neither depends on evaluation. Prefer the
  existing Application.Tests project for orchestration/metrics and Domain.Tests for pure checks.
  The harness has no live source or credential discovery, and never instantiates the hybrid client.
  In evaluation, the separate recorder is the only tool allowed live calls. Tests always reject
  outbound requests, including when credentials exist. Replay reads only approved local recordings.
- Check V1_FolderStructure.md before adding any project or folder. Its `scripts/` area can host a
  standalone recorder without a new .NET project. A dedicated evaluation project/folder is not in
  the blueprint: present its exact path and references and obtain approval before adding it.
  No project or folder is added by this revision. Output-directory creation also belongs to the
  approved recording manifest, not this planning pass.
- Approved Phase 2 membership: six founder profiles, 16 development profiles (two per each of the eight benchmark
  genres), 16 held-out profiles (two per genre), and the existing eight-genre synthetic mechanics
  fixture plus missing/invalid/tied-input cases. These are reviewed profile definitions, not acquired real data.
  Exact membership, seeds, canonicalization and split hashes must be locked before any harness
  scoring; named profiles were reviewed in Phase 2. Development/held-out profiles contain two
  to five declared artist/tag seeds; preserve sparse profiles as such, never auto-fill them.
  No response acquisition is needed
  to lock membership. Founder/pilot seeds cannot be held-out seeds; development and held-out seed
  identities are disjoint. Report upstream candidate/artist overlap without changing the split.
- Synthetic data establishes mechanics only, never coverage or musical quality. Founder/pilot data
  is diagnostic, not held-out. Do not inspect held-out tag frequencies, corpus composition, rankings
  or outcomes during development. Allow acquisition-integrity checks only (hashes, parse success,
  request/byte limits); keep held-out content and statistical reports sealed until freeze.
- Freeze hashes for code, profile membership, recordings, normalization/stoplists, A–D definitions,
  weights, candidate eligibility, tie policy, IDF corpus/factors, match aggregation, metrics,
  thresholds and blind-review protocol. Persist this local record before held-out evaluation.
  The once-only held-out batch includes all predeclared formulas, cutoffs and perturbations;
  those checks are one frozen experiment, not opportunities to tune and rerun. Any result-driven
  revision retires the set and requires a newly approved confirmation set.
- Identical eligible pools and common ordering rules for A–D, at k=3 and k=5. Never pad short lists.
  Known/disliked tracks are excluded; siblings remain eligible. Proposed evaluation-only tie rule:
  canonical track key ordinal. Freeze it for comparisons; it does not settle Phase 6 production
  selection. Preserve upstream rank separately. Popularity must never enter A–D ranking weights.
- Recordings, run manifests, locks, score outputs, ratings and generated reports stay local,
  gitignored and untracked. Proposed containment: an approved absolute path under `recordings/`,
  whose ignore rule already exists. Fail if a target is tracked, resolves outside that root or
  would overwrite an earlier run. Plans and the manifest template here are not generated reports.


<a id="source-contracts--numeric-evaluation-contract--approved-for-planning-on-2026-10-06"></a>

#### Numeric evaluation contract — approved for planning on 2026-10-06

The developer approved these evaluation thresholds before any real scoring. They are not
measurements, scientific guarantees or adoption of a production scoring policy. Changes require review.
Metric denominators include short/empty lists and missingness as specified; show raw counts as well
as percentages. Evaluate guardrails on all 16 held-out profiles, and report each genre separately.

| Check | Proposed definition and acceptance bar |
|---|---|
| Finite scores | 100% of emitted scores, components and contributions finite; zero NaN/infinity. Invalid input must be rejected or marked missing with a reason, never coerced into evidence. |
| Authentic evidence | Zero fabricated tags, counts, matches or source labels. Every real datum traces to a response hash/path; 100% of candidates carry provenance. Zero live/synthetic blending. |
| Determinism | 100 repetitions and 100 seeded permutations of candidates, seeds, tags and discovery paths, under invariant, en-US, tr-TR and az-Latn-AZ cultures. Exact ranked keys and canonical numeric breakdowns must match on the pinned runtime. No clock/process hash dependence. |
| Explanation arithmetic | Store every weighted normalized tag contribution, not only the top five. Their sum equals the tag score within `1e-10 * max(1, abs(score))`; the tag subtotal plus separately named novelty/match components equals the final score at the same tolerance. Never assign non-tag evidence to invented tags. |
| Network isolation | Zero outbound attempts in ordinary tests and the harness; a rejecting handler makes any attempt fail. Recorder tests use fakes. The legacy live spike cannot remain an ordinary test entry point. |
| List coverage | Fraction of all profiles reaching at least 3 eligible picks. Challenger may lose at most 5 percentage points versus A. With 16 held-out profiles this permits zero additional failing profiles. Also report full 5-pick coverage and per-seed yield. |
| Artist concentration | At each k, macro-average the largest single-artist share per profile (`largest artist count / actual list length`; empty list conservatively 1). Challenger may exceed A by at most 0.10. Across all pick slots, the most represented artist's share may exceed A by at most 5 percentage points. |
| Weight stability | Change each positive configurable coefficient one at a time by ±5% and ±10%; leave zero coefficients zero, then also test joint extreme combinations. Use mean top-k overlap `intersection / max(original length, perturbed length)`; both-empty = 0. At k=3 and 5: at least 0.80 for ±5%, 0.70 for ±10%, and no more than 0.05 below A under the same perturbation protocol. Worst perturbation must pass. |
| Popularity-collapse revisit | Diagnostic only. Using a predeclared comparable listener measure, flag if over 60% of selected known-popularity picks come from the highest candidate-pool popularity decile AND this exceeds A by over 15 percentage points; also flag a rise of 0.20 in median within-pool popularity percentile versus A. Either flag requires developer review before promotion; never auto-add a penalty. |
| Popularity missingness | At least 80% of pooled eligible candidates and 80% of selected picks must have that same authentic measure to assess the collapse check. Otherwise mark the guardrail unassessable and the replacement decision inconclusive pending explicit review. Never replace missing counts with zero or use top-track listeners as artist-wide popularity. |

Correctness failures block the affected comparison entirely. Guardrails cannot be traded away for a
better average score. Stability measures ranking robustness; it is not a preference signal. Retain
the per-profile results so a macro-average cannot hide a coverage failure.


<a id="source-contracts--blind-preference-signal-and-decision-rule"></a>

##### Blind preference signal and decision rule

For each of the six founder profiles, pool the top five from every formula, deduplicate by canonical
track identity, shuffle reproducibly with a concealed seed, and hide formula, rank, score and
explanation labels. The developer rates each track like / neutral / dislike. The same rating serves
all formulas containing that track within that profile. Maximum workload: 20 tracks per profile,
120 across six before deduplication. Smaller option: three profiles chosen before results are seen,
at most 60 tracks; retain the same margins but label the narrower preference evidence explicitly.
Pilot and final reviews are separate. Before the Phase 5 freeze, identify already auditioned pilot
tracks and exclude them from the final blind pool for all formulas, documenting lost coverage.

Proposed numeric encoding: like=1, neutral=0, dislike=-1. For each formula/profile compute the sum
of its top-five ratings divided by 5; unfilled recommendation slots contribute 0 and remain visible
in coverage. Every available pooled track needs a rating; an unrated track is not neutral and makes
the comparison incomplete. Macro-average over profiles. Set the winning margin in advance to
**at least +0.20 versus A**, with a positive difference on at least **4 of 6 profiles**, or **2 of 3**
for the smaller option. Report paired differences and counts; this is the founder's preference
evidence, not a population-level efficacy claim. No additional listening rounds to chase a margin.

A challenger is eligible to replace A only if it passes correctness, all held-out guardrails and
the blind-rating margin. Otherwise A remains; **inconclusive** is a valid result. An unassessable
guardrail cannot be called a pass. If multiple challengers qualify, prefer the higher blind mean;
within 0.05, use higher distinct-artist fraction and then observed novelty fraction only as
tie-breakers, leaving the final choice to the developer. Neither diversity nor novelty can make
a failing challenger eligible. Novelty here is relative to declared familiarity, not a claim that
the listener has never heard a track. A itself failing correctness stops scoring; retaining A is
not permission to ship an incorrect implementation.


<a id="source-contracts--pilot-adequacy-bars--approved-concrete-recording-manifest-still-required"></a>

##### Pilot adequacy bars — approved; concrete recording manifest still required

Proposed scope: the existing eight named founder seeds across six clusters, plus three additional
non-held-out seeds chosen in the concrete manifest. Founder clusters: Jakuzi; Son Feci Bisiklet;
Hotline Miami (M.O.O.N., Perturbator, Jasper Byrne); Dying Light (Paweł Błaszczak); ULTRAKILL
(Heaven Pierce Her); Hades (Darren Korb). These are planning inputs from the legacy spike, not
verified coverage. Proposed extra seeds: Agalloch, Pharoah Sanders and Altın Gün, subject to approval.

- Attempt all 11 seeds and all six clusters within the fixed budget. At least 8/11 seeds should
  yield five distinct eligible tracks; each founder cluster should yield at least ten tracks
  across three artists. Overall aim: at least 60 distinct tracks and 20 distinct artists.
  Report raw, deduplicated and eligible yield separately; every absent seed/cluster is a gap.
- At least 80% of distinct candidate artists should have two usable supplied positive-weight tags.
  Raw tag noise should be at most 30% of observed tag entries using a predeclared noise rubric;
  list raw/filtered counts, denominator and ambiguous tags. Do not edit the stoplist during pilot.
  Proposed rubric: the current hydrator's stoplist, frozen by hash after review, using trimmed
  invariant case normalization. The noise denominator is all nonblank artist/tag entries before
  filtering, counting each normalized tag once per artist. Ambiguous tags are reported separately,
  not retrospectively classified to improve the bar.
- Report listener-field availability at its actual track/artist scope, including denominator and
  endpoint, and every absent/invalid value. At most 20% missing is the proposed bar for a usable
  popularity diagnostic; failure limits that diagnostic, not permission to invent values or add
  endpoints. C needs valid comparable match evidence on at least 80% of artist-similarity paths
  for a useful directional comparison; report tag-source paths separately without assigning matches.
- 100% of retained responses must parse or have an explicit error/gap record and provenance.
  At least 95% of attempted requests should return usable responses. Any rate-limit response stops
  recording immediately. Authentication/permission errors also stop; retries are off for the pilot.
- Roughly 5 MB means a budget, not a minimum to fill: estimate 4,000,000 response bytes and cap at
  5,000,000 uncompressed bytes; cap the complete run directory at 6,000,000 bytes including reports,
  manifests and derived files. Lower size is fine if yield bars pass. Record actual bytes by category.
- If yield, tag or integrity bars fail, issue the shape report and stop before formula/listening
  comparisons pending developer review. Missing popularity/match bars mark the affected comparisons
  unassessable. Pilot D uses only the pilot's distinct artists for its explicitly exploratory small
  corpus, with no held-out statistics. No pilot result adopts weights, corpus,
  stoplists, aggregation, formula or production tie policy. All results: **pilot, directional**.


<a id="source-contracts--phase-5-idf-corpus-and-c-evidence"></a>

##### Phase 5 IDF corpus and C evidence

Propose a development-only IDF target of 800 distinct artists, minimum 400, with eight preassigned
genre strata (target 100, minimum 50 each). Assign each artist once for balance accounting using a
frozen rubric; report multi-genre ambiguity, unknown labels and duplicate identities separately.
No known stratum should exceed 25%; unknown labels should be at most 20% of the inventory. Unknown
artists do not fill named-stratum quotas. Failure makes D insufficiently supported, not grounds to
manufacture labels or expand acquisition without approval. These are practical sampling bars, not
a claim of catalog representativeness. No extra endpoint is implied by these targets.

Report corpus artist/tag counts, raw and balanced stratum counts, missing tags/labels, corpus hash,
candidate overlap and evidence provenance. Count each distinct artist once for document frequencies.
Fit IDF exclusively from development; never learn frequencies from held-out, even without labels.
On development only, compare 400 versus 800 artists if available, ten fixed balanced subsamples,
and eight leave-one-genre-out variants. Report score deltas, top-3/top-5 overlap, list coverage and
concentration. Proposed D robustness bar: mean top-five overlap at least 0.80 for subsamples and
0.70 for leave-one-genre-out; all coverage/concentration guardrails must hold. If only 400 artists
are available, label the larger-size comparison unavailable and seek approval before admitting D
to the frozen confirmation comparison. Freeze one corpus/factor set before held-out evaluation.

C's proposed `max-valid-match-v1` rule is specified below. Persist the rule version, all raw path
observations, validity/missing reasons, aggregated value and winning path in local evaluation
artifacts in Phase 5. Database persistence waits for Phase 6 schema approval. This avoids making
Phase 5 depend on the recommendation persistence pipeline.


<a id="source-contracts--draft-run-manifest-format--not-executable-authorization"></a>

#### Draft run-manifest format — not executable authorization

The following is a template, not an approved run. Exact values and hashes must replace placeholders;
the recorder must fail closed while any required field or approval is missing. No credentials or
credential-bearing URLs belong in the manifest. Endpoint names below are existing-client proposals;
current endpoint parameters, terms and response fields require fresh verification before recorder
implementation/use. This planning pass made no live calls and did not reverify Last.fm documentation.

```yaml
manifest_version: 1
run_id: <unique-id>
phase: 4                         # 5 requires a separate manifest and approval
status: draft
label: "pilot, directional"      # mandatory for all Phase 4 outputs
approval: {developer_reference: null, approved_manifest_sha256: null}
lastfm_gate: {reply_reference: null, explicit_waiver_reference: null} # required in Phase 5
verification: {terms_date: null, endpoint_contract_reference: null}
profile_lock_sha256: <hash>
protocol_sha256: <approved-bars-formulas-metrics-and-listening-protocol>
code_revision: <commit>
provider: Last.fm
credential_source: LASTFM_API_KEY # environment variable name only
seeds:                          # all 11 literal entries required in concrete pilot manifest
  - {set: founder, cluster: Jakuzi, kind: artist, value: Jakuzi}
  # Remaining seven founder and three additional seeds, with set/cluster membership.
endpoints:
  - {method: artist.getSimilar, per_seed_limit: 5, max_calls: 11}
  - {method: artist.getTopTracks, per_artist_limit: 5, max_calls: 66}
  - {method: artist.getTopTags, per_artist_limit: 15, max_calls: 66}
expansion: {depth: 1, max_distinct_artists_including_seeds: 66, order: canonical_ordinal}
request_budget: {max_attempts: 160, max_concurrency: 1, automatic_retries: 0}
pacing: {minimum_start_interval_ms: 1000, burst: false, respect_stricter_server_limits: true}
stop_on: [rate_limit, auth_error, permission_error, byte_limit, unknown_inventory, cancellation]
response_budget: {estimated_uncompressed_bytes: 4000000, hard_cap_bytes: 5000000, max_single_response_bytes: 100000}
storage_budget: {run_directory_max_bytes: 6000000, shared_stop_bytes: 80000000}
storage_inventory: {path: <absolute-local-path>, sha256: <hash>, measured_at_utc: <timestamp>}
output_directory: <approved-absolute-repo-path>/recordings/<run-id>
outputs: [responses, provenance, gaps, request_ledger, shape_report, evaluation, blind_ratings]
output_rules: {require_gitignored: true, reject_tracked: true, reject_overwrite: true, reject_path_escape: true}
retention: {expires_at_utc: <explicit-approved-date>, extension_requires_approval: true}
```

The pilot expansion bounds 11 seed similarity calls plus tags/top tracks for at most 66 artists:
143 planned calls before cache reuse; the 160-attempt ceiling is not an invitation to expand.
One request/second is proposed conservative pacing, **not a verified allowance**. A 100,000-byte
per-response bound is a storage safety proposal, not a measured response size. Abort and discard
an oversized partial response with a gap record; never silently parse truncation. Reserve space
for the bounded response and local metadata before every call. Cached reuse carries original
timestamps/provenance and still counts toward storage inventory. No bursts or automatic resumption.

Phase 5 uses the same format with explicit development/held-out partitions, sealed held-out output,
development IDF acquisition plan, founder blind-review candidate inputs, complete literal seed lists,
endpoint verification and limits
estimated from the pilot's bytes/yield. Approve exact numbers in that second manifest before any
full acquisition; no open-ended recording or automatic 5 MB budget multiplication. Partial runs
are incomplete evidence. Any retry/resume or changed manifest requires fresh approval.


<a id="source-contracts--track-selection-proposals-adoption-requires-approval"></a>

#### Track-selection proposals: adoption requires approval

Weekly tie-break: SHA256(Encode(version, userId, ISOYear, ISOWeek, canonicalTrackKey)), using
versioned length-prefixed UTF-8, canonical GUID format and supplied ISO week. Sort hash bytes
ascending, canonical key only on collision. Persist policy version and digest week for replay.
No clock, random values or GetHashCode. It avoids permanent alphabetical preference, not proof of fit.

Alternative: highest upstream-ranked eligible track per artist, skipping known/disliked tracks.
[artist.getTopTracks](https://www.last.fm/api/show/artist.getTopTracks) documents popularity order
and rank (checked 2026-10-06); other providers are not verified. Preserve order/provenance.
This adds within-artist popularity preference and cannot silently replace popularity-neutral policy.


<a id="source-contracts--scoring-proposals-adoption-requires-approval"></a>

#### Scoring proposals: adoption requires approval

A: ordinary cosine + configurable novelty. B: best-seed cosine + novelty, preserving winning seed.
C: B + authentic supplied match evidence. D: separately evaluate IDF cosine before combinations.
Proposed initial coefficients for the untuned pilot: tag similarity 1.0 and novelty 0.2 for all four;
C adds match weight 0.1. Popularity and feedback penalty coefficients are zero; known/disliked
track exclusion is eligibility, not a score penalty. All coefficients live in versioned configuration.
A uses the aggregate taste vector; B/C use the highest cosine over the profile's supplied seed
vectors (ties by canonical seed); D uses IDF on A's aggregate vector, not a B/C combination.
An empty/missing tag vector has no overlap evidence and gives a zero tag subtotal with a reason;
it must never be filled with a generic tag. These initial evaluation weights were approved with
the written plan; subsequent tuning is development-only in Phase 5, never on pilot or held-out outcomes.
The approved interpretation of diversity/novelty as tie-breakers applies to **formula selection**;
novelty remains a component of the A–D evaluation definitions. Production adoption remains separate.

For C, deduplicate by provider/seed/method; M(c) = max valid finite supplied match over paths.
Missing stays missing and gives no bonus. Persist `max-valid-match-v1`, evidence and winning path;
ties by canonical seed. Do not pool incomparable provider scores; blending remains deferred.
Validity includes the documented source range; invalid/out-of-range values remain invalid, never
clamped into evidence. Preserve repeated observations and their response hashes. Where the same
provider/seed/method repeats, use the maximum valid value as the path value, then maximum across
paths; equal values choose canonical seed, method and response hash ordinal. Record missing as
null in the evidence, although its numerical bonus is zero. Pilot C only exercises this proposed
rule directionally; Phase 5 freezes and persists the rule for the real evaluation.
For D, each distinct artist counts once; d(t) = 1 + log((A+1)/(df(t)+1)); unseen vocabulary uses 1.
Here A denotes corpus artist count, not formula A. Apply sqrt(d) to both vectors. Per-tag
contribution is `tagWeight*d(t)*c(t)*u(t)/(sqrt(sum d*c²)*sqrt(sum d*u²))`.
For ordinary cosine use d=1; B/C explain the winning seed vector. Zero denominators yield zero
tag subtotal, not division by zero. Store all normalized contributions, factors and non-tag
components so explanations reconstruct the entire score rather than raw dot-product fragments.
Report artist/tag counts, genre balance, missing labels and overlap. Development-only balanced
subsampling and leave-one-genre-out sensitivity must report metric/ranking changes.
Prefer aggregate A/df counts, version/hash and composition summaries over duplicated corpus;
aggregates still count as derived data. Retain actual factors in per-pick explanations.


<a id="source-contracts--storage-and-retention-proposals"></a>

#### Storage and retention proposals

Shared inventory covers recordings, HTTP cache, derived database data, reports/copies/backups.
Count uncompressed bytes conservatively, category totals, headroom and inventory age.
Acquisition fails closed at projected 1,000,000,000 bytes or unknown accounting; this is a
temporary unresolved placeholder, not a capacity decision. Set the real limit at build-order
step 9 once hosting is chosen. Bound responses.
At threshold, stop acquisition/enrichment and alert. Continue account, feedback, unsubscribe,
delete and delivery-state writes. Pause new derived recommendation snapshots if headroom is
insufficient/unknown; serve existing snapshots. Evict only under approved expiry policy;
never silently delete recommendation history. Count derived feedback without duplicating metadata.
Initially only Worker/recording execution acquires data, never concurrently; reconcile inventory
and conservative headroom before each batch. Reassess cross-process coordination at Phase 7.
Measure complete breakdown and provenance UTF-8 and PostgreSQL pg_column_size: median, p95, max,
total. Current sizes are not verified. Estimate at up to 260 picks/user-year plus all categories.
Retention proposal: response headers plus 24h cache maximum, 30d recordings (explicit extensions),
12mo recommendations/explanations, active seeds/feedback/familiarity until changed/deleted,
30d maximum backups included in deletion disclosures. None adopted. Adjust enrollment/retention
to measured capacity; expired recordings require another approved run for replay.

Phase 2's guard must work offline with fake inventories: aggregate all categories, reserve the
maximum next response plus derived-output overhead, enforce single-writer acquisition, reject
unknown/stale accounting and stop before projected usage reaches 80,000,000 bytes. Proposed
inventory freshness: reconcile at run start and after every write; an external change invalidates
the inventory and stops acquisition. Reports/copies/partial downloads count. The per-run cap is
additional to the shared cap, not a replacement. Guard tests cover exact-boundary, overflow,
oversized responses, cancellation, unknown inventory and concurrent acquisition attempts.
For Phase 4/5, retention is a required run-manifest decision; the general 30-day suggestion is
not silently adopted. Plans may name paths, but no recording/report output is tracked.


<a id="source-contracts--verified-conflicts-and-approval-points-read-only-inspection-2026-10-06"></a>

#### Verified conflicts and approval points (read-only inspection, 2026-10-06)

The table preserves the original audit. The ingestion and hybrid/unknown-fixture conflicts were
addressed locally in [Phase 3](docs/renewal/phase-3-evidence.md); the unsafe legacy spike was replaced with
an approved offline check in [Phase 2](docs/renewal/phase-2-evidence.md). Their baseline descriptions below
are historical, not descriptions of the current implementations. Endpoint/terms documentation
was refreshed on 2026-10-07 for Phase 3; unresolved production decisions retain their gates.

| Finding | Consequence for this plan |
|---|---|
| [Hydrator](src/Infrastructure/RecommendationSources/LastFm/LastFmCandidateHydrator.cs) inserts `indie=0.5`, defaults listeners to 10,000 and treats a top track's count as artist popularity. [Source](src/Infrastructure/RecommendationSources/LastFm/LastFmRecommendationSource.cs) invents matches 0.5/0.70 and discards later duplicate paths. | Phase 3 must remove these behaviors in memory and retain authentic scope and all paths; none is valid real evaluation evidence. |
| [RawCandidateTrack](src/Application/Common/Models/Recommendation/RawCandidateTrack.cs) and [CandidateTrack](src/Domain/Scoring/CandidateTrack.cs) have non-nullable numeric evidence and no complete provenance model. Neither is mapped by DataAccess. | A no-schema route is feasible using reviewed transient contract changes. Source inspection confirms these are not mapped entities; the concrete implementation's no-schema property still requires a Phase 3 preflight. Do not touch mapped Track/Artist fields, migrations or public APIs under this phase. Stop and ask if persistence becomes necessary. |
| [Hybrid client](src/Infrastructure/RecommendationSources/LastFm/LastFmApiClient.cs) falls back to synthetic data; [fixtures](src/Infrastructure/RecommendationSources/LastFm/Fixtures/LastFmFixtureProvider.cs) substitute data for unknown seeds. | Evaluation cannot reuse hybrid selection. Explicit provenance and coverage gaps require future changes; fixture-only mechanics must remain independent of credentials. |
| [Legacy spike](tests/Infrastructure.Tests/Spikes/FounderSeedCoverageSpikeTests.cs) reads credentials, can call live endpoints and writes a tracked documentation path. | Conflicts with zero-network tests and untracked reports. Replacement with a separate recorder and fixture-only test requires explicit test-contract approval; no deletion/weakening is authorized by PR 1A. Until then, exclude it from safe verification and do not claim the entire suite is network-isolated. |
| [Scorer](src/Domain/Scoring/RecommendationScorer.cs) applies popularity/feedback penalties; [parameters](src/Domain/Scoring/ScoringParameters.cs) default popularity to 0.3; contributions are only five raw products. | Existing behavior is not baseline A or a complete explanation. Evaluation needs approved pure A–D implementations plus all normalized contributions; no Phase 1 scorer rewrite is implied. Scorer changes must run the benchmark seed set without deleting policy-conflicting tests before review. |
| [JSONB mapping](src/DataAccess/Persistence/Configurations/WeeklyRecommendationConfiguration.cs), initial migration and current snapshot store ScoreBreakdown as JSONB. [ScoreBreakdown](src/Domain/Scoring/ScoreBreakdown.cs) is a fixed typed record with no formula version or unknown-field extension storage. | JSONB storage is verified in source only. Extensibility, preservation of unknown fields, runtime round trips and migration-free formula evolution are **not verified**. Resolve and test in Phase 6; do not claim JSONB alone fulfills the contract. |
| Existing ranking uses ordinal keys; the source selects only two upstream top tracks per artist. | An evaluation-only ordinal tie policy and common acquisition pool must be frozen before scoring. Upstream candidate-pool popularity bias is reported, not mistaken for a popularity weight. Production hash/ordinal/representative selection remains a Phase 6 decision; a materially different policy requires reassessing applicability of evaluation evidence. |
| V1_FolderStructure.md includes `scripts/` and a test suite, but no evaluation project; older tree examples combine DataAccess and Infrastructure. | Six-layer reconciliation still applies. Prefer existing projects; any new folder/project deviation needs approval before creation. Documentation edits here add neither. |
| V1_Infrastructure.md §§6–7 assigns live acquisition to the Worker/provider pipeline; the requested recorder-only live-call rule is broader if read literally. | This plan enforces recorder-only calls for evaluation. Production live acquisition is an unresolved scope question: before Phase 6, approve either a recorder-fed offline pipeline or an explicit production acquisition exception. Do not silently give the Worker live access. |
| Requested per-tag sums cannot equal a total that also contains independent novelty/match bonuses without misattributing those bonuses. | Approved interpretation: all tags sum to the tag score, and named components sum to the final score. |
| Prior A–D proposals include novelty as a weighted component; the new decision rule says diversity/novelty are tie-breakers only. | Approved interpretation: the tie-breaker restriction applies to selecting a formula after blind ratings, not the score formula itself. |
| Phase 1F already covers deletion fixes and Phase 1G feedback validation/replacement; Phase 6 introduces a feedback-conflict decision and Phase 8a verifies deletion/export. | Phase 1 scopes remain intact. Broader schema/API/export/test-contract changes retain separate review; unresolved categorical/numeric precedence must be presented before any Phase 1G change that depends on it, not guessed until Phase 6. Phase 8a re-verifies all later data. |
| Phase 1 approval excludes unapproved dependency/schema/API/export/test-contract changes; test isolation and evaluation require some such changes later. | This revision authorizes none. Present concrete diffs/contracts in their phases. Phase 9 dependency/CI work also remains gated. |
| Old specs/memory contain numeric API-rate and compliance claims; current Last.fm response-field availability and endpoint contracts have not been refreshed in this pass. | Verify authoritative documentation before coding against it or approving a real run. Endpoint names/pacing in the draft are proposals. No unsupported rate-limit, coverage or legal-compliance claims are adopted. |

No new README similarity-ownership claim was found in this pass: it describes deterministic
re-ranking of upstream candidates and qualifies unverified delivery. Historical changelog claims
remain explicitly qualified. This review does not certify the rest of the implementation.


<a id="source-contracts--attribution-and-enquiry-draft-only"></a>

#### Attribution and enquiry: draft only

[Last.fm terms](https://www.last.fm/api/tos) checked 2026-10-06: storage cap/caching (§4.3.4),
rate limits without a universal numeric quota (§4.4), attribution/public-page approval (§2.7).
UI needs artist/track links and prescribed button; local button hosting permission is unresolved.
Text-only email attribution acceptability, Azerbaijan/EEA conditions, redistribution and benchmark
research permission remain unresolved. No automatic email third-party requests, including hosted images.

Draft subject: Liner Notes — API use, benchmark publication, attribution, and page approval

Hello Last.fm team,

I am developing Liner Notes, an open-source discovery project, from Azerbaijan. It uses Last.fm
candidate/tag data with transparent deterministic re-ranking and does not own the similarity data.
Please clarify territorial consent/hosting/transfers under §§2.3/2.5; whether recorded responses
could ever be redistributed (current policy: local/uncommitted); written public-page approval and
local attribution-button hosting under §2.7; text-only attribution and ordinary artist/track links
in HTML/plaintext emails without automatic third-party requests; and whether publishing benchmark
methodology/aggregate results requires research permission without publishing responses.
We plan shared inventory, acquisition stopping at 80 MB, retention and degradation controls below
the stated 100 MB cap. Repository: **URL must be confirmed by the developer before sending**.
Thank you.
<!-- END SOURCE SNAPSHOT: docs/renewal/contracts.md -->

---

<a id="source-diagnostics"></a>

## Appendix A10 — Diagnostics operations, tooling limits and reproducibility

Original: [diagnostics/README.md](diagnostics/README.md). Full source text. Original source: 10,374 UTF-8 bytes, 127 lines.

Source SHA-256: `979bcd9cdb20130a2eb04d5cd7028d908ff5fba2b8fe1cfffdfdb456f2818059`.

<!-- BEGIN SOURCE SNAPSHOT: diagnostics/README.md -->

<a id="source-diagnostics--net-diagnostics-toolkit"></a>

### .NET diagnostics toolkit

These Bash scripts use the repository's .NET 10 SDK and open-source .NET diagnostics tools. Run them from any directory; they resolve paths relative to the repository. Artifacts go under `diagnostics/results/`, which is gitignored. Existing results are never removed.


<a id="source-diagnostics--repository-setup-and-health"></a>

#### Repository setup and health

The solution targets `net10.0`, uses xUnit with `Microsoft.NET.Test.Sdk`, central package management, lock files, and VSTest. Coverlet MSBuild is centrally pinned. `.NET 10` MTP support is explicitly disabled for these tests because Coverlet's MSBuild integration uses VSTest. The local tool manifest pins ReportGenerator; global tool installations are also supported. No BenchmarkDotNet project exists.

```bash
dotnet tool restore                 # optional if global ReportGenerator is available
diagnostics/health.sh
```

Health reports SDK and tool versions and Linux diagnostic-access notes. Attachment generally requires the same OS user as the target. Linux ptrace policy or container boundaries can restrict collection. SOS may require first-time setup for `dotnet-dump`.

Use the SDK pinned by `global.json`. On this development host it is installed at `/home/mahammadjafarli/.dotnet`; put that directory before `/usr/bin` in PATH if the system `dotnet` cannot resolve the pin. Do not change the SDK pin to run diagnostics.


<a id="source-diagnostics--coverage"></a>

#### Coverage

```bash
diagnostics/coverage.sh
diagnostics/coverage.sh --project 'tests/Domain.Tests/Domain.Tests.csproj'
diagnostics/coverage.sh --configuration Debug --timeout 300
```

The script runs each test project separately with Coverlet line and branch coverage. Default timeout is 300 seconds per project; set `--timeout` to a positive number of seconds. Output includes per-project Cobertura files, a combined Cobertura report at `diagnostics/results/coverage-<UTC>/report/Cobertura.xml`, HTML at `.../report/index.html`, and a short `Summary.txt`. `--no-build` requires that configuration to have already been built.


<a id="source-diagnostics--managed-thread-time-profiling"></a>

#### Managed thread-time profiling

```bash
diagnostics/profile.sh --pid 12345 --duration 30
diagnostics/profile.sh --pid 12345 --duration 90 --name checkout-before
diagnostics/profile.sh --launch --duration 30 -- dotnet run --project src/Presentation/Presentation.csproj
```

Attach requires an explicit PID; the script never discovers or attaches to a process automatically. Durations are whole seconds, 1–3600. Launch mode records from application startup, forwards child IO and preserves the child exit status while converting a valid trace. Each run retains the original `.nettrace` plus a `.speedscope.json` profile. Open the latter at [speedscope.app](https://www.speedscope.app). For a CLI hotspot report, run the printed `dotnet-trace report <trace> topN -n 20` command.

The wrapper uses `dotnet-sampled-thread-time` through `collect`. These managed thread samples include waits and are not exact CPU time. Filter the relevant threads/workload and compare ordinary timings separately. The Linux kernel `cpu-sampling` profile belongs to `collect-linux` and requires separate privileges; the wrapper never silently selects that capture mode. Neither profile establishes request latency or proves a regression by itself.


<a id="source-diagnostics--memory-diagnostics"></a>

#### Memory diagnostics

```bash
diagnostics/memory.sh monitor --pid 12345 --duration 60 --interval 2
diagnostics/memory.sh gcdump --pid 12345 --name checkout --timeout 60
diagnostics/memory.sh dump --pid 12345 --name checkout --yes
diagnostics/memory.sh analyze diagnostics/results/full-checkout.dmp --command 'dumpheap -stat'
```

`monitor` writes System.Runtime counters to a text artifact, including managed heap size, allocation rate, GC counts and process working set when exposed by the runtime. Managed heap size counts managed objects. Process memory/working set also includes native allocations, runtime/JIT, stacks and mapped pages. Allocation rate is allocation traffic, not retained growth. Suspected leaks need repeated measurements under a stable workload and evidence that objects remain rooted unexpectedly.

`gcdump` captures the managed heap and writes a type summary. It forces a full Gen 2 GC and can pause the process. A full dump may be very large, pause the target, and contain credentials, request data, or other secrets. Review authorization and storage before using `--yes`; never share or commit raw dumps or traces without sanitizing and approval. SOS commands include `dumpheap -stat`, `dumpheap -type TypeName`, `gcroot ADDRESS`, `clrstack`, and `dumpobj ADDRESS`.

PerfView is Windows-only. Fedora memory analysis here is primarily command-line based with counters, gcdump/SOS, dotnet-dump and Speedscope. dotnet-dump focuses on managed state; native-memory analysis may need platform-specific tools.


<a id="source-diagnostics--benchmarks"></a>

#### Benchmarks

```bash
diagnostics/benchmark.sh
diagnostics/benchmark.sh --project benchmarks/LinerNotes.Benchmarks/LinerNotes.Benchmarks.csproj --list
diagnostics/benchmark.sh --project benchmarks/LinerNotes.Benchmarks/LinerNotes.Benchmarks.csproj --filter '*Scorer*'
```

There is currently no BenchmarkDotNet project. With none found, the script exits 3 and offers this optional isolated setup; it does not modify the repository:

```bash
dotnet new console -n LinerNotes.Benchmarks -o benchmarks/LinerNotes.Benchmarks
dotnet add benchmarks/LinerNotes.Benchmarks package BenchmarkDotNet
# Add a ProjectReference to only the production project being measured.
# Add a [MemoryDiagnoser] benchmark class, then run diagnostics/benchmark.sh --project <csproj>.
```

Results and raw BenchmarkDotNet reports go under `diagnostics/results/benchmarks/`. Save a baseline run before the change. Keep machine, SDK/runtime, power state, workload, inputs, filter and settings comparable. Compare the same reported statistic; do not claim an improvement or regression from unmatched runs.

The dependency-free [SQL accounting](#source-accounting-control)
and [artifact inventory](#source-inventory-control) control harnesses
measure explicitly frozen Release assemblies. They use ordinary console entry
points, with their own documented commands and measurement boundaries; they are
not BenchmarkDotNet projects. Use the inventory harness for full-scan latency,
allocation, CPU and read-traffic comparisons before changing storage verification.


<a id="source-diagnostics--rider-on-fedora"></a>

#### Rider on Fedora

Use Rider's **External Tools** settings so the launch actions remain user-local and compatible with different Rider versions:

1. Open **File | Settings | Tools | External Tools** (or press **Ctrl+Alt+S**, then select **Tools | External Tools**) and click **Add**.
2. Set **Program** to the script path. Rider accepts project macros; use `$ProjectFileDir$/diagnostics/coverage.sh`. The scripts are executable Bash files.
3. Set **Working directory** to `$ProjectFileDir$`. Enable **Open console for tool output**.
4. Add these entries:

| Name | Program | Arguments |
|---|---|---|
| Diagnostics: coverage | `$ProjectFileDir$/diagnostics/coverage.sh` | *(empty)* |
| Diagnostics: CPU profile | `$ProjectFileDir$/diagnostics/profile.sh` | `--pid 12345 --duration 30` |
| Diagnostics: memory monitor | `$ProjectFileDir$/diagnostics/memory.sh` | `monitor --pid 12345 --duration 60` |
| Diagnostics: benchmarks | `$ProjectFileDir$/diagnostics/benchmark.sh` | `--filter '*'` |

Replace sample PID `12345` with a PID you selected and may inspect. Add separate CPU entries for commonly used durations or names. Launch them from **Tools | External Tools**. Optional shortcuts are under **Settings | Keymap | External Tools**. Rider displays output in its tool window and artifacts remain in the results folder. These tools do not appear inside JetBrains' licensed dotTrace, dotCover, or dotMemory interfaces.

Rider's External Tools configuration is local and is not committed with this repository.


<a id="source-diagnostics--agent-guidance"></a>

#### Agent guidance

For local worker/email tests run alongside restricted filesystem tools, explicitly
place temporary roots in a private ignored directory such as
`diagnostics/results/verification-tmp` (mode 0700) and set `TMPDIR` to its absolute
path before launching .NET. In this environment a concurrent sandboxed command
can expose an empty `/tmp/.git` marker, causing the application's required
Git-exclusion check to stop with `email_sink_tracked`. Retain that check and the
failed evidence; do not delete unrelated markers or weaken the guard. Compare
the namespace seen by the failing process before attributing a stopped result.

Codex reads `AGENTS.md`, Claude Code reads `CLAUDE.md`, and Antigravity CLI reads `GEMINI.md`. All point to this guide. Ordinary changes should use targeted tests, not expensive diagnostics by default. Run coverage for coverage questions, CPU profiling for CPU attribution, memory tools for allocation or retention investigations, and benchmarks for performance comparisons. Never kill production processes, attach without an explicit user-provided PID, collect sensitive dumps without authorization, or silently alter application behavior for profiling.

For before/after comparisons, use distinct names such as `feature-before` and `feature-after`, the same representative workload and environment, and report SDK/tool versions, duration and artifact paths. State observations separately from causal claims; one run does not prove an optimization.


<a id="source-diagnostics--exit-behavior-and-troubleshooting"></a>

#### Exit behavior and troubleshooting

Scripts return nonzero for invalid arguments, missing tools, failed tests or collection, and absent benchmark projects (exit 3). A successful invocation confirms tool execution and artifact generation, not production health. Linux collection may fail due to user identity, disabled runtime diagnostics, namespaces or ptrace policy. See Microsoft's [.NET diagnostics documentation](https://learn.microsoft.com/dotnet/core/diagnostics/) and the [Coverlet project](https://github.com/coverlet-coverage/coverlet).


<a id="source-diagnostics--focused-sql-comparisons"></a>

#### Focused SQL comparisons

The [managed-storage accounting harness](#source-accounting-control)
compares frozen before/after Release assemblies against disposable synthetic
PostgreSQL databases. It records cold/warm gate latency, allocation, backend CPU,
trigger execution, plan buffers and statement WAL. Its guide explains the
AFTER-trigger limitations of EXPLAIN counters and the explicit task-owned
container/connection requirements. No production process or provider is needed.
<!-- END SOURCE SNAPSHOT: diagnostics/README.md -->

---

<a id="source-accounting-control"></a>

## Appendix A11 — Exact SQL accounting controls

Original: [diagnostics/benchmarks/ManagedStorageAccounting/README.md](diagnostics/benchmarks/ManagedStorageAccounting/README.md). Full source text. Original source: 2,960 UTF-8 bytes, 48 lines.

Source SHA-256: `81e3835880f846737a2116394f71a398b4af58ab6324620defaf0e96c230b4b6`.

<!-- BEGIN SOURCE SNAPSHOT: diagnostics/benchmarks/ManagedStorageAccounting/README.md -->

<a id="source-accounting-control--managed-storage-accounting-comparison"></a>

### Managed-storage accounting comparison

Synthetic PostgreSQL-only comparison of `ReadStorageAsync` and a 0/1/1,000-row
evidence INSERT. It creates, migrates and finally drops its own random database.
It never reads application data. Use an explicitly task-owned local PostgreSQL
container and avoid concurrent workloads during measurements. Docker, the pinned
.NET SDK and the matching frozen Release assemblies must already be available.
No package dependency is added by this harness.

Set `LINER_PHASE1_POSTGRES` to that container's loopback admin connection,
`PERF_POSTGRES_CONTAINER` to its name and `PERF_CLOCK_TICKS` to `getconf CLK_TCK`.
The account must be allowed to create databases. Do not use a production cluster.
Keep output in a private directory under ignored `diagnostics/results/`.

Build each source revision separately, then preserve its Release binaries and
dependencies before modifying source. The harness references frozen DLLs rather
than project references, so it cannot silently rebuild the baseline:

```bash
dotnet build diagnostics/benchmarks/ManagedStorageAccounting/ManagedStorageAccounting.csproj \
  -c Release -m:1 -o /absolute/private/results/before-harness \
  -p:MeasuredAssemblies=/absolute/private/results/before-assemblies
dotnet /absolute/private/results/before-harness/ManagedStorageAccounting.dll \
  > /absolute/private/results/before.jsonl
```

Repeat with after assemblies, alternating at least three before/after pairs.
Archive source/DLL hashes, exact runtime/database versions and all samples.
Optional `PERF_GATE_PLAN` names a file containing the exact inventory SQL; the
harness records `EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON)` for attribution.
Optional `PERF_PLAN_MEMORY=1` reports the connection's `CachedPlan%` memory-context
total/used bytes after the warm loop. This requires access to
`pg_backend_memory_contexts` and measures cached-plan contexts, not backend RSS.

Output distinguishes one first-use gate call (includes JIT/initial preparation)
from 200 warmed gate calls. SQL statements have three warmups, then 25 samples
for zero/1,000 rows or 100 for one row. Every insert is rolled back. Wall time and
process-wide allocated bytes cover the awaited call, not database creation,
transaction setup, JSON analysis or rollback. Backend CPU totals use the
task-owned connection's `pg_backend_pid()` and Linux `/proc` counters inside the
named container; clock-tick resolution is recorded. There is no profiler attach.

EXPLAIN plan WAL/buffers exclude some AFTER-trigger work. `transactionWalBytes`
instead brackets the statement with cluster WAL insertion LSN readings, excluding
rollback. Background cluster writes can contaminate that difference; isolate the
cluster and report medians/ranges. CPU includes the measurement/control queries.
These are SQL mechanics and warm-connection controls, not end-to-end application,
peak-memory, physical-storage, production, or live-provider measurements.
<!-- END SOURCE SNAPSHOT: diagnostics/benchmarks/ManagedStorageAccounting/README.md -->

---

<a id="source-inventory-control"></a>

## Appendix A12 — Full versus metadata storage controls

Original: [diagnostics/benchmarks/ArtifactInventory/README.md](diagnostics/benchmarks/ArtifactInventory/README.md). Full source text. Original source: 4,529 UTF-8 bytes, 69 lines.

Source SHA-256: `2382b8196258d06dc5ee9d97310bd067152e804f10362cb3e2474e6e21231f31`.

<!-- BEGIN SOURCE SNAPSHOT: diagnostics/benchmarks/ArtifactInventory/README.md -->

<a id="source-inventory-control--artifact-inventory-controls"></a>

### Artifact inventory controls

Storage-only controls against explicitly frozen Release assemblies. No database,
provider, network or existing application files are used. The Linux-only harness
creates and deletes only its own random synthetic fixture beneath an explicitly
supplied private scratch parent. Allow at least 1 GiB of free disk space.

```bash
dotnet build diagnostics/benchmarks/ArtifactInventory/ArtifactInventory.csproj \
  -c Release -m:1 -o /absolute/private/results/storage-harness \
  -p:MeasuredAssemblies=/absolute/private/results/frozen-assemblies
dotnet /absolute/private/results/storage-harness/ArtifactInventory.dll \
  /absolute/private/results/scratch > /absolute/private/results/storage-before.jsonl
```

Use the pinned SDK and the project's documented single-process build settings.
The parent must already exist with owner-only permissions. The build fails without
frozen Infrastructure assemblies; it cannot silently rebuild the measured product.
No package dependency is added. Keep binaries, source hashes and raw output private
under ignored `diagnostics/results/`. The output records runtime, processor count,
GC mode and Infrastructure/harness SHA-256 hashes.

The shapes are empty, 100/512 MiB in one file, and 100 MiB in 1,000/10,000 files.
Each uses authenticated explicit enrollment, two warmups and five measured cycles
of acquire/reserve/pre-write validation/result verification/checkpoint publication.
These are five validation boundaries, with unchanged SQL state and no SQL commit;
they exclude transaction, queue, provider and delivery costs. Enrollment is reported
separately. Fixture bytes and names are deterministic; the synthetic trust key is
random and is deleted with the fixture.

Wall time and process-wide allocations bracket the awaited operation. CPU is
process CPU; `/proc/self/io` character reads distinguish repeated application reads
from physical reads in a warm cache. Reading those counters has a small fixed cost.
Peak working set is a cumulative process high-water mark, **not** incremental or
per-cycle memory. No heap dump or profiler attachment is performed.

For a comparison, freeze both product revisions, use the same harness/runtime and
fixture settings, alternate at least three before/after runs, and retain all samples.
Do not benchmark alongside builds/tests. Periodic audit pauses and fixed-duration
workloads need separate measurements when incremental mode exists; five warm cycles
cannot establish its audit deadline, aggregate CPU cost or long-running stability.

Set `PERF_ARTIFACT_MANIFESTS=1` to measure the opt-in authenticated manifest format
with full content scans retained. The harness records the mode and verifies actual
metadata bytes against the published accounting. It uses reflection only to set
this optional configuration, allowing the identical harness binary to run against
older frozen builds in default mode. An unsupported build rejects manifest mode;
the harness never silently substitutes legacy checks. Keep comparisons between
legacy and manifest modes explicitly labeled as protocol overhead, not an
incremental-verification improvement.

With manifests enabled, `PERF_ARTIFACT_VERIFICATION=full` compares five isolated
full artifact validations, and `PERF_ARTIFACT_VERIFICATION=metadata` compares five
metadata snapshot validations against the same enrolled inventory. The default
`boundaries` retains the original acquire/reserve/validate/verify/publish workload.
Do not compare an isolated scanner with that complete workload. Both isolated modes
use the same frozen build; they never publish a checkpoint in the timed interval.
They exclude root authentication, SQL receipt checks, leases, periodic audits and
recovery. Metadata mode requires a build with the internal snapshot implementation;
an unsupported build fails rather than substituting another mode.

Metadata mode checks paths and file stats against authenticated expected entries,
hashes each on-disk manifest part once per category, and reconstructs the existing
fingerprints using expected content hashes. It deliberately does not read payloads.
A same-length replacement with restored timestamps can pass this scanner; consumed
bytes and periodic full audits must supply content proof before production admission
can use it. A scanner comparison measures potential bounded-interval savings only.
It does not establish production mutation speed, integrity-window acceptance or
unattended delivery reliability. Incremental admission remains disabled.
<!-- END SOURCE SNAPSHOT: diagnostics/benchmarks/ArtifactInventory/README.md -->

---

<a id="source-consumption-control"></a>

## Appendix A13 — Consumed-read controls

Original: [diagnostics/benchmarks/ArtifactConsumption/README.md](diagnostics/benchmarks/ArtifactConsumption/README.md). Full source text. Original source: 2,411 UTF-8 bytes, 36 lines.

Source SHA-256: `5e8ffad67fbd8d353623695926782f0c101daccb273b25c3b45f0086a6fe51ad`.

<!-- BEGIN SOURCE SNAPSHOT: diagnostics/benchmarks/ArtifactConsumption/README.md -->

<a id="source-consumption-control--consumed-artifact-verification-costs"></a>

### Consumed artifact verification costs

This synthetic control measures G1d2 verification costs and subsequent strict-cache
changes. For the G1d2 cost control, compare a bounded byte read with the same read
followed by authenticated verification. For cache comparisons, set
`PERF_CONSUMED_VERIFY=1` in **both** frozen before/after builds. SQL state is
supplied in memory, so actual database latency and query work are additional.

Freeze release assemblies and compile this harness once against a build supporting
strict manifests. Use the same harness with the frozen before/after assemblies;
the added verifier is bound by reflection only when enabled. Use the project's
installed .NET SDK, `BuildInParallel=false` and `UseSharedCompilation=false`.

```bash
dotnet build diagnostics/benchmarks/ArtifactConsumption/ArtifactConsumption.csproj \
  -c Release -m:1 -p:MeasuredAssemblies=/absolute/frozen/assemblies \
  -o /absolute/private/harness
PERF_CONSUMED_VERIFY=0 dotnet /absolute/private/before/ArtifactConsumption.dll /absolute/private/scratch
PERF_CONSUMED_VERIFY=1 dotnet /absolute/private/after/ArtifactConsumption.dll /absolute/private/scratch
# For a subsequent cache comparison, enable verification in the before build too:
PERF_CONSUMED_VERIFY=1 dotnet /absolute/private/before/ArtifactConsumption.dll /absolute/private/scratch
# For a cache comparison with 25 simultaneous readers, use these settings for both builds:
PERF_CONSUMED_VERIFY=1 PERF_CONSUMED_CONCURRENCY=25 dotnet /absolute/private/after/ArtifactConsumption.dll /absolute/private/scratch
```

Use three alternating fresh-process pairs without concurrent builds, tests or
profilers. The harness creates and removes synthetic private roots, enrolls them,
warms 60 reads per shape, then reports 100 batches of one or 25 operations,
allocations, CPU, GC counts, process I/O counters and peak RSS. `samples` contains
batch latency; CPU/allocation/I/O comparisons can use the `operations` denominator.
The default concurrency is one; other values except 25 are rejected.
Fixtures cover 1/1,000/10,000 entries with 8 KiB
payloads and a single 1 MiB payload. Compare medians and process variability.
Batch tails are local warm-cache component observations, not production p95.
RSS is process cumulative and includes fixture enrollment. No provider or external
email is contacted; no running application is profiled. Keep raw output ignored.
<!-- END SOURCE SNAPSHOT: diagnostics/benchmarks/ArtifactConsumption/README.md -->

---

<a id="source-boundary-control"></a>

## Appendix A14 — Offline boundary and reader-contention probes

Original: [diagnostics/benchmarks/StorageBoundaryScan/README.md](diagnostics/benchmarks/StorageBoundaryScan/README.md). Full source text. Original source: 3,712 UTF-8 bytes, 58 lines.

Source SHA-256: `08c5078b31671394d2d69d0a01206bfc45e6eea527042fa1ff09a809196ef57d`.

<!-- BEGIN SOURCE SNAPSHOT: diagnostics/benchmarks/StorageBoundaryScan/README.md -->

<a id="source-boundary-control--storage-boundary-scan-probes"></a>

### Storage boundary scan probes

Offline characterization against explicitly frozen Release assemblies. No
PostgreSQL, HTTP, provider, email or existing application state is accessed.
Use the pinned SDK and documented single-process build settings. The Linux scratch
parent must already exist with mode 0700; allow 256 MiB free for the 10,000-file fixture
and the enforced physical headroom margin.
The harness creates a random child, touches only that child and removes it afterward.
Keep outputs, synthetic keys, fixtures and binaries under ignored diagnostics results.

```bash
dotnet build diagnostics/benchmarks/StorageBoundaryScan/StorageBoundaryScan.csproj \
  -c Release -m:1 -o /absolute/private/results/boundary-harness \
  -p:MeasuredAssemblies=/absolute/private/results/frozen-assemblies
dotnet /absolute/private/results/boundary-harness/StorageBoundaryScan.dll \
  /absolute/private/results/scratch boundaries
dotnet /absolute/private/results/boundary-harness/StorageBoundaryScan.dll \
  /absolute/private/results/scratch readers
dotnet /absolute/private/results/boundary-harness/StorageBoundaryScan.dll \
  /absolute/private/results/scratch readers scan-first
```

`boundaries` reports three observations:

- A synthetic stream changes only on the third catalog pass. The real importer and
  parser run against in-memory fake admission/store adapters. Count appended rows
  separately from publication; this does not demonstrate a PostgreSQL transaction
  or an attack against current callers' privately owned MemoryStreams.
- A deliberately overlapping provider pacing directory writes into a managed cache
  root. No upstream request is sent. Report the next strict admission result; the
  documented deployment layout uses separate directories.
- Explicit reconciliation can replace an expected content hash after strict
  admission rejects a same-length/restored-time edit. This is the intended operator
  primitive, not a recovery path. Assess HTTP authorization/reachability separately.

`readers` enrolls 10,000 files (one 8 KiB file and 9,999 one-byte files), and warms the
metadata scanner twice and consumed verifier 60 times. Each mode measures 100 reads
in four bursts of 25. One mode has no scan; the other deliberately starts each burst
while the real metadata scanner holds the cache gate. A diagnostic semaphore waiter
measures the gate delay and releases immediately. Reflection accesses only the
internal scanner and gate of this newly created fixture, never an existing process.
An unsuccessful overlap observation fails the trial; preserve it rather than
silently substituting a workload. `scan-first` reverses mode order.

Use at least three fresh processes, alternating order, without simultaneous
builds/tests/profiling. Output includes all request samples, four gate waits/scan
durations, process CPU, allocation, application/physical reads, cumulative peak RSS,
runtime, GC mode and assembly/harness hashes. Calculate nearest-rank p95 separately
for each 100-request process/mode. Bursts and deliberate overlap are correlated
samples: their tails characterize that workload, not ordinary production frequency.

The SQL-state callback is an in-memory stub, with exactly one callback per request.
Real database latency/contention and provider/network costs are excluded. Combined
CPU/allocation/read counters include the four scans and overlap observer; do not
call the difference a per-reader allocation regression. Peak RSS includes fixture
creation, warmups and earlier modes. The scanner remains disconnected from production
admission. These probes provide evidence for activation decisions, not production
performance, integrity-window approval or deployment acceptance.
<!-- END SOURCE SNAPSHOT: diagnostics/benchmarks/StorageBoundaryScan/README.md -->

---

<a id="source-progress"></a>

## Appendix A15 — Full current project progress and historical context

Original: [PROGRESS.md](PROGRESS.md). Full source text. Original source: 23,808 UTF-8 bytes, 281 lines.

Source SHA-256: `4d497fdd70f041eb9490d1ed5e51a7b8831cea64c82d2a646d9eb2b0773956ac`.

<!-- BEGIN SOURCE SNAPSHOT: PROGRESS.md -->

<a id="source-progress--liner-notes-renewal-progress"></a>

### Liner Notes renewal progress


<a id="source-progress--current-status"></a>

#### Current status

The root [Performance_Issues.md](Performance_Issues.md) consolidates the current
issue register, completed work, remaining decisions and full performance reports.
Its dated source snapshots preserve historical evidence; the current-status
register distinguishes those snapshots from outstanding remediation.


<a id="source-progress--comprehensive-performance-remediation-plan--2026-10-25"></a>

##### Comprehensive performance remediation plan — 2026-10-25

The developer approved the [remediation design and implementation plan](#source-plan), including the joint P01/P19/P20 design and D1–D4, on 2026-10-25. Sequential implementation is authorized; external deployment/provider acceptance remains separate.

G1d4's internal metadata snapshot foundation is locally verified on 2026-10-27:
**848/848 checks**, both offline Node checks and the five-project TRX gate pass.
Production admission still uses full content scans. Three matched scanner pairs
show much lower payload-read traffic and CPU on populated synthetic fixtures;
10,000-file latency improves 63%, missing the proposed 80% target, and empty scans
regress. This is an isolated scanner comparison, not production admission acceptance.
A 1/10/100 offline probe advances all 111 recipients, with empty repeat ticks and
exact independent accounting. See the detailed evidence for allocations, CPU,
memory, strict-read controls and limitations. Next: process-local startup/full-audit
trust, the 30/60-second refresh/deadline service, concurrent-reader contention and
remaining writer/reader coverage before measured opt-in incremental admission.
G2–G7 and the original untraced post-commit stop remain outstanding.

The targeted storage reader/writer scan is complete (2026-10-27); see the
[boundary report](#source-scan). It identifies a
Development-only HTTP trust-reset policy gap, an overlapping pacing-directory
configuration gap, and measured reader delays while the inactive metadata scanner
holds the cache gate. A mutable-stream catalog limitation is reproduced, while
current host callers retain private buffers and publication remains blocked on
hash failure. Eleven fresh-process post-commit recovery checks pass; the original
intermittent failure remains unattributed. Production sources and behavior are
unchanged. Resolve the signed-maintenance contract in the report before remediating
that endpoint; incremental activation and G2–G7 are still open.

G0/T01 tooling repair is locally verified: five offline regressions, shell syntax,
health and actual task-launched NetTrace/Speedscope captures passed, including child
exit-status propagation. No application performance gain is claimed from tool repair.
See [implementation evidence](#source-evidence).
G1a's opt-in authenticated checkpoint/known-write recovery foundation is implemented
and locally verified: 716 full-suite tests passed with zero failures/skips, both
Node checks and the TRX gate passed. Its additive bounded operational receipt is
reviewed in the privacy inventory. Full artifact checks remain; general signed-mode
activation waits for complete writer integration. P01/P19/P20 remain open as the
joint group. G1b's statement-trigger accounting is now implemented and locally
verified: 725 tests pass, with all 143 DataAccess tests also passing PostgreSQL
16.15. Matched PostgreSQL 18.6 bulk SQL controls show 68% lower client latency,
69% less backend CPU and 14% less statement WAL; the strict prepared inventory
query is 23% faster warm. Cold preparation and small-statement costs remain
explicit in the evidence. Those G1a/G1b activation gates were historical prerequisites; G1c writer integration
is now verified below. G1d incremental verification remains pending.

G1c writer integration is implemented; final local acceptance passes **769/769
checks** (zero failures/skips), including coordinated historical conversion,
original-ciphertext recovery, ownership conflict handling, quiet-period unsubscribe,
continuous key-state proofs and signed writer fences. The combined discovery/local
capture roots now require an explicit independent integrity key. Full content scans
remain enabled; G1d incremental verification is next. All 163 DataAccess checks also
pass on PostgreSQL 16.15. A fresh probe exposed an intermittent first-tick stop; a
deterministic reservation-growth regression and bounded correction are verified.
Three final probes now advance all 333 recipients, preserve exact accounting and
leave repeated ticks empty. Median 1/10/100 process times are 4.53/7.03/32.15 seconds;
full-scan costs remain. This is local correctness evidence, not production acceptance.
The approved [privacy exception](#source-privacy)
preserves SQL reclamation while uncertain artifacts keep delivery blocked. No live
provider, external email, deployment or VPS acceptance is implied.
See [integration notes](#source-capture) and
[measurement evidence](#source-evidence).

G1c is committed as `383083b`. A first G1d allocation step is now locally verified:
bounded buffer/hash reuse preserves full checks and existing fingerprints. Three
alternating frozen-binary pairs show 10,000-file allocation **535.26 → 94.03 MB**
and median storage-phase latency **1,205.24 → 888.08 ms**. Empty storage has a small
fixed cost; 100 MiB single-file latency is unchanged. Full payload read traffic is
unchanged. The latest scan passes **770/770 tests**, both offline Node checks and
the five-project TRX gate, with no new C# warnings. No migration or activation is
required for this step. Immutable manifests, verified consumption and periodic
incremental-audit admission remain outstanding G1d work; G2–G7 are still pending.

G1d1's strict manifest persistence stage is locally verified: **814/814 checks**
plus all **171 DataAccess checks on PostgreSQL 16.15**, both offline Node checks
and the TRX gate pass. Signed roots now bind the exact independent SQL receipt;
a reproduced filesystem-only rollback of a control-only removal is rejected.
The optional manifest directory supports opaque immutable parts, charged overlap,
pinned resumable enrollment and recoverable retirement. Signed read-only preflight
now handles known physical growth without permitting uncoordinated reservations.
A final manifest-enabled 1/10/100 probe advances all 111 recipients, with empty
repeat ticks, exact SQL accounting and six live parts totaling 8,976 bytes. Full
hashing is retained and the strict format adds measurable overhead. Verified
consumption, bounded cache/deadline/auditor integration and incremental activation
remain the next G1d stages. See [manifest integration](#source-manifest)
and the evidence document for migration constraints and failed controls. An intermittent
stop in the injected post-commit recovery case remains untraced: the final PostgreSQL
16 retry and three isolated repeats pass, but the failed trial remains a G7
investigation item. No production or long-running reliability claim is made.

G1d2 verified consumption is locally implemented (2026-10-27): **823/823 checks**,
the five-project TRX gate and both offline Node checks pass; all **171 DataAccess
checks on PostgreSQL 16.15** also pass. Published email and recording buffers are
verified before parsing/decryption against the signed manifest and current SQL
receipt, with concurrent publication producing a bounded stop. Pending recovery
keeps its existing signed file-plan proof. The added read cost is measured and
documented, without claiming an optimization: uncached manifest reads allocate
about 207 KB extra for one entry and about 1 MB extra for 10,000 entries, plus one
SQL-state read whose real latency is excluded from the component control. The
final 1/10/100 offline probe advances all 111 recipients, leaves empty repeat ticks
and independently exact accounting/manifest hashes. A PostgreSQL 16 trial stopped
with `email_sink_tracked`; sandbox commands expose a transient empty `/tmp/.git`
marker. Tests now use private ignored temporary roots and the failed trial is
preserved. This does not attribute the older post-commit stop. Full scans remain
enabled; bounded cache/deadline/auditor integration, remaining reader/writer coverage,
incremental activation and G2–G7 are still outstanding. See the updated
[integration notes](#source-manifest) and
[evidence](#source-evidence).

G1d3's bounded strict read cache is locally implemented (2026-10-27): **834/834
checks**, both offline Node checks and the five-project TRX gate pass; all **171
DataAccess checks also pass on PostgreSQL 16.15**. It retains only large live
manifest parts from one authenticated generation, with a 48,000,096-byte retained
limit; every consumed buffer and on-disk part is still hashed. Three matched
process pairs show approximately **78% lower allocation** for 10,000-entry reads
and zero measured generation-2 collections for those cached-part controls. The
25-reader control improves CPU/batch latency; sequential latency and CPU results
are mixed and sometimes regress. Metadata traffic and SQL requests are unchanged.
The first cache-every-part trial is retained; the final policy leaves parts at or
below 64 KiB on the direct loader. A final 1/10/100 offline delivery probe advances
all 111 recipients and preserves exact independent accounting and manifest hashes.
This completes the strict consumed-read cache stage, not mutation-admission caching
or P01 incremental activation. Periodic full-audit deadline/service integration,
remaining reader/writer coverage and G2–G7 remain outstanding. The earlier injected
post-commit stopped result remains an open G7 investigation.

Phases 1–4 and 6–9 have locally verified implementations. Phase 5 real-data recommendation evaluation is not authorized. Local implementation evidence does not establish recommendation quality, external provider approval, production readiness, or developer confirmation.

Phase 9 includes pinned build inputs, non-root web and Worker images, local Compose topology, and PostgreSQL-backed CI. Recorded local verification passed 478 .NET checks, 32 offline Python tests, four helper test methods, and all eight container lifecycle groups. GitHub Actions execution and remote scanning have not been verified. See [Phase 9 evidence](docs/renewal/phase-9-evidence.md) and the [local runbook](docs/renewal/phase-9-local-run.md).


<a id="source-progress--guest-discovery"></a>

#### Guest discovery


<a id="source-progress--efficiency-audit-continuation--2026-10-25"></a>

##### Efficiency audit continuation — 2026-10-25

The developer approved architectural directions for P01, P06/P13, P09/P12,
P05/P15 and P16, **without implementation authorization**. Complete the remaining
CPU, memory, SQL, contention, network and deployment investigations and review
the prioritized plan before implementation. See `Performance_Network_Audit.md`.

P01 remains an outstanding architectural optimization: retain SHA-256 and the
existing five-pass implementation while designing immutable artifacts, independently
integrity-protected versioned manifests and incremental verification. Define the
external-change detection window and privileged-administrator threat-model limits;
preserve fail-closed integrity, cross-process coordination, atomicity, accounting
and recovery. A replacement needs design approval, benchmarks and verification.


<a id="source-progress--efficiency-audit-evidence--2026-10-25"></a>

##### Efficiency audit evidence — 2026-10-25

The remaining available investigations are recorded in the root audit: usable
CPU/allocation traces, three managed heap graphs, SQL plans and trigger costs,
actual login/write contention, 25-owner reads, two successful ten-job bursts,
HTTP/cache/pacing controls, two paced public metadata samples, browser revalidation
and mobile throttling, storage growth, and local resource-limit comparisons.
P18 compression, P19 ledger-trigger cost and P20 scheduled-capture inventory
publication/recovery are recorded alongside all original partial/open findings.
The actual scheduled CLI stops safely for 1/10/100 new recipients; successful
unattended throughput is unverified and must not be inferred from stop timings.

No VPS exists yet. The practical initial estimate is 2 vCPU / 2 GiB / 25 GiB SSD;
4 GiB provides a more defensible budget for concurrent large exports before the
approved staged-streaming design. Actual VPS and representative live-discovery
acceptance remain separate. No optimization implementation, schema/dependency
change, purchase, deployment or push was performed. Review the prioritized plan
and design contracts before explicitly authorizing implementation.


<a id="source-progress--performance-remediation--2026-10-24"></a>

##### Performance remediation — 2026-10-24

Bounded fixes from the root [performance audit](#source-audit) now
honor polling retry delays, reuse metadata after pacing, remove one body copy,
prepare invariant scoring inputs once, index candidate aliases, batch catalog
reads and combine account eligibility checks. All 702 Release tests passed with
zero failures/skips against disposable PostgreSQL; both offline Node checks passed.
Isolated allocation comparisons preserve serialized output hashes. Larger storage,
export, authentication coordination and evidence-policy decisions remain deferred
in the report. No migrations, dependency changes, live-provider measurements or
deployment were performed; developer confirmation remains pending.


<a id="source-progress--actual-local-live-website--2026-10-23"></a>

##### Actual local live website — 2026-10-23

The normal `Liner Notes` IDE profile now selects live ListenBrainz/MusicBrainz
configuration. `bash scripts/run_live.sh` starts the real site at
`http://localhost:5080/`, using the persistent `liner_notes_live` database and
private `.linernotes/live/` state. It selects the installed pinned SDK and never
substitutes fixture generation when live configuration fails.

Playwright MCP verified actual artist, album and recording lookup, real
recommendation generation, stored live evidence, feedback persistence and real
recipient-free Recent recommendations. Live metadata acquisition now imports
eligible public core recording metadata into the catalog before scoring. The
provider client honors upstream `s-maxage` for shared freshness while retaining
no-store/no-cache/private restrictions.

The final regression run passed 695 .NET checks with no failures or skips.
These targeted live journeys establish local integration, not recommendation
quality or public deployment readiness. The developer explicitly selected running
on this machine with real provider data. See [operation](docs/renewal/local-live-run.md)
and [live verification](docs/renewal/2026-10-23-local-live-verification.md).


<a id="source-progress--earlier-synthetic-implementation-evidence"></a>

##### Earlier synthetic implementation evidence

The guest-first discovery design and sequential implementation were approved. The first onsite flow now supports artist, album, and style-tag selection; immediate synthetic recommendations with persisted explanations; explicit catalog lookup; feedback; export; registration with guest data preservation; explicit import into an existing account; and guest data deletion.

The implementation passed 648 .NET checks with zero failures/skips and 12 synthetic browser checks. Seven catalog integration checks also passed after strengthening private-evidence retention assertions. The browser covered search, artist/album selection, explicit lookup, generation, return visits, feedback, export, registration/import, cooldown, sparse results and deletion.

Provider selection uses registered polymorphic bundles. Application workflows do not depend on provider implementations or EF Core types. Persistence contracts live in Application; database-specific SQL and mappings remain in DataAccess. A database change still needs schema/data migration, but should not rewrite product workflows.

Bounded public cleanup now preserves the active catalog, fresh lookups, staged imports, canonical identities and private favorite snapshots. Weekly saves, scheduler activity and unsubscribe updates use discovery admission when its runtime is enabled; PostgreSQL verification confirmed a subsequent guest write still succeeds.

The constrained Release runtime passed the 50-profile load scenario with five generating visitors and 5/20/50 concurrent search clients. The final warm search p95 values were 28/123/323 ms; five synthetic batches completed in 2.11–2.27 seconds. A separate run recovered five unfinished requests across a forced website restart. Earlier burst runs were slower; the provisional 100 ms burst target is not met. This is local synthetic evidence, not a production capacity guarantee. See [resource measurements and limits](docs/renewal/guest-discovery-capacity.md).

Remaining confirmation and deployment gates:

- Developer review of the concrete onsite flow remains pending.
- A real catalog acquisition manifest is not approved or imported. Coverage, recommendation quality and live-provider latency remain unverified.
- Optional local email capture is excluded from concurrent guest-runtime operation in both hosts. It needs separate file/key write coordination before that combination is enabled. No external email transport is configured.
- Production hosting, full-disk operating limits, backups, real-catalog load and broader saturation behavior need deployment-specific verification. There is no configured total-user ceiling.

See the [evidence](docs/renewal/guest-discovery-evidence.md), [storage notes](#source-storage), [design](docs/superpowers/specs/2026-10-16-guest-discovery-design.md), [execution plan](docs/superpowers/plans/2026-10-16-guest-discovery.md), and [synthetic runbook](docs/renewal/guest-discovery-local-run.md).


<a id="source-progress--optional-taste-onboarding"></a>

#### Optional taste onboarding

Implementation of the approved 2026-10-19 design passed local verification: 669
.NET tests with zero failures/skips, 21 browser checks and the EF model/migration
consistency check. The 271-tag synthetic generation/export journey passed within
the existing snapshot budget.
Guests and accounts share resumable optional questions, searchable tags without a
selection count cap, broad taste, artist/album/recording favorites and explicit
review. Skipping taste opens a separate recipient-free Recent recommendations
feed. Saved selections are separate from bounded generation windows and replay
snapshots. Export, attachment, explicit import, ownership and deletion include the
new state. Existing name seeds, custom tags and queued request retries remain compatible.

See [implementation evidence](docs/renewal/optional-taste-onboarding-evidence.md)
and the [execution plan](docs/superpowers/plans/2026-10-19-optional-taste-onboarding.md).
Developer confirmation, live recording/vocal coverage and recommendation quality
remain open. Last.fm stays available; unsupported direct recording capability is
reported as a coverage gap. Deployment and real catalog acquisition remain separate gates.


<a id="source-progress--provider-status"></a>

#### Provider status

ListenBrainz is the configured default; Last.fm remains available behind the provider abstraction. Live use is explicitly gated. Per-user provider choice and listening-history imports are deferred. Developer confirmation, deployment-specific usage and license review, and recommendation-quality evaluation remain open. See the [provider evidence](docs/renewal/listenbrainz-provider-evidence.md) and [operations guide](docs/renewal/listenbrainz-provider-runbook.md).


<a id="source-progress--remaining-renewal-work"></a>

#### Remaining renewal work

1. **Developer confirmation:** review locally verified phase evidence and confirm the implementation.
2. **Phase 4 live pilot:** approve a concrete run manifest, reconcile shared-storage inventory, freeze inputs, and complete data-shape checks and blind ratings. No live pilot has been run; fixtures establish mechanics only. See [Phase 4 evidence and run procedure](docs/renewal/phase-4-evidence.md).
3. **Phase 5 decision:** real-data evaluation needs separate authorization, an approved development and held-out manifest, frozen scoring configuration, and documented comparison steps. Recommendation quality remains unverified. See [renewal contracts](#source-contracts).
4. **Provider release gates:** obtain written Last.fm public-page approval and verify applicable territorial, consent, recording, attribution, and hosting requirements. The request remains a draft; confirm the repository URL before sending it. See [Phase 3 references](docs/renewal/phase-3-evidence.md#authoritative-documentation) and the [release checklist](docs/renewal/phase-9-release-checklist.md).
5. **Production operations:** choose hosting and delivery arrangements, validate storage limits against the chosen host, and complete the [Phase 9 release checklist](docs/renewal/phase-9-release-checklist.md). Production deployment and external email delivery have not been performed.
6. **Phase 7 deployment checks:** inspect or convert any existing captured-email files and confirm persistent key-store access and backup protection before relying on encrypted capture. See [Phase 7 evidence](docs/renewal/phase-7-evidence.md).
7. **External CI:** GitHub Actions and Qodana have run and reported failures. The PR merge's malformed dependency lock and older Qodana workflow have local fixes; a successful GitHub rerun and GitGuardian incident classification remain pending. See [PR review evidence](docs/renewal/2026-10-19-pr-review-fixes.md).


<a id="source-progress--phase-implementation-status"></a>

#### Phase implementation status

| Phase | Current status | Evidence |
|---|---|---|
| 1. Baseline and security | Implemented and locally verified; developer confirmation pending | [Phase 1](docs/renewal/phase-1-evidence.md) |
| 2. Harness mechanics | Implemented and locally verified; mechanics only | [Phase 2](docs/renewal/phase-2-evidence.md) |
| 3. Honest ingestion | Implemented and locally verified; provider gates remain | [Phase 3](docs/renewal/phase-3-evidence.md) |
| 4. Pilot tooling | Implemented and locally verified; live pilot remains open | [Phase 4](docs/renewal/phase-4-evidence.md) |
| 5. Real-data evaluation | Not authorized; recommendation quality unverified | [Renewal contracts](#source-contracts) |
| 6. Recommendation generation and feedback | Implemented and locally verified; quality and capacity unverified | [Phase 6](docs/renewal/phase-6-evidence.md) |
| 7. Worker and local email capture | Implemented and locally verified; deployment-specific checks remain open | [Phase 7](docs/renewal/phase-7-evidence.md) |
| 8. Privacy and scheduling | Implemented and locally verified; developer confirmation and external delivery remain open | [Phase 8a](docs/renewal/phase-8a-evidence.md), [Phase 8b](docs/renewal/phase-8b-evidence.md) |
| 9. Reproducibility and release preparation | Implemented and locally verified; external CI and release gates remain open | [Phase 9](docs/renewal/phase-9-evidence.md) |


<a id="source-progress--product-and-evidence-constraints"></a>

#### Product and evidence constraints

- V1 remains popularity-neutral. Do not claim a new recommendation algorithm or ownership of upstream similarity data. Synthetic fixtures and pilot mechanics do not establish recommendation quality.
- Real recordings and evaluation reports require separate approval, stay local, and must remain gitignored and untracked. Do not tune against held-out data.
- Last.fm and ListenBrainz remain provider-boundary concerns. Live acquisition needs explicit configuration and usage review. Listening-history imports remain deferred. Streaming search links are not Spotify API integration.
- The temporary 1,000,000,000-byte managed-storage stop is not a VPS-capacity decision. Hosting, provisioning, public deployment, and external email delivery are not authorized by this progress record.
- Product decisions and status live here; verified preferences and gotchas live in [MEMORY](#source-handoff). User-visible changes belong in [CHANGELOG](#source-changelog).
<!-- END SOURCE SNAPSHOT: PROGRESS.md -->

---

<a id="source-changelog"></a>

## Appendix A16 — Full recorded change chronology

Original: [CHANGELOG.md](CHANGELOG.md). Full source text. Original source: 35,736 UTF-8 bytes, 455 lines.

Source SHA-256: `b84d124e29d8fe13bd806c2800ad56fffa72a17d62c83bf3a8e0f1950f634f87`.

<!-- BEGIN SOURCE SNAPSHOT: CHANGELOG.md -->

<a id="source-changelog--changelog"></a>

### Changelog


<a id="source-changelog--unreleased--renewal"></a>

#### Unreleased — renewal

- Consolidate performance findings, measurements, remediation status, decisions
  and complete report snapshots in the root `Performance_Issues.md`.

- Document the remaining storage reader/writer boundaries and add offline probes
  for reconciliation, pacing overlap, mutable catalog input and reader contention.
  Improve recovery-test failure messages. Production behavior is unchanged.

- Add tested internal metadata snapshot checks and reproducible full/metadata
  scanner controls. Production admission retains full hashing until startup trust,
  periodic audit deadlines and remaining coverage are verified. Populated controls
  reduce reads; empty scans regress and the 10,000-file latency target remains unmet.

- Bound cached manifest metadata to one authenticated generation and reuse large
  validated parts when reading archived copies or recordings. Strict on-disk and
  consumed-buffer hashing remains enabled; small parts use the direct loader.
  Large-part allocation falls in local controls, while latency/CPU remain mixed.

- Verify consumed email-copy and local-recording bytes against authenticated
  manifests and the current SQL receipt before parsing or decrypting them.
  Pending recovery retains its signed file-plan proof. Full scans remain enabled;
  uncached read costs and remaining incremental activation gates are documented.

- Bind authenticated checkpoints to the exact SQL receipt so a filesystem-only
  rollback cannot undo a control-only operation. Ambiguous pre-anchor maintenance
  checkpoints require explicit verified reconciliation before further writes.
- Add opt-in immutable artifact manifests with separately charged metadata,
  recoverable enrollment and retirement. Full content checks remain enabled;
  incremental verification is not activated by this stage.

- Reuse bounded hashing buffers during full artifact verification, retaining the
  existing fingerprints, content checks and storage limits. Incremental audits
  remain a separate stage.

- Keep the local email key-state proof continuous throughout preparation; a key
  change triggers a bounded retry before an immutable capture can be published.

- Coordinate legacy generation, local capture, schedules,
  key rotation, HTTP writes and account-copy removal. Signed rollback/restart and
  uninterrupted offline delivery probes pass. Signed legacy conversion preserves
  historical receipt contents and recovers the original ciphertext. Combined
  discovery/local capture now requires an explicit independent integrity key.
  The approved privacy exception permits verified owned-data SQL reclamation while
  uncertain archive content keeps generation/publication blocked. Local verification
  passes; incremental verification remains a separate pending stage.
- Batch managed-storage accounting per SQL statement while preserving exact byte
  totals and revisions. Strengthen trigger/function tamper detection and reuse the
  fixed inventory query's prepared plan. Migration requires stopped writers and
  explicit reconciliation; cold and small-statement costs are documented.

- Add the authenticated checkpoint and exact committed-write recovery foundation
  for coordinated storage. Runtime activation awaits complete writer integration.

- Repair diagnostics profiling for the installed CLI, preserve captured child exit
  status, and fix the global ReportGenerator fallback.
- Honor server retry delays while polling discovery jobs. Reuse public metadata
  populated during provider pacing and avoid an extra response-body array copy.
- Reuse invariant taste/configuration work within scoring batches and index
  candidate aliases during merging, preserving scores, ranking and explanations.
- Batch catalog selection and lookup-result reads, and check both account
  eligibility conditions in one query while preserving immediate revocation.

- Start the normal local website with live ListenBrainz/MusicBrainz providers,
  a persistent dedicated database and private storage configuration. Add
  `bash scripts/run_live.sh`, including selection of the installed pinned SDK.
- Display the actual discovery evidence mode in the frontend.
- Publish eligible core metadata acquired during live preparation into the public
  catalog so real picks can appear in recipient-free Recent recommendations.

- Fix the legacy taste form sending incorrect tag and weight fields to the API.
  Show backend validation messages when saving taste, registering or signing in.
- Keep account controls, including sign out, accessible in the mobile navigation.
- Revalidate frontend assets so browsers load current API integration code after
  an update instead of continuing to use cached, unversioned scripts.
- Preserve canonical recording identity when storing onsite picks; keep recordings
  with useful recording tags even when artist tags are missing.
- Reject weekly saves after optional taste answers change, and allow catalog
  favorites with display names up to the existing 256-character catalog limit.
- Repair PR merge dependency locks and consolidate Qodana analysis while retaining
  the existing Cloud project token configuration.
- Add Qodana .NET configuration and GitHub PR analysis with pinned
  tooling, locked dependency restore, annotations and downloadable reports.
- Add optional, resumable guest/account taste setup: three skippable questions,
  searchable genre/mood/sound tags without a selection cap, broad taste,
  artist/album/recording favorites and explicit review before generation.
- Preserve recording versions, credits and release hints in private favorites;
  disclose reduced album-to-artist similarity influence in stored explanations.
- Add Recent recommendations for everyone, including skipped-input visitors,
  using a separate recipient-free public catalog projection and truthful demo/empty states.
- Keep large saved taste profiles separate from rotating acquisition windows and
  bounded replayable score snapshots. Include answers in export and account transfer.
  Existing name seeds, custom tags and queued request retries remain compatible.
- Add a dependency-free synthetic load driver and document measured resource use,
  search latency, queued recovery and disk budgeting on a constrained runtime.
- Coordinate weekly digest saves, schedule activity and unsubscribe updates with
  onsite discovery storage. Reject unsupported concurrent local email capture
  in the development Worker as well as the website.
- Reclaim expired public recommendation evidence and catalog search records in
  bounded maintenance passes. Preserve the active catalog, fresh lookups,
  resumable imports, canonical identities and private taste/history evidence.
- Add the onsite guest website: artist/album autocomplete, styles, explicit
  catalog lookup, durable progress, saved explanations, feedback, history,
  cooldown, export/clear, preserved registration and explicit account import.
  Synthetic catalog entries/results are labelled; returning does not regenerate.
- Pace explicit catalog lookup independently from recommendation generation,
  retaining the 30-second generation default. Add a synthetic-only local setup
  and browser verification script.
- Fix discovery after positive feedback: derived artist and tag inputs can share
  recommendation provenance without colliding or sharing their distinct weights.
- Activate website discovery consumers only through DiscoveryRuntime:RunJobs,
  with periodic known-inventory refresh and bounded guest expiry cleanup.
  Concurrent local email capture remains blocked pending writer integration.
- Renew guest activity hourly on visits; storage stops keep saved data readable
  and cookies retain the actual server expiry. Separate configurable guest-start
  pacing from password authentication limits.
- Allow guest/account data reclamation at the managed storage cutoff while
  rejecting transactions that grow tracked data. Keep disk, revision and artifact
  guards. Add known-inventory refresh without accepting an unknown baseline.
- Coordinate authentication, legacy taste and feedback writes when discovery
  jobs are enabled. Taste/feedback revisions cancel stale generation atomically;
  independent catalog lookup stays queued. MVC action exceptions roll back writes.
- Resolve recommendation bundles through registered adapter keys; provider-specific
  live-use restrictions stay in adapter registration. Existing scoring policy
  identifiers remain unchanged by default.
- Keep EF Core context and transaction types inside DataAccess; Application now
  depends on repository and unit-of-work contracts without an EF Core package.
- Release weekly generation storage leases during provider acquisition, then
  reacquire and recheck before persistence. Enabled discovery reuses shared
  artist/album preparation while keeping ISO-week score identity and recorded
  replay compatibility.
- Add private discovery overview/status/results and feedback endpoints. Responses
  include saved explanations and bounded history; feedback retries preserve
  scores and accepted changes cancel stale generation. Cooldown/conflict errors
  use safe responses and retry guidance.
- Connect explicit artist/album lookup to durable jobs, shared acquisition and
  expiring local publication. Lookup and generation alternate on the acquisition
  lane. Search typing remains local; the website runner is not yet registered.
- Keep the conservative reconciled storage charge when PostgreSQL physical
  size decreases without a tracked revision change. Growth and revision changes
  retain their guards, avoiding false capture failures after a commit.
- Add idempotent discovery submission, transactional cooldown checks and taste
  changes that cancel stale work. Separate prepared and acquisition lanes keep
  prepared profiles responsive while source work waits. The runner remains
  unregistered until endpoint and storage/legacy integration is complete.
- Add replayable discovery batch preparation and atomic score/history/job
  completion, with bounded feedback reads and stale evidence/revision checks.
  The onsite runner and website integration remain pending.
- Reuse durable public artist/album seed and candidate evidence after memory
  restart. Prepared-only work cannot fall through to upstream acquisition.
- Add leased, quota-bounded public evidence storage and compact per-profile
  recommendation identity history. Export 2.5 includes history and batch coverage.
  The background consumer and onsite generation flow remain pending.

- Add durable discovery job leases, idempotent queue admission and coordinated
  storage reservations. Database accounting avoids scanning private history for
  every new write. The onsite runner and website flow remain pending.

- Add canonical artist/album selections and style validation, scoped album taste
  evidence with configurable credited-artist fallback, and reusable public candidate
  pools. Provider and prepared caches now have a combined 64 MiB admission budget.
  Export 2.4 includes selection provenance. The onsite product flow remains disabled
  until the durable runner and storage coordination stages are complete.

- Add indexed local artist/album search with aliases, Unicode and typo handling,
  all album credits, source attribution and versioned catalog publication.
  Bounded imports resume verified chunks while keeping the last good catalog.
  Explicit lookup has private status routes and source-policy checks; submission
  remains disabled until its durable background consumer is installed.

- Add guest credentials, seven-day inactive expiry, private export/clear and
  transactional account attachment/import. Cookie mutations require same-origin
  checks and antiforgery tokens. Guest routes remain disabled pending storage
  and background-processing prerequisites. Weekly scheduling now requires
  explicit email opt-in and a verified address; onsite use stays independent.

- Move taste and recommendation ownership to listener profiles while preserving
  historical IDs, feedback and score evidence. Add non-weekly recommendation
  snapshots and include on-demand history in account exports. Registered and
  weekly APIs remain available; guest access is not enabled by this change alone.

- Add listener profile, discovery batch and pending-job foundations with an
  additive legacy-data backfill. Export 2.5 includes the new owned metadata and
  batch links while excluding operational lease credentials. Guest access and
  on-demand generation are not enabled by this foundation alone.

- Add a bounded synthetic catalog resource probe and source-policy record for
  the approved guest discovery work. Local metadata search feasibility is measured
  separately from recommendation readiness; the guest product flow is not yet implemented.

- Make ListenBrainz the default provider while retaining Last.fm as an operator
  option. Discovery, seed tags and candidate tags resolve together. Add bounded
  public-catalog clients, approved recorded replay and explicitly gated live
  generation; preserve historical evidence and derive email credits from it.
  Local Compose remains synthetic. Per-user provider choice and history imports
  are deferred, and deployment-specific usage/license review remains open.

- Reworked the local web interface into clear discovery, taste profile, account,
  and data areas with responsive navigation, accessible focus states, and plain-language
  labels. Guest tracks are marked as examples, and recommendation details no longer
  show a popularity penalty for the popularity-neutral V1 scorer.

- Restore the approved MediatR 12.5.0 licensing exception in central versions and package locks. CI generates a disposable database password for each run, and the local runbook selects the current UTC ISO week.

- Add non-root web and Worker container targets, migration and private-volume initialization, and a local PostgreSQL Compose stack with loopback-only web access. CI supplies PostgreSQL and checks that the full test suite runs without skips. Public release remains gated on provider approval and production operational decisions.

- Pin stable .NET SDK 10.0.401, EF Core/runtime/tool 10.0.12, and current stable dependencies with locked restore. Retain the approved MediatR exception and use NSwag for Development-only API documentation.

- Remove credential-shaped literals from secret checks, Compose, and CI. Generate private database connection strings with local credentials; existing ignored Compose environment files need regeneration.

- Registration uses the selected UTC delivery weekday/hour. Added an explicit, guarded
  local repair tool for legacy registration slots. Bounded scheduler ticks track
  candidate attempts so persistently stopped accounts cannot starve healthy peers.

- Added a Development-only synthetic scheduler with an explicit account allowlist,
  bounded ticks, UTC due slots and a PostgreSQL lease. Verified local capture advances
  the claimed slot by one week; failures, opt-out, changed preferences and cancellation
  preserve it. Restart verifies the original encrypted receipt. Storage reconciliation
  remains an explicit operator step. The local QA menu now includes schedule fixture setup.
- Export 2.2 adds stored Identity profile fields, including username, contact
  confirmation and audit values, to the existing account/taste export. Credentials,
  authentication internals and unsubscribe capabilities remain excluded. Added an
  explicit table/column privacy inventory. The privacy page explains credential
  exclusions and the separate account, local-copy, shared-catalog and backup boundaries.

- Encrypt complete local email captures and partial writes using ASP.NET Core Data
  Protection with a separate file purpose. Recovery, sanitized export and account
  cleanup decrypt in memory. Local operator inspection prints sanitized text without
  creating preview files; explicit conversion replaces authenticated legacy plaintext
  copies with encrypted files and reports unverifiable leftovers. Keys remain in the
  existing database key store, whose access and backups require separate protection.

- Added a runnable local email QA launcher and explicit cleanup retry after account
  deletion. Unix sink directories must be owner-only and MIME files are created private;
  capability queries are excluded
  from request logs, and conflicting receipt publication stops. Phase 7 passed local
  process/browser checks and 388 tests; developer confirmation remains pending.

- Added explicit offline Worker reconcile/generate/capture actions, account ownership
  coordination, opt-out checks under PostgreSQL row locks, and restart recovery
  across file publication/SQL commit. Recorded generation requires a separately
  approved exact local-use manifest; credentials cannot enable live generation.

- Added bounded local UTF-8 plaintext/HTML weekly messages with stored explanations,
  ordinary listening links, unsubscribe and no remote resources or tracking. Atomic
  MIME capture validates durable receipts, accounts for failed partials and supports
  sanitized copy export and ownership-based cleanup. External delivery remains pending.

- Added an explicitly enabled Development-only unsubscribe confirmation flow and
  persistent email opt-out. Export 2.1 exposes the opt-out and local copy inspection
  status; account deletion reports incomplete local copy cleanup after SQL deletion.
  Local capture has its own status and does not claim external delivery.

- Added bounded recorder-fed weekly digest generation with atomic PostgreSQL persistence,
  complete stored evidence, canonical ISO weeks, replay and storage/expiry guards. Export 2.0
  includes complete snapshots, audit fields and all digest history through bounded pages.
  Later ratings preserve familiarity; marking a rated track known preserves its numeric rating.
  Local checks pass; developer confirmation and recommendation quality remain pending.

- Added pure provisional baseline-A scoring with popularity-neutral ranking, one pick per
  artist, complete versioned score inputs/contributions and stored-input replay. Legacy
  payloads remain readable; new snapshots retain unknown JSON fields. Recommendation
  quality remains unverified and real evaluation remains open.

- Redesigned the README with a musical identity, clearer product goals and development
  status, architecture diagrams, and a separate guide for navigating the codebase.
  Added original record sleeve artwork and a Codebase Memory Nebula snapshot with
  source and coverage notes in `docs/architecture`.

- Added a security policy covering supported development code, private vulnerability
  reporting, response targets, and coordinated disclosure.

- Recheck recorder storage inventory after pacing and before each provider request. Preserve blank-name
  Last.fm tag observations, including their count, URL and response position, as excluded evidence.

- Added Phase 4 local pilot tooling: frozen A–D scoring, complete normalized explanations,
  approved-recording replay, shape/adequacy gates, bounded reports and concealed blind review.
  Recorder metadata now retains response receipt time separately from request start time.
  Verified with offline inputs; live manifest approval, recording and developer ratings remain pending.

- Added Phase 3 transient ingestion evidence: missing/invalid values and coverage gaps, every
  discovery path, attribution, response hashes/times and explicit origins. Removed invented tags,
  listener estimates, matches and hybrid fixture substitution; added in-memory recorded replay.
  No schema changes or live acquisition; verification awaits developer confirmation.

- Added Phase 2 fixture evaluation mechanics, reviewed profile membership locks, metric checks,
  and a separate manifest-gated recorder with bounded storage accounting. Replaced the legacy
  founder test with an offline isolation check. No live recording or catalog-quality evaluation
  was performed; implementation verification awaits developer confirmation.

- Revised the renewal plan into nine approval-gated phases, with proposed pilot/evaluation thresholds
  and separate recording manifests; Phase 1 authorization is unchanged and later phases remain unapproved.
- Recorded developer approval of the revised written plan, evaluation thresholds, pilot bars and
  reviewed interpretations; later-phase execution and concrete recording manifests remain gated.

- Added explicit loopback-only development defaults (`postgres` database password and a local JWT
  signing key) at the developer's request. Production credentials remain unset and the development
  signing key is rejected outside Development. Application users still choose their passwords.
- Secured browser sessions and refresh-token rotation, added account lockout and request throttling,
  and made registration, account deletion, and feedback replacement transactional.
- Added CORS, transport and security-header defaults; removed the vulnerable AutoMapper dependency,
  pinned XML cryptography remediation, and stopped tracking generated build output.
- Guarded login and registration against stale responses, preserved familiarity when rating known
  tracks, cleared obsolete rating highlights, and surfaced unsuccessful feedback submissions.
- Added trusted forwarded-header handling for configured proxy IPs and made the Identity FK
  migration tolerate pre-existing domain profiles while enforcing the relationship on new rows.

- Reconciled project documentation with popularity-neutral V1 and explicit approval gates.
- Qualified earlier coverage, delivery, export and privacy claims; previous release entries below are historical assertions, not current verification.

All notable changes to the Liner Notes project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).


<a id="source-changelog--041---2026-10-02"></a>

#### [0.4.1] - 2026-10-02


<a id="source-changelog--security--hardening"></a>

##### Security & Hardening
- **Secret Zeroization & JWT Validation (P0/Finding 1):** Removed hardcoded development JWT signing key from committed `appsettings.json` and eliminated fallback signing keys. Enforced strict 256-bit (>= 32-byte) key validation in `DependencyInjection` and `JwtService`, backed by .NET user-secrets.
- **Refresh Token Validation & Rotation (P1/Finding 2):** Bound refresh tokens to user identity using ASP.NET Core Identity authentication tokens (`UserManager.SetAuthenticationTokenAsync`/`GetAuthenticationTokenAsync`). Enforced strict verification on `/api/auth/refresh` and automatic token rotation.
- **Atomic Registration & Rollback (P1/Finding 3):** Added compensating rollback to `AuthController.Register` to purge orphaned Identity credentials if domain subscriber registration or taste seeding fails.
- **Anti-XSS Protection (P1 & P2/Findings 12 & 13):** Added `escapeHtml` sanitization to all track metadata (title, artist, album) in `app.js` and migrated raw JSON export viewer from `innerHTML` to safe DOM `textContent`.
- **Export Authorization (P1/Finding 6):** Enforced authentication verification on `/api/export/my-data` and refactored client export download to use authenticated `Bearer` token requests with blob streaming.
- **Identity Cleanup on Account Deletion (P1/Finding 9):** Coordinated deletion of ASP.NET Core Identity credentials when purging subscriber profiles via `DELETE /api/subscribers/me`.


<a id="source-changelog--fixed--improved"></a>

##### Fixed & Improved
- **EF Core Database Migration (P1/Finding 4):** Generated and verified migration `AddRatingToWeeklyRecommendation` adding the `Rating` column to `WeeklyRecommendations`.
- **JSON Enum Binding (P1/Finding 5):** Added `JsonStringEnumConverter` to `UserFeedback` and controller JSON options to reliably bind feedback strings (`Liked`, `Disliked`, `AlreadyKnown`).
- **Data Integrity in Upstream Mapping (P1/Findings 7 & 8):** Prevented artist MBID propagation onto track identifiers and eliminated placeholder/invented songs when upstream results are empty.
- **Idempotent Feedback & Contradiction Resolution (P1/Findings 10 & 11):** Purged existing signals before writing updated preferences, preventing duplicate signal accumulation and teaching conflicting tastes.
- **Digest Contract Alignment (P2/Finding 14):** Aligned frontend DTO mapping with backend schema (`noveltyBoost`, `matchedTags`, `tagName`, `feedback`, `rating`).
- **Memory Cache Bounding (P2/Finding 15):** Capped in-memory cache size (`SizeLimit = 10,000`, `CompactionPercentage = 0.20`) with explicit entry size weights (`Size = 1`).
- **Deterministic Cancellation (P2/Finding 16):** Propagated `OperationCanceledException` across candidate discovery and hydration to prevent returning partial recommendation results upon cancellation.


<a id="source-changelog--040---2026-10-02"></a>

#### [0.4.0] - 2026-10-02


<a id="source-changelog--added"></a>

##### Added
- **Upstream Recommendation Source Abstractions (Phase 2):**
  - Added `IRecommendationSource` contract (`GetCandidatesByArtistsAsync`, `GetCandidatesByTagsAsync`) in `src/Application`.
  - Added `ICandidateHydrator` contract (`HydrateCandidateAsync`, `HydrateCandidatesBatchAsync`) in `src/Application`.
  - Added immutable record `RawCandidateTrack` holding unhydrated candidate metadata and upstream match scores.
- **Last.fm Client Subsystem & Infrastructure:**
  - Implemented typed `LastFmApiClient` using `HttpClientFactory` and identifiable `User-Agent`.
  - Built thread-safe `LastFmRateLimiter` implementing token-bucket pacing (4 requests/sec max) complying with Last.fm's fair-use limits.
  - Implemented in-memory response caching adhering to Last.fm's 100 MB reasonable usage cap and HTTP caching terms.
  - Implemented **Hybrid Fixture Fallback Mode** (`LastFmClientMode.Hybrid`): automatically queries live Last.fm APIs when an API key is present and gracefully falls back to deterministic offline fixtures (`LastFmFixtureProvider`) when offline, rate-limited, or in CI environments.
  - Implemented `LastFmRecommendationSource` and `LastFmCandidateHydrator` with logarithmic listener popularity normalization and stoplist tag noise filtering.
  - Wired Last.fm subsystem in `src/Infrastructure/DependencyInjection.cs`.
- **Infrastructure Test Suite (`tests/Infrastructure.Tests`):**
  - Created new test project with 15 passing tests covering rate limiting, caching, HTTP fakes (`MockHttpMessageHandler`), error handling (HTTP 429), and candidate hydration.
  - Solution test suite expanded to 186 passing tests with 100% pass rate.
- **Founder Seeds Coverage Spike:**
  - Executed live coverage probe against Last.fm for the 6 founder taste clusters (Jakuzi, Son Feci Bisiklet, Hotline Miami, Dying Light, ULTRAKILL, Hades).
  - Generated comprehensive findings report in `docs/coverage-spike-founder-seeds.md`.


<a id="source-changelog--030---2026-10-02"></a>

#### [0.3.0] - 2026-10-02


<a id="source-changelog--added-1"></a>

##### Added
- **Granular Feedback & Preference Scaling (Phase 6):**
  - Added optional 1–10 preference rating scale alongside `Liked`, `Disliked`, and `AlreadyKnown` feedback on `WeeklyRecommendation`.
  - Closed feedback loop in `RecordRecommendationFeedbackCommandHandler`: recording feedback now automatically generates provenance-tagged `TasteSignal` entries that nudge tag weights for future weeks.
  - Added `TasteSignalSource.RecommendationAlreadyKnown` and `TasteSignalSource.RecommendationRating` enum flags.
  - Added interactive 1–10 rating bar to web dashboard in `wwwroot/app.js`.
  - Added 64 new unit and integration tests across Domain, DataAccess, Application, and Presentation test suites. Total passing test suite expanded from 107 to 171 tests (0 failures, 0 skipped).
  - Generated comprehensive requirement-driven test cases document `tests/liner-notes-comprehensive-test-cases.md` covering functional, edge cases, error handling, and state transitions.


<a id="source-changelog--changed"></a>

##### Changed
- **Residue Scorer ("Prism") Feedback Granularity:**
  - Updated `RecommendationScorer.DetermineFeedbackPenalty` to distinguish specific track rejection (1.0 penalty) from artist rejection (0.7 penalty), preventing single-track dislikes from permanently banning an entire artist.
- **Copy & Provenance Hardening:**
  - Removed overclaiming language ("anti-algorithmic") in favor of "transparent scoring with popularity-bias control".
  - Clarified streaming links as direct 1-click search links rather than authenticated API URIs.
  - Established engine version codenames: Residue V1 "Prism" and Residue V2 "Resonance".


<a id="source-changelog--020---2026-10-02"></a>

#### [0.2.0] - 2026-10-02


<a id="source-changelog--added-2"></a>

##### Added
- **ASP.NET Core Presentation Layer (`src/Presentation`):**
  - Lightweight composition root wiring Onion Architecture layers (`Application`, `DataAccess`, `Infrastructure`, `Presentation`).
  - ASP.NET Core Identity integration with `ApplicationUser` in `identity` schema, authenticated via JWT Bearer tokens with configurable `JwtOptions`.
  - Concrete service implementations:
    - `CurrentUserService` providing authenticated user context and claims extraction.
    - `JwtService` generating cryptographically signed access and refresh tokens.
    - `AuthService` handling user registration, password verification, and credentials management.
  - RFC 7807 compliant `ApiExceptionFilterAttribute` mapping `ValidationException` (400), `NotFoundException` (404), `UnauthorizedAccessException` (401), and `InvalidOperationException` (400) to standard `ProblemDetails`.
  - Feature-complete REST API controllers:
    - `AuthController`: register, login, refresh token, and authenticated profile (`/api/auth/me`).
    - `SubscribersController`: subscriber profile resolution and GDPR account deletion (`DELETE /api/subscribers/me`).
    - `TasteController`: hybrid onboarding taste profile seeding (3-5 seed tags/artists) and atomic taste signal inspection.
    - `DigestsController`: latest weekly digest retrieval and 1-click sentiment feedback (`Liked`, `Disliked`, `AlreadyKnown`).
    - `ExportController`: complete GDPR verbatim JSON data export (`/api/export/my-data`).
  - Single unified OpenAPI / Swagger UI (`/swagger`) with JWT Bearer security scheme.
  - Health checks endpoint (`/health`) with EF Core `DataContext` connectivity verification.
  - Guest-first discovery and subscriber dashboard in `wwwroot/`:
    - Responsive dark-mode interface with zero-friction 1-click streaming deep-links (YouTube, Spotify, Bandcamp, Apple Music).
    - Stored Residue score breakdown inspector displaying tag overlap, anti-popularity penalty, and novelty boost.
    - Interactive 3-5 seed tag/artist selector for cold-start onboarding.
    - "Your Data" viewer and 1-click JSON export.
- **Presentation Test Suite (`tests/Presentation.Tests`):**
  - 25 unit and controller tests verifying exception filtering, JWT token lifecycle, claims principal extraction, and controller contracts.
  - Total solution test suite now stands at 100 passing tests across Domain, Application, DataAccess, and Presentation.


<a id="source-changelog--010---2026-09-30"></a>

#### [0.1.0] - 2026-09-30


<a id="source-changelog--added-3"></a>

##### Added
- **Onion Architecture Skeleton (.NET 10):**
  - `src/Domain`: Zero-dependency domain model containing pure entities, value objects, and scoring logic.
  - `src/Application`: MediatR CQRS orchestration shell with dependency injection registration.
  - `src/DataAccess`: Dedicated EF Core data access layer targeting PostgreSQL 16+ via Npgsql (AppDbContext, Entity Configurations, Migrations).
  - `src/Infrastructure`: HttpClientFactory (Last.fm, ListenBrainz), email delivery, and in-memory caching.
- **PostgreSQL DataAccess Layer & Migrations:**
  - `AppDbContext` targeting PostgreSQL 16+ with `IAppDbContext` abstraction and `IDataProtectionKeyContext` for at-rest token protection.
  - Automatic UTC timestamp tracking (`CreatedAt` and `LastModifiedAt`) for all `IAuditableEntity` models in `SaveChangesAsync`.
  - Global query filter for soft-deleted `User` entities (`!u.IsDeleted`).
  - Robust entity configurations mapping all 8 domain models, enum string conversions, value object conversions (`IsoWeek`, `ScoreBreakdown` stored as `jsonb`), and cascade rules.
  - Repository interfaces (`IUserRepository`, `IWeeklyDigestRepository`, `ITasteSignalRepository`, `ITrackRepository`) in `Application` and implementations in `DataAccess`.
  - Initial EF Core migration (`InitialCreate`) with PostgreSQL snapshot.
  - Test suite (`tests/DataAccess.Tests`) with 11 tests verifying model configuration, soft-deletes, relationships, audit timestamps, and architectural isolation.
  - `src/Presentation`: Lightweight ASP.NET Core presentation composition root (renamed from `src/Web`).
  - `src/Worker`: Dedicated background service composition root for scheduled batch processing.
  - `tests/Domain.Tests`: Unit test project referencing Domain.
- **Pure Deterministic Recommendation Scorer (`RecommendationScorer`):**
  - Pure function evaluating candidate tracks against user taste profiles with zero I/O, zero randomness, and zero side effects.
  - Four configurable scoring components via `ScoringParameters`:
    1. Tag-overlap cosine similarity over weighted tag vectors ($W_{\text{tag}}$).
    2. Anti-popularity penalty ($W_{\text{pop}}$) penalizing mainstream artists.
    3. Novelty boost ($W_{\text{nov}}$) prioritizing tracks/artists unfamiliar to the user.
    4. Feedback penalty ($W_{\text{fb}}$) heavily penalizing explicitly disliked items.
  - Structured, persistent `ScoreBreakdown` providing full inspectability and deterministic explanation generation without text hallucination.
- **Tag Vector Abstraction (`WeightedTagVector`):**
  - Reusable, immutable value object mapping normalized tag strings to continuous weights.
  - Optimized cosine similarity math and top overlapping tag contribution extraction.
  - `Tag` value object enforcing lowercase, trimmed normalized identity.
- **Atomic Taste Signals (`TasteSignal`):**
  - Provenance-tagged atomic records (`TasteTargetType`, `TasteSignalSource`, `Context`) to support data minimization, user data inspection, and verbatim export/deletion.
- **Weekly Digest & Idempotency (`WeeklyDigest`, `IsoWeek`):**
  - ISO-8601 calendar week value object (`IsoWeek`) enforcing idempotent batch delivery: at most one digest per user per calendar week.
  - `WeeklyRecommendation` holding ranked tracks, stored score breakdowns, and user feedback.
- **Benchmark Seed Set Fixture (`BenchmarkSeedFixtures`):**
  - Fixed benchmark set spanning 8 genres: Metal, Hip-Hop, Electronic, Jazz, Pop, Indie, Classical, and Regional (Anatolian Rock/Mugham).
- **Unit & Architectural Test Suite:**
  - 22 unit tests covering taste similarity, anti-popularity penalty, feedback penalty, novelty boost, determinism across 1,000 iterations, tie-breaking, and architectural zero-dependency verification.
- **Continuous Integration:**
  - GitHub Actions workflow (`.github/workflows/ci.yml`) targeting .NET 8.


<a id="source-changelog--engineering-decisions--judgment-calls"></a>

##### Engineering Decisions & Judgment Calls
1. **WeightedTagVector Design:**
   - Designed as an immutable value object with lazy magnitude caching, case-insensitive normalized tag lookups, and dedicated top-overlapping tag contribution breakdown to feed `ScoreBreakdown`.
2. **Taste Signal Aggregation vs. Storage:**
   - Maintained atomic `TasteSignal` records as discrete immutable records in `Domain` with source provenance, while providing `UserTasteProfile` as the pure input snapshot to the scorer. This decouples the storage/audit requirement from the scorer.
3. **Deterministic Ranking & Tie Breaking:**
   - Resolved candidate ties deterministically using `CandidateKey` (`mbid` or `normalized_artist:normalized_title`) with ordinal comparison so rankings are bitwise repeatable across machines and runs.
4. **Weekly Idempotency:**
   - Implemented `IsoWeek` value object and unique constraint on `WeeklyDigest` to guarantee a subscriber cannot receive two digests in the same calendar week.
<!-- END SOURCE SNAPSHOT: CHANGELOG.md -->

---

<a id="source-handoff"></a>

## Appendix A17 — Performance handoff excerpt only

Original: [MEMORY.md](MEMORY.md). Performance-only excerpt from line 84. Original source: 17,274 UTF-8 bytes, 97 lines.

Source SHA-256: `c05b66d6fcc145f24055cd92af357cc71c38e095222e189a0789599ecf14c19c`.

Included excerpt SHA-256: `03f2ba154cdac2d31faf17bd755e9893f5f91d6e4b64952b5eff8336cf9aab20`.

<!-- BEGIN SOURCE SNAPSHOT: MEMORY.md -->
[verified] Performance remediation handoff: G0/T01, G1a–G1c and G1d0–G1d3 and the G1d4 internal metadata snapshot foundation are locally implemented; the latest combined gate passes 848/848 checks while production full scans remain enabled. G1d3 separately passed all 171 DataAccess checks on PostgreSQL 16.15. Read PROGRESS.md, docs/renewal/performance-remediation-plan.md, docs/renewal/performance-manifest-integration.md and docs/renewal/performance-remediation-evidence.md before continuing. (2026-10-27)
[verified] Remaining authorized sequence: finish G1d mutation-admission metadata snapshots/cache use, periodic full-audit deadline/service, the integration gaps identified in docs/renewal/storage-boundary-scan.md and measured opt-in incremental admission; then G2 staged complete exports/owner-indexed archives, G3 authentication/write contention, G4 transactional revisions/quota accounting/trigger costs, G5 scoring allocations, G6 HTTP/recording/buffering/compression, and G7 combined regression/load/migration/recovery/soak acceptance. Preserve P04/P10/P11/P17; P05/P07 remain measured accepted tradeoffs. (2026-10-27)
[verified] G1d3 retains only live validated parts larger than 64 KiB for one authenticated generation, at most 48,000,096 bytes; it still hashes each on-disk part and consumed buffer. Matched controls reduce 10,000-entry read allocation about 78% and improve the 25-reader control, but sequential latency/CPU are mixed and sometimes regress; SQL-state requests and metadata traffic are unchanged. The cache currently serves consumed reads, not mutation-admission snapshots or incremental auditing. (2026-10-27)
[verified] The older injected post-SQL-commit stopped outcome remains unattributed and must be investigated in G7 without weakening assertions. No VPS, live-provider, real-email or production acceptance is established. (2026-10-27)
[verified] A retained PostgreSQL 16 trial stopped with email_sink_tracked: concurrent sandbox commands expose an empty /tmp/.git marker while unrestricted commands see it absent. Final tests pass with TMPDIR set before launching .NET to a private mode-0700 directory under ignored diagnostics/results. Preserve the Git-exclusion guard and failed evidence; this mechanism does not attribute every earlier stopped result. See diagnostics/README.md. (2026-10-27)
[verified] This checkout uses /home/mahammadjafarli/.dotnet/dotnet (SDK 10.0.401/runtime 10.0.12); set BuildInParallel=false and UseSharedCompilation=false for the recorded build/test recipes. Frozen-binary benchmarks run separately from tests/builds; diagnostics/results/remediation-20261025 contains ignored raw evidence and reusable recipes. Recheck current tool/container availability, avoid the unrelated linernotes-ide-db-1 database, and preserve secrets/payloads outside Git. (2026-10-27)
[verified] G1d4's internal metadata snapshot foundation passes 848/848 local checks, both offline Node checks and the five-project TRX gate. It is not connected to production admission: the same-length/restored-timestamp regression deliberately passes metadata comparison but is rejected by current full admission and consumed-byte verification. Finish process-local startup/full-audit trust, the 30/60-second refresh/deadline service and reader/writer coverage before enabling any shortcut. (2026-10-27)
[verified] G1d4 scanner controls use one frozen build, five full versus five metadata validations, two warmups and three alternating process pairs. Read traffic falls over 99% for populated fixtures, but 10,000-file latency improves only 63% against the 80% target and empty scans regress. Shared-cache gate contention was subsequently measured in docs/renewal/storage-boundary-scan.md and must be resolved before activation. The 111-recipient offline probe has exact accounting; it is functional evidence, not production speed or real email acceptance. See the G1d4 evidence section and ignored g1d4-* results. (2026-10-27)
[stated] For this continuation, the developer clarified that they wanted the next targeted scan, then requested it; keep scan findings distinct from implementation and do not describe an unfinished remediation group as complete. (2026-10-27)
[verified] Targeted storage reader/writer scan at 862af96 is documented in docs/renewal/storage-boundary-scan.md: expanded source candidates cover 159 file-I/O lines/22 files and 119 SQL-access lines/28 files plus callers/operator tools. Production sources are unchanged. Development HTTP reconciliation lacks an operator policy, database admission lease and independent ledger verification; its trust-reset primitive is reproduced offline, but no authenticated HTTP exploit was run and Production does not map the route. Choose CLI-only signed maintenance or a reviewed privileged web protocol before fixing it. (2026-10-27)
[verified] Scan probes reproduce managed-root pacing overlap causing artifact_inventory_changed and a mutable third-pass catalog stream reaching 250 fake-store append rows before hash failure, with publication blocked and current host callers using private buffers. Controlled metadata-scan overlap delays 10,000-entry readers: median of process medians 5.407 to 133.268 ms; this scanner is inactive. Resolve gate contention without violating bounded metadata retention before activation. Raw final evidence: diagnostics/results/remediation-20261025/g1d-storage-scan-final. (2026-10-27)
[verified] Scan verification passes 154 distinct targeted checks (Infrastructure 116, DataAccess 29, Presentation 9), documentation checks and 11 fresh-process observations of the unchanged post-commit recovery expectation. Added failure messages expose stopped reasons without weakening assertions. The original intermittent stop is still unattributed; startup/audit/deadline implementation, activation, G2–G7 and deployment acceptance remain unfinished. (2026-10-27)
[verified] Rebuilding after a local commit changes assembly informational Git metadata and whole-DLL hashes even with identical production sources. Scan frozen/tested Infrastructure hashes differ, but all source hashes and 2,155 managed method bodies/signatures/locals/exception regions match; both provenances are recorded in the boundary report and ignored g1d-scan-assembly-compare evidence. Never relabel those binaries as identical. (2026-10-27)
[stated] Consolidate every performance finding/report, evidence, decision and remaining item in Performance_Issues.md at the project root. Preserve historical qualifications and distinguish scans from implemented remediation. (2026-10-27)
<!-- END SOURCE SNAPSHOT: MEMORY.md -->
