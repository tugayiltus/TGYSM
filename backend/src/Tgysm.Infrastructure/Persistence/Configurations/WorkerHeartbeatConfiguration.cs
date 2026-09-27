using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Operations;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeat>
{
    public void Configure(EntityTypeBuilder<WorkerHeartbeat> builder)
    {
        builder.ToTable("worker_heartbeats");
        builder.ConfigureEntityBase();

        builder.Property(h => h.Job).HasMaxLength(WorkerHeartbeat.JobMaxLength).IsRequired();
        builder.Property(h => h.Instance).HasMaxLength(WorkerHeartbeat.InstanceMaxLength).IsRequired();
        builder.Property(h => h.LastError).HasMaxLength(WorkerHeartbeat.ErrorMaxLength);

        // HeartbeatRecorder upserts on this pair.
        builder.HasIndex(h => new { h.Job, h.Instance }).IsUnique();
    }
}
