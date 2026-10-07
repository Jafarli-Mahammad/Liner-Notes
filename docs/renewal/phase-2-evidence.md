# Phase 2 implementation and offline evidence

Date: 2026-10-07. Starting revision: `f8c4f69` on `Prism`.
The developer requested Phase 2 execution, then explicitly approved the exact
membership table and offline legacy-test replacement in
[the design](phase-2-design.md). Implementation confirmation is pending.

**Mechanics only, no catalog conclusions.**

## Implemented scope

- Harness/metrics in the existing `tests/Application.Tests/Common/` folder;
  references remain Application/Domain only. The existing Domain benchmark source
  is linked into this test assembly, preserving one fixture definition and adding
  no package or project reference. No production scorer/ingestion change.
- Immutable reviewed membership: six founder, sixteen development, sixteen
  held-out and thirteen synthetic profiles. Counts, genres, duplicate identities,
  partition leakage, Unicode normalization and mutation checks are implemented.
  Five singleton founder profiles stay sparse. No held-out response was read.
- Approved hash:
  `de78f92afd725f4dd22da6446a9c41f5ca78f6959eacf14753eb092902b62c40`.
  The local generated lock was written before the first harness ranking, at
  `recordings/phase2-membership-2026-10-07/profile-lock.json`.
  `git check-ignore` confirms it is ignored; `git ls-files` returns no entry.
  Rebuild from `Phase2ProfileMembership.Locked()` and optionally use
  `ProfileLock.WriteLocalAsync` with a new local directory ID.
- The harness accepts only synthetic fixture profiles and provenance, requires a
  valid reviewed lock before invoking its ranking callback, snapshots inputs,
  excludes exact known/disliked tracks, retains siblings, orders ties ordinally,
  rejects invalid/nonfinite/inconsistent explanations, returns short lists without
  padding and exposes coverage gaps. It has no live source or credential lookup.
- Metrics: 3/5-pick coverage, macro/pooled artist concentration, novelty, per-seed
  distinct yield, top-k overlap, coefficient perturbations and stability bars,
  normalized explanation arithmetic, popularity missingness/collapse diagnostics,
  blind-rating arithmetic and paired margin. Fabricated inputs have independently
  specified expected values; no formula is selected or tuned.
- The legacy founder test now uses fixture-only mode and an outbound-rejecting
  handler with a dummy configured key, exercises all eight existing founder seeds,
  checks bounds and zero attempts, and performs no credential discovery or writes.
  Its old unsupported coverage report assertions were replaced with the approved
  isolation contract. Existing fixture fallback behavior remains Phase 3 work.
- `scripts/record_lastfm.py`: a separate recorder, with approved manifest/hash,
  local profile/protocol verification, clean pinned checkout, ignored/untracked
  containment, no overwrite/redirect/retry, canonical depth-one expansion,
  sequential pacing, request/byte caps, sanitized ledger/provenance, response hashes
  and incomplete-run state. Phase 5 requires its separate Last.fm gate and emits
  no held-out statistics. No scoring/shape-analysis routine runs in this tool.
- `scripts/acquisition_guard.py`: complete seven-category accounting, one writer,
  conservative maximum response/output reservation, strict shared threshold,
  additional per-run cap, stale/unknown/changed inventory rejection, overflow,
  cancellation and oversized-response checks. Recorder also takes an OS writer
  lock and reconciles filesystem metadata before/after each own write.

## Verification

```sh
dotnet test tests/Application.Tests/Application.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~FixtureEvaluationHarnessTests|FullyQualifiedName~ProfileLockTests|FullyQualifiedName~EvaluationMetricsTests' \
  --logger 'console;verbosity=minimal'
python3 scripts/test_phase2_recorder.py
dotnet test 'Liner Notes.sln' --no-restore --logger 'console;verbosity=minimal'
```

