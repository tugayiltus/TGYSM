using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Assets;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");
        builder.ConfigureEntityBase();
        builder.Property(b => b.Name).HasMaxLength(CatalogName.MaxLength).IsRequired();
        builder.Property(b => b.NormalizedName).HasMaxLength(CatalogName.MaxLength).IsRequired();
        builder.HasIndex(b => b.NormalizedName).IsUnique();
    }
}

internal sealed class AssetModelConfiguration : IEntityTypeConfiguration<AssetModel>
{
    public void Configure(EntityTypeBuilder<AssetModel> builder)
    {
        builder.ToTable("asset_models", table => table.HasCheckConstraint("ck_asset_models_type", SnakeCaseEnumConversion<AssetType>.CheckSql("type")));
        builder.ConfigureEntityBase();
        builder.Property(m => m.Name).HasMaxLength(CatalogName.MaxLength).IsRequired();
        builder.Property(m => m.NormalizedName).HasMaxLength(CatalogName.MaxLength).IsRequired();
        builder.Property(m => m.Type).HasConversion(SnakeCaseEnumConversion<AssetType>.Converter).HasMaxLength(SnakeCaseEnumConversion<AssetType>.MaxLength);
        builder.HasIndex(m => new { m.BrandId, m.Type, m.NormalizedName }).IsUnique();
        builder.HasOne<Brand>().WithMany().HasForeignKey(m => m.BrandId).OnDelete(DeleteBehavior.Restrict);
    }
}
