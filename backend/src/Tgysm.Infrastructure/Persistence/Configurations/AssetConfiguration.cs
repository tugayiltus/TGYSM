using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Organization;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("assets", table =>
        {
            table.HasCheckConstraint("ck_assets_type", SnakeCaseEnumConversion<AssetType>.CheckSql("type"));
            table.HasCheckConstraint("ck_assets_status", SnakeCaseEnumConversion<AssetStatus>.CheckSql("status"));
        });
        builder.ConfigureEntityBase();

        builder.Property(a => a.AssetTag).HasMaxLength(Asset.TagMaxLength).IsRequired();
        builder.Property(a => a.NormalizedAssetTag).HasMaxLength(Asset.TagMaxLength).IsRequired();
        builder.Property(a => a.Brand).HasMaxLength(Asset.BrandMaxLength).IsRequired();
        builder.Property(a => a.Model).HasMaxLength(Asset.ModelMaxLength).IsRequired();
        builder.Property(a => a.SerialNumber).HasMaxLength(Asset.SerialMaxLength);
        builder.Property(a => a.NormalizedSerialNumber).HasMaxLength(Asset.SerialMaxLength);
        builder.Property(a => a.Notes).HasMaxLength(Asset.NotesMaxLength);
        builder.Property(a => a.Type).HasConversion(SnakeCaseEnumConversion<AssetType>.Converter).HasMaxLength(SnakeCaseEnumConversion<AssetType>.MaxLength);
        builder.Property(a => a.Status).HasConversion(SnakeCaseEnumConversion<AssetStatus>.Converter).HasMaxLength(SnakeCaseEnumConversion<AssetStatus>.MaxLength);

        builder.HasIndex(a => a.NormalizedAssetTag).IsUnique();

        // PostgreSQL unique indexes allow many NULLs: unknown serial numbers never collide.
        builder.HasIndex(a => a.NormalizedSerialNumber).IsUnique();
        builder.HasIndex(a => new { a.Status, a.Type });

        // Substring search on tag and serial (LIKE '%x%'); lower(brand/model) trigram indexes are in migration AssetSearchIndexes.
        builder.HasIndex(a => a.NormalizedAssetTag, "ix_assets_tag_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);
        builder.HasIndex(a => a.NormalizedSerialNumber, "ix_assets_serial_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);

        builder.HasOne<Location>().WithMany().HasForeignKey(a => a.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(a => a.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
