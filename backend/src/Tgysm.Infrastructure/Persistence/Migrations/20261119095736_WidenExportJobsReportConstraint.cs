using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WidenExportJobsReportConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_export_jobs_report",
                table: "export_jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_export_jobs_report",
                table: "export_jobs",
                sql: "report IN ('incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty', 'changes', 'tasks')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_export_jobs_report",
                table: "export_jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_export_jobs_report",
                table: "export_jobs",
                sql: "report IN ('incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty')");
        }
    }
}
