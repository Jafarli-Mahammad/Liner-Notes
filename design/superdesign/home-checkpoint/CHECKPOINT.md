# Home visual checkpoint — 2026-10-26

Historical checkpoint: Home was subsequently approved. The temporary preview has
been removed and personal routes now use React and real APIs. See the current
[frontend review](../frontend-review/REVIEW.md) for implementation status and evidence.

Update after review: the developer explicitly authorized temporary sample content
for visual approval. Localhost:5080 now runs the separate **design-preview build**,
using an ignored, removable `wwwroot/home-preview/feed.json` and four local covers.
Sample names/tags are not embedded in React components or the database. See
[desktop](preview-1440.png), [tablet](preview-768.png), [mobile](preview-390.png).
All 18 normal-mode tests and four additional preview tests passed. Restore the
real API with `npm run build:local` from ClientApp, then remove `home-preview/`.
The original live/API evidence below describes the normal build, not the current
temporary preview. The real public API remains unchanged and empty. A fresh
Waterfox 6.7.6 profile renders Home; the reported black page in the user's active
profile has not been reproduced or confirmed resolved.

Status: frontend foundation, shared shell and Home implemented and locally verified; awaiting developer review. Stopped at the requested boundary. No new Discover, Your Taste, History or Account implementation. No commit, push, deployment, CI/Docker change, backend behavior change or performance-audit remediation.

Open the existing application at **http://localhost:5080/**. Its real public feed currently returns `200 {"items":[],"nextCursor":null}`. Home displays that empty state honestly. No recommendation records or artwork identities were manufactured to fill the live page.

## Implemented

- React/TypeScript/Vite foundation with React Router and native fetch; exact stable direct dependency pins and npm lockfile. Supported installed Node 22.23.1; no environment/toolchain upgrade.
- Reusable shared header, responsive mobile disclosure, artwork frame/fallback, recommendation cards and discovery callout. Exact original PNG logo, self-hosted DM Sans/Inter Latin and extended Latin fonts, bundled licenses and runtime dependency notices.
- Real public feed read at `/api/discovery/recent?limit=4`, with no guest creation, private bootstrap, catalog calls, upstream browser requests or writes from Home. No sample data or design artwork in the production bundle.
- Loading skeleton, empty state, network/status/invalid-response errors, manual retry honoring numeric and date `Retry-After`, bounded artwork refresh and cancellation/visibility handling. Invalid identities and failed/cold cover requests preserve the square frame and visible placeholder.
- Production build served directly by the already-running ASP.NET host. `build:local` copies only the new entry and Home-owned assets. The project file excludes frontend source/Node tooling from server output. Local publish to `/tmp` verified current assets, licenses, legacy interface and manual-test pages.
- Canonical personal navigation destinations bridge to the preserved existing interface in `legacy.html`; a tiny entry script selects its existing tab. History opens existing discovery/history controls. This preserves access without implementing later phases. Original `app.js`, `discovery.js`, `styles.css`, controllers, host/security settings and all core layers are unchanged.

## Screenshots and fidelity

| Width | Actual application, real API | Populated implementation — intercepted test data | Approved reference |
| --- | --- | --- | --- |
| Mobile 390px | [Live empty state](live-390.png) | [Controlled-data comparison](controlled-data-390.png) | [Superdesign](reference-390.png) |
| Tablet 768px | [Live empty state](live-768.png) | [Controlled-data comparison](controlled-data-768.png) | [Superdesign](reference-768.png) |
| Desktop 1440px | [Live empty state](live-1440.png) | [Controlled-data comparison](controlled-data-1440.png) | [Superdesign](reference-1440.png) |

Additional test screenshots: [loading](loading-test.png), [error](error-test.png), [mobile menu](mobile-menu-test.png).

The populated comparisons run the actual production build through ASP.NET, with only feed/image responses intercepted by the test browser. Their four draft albums/covers are visual test inputs, not real recommendations returned by this site's current API. Reference covers are downloaded only to `/tmp`, outside application assets. Unmodified real API screenshots are deliberately provided separately.

