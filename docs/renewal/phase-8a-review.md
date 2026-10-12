# Phase 8a privacy verification — concrete design for review

Date: 2026-10-12. Status: direction approved; written design awaiting review.
Source revision: `64dfc11` on `Prism`.

## Intent and authorization

The developer requested Phase 8, approved starting with 8a, and explicitly selected
the sequence: implement 8a, test 8a, implement 8b, test 8b separately, then test both
together. This is the proceed decision following Phase 7's reassessment. It does
not confirm previous implementations, authorize external sending or deployment,
or settle the temporary storage threshold. Preserve this sequence in the plan.

8a verifies that account/taste records can be inspected/exported, account deletion
physically removes owned records and invalidates access, and remaining copies are
described accurately. 8b remains a separate delivery design. Its scheduling and
recovery work follows successful 8a verification; 8b changes must extend this
inventory and be covered again by the final combined checks.

## Current source findings

The current export is version 2.1. Domain profile, connections, taste signals,
digest/recommendation history, score snapshots, referenced tracks and sanitized
local email copies are exported. Export repositories bypass soft-delete filters
for owned history. Digest pages are ordered by creation timestamp and UUID.

Identity profile fields are stored separately from the Domain profile. Username,
phone number, normalized contact fields and Identity audit fields have no export
mapping. Credential/security internals must remain excluded, but excluding the
entire Identity table would also omit account profile data.

Account deletion bulk-deletes hidden Domain rows inside the Identity transaction.
JWT validation checks both profiles, and refresh validation checks account state.
Authenticated local copies are removed after the SQL commit under an account
lease. Missing/unverifiable copies produce incomplete cleanup rather than a false
erasure claim. Catalog rows, protection keys and backups are shared boundaries.

The Codebase Memory index reports changed metadata for these paths; this design
uses current files, not graph freshness, as its evidence. No new runtime results
or complete privacy coverage are claimed by this review.

## Approaches

Recommended: a complete explicit storage inventory, an additive safe Identity
profile export, and real PostgreSQL/API/file verification in the existing test
projects. This closes the visible profile omission and catches future mapped
columns without a new subsystem or database table.

A documentation-only audit is smaller but would leave the visible profile
omission. A generic database dump would expose credentials/security internals and
other subscribers' records; it is unsuitable for the account export.

## Exact export contract proposed for approval

Keep `GET /api/export/my-data`, its authentication and existing fields. Increment
`exportVersion` to `2.2`. Add an `account` object with the following Identity fields:

- `id`, `userName`, `normalizedUserName`, `email`, `normalizedEmail`,
  `emailConfirmed`, `phoneNumber`, `phoneNumberConfirmed`.
- `createdAt`, `createdBy`, `lastModifiedAt`, `lastModifiedBy`, `deletedAt`,
  `deletedBy`, `isDeleted`.

Read this record using the authenticated account ID through an Application
interface implemented in DataAccess. Return missing-account failure if Identity
or the Domain subscriber is absent. Never let a caller specify another account
through the endpoint. Preserve stored values, including nulls, rather than
reconstructing Identity values from the Domain subscriber.

Exclude Identity password hashes, security/concurrency stamps, tokens, lockout
state, failed-login counts and two-factor configuration as credential/security
internals. Document these exclusions individually. `UserMusicConnections` omits
only `EncryptedToken`, with its credential reason explicit. The export does not
include raw unsubscribe capabilities, protection XML or authenticated file receipts.

Existing export fields and score JSON remain unchanged. The data viewer must show
the added profile fields using safe text rendering. No schema migration,
dependency upgrade, scoring change, retention policy or new settings endpoint is
proposed. Export-version expectations change explicitly; existing assertions are
retained and expanded rather than weakened.

## Storage inventory and coverage

Commit a human-readable inventory under `docs/renewal/` and maintain an explicit
table/column coverage contract in existing DataAccess tests. Compare it against
the current EF relational model and the disposable PostgreSQL schema. New mapped
columns fail the coverage check until their export/deletion/boundary is reviewed.
Do not infer export completeness merely because a column name appears in a DTO.
Use distinctive persisted sentinel values and assert the resulting serialized
values/paths for every exported scalar, including audit fields and hidden history.

