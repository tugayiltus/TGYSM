using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlaCalendarChangedEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sla_events_kind",
                table: "sla_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sla_events_kind",
                table: "sla_events",
                sql: "kind IN ('started', 'paused', 'resumed', 'target_changed', 'warning', 'breached', 'stopped', 'calendar_changed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sla_events_kind",
                table: "sla_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sla_events_kind",
                table: "sla_events",
                sql: "kind IN ('started', 'paused', 'resumed', 'target_changed', 'warning', 'breached', 'stopped')");
        }
    }
}
