using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OutboxRetryAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_outbox_status",
                table: "notification_outbox");

            migrationBuilder.AddColumn<int>(
                name: "attempts",
                table: "notification_outbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "notification_outbox",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_outbox_status",
                table: "notification_outbox",
                sql: "status IN ('pending', 'dispatched', 'skipped', 'failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_outbox_status",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "attempts",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "notification_outbox");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_outbox_status",
                table: "notification_outbox",
                sql: "status IN ('pending', 'dispatched', 'skipped')");
        }
    }
}
