using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "subject_id",
                table: "notification_outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_type",
                table: "notification_outbox",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Existing ticket events name their ticket as subject, so subject queries see old and new rows alike.
            migrationBuilder.Sql("UPDATE notification_outbox SET subject_type = 'ticket', subject_id = ticket_id WHERE ticket_id IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "work_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    search_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hold_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    hold_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    result_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_tasks", x => x.id);
                    table.CheckConstraint("ck_work_tasks_cancel_reason", "status <> 'cancelled' OR cancel_reason IS NOT NULL");
                    table.CheckConstraint("ck_work_tasks_hold", "(status = 'on_hold') = (hold_reason IS NOT NULL)");
                    table.CheckConstraint("ck_work_tasks_hold_reason", "hold_reason IN ('user', 'manager_approval', 'vendor', 'technical_team', 'third_party', 'scheduled_date', 'other')");
                    table.CheckConstraint("ck_work_tasks_priority", "priority IN ('p1', 'p2', 'p3', 'p4')");
                    table.CheckConstraint("ck_work_tasks_result", "status <> 'completed' OR result_note IS NOT NULL");
                    table.CheckConstraint("ck_work_tasks_status", "status IN ('open', 'in_progress', 'on_hold', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_work_tasks_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_tasks_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_tasks_users_assignee_id",
                        column: x => x.assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_tasks_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_assignments", x => x.id);
                    table.CheckConstraint("ck_task_assignments_reason", "reason IN ('expertise_needed', 'wrong_assignment', 'other_team_responsibility', 'authorization_needed', 'workload', 'escalation', 'location', 'other')");
                    table.ForeignKey(
                        name: "fk_task_assignments_teams_from_team_id",
                        column: x => x.from_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_assignments_teams_to_team_id",
                        column: x => x.to_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_assignments_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_assignments_users_from_assignee_id",
                        column: x => x.from_assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_assignments_users_to_assignee_id",
                        column: x => x.to_assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_assignments_work_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "work_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_events", x => x.id);
                    table.CheckConstraint("ck_task_events_kind", "kind IN ('created', 'assigned', 'transferred', 'started', 'held', 'resumed', 'completed', 'cancelled', 'reopened', 'commented')");
                    table.ForeignKey(
                        name: "fk_task_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_events_work_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "work_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_outbox_subject_type_subject_id",
                table: "notification_outbox",
                columns: new[] { "subject_type", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_task_assignments_actor_user_id",
                table: "task_assignments",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_assignments_from_assignee_id",
                table: "task_assignments",
                column: "from_assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_assignments_from_team_id",
                table: "task_assignments",
                column: "from_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_assignments_task_id_occurred_at",
                table: "task_assignments",
                columns: new[] { "task_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_task_assignments_to_assignee_id",
                table: "task_assignments",
                column: "to_assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_assignments_to_team_id",
                table: "task_assignments",
                column: "to_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_events_actor_user_id",
                table: "task_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_events_task_id_occurred_at",
                table: "task_events",
                columns: new[] { "task_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_assignee_id_status",
                table: "work_tasks",
                columns: new[] { "assignee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_created_by_id",
                table: "work_tasks",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_number",
                table: "work_tasks",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_search_text",
                table: "work_tasks",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_team_id_status",
                table: "work_tasks",
                columns: new[] { "team_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_ticket_id",
                table: "work_tasks",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_updated_at_id",
                table: "work_tasks",
                columns: new[] { "updated_at", "id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_assignments");

            migrationBuilder.DropTable(
                name: "task_events");

            migrationBuilder.DropTable(
                name: "work_tasks");

            migrationBuilder.DropIndex(
                name: "ix_notification_outbox_subject_type_subject_id",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "subject_id",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "subject_type",
                table: "notification_outbox");
        }
    }
}
