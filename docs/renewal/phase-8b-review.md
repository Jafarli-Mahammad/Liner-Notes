# Phase 8b scheduling and recovery — concrete design for review

Date: 2026-10-12. Status: phase direction authorized; concrete design awaiting review.

## Intent and scope

The developer selected 8a implementation/testing, then 8b implementation/testing,
then a combined verification pass. This design is the next step after 8a's local
privacy verification. Prior implementation confirmation remains separate.

Implement a bounded Development-only scheduled Worker over the existing synthetic
generation and encrypted local-capture pipeline. Verify scheduling, unsubscribe
enforcement, cancellation, concurrent/restarted processes and durable recovery.
Keep local capture distinct from external delivery: no provider sending, live
acquisition, deployment, provisioning or hosting decision is authorized.

## Approaches

Recommended: use existing `Users.NextDigestAt` as the durable due slot, existing
unique user/week digests as generated state, and authenticated encrypted receipts
as local capture evidence. Add a small Worker hosted service and an Application
schedule-store interface implemented in DataAccess. No new persistent table,
schema migration, export version or dependency is required.

A new durable job/attempt table could support external provider uncertainty and
history, but would require schema/export/deletion contracts and an actual transport
decision. A perpetual production timer or cron deployment would require Phase 9
hosting/lifecycle decisions. Neither is adopted by this local design.

## Exact local Worker contract proposed for approval

- Preserve existing explicit `reconcile`, `generate` and `capture` actions.
- Add `--action schedule --origin Synthetic`, requiring an explicit bounded
  allowlist through `Schedule:Users`. Reject empty/duplicate/invalid IDs and
  non-Synthetic origins. This selects disposable local accounts rather than
  silently enrolling every account in a test operation.
- Require Development and a loopback database, as the current Worker does.
- `Schedule:MaxUsersPerTick`: 1–100, default 10. `Schedule:MaxTicks`: 1–1000,
  default 1. `Schedule:IntervalSeconds`: 1–3600, default 60. All limits are
  configuration values; a process ends after its configured tick limit or
  cancellation. This is not an implicitly perpetual background deployment.
- Use a host-managed BackgroundService, a fresh DI scope per tick/job and
  `TimeProvider` for the clock/delay. Do not put scheduling logic in Presentation
  or call upstream APIs. Existing command handlers perform generation/capture.
- Return bounded per-job status/reason plus tick counts for operator inspection.
  Do not print email addresses, tokens, connection strings, MIME or secret-bearing
  URLs. User/digest IDs are local operational metadata and must be documented in
  the privacy inventory as possible terminal/log copies outside online erasure.

## UTC scheduling semantics

Preserve `DeliveryHourUtc` literally. `DeliveryDay` refers to the UTC weekday.
`TimeZone` remains the stored profile label; do not reinterpret the existing UTC
hour as wall-clock time or introduce daylight-saving rules without a new decision.

For an active eligible user with null `NextDigestAt`, initialize the next matching
UTC day/hour at or after the tick's captured UTC clock. Initialize once through
a conditional update. A future slot does not generate or capture early.

For an existing due `NextDigestAt`, derive the ISO week from that exact durable
UTC slot. Preserve that slot across failures and restarts. Process at most one
due week per account per tick. On verified capture, advance exactly seven days
from the claimed slot, rather than from the restart time. A missed backlog can
therefore progress only through explicitly bounded ticks, never an unbounded
catch-up loop or silent date coalescing. Generated inputs reflect the current
stored taste data; this does not reconstruct historical listening profiles.

Stop a user on invalid weekday/hour/non-UTC/inconsistent due-slot data and report
a bounded reason. Do not repair schedule settings silently. Existing one-shot
actions remain available to an operator; no new public schedule-settings endpoint
or enrollment field is added.

## Concurrency and state transitions

Acquire a dedicated database advisory lease for the scheduled tick, with a
different key/purpose from existing generation/account/copy leases. Use a dedicated
connection; release it on normal completion or cancellation. A second scheduler
returns `scheduler_busy` without advancing a slot. Connection/process loss releases
the session lease. Existing one-shot workers continue to use their existing locks.

Select a stable bounded order from the explicit allowlist, filtered by active
Identity and Domain account and absence of email opt-out. Recheck eligibility
before each job. Account deletion and unsubscribe retain their final-publication
row/account protections; the scheduled selection is not the final authorization.

