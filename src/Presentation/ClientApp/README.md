# Liner Notes frontend

React + TypeScript + Vite, React Router and native Fetch. The existing ASP.NET
host serves the static build and same-origin APIs. Home, Discover, Your Taste,
History, individual history batches and the supported account controls are React
routes. The approved Superdesign exports are retained under
`design/superdesign/phase-1/`; they are references, never runtime templates.

## Local build

Use Node 22.22 or later and the exact dependency versions in `package-lock.json`.

```sh
npm ci
npm run build:local
```

`build:local` typechecks, builds route chunks, copies font licenses and publishes
the generated entry and `home-assets/` into `../wwwroot/`. It preserves existing
legacy and manual-test files. Run it before serving or publishing Presentation
from a fresh checkout. Open the existing host at `http://localhost:5080/`.

`npm run dev` proxies `/api` to port 5080 for local development. Production build
automation, CI, Docker and deployment remain outside the frontend authorization.

## Structure and integration

- `api/`: response contracts, public feed validation, same-origin fetch, CSRF,
  status-based errors and existing JWT refresh. Tokens stay in memory; reloading
  requires signing in again. Guest credentials remain the existing server cookie.
- `app/`: shared navigation, personal session bootstrap, revision-aware writes,
  submission locking, idempotent generation and identity-scoped batch caching.
- `features/`: public Home, personal Discover, taste editor, history and account.
- `components/`: artwork, notices, stored music destinations and feedback.
- `styles.css`: approved tokens, component styles and responsive/reduced-motion rules.

Visiting a page does not create a guest, generate recommendations or call an
upstream provider. Explicit source lookup and generation use server job APIs.
Catalog typing reads the local catalog. Polling respects server retry deadlines.
Artwork reads only the existing recording cache; unavailable identities/covers
show placeholders. Explanations, tags, feedback and ratings come from the backend.

There is no streaming/player UI, fabricated score, demo runtime feed, provider-key
entry, unsupported account-settings form or client-side recommendation engine.
The old interface remains available in preserved legacy files for compatibility.

## Browser verification

With the built host running, use the installed Playwright browser:

```sh
npm test
```

If its default browser is unavailable, set `LINER_BROWSER_EXECUTABLE` to a compatible
installed Chromium. `LINER_HOME_URL` overrides the default localhost base URL.
The normal suite mixes real public read-only checks with explicitly intercepted
contract fixtures. Fixtures stay in `tests/`; they never populate production UI
or databases. `LINER_REFERENCE_COVERS` may point to four local reference JPEGs
named `0.jpg` through `3.jpg` for artwork comparisons; otherwise tests use SVGs.

The opt-in live journey performs real writes, provider lookup, guest generation,
registration, login, exports and deletion using a disposable test account:

```sh
LINER_LIVE_FRONTEND=1 npm test -- --grep 'actual provider-backed'
```

Run it only against an authorized development instance. Credentials are generated
in memory and tracing is disabled. Live and controlled artifact directories are
separate. A backend partial-cleanup response is recorded, never treated as a full
filesystem cleanup. The generated sanitized result contains no tokens/passwords.

See [the current review](../../../design/superdesign/frontend-review/REVIEW.md)
for screenshots, validation scope, design adaptations and outstanding limitations.
