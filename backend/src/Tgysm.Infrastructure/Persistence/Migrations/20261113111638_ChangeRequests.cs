using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "change_id",
                table: "work_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phase",
                table: "work_tasks",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "change_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    search_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    justification = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    affected_service = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    risk = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    impact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    window_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    implementation_plan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    verification_plan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    backout_plan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    owner_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    actual_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    backout_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    backout_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    closing_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_requests", x => x.id);
                    table.CheckConstraint("ck_change_requests_impact", "impact IN ('high', 'medium', 'low')");
                    table.CheckConstraint("ck_change_requests_outcome", "outcome IN ('successful', 'rolled_back', 'failed')");
                    table.CheckConstraint("ck_change_requests_risk", "risk IN ('high', 'medium', 'low')");
                    table.CheckConstraint("ck_change_requests_status", "status IN ('draft', 'pending_approval', 'scheduled', 'implementing', 'verifying', 'closed', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_change_requests_teams_owner_team_id",
                        column: x => x.owner_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_requests_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_requests_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "change_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_approvals", x => x.id);
                    table.CheckConstraint("ck_change_approvals_status", "status IN ('pending', 'approved', 'rejected', 'returned', 'withdrawn', 'void')");
                    table.ForeignKey(
                        name: "fk_change_approvals_change_requests_change_id",
                        column: x => x.change_id,
                        principalTable: "change_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_approvals_users_decided_by",
                        column: x => x.decided_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "change_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_change_assets_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_assets_change_requests_change_id",
                        column: x => x.change_id,
                        principalTable: "change_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "change_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_events", x => x.id);
                    table.CheckConstraint("ck_change_events_kind", "kind IN ('created', 'updated', 'submitted', 'withdrawn', 'approved', 'rejected', 'returned', 'approval_voided', 'implementation_started', 'implementation_finished', 'backout_started', 'closed', 'cancelled', 'assets_changed')");
                    table.ForeignKey(
                        name: "fk_change_events_change_requests_change_id",
                        column: x => x.change_id,
                        principalTable: "change_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_change_id_phase",
                table: "work_tasks",
                columns: new[] { "change_id", "phase" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_tasks_one_link",
                table: "work_tasks",
                sql: "ticket_id IS NULL OR change_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_tasks_phase",
                table: "work_tasks",
                sql: "phase IN ('preparation', 'implementation', 'verification', 'backout')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_tasks_phase_with_change",
                table: "work_tasks",
                sql: "(change_id IS NULL) = (phase IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_change_approvals_change_id_created_at",
                table: "change_approvals",
                columns: new[] { "change_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_change_approvals_decided_by",
                table: "change_approvals",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ux_change_approvals_pending",
                table: "change_approvals",
                column: "change_id",
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_change_assets_asset_id",
                table: "change_assets",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_assets_change_id_asset_id",
                table: "change_assets",
                columns: new[] { "change_id", "asset_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_events_actor_user_id",
                table: "change_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_events_change_id_occurred_at",
                table: "change_events",
                columns: new[] { "change_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_created_by_id",
                table: "change_requests",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_number",
                table: "change_requests",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_owner_id_status",
                table: "change_requests",
                columns: new[] { "owner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_owner_team_id_status",
                table: "change_requests",
                columns: new[] { "owner_team_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_search_text",
                table: "change_requests",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_updated_at_id",
                table: "change_requests",
                columns: new[] { "updated_at", "id" },
                descending: new bool[0]);

            migrationBuilder.AddForeignKey(
                name: "fk_work_tasks_change_requests_change_id",
                table: "work_tasks",
                column: "change_id",
                principalTable: "change_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_work_tasks_change_requests_change_id",
                table: "work_tasks");

            migrationBuilder.DropTable(
                name: "change_approvals");

            migrationBuilder.DropTable(
                name: "change_assets");

            migrationBuilder.DropTable(
                name: "change_events");

            migrationBuilder.DropTable(
                name: "change_requests");

            migrationBuilder.DropIndex(
                name: "ix_work_tasks_change_id_phase",
                table: "work_tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_tasks_one_link",
                table: "work_tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_tasks_phase",
                table: "work_tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_tasks_phase_with_change",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "change_id",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "phase",
                table: "work_tasks");
        }
    }
}
