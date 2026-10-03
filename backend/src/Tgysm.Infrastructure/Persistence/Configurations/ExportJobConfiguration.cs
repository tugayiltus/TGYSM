using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    private const string JsonColumnType = "jsonb";

    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.ToTable("export_jobs", table =>
        {
            table.HasCheckConstraint("ck_export_jobs_status", SnakeCaseEnumConversion<ExportJobStatus>.CheckSql("status"));
            table.HasCheckConstraint("ck_export_jobs_report", SnakeCaseEnumConversion<ReportName>.CheckSql("report"));
            table.HasCheckConstraint("ck_export_jobs_part", SnakeCaseEnumConversion<ReportDrillPart>.CheckSql("part"));
        });
        builder.ConfigureEntityBase();

        builder.Property(j => j.Report).HasEnumName();
        builder.Property(j => j.Metric).HasMaxLength(ExportJob.MetricMaxLength).IsRequired();
        builder.Property(j => j.Bucket).HasMaxLength(ExportJob.BucketMaxLength);
        builder.Property(j => j.Part).HasEnumName();
        builder.Property(j => j.Filters).HasColumnType(JsonColumnType).IsRequired();
        builder.Property(j => j.Status).HasEnumName();
        builder.Property(j => j.FileKey).HasMaxLength(ExportJob.FileKeyMaxLength);
        builder.Property(j => j.LastError).HasMaxLength(ExportJob.LastErrorMaxLength);
        builder.Property(j => j.CreatedAt).IsRequired();

        // "My exports" lists a creator's jobs newest first; the runner and cleanup look jobs up by status.
        builder.HasIndex(j => new { j.CreatedBy, j.CreatedAt });
        builder.HasIndex(j => j.Status);

        builder.HasOne<User>().WithMany().HasForeignKey(j => j.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
