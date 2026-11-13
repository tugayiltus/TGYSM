using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeEmailTemplates : Migration
    {
        private static readonly Guid ApprovalRequestedId = new("cc23b87b-3417-480a-9eff-c5de1c2e2676");
        private static readonly Guid ApprovalDecidedId = new("044da924-4296-49af-af92-6d58e38765d9");
        private static readonly Guid BackoutStartedId = new("c17207b1-df41-4ac5-b602-9391571bea79");
        private static readonly Guid SystemSeedActor = Guid.Empty;
        private static readonly DateTimeOffset SeedTimestamp = new(2026, 11, 13, 0, 0, 0, TimeSpan.Zero);
        private static readonly string[] Columns = ["id", "event_type", "subject", "body", "updated_by", "updated_at"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Insert(migrationBuilder, ApprovalRequestedId, "change_approval_requested",
                "[{{changeNumber}}] Değişiklik onayınızı bekliyor",
                "Sayın {{recipientName}},\n\n{{subject}} başlıklı değişiklik onayınızı bekliyor.\nRisk: {{risk}}\nPencere: {{windowStart}} - {{windowEnd}}\n\nDeğişikliği görüntülemek için: {{changeUrl}}");
            Insert(migrationBuilder, ApprovalDecidedId, "change_approval_decided",
                "[{{changeNumber}}] Değişiklik onayı karara bağlandı",
                "Sayın {{recipientName}},\n\n{{subject}} başlıklı değişikliğin onayı karara bağlandı.\nDurum: {{status}}\nGerekçe: {{reason}}\n\nDeğişikliği görüntülemek için: {{changeUrl}}");
            Insert(migrationBuilder, BackoutStartedId, "change_backout_started",
                "[{{changeNumber}}] Değişiklik için geri dönüş başladı",
                "Sayın {{recipientName}},\n\n{{subject}} başlıklı değişiklik için geri dönüş başladı. Geri dönüş göreviniz var.\nDurum: {{status}}\n\nDeğişikliği görüntülemek için: {{changeUrl}}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "email_templates", keyColumn: "id", keyValues: new object[] { ApprovalRequestedId, ApprovalDecidedId, BackoutStartedId });
        }

        private static void Insert(MigrationBuilder migrationBuilder, Guid id, string eventType, string subject, string body) =>
            migrationBuilder.InsertData(
                table: "email_templates",
                columns: Columns,
                values: new object[] { id, eventType, subject, body, SystemSeedActor, SeedTimestamp });
    }
}
