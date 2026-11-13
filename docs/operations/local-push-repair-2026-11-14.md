# Local push gate repair — 2026-11-14

The repository pre-push hook failed on two Application tests. Package-pruning
and StringTools target-framework warnings were not the failing checks.

## Complete coverage bound

The 1,000-favorite/400-candidate fixture stores a 98,093-byte score receipt and
a 442,051-byte complete coverage audit. The audit exceeded the 262,144-byte
application/database constraint. The new bound is 524,288 bytes (512 KiB),
with an EF migration updating the database constraint to the same bound.
The full evidence is retained and the reservation continues to use the actual
UTF-8 audit length multiplied by four. The receipt retains its 100,000-byte limit.
Existing oversize rejection tests use the shared bound and remain applicable.

## Frozen discovery contract

The frozen test was run on its establishing commit d49c192 in a detached worktree;
it passed there. Original and current CONTRACT_JSON outputs were parsed and
recursively compared, including embedded CoverageJson. Across 16 results the
only differing fields were:

- ConfigurationPolicy.MaximumUpstreamRequests: 80 to 200, an already approved
  acquisition change from commit 6774465.
- ConfigurationSha256, reflecting that changed request budget.
- RejectedOversizedCollaborations: newly present, zero for every benchmark fixture.
- ReservationBytes, accounting for the complete audit and current receipt size.

No picks, order, scores, selection inputs or other fields differed. The frozen
hash was refreshed to match these reviewed metadata changes, rather than changing
recommendation behavior to meet a stale whole-result hash.

The earlier daily-attempt test setup and privacy-inventory regressions were fixed
in 26cf943. This repair does not change the secret-history rewrite or bypass the
pre-push hook. Final verification is reported with the task result.