The draft was fetched again through Superdesign CLI; its SHA-256 exactly matched the earlier original export. Compared all three required widths and corrected metadata row sizing, callout line-height and responsive header grouping. Final measured logo, heading, first cover frame and callout rectangles match the reference exactly at each width; heading font family, size, weight and tracking also match. See [geometry evidence](visual-comparison.json). All populated screenshots were visually inspected; these measurements do not imply every pixel is identical or that different real titles/covers will have identical wrapping.

| Reference / before review | Implementation / after review | Reason |
| --- | --- | --- |
| White text on coral CTA, ~2.9:1 contrast | Exact coral fill with dark reference ink, ~5.7:1; hover uses a slightly deeper coral with white text | Accessible contrast; dimensions, position and typography preserved |
| Genre chips absent from public DTO | Server-provided attribution in the same pastel treatment | Preserve color/spacing without inventing genre metadata |
| Inert mobile button and placeholder links | Keyboard-operable disclosure and links to existing personal interface | Functional shell without implementing unapproved pages |
| CDN Tailwind/Iconify, external fonts/logo | Bundled CSS, small inline decorative SVGs and exact locally hosted identity assets | Existing CSP stays unchanged; no runtime CDN dependencies |
| Only populated sample cards | Honest live empty state and tested loading/error/missing-art states | Actual API availability governs content |

## Verification results

| Check | Result |
| --- | --- |
| TypeScript check and Vite production build | Passed |
| Direct dependency versions vs lockfile | All exact pins matched; npm registry engines/peers checked before install |
| npm advisory report | 0 vulnerabilities reported; [audit output](npm-audit.json) |
| Home Playwright suite | 18 passed; [test output](browser-tests.txt) |
| Automated accessibility | No axe WCAG A/AA findings in real-empty and controlled-populated Home at 390/768/1440 |
| Keyboard | Skip link, visible focus, menu open/tab/Escape/focus return and breakpoint reset passed |
| Responsive/content | Required widths plus 320px long titles; no horizontal overflow; expected 1/2/4-column grids |
| State handling | Loading/empty/network/404/503/HTML/invalid JSON shape/recovery, numeric/date retry delays, cold/error/invalid artwork and bounded refresh passed |
| Reduced motion | Hover movement/transitions disabled; 320px reflow case passed |
| Real host/API/security | Public feed 200 JSON/no-store; unchanged CSP; Home only uses same-origin GETs; no page exceptions |
| Presentation Release build | Passed, 0 errors and 8 existing package/analyzer warnings; [output](dotnet-build.txt) |
| Focused .NET regression checks | 11 passed: host/static-file/manual-test gates and cache-only artwork controller; [output](dotnet-tests.txt) |
| Local .NET publish | Passed to `/tmp/liner-home-publish`; current entry/assets/licenses present, Node sources excluded, existing manual-test/legacy retained |
| Diff/scope checks | Whitespace check passed; core/backend/host/CI/Docker files unchanged; no new commit |

This uses Playwright 1.64.1 with the installed cached Chromium 155.0.8059.12, supplied through `LINER_BROWSER_EXECUTABLE`. Playwright MCP was attempted but its configured Chrome executable is absent. These are direct real-browser tests, not a claim that MCP browser support was repaired. No new browser installation or backend restart was required.

## Performance and dependency evidence

Final single cold local 1440px navigation, real empty feed, no artificial CPU/network throttling: FCP/LCP **160ms**, observed CLS **0.0300**, **0** long tasks, **7** same-origin GETs. See [raw measurements](performance.json). This is a small local navigation sample, not a production benchmark, representative populated-feed result or physical-device claim.

Build output: JavaScript **265,336 bytes** raw / **83,527 bytes** gzip equivalent; CSS **8,137 / 2,450 bytes**. Gzip figures are calculated sizes, not a claim that the existing host transmits compressed assets. The exact original PNG logo is **436,408 bytes** and dominates static identity cost; it was intentionally preserved without lossy modification. Fonts are locally hosted and lazily requested by CSS; unneeded extended subsets are not requested on the English Home sample. No compression/cache-policy changes were made because those belong to the ongoing audit.

