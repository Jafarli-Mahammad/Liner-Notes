using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedStorageLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManagedStorageLedger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CoreRevision = table.Column<long>(type: "bigint", nullable: false),
                    CoreLogicalBytes = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    LogicalBytes = table.Column<long>(type: "bigint", nullable: false),
                    RowCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedStorageLedger", x => x.Id);
                });
            migrationBuilder.Sql("""
                INSERT INTO "ManagedStorageLedger" ("Id","Revision","LogicalBytes","RowCount","CoreRevision","CoreLogicalBytes") VALUES (1,1,0,0,1,0);
                ALTER TABLE "ManagedStorageLedger" ADD CONSTRAINT "CK_ManagedStorageLedger_Values"
                    CHECK ("Id"=1 AND "Revision">0 AND "LogicalBytes">=0 AND "RowCount">=0 AND "CoreRevision">0 AND "CoreLogicalBytes">=0 AND "CoreLogicalBytes"<="LogicalBytes" AND "CoreRevision"<="Revision");
                CREATE FUNCTION liner_managed_storage_delta() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE previous_bytes bigint := 0; next_bytes bigint := 0; row_delta bigint := 0;
                BEGIN
                  IF TG_OP = 'TRUNCATE' THEN
                    EXECUTE format('SELECT COALESCE(sum(octet_length(to_jsonb(t)::text)+512),0),count(*) FROM %I.%I t',TG_TABLE_SCHEMA,TG_TABLE_NAME) INTO previous_bytes,row_delta;
                    UPDATE "ManagedStorageLedger" SET "Revision"="Revision"+1,"LogicalBytes"="LogicalBytes"-previous_bytes,"RowCount"="RowCount"-row_delta,
                      "CoreRevision"="CoreRevision"+CASE WHEN TG_TABLE_SCHEMA='public' AND TG_TABLE_NAME IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN 1 ELSE 0 END,
                      "CoreLogicalBytes"="CoreLogicalBytes"-CASE WHEN TG_TABLE_SCHEMA='public' AND TG_TABLE_NAME IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN previous_bytes ELSE 0 END WHERE "Id"=1;
                    IF NOT FOUND THEN RAISE EXCEPTION 'managed storage ledger missing'; END IF;
                    RETURN NULL;
                  END IF;
                  IF TG_OP <> 'INSERT' THEN previous_bytes := octet_length(to_jsonb(OLD)::text) + 512; END IF;
                  IF TG_OP <> 'DELETE' THEN next_bytes := octet_length(to_jsonb(NEW)::text) + 512; END IF;
                  IF TG_OP = 'INSERT' THEN row_delta := 1; ELSIF TG_OP = 'DELETE' THEN row_delta := -1; END IF;
                  UPDATE "ManagedStorageLedger" SET "Revision"="Revision"+1,
                    "LogicalBytes"="LogicalBytes"+next_bytes-previous_bytes,"RowCount"="RowCount"+row_delta,
                    "CoreRevision"="CoreRevision"+CASE WHEN TG_TABLE_SCHEMA='public' AND TG_TABLE_NAME IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN 1 ELSE 0 END,
                    "CoreLogicalBytes"="CoreLogicalBytes"+CASE WHEN TG_TABLE_SCHEMA='public' AND TG_TABLE_NAME IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN next_bytes-previous_bytes ELSE 0 END WHERE "Id"=1;
                  IF NOT FOUND THEN RAISE EXCEPTION 'managed storage ledger missing'; END IF;
                  RETURN NULL;
                END $$;
                DO $$ DECLARE relation record; bytes bigint; rows bigint;
                BEGIN
                  FOR relation IN SELECT schemaname,tablename FROM pg_tables
                    WHERE schemaname IN ('public','identity') AND tablename NOT IN ('__EFMigrationsHistory','ManagedStorageLedger')
                  LOOP
                    EXECUTE format('SELECT COALESCE(sum(octet_length(to_jsonb(t)::text)+512),0),count(*) FROM %I.%I t',relation.schemaname,relation.tablename) INTO bytes,rows;
                    UPDATE "ManagedStorageLedger" SET "LogicalBytes"="LogicalBytes"+bytes,"RowCount"="RowCount"+rows WHERE "Id"=1;
                    IF relation.schemaname='public' AND relation.tablename IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN
                      UPDATE "ManagedStorageLedger" SET "CoreLogicalBytes"="CoreLogicalBytes"+bytes WHERE "Id"=1;
                    END IF;
                    EXECUTE format('CREATE TRIGGER liner_storage_delta AFTER INSERT OR UPDATE OR DELETE ON %I.%I FOR EACH ROW EXECUTE FUNCTION liner_managed_storage_delta()',relation.schemaname,relation.tablename);
                    EXECUTE format('CREATE TRIGGER liner_storage_truncate BEFORE TRUNCATE ON %I.%I FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_delta()',relation.schemaname,relation.tablename);
                  END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ DECLARE relation record; BEGIN
                  FOR relation IN SELECT n.nspname,c.relname FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE t.tgname='liner_storage_delta'
                  LOOP
                    EXECUTE format('DROP TRIGGER liner_storage_delta ON %I.%I',relation.nspname,relation.relname);
                    EXECUTE format('DROP TRIGGER liner_storage_truncate ON %I.%I',relation.nspname,relation.relname);
                  END LOOP;
                END $$;
                DROP FUNCTION liner_managed_storage_delta();
                """);
            migrationBuilder.DropTable(
                name: "ManagedStorageLedger");
        }
    }
}
