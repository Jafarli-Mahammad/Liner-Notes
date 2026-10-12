# Phase 8a reviewed storage inventory

Review boundary: approved design in [phase-8a-review.md](phase-8a-review.md).
Export 2.2 implementation and full value/deletion verification are in progress.

This inventory lists every scalar column in the current EF relational model and
the migration history table. A column's presence here does not by itself prove
that its persisted value is exported or that deletion ran; the evidence document
records those results separately. `[]` denotes each account-owned collection item.
ScoreBreakdown is one persisted JSONB column; export preserves its complete structured
inputs, components and unknown fields. Derived explanations and WeekIdentifier are not
additional stored columns. Physical deletion includes hidden owned rows.

| Table | Column | Export path or exclusion | Deletion boundary |
|---|---|---|---|
| public.Albums | ArtistId | Excluded: shared catalog without account ownership | Retain shared records |
| public.Albums | ArtistName | Excluded: shared catalog without account ownership | Retain shared records |
| public.Albums | Id | Excluded: shared catalog without account ownership | Retain shared records |
| public.Albums | Mbid | Excluded: shared catalog without account ownership | Retain shared records |
| public.Albums | Title | Excluded: shared catalog without account ownership | Retain shared records |
| public.Artists | Id | Excluded: shared catalog without account ownership | Retain shared records |
| public.Artists | Mbid | Excluded: shared catalog without account ownership | Retain shared records |
| public.Artists | Name | Excluded: shared catalog without account ownership | Retain shared records |
| public.Artists | NormalizedName | Excluded: shared catalog without account ownership | Retain shared records |
| public.DataProtectionKeys | FriendlyName | Excluded: shared cryptographic keys | Retain shared records |
| public.DataProtectionKeys | Id | Excluded: shared cryptographic keys | Retain shared records |
| public.DataProtectionKeys | Xml | Excluded: shared cryptographic keys | Retain shared records |
| public.TasteSignals | Context | tasteSignals[].context | Remove all owned rows |
| public.TasteSignals | CreatedAt | tasteSignals[].createdAt | Remove all owned rows |
| public.TasteSignals | CreatedBy | tasteSignals[].createdBy | Remove all owned rows |
| public.TasteSignals | DeletedAt | tasteSignals[].deletedAt | Remove all owned rows |
| public.TasteSignals | DeletedBy | tasteSignals[].deletedBy | Remove all owned rows |
| public.TasteSignals | Id | tasteSignals[].id | Remove all owned rows |
| public.TasteSignals | IsDeleted | tasteSignals[].isDeleted | Remove all owned rows |
| public.TasteSignals | LastModifiedAt | tasteSignals[].lastModifiedAt | Remove all owned rows |
| public.TasteSignals | LastModifiedBy | tasteSignals[].lastModifiedBy | Remove all owned rows |
| public.TasteSignals | NormalizedTargetValue | tasteSignals[].normalizedTargetValue | Remove all owned rows |
| public.TasteSignals | Source | tasteSignals[].source | Remove all owned rows |
| public.TasteSignals | TargetType | tasteSignals[].targetType | Remove all owned rows |
| public.TasteSignals | TargetValue | tasteSignals[].targetValue | Remove all owned rows |
| public.TasteSignals | UserId | tasteSignals[].userId | Remove all owned rows |
| public.TasteSignals | Weight | tasteSignals[].weight | Remove all owned rows |
| public.Tracks | AlbumTitle | digests[].recommendations[].track.albumTitle | Retain shared catalog; remove owned recommendation references |
| public.Tracks | ArtistName | digests[].recommendations[].track.artistName | Retain shared catalog; remove owned recommendation references |
| public.Tracks | DurationSeconds | digests[].recommendations[].track.durationSeconds | Retain shared catalog; remove owned recommendation references |
| public.Tracks | ExternalSpotifyUrl | digests[].recommendations[].track.externalSpotifyUrl | Retain shared catalog; remove owned recommendation references |
| public.Tracks | ExternalYoutubeUrl | digests[].recommendations[].track.externalYoutubeUrl | Retain shared catalog; remove owned recommendation references |
| public.Tracks | Id | digests[].recommendations[].track.id | Retain shared catalog; remove owned recommendation references |
| public.Tracks | Mbid | digests[].recommendations[].track.mbid | Retain shared catalog; remove owned recommendation references |
| public.Tracks | NormalizedArtistName | digests[].recommendations[].track.normalizedArtistName | Retain shared catalog; remove owned recommendation references |
| public.Tracks | NormalizedTitle | digests[].recommendations[].track.normalizedTitle | Retain shared catalog; remove owned recommendation references |
| public.Tracks | Title | digests[].recommendations[].track.title | Retain shared catalog; remove owned recommendation references |
| public.UserMusicConnections | CreatedAt | connections[].createdAt | Remove all owned rows |
| public.UserMusicConnections | CreatedBy | connections[].createdBy | Remove all owned rows |
| public.UserMusicConnections | DeletedAt | connections[].deletedAt | Remove all owned rows |
| public.UserMusicConnections | DeletedBy | connections[].deletedBy | Remove all owned rows |
| public.UserMusicConnections | EncryptedToken | Excluded: provider credential ciphertext | Remove all owned rows |
| public.UserMusicConnections | ExternalUsername | connections[].externalUsername | Remove all owned rows |
| public.UserMusicConnections | Id | connections[].id | Remove all owned rows |
| public.UserMusicConnections | IsActive | connections[].isActive | Remove all owned rows |
| public.UserMusicConnections | IsDeleted | connections[].isDeleted | Remove all owned rows |
| public.UserMusicConnections | LastModifiedAt | connections[].lastModifiedAt | Remove all owned rows |
| public.UserMusicConnections | LastModifiedBy | connections[].lastModifiedBy | Remove all owned rows |
| public.UserMusicConnections | LastSyncedAt | connections[].lastSyncedAt | Remove all owned rows |
| public.UserMusicConnections | ServiceType | connections[].serviceType | Remove all owned rows |
| public.UserMusicConnections | UserId | connections[].userId | Remove all owned rows |
| public.Users | CreatedAt | subscriber.createdAt | Remove all owned rows |
| public.Users | CreatedBy | subscriber.createdBy | Remove all owned rows |
| public.Users | DeletedAt | subscriber.deletedAt | Remove all owned rows |
| public.Users | DeletedBy | subscriber.deletedBy | Remove all owned rows |
| public.Users | DeliveryDay | subscriber.deliveryDay | Remove all owned rows |
| public.Users | DeliveryHourUtc | subscriber.deliveryHourUtc | Remove all owned rows |
| public.Users | Email | subscriber.email | Remove all owned rows |
| public.Users | EmailUnsubscribedAtUtc | subscriber.emailUnsubscribedAtUtc | Remove all owned rows |
| public.Users | Id | subscriber.id | Remove all owned rows |
| public.Users | IsDeleted | subscriber.isDeleted | Remove all owned rows |
| public.Users | LastModifiedAt | subscriber.lastModifiedAt | Remove all owned rows |
| public.Users | LastModifiedBy | subscriber.lastModifiedBy | Remove all owned rows |
| public.Users | NextDigestAt | subscriber.nextDigestAt | Remove all owned rows |
| public.Users | TimeZone | subscriber.timeZone | Remove all owned rows |
| public.WeeklyDigests | CreatedAt | digests[].createdAt | Remove all owned rows |
| public.WeeklyDigests | CreatedBy | digests[].createdBy | Remove all owned rows |
| public.WeeklyDigests | DeletedAt | digests[].deletedAt | Remove all owned rows |
| public.WeeklyDigests | DeletedBy | digests[].deletedBy | Remove all owned rows |
| public.WeeklyDigests | ErrorMessage | digests[].errorMessage | Remove all owned rows |
| public.WeeklyDigests | Id | digests[].id | Remove all owned rows |
| public.WeeklyDigests | IsDeleted | digests[].isDeleted | Remove all owned rows |
| public.WeeklyDigests | LastModifiedAt | digests[].lastModifiedAt | Remove all owned rows |
| public.WeeklyDigests | LastModifiedBy | digests[].lastModifiedBy | Remove all owned rows |
| public.WeeklyDigests | SentAt | digests[].sentAt | Remove all owned rows |
| public.WeeklyDigests | Status | digests[].status | Remove all owned rows |
| public.WeeklyDigests | UserId | digests[].userId | Remove all owned rows |
| public.WeeklyDigests | Week | digests[].week | Remove all owned rows |
| public.WeeklyDigests | WeekEndDate | digests[].weekEndDate | Remove all owned rows |
| public.WeeklyDigests | WeekStartDate | digests[].weekStartDate | Remove all owned rows |
| public.WeeklyRecommendations | CreatedAt | digests[].recommendations[].createdAt | Remove all owned rows |
| public.WeeklyRecommendations | CreatedBy | digests[].recommendations[].createdBy | Remove all owned rows |
| public.WeeklyRecommendations | DeletedAt | digests[].recommendations[].deletedAt | Remove all owned rows |
| public.WeeklyRecommendations | DeletedBy | digests[].recommendations[].deletedBy | Remove all owned rows |
| public.WeeklyRecommendations | Feedback | digests[].recommendations[].feedback | Remove all owned rows |
| public.WeeklyRecommendations | FeedbackComment | digests[].recommendations[].feedbackComment | Remove all owned rows |
| public.WeeklyRecommendations | FeedbackGivenAt | digests[].recommendations[].feedbackGivenAt | Remove all owned rows |
| public.WeeklyRecommendations | Id | digests[].recommendations[].id | Remove all owned rows |
| public.WeeklyRecommendations | IsDeleted | digests[].recommendations[].isDeleted | Remove all owned rows |
| public.WeeklyRecommendations | LastModifiedAt | digests[].recommendations[].lastModifiedAt | Remove all owned rows |
| public.WeeklyRecommendations | LastModifiedBy | digests[].recommendations[].lastModifiedBy | Remove all owned rows |
| public.WeeklyRecommendations | Rank | digests[].recommendations[].rank | Remove all owned rows |
| public.WeeklyRecommendations | Rating | digests[].recommendations[].rating | Remove all owned rows |
| public.WeeklyRecommendations | ScoreBreakdown | digests[].recommendations[].scoreBreakdown | Remove all owned rows |
| public.WeeklyRecommendations | TrackId | digests[].recommendations[].track.id | Remove all owned rows |
| public.WeeklyRecommendations | UserId | digests[].recommendations[].userId | Remove all owned rows |
| public.WeeklyRecommendations | WeeklyDigestId | digests[].recommendations[].weeklyDigestId | Remove all owned rows |
| identity.RoleClaims | ClaimType | Excluded: authentication/authorization internal | Retain shared records |
| identity.RoleClaims | ClaimValue | Excluded: authentication/authorization internal | Retain shared records |
| identity.RoleClaims | Id | Excluded: authentication/authorization internal | Retain shared records |
| identity.RoleClaims | RoleId | Excluded: authentication/authorization internal | Retain shared records |
| identity.Roles | ConcurrencyStamp | Excluded: authentication/authorization internal | Retain shared records |
| identity.Roles | Id | Excluded: authentication/authorization internal | Retain shared records |
| identity.Roles | Name | Excluded: authentication/authorization internal | Retain shared records |
| identity.Roles | NormalizedName | Excluded: authentication/authorization internal | Retain shared records |
| identity.UserClaims | ClaimType | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserClaims | ClaimValue | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserClaims | Id | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserClaims | UserId | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserLogins | LoginProvider | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserLogins | ProviderDisplayName | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserLogins | ProviderKey | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserLogins | UserId | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserRoles | RoleId | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserRoles | UserId | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.Users | AccessFailedCount | Excluded: authentication abuse-control state | Remove all owned rows |
| identity.Users | ConcurrencyStamp | Excluded: Identity concurrency/security internal | Remove all owned rows |
| identity.Users | CreatedAt | account.createdAt | Remove all owned rows |
| identity.Users | CreatedBy | account.createdBy | Remove all owned rows |
| identity.Users | DeletedAt | account.deletedAt | Remove all owned rows |
| identity.Users | DeletedBy | account.deletedBy | Remove all owned rows |
| identity.Users | Email | account.email | Remove all owned rows |
| identity.Users | EmailConfirmed | account.emailConfirmed | Remove all owned rows |
| identity.Users | Id | account.id | Remove all owned rows |
| identity.Users | IsDeleted | account.isDeleted | Remove all owned rows |
| identity.Users | LastModifiedAt | account.lastModifiedAt | Remove all owned rows |
| identity.Users | LastModifiedBy | account.lastModifiedBy | Remove all owned rows |
| identity.Users | LockoutEnabled | Excluded: authentication abuse-control configuration | Remove all owned rows |
| identity.Users | LockoutEnd | Excluded: authentication abuse-control state | Remove all owned rows |
| identity.Users | NormalizedEmail | account.normalizedEmail | Remove all owned rows |
| identity.Users | NormalizedUserName | account.normalizedUserName | Remove all owned rows |
| identity.Users | PasswordHash | Excluded: password verifier | Remove all owned rows |
| identity.Users | PhoneNumber | account.phoneNumber | Remove all owned rows |
| identity.Users | PhoneNumberConfirmed | account.phoneNumberConfirmed | Remove all owned rows |
| identity.Users | SecurityStamp | Excluded: credential invalidation secret | Remove all owned rows |
| identity.Users | TwoFactorEnabled | Excluded: authentication configuration | Remove all owned rows |
| identity.Users | UserName | account.userName | Remove all owned rows |
| identity.UserTokens | LoginProvider | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserTokens | Name | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserTokens | UserId | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| identity.UserTokens | Value | Excluded: authentication/authorization internal | Remove account-owned rows by UserId cascade |
| public.__EFMigrationsHistory | MigrationId | Excluded: schema administration | Retain |
| public.__EFMigrationsHistory | ProductVersion | Excluded: schema administration | Retain |

