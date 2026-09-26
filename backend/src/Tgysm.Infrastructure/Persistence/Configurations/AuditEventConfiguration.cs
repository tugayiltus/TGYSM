using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Audit;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    private const string JsonColumnType = "jsonb";

    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Action).HasMaxLength(AuditEvent.ActionMaxLength).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(AuditEvent.EntityTypeMaxLength).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(AuditEvent.ReasonMaxLength);
        builder.Property(a => a.CorrelationId).HasMaxLength(AuditEvent.CorrelationIdMaxLength).IsRequired();
        builder.Property(a => a.OldValues).HasColumnType(JsonColumnType);
        builder.Property(a => a.NewValues).HasColumnType(JsonColumnType);

        // No FK on actor_user_id: audit rows must survive any change to users.
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => new { a.ActorUserId, a.OccurredAt });
    }
}