1. Read the due slot, user preferences and current eligibility.
2. Generate/reuse the digest for that user and exact week using the existing
   synthetic command. A stopped generation leaves the slot due.
3. Capture/recover the encrypted local message using the existing capture command.
   Only authenticated `local_captured` / `already_captured` outcomes can advance.
4. Conditionally advance `NextDigestAt` where the user remains active/subscribed
   and the stored slot still equals the claimed slot. A deletion, opt-out or
   concurrent schedule change cannot be overwritten by a stale advance.

Do not hold a SQL row transaction across generation, rendering, file I/O or delay.
The scheduler lease serializes scheduling; the existing account and row leases
serialize publication/deletion/unsubscribe. Respect their established ordering.

No single SQL/filesystem transaction is claimed. If the process dies after copy
publication or digest completion but before schedule advancement, the slot remains
due. The next tick verifies the original receipt and advances without recreating
the message. A missing/conflicting completed receipt remains an integrity stop.

## Cancellation, retries and storage

Pass cancellation through query, generation, capture, advancement and delay. A
cancelled or failed job never advances merely because a digest row exists. Release
all owned leases, scopes and connections; a fresh process can retry the same slot.
Existing bounded generation/capture retries remain authoritative. A later tick
may retry a due failed job only within the explicit tick/account limits. Do not
invent provider success, recreate a conflicting receipt or create an endless
retry loop. Opt-out after already completed publication cannot retract that copy;
conditional advancement must report the late opt-out and future ticks skip it.

Preserve the temporary 1 GB stop, fresh inventory requirement and recorder/live
boundaries. The scheduler does not silently reconcile changed/unknown storage or
override `database_inventory_changed`; operator reconciliation remains explicit.
Initialization/advancement can change measured database size, so this conservative
stop and its recovery must be exercised rather than labelled validated capacity.

## Privacy accounting

No new stored scalar column is proposed. `NextDigestAt`, digest lifecycle/error
and score/copy data are already inventoried/exported and account-owned SQL deletion
already removes them. Extend 8a evidence for scheduled writes and terminal outputs.
The explicit user allowlist is operator-supplied configuration, not a new retained
subscription/taste table; its files/copies are outside online account deletion,
like the existing generation-use permission manifest. Do not claim those are erased.

## Verification and commit breakdown

Expected effort: three focused implementation/verification commit sets, followed
by an evidence commit. Existing package versions are retained. No quality,
production capacity or external delivery claim comes from synthetic tests.

1. Pure UTC slot/week policy and configuration validation: exact slot, future/null
   initialization, overdue slot, year/ISO-week boundary, bounded backlog and invalid
   parameters. Use fake clocks without sleeping.
2. Application/DataAccess scheduling store and Worker integration: actual PostgreSQL
   lease contention, active-account filtering, conditional initialization/advance,
   opt-out/deletion between selection and publication, and cancellation release.
   Keep score logic/provider calls inside existing abstractions.
3. Real local process/restart tests and runnable QA: two processes for one due slot,
   failure before generation, after generation, after file publication/SQL completion,
   and before schedule advance. Verify one digest/receipt per user/week and exact
   persisted due time across restart. Missing receipt and storage changes must stop.
4. Run 8b's dedicated checks separately. Then run the combined scenario:
   scheduled work → stored snapshots → capture/recovery → complete export → opt-out
   → suppressed later work → deletion/access invalidation/copy cleanup. Run the
   whole solution with local PostgreSQL enabled and record the three test outcomes
   separately, as the developer requested. Create focused local commits after
   verification; developer confirmation remains pending.

## Sources and review gates

Existing User schedule fields, ISO-week value object, LocalWorkerOptions/runner,
generation/capture stores and handlers, archive/account leases and 8a contracts
are the source boundaries. The host design follows Microsoft's
[hosted-service lifetime/scoping guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0).
The database lease follows PostgreSQL's
[session advisory-lock semantics](https://www.postgresql.org/docs/16/explicit-locking.html#ADVISORY-LOCKS).
Verify these APIs against installed packages before implementing; documentation
does not authorize an upgrade or prove the current implementation works.

Concrete written-spec review and the subsequent implementation-plan review are
required by the brainstorming workflow. The developer already selected sequential
execution and separate/combined testing; do not ask for that choice again.
