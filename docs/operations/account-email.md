# Account email and weekly delivery

The workflow is opt-in and uses the existing database, storage admission, account
leases and stopped-writer operator model. It does not require a domain to build
or run local contract tests. Real delivery requires a verified sender and a real
public origin. This guide does not grant third-party permission or claim inbox
placement. See [verification and limits](account-email-evidence-2026-11-13.md).

## Public information and source-use review

Production configuration requires these non-secret values in the private runtime
settings used by web and Worker:

| Setting | Meaning |
| --- | --- |
| `PublicService:OperatorName` | Actual operator identity; no fictitious/default public operator |
| `PublicService:PrivacyContact` | Private contact as an HTTPS URL or `mailto:` address |
| `PublicService:RightsReference` | Reference to the actual reviewed source-use record |
| `PublicService:MusicBrainzUse` | `noncommercial` or `licensed`, matching the actual use |
| `PublicService:MusicBrainzLicenseReference` | Actual license reference when `licensed` is selected |
| `PublicService:CoverArtUseReference` | Optional actual cover-use review; without it public recording covers return 404/placeholders |

`init_production.py` accepts `--operator-name`, `--privacy-contact`,
`--rights-reference` and `--musicbrainz-use`. Its disposable acceptance mode uses
explicit fixture labels, not a live rights approval. Normal initialization leaves
unsupplied operator/rights values empty so Production refuses incomplete setup.
Read the [source-use matrix](source-use-review.md) before filling these fields.
A nonempty reference is an operator attestation; the software cannot grant or
independently establish a license. Last.fm remains disabled for public live use.

Visitors can read `/privacy`, `/sources` and `/support` without a guest profile.
`/api/public-information` deliberately exposes only operator/contact, policy
version and mail availability/processor, never runtime configuration or secrets.
Confirm the notice against actual host, backup and processor retention practices.

## Activate an email provider

