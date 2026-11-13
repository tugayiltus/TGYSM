using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeApprovalPlanEditor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
