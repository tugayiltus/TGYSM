using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class AssetImportJobConfiguration : IEntityTypeConfiguration<AssetImportJob>
{
    private const string JsonColumnType = "jsonb";

    public void Configure(EntityTypeBuilder<AssetImportJob> builder)
    {
        builder.ToTable("import_jobs", table =>
            table.HasCheckConstraint("ck_import_jobs_status", SnakeCaseEnumConversion<AssetImportStatus>.CheckSql("status")));
        builder.ConfigureEntityBase();

        builder.Property(j => j.FileName).HasMaxLength(AssetImportJob.FileNameMaxLength).IsRequired();
        builder.Property(j => j.Status).HasConversion(SnakeCaseEnumConversion<AssetImportStatus>.Converter).HasMaxLength(SnakeCaseEnumConversion<AssetImportStatus>.MaxLength);
        builder.Property(j => j.RowsJson).HasColumnName("rows").HasColumnType(JsonColumnType).IsRequired();
        builder.Property(j => j.ErrorsJson).HasColumnName("errors").HasColumnType(JsonColumnType).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(j => j.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