The initial adapter is Resend behind `IEmailTransport`. No vendor SDK or paid plan
was added. Review [current terms](https://resend.com/legal/terms-of-service),
[privacy](https://resend.com/legal/privacy-policy), applicable processing terms and
[current limits](https://resend.com/docs/api-reference/rate-limit) for the actual
account/use. Configure a domain you control with the provider's required DNS and
verify DKIM/SPF/DMARC and actual sender authorization. Disable open/click tracking
in the provider; inspect a received message to confirm no tracking was added.

Supply these in private runtime configuration, never source control or chat:

| Setting | Value/requirement |
| --- | --- |
| `EmailDelivery:Enabled` | `true` only after activation; default is false |
| `EmailDelivery:PublicOrigin` | Canonical HTTPS origin, exactly matching `GuestDiscovery:PublicOrigin` |
| `EmailDelivery:FromAddress` | Verified plain sender email address |
| `EmailDelivery:ApiKey` | Private provider API credential |
| `EmailDelivery:WebhookSecret` | Private `whsec_...` signing secret for this endpoint |
| `EmailDelivery:TrackingDisabledConfirmed` | True only after checking the actual domain/provider configuration |
| `EmailDelivery:DailyLimit` | Conservative attempt budget; default 90, including security and weekly work |
| `Schedule:MaxUsersPerTick` | 1–100; default 5 |
| `Schedule:MaxDispatchesPerTick` | 1–100; default 10 |
| `ProductionWorker:TimeoutSeconds` | 30–600; default 180 |

`LocalEmail:Enabled` must remain false with external delivery. Production also
requires the existing LiveOnly ListenBrainz approval, storage admission and key
configuration. Share identical email, key and origin settings between web and
Worker. Persist and back up the key material: queued bodies and unsubscribe
capabilities must remain decryptable after a restart/restore.

Create the provider webhook targeting `POST /api/email-events` on the real HTTPS
host. Subscribe to delivered, bounced, complained, failed and suppressed email
events. Raw bytes, timestamp and Svix signature are checked before parsing;
invalid signatures fail, engagement events are ignored, duplicated/out-of-order
outcomes cannot requeue a completed dispatch. A webhook arriving before local
acceptance persistence gets 503 so the provider can retry. Deleted account data
is never recreated by an event.

## Scheduling and delivery

After reviewing/installing the updated images and schema, run one bounded tick:

```sh
python3 scripts/ops/production.py --configuration /srv/linernotes/operations.json mail
```

Review and install `deploy/systemd/linernotes-mail.service` and
`deploy/systemd/linernotes-mail.timer` under the real operator's paths/account.
The timer runs bounded work about every five minutes, with jitter. It shares the
operator lifecycle lock with backup/restore/upgrade and refuses active
maintenance. A PostgreSQL lease excludes duplicate schedulers/dispatchers.

Only active confirmed opted-in accounts without unsubscribe/suppression qualify.
Schedules retain the existing **UTC weekday/hour** semantics; a stored time-zone
label does not silently change that hour. An absent slot initializes to the next
matching slot. A tick processes a bounded due set, generates/reuses the persisted
user/week digest, queues one unique dispatch and then advances its slot. A retry
reuses those picks. Multiple overdue weeks catch up through bounded ticks rather
than an unlimited loop. Queued mail is dispatched before music acquisition so recovery messages do not
wait behind a slow music provider. Security messages get priority while reserving
capacity for weekly messages. A remaining bounded allowance runs after generation.

API acceptance is separate from recipient-server delivery. `SentAt` records
external submission acceptance; `DeliveredAt` needs a provider event/reconciliation.
A recipient-server delivery event does not prove a human read the message or that
it reached the inbox rather than spam. Local captures never set external SentAt.

## Retry, reconciliation and operational checks

Requests reuse an immutable protected body, frozen sender, target and idempotency
key. The provider's documented key retention is 24 hours; automatic retry stops
before 23 hours from the first attempt, or earlier on payload expiry/attempt limit.
An ambiguous request outside that window is quarantined, never resent with a new
key. Do not manually reset its state to queued to make a warning disappear.

For a known provider message UUID, use the private operator path:

```sh
python3 scripts/ops/production.py --configuration /srv/linernotes/operations.json reconcile-mail --message-id PROVIDER_MESSAGE_UUID
```

This reads provider status and applies an outcome only to an existing local
receipt. It does not resend. If no provider ID was durably recorded, inspect the
provider's records privately; an unknown result is not evidence that nothing was
sent. Exactly-once delivery over an external network is not promised.

`/ops/status` exposes aggregate mail backlog, ambiguous/failure/suppression counts
and remaining budget, without recipient identities or bodies. The operator
status includes `last-mail`; enabled delivery with no recent timer result is
reported degraded. Configure and test the existing notification destination.
Provider throttling, bounce/complaint suppression, stale queues and exhausted
budgets need operator attention. Budget accounting is conservative across UTC
boundaries: retries may consume allowance counted from earlier attempts.

## Account and privacy behavior

Registration queues confirmation atomically when mail is enabled; confirmation
does not opt the user into weekly mail. Request endpoints return the same
acknowledgement for unknown/ineligible addresses, with IP limiting and a durable
per-account/purpose resend cooldown. Links carry capabilities in the browser
fragment, remove it after parsing and require POST submission. Confirmation
expires after 24 hours; reset expires after one hour. Reset rotates the Identity
security stamp, invalidates refresh sessions and fences stale prepared logins.
Pending security mail issued before a stamp change is cancelled before sending.
Previously issued short-lived access JWTs retain the existing expiry behavior.

Account provides confirmation status/resend and weekly preference controls.
Weekly mail has a normal unsubscribe link and one-click POST headers. GET does
not unsubscribe. External dispatch is serialized with opt-out/deletion; already
accepted mail cannot be recalled. Security mail is separate from weekly consent.

Expired payloads are cleared in bounded web maintenance and dispatch passes;
maintenance must be running. Paused/offline maintenance delays physical cleanup,
not token expiry. Terminal bodies are cleared, safe outcome metadata is kept for
90 days, and suppression remains until account deletion or reviewed recovery.
Export 2.8 includes all mail/suppression metadata and redacted retained messages;
it includes the current UTC-day attempt count alongside lifetime attempts.
capability links, credentials and security stamps are excluded. Account deletion
cascades both new tables. Independent backups/provider copies have separate
retention; do not promise their immediate erasure.

## Migration and activation acceptance

The additive `AddOutboundEmailDispatch` migration creates the two account-owned
tables and their managed-storage accounting/truncate guards. Existing Identity
columns and historical scores are not rewritten. Downgrade refuses retained
mail/suppression state or active artifact publications. Use a verified backup
and stopped writers for any retained database. The existing compatible-only
upgrade command intentionally refuses pending schema changes; do not bypass its
witness checks. Existing retained deployments need their reviewed schema/witness
transition before running new writers. A fresh production installation uses the
normal migration then storage-establishment procedure.

Before enabling public sending, exercise a developer-controlled account through
signup, received confirmation, saved taste, scheduled persisted digest, actual
accepted/delivered mail, unsubscribe, retry and a second week. Check raw mail for
public links, attribution, one-click headers/DKIM and absence of tracking. Verify
restart, export/deletion, off-host recovery and alerts. No live provider account,
domain, real sender or recipient delivery was configured during local testing.
