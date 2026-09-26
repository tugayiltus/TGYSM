using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");
        builder.ConfigureEntityBase();

        builder.Property(t => t.Name).HasMaxLength(Team.NameMaxLength).IsRequired();
        builder.Property(t => t.NormalizedName).HasMaxLength(Team.NameMaxLength).IsRequired();
        builder.HasIndex(t => t.NormalizedName).IsUnique();
    }
}
