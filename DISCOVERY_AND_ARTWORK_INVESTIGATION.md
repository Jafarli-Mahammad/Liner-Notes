# Why discovery and album covers can take so long

Investigated 2026-10-27 against source commit `930e107` in this checkout.

This is an investigation, not a fix. No application behavior or provider limits were changed. The findings below cover the traced discovery and artwork paths; they do not claim that every possible production failure has been reproduced.

## Main findings

Discovery does a potentially large amount of sequential provider work behind one acquisition worker per web process. Requests can queue, time out, repeat preparation, or remain deferred without an overall deadline. The UI also has recovery cases where a completed request is not refreshed automatically.

Covers have their own bottleneck: a background worker scans public recent recommendations, downloads covers sequentially, and only then fills an in-memory cache. Opening a cover URL does not enqueue it. The browser checks missing images every 30 seconds and stops after four retries. A cover can become available after the browser has stopped checking, or never be scheduled at all.

**Fast cached responses do not establish fast cold preparation.** In the small live probe below, already cached covers took milliseconds. The strongest problems are the work needed before that cache hit and the coordination between that work and the browser.

## Evidence and limits

Evidence labels used below:

- **Observed:** measured in this session.
- **Confirmed path:** directly established by current source and, where noted, existing tests. The incident frequency and wall-clock impact remain unmeasured.
- **Conditional:** requires particular data, load, configuration, or upstream behavior. Not established as the cause of the developer's original session.

### Local live probe

The normal local live profile was launched at `http://127.0.0.1:5081` using the existing Debug binary with `--no-build`. That binary was built earlier the same day; a byte-for-byte source/build provenance comparison was not performed. Source findings refer to the inspected checkout, while timings describe that running binary. The API reported `Live`; the private local configuration selects `listenbrainz`. Requests used the configured origin `http://localhost:5080`. A separate temporary guest used one supported tag, `synthpop`; no existing listener's taste or feedback was edited. The diagnostic guest was deleted and the temporary app was stopped afterward.

| Measurement | Result |
| --- | --- |
| First overview request | 161 ms |
| Public recent feed before diagnostic generation | 13 ms; empty |
| Temporary guest creation | 132 ms |
| Save diagnostic taste | 249 ms |
| Submit discovery | HTTP 202 in 216 ms |
| First job status check, 14 seconds after submission | Completed; exact finish time was not sampled |
| Read completed batch | 42 ms; three picks, five requested |
| Three cached cover reads | 9, 7, and 10 ms; JPEGs of 67,516, 60,072, and 74,655 bytes |
| Fresh Chromium context on `/discover`, after warming | API resources 2–6 ms; all three covers loaded; no page errors |
| Delete diagnostic guest | HTTP 204 in 177 ms |

The batch recorded `recording_metadata_missing` and `no_valid_artist_tag_votes` gaps. This confirms that incomplete provider evidence can reduce a successful batch's size. It does not demonstrate a general provider outage or reproduce the developer's exact slow request.

The first browser-tool attempt could not start because its configured Chrome executable was absent. Browser verification then used the already installed Chromium executable directly. Initial HTTP probes without the configured Origin were rejected as expected; these probe setup errors are not attributed to the reported bug.

No CPU profiler was attached, no memory dumps were collected, and no production processes were inspected with a profiler. CPU time, allocation, database plans, the original user's queue position, and individual upstream request durations were not measured. This was a small live API/browser trace plus source investigation, not a load test.

## Discovery request trace

`Discover.generate()` → `Personal.generate()` → POST `/api/discovery/batches` → `SubmitDiscoveryHandler` → durable job → prepared worker lane → acquisition lane on cache miss → taste materialization → candidate pools and hydration → exclusions/scoring → coordinated persistence → UI job polling → overview reload → batch read → artwork requests.

### D1. One acquisition lane serializes cold jobs and catalog lookups

