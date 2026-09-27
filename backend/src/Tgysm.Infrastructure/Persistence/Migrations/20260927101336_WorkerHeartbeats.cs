using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkerHeartbeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "worker_heartbeats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    instance = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_worker_heartbeats", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_worker_heartbeats_job_instance",
                table: "worker_heartbeats",
                columns: new[] { "job", "instance" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "worker_heartbeats");
        }
    }
}
