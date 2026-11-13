# Browser review fixes — 2026-11-09

Implemented and locally verified against the [original review](../../QA-REVIEW-2026-11-09.md). Developer confirmation remains pending. The app's existing design, scoring policy, canonical identity requirements and provider boundaries remain in place.

| Finding | Before | After | Verification |
|---|---|---|---|
| 1. Catalog search | Refined title plus artist was treated as one title; artist-name tokens could outrank the full name | Album/track searches combine literal terms across title and artist fields; the UI explains refinement. Exact artist display names precede longer token matches in local and online results | Live `Paranoid Android Radiohead` returned Radiohead's 1997 recording first, selected and persisted. Live keyboard `Radiohead` selected Radiohead. PostgreSQL ordering and literal-query tests pass |
| 2. Artwork | Successfully acquired portrait had an expiry in the past, causing repeated acquisition; initials lost their flex layout; retry hints ignored negative-cache timing | Artist/recording paths share freshness arithmetic, cap retention after origin-age subtraction, use the existing bounded policy for headerless public media, and reject expired acquisitions. Portrait retry hints reflect the negative cache, browser retries back off, and fallback initials remain centered | Direct live probe changed from an expiry on November 8 at 23:00 while acquiring November 9 at 14:52, to a future expiry. Live canonical photo decoded at 250px with `Raph_PH · CC BY 4.0`. Recording cover also reached ready and decoded. Stale/private/non-cacheable guard tests remain passing |
| 3. Taste context | Current profile appeared to describe an earlier batch | Current chips are labeled “Your taste for the next discovery,” with an explicit statement that saved picks reflect the taste at request time | Controlled regression and live jazz edit over retained rock-based picks |
| 4. Search message | Separate visible and screen-reader copies of one status | One persistent live status inside the search surface, also referenced by the input | Single-message and keyboard catalog regressions |
| 5. Release label | Identical release titles repeated | Trimmed identical titles are displayed once; underlying canonical release records remain intact | Regression with two distinct release IDs sharing a title |
| 6. Feedback | 8/10 plus “Not for me” reached the server as contradictory input | The user's latest action wins: selecting a reaction clears an incompatible numeric rating; explicitly saving a numeric rating clears a conflicting reaction. Notes remain. A status explains the adjustment; controls disable during save | Original live 8/10 + Unicode note → Already know → Not for me succeeded and survived reload. Both conflict directions and explicit numeric-save behavior are regression-tested |
| 7. Favicon | No declared icon; browser fallback returned 404 | Branded SVG declared under the existing published `home-assets` path | Vite and the actual ASP.NET host return 200 with SVG content type |

## Root-cause details

The feedback server was enforcing its existing domain rule: a high rating cannot accompany Disliked, and a low rating cannot accompany Liked. The original slower check had first cleared the rating, so it did not reproduce the same state. Domain validation remains unchanged; the frontend reconciles the input before submitting and preserves notes.

Artwork acquisition succeeded, but a large upstream `Age` value was subtracted from the application's fallback lifetime for headerless public media. That made the effective expiry precede acquisition, immediately evicted the result and allowed another acquisition. Recording artwork also capped an explicit origin lifetime before subtracting age, which could reject still-fresh long-lived media. [ArtworkFreshness](../../src/Infrastructure/Artwork/ArtworkFreshness.cs) now centralizes these calculations. Explicit shared-cache lifetime takes precedence, actual origin age is subtracted before the local retention cap, and expired metadata or media remains ineligible. No unrelated image source or no-store forwarding was introduced.

MusicBrainz search syntax was verified against its [indexed-search documentation](https://musicbrainz.org/doc/Indexed_Search_Syntax). Title and artist clauses use quoted literal terms; text such as `OR` or `artist:evil` remains literal input. Search cache keys were versioned so an old no-match response cannot mask the corrected query. Existing pacing and acquisition limits remain unchanged.

## Verification

- 112 targeted Infrastructure tests passed, including artwork freshness/expiry, request transport, catalog parsing, recording discovery and shared preparation.
- 11 targeted Presentation tests passed for artwork controllers, warmup and taste handling.
- 10 PostgreSQL catalog tests passed with zero skips, using test-created databases rather than changing the live catalog tables directly.
- 30 Playwright regression tests passed across discovery, taste, history and experience recovery, including the five new review regressions. After the final search-status placement adjustment, the 11 affected taste/review tests passed again.
- Frontend type checking and production build/local publication passed. The actual published favicon returned `200 image/svg+xml`.
- Playwright MCP exercised the actual live app: refined recording search, exact local artist selection, save/reload, a five-pick live batch, rated feedback and note persistence, canonical image decoding, and updated taste beside old recommendations. Controlled route fixtures were used only for regression coverage, not as evidence of live provider success.
- Desktop/mobile screenshots were visually inspected. [Desktop taste](taste-desktop.png), [mobile taste](taste-mobile.png), [discovery context](discover-context.png).

No dependencies, database schema, recommendation scoring weights or production deployment configuration were changed. Expected upstream absence or temporary outages still use placeholders and paced retries. This work does not establish subjective recommendation quality or universal artwork availability.

The disposable guest profile and its test feedback/history were cleared after verification. Public catalog and cache material remains subject to normal retention. Raw credentials and private exports were not committed.
