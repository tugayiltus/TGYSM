using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Notifications;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    private const string JsonColumnType = "jsonb";

    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("notification_outbox", table =>
            table.HasCheckConstraint("ck_notification_outbox_status", SnakeCaseEnumConversion<OutboxStatus>.CheckSql("status")));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EventType).HasMaxLength(OutboxEvent.EventTypeMaxLength).IsRequired();
        builder.Property(e => e.Payload).HasColumnType(JsonColumnType).IsRequired();
        builder.Property(e => e.Status).HasEnumName();

        // No FK on ticket_id/user_id: an event must survive the ticket or user it names being changed further.
        builder.HasIndex(e => e.TicketId);
        builder.HasIndex(e => e.Status);
    }
}
