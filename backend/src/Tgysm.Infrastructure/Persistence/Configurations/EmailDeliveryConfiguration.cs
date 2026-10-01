using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Notifications;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class EmailDeliveryConfiguration : IEntityTypeConfiguration<EmailDelivery>
{
    public void Configure(EntityTypeBuilder<EmailDelivery> builder)
    {
        builder.ToTable("email_deliveries", table =>
            table.HasCheckConstraint("ck_email_deliveries_status", SnakeCaseEnumConversion<DeliveryStatus>.CheckSql("status")));
        builder.ConfigureEntityBase();

        builder.Property(d => d.OutboxId).IsRequired();
        builder.Property(d => d.RecipientUserId).IsRequired();
        builder.Property(d => d.RecipientEmail).HasMaxLength(EmailDelivery.RecipientEmailMaxLength).IsRequired();
        builder.Property(d => d.TemplateType).HasMaxLength(EmailDelivery.TemplateTypeMaxLength).IsRequired();
        builder.Property(d => d.Subject).HasMaxLength(EmailDelivery.SubjectMaxLength).IsRequired();
        builder.Property(d => d.Body).HasMaxLength(EmailDelivery.BodyMaxLength);
        builder.Property(d => d.Status).HasEnumName();
        builder.Property(d => d.Attempts).IsRequired();
        builder.Property(d => d.NextAttemptAt).IsRequired();
        builder.Property(d => d.LockedUntil);
        builder.Property(d => d.LastError).HasMaxLength(EmailDelivery.LastErrorMaxLength);
        builder.Property(d => d.SentAt);

        // At-least-once delivery is accepted (ADR-0015); this is the guard against an application-level duplicate
        // row when two dispatcher runs somehow both resolve the same event for the same recipient.
        builder.HasIndex(d => new { d.OutboxId, d.RecipientUserId }).IsUnique();
        builder.HasIndex(d => d.Status);

        // notification_outbox rows are never deleted in normal operation, but a delivery is owned by the event
        // that created it, unlike the ticket/user ids OutboxEvent itself deliberately leaves unconstrained.
        builder.HasOne<OutboxEvent>().WithMany().HasForeignKey(d => d.OutboxId).OnDelete(DeleteBehavior.Cascade);
    }
}
