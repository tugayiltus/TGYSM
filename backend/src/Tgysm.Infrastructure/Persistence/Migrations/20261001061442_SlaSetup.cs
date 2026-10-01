using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlaSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_calendar",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_calendar", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "business_hours",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekday = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ends_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_hours", x => x.id);
                    table.CheckConstraint("ck_business_hours_range", "starts_at < ends_at");
                    table.CheckConstraint("ck_business_hours_weekday", "weekday BETWEEN 0 AND 6");
                });

            migrationBuilder.CreateTable(
                name: "holidays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holidays", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sla_policy_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_policy_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sla_pause_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pauses_first_response = table.Column<bool>(type: "boolean", nullable: false),
                    pauses_resolution = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_pause_rules", x => x.id);
                    table.CheckConstraint("ck_sla_pause_rules_hold_reason", "hold_reason IN ('user', 'manager_approval', 'vendor', 'technical_team', 'third_party', 'scheduled_date', 'other')");
                    table.ForeignKey(
                        name: "fk_sla_pause_rules_sla_policy_versions_policy_version_id",
                        column: x => x.policy_version_id,
                        principalTable: "sla_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    request_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metric = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_minutes = table.Column<int>(type: "integer", nullable: false),
                    warning_percent = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_targets", x => x.id);
                    table.CheckConstraint("ck_sla_targets_kind", "ticket_kind IN ('incident', 'request')");
                    table.CheckConstraint("ck_sla_targets_metric", "metric IN ('first_response', 'resolution')");
                    table.CheckConstraint("ck_sla_targets_minutes", "target_minutes > 0");
                    table.CheckConstraint("ck_sla_targets_priority", "priority IN ('p1', 'p2', 'p3', 'p4')");
                    table.CheckConstraint("ck_sla_targets_warning", "warning_percent BETWEEN 1 AND 99");
                    table.ForeignKey(
                        name: "fk_sla_targets_request_types_request_type_id",
                        column: x => x.request_type_id,
                        principalTable: "request_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sla_targets_sla_policy_versions_policy_version_id",
                        column: x => x.policy_version_id,
                        principalTable: "sla_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_hours_weekday",
                table: "business_hours",
                column: "weekday",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_holidays_date",
                table: "holidays",
                column: "date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_pause_rules_policy_version_id_hold_reason",
                table: "sla_pause_rules",
                columns: new[] { "policy_version_id", "hold_reason" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_policy_versions_number",
                table: "sla_policy_versions",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sla_policy_versions_current",
                table: "sla_policy_versions",
                column: "is_current",
                unique: true,
                filter: "is_current");

            migrationBuilder.CreateIndex(
                name: "ix_sla_targets_policy_version_id_ticket_kind_priority_request_",
                table: "sla_targets",
                columns: new[] { "policy_version_id", "ticket_kind", "priority", "request_type_id", "metric" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_sla_targets_request_type_id",
                table: "sla_targets",
                column: "request_type_id");

            // Sample calendar and policy version 1 (spec §1, ADR-0018); defined once in SlaSeed.
            migrationBuilder.Sql(SlaSeed.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_calendar");

            migrationBuilder.DropTable(
                name: "business_hours");

            migrationBuilder.DropTable(
                name: "holidays");

            migrationBuilder.DropTable(
                name: "sla_pause_rules");

            migrationBuilder.DropTable(
                name: "sla_targets");

            migrationBuilder.DropTable(
                name: "sla_policy_versions");
        }
    }
}