## Non-table stores and writer audit

- `AuthService` registers Identity username/email and audit records; its refresh
  session writer uses `identity.UserTokens` for protected authentication state.
  Role enrollment uses `AddToRoleAsync`. Current source has no writer of personal
  custom claims or external-login profile payloads. Test fixtures seed authentication
  dependents to verify cascade deletion; a future personal claim writer requires
  review rather than inheriting a blanket exclusion.
- Shared `Tracks` hold music metadata, not a user's association; that association
  lives in recommendations and snapshots. Artists/Albums have no account-ownership
  foreign key. Their entire shared catalog is not included in a subscriber export.
- Encrypted local MIME final/partial files contain recipient, taste content,
  stored explanations, template metadata, digest/user/week IDs, message identity,
  fingerprint and unsubscribe capability inside the protected payload. Export
  exposes authenticated sanitized plaintext/HTML, digest/week/template/fingerprint
  and byte count; the capability and raw authenticated receipt stay excluded.
  Cleanup removes authenticated owned final/partial/legacy files from the configured
  sink only. Truncated, corrupt or unowned files produce bounded incomplete issues.
- `LocalRecordedGenerationManifest` is developer-supplied authorization input,
  including version/use/origin, UserId, week, expiry, developer reference, source
  manifest path/hash and response method/identity/path/hash/time/limit/origin.
  The application reads this file and does not create or enroll it automatically.
  Its account association must not be mistaken for a shared upstream response.
  It is outside the configured online account-deletion stores: the operator must
  remove account-specific permission files and copies explicitly after deletion.
  This is an unresolved operational boundary, not a claim of universal erasure.
