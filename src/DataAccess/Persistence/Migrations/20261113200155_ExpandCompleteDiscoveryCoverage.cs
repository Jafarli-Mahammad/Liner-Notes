using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCompleteDiscoveryCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DiscoveryBatches_Coverage",
                table: "DiscoveryBatches");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DiscoveryBatches_Coverage",
                table: "DiscoveryBatches",
                sql: "\"CoverageJson\" IS NULL OR octet_length(\"CoverageJson\"::text)<=524288");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DiscoveryBatches_Coverage",
                table: "DiscoveryBatches");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DiscoveryBatches_Coverage",
                table: "DiscoveryBatches",
                sql: "\"CoverageJson\" IS NULL OR octet_length(\"CoverageJson\"::text)<=262144");
        }
    }
}
