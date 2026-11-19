using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ChangeTicketLinkConfiguration : IEntityTypeConfiguration<ChangeTicketLink>
{
    public void Configure(EntityTypeBuilder<ChangeTicketLink> builder)
    {
        builder.ToTable("change_ticket_links", table => table.HasCheckConstraint("ck_change_ticket_links_kind", SnakeCaseEnumConversion<ChangeTicketLinkKind>.CheckSql("kind")));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Kind).HasEnumName();
        builder.HasIndex(l => new { l.ChangeId, l.TicketId }).IsUnique().HasDatabaseName("ux_change_ticket_links_change_ticket");
        builder.HasIndex(l => l.TicketId).HasDatabaseName("ix_change_ticket_links_ticket_id");
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(l => l.ChangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(l => l.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
