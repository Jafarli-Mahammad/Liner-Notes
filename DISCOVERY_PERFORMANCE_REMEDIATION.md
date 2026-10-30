# Discovery, artwork and performance remediation

Storage/authentication implementation update, 2026-10-30: [architecture, guarantees, 839 backend tests, browser checks, matched measurements and migration instructions](STORAGE_AUTH_REDESIGN_IMPLEMENTATION.md). The original findings below remain historical evidence; the tracked register incorporates the newly verified status.
Local verification: 2026-10-29. Developer confirmation and production acceptance remain pending. Baseline: commit `623f29a`; safe changes build on the October 27 fixes rather than replacing their design.

**Result:** the saturated artwork queue is measurably faster; discovery failure/recovery and cache correctness are improved. Matched synthetic cold/warm discovery timings do **not** establish an overall discovery speedup. Storage scan/coordination costs remain. The [unified storage/authentication proposal](STORAGE_INTEGRITY_DESIGN_PROPOSAL.md) is for review; its architectural implementation has not been applied.

## A. Problems corrected

| Root cause | Implementation | Evidence |
| --- | --- | --- |
| A source could fail temporarily after returning some candidates, allowing a reduced pool to be cached/ranked as success. | Reject failed acquisition before shared cache publication and before ranking. Retry temporary failures; fail configuration/malformed-source errors clearly. Advance prepared pool/seed namespaces to invalidate old partial entries. | Partial-source red/green cases, persistent/shared cache retry tests, full serialized-output contract. No candidate/seed/scoring shortcuts. |
| Fixed hash stripes serialized unrelated preparation keys/releases. | Refcounted exact-key gates, bounded to 32 active keys; cancellation releases references. Same-key work coalesces without false hash collisions. | Cancel/overflow/reuse tests; deliberately colliding distinct keys/releases progress independently. |
| Overlapping metadata calls dispatched duplicates before the first response arrived. | Bound and coordinate same-key requests across scoped clients. Reuse only a completed, fresh response permitted in the public cache. Independent cancellation and dispatch budgets remain intact. | Eight overlapping callers cause one cacheable HTTP dispatch; no-store/no-cache/private responses cause independent dispatches; a cancelled owner does not cancel its waiter. Separate hosts can still duplicate cold metadata. |
| Whole artwork batches held image lanes, so a visible cover waited behind an unrelated backlog. | Two metadata preparers retain batches of eight; two acquisition workers take individual prepared images. Priority survives the handoff. Both stages provide maintenance progress after seven demanded items. Capacity 256 includes queued, preparing, prepared and acquiring work. | Prepared promotion/fairness tests; saturated 16-cover worker test; matched 20-sample queue benchmark below. All images complete with peak two downloads. |
| Expired public catalog eligibility prevented a legitimate refresh. | Bounded revalidation consults retained public/CC0 catalog provenance only, fetches fixed public metadata endpoints, validates fresh identities/artist core metadata and publishes through existing admission. Eligibility is checked again before image acquisition. | Six revalidation cases; PostgreSQL distinguishes current/expired public recordings from synthetic/arbitrary IDs. Missing proof, malformed/non-retainable data or failed revalidation never grants acquisition. |
| A restricted image was retained for a one-second shared handoff; malformed images could remain pending. | Never retain/reuse no-store/private/no-cache or expired image bytes. Reject invalid types/signatures/size/redirects and permanent failures; retain transient backoff and existing release/release-group fallback. | Restricted-policy, freshness, validation, fallback and negative-cache tests. No image proxy is introduced. |
| A successful HEAD followed by failed GET/expiry stopped recovery, or returned a successful placeholder. | Ready GET returns actual image; pending/unavailable return 503/404 with explicit state. Mark loaded only after image load succeeds. Retry failed retrieval and respect negative-cache Retry-After. | Controller tests and browser GET-failure/HEAD→GET expiry cases. No permanent success-shaped placeholder. |
| Visibility changes reset deadlines or exhausted them before first demand. | Start the ten-minute artwork recovery budget at first visible demand; preserve it across visibility changes and clear failed image state while hidden. Legacy code tracks successful loads and accepts legitimate 80px images. | Offscreen/hidden/re-entry browser cases and the existing Node/VM artwork check. |
| Transient database/session errors became generic failures and could discard a valid sign-in. | Sanitized database 503/Retry-After; temporary refresh failure retains in-memory credentials. Permanent rejection still signs out. Logs omit exception payloads/private provider data. | Direct/wrapped database failure tests, browser refresh/retrieval recovery tests. A transport outage itself is not claimed fixed. |
| Metadata/static transfers lacked compression. | Metadata handler negotiates gzip/deflate/Brotli with the existing decoded size cap. Compress only a terminal public-static branch; APIs and SPA HTML stay outside it. | Six production-handler loopback cases; decoded-byte equality, size cap, ETag/304, private-response exclusion and routing tests. Real static transfer measurements below. |
| Weekly input revision hashing allocated the complete serialized JSON bytes. | Stream the same serialization into SHA-256 instead of materializing an additional whole-revision byte array. Full feedback/history remains intact. | PostgreSQL test compares against the original byte-array algorithm with nonempty feedback; existing revision/change/rollback tests pass. No total-export-memory improvement is claimed. |
| Latency lacked useful stage attribution; profiler configuration was incompatible with installed tools. | Fixed-label stage/cache metrics and optional activities cover preparation, provider gates/dispatch, hydration, ranking, database admission/hold/inventory, artifact scans, job age, artwork queues/acquisition/revalidation and metadata coalescing. Correct launch-compatible sampling profile and output-path checks. | Instrumentation parentage/privacy/disabled-overhead tests, launch traces/counters and successful Speedscope conversion. No automatic telemetry persistence/exporter. |

