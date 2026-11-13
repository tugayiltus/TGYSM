using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeApprovalEditors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_change_approvals_users_plan_editor",
                table: "change_approvals");

            migrationBuilder.DropIndex(
                name: "ix_change_approvals_plan_editor",
                table: "change_approvals");

            migrationBuilder.DropColumn(
                name: "plan_editor",
                table: "change_approvals");

            migrationBuilder.CreateTable(
                name: "change_approval_editors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_approval_editors", x => x.id);
                    table.ForeignKey(
                        name: "fk_change_approval_editors_change_approvals_approval_id",
                        column: x => x.approval_id,
                        principalTable: "change_approvals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_approval_editors_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_change_approval_editors_approval_id_user_id",
                table: "change_approval_editors",
                columns: new[] { "approval_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_approval_editors_user_id",
                table: "change_approval_editors",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "change_approval_editors");

            migrationBuilder.AddColumn<Guid>(
                name: "plan_editor",
                table: "change_approvals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_approvals_plan_editor",
                table: "change_approvals",
                column: "plan_editor");

            migrationBuilder.AddForeignKey(
                name: "fk_change_approvals_users_plan_editor",
                table: "change_approvals",
                column: "plan_editor",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
