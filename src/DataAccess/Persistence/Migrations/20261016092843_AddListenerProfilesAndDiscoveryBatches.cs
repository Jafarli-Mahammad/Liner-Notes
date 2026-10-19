using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListenerProfilesAndDiscoveryBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "WeeklyDigests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ListenerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActiveAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListenerProfiles", x => x.Id);
                    table.CheckConstraint("CK_ListenerProfiles_Lifetime", "(\"UserId\" IS NULL AND \"ExpiresAtUtc\" IS NOT NULL) OR (\"UserId\" IS NOT NULL AND \"ExpiresAtUtc\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ListenerProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscoveryBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    InputRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CatalogVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EvidenceVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryBatches", x => x.Id);
                    table.UniqueConstraint("AK_DiscoveryBatches_Id_ProfileId", x => new { x.Id, x.ProfileId });
                    table.ForeignKey(
                        name: "FK_DiscoveryBatches_ListenerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ListenerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscoveryJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    InputRevision = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProgressCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResultBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryJobs", x => x.Id);
                    table.CheckConstraint("CK_DiscoveryJobs_PayloadBound", "octet_length(\"PayloadJson\"::text) <= 16384");
                    table.CheckConstraint("CK_DiscoveryJobs_ResultBound", "\"ResultJson\" IS NULL OR octet_length(\"ResultJson\"::text) <= 32768");
                    table.ForeignKey(
                        name: "FK_DiscoveryJobs_DiscoveryBatches_ResultBatchId_ProfileId",
                        columns: x => new { x.ResultBatchId, x.ProfileId },
                        principalTable: "DiscoveryBatches",
                        principalColumns: new[] { "Id", "ProfileId" });
                    table.ForeignKey(
                        name: "FK_DiscoveryJobs_ListenerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ListenerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyDigests_BatchId",
                table: "WeeklyDigests",
                column: "BatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryBatches_ProfileId_CreatedAtUtc_Id",
                table: "DiscoveryBatches",
                columns: new[] { "ProfileId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryBatches_ProfileId_RequestId",
                table: "DiscoveryBatches",
                columns: new[] { "ProfileId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryJobs_OneUnfinishedGeneration",
                table: "DiscoveryJobs",
                column: "ProfileId",
                unique: true,
                filter: "\"Kind\" = 'Generation' AND \"State\" IN ('Pending','Running','Retryable')");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryJobs_ProfileId_RequestKey",
                table: "DiscoveryJobs",
                columns: new[] { "ProfileId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryJobs_ResultBatchId_ProfileId",
                table: "DiscoveryJobs",
                columns: new[] { "ResultBatchId", "ProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryJobs_State_NextAttemptAtUtc_Id",
                table: "DiscoveryJobs",
                columns: new[] { "State", "NextAttemptAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ListenerProfiles_ExpiresAtUtc",
                table: "ListenerProfiles",
                column: "ExpiresAtUtc",
                filter: "\"ExpiresAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ListenerProfiles_UserId",
                table: "ListenerProfiles",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyDigests_DiscoveryBatches_BatchId",
                table: "WeeklyDigests",
                column: "BatchId",
                principalTable: "DiscoveryBatches",
                principalColumn: "Id");

            // Preserve legacy IDs and score rows. This additive step does not change taste/recommendation ownership.
            migrationBuilder.Sql("""
                INSERT INTO "ListenerProfiles" ("Id","UserId","Revision","CreatedAtUtc","LastActiveAtUtc","ExpiresAtUtc")
                SELECT "Id","Id",0,"CreatedAt",GREATEST("CreatedAt",COALESCE("LastModifiedAt","CreatedAt")),NULL
                FROM "Users";

                INSERT INTO "DiscoveryBatches" ("Id","ProfileId","RequestId","Kind","InputRevision",
                    "CreatedAtUtc","CompletedAtUtc","CatalogVersion","EvidenceVersion")
                SELECT d."Id",d."UserId",'weekly:' || d."Week",'Weekly',0,d."CreatedAt",
                    CASE WHEN EXISTS (SELECT 1 FROM "WeeklyRecommendations" r WHERE r."WeeklyDigestId"=d."Id")
                         THEN d."CreatedAt" ELSE NULL END,NULL,NULL
                FROM "WeeklyDigests" d;

                UPDATE "WeeklyDigests" SET "BatchId"="Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyDigests_DiscoveryBatches_BatchId",
                table: "WeeklyDigests");

            migrationBuilder.DropTable(
                name: "DiscoveryJobs");

            migrationBuilder.DropTable(
                name: "DiscoveryBatches");

            migrationBuilder.DropTable(
                name: "ListenerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyDigests_BatchId",
                table: "WeeklyDigests");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "WeeklyDigests");
        }
    }
}