### Preserved boundaries

The ten-minute total discovery age includes queueing/preparation; expired workers remain fenced and cannot publish. A timeout never silently becomes a reduced recommendation pool. Existing pacing, provider deferrals, request budgets, evidence freshness and shared acquisition leases remain. Domain/scorer weights, full candidate/seed coverage, supporting evidence and exclusions are unchanged.

Artwork remains public-cache-only with HTTPS/redirect allowlists, signature/size checks, checksums, bounded storage/memory and existing licensing boundaries. Previously public proof must still exist: expired records already purged by normal cleanup cannot be reconstructed from private recommendation history. Revalidation attempts are serialized per host, batch-limited and suppressed for one minute after unsuccessful attempts; temporary provider failures remain retryable. Already approved current metadata can be reused within its freshness limit. Only core metadata is projected; provider tags/private taste data do not enter the catalog refresh artifact.

Public metadata license assumptions were rechecked against [MusicBrainz's data license](https://musicbrainz.org/doc/About/Data_License). That license does not make album-cover copyrights CC0. Existing artwork restrictions remain. Non-cacheable image delivery is unchanged by the developer's decision; unavailable is an honest result. Cache-control handling follows [RFC 9111](https://www.rfc-editor.org/rfc/rfc9111.html).

## B. Verification and measurements

### Tests

| Verification | Result |
| --- | --- |
| Domain | 151 passed |
| Application | 147 passed |
| Infrastructure | 249 passed |
| DataAccess, including disposable PostgreSQL integration | 136 passed |
| Presentation | 95 passed |
| Total backend | **778 passed, zero failed or skipped** |
| Browser desktop/mobile and failure/recovery journeys | **55 passed** |
| Frontend production build and TypeScript check | Passed |
| Existing legacy artwork VM check, JS/shell/Python syntax checks | Passed |
| Standalone diagnostic probe Release build | Passed |

The first browser attempt failed to launch because Playwright's expected browser executable was absent. No product assertion was bypassed or skipped: all 55 checks passed on the full rerun using the already-installed Chromium via the existing `LINER_BROWSER_EXECUTABLE` option. Backend verification uses SDK 10.0.401 and runtime 10.0.12. PostgreSQL tests use only disposable databases in the task-owned container. The container's loopback port changed after restart; the private test configuration was refreshed, then the complete PostgreSQL suite passed.

Covered failures include temporary partial acquisition, 429/503, slow/failed requests, malformed/oversized metadata, sparse evidence, cancellation, cache expiry/eviction, job age/lease replacement, stale inputs, failed writes/storage pressure, image fallback/validation/backpressure/persisted restoration, retrieval after completion, temporary refresh errors, image GET/HEAD races, hidden/offscreen behavior and desktop/mobile widths. Existing process-recovery tests cover scheduled/job execution. A separate two-process probe persists a generated PNG through coordinated PostgreSQL storage, exits, then restores the identical 68 bytes into a fresh memory cache in another process with all provider methods configured to throw. It passed with zero provider calls; this establishes fresh-process restoration, not every possible crash during acquisition. Simultaneous independent-host artwork saturation and a crash matrix for the proposed storage protocol are not claimed covered.

### Exact recommendation compatibility

Eight existing genre fixtures, acquisition enabled/disabled, fixed clock/IDs: **16 scenarios, 80 picks, 364,769 serialized bytes identical** between baseline and changed preparation. The comparison includes complete results, scores/order, provenance, gaps, exclusions, coverage, reservation and expiry behavior.

SHA-256: `6b8e6dd62d50a4ce6060a333ef7a2867879bd09f7077b29bd017fc596135faef`.

The compatibility test passes in the final Application suite. Intentional failure behavior differs: temporary failed acquisition is retried rather than being called a successful incomplete generation. This is not a real-provider taste-fit or listening-quality evaluation. Recommendation shortcuts remain rejected.

### Matched local end-to-end runs

Same frozen baseline/current probe helper, fixtures, generated users, database template, SDK/runtime, server GC, host resources, artifact shape and 250 ms result polling. Cold samples restart the host/reset public evidence; warm/large/concurrent samples reuse appropriate evidence. Both versions completed all 50 jobs and returned every result with HTTP 200. There were no live-provider HTTP calls in these synthetic runs.

| Scenario | Samples/version | Before p50 / p95 | After p50 / p95 |
| --- | ---: | ---: | ---: |
| Cold discovery | 5 | 3,531 / 3,640 ms | 3,569 / 3,773 ms |
| Warm discovery | 20 | 1,092 / 1,110 ms | 1,092 / 1,122 ms |
| Ten-tag profile | 5 | 1,101 / 2,900 ms | 1,107 / 2,895 ms |
| Four simultaneous requests | 20 | 1,216 / 1,390 ms | 1,212 / 1,488 ms |

No overall discovery latency improvement is demonstrated. Five-sample tails are descriptive maxima, not robust population p95 estimates. Queue polling, fixture execution and empty-artifact storage do not predict real-provider or large-storage timings. Earlier exploratory workstation-GC runs and an interrupted transport-error run are excluded from these matched results.

In the final changed warm/concurrent host, stage medians included about 775 ms job age at dispatch, 88 ms job execution, 59 ms coordinated persistence, 49 ms database lease hold, 8 ms inventory read and 0.17 ms artifact scan with empty fixture roots. These are instrumented internal stages, not additive totals or before/after deltas. Cache-hit preparation is substantially different from live cold provider acquisition.

The final sustained host portion used approximately 18.45 CPU seconds/278.1 MiB peak working set before and 19.14 seconds/278.7 MiB after. No CPU/RSS improvement or leak conclusion is claimed. CPU, GC and page-cache behavior on an inexpensive VPS still need deployment-specific measurement. Existing thread samples include waits and are not presented as exclusive CPU attribution; no production process was attached and no memory dump was collected.

### Artwork and static transfer

For 16 queued 250 ms fixture images followed by one visible 10 ms image, 20 samples per version:

| Measure | Before | After |
| --- | ---: | ---: |
| Visible artwork ready p50 | 994.4 ms | **240.4 ms** |
| Visible artwork ready p95 | 997.6 ms | **242.0 ms** |
| Completed downloads per sample | 17 | 17 |
| Peak simultaneous downloads | 2 | 2 |

This isolates queue scheduling with controlled metadata/HTTP and no persistent-store cost. It does not predict CAA latency. A discarded intermediate two-batch-worker approach performed worse under this saturated case and was replaced by the staged per-image scheduler; the final numbers compare against the original baseline.

Twenty main-JavaScript requests per version transferred 336,730 bytes before versus 149,086 bytes after gzip negotiation; decoded content SHA-256 and length were identical. These wire bytes differ from Vite's compressed-size estimate because the server uses its configured fast compression level.

### Storage, SQL and allocation findings

Five complete content-validation passes remain. Original controlled medians were 537 ms for 100 MiB/one file, 3,144 ms for 512 MiB/one file and 765 ms for 100 MiB/1,000 files. Warm page-cache reads still consume CPU and hold coordination even with zero measured physical disk reads. The standalone proposal experiments compare buffered/parallel **full reads**, exact insert accounting/rollback, and Identity hashing; they do not implement a replacement. See the [proposal](STORAGE_INTEGRITY_DESIGN_PROPOSAL.md) and [numeric results](diagnostics/proposals/measurements.json).

Large-history baseline observations remain material: at 10,000 feedback records the weekly input read allocated about 231 MB per operation, and a complete export about 243 MB, with an approximately 53 MB JSON result in the fixture. Streaming the revision hash removes one additional byte-array materialization but does not solve those aggregate graphs. At 50,000 cache rows, quota aggregates measured about 14 ms median/17.5 ms p95 locally. An idle worker observation issued about 50 SQL commands in ten seconds. These are retained costs, not improvements claimed by this change.

Sanitized matched results are in [the numeric summary](diagnostics/remediation-2026-10-29-summary.json). Raw local traces, samples, TRX files and failure logs remain under the ignored `diagnostics/results/remediation-20261027/` evidence directory; they are not production telemetry.

## C. Original findings ledger

`Fixed` means the identified local mechanism has passing reproduction/regression evidence; it does not mean production or developer acceptance. `Mitigated` means bounded/improved with a remaining limit. Earlier committed corrections are identified as reproduced rather than credited as new optimizations.

| Finding | Disposition | Evidence / remaining scope |
| --- | --- | --- |
| D1 acquisition lanes | Mitigated | Existing bounded lanes/ownership tests reproduced; four-caller runs pass. Independent-host fairness remains unmeasured. |
| D2 sequential preparation | Mitigated | Existing bounded overlap retained; full output contract passes. No speculative parallel stateful taste changes. |
| D3 provider pacing | Mitigated | Pacing/deferral tests retained; duplicate cacheable dispatch reduced. Upstream gate latency is unavoidable. |
| D4 timeouts/retries | Mitigated | Cancellation/transport/age cases pass; interrupted acquisitions are not reused as partial success. Live outage distributions remain unknown. |
| D5 indefinite deferral | Fixed | Prior ten-minute expiry/fencing reproduced, including queued/deferred jobs. |
| D6 cache effectiveness | Mitigated | Public pool/seed reuse and policy-aware request coordination; expiry/no-store and independent-host cold misses remain. |
| D7 redundant artist evidence | Deferred | Developer rejects skipping supporting evidence without representative quality proof. |
| D8 storage coordination | Unresolved | Full reads and shared lock measured; unified proposal requires review. |
| D9 sparse/invalid evidence | Fixed | Failed partial acquisition cannot become successful cached/ranked output; genuine sparse evidence stays honest. |
| D10 HTTP/poll recovery | Fixed | Timeout/permanent-error/visibility/session recovery regression checks pass. |
| D11 completed-result retrieval | Fixed | Independent result-retrieval recovery/browser test passes; no resubmission required. |
| D12 progress/error attribution | Mitigated | Fixed-label stages, safe job correlation and clear errors; no automatic browser telemetry or exporter. |
| A1 artwork admission | Mitigated | Public eligibility and retained-public revalidation pass; purged proof remains unavailable. |
| A2 artwork batch blocking | Fixed | Per-image scheduling and exact release gates; 20 matched saturated samples complete all downloads. |
| A3 maintenance starvation | Fixed | Demand/maintenance fairness at both stages, promotion and bounded queue tests. |
| A4 browser recovery | Mitigated | GET/visibility/offscreen recovery fixed; intentional ten-minute recovery limit remains. |
| A5 artwork cache/persistence | Mitigated | Separate quotas, checksums, restoration and bounded concurrency pass. Multi-host cold duplication remains possible. |
| A6 negative cache/backoff | Fixed | Permanent/transient distinctions, release negatives and remaining Retry-After tested. |
| A7 fallback | Fixed | Primary release → distinct CAA release → release group tested with redirect/security constraints. |
| A8 validity/rendering | Mitigated | Headers/signatures/size/freshness plus browser decoding recovery. Header checks are not a full server image decoder. |
| A9 readiness HTTP load | Mitigated | Honest 503/404/200 contract, negative-wait-aware HEAD and GET recovery; concurrent live-user network load not established. |
| A10 artwork diagnostics | Mitigated | Queue/preparation/image/acquisition/revalidation measurements; no provider-sensitive payload logs. |
| P01 artifact scans | Mitigated | Five integrity checkpoints retained; live kernel proofs avoid redundant full reads for eligible large files, and initial verification leaves the DB gate. Forced fallback and 900 MB distributions are in the storage/auth implementation record. |
| P02 duplicate metadata | Mitigated | Same-process overlapping cacheable requests share; restricted responses and independent hosts intentionally do not share unsupported data. |
| P03 response allocation | Mitigated | Existing bounded response handling retained; decoded cap/compression tested. No new allocation-reduction claim for the old optimization. |
| P04 retry timing | Fixed | Current React and legacy browser recovery/backoff tests pass. |
| P05 stripe contention | Fixed | Exact-key bounded gates remove unrelated-key collisions, with cancellation and overflow coverage. |
| P06 full export memory | Unresolved | Stable bounded paging already exists but the complete DTO remains materialized. Streaming needs ownership/consistency/failure semantics preserved. |
| P07 idle polling | Unresolved | Bounded existing SQL polling retained; reducing it without harming cross-host responsiveness needs further evidence. |
| P08 snapshot allocation | Mitigated | Prior scorer changes retained, current bounded compute measured and exact outputs equal. No new scorer optimization. |
| P09 feedback/revision reads | Mitigated | Revision hashing streamed with exact digest compatibility; complete feedback graph cost remains. |
| P10 alias merge | Mitigated | Existing bounded merge/output behavior retained; no changed merge quality rule or speedup claimed. |
| P11 catalog read fan-out | Mitigated | Existing batched catalog behavior/integration tests retained; no blanket query-elimination claim. |
| P12 cache quota aggregates | Unresolved | 50k-row query cost measured; do not cache approximate totals or weaken coordinated quota admission. |
| P13 archive scanning | Mitigated | Known owned copies delete in bounded journal batches with restart recovery and fewer integrity admissions. The authenticated legacy/unlinked-copy scan remains necessary, and large archive read costs remain. |
| P14 pool encode/decode | Mitigated | Prior serialization optimization retained; exact validated projection/output coverage remains. |
| P15 auth admission | Fixed locally | Standard Identity verification/rehash prepares outside broad admission; locked current-state fencing, parallel lockout, reset/stamp/session races and storage-pressure tests pass. Small-workload coordination overhead remains. |
| P16 artist supporting evidence | Deferred | Same rejected shortcut/controlled quality-evaluation gate as D7. |
| P17 account queries | Mitigated | Existing account eligibility behavior/tests retained; large exports are still separate work. |
| P18 compression | Fixed | Production-handler/static negotiation, security boundaries and identical decoded bytes verified. |
| P19 ledger trigger overhead | Mitigated | Exact statement accounting, complete coverage/fingerprint checks and deferred commit witnesses pass rollback/reconciliation tests. Additional witness SQL raises empty-storage sequential latency; no universal DB-write speedup is claimed. |
| P20 scheduled/capture publication | Fixed locally | Durable reservations/staging/intent/finalization, process-kill restart tests, scheduled capture and interrupted account-copy cleanup pass. PostgreSQL commit and rename remain distinct recoverable states. |

## D. Remaining decisions and operations

- The developer approved the unified storage/authentication implementation. See [the locally verified implementation record](STORAGE_AUTH_REDESIGN_IMPLEMENTATION.md) for selected guarantees, test/failure evidence, measurements, remaining costs and operational requirements. The earlier proposal is historical.
- Keep non-cacheable images unavailable. Keep all recommendation coverage, deterministic scoring, full-pool ranking and supporting evidence. No proxy or recommendation shortcut was introduced.
- Large export/history/archive graphs remain. Further safe projection/streaming/index work is authorized, but a replacement must prove identical ownership, retained data, snapshot/failure semantics and managed accounting. No truncation or partial export is substituted as success.
- The earlier discovery/artwork safe fixes required no migration. The storage/authentication redesign requires three migrations and explicit stopped-writer establishment; follow its implementation record. No new external service, production dependency or provider/scoring configuration was introduced, and nothing was deployed.
- Pool/seed namespaces become `prepared-pool-v3`/`prepared-seed-v2`. Expect bounded cold preparation after upgrade; prior completed discoveries remain readable. Restart participating application instances and preserve the existing shared MetaBrainz pacing directory for a shared upstream IP.
- Rebuild/publish frontend assets through the existing production process. Artwork consumers should respect X-Artwork-State, HTTP 503/404/200 and Retry-After. CSP/private cache boundaries remain unchanged.
- Monitor queue/attempt age, provider waits/cache hits, database wait/hold, inventory/scans and artwork queue/acquisition stages in bounded sessions. Do not add user IDs/taste text/tokens as metric labels. No telemetry export is automatically enabled.
- No sustained real-provider distribution, target-VPS capacity guarantee, simultaneous independent-host saturation test, exhaustive external-filesystem race proof or sensitive process dump is claimed. Local fixture equivalence is not perceived recommendation-quality validation.
