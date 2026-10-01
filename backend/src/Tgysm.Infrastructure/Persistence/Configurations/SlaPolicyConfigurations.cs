using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class SlaPolicyVersionConfiguration : IEntityTypeConfiguration<SlaPolicyVersion>
{
    public void Configure(EntityTypeBuilder<SlaPolicyVersion> builder)
    {
        builder.ToTable("sla_policy_versions");
        builder.ConfigureEntityBase();
        builder.Property(v => v.Number).IsRequired();
        builder.Property(v => v.CreatedBy).IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.IsCurrent).IsRequired();
        builder.HasIndex(v => v.Number).IsUnique();

        // Exactly one current version at a time.
        builder.HasIndex(v => v.IsCurrent).IsUnique().HasFilter("is_current").HasDatabaseName("ux_sla_policy_versions_current");

        builder.HasMany(v => v.Targets).WithOne().HasForeignKey(t => t.PolicyVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Targets).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(v => v.PauseRules).WithOne().HasForeignKey(r => r.PolicyVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.PauseRules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SlaTargetConfiguration : IEntityTypeConfiguration<SlaTarget>
{
    public void Configure(EntityTypeBuilder<SlaTarget> builder)
    {
        builder.ToTable("sla_targets", table =>
        {
            table.HasCheckConstraint("ck_sla_targets_kind", SnakeCaseEnumConversion<TicketKind>.CheckSql("ticket_kind"));
            table.HasCheckConstraint("ck_sla_targets_priority", SnakeCaseEnumConversion<Priority>.CheckSql("priority"));
            table.HasCheckConstraint("ck_sla_targets_metric", SnakeCaseEnumConversion<SlaMetric>.CheckSql("metric"));
            table.HasCheckConstraint("ck_sla_targets_minutes", "target_minutes > 0");
            table.HasCheckConstraint("ck_sla_targets_warning", $"warning_percent BETWEEN {SlaTarget.MinWarningPercent} AND {SlaTarget.MaxWarningPercent}");
        });
        builder.ConfigureEntityBase();
        builder.Property(t => t.Kind).HasColumnName("ticket_kind").HasEnumName().IsRequired();
        builder.Property(t => t.Priority).HasEnumName().IsRequired();
        builder.Property(t => t.Metric).HasEnumName().IsRequired();
        builder.Property(t => t.TargetMinutes).IsRequired();
        builder.Property(t => t.WarningPercent).IsRequired();
        builder.HasOne<RequestType>().WithMany().HasForeignKey(t => t.RequestTypeId).OnDelete(DeleteBehavior.Restrict);

        // One target per version, kind, priority, request type (or none) and metric.
        builder.HasIndex(t => new { t.PolicyVersionId, t.Kind, t.Priority, t.RequestTypeId, t.Metric })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}

internal sealed class SlaPauseRuleConfiguration : IEntityTypeConfiguration<SlaPauseRule>
{
    public void Configure(EntityTypeBuilder<SlaPauseRule> builder)
    {
        builder.ToTable("sla_pause_rules", table =>
            table.HasCheckConstraint("ck_sla_pause_rules_hold_reason", SnakeCaseEnumConversion<HoldReason>.CheckSql("hold_reason")));
        builder.ConfigureEntityBase();
        builder.Property(r => r.HoldReason).HasEnumName().IsRequired();
        builder.Property(r => r.PausesFirstResponse).IsRequired();
        builder.Property(r => r.PausesResolution).IsRequired();
        builder.HasIndex(r => new { r.PolicyVersionId, r.HoldReason }).IsUnique();
    }
}
