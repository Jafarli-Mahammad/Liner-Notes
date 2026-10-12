# Phase 8b local scheduling QA

The approved scheduler is implemented and locally verified; developer confirmation
remains pending. Use the [existing launcher](phase-7-local-run.md) with a disposable
`liner_notes_manual_qa` database and fabricated account. Register the account, save
fixture tags, and enter its subscriber GUID and an ISO week in the launcher.

1. Choose `schedule-setup` and enter a whole-hour UTC due time, for example
   `2026-10-12T10:00:00Z`. Choose an hour at or before the current UTC time to
   exercise due work. This updates the disposable account's delivery weekday/hour
   and `NextDigestAt`; it preserves its TimeZone label.
2. Choose `reconcile`, then `schedule`. One bounded tick generates the exact due
   week's digest. Capture stops with `database_inventory_changed` because generation
   changes accounting. The due slot remains unchanged.
3. Choose `reconcile` explicitly, then `schedule` again. Expect `advanced`,
   `local_captured`, and `NextDigestAt` exactly seven days after the claimed due
   time. New physical database allocations can require another explicit
   reconciliation; a changed-inventory stop never permits automatic advancement.
4. Choose `schedule` again. With a future next slot its job list is empty. Inspect
   the export: version 2.2, stored snapshots, advanced `NextDigestAt`, sanitized
   local copy, `LocalCaptured` and null `SentAt`.
5. Confirm unsubscribe through the existing local link flow. Scheduling skips the
   opted-out account. Delete it through the page and verify protected access is
   denied and configured owned copies are removed.

The launcher's supplied ISO week selects one-shot inspection actions; the scheduler
derives its week from `NextDigestAt`. Use the matching week when inspecting a scheduled
digest. Fixture setup requires Development, a loopback database named exactly
`liner_notes_manual_qa`, a whole UTC hour and an active subscribed account. It takes
the scheduler/account leases and performs a conditional update. It creates no public
settings endpoint or automatic enrollment.

With the same database, private sink, keys and all six configured storage roots:

```bash
dotnet run --file scripts/phase8_schedule_fixture.cs --no-cache -- \
  --user YOUR_DISPOSABLE_ACCOUNT_GUID --due 2026-10-12T10:00:00Z
dotnet run --project src/Worker/Worker.csproj -- --action schedule --origin Synthetic \
  --Schedule:Users:0 YOUR_DISPOSABLE_ACCOUNT_GUID
```

The schedule action rejects legacy `--user`/`--week` selectors and Recorded/Live
origins. It requires 1–100 unique nonempty account GUIDs. Defaults are one tick,
ten users per tick and a 60-second interval. Optional `--Schedule:MaxTicks` (1–1000),
`--Schedule:MaxUsersPerTick` (1–100) and `--Schedule:IntervalSeconds` (1–3600) remain
bounded. At most one due week per account is processed per tick. Backlogs preserve
the original slots; they do not reconstruct historical taste profiles.

Exit 0 means the bounded run completed; 2 means stopped/busy; 130 means cancelled.
Result JSON contains account IDs, due slots, weeks and bounded reasons, with no email
addresses or capabilities. Operator allowlists and terminal copies are outside online
account erasure. UTC day/hour are literal; TimeZone causes no daylight-saving conversion.

For retained restart testing, keep the exact keys/database/sink/configuration. The
launcher removes its temporary sink on exit. Automated process tests use disposable
retained fixtures and controlled PostgreSQL barriers; see [evidence](phase-8b-evidence.md).
Missing/conflicting completed receipts stop rather than create new messages.
No external email, live acquisition or production deployment is exercised.
