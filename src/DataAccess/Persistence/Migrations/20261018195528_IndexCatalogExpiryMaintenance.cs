using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndexCatalogExpiryMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CatalogPublications_State_ExpiresAtUtc_Id",
                table: "CatalogPublications",
                columns: new[] { "State", "ExpiresAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogPublications_State_ExpiresAtUtc_Id",
                table: "CatalogPublications");
        }
    }
}
