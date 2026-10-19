using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "WeeklyRecommendations" ALTER CONSTRAINT "FK_WeeklyRecommendations_DiscoveryBatches_BatchId_ProfileId" DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE "DiscoveryJobs" ALTER CONSTRAINT "FK_DiscoveryJobs_DiscoveryBatches_ResultBatchId_ProfileId" DEFERRABLE INITIALLY DEFERRED;
                """);
            migrationBuilder.CreateTable(
                name: "GuestSessions",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestSessions", x => x.ProfileId);
                    table.ForeignKey(
                        name: "FK_GuestSessions_ListenerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ListenerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestSessions_CredentialHash",
                table: "GuestSessions",
                column: "CredentialHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "GuestSessions") THEN
                        RAISE EXCEPTION 'Restore a backup instead of dropping guest sessions';
                    END IF;
                END $$;
                ALTER TABLE "WeeklyRecommendations" ALTER CONSTRAINT "FK_WeeklyRecommendations_DiscoveryBatches_BatchId_ProfileId" NOT DEFERRABLE;
                ALTER TABLE "DiscoveryJobs" ALTER CONSTRAINT "FK_DiscoveryJobs_DiscoveryBatches_ResultBatchId_ProfileId" NOT DEFERRABLE;
                """);
            migrationBuilder.DropTable(
                name: "GuestSessions");
        }
    }
}
