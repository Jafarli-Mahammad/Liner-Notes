using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionalTasteOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ DECLARE relation record; bytes bigint:=0; core_bytes bigint:=0; rows bigint:=0; measured_bytes bigint; measured_rows bigint;
                BEGIN
                  PERFORM pg_advisory_xact_lock(1280200758);
                  FOR relation IN SELECT schemaname,tablename FROM pg_tables WHERE schemaname IN ('public','identity')
                    AND tablename NOT IN ('ManagedStorageLedger','__EFMigrationsHistory') ORDER BY schemaname,tablename
                  LOOP
                    EXECUTE format('LOCK TABLE %I.%I IN SHARE ROW EXCLUSIVE MODE',relation.schemaname,relation.tablename);
                    IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid=(quote_ident(relation.schemaname)||'.'||quote_ident(relation.tablename))::regclass AND tgname='liner_storage_delta' AND tgenabled='O') OR
                      NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid=(quote_ident(relation.schemaname)||'.'||quote_ident(relation.tablename))::regclass AND tgname='liner_storage_truncate' AND tgenabled='O') THEN
                      RAISE EXCEPTION 'uncovered managed table before schema change'; END IF;
                    EXECUTE format('SELECT COALESCE(sum(octet_length(to_jsonb(t)::text)+512),0),count(*) FROM %I.%I t',relation.schemaname,relation.tablename) INTO measured_bytes,measured_rows;
                    bytes:=bytes+measured_bytes; rows:=rows+measured_rows;
                    IF relation.schemaname='public' AND relation.tablename IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN core_bytes:=core_bytes+measured_bytes; END IF;
                  END LOOP;
                  IF NOT EXISTS (SELECT 1 FROM "ManagedStorageLedger" WHERE "Id"=1 AND "LogicalBytes"=bytes AND "CoreLogicalBytes"=core_bytes AND "RowCount"=rows) THEN
                    RAISE EXCEPTION 'managed storage accounting mismatch before schema change'; END IF;
                END $$;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE "TasteSignals" DROP CONSTRAINT "CK_TasteSignals_CatalogSelection";
                ALTER TABLE "TasteSignals" ADD CONSTRAINT "CK_TasteSignals_CatalogSelection" CHECK (
                  ("CanonicalEntityId" IS NULL AND "CatalogRecordId" IS NULL AND "SelectionJson" IS NULL) OR
                  ("CanonicalEntityId" IS NOT NULL AND "CatalogRecordId" IS NOT NULL AND "SelectionJson" IS NOT NULL AND
                   "CanonicalEntityId" <> '00000000-0000-0000-0000-000000000000'::uuid AND
                   "CatalogRecordId" <> '00000000-0000-0000-0000-000000000000'::uuid AND
                   "TargetType" IN ('Artist','Album','Track') AND "Source"='InitialSeedManual' AND
                   octet_length("SelectionJson"::text) <= 49152));
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_CatalogRecords_Identity",
                table: "CatalogRecords");

            migrationBuilder.AddColumn<string>(
                name: "OnboardingJson",
                table: "ListenerProfiles",
                type: "text",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecentRecommendations",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ArtistName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attribution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MetadataExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecentRecommendations", x => x.Sequence);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CatalogRecords_Identity",
                table: "CatalogRecords",
                sql: "(\"Kind\"='Artist' AND \"ArtistId\" IS NOT NULL AND \"AlbumId\" IS NULL) OR (\"Kind\"='Album' AND \"AlbumId\" IS NOT NULL AND \"ArtistId\" IS NULL) OR (\"Kind\"='Track' AND \"ArtistId\" IS NULL AND \"AlbumId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_RecentRecommendations_RecordingId",
                table: "RecentRecommendations",
                column: "RecordingId",
                unique: true);
            migrationBuilder.Sql("""
                CREATE TRIGGER liner_storage_delta AFTER INSERT OR UPDATE OR DELETE ON "RecentRecommendations"
                  FOR EACH ROW EXECUTE FUNCTION liner_managed_storage_delta();
                CREATE TRIGGER liner_storage_truncate BEFORE TRUNCATE ON "RecentRecommendations"
                  FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_delta();
                """);
            migrationBuilder.Sql("""
                DO $$ DECLARE relation record; bytes bigint:=0; core_bytes bigint:=0; rows bigint:=0; measured_bytes bigint; measured_rows bigint;
                BEGIN
                  PERFORM pg_advisory_xact_lock(1280200758);
                  FOR relation IN SELECT schemaname,tablename FROM pg_tables WHERE schemaname IN ('public','identity')
                    AND tablename NOT IN ('ManagedStorageLedger','__EFMigrationsHistory') ORDER BY schemaname,tablename
                  LOOP
                    EXECUTE format('LOCK TABLE %I.%I IN SHARE ROW EXCLUSIVE MODE',relation.schemaname,relation.tablename);
                    
                    EXECUTE format('SELECT COALESCE(sum(octet_length(to_jsonb(t)::text)+512),0),count(*) FROM %I.%I t',relation.schemaname,relation.tablename) INTO measured_bytes,measured_rows;
                    bytes:=bytes+measured_bytes; rows:=rows+measured_rows;
                    IF relation.schemaname='public' AND relation.tablename IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN core_bytes:=core_bytes+measured_bytes; END IF;
                  END LOOP;
                  UPDATE "ManagedStorageLedger" SET "LogicalBytes"=bytes,"CoreLogicalBytes"=core_bytes,"RowCount"=rows,
                    "Revision"="Revision"+1,"CoreRevision"="CoreRevision"+1 WHERE "Id"=1;
                END $$;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ DECLARE relation record; bytes bigint:=0; core_bytes bigint:=0; rows bigint:=0; measured_bytes bigint; measured_rows bigint;
                BEGIN
                  PERFORM pg_advisory_xact_lock(1280200758);
                  FOR relation IN SELECT schemaname,tablename FROM pg_tables WHERE schemaname IN ('public','identity')
                    AND tablename NOT IN ('ManagedStorageLedger','__EFMigrationsHistory') ORDER BY schemaname,tablename
                  LOOP
                    EXECUTE format('LOCK TABLE %I.%I IN SHARE ROW EXCLUSIVE MODE',relation.schemaname,relation.tablename);
                    IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid=(quote_ident(relation.schemaname)||'.'||quote_ident(relation.tablename))::regclass AND tgname='liner_storage_delta' AND tgenabled='O') OR
                      NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid=(quote_ident(relation.schemaname)||'.'||quote_ident(relation.tablename))::regclass AND tgname='liner_storage_truncate' AND tgenabled='O') THEN
                      RAISE EXCEPTION 'uncovered managed table before schema change'; END IF;
                    EXECUTE format('SELECT COALESCE(sum(octet_length(to_jsonb(t)::text)+512),0),count(*) FROM %I.%I t',relation.schemaname,relation.tablename) INTO measured_bytes,measured_rows;
                    bytes:=bytes+measured_bytes; rows:=rows+measured_rows;
                    IF relation.schemaname='public' AND relation.tablename IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN core_bytes:=core_bytes+measured_bytes; END IF;
                  END LOOP;
                  IF NOT EXISTS (SELECT 1 FROM "ManagedStorageLedger" WHERE "Id"=1 AND "LogicalBytes"=bytes AND "CoreLogicalBytes"=core_bytes AND "RowCount"=rows) THEN
                    RAISE EXCEPTION 'managed storage accounting mismatch before schema change'; END IF;
                END $$;
                """);
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "ListenerProfiles" WHERE "OnboardingJson" IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM "RecentRecommendations")
                    OR EXISTS (SELECT 1 FROM "CatalogRecords" WHERE "Kind"='Track') THEN
                    RAISE EXCEPTION 'Cannot discard onboarding, recording selections or public feed; export and restore explicitly';
                  END IF;
                END $$;
                """);
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "TasteSignals" WHERE "TargetType"='Track' AND "CanonicalEntityId" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot discard canonical recording favorites';
                  END IF;
                END $$;
                ALTER TABLE "TasteSignals" DROP CONSTRAINT "CK_TasteSignals_CatalogSelection";
                ALTER TABLE "TasteSignals" ADD CONSTRAINT "CK_TasteSignals_CatalogSelection" CHECK (
                  ("CanonicalEntityId" IS NULL AND "CatalogRecordId" IS NULL AND "SelectionJson" IS NULL) OR
                  ("CanonicalEntityId" IS NOT NULL AND "CatalogRecordId" IS NOT NULL AND "SelectionJson" IS NOT NULL AND
                   "CanonicalEntityId" <> '00000000-0000-0000-0000-000000000000'::uuid AND
                   "CatalogRecordId" <> '00000000-0000-0000-0000-000000000000'::uuid AND
                   "TargetType" IN ('Artist','Album') AND "Source"='InitialSeedManual' AND
                   octet_length("SelectionJson"::text) <= 49152));
                """);
            migrationBuilder.DropTable(
                name: "RecentRecommendations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CatalogRecords_Identity",
                table: "CatalogRecords");

            migrationBuilder.DropColumn(
                name: "OnboardingJson",
                table: "ListenerProfiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CatalogRecords_Identity",
                table: "CatalogRecords",
                sql: "(\"Kind\"='Artist' AND \"ArtistId\" IS NOT NULL AND \"AlbumId\" IS NULL) OR (\"Kind\"='Album' AND \"AlbumId\" IS NOT NULL AND \"ArtistId\" IS NULL)");
            migrationBuilder.Sql("""
                DO $$ DECLARE relation record; bytes bigint:=0; core_bytes bigint:=0; rows bigint:=0; measured_bytes bigint; measured_rows bigint;
                BEGIN
                  PERFORM pg_advisory_xact_lock(1280200758);
                  FOR relation IN SELECT schemaname,tablename FROM pg_tables WHERE schemaname IN ('public','identity')
                    AND tablename NOT IN ('ManagedStorageLedger','__EFMigrationsHistory') ORDER BY schemaname,tablename
                  LOOP
                    EXECUTE format('LOCK TABLE %I.%I IN SHARE ROW EXCLUSIVE MODE',relation.schemaname,relation.tablename);
                    
                    EXECUTE format('SELECT COALESCE(sum(octet_length(to_jsonb(t)::text)+512),0),count(*) FROM %I.%I t',relation.schemaname,relation.tablename) INTO measured_bytes,measured_rows;
                    bytes:=bytes+measured_bytes; rows:=rows+measured_rows;
                    IF relation.schemaname='public' AND relation.tablename IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN core_bytes:=core_bytes+measured_bytes; END IF;
                  END LOOP;
                  UPDATE "ManagedStorageLedger" SET "LogicalBytes"=bytes,"CoreLogicalBytes"=core_bytes,"RowCount"=rows,
                    "Revision"="Revision"+1,"CoreRevision"="CoreRevision"+1 WHERE "Id"=1;
                END $$;
                """);

        }
    }
}
