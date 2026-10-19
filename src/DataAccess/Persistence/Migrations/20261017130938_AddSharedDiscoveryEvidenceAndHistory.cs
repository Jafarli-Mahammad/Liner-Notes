using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedDiscoveryEvidenceAndHistory : Migration
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
            migrationBuilder.AddColumn<string>(
                name: "CoverageJson",
                table: "DiscoveryBatches",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PublicEvidenceCache",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StoredBytes = table.Column<long>(type: "bigint", nullable: false, computedColumnSql: "COALESCE(octet_length(\"PayloadJson\"),0)", stored: true),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEvidenceCache", x => x.Key);
                    table.CheckConstraint("CK_PublicEvidenceCache_Origin", "\"Origin\" IS NULL OR \"Origin\" IN ('Live','Synthetic')");
                    table.CheckConstraint("CK_PublicEvidenceCache_Payload", "\"PayloadJson\" IS NULL OR (octet_length(\"PayloadJson\") <= 2097152 AND jsonb_typeof(\"PayloadJson\"::jsonb)='object')");
                });

            migrationBuilder.CreateTable(
                name: "RecommendedIdentities",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false, collation: "C"),
                    FirstRecommendedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendedIdentities", x => new { x.ProfileId, x.Alias });
                    table.ForeignKey(
                        name: "FK_RecommendedIdentities_ListenerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ListenerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DiscoveryBatches_Coverage",
                table: "DiscoveryBatches",
                sql: "\"CoverageJson\" IS NULL OR octet_length(\"CoverageJson\"::text)<=32768");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEvidenceCache_ExpiresAtUtc_Key",
                table: "PublicEvidenceCache",
                columns: new[] { "ExpiresAtUtc", "Key" });
            migrationBuilder.Sql("""
                CREATE TRIGGER liner_storage_delta AFTER INSERT OR UPDATE OR DELETE ON "PublicEvidenceCache" FOR EACH ROW EXECUTE FUNCTION liner_managed_storage_delta();
                CREATE TRIGGER liner_storage_truncate BEFORE TRUNCATE ON "PublicEvidenceCache" FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_delta();
                CREATE TRIGGER liner_storage_delta AFTER INSERT OR UPDATE OR DELETE ON "RecommendedIdentities" FOR EACH ROW EXECUTE FUNCTION liner_managed_storage_delta();
                CREATE TRIGGER liner_storage_truncate BEFORE TRUNCATE ON "RecommendedIdentities" FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_delta();
                CREATE FUNCTION liner_recommended_identity() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  INSERT INTO "RecommendedIdentities" ("ProfileId","Alias","FirstRecommendedAtUtc")
                    SELECT NEW."ProfileId",a.alias,NEW."CreatedAt" FROM "Tracks" t
                    CROSS JOIN LATERAL (VALUES (t."NormalizedArtistName"||':'||t."NormalizedTitle"),
                      (CASE WHEN NULLIF(t."Mbid",'') IS NOT NULL THEN 'mbid:'||lower(t."Mbid") END)) AS a(alias)
                    WHERE t."Id"=NEW."TrackId" AND a.alias IS NOT NULL
                    ON CONFLICT ("ProfileId","Alias") DO UPDATE SET "FirstRecommendedAtUtc"=LEAST("RecommendedIdentities"."FirstRecommendedAtUtc",EXCLUDED."FirstRecommendedAtUtc");
                  RETURN NULL;
                END $$;
                CREATE TRIGGER liner_recommendation_history AFTER INSERT OR UPDATE OF "ProfileId" ON "WeeklyRecommendations"
                  FOR EACH ROW EXECUTE FUNCTION liner_recommended_identity();
                INSERT INTO "RecommendedIdentities" ("ProfileId","Alias","FirstRecommendedAtUtc")
                  SELECT r."ProfileId",a.alias,min(r."CreatedAt") FROM "WeeklyRecommendations" r JOIN "Tracks" t ON t."Id"=r."TrackId"
                  CROSS JOIN LATERAL (VALUES (t."NormalizedArtistName"||':'||t."NormalizedTitle"),
                    (CASE WHEN NULLIF(t."Mbid",'') IS NOT NULL THEN 'mbid:'||lower(t."Mbid") END)) AS a(alias)
                  WHERE a.alias IS NOT NULL GROUP BY r."ProfileId",a.alias
                  ON CONFLICT ("ProfileId","Alias") DO UPDATE SET "FirstRecommendedAtUtc"=LEAST("RecommendedIdentities"."FirstRecommendedAtUtc",EXCLUDED."FirstRecommendedAtUtc");
                
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
                
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "DiscoveryBatches" WHERE "CoverageJson" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot downgrade with discovery coverage history; export and restore explicitly'; END IF;
                  IF EXISTS (
                    SELECT 1 FROM "RecommendedIdentities" h LEFT JOIN (
                      SELECT r."ProfileId",a.alias,min(r."CreatedAt") AS first_at FROM "WeeklyRecommendations" r JOIN "Tracks" t ON t."Id"=r."TrackId"
                      CROSS JOIN LATERAL (VALUES (t."NormalizedArtistName"||':'||t."NormalizedTitle"),
                        (CASE WHEN NULLIF(t."Mbid",'') IS NOT NULL THEN 'mbid:'||lower(t."Mbid") END)) AS a(alias)
                      WHERE a.alias IS NOT NULL GROUP BY r."ProfileId",a.alias
                    ) r ON r."ProfileId"=h."ProfileId" AND r.alias=h."Alias"
                    WHERE r.first_at IS NULL OR r.first_at<>h."FirstRecommendedAtUtc"
                  ) THEN RAISE EXCEPTION 'Cannot discard independent recommendation history'; END IF;
                END $$;
                DROP TRIGGER liner_recommendation_history ON "WeeklyRecommendations";
                DROP FUNCTION liner_recommended_identity();
                """);
            migrationBuilder.DropTable(
                name: "PublicEvidenceCache");

            migrationBuilder.DropTable(
                name: "RecommendedIdentities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DiscoveryBatches_Coverage",
                table: "DiscoveryBatches");

            migrationBuilder.DropColumn(
                name: "CoverageJson",
                table: "DiscoveryBatches");
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
