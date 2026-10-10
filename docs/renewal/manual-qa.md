# Local manual QA

This page exercises the application's real Development APIs against an explicitly named
local PostgreSQL database. It uses synthetic Last.fm fixtures, temporary storage roots and
the existing Phase 6 generation command. It does not enable live Last.fm requests.

## Prerequisites

- .NET 10 SDK and the repository's restored packages.
- A local PostgreSQL server.
- A disposable database named `liner_notes_manual_qa` created in that local server.
- A PostgreSQL connection string targeting `localhost` or `127.0.0.1` and that exact
  database name. The launcher reads this value from `LINER_MANUAL_QA_CONNECTION_STRING`;
  do not commit it or paste it into a tracked file.

Create the database using your local PostgreSQL administration tool. Then enter its
connection string through a non-echoing prompt and launch from the same shell:

```bash
read -r -s -p 'Local manual QA PostgreSQL connection string: ' LINER_MANUAL_QA_CONNECTION_STRING
printf '\n'
export LINER_MANUAL_QA_CONNECTION_STRING
bash scripts/run_manual_qa.sh
```

The launcher rejects a non-loopback host or a database name other than
`liner_notes_manual_qa`. It never prints the value. After stopping the app, run
`unset LINER_MANUAL_QA_CONNECTION_STRING` to clear it from the current shell.

## Start

From the repository root, run:

```bash
bash scripts/run_manual_qa.sh
```

The script applies the tracked EF migrations to `liner_notes_manual_qa`, sets the host to
Development and generation to Synthetic, creates isolated temporary roots for all six
storage categories, then starts Presentation. It never creates or drops a database and
never prints the connection string. The temporary artifact roots are removed when the app
stops. Account records remain in the disposable PostgreSQL database until you delete them.

Open the host URL printed by ASP.NET Core in the terminal and append `/manual-test` (the
default local HTTP URL is `http://localhost:5000/manual-test`). Stop the app with Ctrl+C.

## Browser scenarios

Use a disposable account. Registration and account deletion change the QA database; deletion
is permanent. Check the test page's Development and Synthetic status before sending requests.

1. Check the status panel reports Development, Synthetic input and configured storage.
   Check app health and inspect same-origin security headers in browser developer tools.
2. Register with valid details and inspect the created profile, including delivery settings.
   Try a duplicate email or invalid registration to see server validation responses.
3. Sign out and sign in. Try one invalid login. The account lockout threshold is five failed
   attempts for 15 minutes; authentication requests are rate-limited to ten per minute. Do
   not repeatedly click invalid login on an account you want to keep using.
4. Inspect the current profile, manually refresh the session, then sign out and confirm the
   page returns to an unauthenticated state.
5. Click **Send 2-seed validation probe** and confirm the server rejects the count. Then save
   three to five tags/artists. Read stored taste signals and confirm the values match.
6. Reconcile storage explicitly. The page reports reconciliation time and byte totals.
   Seed/profile writes change database accounting, so reconcile again immediately before a
   new generation attempt if the inventory has become stale.
7. Generate a digest for an ISO week. Confirm the selected picks, rank, complete score
   snapshot and coverage gaps. Repeat the same week and confirm the result is `existing`.
8. Try a contradictory pair (Liked with rating 2, or Disliked with rating 9) and confirm
   validation rejects it. Then change a pick's familiarity and numeric rating independently.
   Try `AlreadyKnown` with a rating, change the rating while keeping `AlreadyKnown`, then
   issue `None` without a rating as an explicit reset. Reload the digest after each change.
9. Inspect the complete export JSON and download the export. Check export version, current
   taste signals, digest history and stored score snapshots.
10. Only when finished, type the deletion confirmation for the disposable account. Confirm
    the page signs out and a protected account request is denied afterward.

The manual refresh button rotates the current token pair. Protected API calls also follow
the existing browser behavior and attempt one refresh/retry after a 401 response. Do not
test lockout or rate-limit boundaries with an account you intend to keep.

The browser activity list shows timestamps, same-origin paths and HTTP statuses. It does not
show passwords, bearer tokens, request bodies, manifest hashes, filesystem paths or database
connection data. The review checklist is held in browser memory and is not saved as user
data.

## Limits

This page covers the current user-facing Presentation workflows and Development-only Phase 6
generation. It does not expose the approved but unimplemented popularity toggle. It does not
replace internal checks for token hashing, every migration-preservation case or account-table
deletion inventory. Phase 2–4 recorder/evaluation scripts keep their own offline and live-run
gates. Live Last.fm acquisition, scheduled email, delivery recovery and recommendation quality
remain outside this page. Manual results do not establish catalog quality or production
readiness.

Recorded generation remains unavailable unless its approved manifest metadata and injected
immutable responses are already configured. This local launcher always selects Synthetic.
