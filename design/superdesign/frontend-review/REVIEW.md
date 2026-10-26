# Remaining frontend review — 2026-10-26

Implemented and locally verified; awaiting the developer's final review. The
current application is served at **http://localhost:5080/**. No commit, push,
deployment, backend behavior change, database change, CI/Docker change or
performance-audit remediation was made.

## Delivered behavior

| Route | Implementation |
| --- | --- |
| `/` | Approved Home retained; recent public recommendations remain first. Temporary sample mode and its local JSON/covers are removed. Always reads the real public API. |
| `/discover` | Personal hero recommendation, stored explanations/tags, remaining picks, external music destinations, feedback/rating/note controls, explicit generation, cooldown and background-job progress. Honest first-visit, sparse, loading and error states. |
| `/taste` | Local catalog search for artists/albums/tracks, explicit provider lookup, selected favorites, canonical style search, optional goals/exploration/vocals, save or save-and-generate, conflict recovery and unsaved-change protection. |
| `/history` | Dated session cards, cached artwork thumbnails, actual feedback, bounded detail loading and incremental display of the sessions returned by the server. |
| `/history/:batchId` | Owned historical batch and available recommendation interactions; missing/unauthorized batch recovery. |
| `/account` | Login, registration, optional guest preservation and email consent, account summary, explicit guest-import review, existing JSON exports, local sign-out and confirmed guest/account deletion. Partial cleanup is reported accurately. |

The site remains exclusively a recommendation product. External destinations
are links/searches; no playback, streaming, playlist synchronization, fabricated
match percentages, generated explanations or unsupported account settings were added.

## Design source and comparison

The authenticated Superdesign CLI v0.14.0 retrieved all four approved drafts
again. Their downloaded hashes match the saved reference exports in
[phase-1](../phase-1/manifest.json). Draft identifiers:

- Home: `079fdf42-6283-4cec-b55c-8548658b3329`
- Discover: `6da1a4f6-b08c-4d64-881a-d62d7bff86ed`
- Your Taste: `1c3494b2-a972-4d0b-9686-dd82a6495693`
- History: `01873523-0319-4c75-853a-74c7fe37e0c6`

The exact approved violet/pink logo PNG is preserved. DM Sans and Inter are
self-hosted with their licenses. The existing warm white, coral, mint, honey,
lilac and blue tokens, editorial headings, container widths, responsive gutters,
white artwork frames, rounded panels and hierarchy remain the design system.
Covers never have rank badges over them.

Reference and implementation screenshots were reviewed at 390, 768 and 1440px.
Corrections included the Discover 12-column split and hero artwork dimensions,
Taste heading/intro spacing, mobile selection-row spacing, and History card/footer
height and alignment. Controlled contract fixtures reproduce the reference
artwork for visual comparison; they are test-only and cannot become runtime data.
Live screenshots show the actual provider results and available artwork instead.

Meaningful differences, with reasons:

| Difference | Reason |
| --- | --- |
| Live personal picks show neutral artwork placeholders when no cached recording cover is available; taste artist/album rows also have placeholders. | The backend exposes a recording artwork cache, not arbitrary artist/album image URLs. No guessed identities, direct browser upstream fetches or reference covers are substituted for live picks. |
| Explanations, tags, titles and number of picks vary; sparse-evidence notice can add height. | These are actual persisted backend results. Reference copy and label metadata are not fabricated. |
| Extra destinations, rating/note and evidence controls use disclosures. | The existing APIs support these interactions; disclosures preserve the reference's primary visual hierarchy. External search links explicitly say “Search”. |
| Exploration has three discrete positions and optional unanswered state. | Existing backend choices are close/balance/surprise, not an arbitrary numeric preference or popularity control. |
| A catalog-kind selector, explicit source lookup, optional answer details and separate save action are present. | Required to operate the real local-catalog/job/onboarding contracts without fake search results or automatic upstream calls. |
| History offers only the latest ten completed sessions and accurate guest expiry copy. | The existing overview returns up to ten summaries and no older-history pagination contract. Guest data is server-stored and expires after seven inactive days. |
| Coral primary buttons use dark text and coral links use a darker ink. | Contrast correction retains the approved coral backgrounds while satisfying automated AA checks. |
| Account uses the shared editorial design system. | There is no approved fifth Account draft. Only existing backend functions are shown. |

