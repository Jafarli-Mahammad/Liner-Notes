# Discovery and artwork fixes — 2026-10-27

2026-10-30 follow-up: the approved storage/authentication redesign is implemented and locally verified. See [the implementation record](STORAGE_AUTH_REDESIGN_IMPLEMENTATION.md) for measured improvements, costs, recovery guarantees and deployment requirements. Historical findings below are not a claim about the final implementation.
Implementation follows [the investigation](DISCOVERY_AND_ARTWORK_INVESTIGATION.md).
Follow-up verification and current limits are in [the 2026-10-29 remediation report](DISCOVERY_PERFORMANCE_REMEDIATION.md). Developer confirmation is still pending. This is local verification, not a production latency guarantee.

## Implemented

- **No indefinitely deferred discovery jobs:** the chosen total age limit is ten minutes, including queue time and preparation handoffs. Expiry clears the lease/payload and prevents an expired worker from publishing. A timeout fails the request; it does not return an abbreviated candidate set. `DiscoveryRuntime:MaximumJobAgeSeconds` defaults to 600, with validation.
- **Bounded overlap:** two acquisition lanes per host, configurable from one to four, keep one cold request from monopolizing all acquisition. Each generation overlaps at most two independent pools. Existing per-provider filesystem pacing, shared leases, request limits, candidate windows, score weights, exclusions and ranking remain in force. Pool results retain input order.
- **Reliable result retrieval:** accepted submissions begin polling without requiring another successful overview read. Completed-result reloads retry independently from job polling; permanent status errors stop polling. Ordinary API fetches and refresh calls have bounded timeouts. UI progress matches the backend retry code, and timeout messages explain that saved information remains safe.
- **Covers are scheduled for demand:** successful batches enqueue their recording IDs after commit. Cache misses enqueue visible requests without contacting providers on the request thread. The bounded queue holds at most 256 queued/in-flight IDs, coalesces duplicates, prioritizes demand over maintenance, and processes batches of eight with at most two concurrent image fetches.
- **Public-only acquisition:** the worker checks published, unexpired public track metadata before image acquisition. A bounded revalidation path may refresh retained, previously published public CC0 identities and must publish freshly validated core metadata before acquisition. An arbitrary GUID does not trigger a provider lookup. Already stored, validated public covers can be restored without refreshing expired catalog metadata.
- **Accurate image readiness:** a same-origin HEAD response distinguishes pending, unavailable and ready covers. The browser retries pending/temporary failures with backoff and Retry-After for up to ten minutes starting at first visible demand, then downloads ready images normally. It does not infer missing artwork from an 80-pixel width. Offscreen requests are deferred; already loaded images remain available when scrolled back into view. CSP is unchanged.
- **Persistence within existing storage controls:** eligible covers are stored in PostgreSQL through the existing coordinated write admission. The image partition has a 64 MiB serialized-payload budget and a 1,000-entry cap; recommendation evidence retains its own budget and eviction partition. The 16 MiB image memory budget remains separate. No new private user columns, dependency, filesystem cache, or schema migration was introduced.
- **Image validation and freshness:** checksums, image signatures, allowed redirect hosts, HTTPS upgrades, byte bounds, shared pacing and provider deferrals remain enforced. A distinct `caa_release_mbid` is tried after the primary release and before the release group. Positive cache lifetime is at most one day and respects shorter upstream freshness. No-store/private/no-cache or expired responses are neither retained nor reused in a shared handoff. They may honestly leave artwork unavailable; no forwarding proxy is introduced. Temporary failures remain pending; missing covers can stop retrying.

The public cache contains recording identities and image bytes, never owner IDs, taste selections, ratings or listening history. Artwork is copyrighted by its rights holders; this cache does not relabel images as CC0. The existing public metadata eligibility check refers to catalog metadata licensing.

## Recommendation-quality changes stopped for review

These proposed shortcuts were **not implemented**:

