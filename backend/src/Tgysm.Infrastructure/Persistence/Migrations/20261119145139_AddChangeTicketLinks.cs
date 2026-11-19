using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeTicketLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "change_ticket_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_ticket_links", x => x.id);
                    table.CheckConstraint("ck_change_ticket_links_kind", "kind IN ('prompted', 'caused')");
                    table.ForeignKey(
                        name: "fk_change_ticket_links_change_requests_change_id",
                        column: x => x.change_id,
                        principalTable: "change_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_ticket_links_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_ticket_links_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_change_ticket_links_created_by_id",
                table: "change_ticket_links",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_ticket_links_ticket_id",
                table: "change_ticket_links",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ux_change_ticket_links_change_ticket",
                table: "change_ticket_links",
                columns: new[] { "change_id", "ticket_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "change_ticket_links");
        }
    }
}
