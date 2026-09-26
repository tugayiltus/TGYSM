using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        builder.ConfigureEntityBase();

        builder.Property(l => l.Name).HasMaxLength(Location.NameMaxLength).IsRequired();
        builder.Property(l => l.NormalizedName).HasMaxLength(Location.NameMaxLength).IsRequired();

        // Two root locations (parent_id NULL) with the same name must also collide.
        builder.HasIndex(l => new { l.ParentId, l.NormalizedName }).IsUnique().AreNullsDistinct(false);

        builder.HasOne<Location>().WithMany().HasForeignKey(l => l.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
