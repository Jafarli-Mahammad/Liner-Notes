using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Artists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Mbid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ArtistName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedArtistName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AlbumTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Mbid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ExternalSpotifyUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ExternalYoutubeUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeliveryDay = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeliveryHourUtc = table.Column<int>(type: "integer", nullable: false),
                    NextDigestAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ArtistName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Mbid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Albums_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TasteSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedTargetValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Context = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasteSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasteSignals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMusicConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalUsername = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EncryptedToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMusicConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMusicConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyDigests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Week = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WeekStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WeekEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyDigests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyDigests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WeeklyDigestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    ScoreBreakdown = table.Column<string>(type: "jsonb", nullable: false),
                    Feedback = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FeedbackComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FeedbackGivenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyRecommendations_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeeklyRecommendations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WeeklyRecommendations_WeeklyDigests_WeeklyDigestId",
                        column: x => x.WeeklyDigestId,
                        principalTable: "WeeklyDigests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Albums_ArtistId",
                table: "Albums",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_Mbid",
                table: "Albums",
                column: "Mbid");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_Mbid",
                table: "Artists",
                column: "Mbid");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_NormalizedName",
                table: "Artists",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_TasteSignals_UserId_TargetType_NormalizedTargetValue",
                table: "TasteSignals",
                columns: new[] { "UserId", "TargetType", "NormalizedTargetValue" });

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Mbid",
                table: "Tracks",
                column: "Mbid");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_NormalizedArtistName_NormalizedTitle",
                table: "Tracks",
                columns: new[] { "NormalizedArtistName", "NormalizedTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMusicConnections_UserId_ServiceType",
                table: "UserMusicConnections",
                columns: new[] { "UserId", "ServiceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NextDigestAt",
                table: "Users",
                column: "NextDigestAt");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyDigests_UserId_Week",
                table: "WeeklyDigests",
                columns: new[] { "UserId", "Week" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyDigests_UserId_WeekStartDate",
                table: "WeeklyDigests",
                columns: new[] { "UserId", "WeekStartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyRecommendations_TrackId",
                table: "WeeklyRecommendations",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyRecommendations_UserId",
                table: "WeeklyRecommendations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyRecommendations_WeeklyDigestId_Rank",
                table: "WeeklyRecommendations",
                columns: new[] { "WeeklyDigestId", "Rank" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Albums");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "TasteSignals");

            migrationBuilder.DropTable(
                name: "UserMusicConnections");

            migrationBuilder.DropTable(
                name: "WeeklyRecommendations");

            migrationBuilder.DropTable(
                name: "Artists");

            migrationBuilder.DropTable(
                name: "Tracks");

            migrationBuilder.DropTable(
                name: "WeeklyDigests");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
