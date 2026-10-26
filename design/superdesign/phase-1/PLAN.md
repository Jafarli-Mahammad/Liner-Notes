# Liner Notes frontend — inspection and proposed implementation

Date: 2026-10-26. Status: Phase 1 inspection and planning only; implementation awaits explicit approval.

## Scope and evidence

Implement the approved refined vibrant editorial direction as reusable application components over the existing backend. Liner Notes recommends recordings and links to external music services; it does not stream music. Home starts with public recent recommendations; Discover is personal. Preserve the custom logo, artwork framing, typography, hierarchy and responsive spacing. Keep covers unobstructed and scoring popularity-neutral.

Inspected the current `Prism` checkout, `AGENTS.md`, `MEMORY.md`, `PROGRESS.md`, README, developer guide, architecture documentation, V1 infrastructure/folder documents, renewal contracts, guest-discovery and optional-onboarding specifications/evidence, current static frontend, host/security configuration, controllers, DTOs, application workflows, storage readers, artwork pipeline, Dockerfile and CI. Current code takes precedence over older documentation and memories. The separate React worktree is historical context, not the frontend in this checkout.

No application/backend/configuration/dependency changes, application startup, provider acquisition, database writes, performance experiments or deployment were performed. Existing dirty files, including the ongoing performance audit, security report, workflow, observation log and `design/superdesign/home.html`, are outside this change.

Superdesign CLI v0.14.0 authenticated to the Personal team. Retrieved these exact drafts with `get-design --draft-id … --output …` into this directory:

| Route | Reference | Draft |
| --- | --- | --- |
| `/` | [home.html](home.html) | `079fdf42-6283-4cec-b55c-8548658b3329` |
| `/discover` | [discover.html](discover.html) | `6da1a4f6-b08c-4d64-881a-d62d7bff86ed` |
| `/taste` | [taste.html](taste.html) | `1c3494b2-a972-4d0b-9686-dd82a6495693` |
| `/history` | [history.html](history.html) | `01873523-0319-4c75-853a-74c7fe37e0c6` |

