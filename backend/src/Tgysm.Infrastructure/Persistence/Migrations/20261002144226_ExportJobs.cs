using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "export_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    metric = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bucket = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    part = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    filters = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: true),
                    file_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_export_jobs", x => x.id);
                    table.CheckConstraint("ck_export_jobs_part", "part IN ('numerator', 'denominator')");
                    table.CheckConstraint("ck_export_jobs_report", "report IN ('incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty')");
                    table.CheckConstraint("ck_export_jobs_status", "status IN ('pending', 'running', 'ready', 'failed', 'expired')");
                    table.ForeignKey(
                        name: "fk_export_jobs_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_export_jobs_created_by_created_at",
                table: "export_jobs",
                columns: new[] { "created_by", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_export_jobs_status",
                table: "export_jobs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "export_jobs");
        }
    }
}
