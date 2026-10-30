using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BatchManagedStorageAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Transition tables preserve the row-based charge and revision semantics while
            // updating the singleton ledger once per statement, rather than once per row.
            migrationBuilder.Sql("""
CREATE FUNCTION liner_managed_storage_statement_delta() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE previous_bytes bigint := 0; next_bytes bigint := 0; row_delta bigint := 0; changed_rows bigint := 0;
BEGIN
 IF TG_OP IN ('UPDATE','DELETE') THEN
  SELECT COALESCE(sum(octet_length(to_jsonb(t)::text)+512),0),count(*) INTO previous_bytes,changed_rows FROM liner_old_rows t;
 END IF;
 IF TG_OP IN ('INSERT','UPDATE') THEN
  SELECT COALESCE(sum(octet_length(to_jsonb(t)::text)+512),0),count(*) INTO next_bytes,changed_rows FROM liner_new_rows t;
 END IF;
 IF changed_rows=0 THEN RETURN NULL; END IF;
 IF TG_OP='INSERT' THEN row_delta:=changed_rows; ELSIF TG_OP='DELETE' THEN row_delta:=-changed_rows; END IF;
 UPDATE public."ManagedStorageLedger" SET "Revision"="Revision"+changed_rows,
  "LogicalBytes"="LogicalBytes"+next_bytes-previous_bytes,"RowCount"="RowCount"+row_delta,
  "CoreRevision"="CoreRevision"+CASE WHEN TG_TABLE_SCHEMA='public' AND TG_TABLE_NAME IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN changed_rows ELSE 0 END,
  "CoreLogicalBytes"="CoreLogicalBytes"+CASE WHEN TG_TABLE_SCHEMA='public' AND TG_TABLE_NAME IN ('Tracks','TasteSignals','WeeklyDigests','WeeklyRecommendations','ListenerProfiles','DiscoveryBatches','DiscoveryJobs','GuestSessions') THEN next_bytes-previous_bytes ELSE 0 END
 WHERE "Id"=1;
 IF NOT FOUND THEN RAISE EXCEPTION 'managed_storage_ledger_missing'; END IF;
 RETURN NULL;
END $$;
DO $$ DECLARE relation record; BEGIN
 FOR relation IN SELECT schemaname,tablename FROM pg_tables WHERE schemaname IN ('public','identity') AND tablename NOT IN ('__EFMigrationsHistory','ManagedStorageLedger') LOOP
  EXECUTE format('DROP TRIGGER liner_storage_delta ON %I.%I',relation.schemaname,relation.tablename);
  EXECUTE format('CREATE TRIGGER liner_storage_delta AFTER INSERT ON %I.%I REFERENCING NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta()',relation.schemaname,relation.tablename);
  EXECUTE format('CREATE TRIGGER liner_storage_update AFTER UPDATE ON %I.%I REFERENCING OLD TABLE AS liner_old_rows NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta()',relation.schemaname,relation.tablename);
  EXECUTE format('CREATE TRIGGER liner_storage_delete AFTER DELETE ON %I.%I REFERENCING OLD TABLE AS liner_old_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta()',relation.schemaname,relation.tablename);
 END LOOP;
END $$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$ DECLARE relation record; BEGIN
 FOR relation IN SELECT schemaname,tablename FROM pg_tables WHERE schemaname IN ('public','identity') AND tablename NOT IN ('__EFMigrationsHistory','ManagedStorageLedger') LOOP
  EXECUTE format('DROP TRIGGER liner_storage_delta ON %I.%I',relation.schemaname,relation.tablename);
  EXECUTE format('DROP TRIGGER liner_storage_update ON %I.%I',relation.schemaname,relation.tablename);
  EXECUTE format('DROP TRIGGER liner_storage_delete ON %I.%I',relation.schemaname,relation.tablename);
  EXECUTE format('CREATE TRIGGER liner_storage_delta AFTER INSERT OR UPDATE OR DELETE ON %I.%I FOR EACH ROW EXECUTE FUNCTION liner_managed_storage_delta()',relation.schemaname,relation.tablename);
 END LOOP;
END $$;
DROP FUNCTION liner_managed_storage_statement_delta();
""");
        }
    }
}
