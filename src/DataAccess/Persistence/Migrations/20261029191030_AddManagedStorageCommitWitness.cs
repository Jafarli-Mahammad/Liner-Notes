using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedStorageCommitWitness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManagedStorageCommit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Initialized = table.Column<bool>(type: "boolean", nullable: false),
                    CommitId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeforeInventoryJson = table.Column<string>(type: "character varying(131072)", maxLength: 131072, nullable: false),
                    ArtifactCategoriesJson = table.Column<string>(type: "character varying(131072)", maxLength: 131072, nullable: false),
                    OwnedChangesJson = table.Column<string>(type: "character varying(131072)", maxLength: 131072, nullable: false),
                    RootFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MeasuredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpectedRevision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedLogicalBytes = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedCoreRevision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedCoreLogicalBytes = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedRowCount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SchemaFingerprint = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedStorageCommit", x => x.Id);
                });
            migrationBuilder.Sql("""
                ALTER TABLE "ManagedStorageCommit" ADD CONSTRAINT "CK_ManagedStorageCommit_Singleton" CHECK ("Id"=1);
                ALTER TABLE "ManagedStorageCommit" ADD CONSTRAINT "CK_ManagedStorageCommit_Counters" CHECK (
                  "ExpectedRevision" ~ '^[0-9]{20}$' AND "ExpectedLogicalBytes" ~ '^[0-9]{20}$' AND
                  "ExpectedCoreRevision" ~ '^[0-9]{20}$' AND "ExpectedCoreLogicalBytes" ~ '^[0-9]{20}$' AND
                  "ExpectedRowCount" ~ '^[0-9]{20}$');
                CREATE TRIGGER liner_storage_delta AFTER INSERT OR UPDATE OR DELETE ON "ManagedStorageCommit"
                  FOR EACH ROW EXECUTE FUNCTION liner_managed_storage_delta();
                CREATE TRIGGER liner_storage_truncate BEFORE TRUNCATE ON "ManagedStorageCommit"
                  FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_delta();
                INSERT INTO "ManagedStorageCommit" ("Id","Initialized","CommitId","BeforeInventoryJson","ArtifactCategoriesJson",
                  "OwnedChangesJson","RootFingerprint","MeasuredAtUtc","ExpectedRevision","ExpectedLogicalBytes",
                  "ExpectedCoreRevision","ExpectedCoreLogicalBytes","ExpectedRowCount","SchemaFingerprint")
                VALUES (1,false,'00000000-0000-0000-0000-000000000000','{}','{}','[]',repeat('0',64),'1970-01-01T00:00:00Z',
                  repeat('0',20),repeat('0',20),repeat('0',20),repeat('0',20),repeat('0',20),repeat('0',32));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DROP TABLE does not fire row accounting triggers.
            migrationBuilder.Sql("DELETE FROM \"ManagedStorageCommit\";");
            migrationBuilder.DropTable(
                name: "ManagedStorageCommit");
        }
    }
}
