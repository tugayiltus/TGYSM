using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendEventKindChecksForTicketLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_events_kind",
                table: "ticket_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_change_events_kind",
                table: "change_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_events_kind",
                table: "ticket_events",
                sql: "kind IN ('created', 'assigned', 'transferred', 'status_changed', 'public_comment', 'internal_note', 'priority_changed', 'approval_requested', 'approval_decided', 'approval_voided', 'approval_reassigned', 'related_request_created', 'asset_linked', 'asset_unlinked', 'completed', 'cancelled', 'change_linked', 'change_unlinked')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_change_events_kind",
                table: "change_events",
                sql: "kind IN ('created', 'updated', 'submitted', 'withdrawn', 'approved', 'rejected', 'returned', 'approval_voided', 'implementation_started', 'implementation_finished', 'backout_started', 'closed', 'cancelled', 'assets_changed', 'ticket_linked', 'ticket_unlinked')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ticket_events_kind",
                table: "ticket_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_change_events_kind",
                table: "change_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ticket_events_kind",
                table: "ticket_events",
                sql: "kind IN ('created', 'assigned', 'transferred', 'status_changed', 'public_comment', 'internal_note', 'priority_changed', 'approval_requested', 'approval_decided', 'approval_voided', 'approval_reassigned', 'related_request_created', 'asset_linked', 'asset_unlinked', 'completed', 'cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_change_events_kind",
                table: "change_events",
                sql: "kind IN ('created', 'updated', 'submitted', 'withdrawn', 'approved', 'rejected', 'returned', 'approval_voided', 'implementation_started', 'implementation_finished', 'backout_started', 'closed', 'cancelled', 'assets_changed')");
        }
    }
}
