using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskAssignedEmailTemplate : Migration
    {
        private static readonly Guid TemplateId = new("1e416475-4038-46ad-a252-c138f9005044");
        private static readonly Guid SystemSeedActor = Guid.Empty;
        private static readonly DateTimeOffset SeedTimestamp = new(2026, 11, 13, 0, 0, 0, TimeSpan.Zero);
        private static readonly string[] Columns = ["id", "event_type", "subject", "body", "updated_by", "updated_at"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "email_templates",
                columns: Columns,
                values: new object[]
                {
                    TemplateId,
                    "task_assigned",
                    "[{{taskNumber}}] Görev size veya ekibinize atandı",
                    "Sayın {{recipientName}},\n\n{{subject}} başlıklı görev size veya ekibinize atandı.\nEkip: {{teamName}}\nDurum: {{status}}\nBitiş tarihi: {{dueDate}}\n\nGörevi görüntülemek için: {{taskUrl}}",
                    SystemSeedActor,
                    SeedTimestamp,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "email_templates", keyColumn: "id", keyValue: TemplateId);
        }
    }
}
