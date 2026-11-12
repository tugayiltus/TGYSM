using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LdapSignInFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ldap_sign_in_failures",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(262)", maxLength: 262, nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ldap_sign_in_failures", x => x.key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ldap_sign_in_failures");
        }
    }
}
