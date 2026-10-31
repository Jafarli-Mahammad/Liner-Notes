# Discovery operations and artwork recovery — 2026-10-31

Follow-up: [the final original-account investigation](FINAL_DISCOVERY_RELIABILITY.md)
reproduces the remaining receipt-limit failure and verifies the repaired original
account through a real browser request, refresh and application restart. The
findings below remain the record of the earlier investigation.

Local investigation only. Developer confirmation remains pending. Newly saved
results were verified below; the underlying exception of the historical reported
operation cannot be recovered from its generic persisted error.

## The reported operation

PostgreSQL, queried by the exact supplied ID, contains:

| Operation | Created, UTC | Last update, UTC | Final state | Attempts | Result |
|---|---|---|---|---|---|
| `32c4d24e-87a5-48ef-8b4b-4401c507786b` | Oct 30 21:30:02.381447 | Oct 30 21:31:36.150825 | Failed | 1 | `generation_unavailable`; no batch |

It did not remain Running indefinitely in persistence. The captured Running
response preceded a terminal failure roughly 94 seconds after submission. Other
later operations also failed with this generic code. Their states are not evidence
about this operation, and the older completed batch is not evidence of its success.

`DiscoveryJobService.ProcessAsync` previously converted every
`GenerationStoppedException` into `generation_unavailable`, without recording the
original stop code. The available host logs do not recover that exception. An
exact historical attribution to a particular provider, timeout or storage guard
would be speculation. Safe application stop codes and their code locations are
now retained for subsequent failures; exception payloads/provider bodies are not
logged.

## Execution, scheduling and recovery

The Presentation host owns this on-demand queue; `src/Worker` is the weekly email
composition root. `DiscoveryRuntime.RunJobs` enables both discovery consumers and
artwork consumers. Successful status GETs only read the job row and do not execute
work or renew its lease.

1. `SubmitDiscovery` validates inputs and enqueues within storage admission.
   `DiscoveryJobStore.EnqueueAsync` serializes submissions per profile, applies
   idempotency, unfinished-job exclusion and the 30-second submission cooldown.
2. `DiscoveryJobService.LaneAsync` runs one prepared lane and the configured
   acquisition lanes. `HasReadyAsync` precedes a coordinated `LeaseNextAsync`,
   which uses row locks and `SKIP LOCKED`.
3. `AcquireLease` persists Running/preparing, increments attempts and sets a
   token and lease expiry. Missing prepared evidence is deferred as
   `waiting_for_music`, releases ownership and refunds the handoff attempt.
4. `RunDiscoveryJob` reads the exact input revision, materializes taste, obtains
   complete candidate pools, hydrates and validates evidence, applies the unchanged
   scorer/exclusions, then calls `DiscoveryBatchStore.CompleteAsync` under write
   admission. No partial-source shortcut was added.
5. Completion checks ownership, input revision, freshness and recommendation
   history again. Batch rows, recommendations, stored score breakdowns and the
   operation's result batch ID commit together. Failed persistence rolls back.
6. Heartbeats renew ownership while work runs. Expired leases can be reclaimed;
   old tokens cannot publish. Work is bounded by attempt deadlines, retry count
   and the total job age. Lease/age recovery tests cover these boundaries.

`NextAttemptAtUtc` is the last dispatch eligibility time, not a running-attempt
deadline. `AcquireLease` and heartbeats intentionally leave it unchanged. While
Running, readiness depends on **lease expiry**, so an overdue eligibility timestamp
does not require another attempt. It is advanced on retry/defer. Regression coverage
now explicitly verifies that an overdue timestamp cannot bypass a renewed lease.

The page's `revision` versions **taste and feedback inputs**, not successful batch
count. A successful batch need not advance revision 21. Batch IDs, stored rows,
completion state and the latest-batch list establish generation. The cooldown is
computed from submission time and only controls accepting another request. It
does not pause an accepted operation or its retries.

## Confirmed retry defect and current source failure

`MetaBrainzRequestGate.UpdateAsync` reads shared, persisted provider pacing. When
the remaining delay exceeded 30 seconds, it threw
`GenerationStoppedException("upstream_backoff_active")`. The runner consequently
treated temporary backoff as terminal. A regression reproduces the path with two
independent gate instances and a persisted two-minute backoff; it failed before
the fix.

The gate now raises a retryable `DiscoverySourceException` carrying the remaining
delay. The runner persists a retry no earlier than that delay, bounded by the
existing ten-minute retry-delay/total-age rules. The worker regression verifies
the actual Retryable state and future eligibility rather than only an HTTP code.
Artwork acquisition also preserves this transient classification.

Separate probes of the original profile inputs encountered `ArtistSearch`
`upstream_unavailable` responses. `ListenBrainzApiClient` maps HTTP 5xx to that
gap; `SharedSeedPreparation`/`DiscoverySourceValidation` reject it as a retryable
source failure. Diagnostic operation `45a470e0-c128-4701-9186-f9f0888918c3`, submitted
through the application with unchanged revision 21 and no taste edits, exhausted
three attempts with `source_temporarily_unavailable` and no batch. This was **not a
browser click** and was not one of the successful operations below. Its failure
remains a live provider limitation. It does not establish which stop exception
caused the historical operation's one-attempt failure.

## Artwork: reproduced cause and recovery

For `e0ccb2c6-f4cb-42ca-996d-ae3438525343`, direct local requests reproduced
`503/pending` followed by `404/unavailable` two seconds later. ListenBrainz's public
artwork metadata endpoint returned HTTP 200 with a release and CAA release ID.
The local catalog contained no row for the recording, while the separate public
feed still retained an expired Public entry for it.