1. **Reduce seed/candidate windows or return the first five usable tracks.** This changes the set being scored and can hide better matches. Current windows and full-pool ranking are retained.
2. **Stop fetching artist evidence whenever recording tags exist.** Although direct tags may already determine the vector, artist responses currently contribute supporting provenance and gaps. Defining sufficient recording-only evidence needs separate review and evaluation.
3. **Relax evidence validation, tolerate malformed responses, replace sparse provider data with invented recommendations, or retune weights.** These would change recommendation support or quality. None was done.

The parallel preparation regression test compares complete serialized picks, scores and evidence plus coverage/expiry against sequential execution for identical controlled inputs. It passed. This establishes output preservation for those inputs; it does not establish subjective taste quality or equivalence of changing live provider responses.

## Remaining limits

- Real providers can still be slow, unavailable or have sparse evidence. Shared rate limits intentionally constrain throughput. Larger tastes still require more acquisition; this change cannot promise instant cold discovery.
- Storage admission still performs the existing global coordination and artifact-integrity checks. Replacing those checks requires the separate integrity design and measurements recorded in the original audit; removing them for speed would weaken security.
- Artwork queues and memory caches are local to each host; image storage is shared through PostgreSQL. Queue entries are disposable and recreated on demand or by the maintenance scan after restart. Different hosts may repeat some downloads, but retain shared upstream pacing and coordinated image storage.
- Public cache cleanup and byte accounting remain bounded by the existing coordinator. The image budget covers serialized payload, not total PostgreSQL physical overhead; global storage admission still applies.
- Readiness checking adds a small HEAD request before a ready image download. This preserves the existing CSP and avoids rendering placeholders as real covers. Browser retries are finite and respect visibility/backoff.
- No VPS or production-load acceptance run was performed. Deploy the updated host/Worker together through the normal build process. Keep the existing shared pacing directory across hosts on the same upstream IP.

## Verification

- Domain: 151 tests passed.
- Application: 141 tests passed, including exact sequential/concurrent recommendation-output comparison and validated runtime limits.
- Infrastructure: 78 targeted artwork, ListenBrainz and shared/prepared-cache tests passed.
- Presentation: six targeted artwork/worker tests passed, including cached progress while another request is blocked and a second cold request progressing concurrently.
- PostgreSQL: 27 coordinated-storage/discovery tests plus the public artwork-admission test passed, with no skips. These include job expiry, image/evidence quota isolation, persistence through a fresh scope, and inventory verification. Each fixture used a disposable database.
- Frontend: production build/typecheck passed; 48 browser tests passed across home, discovery, history, taste and account flows, including accessibility checks at multiple widths. New recovery tests cover a failed completed-result reload, permanent polling errors, and artwork becoming ready after the former two-minute retry horizon.
- Live provider check: a separate `synthpop` diagnostic guest returned five picks from nine supported candidates. It was running at the four-second status sample and completed by the 46-second sample; exact completion latency was not sampled. Five cached JPEG reads took 4–21 ms. Five persistent public cover entries were observed. This run is not comparable to the earlier three-pick run and is not evidence of a measured speedup.
- After a host restart, the public cover endpoint returned a ready JPEG and a fresh Chromium context decoded its 500-pixel image without page errors. The diagnostic guest was deleted; its private discovery history was removed, and the public image cache remained readable.

Existing package/analyzer warnings remain; dependencies were not changed. Generated review screenshots are excluded from the changes.

## References checked

- [ListenBrainz metadata API](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html): release and CAA release identities.
- [Cover Art Archive API](https://musicbrainz.org/doc/Cover_Art_Archive/API): front images, redirects, missing art and throttling.
- [Cover Art Archive policy](https://musicbrainz.org/doc/Cover_Art_Archive): images retain rights-holder copyright.
- [AbortSignal timeout](https://developer.mozilla.org/en-US/docs/Web/API/AbortSignal/timeout_static): browser request deadlines.
