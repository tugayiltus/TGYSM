using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlaInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sla_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_minutes = table.Column<int>(type: "integer", nullable: false),
                    warning_percent = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    elapsed_seconds = table.Column<long>(type: "bigint", nullable: false),
                    running_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    warn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    warned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    breached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    stopped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_instances", x => x.id);
                    table.CheckConstraint("ck_sla_instances_metric", "metric IN ('first_response', 'resolution')");
                    table.CheckConstraint("ck_sla_instances_result", "result IN ('met', 'breached', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_sla_instances_sla_policy_versions_policy_version_id",
                        column: x => x.policy_version_id,
                        principalTable: "sla_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sla_instances_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sla_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_events", x => x.id);
                    table.CheckConstraint("ck_sla_events_kind", "kind IN ('started', 'paused', 'resumed', 'target_changed', 'warning', 'breached', 'stopped')");
                    table.ForeignKey(
                        name: "fk_sla_events_sla_instances_sla_instance_id",
                        column: x => x.sla_instance_id,
                        principalTable: "sla_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_pauses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sla_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_pauses", x => x.id);
                    table.CheckConstraint("ck_sla_pauses_hold_reason", "hold_reason IN ('user', 'manager_approval', 'vendor', 'technical_team', 'third_party', 'scheduled_date', 'other')");
                    table.ForeignKey(
                        name: "fk_sla_pauses_sla_instances_sla_instance_id",
                        column: x => x.sla_instance_id,
                        principalTable: "sla_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sla_events_sla_instance_id_occurred_at",
                table: "sla_events",
                columns: new[] { "sla_instance_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sla_instances_due_open",
                table: "sla_instances",
                column: "due_at",
                filter: "stopped_at IS NULL AND due_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sla_instances_policy_version_id",
                table: "sla_instances",
                column: "policy_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_sla_instances_ticket_id_metric",
                table: "sla_instances",
                columns: new[] { "ticket_id", "metric" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_instances_warn_open",
                table: "sla_instances",
                column: "warn_at",
                filter: "stopped_at IS NULL AND warn_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sla_pauses_sla_instance_id",
                table: "sla_pauses",
                column: "sla_instance_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sla_events");

            migrationBuilder.DropTable(
                name: "sla_pauses");

            migrationBuilder.DropTable(
                name: "sla_instances");
        }
    }
}