**Confirmed path; high impact under concurrency.** [DiscoveryJobService](src/Presentation/Background/DiscoveryJobService.cs#L12) starts one prepared lane and one acquisition lane per host. Each lane awaits `ProcessAsync` before taking another job. The acquisition lane alternates preference between catalog lookup and generation. Slow provider acquisition blocks subsequent cold discoveries; catalog search work competes on the same lane.

The prepared lane is a useful fast path for cached evidence, but it does not increase cold acquisition throughput. Queue capacity defaults to 256; it is an admission bound, not a latency guarantee. Idle polling and the prepared-to-acquisition deferral each add scheduling delay. **Next action:** measure queue age and acquisition occupancy; use bounded, provider-aware concurrency rather than unrestricted parallel calls.

### D2. Seed and candidate preparation are sequential, even for a five-pick result

**Confirmed path; high impact for larger tastes.** [MaterializeDiscoveryTaste](src/Application/Features/Discovery/MaterializeDiscoveryTaste.cs#L48) awaits seed evidence inside a loop. [PrepareDiscoveryBatch](src/Application/Features/Discovery/PrepareDiscoveryBatch.cs#L24) builds requests, then awaits each pool at line 42. Album tag fallback can add credited-artist evidence calls. Default limits permit 20 artist/recording seeds plus 20 tag seeds, up to 400 candidates, to produce at most five picks.

[ListenBrainzRecommendationProvider](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecommendationProvider.cs#L42) may require artist resolution, radio/similar-recording results, recording metadata, and artist tag hydration. These stages accumulate network latency. There is no early return when five promising candidates have arrived: all selected pools are prepared before ranking. **Next action:** instrument per-seed and per-stage durations, then batch compatible metadata work and consider a smaller explicitly defined acquisition budget. Changing the candidate window can change results and needs evaluation.

### D3. Shared upstream pacing adds a deliberate wait to every uncached request

**Confirmed path; rate protection must remain.** [MetaBrainzRequestGate](src/Infrastructure/RecommendationSources/ListenBrainz/MetaBrainzRequestGate.cs#L22) spaces request starts by 1.05 seconds for each provider bucket. All processes using that pacing directory share the bucket; recommendation acquisition and artwork metadata both use `ListenBrainz`. Redirect hops for covers use the separate `CoverArtArchive` bucket.

Illustration: 60 cold calls to one bucket require approximately 59 × 1.05 = 61.95 seconds between their first and last starts, before accounting for serial network response time. This is arithmetic from implementation, not a measured session duration. The gate also opens, writes, and flushes its pacing file for each admitted request. **Next action:** reduce duplicate requests and trace gate wait separately from HTTP time. Do not remove provider pacing to hide the problem.

### D4. Timeouts and retries can accumulate into minutes

**Confirmed path; exact duration depends on the failure.** [DiscoveryRuntimeOptions](src/Application/Features/Discovery/DiscoveryRuntimeOptions.cs#L9) defaults to 180 seconds per execution and three attempts. [SharedPublicEvidence](src/Infrastructure/Discovery/SharedPublicEvidence.cs#L29) and [SharedDiscoveryPreparation](src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs#L58) impose 45-second preparation deadlines. The HTTP client has a 15-second timeout; enclosing cancellation also bounds response-body work.

[DiscoveryJobService](src/Presentation/Background/DiscoveryJobService.cs#L49) treats preparation cancellation as retryable and delays retries by 5 seconds times the attempt count. Three fully consumed 180-second executions plus the first two retry delays would be about 555 seconds, excluding queue/admission time; individual 45-second failures often stop an attempt sooner. A retry begins preparation again, reusing only evidence that was successfully cached. **Next action:** expose queue age, current stage, and a total request deadline; distinguish retryable transport failures from permanent problems.

### D5. Shared-preparation deferrals have no total deadline

**Confirmed path; potential indefinite waiting, not reproduced here.** Cache misses in the prepared lane and occupied shared-preparation leases raise `DiscoveryPreparationRequiredException` or `PublicPreparationBusyException`. [DiscoveryJobService](src/Presentation/Background/DiscoveryJobService.cs#L64) defers them by one second with `waiting_for_music`.

[DiscoveryJob.DeferLease](src/Domain/Discovery/DiscoveryJob.cs#L99) decrements `Attempts`. Repeated deferrals therefore do not exhaust `MaximumAttempts`. The execution timeout covers one leased execution, not total time since enqueueing. Persistent contention or repeated acquisition ownership problems can keep a job unfinished without reaching its nominal retry limit. **Next action:** retain fair deferral, but bound total job age or consecutive deferrals and report why it is waiting.

### D6. Cache effectiveness depends on upstream freshness and exact request identity

**Confirmed path; impact requires cache measurements.** [ListenBrainzApiClient](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzApiClient.cs#L23) keys cache entries by root and exact request key. Cache headers and response gaps determine whether successful evidence can be cached, with a configured 900-second default ceiling. [SharedDiscoveryPreparation](src/Infrastructure/Discovery/SharedDiscoveryPreparation.cs#L18) also includes generation configuration in its pool key. Each cold pool gets a new provider scope, limiting reuse of provider-local dictionaries across pools.

Expired evidence, process-local metadata-cache loss, configuration changes, different recording batches, and uncacheable responses can all force acquisition again. Persistent shared evidence exists, so it would be inaccurate to claim all discovery evidence disappears on restart. Artwork asks for `inc=release` separately from recommendation metadata's `inc=artist tag`; those are distinct cache entries. **Next action:** measure memory/persistent hit ratios, expired hits, uncacheable responses, and repeated entity requests.

### D7. Hydration fetches artist tags before deciding that recording tags suffice

**Confirmed path; unnecessary latency is data-dependent.** [ListenBrainzRecommendationProvider](src/Infrastructure/RecommendationSources/ListenBrainz/ListenBrainzRecommendationProvider.cs#L114) batches artist metadata for the pool before inspecting direct recording tags at line 138. A recording with usable tags can still wait for artist metadata that does not determine its selected scoring vector.

These responses are also stored as supporting evidence, so removing them requires an explicit provenance decision. **Next action:** use sufficient recording evidence first and represent additional evidence as not requested where appropriate; preserve deterministic explanations.

### D8. Storage admission serializes writes and repeatedly validates all managed artifacts

**Confirmed path; current latency contribution unmeasured.** [DiscoveryWriteCoordinator](src/DataAccess/Persistence/Repositories/DiscoveryWriteCoordinator.cs#L51) acquires the shared batch/advisory lock, reads storage state repeatedly, reserves capacity, verifies the mutation, and publishes committed inventory. This runs for submission, leases, evidence claims/publications, heartbeats, failures, and completion.

[LocalGenerationStorage](src/Infrastructure/Storage/LocalGenerationStorage.cs#L139) validates artifacts by enumerating managed roots and SHA-256 hashing files. [CoordinatedGenerationStorage](src/Infrastructure/Storage/CoordinatedGenerationStorage.cs#L94) invokes validation during admission and publication; reservation validation adds more passes. Cost grows with managed bytes/files, and competing writes wait behind the shared lock. Schema/storage inventory queries add database work too. This can delay an otherwise small job or cause its heartbeat to fail; it is not proof that hashing dominated the live probe. **Next action:** time admission, each scan, inventory SQL, and lock waits. Any optimization must preserve the integrity boundary.

### D9. Provider gaps and safety limits can yield failures or empty/short batches

**Observed short batch; other cases conditional.** [PrepareDiscoveryBatch](src/Application/Features/Discovery/PrepareDiscoveryBatch.cs#L47) rejects certain response/configuration gaps, retries empty transport-failure results, and enforces candidate/snapshot limits. The provider also bounds requests and working bytes. Missing recording metadata or usable tags can remove candidates; history and explicit exclusions suppress previously encountered tracks. The pipeline can complete with fewer than five picks, including zero when no eligible evidence remains.

The live test returned three picks with explicit metadata/tag gaps. A completed empty result is different from a pending job, even if both feel like the button did nothing. **Next action:** show the saved reason for an empty/short result and distinguish permanent source/configuration errors from temporary failures.

### D10. A hung HTTP request or persistent polling error has no client deadline

**Confirmed path.** [api/client.ts](src/Presentation/ClientApp/src/api/client.ts#L64) forwards a caller's abort signal but supplies no elapsed-time timeout. `Personal.generate()` makes its submission and reload requests without a timeout signal. A hung request can leave `busy` true until the browser/network terminates it.

[Discover.tsx](src/Presentation/ClientApp/src/features/discovery/Discover.tsx#L47) polls roughly every two seconds while visible, pauses while hidden, and schedules another poll after errors. Persistent 404/401 or network failures do not terminate that loop or provide an overall wait bound. **Next action:** add bounded request timeouts and explicit recovery for terminal errors while retaining server Retry-After handling.

### D11. Completion can stop polling before refreshing the visible picks successfully

**Confirmed path; failure injection not run.** [Discover.tsx](src/Presentation/ClientApp/src/features/discovery/Discover.tsx#L58) stores terminal job status before awaiting the overview reload. Once the terminal status makes `active` false, the polling effect cleans up and aborts its controller. If the overview reload fails, its catch may schedule another poll, but that poll sees the aborted signal and exits. Old picks can remain until manual refresh or navigation.

[Personal.generate](src/Presentation/ClientApp/src/app/Personal.tsx#L93) also awaits a session/overview reload after a successful submission before returning its job. A reload failure can make an accepted request appear to have failed. **Next action:** handle submission, job completion, and result retrieval as separate recoverable states; retry result retrieval independently.

### D12. Progress messages conceal useful failure and retry detail

**Confirmed path.** [DiscoveryJob.Fail](src/Domain/Discovery/DiscoveryJob.cs#L88) writes `waiting_to_retry`, but [Discover.tsx](src/Presentation/ClientApp/src/features/discovery/Discover.tsx#L72) maps `retrying` instead. The UI falls back to a generic message. Terminal failures become a generic source-failure message despite the status DTO containing `errorCode` and `nextAttemptAtUtc`. Heartbeats repeatedly set `preparing` rather than a meaningful substage.

This does not make upstream work slower; it makes retries, queueing, and faults look like one endless operation. **Next action:** align progress codes and show safe stage/retry information with a clear recovery action.

## Artwork request trace

Background `ArtworkWarmupService` → public recent-feed page → ListenBrainz release metadata → release front image → optional release-group fallback → memory cache. Separately, browser `<img>` → cache-only artwork controller → cached image or placeholder → browser retry timer.

### A1. Requested covers are not queued; warmup only sees public recent recommendations

**Confirmed path; primary coverage gap.** [ArtworkWarmupService](src/Presentation/Background/ArtworkWarmupService.cs#L18) reads 50 recent items and warms only `Origin == "Public"`. [ArtworkController](src/Presentation/Discovery/ArtworkController.cs#L15) only reads cache; a miss does not schedule work. This is the correct boundary for keeping provider calls off rendering, but there is no asynchronous demand queue behind the miss.

Taste favorites, historical picks, and recordings no longer eligible for the public feed may never reach warmup. [DiscoveryBatchStore](src/DataAccess/Persistence/Repositories/DiscoveryBatchStore.cs#L114) only publishes picks with eligible public catalog metadata; [RecentRecommendations](src/DataAccess/Persistence/Repositories/RecentRecommendations.cs#L13) filters expired/ineligible publications, and the feed is trimmed to 1,000 entries. **Next action:** add bounded asynchronous warmup admission for relevant recording identities, including newly saved picks, with deduplication and public-metadata checks. Keep image reads cache-only.

### A2. A slow cover blocks every following recording in its page

**Confirmed path; high impact on cold cache.** [RecordingArtwork.WarmAsync](src/Infrastructure/Artwork/RecordingArtwork.cs#L19) takes one global cache semaphore and awaits each recording's release/fallback serially. [FetchAsync](src/Infrastructure/Artwork/RecordingArtwork.cs#L63) allows 30 seconds per release or release-group chain; each HTTP client's configured timeout is 15 seconds, with the enclosing deadline bounding the whole chain.

One recording can occupy approximately 60 seconds if both release and group chains consume their deadlines. A pathological 50-recording page could therefore occupy roughly 50 minutes before moving on, excluding metadata and scheduling overhead. This is an upper-budget illustration, not an observed run. **Next action:** isolate failures per recording and use bounded concurrent acquisition with the existing shared rate gate.

### A3. Pagination plus a 15-second sleep can delay new picks for a whole sweep

**Confirmed path.** [ArtworkWarmupService](src/Presentation/Background/ArtworkWarmupService.cs#L12) walks a descending cursor and sleeps 15 seconds after each page. While walking older pages, new records above the cursor wait until the sweep restarts. At 1,000 entries and 50/page, a complete sweep has 20 sleeps, approximately five minutes before network work is counted. A failed warmup leaves the cursor unchanged, so a repeatedly failing page can delay older pages too.

**Next action:** prioritize newly generated/viewed identities and use the sweep as maintenance rather than the sole scheduling mechanism.

### A4. The browser adds 30-second steps and eventually stops checking

**Confirmed path.** [Artwork.tsx](src/Presentation/ClientApp/src/components/Artwork.tsx#L22) retries a missing cover every 30 seconds, at most four times, while visible. With immediate placeholder responses, checks occur approximately at 0/30/60/90/120 seconds. If warmup finishes just after a check, display waits almost another 30 seconds. If it finishes after the last check, the mounted component does not automatically discover the image.

This retry horizon is shorter than a five-minute full background sweep even without slow downloads. **Next action:** distinguish pending work from unavailable artwork and coordinate retry timing with job/cache readiness; avoid endless polling.

### A5. Artwork is held in a small, process-local cache

**Confirmed path; eviction frequency unmeasured.** [RecordingArtworkCache](src/Infrastructure/Artwork/RecordingArtworkCache.cs#L8) has a 16 MiB budget. Successful entries live for one day but can be evicted sooner. Release and recording entries both charge the retained bytes because they can be evicted independently. Thus 16 MiB is not 16 MiB of unique recording cover capacity.

A restart loses this cache, and separate web processes have separate images. A browser request routed to an unwarmed process still gets a placeholder. Recording misses do not enqueue repopulation. **Next action:** measure evictions and consider a bounded persistent/shared image cache with license, expiry, and integrity rules.

### A6. Negative caching and upstream deferral extend unavailability

**Confirmed path; triggering failures conditional.** [RecordingArtwork](src/Infrastructure/Artwork/RecordingArtwork.cs#L60) caches missing recording mappings for one minute; a release/group 404 for six hours; other fetch failures generally for one minute; 429/503 uses Retry-After, clamped from one second to one day. The shared gate applies this deferral to later Cover Art Archive requests too.

Browser query-string retries do not bypass those server entries. A just-added cover after a cached 404 can remain unavailable for hours. **Next action:** expose safe cache-state reasons, align retry scheduling with eligibility, and retain upstream backoff.

### A7. Release selection can skip the supplied CAA release identity

**Confirmed path; affected metadata not observed locally.** [RecordingArtwork](src/Infrastructure/Artwork/RecordingArtwork.cs#L35) uses `Id(release, "mbid") ?? Id(release, "caa_release_mbid")`. When both exist, it tries the ordinary release MBID and then the release group; it never tries a distinct `caa_release_mbid` after the first release fails. A valid supplied artwork identity can therefore go unused.

**Next action:** verify metadata examples with differing IDs and test an explicit, deduplicated fallback order. Do not replace the release/group behavior blindly.

### A8. Validity checks and missing metadata deliberately end at a placeholder

**Confirmed path; individual rejection rates unmeasured.** [FetchAsync](src/Infrastructure/Artwork/RecordingArtwork.cs#L83) follows at most six allowlisted redirect hops and accepts JPEG/PNG/WebP with matching signatures, up to 1 MiB. Missing release/group IDs, unsupported hosts/types, oversized files, invalid bytes, 404s, and network errors all produce no cover. [Artwork.tsx](src/Presentation/ClientApp/src/components/Artwork.tsx#L3) also refuses non-GUID/empty IDs.

These are distinct from a slow successful download. The browser treats a natural image width of exactly 80 as a placeholder; a real 80-pixel image would also be hidden/retried. **Next action:** count safe rejection reasons and use an explicit availability signal instead of an image-dimension sentinel. Preserve redirect and byte validation.

### A9. Lazy loading and shared API rate limits add conditional delays

**Confirmed path; no throttling observed.** [Artwork.tsx](src/Presentation/ClientApp/src/components/Artwork.tsx#L35) eagerly loads only explicitly featured art; other covers are lazy. Missing-image retries require viewport visibility. This is reasonable for offscreen cards but explains why scrolling exposes covers later.

Artwork, polling, catalog, and discovery endpoints share the per-IP `catalog` policy: [DependencyInjection](src/Presentation/DependencyInjection.cs#L56), 1,200 requests/minute, no queue. Large shared-IP traffic or image bursts can return 429s; the image component handles those as generic missing images and waits 30 seconds. **Next action:** measure rejection counts and ensure scheduling separates genuine unavailability from throttling. The small probe was far below this limit.

### A10. The artwork pipeline lacks enough diagnostics to explain each miss

**Confirmed path.** [RecordingArtwork](src/Infrastructure/Artwork/RecordingArtwork.cs#L113) catches transport/IO/timeout failures without recording a reason. The worker only logs exception type for failures that escape. The controller returns HTTP 200 for both JPEGs and placeholders, and the browser infers availability from image width. Operators cannot distinguish unwarmed, queued, negative-cached, timed-out, evicted, and permanently unsupported artwork from HTTP status alone.

**Next action:** record low-cardinality reason counters, queue age, fetch duration, and cache state without credentials, response bodies, or personal taste data.

## What to fix first

1. **Stop indefinite/opaque waiting:** total job-age bound, safe stage/retry detail, terminal polling handling, and independent result-reload recovery (D5, D10–D12).
2. **Make cover scheduling match what users see:** bounded asynchronous admission for new/viewed picks, priority over maintenance scans, and retries aligned with actual readiness (A1–A4).
3. **Reduce cold acquisition work:** deduplicate/batch metadata, use sufficient recording tags, and add bounded concurrency while preserving shared rate limits and evidence semantics (D1–D3, D6–D7, A2).
4. **Make artwork caching durable enough for the deployment:** measure cache size/evictions, review CAA release fallback, and distinguish temporary/permanent misses (A5–A8).
5. **Measure storage admission before redesigning it:** capture lock/scan/SQL time on a realistic managed-artifact set; preserve integrity and accounting checks (D8).

These are proposed remedies, not implemented changes or promised speedups.

## Verification and follow-up

Existing targeted tests passed: **30 Infrastructure**, **4 Presentation**, **3 Domain**. They cover artwork lookup/cache safety, shared/persistent preparation, the artwork worker/controller, and discovery leases. They do not establish cold-load latency or absence of the recovery failures above. The Domain test runner initially failed because the sandbox denied its local socket; the rerun outside the sandbox passed.

For the original slow session, the next useful evidence is one correlated timeline: submit latency; queue age/lane; each seed and pool; gate wait; upstream duration/status; cache hit/miss; storage admission and heartbeat; terminal status; result reload; cover queue age/fetch/cache state; browser retry. Use a fresh diagnostic owner and a representative large taste as well as a small warm taste. Record distributions under controlled concurrency, rather than claiming improvements from one request. No existing user's account, secrets, provider payloads, or listening history should appear in the trace.

The current findings are based on current source inspection; older broad performance reports were not reused as current timing evidence. Other-worktree remediation is not assumed to exist here.

### Upstream references checked

- [ListenBrainz metadata API](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html): recording metadata supports selectable artist/tag/release information. This supports the separate release-metadata stage; it is not a provider latency guarantee.
- [Cover Art Archive API](https://musicbrainz.org/doc/Cover_Art_Archive/API): front-image/thumbnail routes, redirects, missing images, and throttling responses. Search indexing was available, but direct page retrieval returned HTTP 429 during this investigation. No current CAA numeric throughput entitlement was established.
- [MusicBrainz API rate limiting](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting): upstream rate protection is required. The 1.05-second spacing discussed above is this application's implementation, not a claim about a universal limit across MetaBrainz services.