- Recorder acquisition manifests, raw provider responses, evaluation artifacts and
  storage inventories are local ignored operator files; provider artist/tag seeds
  are shared input, not listening-history imports. Inventories record category
  paths/counts/bytes, timestamps and revisions. Batch/account leases coordinate
  work and do not constitute additional retained subscriber taste records.
  Inspect local operator artifacts for account-specific associations; no blanket
  deletion of shared recordings is adopted by Phase 8a.
- Application logging excludes raw capabilities and credentials in reviewed paths.
  This is source/test evidence for the application only; real reverse-proxy, host,
  database, browser and operator logs have not been inspected. No claim is made that
  production logs or unconfigured files contain no personal data.

## Backups and restore boundaries

Online SQL deletion and configured-sink cleanup cannot erase database/file backups,
operator previews, copied export downloads, or files outside that sink. Restore can
reintroduce deleted account rows, tokens, permission files and captures. Historical
Data Protection keys are required to authenticate/decrypt retained copies for other
accounts and cleanup, so deletion must not purge the shared key ring. Key XML is not
additionally wrapped by this application. Database, backup access and encryption
remain operator responsibilities. Backup expiry and safe restore/deletion replay
policy require hosting decisions; neither an expiry duration nor a universal erasure
guarantee is introduced here. These limits must be included in developer review.

## Consistency and capacity boundaries

Export uses stable bounded digest queries but constructs the full response in memory.
Separate SQL reads and filesystem inspection do not form an atomic point-in-time
snapshot during concurrent edits. Complete-history checks use quiescent fixtures;
production maximum history/response size and a consistent SQL/files snapshot are not
verified. A failure/cancellation must not return silently truncated account history.
An explicitly reported incomplete archive remains an honest successful SQL export.
The temporary 1 GB stop does not prevent inspection, export or deletion and is not
a capacity or backup-retention decision. No legal-compliance certification is claimed.