## Actual application screenshots

These are un-intercepted localhost screenshots. The disposable account shown in
Account captures was deleted through the existing endpoint after the journey.

| Page | Desktop 1440px | Tablet 768px | Mobile 390px |
| --- | --- | --- | --- |
| Home | [Desktop](home-live-1440.png) | [Tablet](home-live-768.png) | [Mobile](home-live-390.png) |
| Discover | [Desktop](discover-live-1440.png) | [Tablet](discover-live-768.png) | [Mobile](discover-live-390.png) |
| Your Taste | [Desktop](taste-live-1440.png) | [Tablet](taste-live-768.png) | [Mobile](taste-live-390.png) |
| History | [Desktop](history-live-1440.png) | [Tablet](history-live-768.png) | [Mobile](history-live-390.png) |
| Account, signed in | [Desktop](account-live-1440.png) | [Tablet](account-live-768.png) | [Mobile](account-live-390.png) |
| Account, forms | [Desktop](account-anonymous-1440.png) | [Tablet](account-anonymous-768.png) | [Mobile](account-anonymous-390.png) |

Controlled visual comparisons (explicit test fixtures, not real recommendations):

| Page | Reference 1440 / 768 / 390 | Implementation 1440 / 768 / 390 |
| --- | --- | --- |
| Discover | [1440](reference-discover-1440.png) / [768](reference-discover-768.png) / [390](reference-discover-390.png) | [1440](discover-controlled-1440.png) / [768](discover-controlled-768.png) / [390](discover-controlled-390.png) |
| Taste | [1440](reference-taste-1440.png) / [768](reference-taste-768.png) / [390](reference-taste-390.png) | [1440](taste-controlled-1440.png) / [768](taste-controlled-768.png) / [390](taste-controlled-390.png) |
| History | [1440](reference-history-1440.png) / [768](reference-history-768.png) / [390](reference-history-390.png) | [1440](history-controlled-1440.png) / [768](history-controlled-768.png) / [390](history-controlled-390.png) |

Additional fresh-profile Waterfox 6.7.6 screenshots are available as
`{home,discover,taste,history,account}-waterfox-{390,768,1440}.png`.
[Waterfox results](waterfox.json) verify all fifteen anonymous route/viewport
combinations rendered, loaded the exact logo and had no horizontal overflow or
JavaScript error log entries. This does not claim a signed-in Waterfox journey.

## Backend mapping and safeguards

| UI responsibility | Existing API |
| --- | --- |
| Public Home | `GET /api/discovery/recent?limit=4` |
| Personal bootstrap/guest creation | `GET /api/discovery/session`, `GET /api/discovery`, explicit `POST /api/discovery/session` |
| Catalog | `GET /api/catalog/search`, explicit `POST /api/catalog/lookup`, `GET /api/catalog/lookups/{id}` |
| Styles and taste | `GET /api/discovery/onboarding/tags`, `PUT /api/discovery/onboarding` |
| Generation | `POST /api/discovery/batches`, `GET /api/discovery/jobs/{id}` |
| Batch/history | Overview summaries and `GET /api/discovery/batches/{id}` |
| Feedback | `POST /api/discovery/recommendations/{id}/feedback` |
| Artwork | `GET /api/artwork/recordings/{recordingMbid}` |
| Authentication | Existing `POST /api/auth/login`, `/register`, `/refresh`; `GET /api/auth/me` |
| Guest import | `GET /api/discovery/session/import-preview`, explicit `POST /api/discovery/session/import` |
| Exports | `GET /api/discovery/session/data`, `GET /api/export/my-data` |
| Removal | `DELETE /api/discovery/session`, `DELETE /api/subscribers/me` |

