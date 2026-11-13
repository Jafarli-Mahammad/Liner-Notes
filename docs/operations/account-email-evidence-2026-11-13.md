# Publication and account email verification — 2026-11-13

The [approved design](../superpowers/specs/2026-11-13-publication-account-mail-design.md)
is implemented locally. Developer acceptance, provider permission, public deployment
and real delivery remain open. No production account, domain, sender, mail purchase,
external message, push or deployment was created by this implementation.

## Implemented behavior

- Anonymous privacy, source attribution and support pages; operator/contact and
  source-use configuration must be supplied before Production startup. Last.fm
  public use remains gated; unreviewed cover artwork is withheld.
- Identity email confirmation and password reset, generic request acknowledgements,
  expiring purpose-specific tokens, persistent request cooldown and security-stamp
  invalidation. Recovery links use fragments and require explicit submission.
- Encrypted durable dispatch with stable message bodies and idempotency keys;
  bounded retries and daily attempts; ambiguous sends quarantined rather than
  blindly resent. Account leases coordinate dispatch, opt-out and deletion.
- Replaceable Resend HTTP transport, signed bounded webhook validation,
  delivered/bounced/complained events, suppression and read-only reconciliation.
  Provider acceptance is distinct from delivery.
- Weekly scheduling uses stored digest recommendations and explanations, consent,
  confirmed eligible accounts, public links and one-click unsubscribe. Security
  mail is dispatched before slower acquisition; weekly work retains a fair share.
- New dispatch/suppression schema includes managed-storage accounting and
  owner-scoped export 2.7/deletion. Maintenance bounds retained mail metadata.
  Protected queued bodies and security tokens are excluded from readable exports.
- Production operator commands and systemd service/timer, operational mail summary,
  migration and activation documentation. External mail defaults to disabled.

See [the runbook](account-email.md) and [source-use review](source-use-review.md)
for exact configuration and the external decisions still needed. Software cannot
supply third-party permission or a real operator identity.

## Positive verification

Pinned user-local .NET SDK 10.0.403 was available for implementation checks;
the earlier analysis report's SDK limitation is historical. No dependency was
upgraded or removed. Existing build/analyzer warnings remain.

| Check | Evidence and limit |
| --- | --- |
| Backend build | Passed with pinned SDK; this is local evidence. |
| Domain tests | 183 passed, including mail lifecycle, retry expiry and out-of-order events. |
| Infrastructure tests | 308 passed; transport/signature/reconciliation tests use HTTP fixtures, never the delivery service. |
| Presentation tests | 152 passed; includes real PostgreSQL account/mail, unsubscribe, export, deletion and accounting coverage. |
| Application suite | 171 passed with four cases from two baseline-failing methods excluded; exclusions and failures are below. |
| Focused persistence checks | 31 passed for storage/accounting/scheduling; all eight legacy local-worker PostgreSQL tests passed in a separate focused run. |
| Browser | 16 account/publication checks passed after final preference fix; five additional review regression checks passed. Includes anonymous routes, fragment handling, pending preference rollback, keyboard/mobile layout and accessibility checks. |
| Frontend | Typecheck and local production build passed; mobile privacy/recovery screenshots inspected. |
| Operator/check scripts | 16 operator, 11 production-check and 10 Phase 9 checks passed; secret-default and renewal-doc gates passed. |
| Schema | EF-scaffolded migration plus accounting triggers; exercised against disposable PostgreSQL. No retained application/production database migrated; EF reports no pending model changes. |

PostgreSQL mail tests exercise queue encryption, confirmation/reset consumption,
expired/stale credentials, cooldown, retries across scopes, a competing worker,
provider acceptance versus delivery, complaint suppression, budgets, expiry,
export and account-deletion accounting. Two stored weekly digests spanning two
scheduled weeks produce exactly two messages despite duplicate enqueue attempts.
That scheduling test deliberately uses synthetic Live-shaped evidence and a fake
transport; it proves the protocol and persistence, not licensed live-provider use,
inbox delivery, recommendation quality or production capacity.

A separate restarted host on port 5291 and a disposable database passed real
browser signup, unconfirmed status, disabled external-mail controls, preference
save/reload, opt-out and account deletion. This was not a mocked HTTP account
journey. Its unmanaged test configuration had local capture disabled, so deletion
correctly returned the existing warning that local archive cleanup could not be
verified. SQL/account deletion succeeded; this is not evidence of verified archive
erasure. No existing user host/database was used.

## Broad-suite failures and limits

The complete suite is **not green**. No scorer, frozen recommendation expectation,
coverage limit or storage guard was changed to conceal these failures.

On untouched design revision `f42930d`, a detached worktree reproduced:

- `DiscoveryOutputContractTests.EntirePreparedResultsMatchFrozenEightGenreContract`:
  frozen output hash mismatch.
- `FavoriteEvidenceTests.ArtistRecordingAndLegacyAcquisitionShareOneFairWindowAndReceiptRemainsBounded`
  for the 1,000-favorite case: `coverage_limit`.

The two methods contain four cases: two failed and two passed on baseline.
The filtered 171-case Application run excluded both whole methods, including
their two otherwise-passing cases. It is not a complete passing Application run.

A broad DataAccess run passed 197/198, and a later full repeat passed 196/198.
Observed failing cases included
`CancellationAfterPublication_ReleasesBothLeasesAndRecovers` and
`CompletionClearsExpiredMetadataButRetainsBoundedPublicArtworkProof`, with
`database_inventory_changed`/physical-inventory drift at fault/cleanup boundaries.
The former passed when run with the local-worker class in isolation. The untouched
baseline also passed 197/198 with an inventory failure in
`PublicFeedUsesEligibleCatalogMetadataAndIsAtomicIndependentAndDeduplicated`.
This reproduces the class of suite instability, not proof that each individual
failure has the same cause. Its root cause and reliable broad-suite green status
remain unresolved. Storage admission and inventory checks remain enforced.

External acceptance still requires an actual operator/contact and source-use
review, verified sender/public HTTPS origin, private Resend credentials/webhook
setup with tracking disabled, deployed timer, real delivered confirmation/reset/
weekly/unsubscribe journey, operational monitoring and the existing release gates.
Resend reconciliation was checked against its official
[retrieve-email contract](https://resend.com/docs/api-reference/emails/retrieve-email).
No claim of legal clearance or production readiness follows from these tests.

## Commit disposition

The developer explicitly authorized committing and pushing the implementation
with the above verification failures documented. The earlier report/design commits
remain separate. This authorization does not activate email or deploy the service.
