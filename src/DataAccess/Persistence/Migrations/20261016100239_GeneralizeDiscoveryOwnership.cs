using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeDiscoveryOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyDigests_DiscoveryBatches_BatchId",
                table: "WeeklyDigests");

            migrationBuilder.AlterColumn<Guid>(
                name: "WeeklyDigestId",
                table: "WeeklyRecommendations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "WeeklyRecommendations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "WeeklyRecommendations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProfileId",
                table: "WeeklyRecommendations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProfileId",
                table: "WeeklyDigests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TasteSignals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ProfileId",
                table: "TasteSignals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Cutover requires stopped old writers. Reconcile rows created since the additive migration.
            migrationBuilder.Sql("""
                INSERT INTO "ListenerProfiles" ("Id","UserId","Revision","CreatedAtUtc","LastActiveAtUtc","ExpiresAtUtc")
                SELECT "Id","Id",0,"CreatedAt",GREATEST("CreatedAt",COALESCE("LastModifiedAt","CreatedAt")),NULL
                FROM "Users" ON CONFLICT ("Id") DO NOTHING;
                INSERT INTO "DiscoveryBatches" ("Id","ProfileId","RequestId","Kind","InputRevision",
                    "CreatedAtUtc","CompletedAtUtc","CatalogVersion","EvidenceVersion")
                SELECT d."Id",p."Id",'weekly:' || d."Week",'Weekly',0,d."CreatedAt",
                    CASE WHEN EXISTS (SELECT 1 FROM "WeeklyRecommendations" r WHERE r."WeeklyDigestId"=d."Id")
                         THEN d."CreatedAt" ELSE NULL END,NULL,NULL
                FROM "WeeklyDigests" d JOIN "ListenerProfiles" p ON p."UserId"=d."UserId"
                ON CONFLICT ("Id") DO NOTHING;
                UPDATE "WeeklyDigests" d SET "ProfileId"=p."Id", "BatchId"=d."Id"
                    FROM "ListenerProfiles" p WHERE p."UserId"=d."UserId";
                UPDATE "WeeklyRecommendations" r SET "ProfileId"=d."ProfileId", "BatchId"=d."BatchId"
                    FROM "WeeklyDigests" d WHERE r."WeeklyDigestId"=d."Id";
                UPDATE "TasteSignals" s SET "ProfileId"=p."Id"
                    FROM "ListenerProfiles" p WHERE p."UserId"=s."UserId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyRecommendations_BatchId_ProfileId",
                table: "WeeklyRecommendations",
                columns: new[] { "BatchId", "ProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyRecommendations_BatchId_Rank",
                table: "WeeklyRecommendations",
                columns: new[] { "BatchId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyRecommendations_ProfileId_CreatedAt",
                table: "WeeklyRecommendations",
                columns: new[] { "ProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyDigests_BatchId_ProfileId",
                table: "WeeklyDigests",
                columns: new[] { "BatchId", "ProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasteSignals_ProfileId_TargetType_NormalizedTargetValue",
                table: "TasteSignals",
                columns: new[] { "ProfileId", "TargetType", "NormalizedTargetValue" });

            migrationBuilder.AddForeignKey(
                name: "FK_TasteSignals_ListenerProfiles_ProfileId",
                table: "TasteSignals",
                column: "ProfileId",
                principalTable: "ListenerProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyDigests_DiscoveryBatches_BatchId_ProfileId",
                table: "WeeklyDigests",
                columns: new[] { "BatchId", "ProfileId" },
                principalTable: "DiscoveryBatches",
                principalColumns: new[] { "Id", "ProfileId" });

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyRecommendations_DiscoveryBatches_BatchId_ProfileId",
                table: "WeeklyRecommendations",
                columns: new[] { "BatchId", "ProfileId" },
                principalTable: "DiscoveryBatches",
                principalColumns: new[] { "Id", "ProfileId" },
                onDelete: ReferentialAction.Cascade);
            // Nullable account references are compatibility metadata, never authority. When present
            // they must agree with the owning profile; deferred checks allow atomic guest attachment.
            migrationBuilder.Sql("""
                ALTER TABLE "ListenerProfiles" ADD CONSTRAINT "AK_ListenerProfiles_Id_UserId" UNIQUE ("Id","UserId");
                ALTER TABLE "TasteSignals" ADD CONSTRAINT "FK_TasteSignals_ProfileAccount"
                    FOREIGN KEY ("ProfileId","UserId") REFERENCES "ListenerProfiles" ("Id","UserId")
                    DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE "WeeklyRecommendations" ADD CONSTRAINT "FK_Recommendations_ProfileAccount"
                    FOREIGN KEY ("ProfileId","UserId") REFERENCES "ListenerProfiles" ("Id","UserId")
                    DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE "WeeklyDigests" ADD CONSTRAINT "FK_Digests_ProfileAccount"
                    FOREIGN KEY ("ProfileId","UserId") REFERENCES "ListenerProfiles" ("Id","UserId")
                    DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE "WeeklyRecommendations" ADD CONSTRAINT "CK_Recommendations_WeeklyBatch"
                    CHECK ("WeeklyDigestId" IS NULL OR "BatchId"="WeeklyDigestId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "ListenerProfiles" WHERE "UserId" IS NULL)
                       OR EXISTS (SELECT 1 FROM "WeeklyRecommendations" WHERE "WeeklyDigestId" IS NULL) THEN
                        RAISE EXCEPTION 'Restore a backup instead of downgrading discovery data';
                    END IF;
                END $$;
                ALTER TABLE "WeeklyRecommendations" DROP CONSTRAINT "CK_Recommendations_WeeklyBatch";
                ALTER TABLE "WeeklyDigests" DROP CONSTRAINT "FK_Digests_ProfileAccount";
                ALTER TABLE "WeeklyRecommendations" DROP CONSTRAINT "FK_Recommendations_ProfileAccount";
                ALTER TABLE "TasteSignals" DROP CONSTRAINT "FK_TasteSignals_ProfileAccount";
                ALTER TABLE "ListenerProfiles" DROP CONSTRAINT "AK_ListenerProfiles_Id_UserId";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_TasteSignals_ListenerProfiles_ProfileId",
                table: "TasteSignals");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyDigests_DiscoveryBatches_BatchId_ProfileId",
                table: "WeeklyDigests");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyRecommendations_DiscoveryBatches_BatchId_ProfileId",
                table: "WeeklyRecommendations");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyRecommendations_BatchId_ProfileId",
                table: "WeeklyRecommendations");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyRecommendations_BatchId_Rank",
                table: "WeeklyRecommendations");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyRecommendations_ProfileId_CreatedAt",
                table: "WeeklyRecommendations");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyDigests_BatchId_ProfileId",
                table: "WeeklyDigests");

            migrationBuilder.DropIndex(
                name: "IX_TasteSignals_ProfileId_TargetType_NormalizedTargetValue",
                table: "TasteSignals");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "WeeklyRecommendations");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "WeeklyRecommendations");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "WeeklyDigests");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "TasteSignals");

            migrationBuilder.AlterColumn<Guid>(
                name: "WeeklyDigestId",
                table: "WeeklyRecommendations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "WeeklyRecommendations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TasteSignals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyDigests_DiscoveryBatches_BatchId",
                table: "WeeklyDigests",
                column: "BatchId",
                principalTable: "DiscoveryBatches",
                principalColumn: "Id");
        }
    }
}
