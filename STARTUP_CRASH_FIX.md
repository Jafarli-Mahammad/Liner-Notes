# Local startup crash — 2026-10-30

The Rider process aborted because its existing local database had not received the three storage migrations. The first startup query failed with PostgreSQL `42P01: relation "ManagedStorageCommit" does not exist`; the unhandled managed exception produced exit 134 and an OS core dump.

The application now translates missing witness tables/columns into `storage_schema_upgrade_required`. Expected storage startup failures print upgrade/recovery instructions and exit **2**, retaining the fail-closed boundary. Missing tables are never treated as an empty valid inventory, and startup never applies migrations or blindly reconciles files automatically.

Two real child-process PostgreSQL regressions cover the old schema and a migrated but uninitialized witness. Both stop before listening, include actionable instructions and exit 2 without an unhandled abort. A separate regression ensures verified establishment refuses changed artifacts without replacing the original inventory. The original reconciliation API remains compatible; the strict operator variant additionally compares all measured categories to the expected baseline under filesystem ownership before publishing any witness.

## Local database upgrade

Only the local development database was upgraded. Production was not deployed, the Rider process was not attached to, and the original core dump was not opened or deleted.

Before migration, read-only checks confirmed that all six artifact-category hashes and the original database revision matched the saved inventory. The categories contained no artifact payload files. While holding database write coordination, the upgrade created a private custom-format PostgreSQL backup, retained the matching settings and original inventory, validated the archive by processing it through `pg_restore` without connecting to a database, and applied exactly these migrations:

- `20261029191030_AddManagedStorageCommitWitness`
- `20261029193819_BatchManagedStorageAccounting`
- `20261029194803_AddArtifactPublicationReservations`

Original row counts and aggregate SHA-256 fingerprints matched before and after across **28 existing tables**. These comparisons excluded the intentionally changed accounting ledger and migration history. Row fingerprints used PostgreSQL's [documented SHA-256 binary function](https://www.postgresql.org/docs/18/functions-binarystring.html), without exporting raw row contents into diagnostic logs. Full accounting verification and strict comparison to the original artifact baseline passed before establishing the new witness. The managed storage startup service then passed validation. Provider/application workers remained stopped during the upgrade.

The retained backup is `.linernotes/upgrade-backups/20261030T183035Z/`: `database.dump`, `settings.json`, `inventory.before.json` and `verification.json`. Its directory is private and files are readable/writable only by the owner. It contains credentials and user data and must remain private and untracked. Earlier duplicate attempt backups were removed after successful verification.

The first backup-check helper exited early on a broken pipe because `pg_restore --list` did not consume the whole input stream. No migration had run. The corrected check consumes the archive completely. The helper's OS-created dump was removed; the original application's dump remains untouched. No raw dump was analyzed.

## Verification

Updated affected suites passed against disposable PostgreSQL databases: **116 Presentation, 168 DataAccess and 260 Infrastructure tests**, zero failures/skips. An actual temporary Presentation host using the upgraded local database returned **HTTP 200 at `/health`** with provider jobs disabled and was then stopped. The local upgrade preserves existing data; the tests do not use the developer's accounts as fixtures. See the accompanying storage implementation report for the original broad verification and performance evidence.