Native Fetch sends existing same-origin credentials, bearer tokens where needed
and server CSRF tokens on discovery writes. Private credentials stay in memory.
Concurrent refresh is shared, generation retries retain the request key after an
uncertain response, writes use the current revision, and identity changes discard
stale private responses/caches. History detail reads are limited to two concurrent
requests. Local catalog typing is debounced and abortable; provider lookup needs
an explicit action. Job polling observes Retry-After and pauses while hidden.
Home remains a public read-only experience and does not bootstrap private data.

No backend endpoint/security/scoring changes were needed. The only host project
edit remains the previously approved exclusion of ClientApp source/Node tooling
from server publish inputs. The local build copies static assets into wwwroot;
production build automation was not changed.

## Verification results

| Verification | Result |
| --- | --- |
| TypeScript + Vite local production build | Passed; [build output](build.txt). Exact package pins and lockfile retained; no additional dependencies. |
| Normal Playwright suite | **45 passed**; [output](browser-tests.txt). Public real-API reads plus explicitly intercepted contract/error cases. |
| Real provider-backed journey | **1 passed**; [output](live-tests.txt), [sanitized steps](live-journey.json). Local catalog/provider lookup, guest taste save, five real picks, feedback/history, guest export, registration preserving guest progress, account export, sign-out/login and account deletion. No page exceptions. |
| Accessibility | No axe WCAG A/AA findings in tested Home, Discover, Taste, History and anonymous/signed-in Account states at all three widths. Keyboard catalog selection, native preference controls, skip link/menu focus, unsaved navigation, long-content reflow and reduced-motion behavior checked. This is not a screen-reader certification. |
| Failure/concurrency coverage | Public network/status/HTML/malformed payloads, retry deadlines, missing artwork, bounded refresh, empty Discover, stale revisions with retained drafts, field errors, explicit import, partial deletion, shared JWT refresh, private-response invalidation after sign-out, uncertain generation retry and bounded history loading passed. |
| ASP.NET targeted host/artwork checks | **13 passed**; [output](host-tests.txt). Initial startup hit the machine's exhausted inotify limit; retry used `DOTNET_USE_POLLING_FILE_WATCHER=1` only for the test process. No application configuration change. |
| Local ASP.NET publish | Passed to `/tmp/liner-full-publish`; entry matches the served build; current route chunks/fonts/licenses included, Node tooling excluded and existing manual-test files preserved. [Output](publish.txt). Existing .NET dependency/target-support warnings remain out of scope. |
| Waterfox | Fresh isolated profile; 15 real anonymous route/viewport render checks passed via native WebDriver BiDi. No existing user profile was accessed. |
| Boundaries | Baseline hashes for the unrelated Qodana workflow, performance audit, security report, Superdesign working file and existing observation log remain unchanged. No commit or push. |

An initial parallel browser invocation collided with another invocation's artifact
cleanup; separate live/controlled output directories resolved it. The final
45-test suite and live journey both pass. A deletion assertion initially expected
only HTTP 204 semantics; it now also verifies the backend's documented partial
cleanup result rather than concealing that warning.

## Performance and maintainability

Personal route modules are lazy loaded. Current build: initial JavaScript
335,353 bytes (104,680 bytes using Node gzip), shared CSS 27,613 bytes (5,817 gzip).
Personal route chunks are approximately 4.3–15.7 KB raw. Fonts are local; artwork
has reserved geometry, lazy below-fold loading and bounded cache refresh.

[One cold local Chromium navigation](performance.json), with no throttling,
recorded first contentful paint 52ms, LCP 124ms, CLS 0.050 and zero long tasks.
These are local observations, not production Core Web Vitals or a load benchmark.
The exact approved PNG logo remains 436,408 bytes. No backend compression/cache
or performance-audit changes were made. Historical hashed outputs can remain in
the local owned asset directory so existing open tabs keep their chunks; the
entry references only the current build.

## Changed files and tools

