using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentRequestTicketFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_asset_assignments_request_ticket_id",
                table: "asset_assignments",
                column: "request_ticket_id");

            migrationBuilder.AddForeignKey(
                name: "fk_asset_assignments_tickets_request_ticket_id",
                table: "asset_assignments",
                column: "request_ticket_id",
                principalTable: "tickets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asset_assignments_tickets_request_ticket_id",
                table: "asset_assignments");

            migrationBuilder.DropIndex(
                name: "ix_asset_assignments_request_ticket_id",
                table: "asset_assignments");
        }
    }
}
