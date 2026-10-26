# Browser app

The browser app uses React, TypeScript, Vite and React Router. ASP.NET Core
serves its built files from `src/Presentation/wwwroot`, alongside the same-origin
API. The app has Home, Discover, Your Taste, History, batch-detail and account
routes.

## Build and run

Run these commands from `src/Presentation/ClientApp`. Use Node 22.22 or later
and the versions pinned in `package-lock.json`:

```sh
npm ci
npm run build:local
```

`build:local` typechecks and builds the app, copies font licenses, then publishes
the generated entry files and `home-assets/` to the Presentation host's
`wwwroot`. Run it after a fresh checkout before starting or publishing the host.

For frontend iteration, start the ASP.NET host on port 5080. From this directory,
run:

```sh
npm run dev
```

Vite serves the app locally and proxies `/api` requests to the host. See the
[local website runbook](../../../docs/renewal/local-live-run.md) for backend,
database and provider setup.

## How the app uses the API

`api/` contains response types, request helpers, CSRF handling and session
refresh. `app/` contains navigation, session state and shared request behavior.
`features/` holds page-level flows; `components/` holds reusable interface
elements; `styles.css` contains design tokens, layout and responsive behavior.

Opening a page does not create a guest, generate recommendations or contact a
music provider. The user starts catalog lookup or recommendation generation
explicitly. Catalog typing searches local data; an explicit lookup can request
missing catalog entries. The server supplies recommendation evidence,
explanations, feedback and ratings. Artwork comes from the recording cache; an
unavailable cover shows a placeholder.

Access tokens live in memory, so reloading the page requires signing in again.
Guest credentials use the existing server cookie. The client does not contain a
recommendation engine, fabricated scores, provider-key settings or audio player.

## Browser checks

With the host running, run the Playwright suite:

```sh
npm test
```

Set `LINER_BROWSER_EXECUTABLE` to use an installed compatible Chromium if the
default browser is unavailable. `LINER_HOME_URL` changes the host URL. The
normal suite includes public read-only checks and intercepted API fixtures;
fixtures are test inputs and do not populate the application.

To run the opt-in journey that writes to a development instance:

```sh
LINER_LIVE_FRONTEND=1 npm test -- --grep 'actual provider-backed'
```

Use an authorized development instance and a disposable account. This journey
does real catalog lookup, discovery, registration, login, export and deletion.
Its result records partial cleanup responses as partial; it does not claim
filesystem cleanup that the backend did not confirm.

The [frontend review](../../../design/superdesign/frontend-review/REVIEW.md)
contains screenshots, review scope and known limitations.
