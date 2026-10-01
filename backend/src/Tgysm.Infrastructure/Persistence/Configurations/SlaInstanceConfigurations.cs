using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class SlaInstanceConfiguration : IEntityTypeConfiguration<SlaInstance>
{
    public void Configure(EntityTypeBuilder<SlaInstance> builder)
    {
        builder.ToTable("sla_instances", table =>
        {
            table.HasCheckConstraint("ck_sla_instances_metric", SnakeCaseEnumConversion<SlaMetric>.CheckSql("metric"));
            table.HasCheckConstraint("ck_sla_instances_result", SnakeCaseEnumConversion<SlaResult>.CheckSql("result"));
        });
        builder.ConfigureEntityBase();
        builder.Property(i => i.Metric).HasEnumName().IsRequired();
        builder.Property(i => i.Result).HasEnumName();
        builder.Property(i => i.TargetMinutes).IsRequired();
        builder.Property(i => i.WarningPercent).IsRequired();
        builder.Property(i => i.ElapsedSeconds).IsRequired();
        builder.Property(i => i.StartedAt).IsRequired();

        // One timer per metric and ticket.
        builder.HasIndex(i => new { i.TicketId, i.Metric }).IsUnique();

        // The monitor's sweeps look at running, not yet stopped instances only.
        builder.HasIndex(i => i.DueAt).HasFilter("stopped_at IS NULL AND due_at IS NOT NULL").HasDatabaseName("ix_sla_instances_due_open");
        builder.HasIndex(i => i.WarnAt).HasFilter("stopped_at IS NULL AND warn_at IS NOT NULL").HasDatabaseName("ix_sla_instances_warn_open");
        builder.HasOne<Ticket>().WithMany().HasForeignKey(i => i.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SlaPolicyVersion>().WithMany().HasForeignKey(i => i.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Pauses).WithOne().HasForeignKey(p => p.SlaInstanceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Pauses).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(i => i.Events).WithOne().HasForeignKey(e => e.SlaInstanceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SlaPauseConfiguration : IEntityTypeConfiguration<SlaPause>
{
    public void Configure(EntityTypeBuilder<SlaPause> builder)
    {
        builder.ToTable("sla_pauses", table =>
            table.HasCheckConstraint("ck_sla_pauses_hold_reason", SnakeCaseEnumConversion<HoldReason>.CheckSql("hold_reason")));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.HoldReason).HasEnumName().IsRequired();
        builder.Property(p => p.StartedAt).IsRequired();
        builder.HasIndex(p => p.SlaInstanceId);
    }
}

internal sealed class SlaEventConfiguration : IEntityTypeConfiguration<SlaEvent>
{
    public void Configure(EntityTypeBuilder<SlaEvent> builder)
    {
        builder.ToTable("sla_events", table =>
            table.HasCheckConstraint("ck_sla_events_kind", SnakeCaseEnumConversion<SlaEventKind>.CheckSql("kind")));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasEnumName().IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.SlaInstanceId, e.OccurredAt });
    }
}
