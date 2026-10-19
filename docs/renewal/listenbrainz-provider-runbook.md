# ListenBrainz provider operations

ListenBrainz is the default. Last.fm is retained through
`Generation:Provider=lastfm`. Per-user selection and account linking are deferred.
Web and Worker use the same provider resolver.

## Configuration and credentials

Local Compose explicitly selects ListenBrainz **synthetic fixtures**. Both
`Generation:LiveEnabled` and `ListenBrainz:LiveUseApproved` default to false.
Neither a token nor provider selection can enable HTTP automatically. Ordinary
page views do not fetch upstream data.

Web and Worker share Development User Secrets ID `linernotes-secrets-app-v1`.
Keep `ListenBrainz:Token` there or set `ListenBrainz__Token` in the runtime
environment. Never put it in tracked configuration, URLs, arguments, recordings
or reports. The ignored `.env.listenbrainz.local` file has owner-only access;
.NET does not load dotenv files automatically. Do not print expanded settings.

Live generation requires every setting below:

| Setting | Value |
|---|---|
| `Generation__Provider` | `listenbrainz` |
| `Generation__Origin` | `Live` |
| `Generation__LiveEnabled` | `true` |
| `ListenBrainz__Mode` | `LiveOnly` |
| `ListenBrainz__LiveUseApproved` | `true`, after usage/license review |
| `ListenBrainz__RateLimitDirectory` | Absolute directory shared across processes on this upstream IP |
| Storage budget/inventory | Explicit `GenerationStorage__StopBytes` and reconciled inventory, as in the local runbook |

The current Worker requires Development and a loopback database. After storage
reconciliation, use an explicit one-shot `--action generate --user <id>
--week <YYYY-Www> --origin Live`. Capture remains separate. Scheduled QA remains
Synthetic; this work adds no production scheduler or external email delivery.

The default budget is 80 network requests per resolved client (range 1–200).
Responses are capped at 1 MB. Successful public metadata is cached up to 900
seconds, shortened by HTTP freshness. Pacing is at least 1.05 seconds per
upstream; response rate headers can impose further backoff. Containers sharing
an IP must mount the same pacing directory. Multiple machines or other upstream
consumers need a common coordination arrangement before live use.

## Bounded catalog smoke

This checks response contracts and small-sample coverage. It requests no account
history, creates no database rows, saves no raw recordings, and does not prove
recommendation quality. The token goes only to `api.listenbrainz.org`, never
MusicBrainz. The command permits at most 12 requests within 90 seconds:

```bash
dotnet run --file scripts/listenbrainz_smoke.cs --no-cache -- \
  --approve-read-only-catalog --token-file .env.listenbrainz.local \
  --artist Jakuzi --tag synthpop
```

Output contains counts, endpoint names and gap reasons only. Broad tags may time
out or have sparse coverage. Production live origins remain disabled by default;
passing a smoke is not deployment or usage approval.

## Approved recorded generation

Existing `LocalRecordedInput` path/hash approval and checks apply to both
providers. Last.fm retains version-1 local-use manifests and phase-4/5 source
approvals. ListenBrainz uses version 2 with `Provider: "listenbrainz"` and
`Use: "local-digest-generation"`, bound to a user/week, expiry and manifest hash.

Its source approval requires `provider: "ListenBrainz"`,
`purpose: "provider-adapter-validation"`, `status: "approved"`, a future
`retention.expires_at_utc` and the existing canonical approval hash. Response
methods are `artist.search`, `lb-radio.artist`, `lb-radio.tags`,
`metadata.artist` or `metadata.recording`. Identities and limits must match
requests. Bodies are hash checked before immutable replay. No file discovery,
live fallback, or evaluation-only exclusion is relaxed. Held-out recordings
cannot be repurposed for product generation.

## Failures and rollback

There is no automatic fallback to Last.fm. Live transport/rate/schema failures
stop generation; no supported picks also stops it. Existing user/week digests
remain idempotent. Ambiguous identities and absent tag votes become gaps.

To use Last.fm, set `Generation__Provider=lastfm` and choose Synthetic or approved
Recorded origin. Last.fm live digest generation remains disabled; its HTTP
client/pilot tooling and approval requirements remain. Reconcile storage when
required by the existing guard. History and email attribution use stored
evidence and are not relabeled or regenerated when provider settings change.

Public Last.fm use still needs its existing approval/attribution decisions.
MusicBrainz tags are supplementary data under CC BY-NC-SA 3.0; deployment use,
redistribution and commercial licensing need separate review. Credits identify
MusicBrainz tags and normalized weights; the software license is unchanged.
See [implementation evidence](listenbrainz-provider-evidence.md).
