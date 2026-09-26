using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    private const string ActiveAssignmentFilter = "returned_at IS NULL";

    public void Configure(EntityTypeBuilder<AssetAssignment> builder)
    {
        builder.HasOne<Domain.Tickets.Ticket>().WithMany().HasForeignKey(a => a.RequestTicketId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("asset_assignments");
        builder.ConfigureEntityBase();

        builder.Property(a => a.Note).HasMaxLength(AssetAssignment.NoteMaxLength);
        builder.Ignore(a => a.IsActive);

        // One active assignment per asset, enforced by the database even under races (ADR-0010).
        builder.HasIndex(a => a.AssetId).IsUnique().HasFilter(ActiveAssignmentFilter).HasDatabaseName("ux_asset_assignments_active");
        builder.HasIndex(a => new { a.UserId, a.ReturnedAt });

        builder.HasOne<Asset>().WithMany().HasForeignKey(a => a.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
