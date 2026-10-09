using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Records the post-migration steps that ran (<c>PostMigrationRunner</c>): data fixes that need application code,
    /// which an EF migration cannot call. Not part of the EF model; only the runner reads and writes it.
    /// </summary>
    public partial class MaintenanceSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("CREATE TABLE maintenance_steps (id text PRIMARY KEY, completed_at timestamptz NOT NULL);");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("DROP TABLE maintenance_steps;");
    }
}
