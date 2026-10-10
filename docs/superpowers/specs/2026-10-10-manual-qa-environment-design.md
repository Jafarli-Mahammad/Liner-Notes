# Manual QA Environment Design

## Status and intent

The developer approved a development-only manual test frontend and clarified that it
should exercise everything already implemented. The environment must call the local
application's real APIs and show their actual results. It is for manual review; it must
not claim to replace automated verification or the developer's confirmation.

The separate `Users.PopularityPenaltyEnabled` setting, migration, settings API/UI and
export field remain unimplemented and out of scope. No other pending product approval
is included. Live Last.fm acquisition, email delivery, scheduling, Phase 5 evaluation,
and public deployment remain out of scope.

## Current behavior in scope

The current Presentation host exposes these user workflows:

- Register with account and delivery preferences, sign in, inspect the authenticated
  profile, refresh a session and sign out in the current browser tab.
- Seed a taste profile and read its stored signals.
- Read the latest digest and submit categorical feedback or a 1–10 rating. Familiarity
  (`AlreadyKnown`) can coexist with a rating; rating changes and explicit resets can be
  checked by reloading the stored digest.
- Inspect and download the versioned user-data export.
- Permanently delete the current account and its account-owned data.

Phase 6 additionally implements digest generation as an Application command, but the
Presentation host currently has no generation endpoint. A Development-only endpoint is
needed to exercise that workflow from a browser.

The Phase 2–4 recorder and evaluation scripts are separate operator tools with their own
approval and inventory gates. The manual web environment will not launch them or make
live calls. The existing `/health` and Development Swagger pages remain available.

## Proposed architecture

Add a plain HTML/CSS/JavaScript page under a dedicated `wwwroot/manual-test/` directory.
The host will serve that path only when `IHostEnvironment.IsDevelopment()` is true; a
non-Development request receives 404 before static-file handling. No JavaScript package,
database schema change or production UI change is needed.

Add Development-only manual-test endpoints, registered only in Development:

- `GET /api/dev/manual-test/status` reports the app environment, configured generation
  origin, recording readiness/expiry state and whether storage accounting is configured.
  It never returns credentials, JWTs, manifest hashes, artifact paths or database connection data.
- `POST /api/dev/manual-test/storage/reconcile` requires authentication, reads current
  database accounting and explicitly calls the configured local storage reconciler. It
  returns reconciliation time and byte totals only, never paths or content hashes.
- `POST /api/dev/manual-test/digests` requires authentication, accepts an ISO week only,
  derives the user ID from the authenticated principal and sends the existing
  `GenerateDigestCommand`. The request cannot select or override provider origin, user ID,
  storage roots, manifest data or scoring configuration.

The page calls existing account, profile, taste, digest/feedback, export and deletion
endpoints directly. It stores access and refresh tokens in tab memory only, consistent
with the existing client. It does not print request bodies, passwords, bearer tokens,
provider payloads or secret configuration to its activity log.

## Manual test experience

The page presents a Development banner and a short ordered workflow:

1. Check environment readiness and `/health`; show whether generation is configured for
   `Synthetic` or `Recorded`, without exposing secret material or local filesystem paths.
2. Register a disposable account or sign in; inspect profile; manually refresh the session;
   sign out. Include a single invalid-login check and validation feedback. Never automate
   repeated failures or rate-limit bursts.
3. Seed three to five supported tags/artists and inspect the stored signal list. Include a
   supported fixture-seed suggestion so synthetic generation can be exercised locally.
4. Explicitly reconcile storage inventory, then generate for a chosen ISO week using the
   host's fixed configuration and load the
   persisted digest. Show generated/existing/stopped outcome, stop reason, pick order,
   complete score snapshot and coverage gaps. Repeating the same week checks idempotency.
5. Change familiarity and numeric rating independently, try a categorical response and an
   explicit reset, then reload the digest to inspect persisted state.
6. Inspect the complete export JSON and download it. Offer permanent deletion only as a
   separately labeled final action with a typed confirmation and a disposable-account
   warning; afterward verify the deleted session cannot access account routes.
7. Show relevant same-origin response security headers and request status codes where
   browser APIs permit. The page does not run destructive lockout/rate-limit loops.

All result panels reflect server responses. Errors show the status and safe problem detail;
there are no fabricated successful states or hidden fixture fallbacks. A browser-memory
checklist lets the developer mark scenarios reviewed without persisting test judgments.

Recorded generation is available only when its existing approved, unexpired manifest
configuration and injected immutable response set are present. Otherwise the status page
explains that it is not ready. Synthetic generation uses the existing fixture-only source.
The frontend cannot switch modes, enable live acquisition, or bypass storage checks.

## Run and data safety

The implementation will include `scripts/run_manual_qa.sh` and concise local run
instructions. The script requires an explicitly supplied connection string for an existing
local disposable PostgreSQL database, creates temporary isolated artifact roots, forces
`Generation:Origin=Synthetic`, and runs the app in Development. It does not create/drop a
database, create accounts, or write recording artifacts. It applies the existing EF
migrations to the explicitly named disposable database before launching. The connection
string is read from `LINER_MANUAL_QA_CONNECTION_STRING`, must target loopback and a database
named `liner_notes_manual_qa`, and is never printed. The developer creates the test account
in the UI; account deletion is always initiated explicitly by the developer.

No API secret or test credential is committed or printed. All test requests remain
same-origin. The QA static files and both new endpoints must be unavailable in every
non-Development environment, including Production.

## Acceptance criteria

- `/manual-test` and the new API routes work in Development and return 404 outside it.
- A developer can manually exercise every current user-facing account, taste, digest,
  feedback, export and deletion flow against actual local persistence.
- The page can generate and reload a digest through the existing Phase 6 command, show
  idempotency on retry and display stopped generation reasons without mutating configuration.
- Storage reconciliation is an explicit visible action and reports only byte totals and time.
- Synthetic mode makes no outbound HTTP calls; Recorded mode remains manifest-gated and
  consumes only the already injected immutable responses.
- Credentials, tokens, manifest hashes, database connection data and artifact paths never
  appear in the page or activity log.
- The popularity toggle and other unimplemented approvals remain absent.
- Local manual run instructions and isolated synthetic artifact setup are included; no
  package or database schema migration is introduced.

## Implementation plan boundary

After this spec is reviewed, the implementation plan should split work into: (1) guarded
Development-only static page and safe status/reconcile/generation endpoints, (2) end-to-end
manual flows and responsive/accessibility polish, and (3) the local launcher, run
instructions and security-boundary verification. Verification should include focused
Development/Production route checks and the existing relevant application test suites.
Manual visual and behavior confirmation remains for the developer to perform in the browser.
