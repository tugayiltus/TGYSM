using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FoldDotlessIInKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TurkishText.ToSearchKey now folds dotless ı to i; recompute stored keys to match.
            migrationBuilder.Sql("""
                UPDATE users SET search_name = replace(search_name, 'ı', 'i') WHERE search_name LIKE '%ı%';
                UPDATE departments SET normalized_name = replace(normalized_name, 'ı', 'i') WHERE normalized_name LIKE '%ı%';
                UPDATE locations SET normalized_name = replace(normalized_name, 'ı', 'i') WHERE normalized_name LIKE '%ı%';
                UPDATE teams SET normalized_name = replace(normalized_name, 'ı', 'i') WHERE normalized_name LIKE '%ı%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Folding loses information; the old keys cannot be restored. Nothing to undo in the schema.
        }
    }
}
