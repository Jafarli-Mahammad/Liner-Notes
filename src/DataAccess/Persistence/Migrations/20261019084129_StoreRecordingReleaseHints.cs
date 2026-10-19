using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreRecordingReleaseHints : Migration
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
                name: "ReleasesJson",
                table: "CatalogRecords",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
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
                  IF EXISTS (SELECT 1 FROM "CatalogRecords" WHERE "ReleasesJson" <> '[]'::jsonb) THEN
                    RAISE EXCEPTION 'Cannot discard recording release hints; export and restore explicitly';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "ReleasesJson",
                table: "CatalogRecords");
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