The exact local path was:

1. `ArtworkController.Read` calls `RecordingArtwork.Request` on a cache miss.
   Request enqueues a priority item and returns Pending without an upstream call.
2. `ArtworkWarmupService.PrepareAsync` restores retained images, checks fresh
   public catalog eligibility and asks `ArtworkPublicRevalidation` to refresh
   stale public identities.
3. `ArtworkCatalog.PreviouslyPublicAsync` previously checked only catalog rows.
   Once catalog cleanup reclaimed them, the surviving public-feed proof was
   ignored. No public revalidation or image acquisition could run for this ID.
4. Preparation produced no acquisition plan; queue completion installed a
   one-minute negative recording entry. Request then returned Unavailable.

Previously-public lookup now includes retained Public feed identities. A second
recovery gap was verified in `DiscoveryBatchStore.CompleteAsync`: each new batch
deleted expired feed rows, erasing this remaining proof. Completion now clears
expired public titles, artists and attribution while retaining only identity,
origin, sequence and expiry within the existing 1,000-row bound. Expired synthetic
rows are removed. Fresh eligible publication can repopulate metadata without
moving a repeated recording's feed position. Reads still hide expired rows.

The worker still requires a fresh, validated, cacheable CC0 catalog publication
before obtaining artwork. Arbitrary IDs and private recommendation snapshots do
not authorize upstream acquisition. Revalidation attempts remain bounded to one
per minute and provenance is checked again after the network wait. Identities
already erased before this change cannot be inferred from private history.

After the lookup fix, the reported ID reached `200/ready`, returned a valid 500×500
JPEG and created a persistent Artwork cache entry. That verifies actual image
acquisition and persistence, separately from placeholder behavior.

The original completion cleanup erased this ID's feed proof during the first
browser tests, before the cleanup correction was loaded. A one-off local repair
restored only the canonical ID and expired timestamp independently observed in
that public table earlier in this investigation, with empty metadata, through
managed write admission. The normal worker then revalidated it again. This was
not a private-history backfill or a fabricated public catalog publication.

Other state rules checked:

- Recording negatives last one minute; after expiry a request may enqueue again
  and become Pending. They are not permanently persisted unavailable states.
- CAA release-level misses/invalid images have a bounded six-hour negative cache.
  Successful image retention is additionally capped by fresh recording metadata
  and upstream cache policy. Non-cacheable images are not forwarded as a bypass.
- Artwork metadata omitted `request_timeout` from its transient list. It now
  remains Pending with a retry delay, like rate limits and transport failures.
- The React component stopped its current polling loop after 404 but restarted
  it on visibility changes because only successful images marked the ID settled.
  Terminal responses now settle the mounted ID; a missing source no longer
  renders an image element. The regression scrolls a 404 card away and back and
  verifies one HEAD and an unavailable fallback.

## Real browser and persistence evidence

Playwright MCP's configured Chrome path was missing. Verification used a local
Playwright MCP server with the installed Chromium executable, against the actual
ASP.NET host at `http://localhost:5080`. No API routes were mocked for these runs.
A disposable guest selected the real jazz style through the UI.

| Browser submission | Exact operation ID | Persisted result batch | Result |
|---|---|---|---|
| First, with no saved batches | `b0f59694-4606-4124-be05-9a080a83fec1` | `e57c5026-409e-439f-8d71-f9d97b93adc4` | Completed, five new recommendations |
| Second, while the first batch was displayed | `116a18f2-c6cb-49a4-b55f-721f0b25021e` | `412b588b-406c-4261-a85b-319ce1ff4137` | Completed, five new recommendations replacing the first five |
| Final backend, after adding the electronic style through the UI | `b94ed6b8-242e-4d97-8330-d8ff7948ef1d` | `e8856035-f531-46d1-b29d-49ca0dca8de3` | Completed, five new recommendations; displayed titles match the persisted batch |

All three POSTs returned 202 with their captured operation IDs. PostgreSQL independently
confirmed five recommendation rows with stored score breakdowns for each batch.
Browser batch reads matched the displayed titles; the second page's latest batch
was the second operation's result, with no displayed error. Revision remained 1
because taste was saved once and generation does not change input revision.
The final request used revision 2 after the explicit taste change.

The unrelated catalog lookup was operation
`1a9bddc3-1e4d-4dde-b618-b1666691b480`; it completed after two attempts and was not
used as evidence of recommendation generation.

## Verification and limits

Targeted verification covers provider transport/backoff, artwork processing,
PostgreSQL ownership/publication/cleanup, lease behavior, worker retry scheduling
and React recovery. Build and browser regression results are recorded with the
task's final status. Diagnostics captured under `/tmp/liner-*` are local evidence,
not portable committed artifacts.

Passed targeted checks: 68 Infrastructure, 11 Presentation, 52 PostgreSQL
coordination/loading, five Domain lease tests, and four Playwright recovery tests.
The enhanced cleanup/metadata-refresh test was rerun separately after its final
assertions were added and passed. The frontend and Presentation host builds passed.

Two successful live guest batches do not establish that the original profile's
currently failing upstream searches work. The historical generic exception remains
unrecoverable. No scorer, provider selection, candidate limits, secrets, dependency
versions, deployment or upstream licensing policy was changed. Developer
confirmation and original-profile provider recovery remain outstanding.
