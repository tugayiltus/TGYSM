using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Assets;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class AssetMovementConfiguration : IEntityTypeConfiguration<AssetMovement>
{
    public void Configure(EntityTypeBuilder<AssetMovement> builder)
    {
        builder.ToTable("asset_movements");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Kind).HasConversion(SnakeCaseEnumConversion<AssetMovementKind>.Converter).HasMaxLength(SnakeCaseEnumConversion<AssetMovementKind>.MaxLength);
        builder.Property(m => m.FromStatus).HasConversion(SnakeCaseEnumConversion<AssetStatus>.Converter).HasMaxLength(SnakeCaseEnumConversion<AssetStatus>.MaxLength);
        builder.Property(m => m.ToStatus).HasConversion(SnakeCaseEnumConversion<AssetStatus>.Converter).HasMaxLength(SnakeCaseEnumConversion<AssetStatus>.MaxLength);
        builder.Property(m => m.Reason).HasMaxLength(AssetMovement.ReasonMaxLength);

        builder.HasIndex(m => new { m.AssetId, m.OccurredAt });
        builder.HasOne<Asset>().WithMany().HasForeignKey(m => m.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