[Project canvas](https://superdesign.dev/teams/01ff603e-4609-4536-908b-45bbea9e8598/projects/0297b205-654d-40c9-bbc2-33b2f207125b). `manifest.json` records downloaded HTML hashes and external asset URLs. These are reference files, not a proposed source-code transplant. They contain sample music and inert controls and must never become runtime fallback data.

All four local HTML files were rendered in installed cached Chromium at 1440×1000, 768×1000 and 390×1000. All image elements loaded in the 12 renders; no horizontal document overflow was detected. Used DM Sans/Inter font faces loaded; unused font subsets remaining unloaded are normal. Desktop and mobile screenshots were visually inspected, including tablet History. The Playwright MCP browser could not launch because its Chrome channel is absent; the fallback was direct Playwright with cached Chromium. This establishes reference rendering only, not live application integration, interactive accessibility or cross-browser acceptance. Screenshots and raw measurements are under `/tmp/liner-*`; they are not production assets.

## Exact visual system

| Element | Reference values and treatment |
| --- | --- |
| Surfaces | Main `#FAF9F6`, cards `#FFFFFF`; warm white foundation rather than a tinted dashboard |
| Text | Main `#18211F`, secondary `#626B69`, subtle `#78817D`; outline `#E6E9E4` |
| Accents | Coral `#FF674A`, coral ink `#CF4C30`, mint `#C9F0D9`, honey `#F9DB8B`, lilac `#DCCFFD`, blue `#BBDCE9` |
| Typography | DM Sans 700/800 for display, Inter 400/500/600 for body; display tracking `-0.045em`; uppercase metadata 11px, 600, `0.08em` tracking |
| Container | Maximum outer width 1380px, centered; horizontal padding 20px / 32px from 768px / 72px from 1024px |
| Navigation | Sticky 64px bar, subtle lower border, 190px logo below 768px and 230px above; active coral underline; mobile menu below 768px |
| Type scale | Home heading 30/36px; Discover 48/52.8px; Taste 36/48px; History 36/48px; body generally 14–18px |
| Artwork | Square, full-color, rounded 12px inside a white 16px frame with 8–12px padding; no numbered overlays, playback overlays or decorative filters |
| Cards | Fine borders, 16–24px corner radii, History 24px; content panels usually 24–32px padding |
| Shadow | `0 4px 24px rgba(24,33,31,.04)`; hover `0 8px 32px rgba(24,33,31,.08)` |
| Spacing | Common 8/12/16/20/24/28/32/36/40/48/56/64px; preserve each route's existing rhythm rather than making every panel the same |
| Motion | Card lift −2px over 200ms; image zoom 1.025 over 400ms; restrained fades. Draft transition rules differ across pages; unify in the app and disable movement, smooth scrolling and transitions for reduced motion |

The shared logo is the exact PNG referenced by all four drafts: `brand-liner-notes-working-violet-pink-wordmark.png/liner-notes-logo-concept-02.png`. It combines a dark geometric mark with violet/pink/coral lettering. Reuse that asset; do not redraw, approximate or replace it with initials. Acquire a local copy and verify its dimensions/transparency during approved implementation. Request a vector original only if one exists; it is not a prerequisite.

Reference content artwork includes Cocteau Twins, Depeche Mode, Beach House and Brian Eno. These images illustrate composition; they must not be assigned to unrelated recommendations. The Supabase URLs are Superdesign storage assets, not an instruction to add Supabase to Liner Notes.

## Architecture recommendation and alternatives

**Recommended: React + TypeScript + Vite, static output served by the existing ASP.NET Core host.** React provides reusable artwork, pick, feedback, taste and history components; TypeScript makes contract differences explicit. Vite is a build/development tool, with no Node process required in production. Keep the current same-origin API, host, provider abstractions, Domain scoring and storage unchanged.

Use the small `react-router` browser-routing API for real routes, deep links and back/forward behavior. Use native fetch plus focused hooks and Context/reducers for session and taste draft state. Do not add Redux, a general query cache, UI kit, animation library or Tailwind runtime. Plain CSS variables and component styles can reproduce the references precisely.

Alternatives considered:

1. Retain plain JavaScript and split the two current global scripts into modules. Fewer dependencies, but substantially more manual work to keep shared UI, asynchronous state and route lifecycles consistent. Reasonable only if avoiding a build tool outweighs component maintainability.
2. Adopt an SSR/full-stack JavaScript framework. Useful if public SEO/server rendering becomes an explicit requirement, but adds build/hosting concerns and duplicates the existing server's responsibilities. Defer for this scope. Client-rendered public content has an SEO/first-render tradeoff; this proposal does not claim SSR.

Official references checked for architectural compatibility: [React's Vite/TypeScript path](https://react.dev/learn/build-a-react-app-from-scratch), [Vite static production output](https://vite.dev/guide/build), [React Router browser installation](https://reactrouter.com/start/declarative/installation). Exact current stable package versions and Node compatibility have **not** been selected or verified; verify releases, licenses and peer/engine requirements and pin compatible stable versions before installation. No product packages were installed in Phase 1.

Proposed source arrangement:

```text
src/Presentation/ClientApp/
  src/app/                  routing, shell, session context, route boundary
  src/api/                  typed DTOs, same-origin client, errors, auth refresh
  src/components/           Logo, Artwork, Button, Tag, Status, EmptyState
  src/features/home/        public recommendation cards and cursor paging
  src/features/discovery/   featured/secondary picks, generation, feedback
  src/features/taste/       draft state, catalog picker, tags, preferences
  src/features/history/     batch summaries and detail route
  src/features/account/     auth, guest import, export and deletion
  src/styles/               tokens, foundations, responsive component styles
  public/assets/            exact logo and licensed local font files
  tests/                    browser journeys and contract/error scenarios
```

Keep feature modules small; avoid generic service/repository layers in the browser. API services map responses, not recommendation logic. A public-feed item and personal pick are separate types: public content never inherits personal explanation or feedback fields.

Build to a staged directory, then copy generated `index.html` and hashed assets into the Presentation static publish output. Preserve `wwwroot/manual-test` and its development-only gate. Do not use a build step that empties all of `wwwroot`. The existing SPA fallback supports deep links; verify `/history/:batchId` reload and unknown-route handling. Do not ship draft HTML in the app's web root.

Development: use a same-origin ASP.NET-served production build for final integration; a Vite development proxy may forward `/api` and `/health` without changing backend CORS/CSRF policy. Preserve the browser Origin and test CSRF rather than rewriting it to bypass validation. Docker/CI will need an explicitly approved frontend build stage, pinned Node toolchain and `npm ci`; these packaging files are proposed changes, not backend business changes. If approval excludes packaging, stop at a locally reviewable build and report that limitation.

## Backend-to-frontend mapping

Routes below are verified from controllers and contracts, not from running a new live API session. Discovery endpoints depend on `GuestDiscovery:Enabled`; generation also requires configured runtime jobs. Private reads and writes resolve ownership server-side.

| UI need | Existing API and payload | Frontend handling |
| --- | --- | --- |
| Public Home feed | `GET /api/discovery/recent?limit=4&before=<sequence>` → `items`, `nextCursor`; each item has `sequence`, `recordingId`, `title`, `artistName`, `origin`, `attribution` | Initial four cards; optional explicit Load more. No guest profile creation or private bootstrap needed on Home. No dates, genres, album names, personal reasons or feedback are supplied here |
| Session/bootstrap | `GET /api/discovery/session` → `csrfToken`, nullable `profile` (`id`, `userId`, `revision`, `isGuest`, `expiresAtUtc`), `guestImportAvailable` | Bootstrap personal routes; no automatic guest creation just for browsing Home |
| Start guest | `POST /api/discovery/session` | Only on an explicit personal-flow action; HttpOnly `liner.guest` cookie; bootstrap again afterward |
| Personal overview | `GET /api/discovery` → `page`, `styles`, `cooldownSeconds`, `evidenceOrigin` | Page has revision, favorites, tags, batches, current job, next request time, onboarding. Null page is a first visit, not an error |
| Generate batch | `POST /api/discovery/batches` with `{revision, requestKey, selection?}` → 202 job status | UUID idempotency key per intent, retained for a retry of that same payload; no automatic duplicate POSTs |
| Generation status | `GET /api/discovery/jobs/{id}` → id/state/progressCode/errorCode/batchId/nextAttemptAtUtc; unfinished responses carry `Retry-After` | Resume existing job after navigation; poll only active visible flow; show meaningful stages, no fabricated progress percentages |
| Personal picks | `GET /api/discovery/batches/{id}` → id/kind/createdAtUtc/coverage/picks | At most five picks. Each has id/title/artistName/album/rank/explanation/matchedTags/feedback/rating/comment/mbid/spotifyUrl/youtubeUrl/sources |
| Feedback | `POST /api/discovery/recommendations/{id}/feedback` with `{revision, feedback, rating?, comment?}` → `{revision}` | `None`, `Liked`, `Disliked`, `AlreadyKnown`; 1–10 rating and note up to 1000 chars. Await success, update revision and synchronize detail/history state |
| Artwork | `GET /api/artwork/recordings/{recordingMbid}` | Same-origin cached bytes or SVG placeholder, both HTTP 200. Public item `recordingId` is usable; personal pick needs a valid `mbid`. This is not a URL for a release ID, arbitrary catalog row ID or artist |
| Catalog search | `GET /api/catalog/search?query=…&kind=Artist\|Album\|Track&limit=10&offset=…` → `items`, `hasMore`, normalization version | Explicit kind selector; accessible combobox; debounce and cancel local searches. Retain canonical IDs, catalog record IDs and legacy signal IDs distinctly |
| Explicit online lookup | `POST /api/catalog/lookup` with `{query,kind,requestKey}`; `GET /api/catalog/lookups/{id}` | Show only as a user-triggered action after local search; pending/results/error states. Never run upstream searches for every keypress |
| Tags | `GET /api/discovery/onboarding/tags?query=…&category=…&offset=…&limit=24` | Searchable genre/mood/sound vocabulary; selected tags removable; suggestions from backend, not static sample music |
| Save taste | `PUT /api/discovery/onboarding` with `{revision, answers, selection}` → `{revision}` | `selection={catalogRecordIds,tags,legacySignalIds?}`; retain legacy favorites. Save explicit changes with pending/success/failure states and draft preservation |
| Answers | Version `taste-onboarding-v1`; step questions/tags/favorites/review/complete; goal, exploration, vocals, allStyles | Retain optional answers and skipping; no mandatory minimum of three favorites |
| History | Overview `page.batches`; individual batch endpoint above | Only ten latest completed batches listed. Summary has no pick count, artwork or feedback: load details on demand with bounded concurrency |
| Auth | `POST /api/auth/register`, `/login`, `/refresh`; `GET /api/auth/me` | Existing JWT contract; in-memory access/refresh credentials, one coordinated refresh, clear identity state on logout; no new token storage mechanism |
| Preserve/import guest | Registration `preserveGuestProgress`; `GET /api/discovery/session/import-preview`, `POST /api/discovery/session/import` | Preserve on registration; explicit preview/consent for existing-account import, including conflict information. No silent merging |
| Data controls | Guest `GET /api/discovery/session/data`, `DELETE /api/discovery/session`; account `GET /api/export/my-data`, `DELETE /api/subscribers/me` | Keep export and deletion accessible through Account; distinguish account deletion from an incomplete local-copy cleanup response |
| Legacy weekly digest | `GET /api/digests/latest`, `POST /api/digests/recommendations/{id}/feedback`; legacy taste endpoints | Preserve compatibility; do not treat these as onsite batch/history APIs or overwrite current taste through legacy seeding |

API base paths are supplied by `ApiControllerBase`; string enums are serialized by the host. Register supports username/email/password, timezone/day/hour, `preserveGuestProgress` and `weeklyEmailOptIn` (default false). There is no general subscription-settings update or production email-delivery guarantee to expose as a working feature.

## Design gaps and required adaptations

| Draft detail / gap | Proposed frontend-only resolution | Separate backend approval needed? |
| --- | --- | --- |
| Home genre chips | Omit when absent; retain composition through title, artist and attribution. Do not fetch personal data or invent genre labels | Only if genres are required on public cards |
| Example album titles and prose | Display actual recommended recording titles and server-generated explanations from stored score data. Optional album text only when returned | No |
| Record label chip such as “4AD” | Omit: not provided in personal pick DTO | Only if required |
| Numeric match or full score breakdown | No match percentage exists. Onsite DTO supplies explanation and up to three matched tags, not raw score components; do not fabricate them or recompute scores | Yes, if raw onsite score details are required |
| Artwork assumed everywhere | Cached recording artwork already exists; use it with honest missing/loading placeholders. No artist image or general album-cover API exists. Favorite album/artist rows use restrained type placeholders; recording favorites can use valid canonical recording IDs where available | Only for broader artist/album artwork coverage |
| Cold cache, historical artwork | Background warmup scans eligible public recommendations, not all private/history/favorite items. Cache is in memory and can be cold after restart. No guarantee of covers for every card | Broader warmup/persistence/resolution requires separate approval |
| Familiarity 0–100 slider, “Safe bets / Deep cuts” | Replace with discrete Closer / Balanced / More exploratory choices mapped to `exploration=close/balance/surprise`, plus skip. Preserve panel treatment. Never imply popularity preference | No; continuous preference requires backend design |
| Vocals buttons | Map Any → `no-preference`, Instrumental → `instrumental`, Vocals → `vocals`; preserve existing `both` and skipped/null values. Evidence gaps remain unknown | No |
| Goal/all-styles missing from draft | Keep existing optional goal choices and “all styles” capability in the same preferences panel or a compact disclosure. Never erase saved answers simply because draft omits a control | No |
| “Discover more like this” | Label “Find more recommendations”: current API generates from profile, not a selected song. Respect cooldown and current job | Per-track generation would need new backend behavior |
| Apple/Spotify/Bandcamp placeholder links | Use valid stored Spotify/YouTube links when supplied; otherwise explicit “Search on …” links built from encoded artist/title as existing frontend does. Apple/Bandcamp are searches, not resolved recordings | Resolved destinations would require integration |
| Guest “saved locally/on this device/permanently” claims | Explain server-stored guest data, browser credential and seven inactive-day expiry; offer account preservation without promising permanence | No |
| History claims to show everything | Label “Recent discovery history”; explicitly state up to ten latest completed batches. No fake pagination. Export remains the data portability path, without claiming complete coverage was newly verified | Yes for arbitrary historical paging or weekly-digest history |
| Static History counts/statuses | Derive counts, thumbnails, ratings and feedback only from batch details. Until loaded show a skeleton, not guessed counts | No; richer summary DTO would remove extra requests |
| Static hamburger, search, feedback and links | Implement real accessible controls and state transitions; drafts contain no application wiring | No |
| Coral/white and subtle text contrast | Check contrast before shipping; use coral ink/darker action fill or dark text where required while preserving accent palette | No |
| CDN scripts/fonts/images | Bundle app JS/CSS and local licensed fonts/logo. Current CSP allows self-hosted assets, not draft CDN scripts, external styles/fonts or remote image hosts | No CSP relaxation proposed |

Recommendation: approve the frontend-only resolutions above. None of these gaps requires a backend change to deliver an honest usable first version. Full history, richer public metadata and broad favorite artwork are separate backlog items.

## Route-by-route implementation plan

### `/` — Home

Persistent top navigation with the exact logo. Recent recommendations is the first main section, above the compact mint discovery callout. Start with four real public cards; grid 1 column below 640px, 2 from 640px, 4 from 1024px, gaps 20/24px. No sidebar, personal match language, recipient information or playback controls. Provide external search links if a card needs an action; do not invent a recording detail route. Load more uses the public cursor only when non-null.

Components: `AppHeader`, `PublicRecentFeed`, `PublicMusicCard`, `Artwork`, `Attribution`, `DiscoveryCallout`. Skeleton reserves artwork dimensions; empty feed says no public recommendations are available; network error offers retry; disabled discovery is distinguished from an empty list. Home fetch is independent of account/bootstrap failures.

### `/discover` — Personal discovery

Keep the editorial headline/taste strip on the left and featured pick on the right (5/7 columns from 1024px). Below, lay out remaining returned picks in three columns from 768px and one below; support zero to five picks and dynamic counts. Coral backdrop sits behind the frame only on wide screens; numbers, feedback and links remain outside artwork.

Components: `DiscoveryIntro`, `TasteSummary`, `FeaturedPick`, `RecommendationCard`, `StoredExplanation`, `ExternalMusicLinks`, `FeedbackControls`, `GenerationStatus`, `CooldownAction`. Bootstrap → overview → existing batch or honest first-visit prompt. Create guest only after the visitor elects personal discovery. Existing answers can be skipped; an entirely empty taste without all-styles opens the public Home experience rather than claiming personalized results. All-styles can still generate through supported backend behavior.

Generation states: idle, submitting, queued/running/retryable, completed, failed/cancelled, cooldown. Preserve current picks while requesting more; show sparse/empty coverage truthfully. Show saved explanation and matched tags, never the draft's sample editorial review. Each pick supports current feedback, clear, optional rating/note and pending/error behavior. Route and session changes cancel work; new generation does not run merely because a route renders.

### `/taste` — Your Taste

Keep the 8/4 desktop split, 40/64px gaps, large selection and tag panels, and preferences sidebar sticky below the navigation at wide widths. Stack on smaller screens; no sticky panel covering mobile content. Place controls for artist/album/track search and local results inside the existing visual language. Preserve recording and legacy name-based favorites, unlimited tag selection subject to existing server limits, optional answers and broad taste.

Components: `CatalogPicker`, `CatalogKindControl`, `CatalogResults`, `LookupStatus`, `FavoriteList`, `FavoriteRow`, `TagPicker`, `SelectedTags`, `DiscoveryPreferences`, `TasteSaveBar`. Use keyboard-operable search results with unambiguous artist/title/disambiguation. Selected count comes from current draft. Replace the continuous slider as described above. Saving and generation are deliberate separate operations: primary Find recommendations saves successful complete onboarding first, then submits one generation intent and navigates to Discover. A secondary Save action persists without generating. Save failure preserves edits and prevents generation. Route-leave confirmation protects unsaved edits; do not persist taste or tokens to localStorage.

### `/history` and `/history/:batchId`

Keep dated white batch cards, two columns from 1024px and one below; 32/40px gaps, 24/32px padding, 64/80px thumbnails. Use server timestamps formatted in the browser locale. Empty history invites personal discovery. Show the guest-expiry/account-preservation notice only for guests.

Overview supplies up to ten summaries. Hydrate visible cards with batch details using at most two concurrent requests and reuse identity-scoped in-memory details during route navigation. Load failures are per card. No profile-wide export request for card previews. Counts and feedback appear only after actual details load. Detail route reuses the personal pick components and ownership-bound batch endpoint; show an unavailable state for missing/expired/foreign batches, without inferring their owner. Back returns to History rather than the newest Discover batch.

### `/account` — supporting flow required to preserve existing capabilities

No fifth draft exists. Apply the established tokens and simple form layout, without redesigning the four primary routes. Provide sign in/register, guest-preservation/import preview, current account state, local logout, guest/account export and clear/delete controls. Header Sign in routes here and returns to the initiating route after success. Preserve password policy (12+ characters with existing complexity requirements), validation feedback and explicit email consent defaulting off. Do not expose working provider imports, account settings changes, password recovery or delivery guarantees without backend support. Keep `/manual-test` and `/swagger` as development tools, outside listener navigation.

## Shared state, performance and accessibility

- Client service: validate JSON/content type, normalize ProblemDetails and field errors, preserve status and `Retry-After` (seconds or HTTP-date), send bearer credentials only to same-origin API, and use same-origin guest cookies plus `X-CSRF-TOKEN` for discovery mutations. Refresh CSRF on session change; never blindly replay a failed write with a different request key.
- Session safety: single-flight JWT refresh, identity epoch checks against late responses, abort controllers, and timer cleanup. Clear private in-memory data on logout, guest clear, import/account changes and expiry. Serialize revision-dependent mutations across taste, feedback and generation. A 409 reloads current state and preserves the draft for explicit reconciliation; do not silently overwrite or retry with a newer revision.
- Request lifecycle: bootstrap once per identity transition and revalidate when needed; avoid mount/render-triggered POSTs and duplicate development-effect requests. Hide/stop polling on route departure or hidden tab; resume/revalidate on visibility return. Honor server retry deadlines before requests, including catalog lookup. Show 400 field errors, 401 session expiry, 403 reload verification, 404 unavailable/disabled, 409 changed state, 429 delayed retry and 503 temporary unavailability.
- Artwork: use the same-origin endpoint and valid recording IDs; reserve square geometry, explicit dimensions and asynchronous decoding. Eager/high-priority only the prominent visible cover; lazy-load below-fold art. SVG placeholders are successful responses, so an image `onerror` alone cannot detect them. Reuse the existing bounded placeholder-refresh approach only for visible candidates, at most four 30-second retries; stop on hidden/unmounted views and avoid endlessly cache-busting all covers. Missing valid identity uses a local placeholder without a network request.
- Assets: self-host the exact logo and licensed WOFF2 font subsets, include license files, preload only necessary faces. Keep finite local inline SVG icons matching the Lucide shapes in the draft; verify icon license when sourcing them. No Iconify runtime, Tailwind CDN or external artwork requests in the production browser.
- Delivery: route-level splitting, no blanket prefetch of all private batches, bounded history detail hydration and cursor-based public pagination. Respect existing API no-store and static no-cache headers. Fingerprinted filenames alone do not establish immutable HTTP caching; cache-policy changes belong to the performance-audit approval boundary. Measure production build size, LCP, CLS and network request counts before setting evidence-backed budgets.
- Accessibility: semantic header/nav/main/headings; skip link; visible focus, `aria-current`, live status regions and field-linked errors. Mobile menu needs expanded state, Escape/close behavior and focus return. Use native radio/checkbox groups for preferences and tags, correct combobox/listbox behavior for catalog search, descriptive removal labels, `aria-pressed` for feedback, and keyboard-operable destructive confirmation. Aim for WCAG 2.2 AA contrast and 44px touch targets. Artwork alt text must add useful identity or be empty when immediately redundant. Respect 200% zoom, text reflow, long names and reduced motion, including view-transition pseudo-elements.
- Preserve provider attribution verbatim where supplied. Do not claim custom similarity data, musical quality evaluation, scheduled production delivery, complete export verification or production readiness. Current provider terms/public-page approval remain a deployment gate; no new provider APIs are proposed here.

## Proposed dependencies and change boundary

Runtime: `react`, `react-dom`, `react-router`. Development: TypeScript, React type definitions, Vite, official React Vite plugin and Playwright test runner; Node type definitions if needed by configuration. Prefer Playwright plus native Node tests for isolated pure client logic; no additional unit-test framework or UI kit is justified initially. Version/engine/license verification and a lockfile are part of the first approved implementation commit, not completed work today.

Expected frontend changes: new `ClientApp`, local logo/font/license assets, static app entry/assets, frontend browser tests and targeted existing JavaScript-check migration, CHANGELOG for visible changes, developer build instructions. Packaging integration may touch Presentation project build metadata, Dockerfile and CI only if that portion is explicitly approved. Preserve manual-test assets and all unrelated changes. Do not remove the existing scripts until replacement journeys and packaging pass. No Domain/Application/DataAccess/Infrastructure/controller changes, schema migrations, provider settings, package upgrades to .NET dependencies, audit changes, runtime restarts or pushes are included.

## Proposed implementation phases and focused commits

Estimates are engineering effort ranges, not elapsed-time promises; assume the existing API contracts stay unchanged and no new artwork/history capabilities are requested. Overall roughly 7–11 focused developer days, with browser review and correction included. Developer approval/review waiting and provider availability can extend elapsed time.

| Phase / proposed commit | Work | Verification and review gate | Effort |
| --- | --- | --- | --- |
| 1. Frontend foundation | Pin verified stable dependencies; React/TS/Vite, routes, client/session boundary, local assets, tokens, build/publish strategy | Typecheck/build, known-route refresh, CSP compatibility, manual-test retained. No backend contract changes | 1–1.5 days |
| 2. Shared shell + Home | Exact logo/nav, responsive primitives, real public feed, states and artwork | Screenshot comparison at 390/768/1440, keyboard nav, empty/error cases, no guest creation/private/upstream requests from Home | 1 day |
| 3. Taste | Catalog search/explicit lookup, favorites/tags/preferences, save and skip, responsive panels | Existing guest/account flows, keyboard combobox, retained legacy selections, 409/429, no keypress-triggered upstream calls | 1.5–2 days |
| 4. Discover | Featured/secondary real picks, generation/polling/cooldown, stored explanations, links and feedback/rating | Idempotent submit/retry, refresh races, sparse results, feedback persistence, reduced motion, covers unobstructed | 1.5–2 days |
| 5. History + account continuity | Bounded batch previews/detail, auth/import/export/delete | Guest expiry, account switch/ownership, registration preservation, import conflicts, export and partial-cleanup handling | 1–2 days |
| 6. Integration and visual acceptance | Production static build integration, approved packaging, final responsive/accessibility/network pass, docs/changelog | Browser journeys against actual app, focused regression checks, clean focused commits, developer review | 1–2 days |

Each commit includes relevant verification. If checks fail or cannot run, report that and seek approval before committing, as required by AGENTS.md. Do not push. A passing local test run is not developer confirmation; final acceptance requires the developer to review the running result.

## Test and acceptance plan

1. Reference fidelity: compare each route at 390, 768 and 1440px; additionally check 320px and 200% zoom, long titles, one/five/zero picks and many selected tags. Preserve logo proportions, hierarchy, frame padding and spacing. No artwork badges or playback controls. Home public feed precedes the callout at every breakpoint.
2. Client/contract tests: HTTP/date retry parsing, one refresh during parallel 401s, stale response suppression on identity change, serialized revisions, idempotency across transport retries, safe external-link construction, unknown enum/content-type/error handling and timer cancellation. Test behavior rather than duplicating component markup.
3. Browser state tests: guest first visit, public-only browsing, skipped setup, all styles, catalog result ambiguity/empty/error, explicit lookup lifecycle, save failure, generation states/cooldown, sparse picks, feedback clear/rating/note persistence, bounded History loading/detail/back, account preservation/import, export/delete and expiry. Controlled intercepted responses are test-only and must be labeled; never ship fixture fallback.
4. Live integration after approval: use the actual configured local website and developer-approved test identity/data. Confirm real feed, catalog, generation, feedback reload and real/cold/missing artwork. No provider acquisition just to populate a visual screenshot; no synthetic substitute if live setup fails. Destructive account tests use disposable identities. Coordinate any process changes with ongoing performance work.
5. Accessibility: keyboard-only route/menu/search/forms/feedback/dialog paths, focus after navigation/error, contrast measurements, screen-reader announcements, reduced-motion emulation and reflow. Cached Chromium alone is not cross-browser/physical-device evidence; include Firefox/WebKit if installed or explicitly record the unverified coverage.
6. Performance/build: typecheck and production build; inspect bundle/asset sizes, request waterfall, reserved cover dimensions and layout shift. Assert no browser provider/CDN requests, no duplicate submissions, bounded polling and history fan-out. Verify locally served production output and approved publish/container path. Use existing focused .NET host/artwork/guest tests only when changed integration justifies them; no load test/profiler/database scan is needed for this planning phase.

## Approval scope and remaining decisions

The proposed approval is for the React/Vite frontend, four reference routes plus necessary account/batch-detail support, frontend-only contract adaptations, local assets, verification and explicitly included build packaging. The backend and ongoing performance-audit work remain outside scope. Exact dependency versions will be verified before installation; any unavoidable backend requirement comes back as a concrete separate proposal.

The main decisions for review are the recommended React stack, discrete exploration control, honest placeholder/metadata limitations, ten-batch History boundary, ancillary Account flow and packaging scope. No application implementation will begin until explicit approval.

## Workflow observation

Read-only design retrieval should be treated separately from design generation. Skill instructions that require repository initialization before generation should explicitly allow exporting and inspecting existing approved drafts without generating new designs, creating canvas resources, scaffolding product code or spending generation credits. Pair each visual element with a concrete response field and inspect representative responsive renders before proposing implementation; this reveals metadata and control mismatches early.
