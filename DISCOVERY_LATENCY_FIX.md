# Discovery latency fix — 2026-10-31

Locally verified in the `bf60` worktree; developer confirmation and deployment remain outstanding.

## Browser results

Playwright MCP drove the actual React application on port 5087, using disposable accounts and loopback PostgreSQL databases. The existing application on 5080 and real accounts/caches were left alone. The configured MCP browser lacked Chrome; a separate MCP session used the already-installed Chromium. Waterfox was also tried at the developer's request, but exited when launched with Playwright's Firefox automation protocol.

The profile used **indie, electronic, jazz and rock**, with exploration set to **close**. Each seed still requests ten candidates; all available metadata, evidence and full-pool ranking are retained. Timing starts at the browser click and ends when the new batch's five titles are rendered. Network records include submission, status transitions and result retrieval. Result observers identify the new batch rather than counting previously displayed cards.

| Run | Click to result | Outcome |
| --- | ---: | --- |
| Original, two-tag initial reproduction | 16.664 s | Five recommendations; different workload, not the comparison baseline |
| Original, four-tag profile | 110.553 s | Failed after three acquisition attempts; no new recommendations |
| Longer timeout and shorter handoff/polling, still two pools | 35.460 s | Cold, five recommendations on first attempt |
| Final, four pools, fresh database and empty caches | **23.427 s** | Five recommendations on first attempt |
| Final, same profile repeated in the same process | **21.241 s** | Five recommendations on first attempt; **zero evidence-cache hits** |

These are individual live-provider observations, not a matched-response benchmark or a percentage speedup guarantee. Radio responses vary between calls. The intermediate run established a scheduling bottleneck, not a separate release.

## Verified causes and changes

1. **The 15-second HTTP timeout discarded valid slow radio work.** The original four-tag run hit that timeout three times. A gate-respecting diagnostic request returned valid `rock` radio data in **17.486 s**; final browser tracing also observed a successful **17.661 s** provider request. The ListenBrainz HTTP timeout is now **30 seconds**. Existing 45-second pool deadlines, request/size budgets, cancellation, job age and retry limits remain in force.
2. **Only two independent pools could progress.** In the intermediate cold run the final pool started **14.349 s** after acquisition began, then occupied **19.336 s**. Bounded overlap is now four. In the final cold trace all four pools start **5–7 ms** after acquisition begins. The shared provider gate still spaces dispatches and honors backoff; neither candidate windows nor request budgets change.
3. **Avoidable local handoff and observation waits.** A first cache miss no longer inserts a one-second retry delay before acquisition. A genuinely busy shared preparation retains its backoff. Discover polls every second, honors larger `Retry-After` deadlines, pauses while hidden, and stops on completion or permanent errors. Idle worker polling remains unchanged.

## Backend measurements

Existing `LinerNotes.Discovery` activities/meters, HTTP activities and Npgsql activities were collected in a diagnostic launcher around the real composition root. `dotnet-trace` launched the owned processes and retained sampled-thread traces before and after. Samples are dominated by waits; their percentages are not exclusive CPU attribution. No production process was attached to and no memory dump was collected.

| Stage | Final cold | Final repeat |
| --- | ---: | ---: |
| Submission HTTP response | 137.3 ms | 47.5 ms |
| Click → first worker dispatch, including submission | 828.9 ms | 445.7 ms |
| Prepared worker execution | 117.8 ms | 39.2 ms |
| Prepared → acquisition handoff gap | 938.8 ms | 31.9 ms |
| Acquisition worker execution | 20,919.1 ms | 20,258.4 ms |
| Longest public preparation pool | 20,716.6 ms | 20,196.4 ms |
| Provider dispatch/body/parse on that pool's path | 18,708.6 ms | 18,359.4 ms |
| Rate-limit gate waits on that path | 1,590.6 ms | 1,519.7 ms |
| Evidence hydration on that path, nested in preparation/provider work | 1,324.9 ms | 1,043.4 ms |
| Taste preparation in acquisition | 1.8 ms | 0.1 ms |
| Scoring/ranking | 13.6 ms | 0.8 ms |
| Acquisition completion → visible new batch | 622.7 ms | 466.0 ms |

Submission is included in dispatch latency; nested preparation/provider stages must not be added together. The original failed run spent **89.234 s** in worker executions across its three attempts and **21.319 s** outside them in submission, queue/retry waits and completion observation.

Final cold acquisition issued 580 PostgreSQL commands totaling **1,290.3 ms** of aggregate command time. Coordinated persistence totaled **1,741.0 ms**, including those database operations; artifact scans totaled **74.9 ms**. These aggregates overlap across pools and are not additive critical-path durations. The diagnostic artifact roots contained no payload files requiring content hashing. This does not measure large-storage performance, and no integrity/accounting code changed.

The final cold batch ranked **33 hydrated candidates** from the complete four requested windows. Missing metadata and artist-tag observations remain recorded as coverage gaps; five picks were supported. The changes do not return the first five usable candidates.

## Cache limitation and remaining wait

The repeat is **not** a warm-cache speedup. Its cache counters show misses throughout. A separate policy probe received HTTP 200 with **`Cache-Control: private`** from `/1/lb-radio/tags`, both with and without authentication; `SharedCacheUntilUtc` was null. The current shared cache must reject those responses. A genuine shared warm-cache comparison for this live path is therefore externally blocked. No private response was retained to manufacture a warm result. Cacheable-response reuse, expiry, cancellation and non-retainable-response behavior remain covered by automated tests.

The longest final radio response still takes about eighteen seconds. Further improvements to that upstream work cannot be claimed locally. Artwork requests sometimes return pending HTTP 503 responses after picks become visible; they are outside the measured discovery critical path and were left alone. MusicBrainz artist search also failed during initial taste setup, so the measured profile uses the supported tag picker.

## Verification and evidence

- **100 distinct targeted .NET checks passed:** 48 ListenBrainz transport/provider checks, 20 shared/persistent preparation checks, 30 Application discovery checks and two worker checks. An additional rerun of the existing cross-instance pacing/backoff check passed.
- The new real-time transport regression fails at the old deadline and passes when a 16-second response completes once. The bounded-overlap regression fails with two lanes and passes with four, comparing complete picks, scores, evidence, coverage and expiry against sequential preparation across five pools.
- The frozen eight-genre contract still hashes to `6b8e6dd62d50a4ce6060a333ef7a2867879bd09f7077b29bd017fc596135faef` across sixteen preparation scenarios. This proves fixture output compatibility, not live taste-fit quality.
- **Nine distinct Playwright regression checks passed:** seven Discover checks plus completed-result recovery and permanent-error polling checks. Server backoff is still respected. The frontend production build and `git diff --check` passed.

Raw local MCP records, sanitized stage/counter JSONL, numeric summaries, test logs, `.nettrace` files and hotspot reports are under ignored `diagnostics/results/discovery-bf60/`. Private runtime settings and disposable state remain under ignored `.linernotes/`; none are committed. Diagnostic hosts were stopped after collection. No deployment or push was performed.
