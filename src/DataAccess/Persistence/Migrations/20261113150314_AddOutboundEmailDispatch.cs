using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinerNotes.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundEmailDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DigestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipientHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedPayload = table.Column<string>(type: "character varying(350000)", maxLength: 350000, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FirstAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OutcomeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailDispatches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailSuppressions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSuppressions", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_EmailSuppressions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDispatches_ProviderMessageId",
                table: "EmailDispatches",
                column: "ProviderMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailDispatches_RequestKey",
                table: "EmailDispatches",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailDispatches_State_NextAttemptAt",
                table: "EmailDispatches",
                columns: new[] { "State", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDispatches_UserId_Purpose_CreatedAt",
                table: "EmailDispatches",
                columns: new[] { "UserId", "Purpose", "CreatedAt" });
            migrationBuilder.Sql("""
CREATE TRIGGER liner_storage_delta AFTER INSERT ON public."EmailDispatches" REFERENCING NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
CREATE TRIGGER liner_storage_update AFTER UPDATE ON public."EmailDispatches" REFERENCING OLD TABLE AS liner_old_rows NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
CREATE TRIGGER liner_storage_delete AFTER DELETE ON public."EmailDispatches" REFERENCING OLD TABLE AS liner_old_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
CREATE TRIGGER liner_storage_truncate BEFORE TRUNCATE ON public."EmailDispatches" FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_delta();
CREATE TRIGGER liner_storage_delta AFTER INSERT ON public."EmailSuppressions" REFERENCING NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
CREATE TRIGGER liner_storage_update AFTER UPDATE ON public."EmailSuppressions" REFERENCING OLD TABLE AS liner_old_rows NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
CREATE TRIGGER liner_storage_delete AFTER DELETE ON public."EmailSuppressions" REFERENCING OLD TABLE AS liner_old_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
CREATE TRIGGER liner_storage_truncate BEFORE TRUNCATE ON public."EmailSuppressions" FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_delta();
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM public."EmailDispatches") OR EXISTS (SELECT 1 FROM public."EmailSuppressions") OR EXISTS (SELECT 1 FROM public."ArtifactPublications" WHERE "Phase" NOT IN (3,4,6)) THEN
  RAISE EXCEPTION 'email_delivery_downgrade_requires_reviewed_recovery';
 END IF;
END $$;
""");
            migrationBuilder.DropTable(
                name: "EmailDispatches");

            migrationBuilder.DropTable(
                name: "EmailSuppressions");
        }
    }
}
