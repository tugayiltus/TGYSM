using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tgysm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailTemplates : Migration
    {
        // Seeded by this migration, not a real admin; UpdatedBy for these rows until an admin edits them (spec §3.4, ADR-0016).
        private static readonly Guid SystemSeedActor = Guid.Empty;
        private static readonly DateTimeOffset SeedTimestamp = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

        private static readonly string[] Columns = ["id", "event_type", "subject", "body", "updated_by", "updated_at"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_templates_event_type",
                table: "email_templates",
                column: "event_type",
                unique: true);

            migrationBuilder.InsertData(
                table: "email_templates",
                columns: Columns,
                values: new object[,]
                {
                    {
                        new Guid("0bc66415-1ea6-485e-871d-e2e590aaf0ea"),
                        "ticket_created",
                        "[{{ticketNumber}}] Kaydınız oluşturuldu",
                        "Sayın {{recipientName}},\n\n{{subject}} konulu kaydınız oluşturuldu.\nDurum: {{status}}\n\nKaydı görüntülemek için: {{ticketUrl}}",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                    {
                        new Guid("af7776ed-535c-4a9c-8be9-0ede11aee005"),
                        "ticket_assigned",
                        "[{{ticketNumber}}] Kayıt size atandı",
                        "Sayın {{recipientName}},\n\n{{subject}} konulu kayıt size atandı.\nDurum: {{status}}\n\nKaydı görüntülemek için: {{ticketUrl}}",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                    {
                        new Guid("3284c1b8-8f15-4884-a375-a0bfcf78f5e0"),
                        "public_reply",
                        "[{{ticketNumber}}] Kaydınıza yeni yanıt geldi",
                        "Sayın {{recipientName}},\n\n{{subject}} konulu kaydınıza yeni bir yanıt eklendi.\nDurum: {{status}}\n\nYanıtı görüntülemek için: {{ticketUrl}}",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                    {
                        new Guid("5ab07468-6957-44aa-88b0-44374146cb52"),
                        "approval_requested",
                        "[{{ticketNumber}}] Onayınız bekleniyor",
                        "Sayın {{recipientName}},\n\n{{subject}} konulu talep onayınızı bekliyor.\nDurum: {{status}}\n\nOnaylamak için: {{ticketUrl}}",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                    {
                        new Guid("674467d1-9c62-425c-9e86-bb1a85aadbef"),
                        "approval_decided",
                        "[{{ticketNumber}}] Onay sonucu belli oldu",
                        "Sayın {{recipientName}},\n\n{{subject}} konulu talebinizin onay süreci sonuçlandı.\nDurum: {{status}}\n\nAyrıntılar için: {{ticketUrl}}",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                    {
                        // Body follows the sample completion message in tgysm.md §10 (Bildirim / Çözüm / Durum), with
                        // the fixed example text replaced by the corresponding template variables.
                        new Guid("2bd3facd-ca32-4588-b6bd-49195be64903"),
                        "ticket_completed",
                        "[{{ticketNumber}}] {{subject}} tamamlandı",
                        "Sayın {{recipientName}},\n\nBildirim: {{subject}}\nÇözüm: {{resolution}}\nDurum: {{status}}\nTamamlanma zamanı: {{completedAt}}\n\nAyrıntıları kayıt ekranından görüntüleyebilirsiniz: {{ticketUrl}}",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                    {
                        new Guid("c3aba1fd-3f1e-4086-845a-d65b7b158bbe"),
                        "sla_warning",
                        "[{{ticketNumber}}] SLA uyarısı",
                        "Sayın {{recipientName}},\n\n{{subject}} konulu kaydın {{slaMetric}} süresi dolmak üzere.\nHedef zaman: {{dueAt}}\nDurum: {{status}}\n\nKaydı görüntülemek için: {{ticketUrl}}",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                    {
                        new Guid("c6298b32-9fd3-47fa-8722-46ae0a888c9f"),
                        "sla_breached",
                        "[{{ticketNumber}}] SLA ihlali",
                        "Sayın {{recipientName}},\n\n{{subject}} konulu kaydın {{slaMetric}} hedefi aşıldı.\nHedef zaman: {{dueAt}}\nDurum: {{status}}\n\nKaydı görüntülemek için: {{ticketUrl}}",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                    {
                        new Guid("4d8bc1ea-1981-4386-a64b-9d4d59547022"),
                        "account_activation",
                        "Hesabınızı etkinleştirin",
                        "Sayın {{recipientName}},\n\nTGYSM hesabınızı etkinleştirmek için aşağıdaki bağlantıyı kullanın.\nBağlantı: {{actionUrl}}\nBağlantı şu zamana kadar geçerlidir: {{expiresAt}}\n\nBu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz.",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                    {
                        new Guid("6a710bd7-1d32-45ea-b0c3-253e841c1bb0"),
                        "password_reset",
                        "Şifre sıfırlama isteği",
                        "Sayın {{recipientName}},\n\nŞifrenizi sıfırlamak için aşağıdaki bağlantıyı kullanın.\nBağlantı: {{actionUrl}}\nBağlantı şu zamana kadar geçerlidir: {{expiresAt}}\n\nBu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz.",
                        SystemSeedActor,
                        SeedTimestamp,
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_templates");
        }
    }
}
