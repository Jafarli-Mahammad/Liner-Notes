using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandDiscoveryCoverageBound : Migration
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
                sql: "\"CoverageJson\" IS NULL OR octet_length(\"CoverageJson\"::text)<=262144");
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
                sql: "\"CoverageJson\" IS NULL OR octet_length(\"CoverageJson\"::text)<=32768");
        }
    }
}
