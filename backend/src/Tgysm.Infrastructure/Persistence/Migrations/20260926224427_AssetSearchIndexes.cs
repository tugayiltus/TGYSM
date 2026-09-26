using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssetSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_assignee_id_updated_at",
                table: "tickets",
                columns: new[] { "assignee_id", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_search_text",
                table: "tickets",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_team_id_updated_at",
                table: "tickets",
                columns: new[] { "team_id", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_updated_at_id",
                table: "tickets",
                columns: new[] { "updated_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_assets_normalized_asset_tag1",
                table: "assets",
                column: "normalized_asset_tag")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_assets_normalized_serial_number1",
                table: "assets",
                column: "normalized_serial_number")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        
            // Expression indexes EF cannot model: case-insensitive brand/model search in ListAssets.
            migrationBuilder.Sql("CREATE INDEX ix_assets_model_trgm ON assets USING gin (lower(model) gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX ix_assets_brand_trgm ON assets USING gin (lower(brand) gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_assets_model_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_assets_brand_trgm;");

            migrationBuilder.DropIndex(
                name: "ix_tickets_assignee_id_updated_at",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_search_text",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_team_id_updated_at",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_updated_at_id",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_assets_normalized_asset_tag1",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_assets_normalized_serial_number1",
                table: "assets");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
