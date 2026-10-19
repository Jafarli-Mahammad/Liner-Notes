using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalTasteSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalEntityId",
                table: "TasteSignals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CatalogRecordId",
                table: "TasteSignals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectionJson",
                table: "TasteSignals",
                type: "jsonb",
                nullable: true);
            migrationBuilder.Sql("""
                ALTER TABLE "TasteSignals" ADD CONSTRAINT "CK_TasteSignals_CatalogSelection" CHECK (
                  ("CanonicalEntityId" IS NULL AND "CatalogRecordId" IS NULL AND "SelectionJson" IS NULL) OR
                  ("CanonicalEntityId" IS NOT NULL AND "CatalogRecordId" IS NOT NULL AND "SelectionJson" IS NOT NULL AND
                   "CanonicalEntityId" <> '00000000-0000-0000-0000-000000000000'::uuid AND
                   "CatalogRecordId" <> '00000000-0000-0000-0000-000000000000'::uuid AND
                   "TargetType" IN ('Artist','Album') AND "Source"='InitialSeedManual' AND
                   octet_length("SelectionJson"::text) <= 49152));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS (SELECT 1 FROM "TasteSignals" WHERE "CanonicalEntityId" IS NOT NULL) THEN
                RAISE EXCEPTION 'Cannot downgrade while canonical taste selections exist; export and restore explicitly';
                END IF; END $$;
                ALTER TABLE "TasteSignals" DROP CONSTRAINT "CK_TasteSignals_CatalogSelection";
                """);
            migrationBuilder.DropColumn(
                name: "CanonicalEntityId",
                table: "TasteSignals");

            migrationBuilder.DropColumn(
                name: "CatalogRecordId",
                table: "TasteSignals");

            migrationBuilder.DropColumn(
                name: "SelectionJson",
                table: "TasteSignals");
        }
    }
}
