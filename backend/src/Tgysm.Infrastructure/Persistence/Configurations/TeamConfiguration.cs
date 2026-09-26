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

        // At most one default intake team (spec §2.2).
        builder.HasIndex(t => t.IsDefaultIntake).IsUnique().HasFilter("is_default_intake").HasDatabaseName("ux_teams_default_intake");
    }
}
