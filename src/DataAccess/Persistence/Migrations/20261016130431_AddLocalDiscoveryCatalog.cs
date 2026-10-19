using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalDiscoveryCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReleaseGroupMbid",
                table: "Albums",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlbumCredits",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumCredits", x => new { x.AlbumId, x.Position });
                    table.ForeignKey(
                        name: "FK_AlbumCredits_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumCredits_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogPublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceUri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MetadataLicense = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TagLicense = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Attribution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArtifactBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedRecords = table.Column<int>(type: "integer", nullable: false),
                    ImportedRecords = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NormalizationVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsLookup = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogPublications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogHead",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogHead", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogHead_CatalogPublications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "CatalogPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ArtistDisplayName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Disambiguation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirstReleaseDate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AliasesJson = table.Column<string>(type: "jsonb", nullable: false),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreditsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    RowSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRecords", x => x.Id);
                    table.UniqueConstraint("AK_CatalogRecords_Id_Kind", x => new { x.Id, x.Kind });
                    table.CheckConstraint("CK_CatalogRecords_Identity", "(\"Kind\"='Artist' AND \"ArtistId\" IS NOT NULL AND \"AlbumId\" IS NULL) OR (\"Kind\"='Album' AND \"AlbumId\" IS NOT NULL AND \"ArtistId\" IS NULL)");
                    table.CheckConstraint("CK_CatalogRecords_JsonBound", "octet_length(\"AliasesJson\"::text)<=16384 AND octet_length(\"TagsJson\"::text)<=16384 AND octet_length(\"CreditsJson\"::text)<=16384");
                    table.ForeignKey(
                        name: "FK_CatalogRecords_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatalogRecords_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatalogRecords_CatalogPublications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "CatalogPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSearchTerms",
                columns: table => new
                {
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedTerm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, collation: "C"),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSearchTerms", x => new { x.RecordId, x.NormalizedTerm });
                    table.ForeignKey(
                        name: "FK_CatalogSearchTerms_CatalogRecords_RecordId_Kind",
                        columns: x => new { x.RecordId, x.Kind },
                        principalTable: "CatalogRecords",
                        principalColumns: new[] { "Id", "Kind" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Albums_ReleaseGroupMbid",
                table: "Albums",
                column: "ReleaseGroupMbid",
                unique: true,
                filter: "\"ReleaseGroupMbid\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumCredits_AlbumId_ArtistId",
                table: "AlbumCredits",
                columns: new[] { "AlbumId", "ArtistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlbumCredits_ArtistId",
                table: "AlbumCredits",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogHead_PublicationId",
                table: "CatalogHead",
                column: "PublicationId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogPublications_State_AcquiredAtUtc",
                table: "CatalogPublications",
                columns: new[] { "State", "AcquiredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRecords_AlbumId",
                table: "CatalogRecords",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRecords_ArtistId",
                table: "CatalogRecords",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRecords_PublicationId_Kind_CanonicalId",
                table: "CatalogRecords",
                columns: new[] { "PublicationId", "Kind", "CanonicalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSearchTerms_NormalizedTerm_Kind_RecordId",
                table: "CatalogSearchTerms",
                columns: new[] { "NormalizedTerm", "Kind", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSearchTerms_RecordId_Kind",
                table: "CatalogSearchTerms",
                columns: new[] { "RecordId", "Kind" });
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pg_trgm;
                CREATE INDEX "IX_CatalogSearchTerms_ArtistTrigram" ON "CatalogSearchTerms"
                    USING gist ("NormalizedTerm" gist_trgm_ops(siglen=64)) WHERE "Kind"='Artist';
                CREATE INDEX "IX_CatalogSearchTerms_AlbumTrigram" ON "CatalogSearchTerms"
                    USING gist ("NormalizedTerm" gist_trgm_ops(siglen=64)) WHERE "Kind"='Album';
                CREATE INDEX "IX_Artists_LowerMbid" ON "Artists" (lower("Mbid")) WHERE "Mbid" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Artists_LowerMbid";""");
            migrationBuilder.DropTable(
                name: "AlbumCredits");

            migrationBuilder.DropTable(
                name: "CatalogHead");

            migrationBuilder.DropTable(
                name: "CatalogSearchTerms");

            migrationBuilder.DropTable(
                name: "CatalogRecords");

            migrationBuilder.DropTable(
                name: "CatalogPublications");

            migrationBuilder.DropIndex(
                name: "IX_Albums_ReleaseGroupMbid",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "ReleaseGroupMbid",
                table: "Albums");
        }
    }
}
