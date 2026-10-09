using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssetCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "model_id",
                table: "assets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "brands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_models", x => x.id);
                    table.CheckConstraint("ck_asset_models_type", "type IN ('desktop', 'laptop', 'phone', 'tablet')");
                    table.ForeignKey(
                        name: "fk_asset_models_brands_brand_id",
                        column: x => x.brand_id,
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assets_model_id",
                table: "assets",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_models_brand_id_type_normalized_name",
                table: "asset_models",
                columns: new[] { "brand_id", "type", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_brands_normalized_name",
                table: "brands",
                column: "normalized_name",
                unique: true);

            // Existing free-text brand/model pairs become catalog entries (ADR-0023). The key mirrors CatalogName.Normalize:
            // whitespace collapsed, then Turkish lower case with I, İ and ı folded to i.
            migrationBuilder.Sql(@"
WITH src AS (SELECT DISTINCT regexp_replace(trim(brand), '\s+', ' ', 'g') AS name FROM assets),
keyed AS (SELECT lower(translate(name, 'Iİı', 'iii')) AS key, min(name) AS name FROM src GROUP BY 1)
INSERT INTO brands (id, name, normalized_name, is_active, created_at, updated_at)
SELECT gen_random_uuid(), name, key, true, now(), now() FROM keyed;

WITH src AS (
    SELECT DISTINCT lower(translate(regexp_replace(trim(brand), '\s+', ' ', 'g'), 'Iİı', 'iii')) AS brand_key, type,
           regexp_replace(trim(model), '\s+', ' ', 'g') AS name
    FROM assets),
keyed AS (SELECT brand_key, type, lower(translate(name, 'Iİı', 'iii')) AS model_key, min(name) AS name FROM src GROUP BY brand_key, type, 3)
INSERT INTO asset_models (id, brand_id, type, name, normalized_name, is_active, created_at, updated_at)
SELECT gen_random_uuid(), b.id, k.type, k.name, k.model_key, true, now(), now()
FROM keyed k JOIN brands b ON b.normalized_name = k.brand_key;

UPDATE assets a SET model_id = m.id, brand = b.name, model = m.name
FROM asset_models m JOIN brands b ON b.id = m.brand_id
WHERE b.normalized_name = lower(translate(regexp_replace(trim(a.brand), '\s+', ' ', 'g'), 'Iİı', 'iii'))
  AND m.type = a.type
  AND m.normalized_name = lower(translate(regexp_replace(trim(a.model), '\s+', ' ', 'g'), 'Iİı', 'iii'));
");

            migrationBuilder.AddForeignKey(
                name: "fk_assets_asset_models_model_id",
                table: "assets",
                column: "model_id",
                principalTable: "asset_models",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assets_asset_models_model_id",
                table: "assets");

            migrationBuilder.DropTable(
                name: "asset_models");

            migrationBuilder.DropTable(
                name: "brands");

            migrationBuilder.DropIndex(
                name: "ix_assets_model_id",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "model_id",
                table: "assets");
        }
    }
}