Source changes are in [ClientApp](../../../src/Presentation/ClientApp/README.md):
`api/client.ts`, `api/models.ts`, `app/Personal.tsx`, `app/Header.tsx`,
`components/{Artwork,FeedbackControls,MusicLinks,Notice}.tsx`,
`features/{home,discovery,taste,history,account}/`, `hooks/useRetryDelay.ts`,
`main.tsx`, `styles.css`, `index.html`, package/config/lock/build scripts and
browser tests/fixtures. Shared Home assets and fonts are reused.

Integration/documentation files: `src/Presentation/wwwroot/index.html`, generated
ignored `wwwroot/home-assets/`, preserved `legacy.html`/`legacy-entry.js`,
`src/Presentation/Presentation.csproj`, `.gitignore`, `CHANGELOG.md`, `PROGRESS.md`,
the ClientApp README, historical checkpoint note and this evidence directory.
The five ignored temporary `home-preview/` files were removed.

Skills actually consulted: Superdesign, task-observer, brainstorming (existing
approved scope), ponytail, frontend-design, emil-design-eng, ui-ux-pro-max and
web-design-guidelines. The approved references took precedence over generic
style suggestions. Tools used: Superdesign CLI, codex-mem MCP, local shell/file
tools, npm/TypeScript/Vite, direct Playwright with installed Chromium, axe,
Waterfox native BiDi, .NET test/publish and repository/API inspection.
The Playwright MCP Chrome distribution was unavailable, so direct Playwright
provided real browser evidence. The integrations were checked before reliance.
Focused interface review used the current [official review rules](https://raw.githubusercontent.com/vercel-labs/web-interface-guidelines/main/command.md).

## Remaining limitations and approval

- **Cover delay:** `GET /api/artwork/recordings/{id}` is cache-only on page reads.
  Its warmup worker covers recent *public* picks; it does not queue the personal
  batch's recording IDs. A cache miss gets an 80px SVG placeholder, browser-cached
  for 15 seconds; the frontend retries a visible placeholder every 30 seconds,
  up to four times. The shared warmup lane waits at least 1.05 seconds between
  ListenBrainz/MusicBrainz requests and sequentially looks up Cover Art Archive
  releases, so busy public-feed warmup can also take time. A recording with no
  upstream cover can stay a placeholder. Actual live personal screenshots show
  that state. The current public recent endpoint returned an empty list during
  this follow-up. Fixing per-personal-pick warmup or serving uncached artwork
  needs backend scope and upstream request review.
- **Recommendation delay/failure:** the page shows the server's `nextRequestAtUtc`
  cooldown and disables generation until that time. The API queues accepted
  requests as jobs; job status is polled every two seconds while work is active.
  Providers are deliberately paced at about one request per second per shared
  provider gate. A request can need multiple provider calls, and transient
  upstream failures are retried by the worker. Slow upstream responses/backoff,
  an unavailable worker, depleted evidence, validation/conflict or terminal
  provider failure can therefore delay or prevent a new batch. The frontend
  reports pending progress and preserves existing saved picks on failure; it
  cannot shorten a backend cooldown or make an upstream provider available.

- **Backend local email-copy cleanup:** the real deletion API reported
  `accountDeleted: true, localCopiesDeleted: false`. Account credentials/data
  deletion completed, while local-copy cleanup was not confirmed. The existing
  controller returns this result when its archive cleanup reports unavailable
  or incomplete cleanup. No claim is made that email files remain for this
  disposable account, nor that all local copies were erased. Backend diagnosis
  or remediation needs separate authorization; frontend displays the warning.
- Artwork coverage and older-history pagination cannot be expanded without
  separately authorized backend support. Existing placeholders and truthful
  limits remain visible.
- Tokens are intentionally not persisted by this frontend: reload requires
  signing in again; guest progress still uses the existing cookie. There is no
  server logout endpoint, settings update, password-reset or listening-account
  integration UI to invent.
- The reported Presentation “crash” was clarified by the developer as an asleep
  server. It is not an application-crash finding and required no backend fix.
- No full backend suite, external provider/legal release gates, email-delivery
  acceptance, production infrastructure, Safari/native-device testing or
  production performance claim is included in these local results.

Stopped for final developer review. Passing verification does not constitute
deployment approval or developer confirmation of the finished product.
