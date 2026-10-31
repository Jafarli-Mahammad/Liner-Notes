using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArtifactPublicationReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArtifactPublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Root = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FinalName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StageName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReservedBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastWriteTimeUtcTicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactPublications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPublications_OwnerId_Id", table: "ArtifactPublications", columns: new[] { "OwnerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPublications_Phase",
                table: "ArtifactPublications",
                column: "Phase");

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPublications_Root_FinalName",
                table: "ArtifactPublications",
                columns: new[] { "Root", "FinalName" },
                unique: true,
                filter: "\"Phase\" NOT IN (4,6)");
            migrationBuilder.Sql("""
ALTER TABLE "ArtifactPublications" ADD CONSTRAINT "CK_ArtifactPublications_State" CHECK
 ("Id" <> '00000000-0000-0000-0000-000000000000'::uuid AND "Phase" BETWEEN 0 AND 7 AND "Version">=0 AND "LastWriteTimeUtcTicks">=0 AND
  "ReservedBytes">0 AND "ReservedBytes"<=67108864 AND "ContentBytes"="ReservedBytes" AND "Sha256" ~ '^[0-9a-f]{64}$' AND
  "StageName"=replace("Id"::text,'-','')||'.staged' AND "StageName"<>"FinalName" AND
  ("Category"<>'copies' OR "OwnerId" IS NOT NULL));
CREATE TRIGGER liner_storage_delta AFTER INSERT ON "ArtifactPublications" REFERENCING NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
CREATE TRIGGER liner_storage_update AFTER UPDATE ON "ArtifactPublications" REFERENCING OLD TABLE AS liner_old_rows NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
CREATE TRIGGER liner_storage_delete AFTER DELETE ON "ArtifactPublications" REFERENCING OLD TABLE AS liner_old_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
CREATE TRIGGER liner_storage_truncate BEFORE TRUNCATE ON "ArtifactPublications" FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_delta();
CREATE FUNCTION liner_require_managed_commit_witness() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ledger public."ManagedStorageLedger"; witness public."ManagedStorageCommit";
BEGIN
 SELECT * INTO STRICT ledger FROM public."ManagedStorageLedger" WHERE "Id"=1;
 -- Earlier statement events need not recheck the same final counters.
 IF NEW."Revision" <> ledger."Revision" THEN RETURN NULL; END IF;
 SELECT * INTO STRICT witness FROM public."ManagedStorageCommit" WHERE "Id"=1;
 IF witness."Initialized" AND
   (witness."ExpectedRevision"::bigint <> ledger."Revision" OR
    witness."ExpectedLogicalBytes"::bigint <> ledger."LogicalBytes" OR
    witness."ExpectedCoreRevision"::bigint <> ledger."CoreRevision" OR
    witness."ExpectedCoreLogicalBytes"::bigint <> ledger."CoreLogicalBytes" OR
    witness."ExpectedRowCount"::bigint <> ledger."RowCount") THEN
   RAISE EXCEPTION 'managed commit witness required' USING ERRCODE='23514', CONSTRAINT='managed_storage_commit_witness';
 END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER liner_storage_commit_witness AFTER UPDATE ON "ManagedStorageLedger"
 DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION liner_require_managed_commit_witness();
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM "ArtifactPublications" WHERE "Phase" NOT IN (3,4,6)) THEN
   RAISE EXCEPTION 'recover active artifact publications before rollback';
 END IF;
END $$;
""");
            migrationBuilder.Sql("DROP TRIGGER liner_storage_commit_witness ON \"ManagedStorageLedger\"; DROP FUNCTION liner_require_managed_commit_witness();");
            migrationBuilder.Sql("DELETE FROM \"ArtifactPublications\"");
            migrationBuilder.DropTable(
                name: "ArtifactPublications");
        }
    }
}
