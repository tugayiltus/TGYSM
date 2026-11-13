using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Changes;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ChangeAssetConfiguration : IEntityTypeConfiguration<ChangeAsset>
{
    public void Configure(EntityTypeBuilder<ChangeAsset> builder)
    {
        builder.ToTable("change_assets");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.HasIndex(a => new { a.ChangeId, a.AssetId }).IsUnique();
        builder.HasIndex(a => a.AssetId);
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(a => a.ChangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(a => a.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