| Storage | Export boundary | Deletion boundary |
|---|---|---|
| `Users` | All persisted profile/schedule/opt-out/audit columns | Physically delete the account row, including hidden profiles |
| `UserMusicConnections` | All columns except credential `EncryptedToken` | Physically delete all owned rows |
| `TasteSignals` | All columns, including hidden rows and provenance | Physically delete all owned rows |
| `WeeklyDigests` | All columns and all history pages | Physically delete all owned rows |
| `WeeklyRecommendations` | All columns; `TrackId` represented by nested track ID; complete score JSON and feedback | Physically delete all owned rows |
| `Tracks` | All columns for the account's referenced tracks | Shared catalog retained; remove owned references |
| `Artists`, `Albums` | Shared catalog with no account ownership FK; not a complete catalog export | Retain shared rows; document their lack of account ownership |
| `identity.Users` | Safe profile fields listed above; individually documented security exclusions | Physically delete the account and cascaded dependent rows |
| `identity.UserClaims`, `identity.UserLogins`, `identity.UserRoles`, `identity.UserTokens` | Authentication/authorization internals excluded and documented | Verify all account-owned dependents are physically removed |
| `identity.Roles`, `identity.RoleClaims` | Shared authorization configuration excluded | Preserve shared configuration and other accounts |
| `DataProtectionKeys` | Shared cryptographic internals excluded | Preserve keys needed for other accounts and copy inspection |
| EF migration history | Operational metadata excluded | Preserve |
| Local encrypted final/partial/legacy copies | Sanitized owned message alternatives and inspection issues | Remove only authenticated owned files; report unverifiable leftovers |
| Recording/replay inputs, manifests, inventories and leases | Document synthetic/shared upstream input and operational boundaries; audit formats for account IDs | No blanket deletion of shared recordings; identify any owned metadata before claiming completeness |
| Logs, backups, operator previews and out-of-root copies | Outside the configured export store; document known formats and risks | No claim that online deletion erases these copies |

The inventory must enumerate individual columns from the model; the grouped table
above is the design boundary, not the finished inventory. Security exclusions
still count as inventoried columns. Arbitrary personal data in custom claims is
not permitted by this exclusion: audit actual writers and flag any such storage
as an export gap requiring review.

## Consistency, errors and physical deletion

Verify stable pagination across more than 200 digests with equal timestamps,
hidden rows, unknown score fields, multiple accounts and no lost/duplicate rows.
Document the existing multi-query export's consistency limits during concurrent
feedback/history updates. Do not silently introduce a new isolation contract or
claim an atomic SQL-plus-filesystem snapshot. A failed/cancelled export must fail
the request rather than return a silently truncated successful result.

Exercise deletion through the actual authenticated HTTP endpoint. Seed every
owned table, credential dependents, hidden rows and encrypted copies for two
accounts. Assert the deleted account's rows are absent with query filters
disabled, other account/copy data remains intact, shared catalog/keys remain,
access and refresh tokens fail, and unsubscribe capabilities cannot affect the
deleted account. Exercise SQL rollback and the post-commit file-failure boundary
separately. Test cleanup retry after account deletion and corrupted/unverifiable
files using fabricated disposable files only.

Fix verified deletion/export defects within the written contract. A new schema,
additional public field or expanded storage-deletion rule requires a concrete
review update. Keep backups outside the deletion transaction. The inventory
must describe that restored backups can reintroduce deleted data; operational
backup expiry/restore policy remains a hosting decision and is not invented here.

## Verification and commit sequence

1. Inventory and explicit relational-model coverage; verify every stored scalar's
   boundary and audit existing writers. Record discovered gaps without concealing
   them behind a passing test count.
2. Add the reviewed account export through Application/DataAccess, update the
   safe viewer/export contract and changelog, and add serialization/API checks.
3. Run 8a separately: inventory/export tests, PostgreSQL full-history and physical
   deletion checks, token revocation, copy ownership/failure/retry checks. Create
   focused local commits only after relevant verification passes.
4. Write 8a evidence with per-approval implemented/deferred/not-done accounting.
   Then complete the separately reviewed 8b design and implementation, run its
   dedicated checks, and finally run the combined privacy/delivery checks and
   whole solution as the developer requested.

Expected effort: two focused 8a commit sets, with a separate narrowly scoped
deletion fix only if a verified defect requires one. The cross-layer PostgreSQL,
HTTP and filesystem checks are the main work; existing Phase 1/6/7 checks are
reused. Test failures or unavailable verification are reported before committing
implementation. Passing local checks remain distinct from developer confirmation.

## Sources and remaining gates

Current source: `DataContext`, Identity/domain entities, `MappingExtensions`,
export repositories/handler, `SubscribersController`, `AuthService`, JWT events,
local capture/archive implementations and existing Phase 1/6/7 tests.

PostgreSQL's [transaction isolation documentation](https://www.postgresql.org/docs/16/transaction-iso.html)
was refreshed on 2026-10-12 for the export consistency boundary. No API, provider,
package, rate allowance or pricing integration is added by this design.

Written-spec review and a written implementation plan remain required by the
brainstorming skill. The developer's requested execution order is already
selected and must not be asked again. No popularity preference, real evaluation,
live acquisition, external sending, provisioning, public deployment, CI upgrade,
history rewrite or remote push is authorized by this review.
