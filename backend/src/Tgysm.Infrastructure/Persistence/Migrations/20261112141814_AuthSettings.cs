using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "entra_object_id",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ldap_object_guid",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "auth_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entra_tenant_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    entra_client_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    entra_client_secret_protected = table.Column<string>(type: "text", nullable: true),
                    entra_allowed_domains = table.Column<List<string>>(type: "text[]", nullable: false),
                    ldap_host = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    ldap_port = table.Column<int>(type: "integer", nullable: false),
                    ldap_security = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ldap_base_dn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ldap_bind_dn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ldap_bind_password_protected = table.Column<string>(type: "text", nullable: true),
                    ldap_user_filter = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    entra_tested_fingerprint = table.Column<string>(type: "text", nullable: true),
                    ldap_tested_fingerprint = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_settings", x => x.id);
                });

            // The single settings row; login stays local until an admin switches it (ADR-0030).
            migrationBuilder.Sql(
                "INSERT INTO auth_settings (id, method, entra_tenant_id, entra_client_id, entra_allowed_domains, ldap_host, ldap_port, ldap_security, ldap_base_dn, ldap_bind_dn, updated_at) " +
                "VALUES ('00000000-0000-7000-8000-00000000a07e', 'local', '', '', '{}', '', 636, 'ldaps', '', '', now())");

            migrationBuilder.CreateIndex(
                name: "ix_users_entra_object_id",
                table: "users",
                column: "entra_object_id",
                unique: true,
                filter: "entra_object_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_ldap_object_guid",
                table: "users",
                column: "ldap_object_guid",
                unique: true,
                filter: "ldap_object_guid IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_settings");

            migrationBuilder.DropIndex(
                name: "ix_users_entra_object_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_ldap_object_guid",
                table: "users");

            migrationBuilder.DropColumn(
                name: "entra_object_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ldap_object_guid",
                table: "users");
        }
    }
}