Runtime pins: React/React DOM 19.3.1, React Router 8.5.0. Development pins: Vite 8.3.5, official React plugin 6.1.2, TypeScript 7.0.2, React type definitions 19.3.0, Node 22 types 22.20.5, Playwright 1.64.1 and axe Playwright 4.14.0. [Registry metadata](dependencies.json) and the [lockfile](../../../src/Presentation/ClientApp/package-lock.json) record the selected releases. No prereleases, UI kit, general state manager, query cache, animation package or backend dependency changes.

## Files changed

| Area | Files / purpose |
| --- | --- |
| Frontend source | [ClientApp](../../../src/Presentation/ClientApp/README.md): manifest/lockfile/TS/Vite/Playwright config, entry document, `main.tsx`, `app/Header.tsx`, `features/home/Home.tsx`, `api/recent.ts`, `components/Artwork.tsx`, `components/ArrowIcon.tsx`, `styles.css` |
| Identity assets | Exact PNG, four WOFF2 files, asset-source inventory and two SIL font licenses |
| Local build / checks | `scripts/publish-local.mjs`, `licenses.mjs`, `capture-home.mjs`, `measure-home.mjs`, `tests/home.spec.ts` |
| Existing webroot | Generated `index.html`; preserved `legacy.html` and new `legacy-entry.js`; generated `home-assets` ignored |
| Host build metadata | `Presentation.csproj`: exclude `ClientApp/**` from default server items only |
| Repository docs | `.gitignore`, `CHANGELOG.md`, this checkpoint and evidence/screenshot files |

Unrelated existing workflow, security, audit, observation-log and reference-file changes were left untouched. The final source diff is uncommitted for review as explicitly requested.

## Skills and tools actually used

- Superdesign skill and CLI: authenticated, retrieved the exact approved draft, verified export identity; no generation or redesign.
- Task-observer: tracked scope/evidence lessons; see the observation below.
- Frontend-design: followed the supplied visual source and reviewed rendered fidelity; no new aesthetic direction.
- Ponytail: kept components/API client focused and avoided speculative personal-page state or extra runtime libraries.
- Emil design-engineering guidance: fine-pointer hover treatment, precise transitions, reduced-motion behavior and Before/After comparison above.
- UI/UX Pro Max: searched keyboard/focus navigation guidance, applied responsive and accessibility checks.
- Web-design-guidelines: fetched the [current official review rules](https://raw.githubusercontent.com/vercel-labs/web-interface-guidelines/main/command.md); corrected live status updates, sticky-header anchor offset and active navigation state. No outstanding Home findings from this focused review; automated checks do not replace assistive-technology testing.
- Tools: codex-mem project lookup, shell/rg/git read-only checks, npm registry/install/audit, TypeScript/Vite, direct Playwright/cached Chromium, axe, image viewer, curl and .NET build/test/publish. Git was not used to stage or commit implementation changes.

## Known limits and next approval boundary

1. Live feed is empty. Populated live recommendation/artwork behavior remains unverified in this checkpoint; controlled browser tests establish rendering/error mechanics only. No backend/data repair or acquisition was attempted to change that.
2. Personal pages remain the old interface. The new React Discover/Taste/History/Account experiences are not implemented. No new auth refresh/session management was built because Home does not require private APIs; existing security remains in the existing interface.
3. Artwork cache warming/coverage and original identity resolution remain backend responsibilities. No artist/general album artwork support, richer public metadata or new API fields were introduced.
4. This is a local build integration. Run `npm ci && npm run build:local` before serving/publishing a fresh checkout. CI/Docker do not automatically build the frontend; production packaging/deployment require separate authorization.
5. No Firefox/WebKit, physical-phone, screen-reader or real browser-zoom acceptance was performed. Automated accessibility, keyboard, reduced-motion and narrow-viewport checks passed; they do not establish complete WCAG conformance.
6. Existing .NET warnings were reported and left outside scope. The full backend suite and performance audit were not rerun.

Workflow observation: a real empty API response and a populated design reference require separate evidence tracks. Preserve unmodified live screenshots, label intercepted data clearly, and compare component geometry using reference-only browser inputs. Never populate the application's database or ship sample data merely to make a visual checkpoint look complete.

Awaiting the developer's Home review. Passing checks do not authorize another phase.