| Check | Result |
|---|---|
| Focused harness, lock and metric tests | 22 passed |
| Fake recorder/storage checks | 21 passed |
| Full .NET suite, including founder test | 216 passed; 9 existing PostgreSQL tests skipped; 0 failed |
| Existing Domain suite/benchmark checks | 77 passed as part of full suite |
| Harness determinism | 100 repetitions and 100 seeded permutations per each of four cultures |
| Cultures | invariant, en-US, tr-TR, az-Latn-AZ |
| Recordings/lock containment | lock ignored and untracked |

The .NET sandbox attempt exited with no diagnostics; the successful commands used
approved outside-sandbox execution for MSBuild IPC. The pre-existing NU1510 XML
cryptography package warning remains. No dependency change was made. The PostgreSQL
tests require an explicit local database configuration and were not exercised here;
Phase 2 changes no database behavior. No claim that this run re-confirms Phase 1.

Determinism checks cover fixture harness ordering, numeric output, seed/profile
lock permutations and contribution permutations. They do not establish A–D scorer
correctness or real candidate/path/tag determinism; those implementations and their
frozen experiments belong to later phases. The injected callback in the tests
returns precomputed fabricated scores, avoiding a Phase 2 production formula rewrite.

## Recorder preflight and remaining run gates

The recorder is written and tested with fake inputs; **no live run was performed**.
There is no approved live manifest in this change. Ordinary tests never invoke
`live_transport`. Successful fake recordings exist only in temporary directories.
Live CLI execution requires both a separately approved concrete manifest and the
explicit execution flag. No default or sample approved manifest is shipped.

Manifests use JSON with the field names in the contracts' YAML planning template,
plus `profile_lock_path` and `protocol_path`. Both are absolute ignored/untracked
local files under `recordings/`. Hash canonical sorted compact UTF-8 JSON with
`approval.approved_manifest_sha256` set to null; pass that same approved digest
externally. This avoids circular hashing. Lock hashing matches the C# versioned,
length-prefixed representation; protocol SHA-256 covers exact file bytes.

The inventory file is an explicitly reconciled JSON artifact containing:

- `measured_at_utc` (UTC, at most five minutes old at startup), `reconciled: true`,
  and an `exclusive_acquisition_reference`;
- `categories` with integer uncompressed-byte totals for `recordings`, `http_cache`,
  `derived_database`, `reports`, `copies`, `backups`, and `partials`;
- absolute existing `filesystem_roots` covering recordings and all file-based data,
  `filesystem_revision` from `filesystem_revision(roots)[0]`, and `filesystem_bytes`.

The manifest pins the inventory path, exact file SHA-256 and matching measurement
timestamp. Include the stable `.acquisition.lock` file in the reconciled inventory
before approving a run. Filesystem roots must not overlap or contain symlinks.
Recordings/locks/manifests/reports count toward inventory. The complete file-backed
inventory is rechecked before every request and reconciled after writes. Response
and metadata reservation happens before a call; reaching 80,000,000 projected bytes
stops it. Final status space is reserved conservatively at startup.

Database/cache/backups accounting is an explicit operator reconciliation input,
not an automatic database measurement. Cross-process production database/cache
coordination remains Phase 7 work; the recorder is not a production storage monitor.
No retention policy or deletion job is adopted. Unknown external accounting must
be marked unreconciled. A concrete Phase 4/5 run must review the full inventory and
exclusive acquisition arrangement before execution.

The transport requests uncompressed identity encoding and rejects compression;
it reads at most the response bound plus one byte and discards an oversized body.
It stops on provider/HTTP errors or `Retry-After`, requiring a revised approved run
for stricter pacing. It retains safe caching headers, raw responses and provenance;
it does not turn absent tag counts or track listeners into artist-wide evidence.
Last.fm endpoint/terms documentation was checked on 2026-10-07; see the linked
authoritative references in [the design](phase-2-design.md#authoritative-provider-checks-2026-10-07).

Phase 3 ingestion honesty, real A–D scoring, listening, formula adoption, acquisition,
schema, pipeline, delivery and release remain outside this implementation. Future
Phase 4/5 manifests must include the concrete protocol and all required approvals.
