# Explicit legacy registration slot repair

Registration now selects the next matching UTC weekday/hour using the same policy
as scheduled ticks. Invalid non-null slots remain stopped; ticks never repair them.

For a local Development account known to have a legacy registration slot, preview:

```bash
DOTNET_ENVIRONMENT=Development dotnet run --file scripts/phase8_repair_registration_slot.cs --no-cache -- \
  --user <account-id> --expected 2026-10-19T17:00:00Z --replacement 2026-10-23T17:00:00Z
```

Supply the database connection through the existing configuration/environment,
not command arguments. Only loopback PostgreSQL is accepted. Inspect the account's
creation date, configured weekday/hour and existing due date first. Choose the
replacement explicitly: preserving the original intended week or choosing a
future slot is an operator decision. Repeat with `--apply true` to write.

Apply holds the scheduler, generation-batch and account leases, compares the exact expected slot and
preferences, requires an active subscribed account with active Identity, rejects
already valid slots, and requires the old slot to equal UTC creation date plus
seven days at the configured hour. Any existing digest work rejects the repair.
This pattern cannot prove provenance; the operator must identify the account as
legacy registration data. Slots outside this narrow pattern require separate
investigation. Concurrent edits reject the compare-and-set. No bulk rewrite runs.

Candidate selection records scheduling activity in `Users.LastModifiedAt` and
clears `LastModifiedBy`. Least recently modified due accounts are selected first,
within the existing limit. Each selected batch receives a stamp newer than every
allowlisted account's audit stamp. This survives restart and fixed test clocks;
failures keep their original due slots and return behind peers. Other account
edits also count as recent activity. Audit data remains covered by export 2.2.

Verification (2026-10-12): 20 targeted application checks passed; the final
whole-solution run passed 474 checks with zero failures/skips, including 13 real
PostgreSQL scheduler checks. The repair file builds successfully. Published
Docker loopback ports reset connections in this environment; database checks
ran in the cached .NET SDK container sharing the PostgreSQL container network.
Developer confirmation remains pending; no existing accounts were repaired.
