using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets", table =>
        {
            table.HasCheckConstraint("ck_tickets_kind", SnakeCaseEnumConversion<TicketKind>.CheckSql("kind"));
            table.HasCheckConstraint("ck_tickets_status", SnakeCaseEnumConversion<TicketStatus>.CheckSql("status"));
            table.HasCheckConstraint("ck_tickets_hold_reason", SnakeCaseEnumConversion<HoldReason>.CheckSql("hold_reason"));
            table.HasCheckConstraint("ck_tickets_impact", SnakeCaseEnumConversion<Level>.CheckSql("impact"));
            table.HasCheckConstraint("ck_tickets_urgency", SnakeCaseEnumConversion<Level>.CheckSql("urgency"));
            table.HasCheckConstraint("ck_tickets_priority", SnakeCaseEnumConversion<Priority>.CheckSql("priority"));
            table.HasCheckConstraint("ck_tickets_outcome", SnakeCaseEnumConversion<TicketOutcome>.CheckSql("outcome"));
            table.HasCheckConstraint("ck_tickets_request_type", "(kind = 'request') = (request_type_id IS NOT NULL)");
            table.HasCheckConstraint("ck_tickets_hold", "(status = 'on_hold') = (hold_reason IS NOT NULL)");
            table.HasCheckConstraint("ck_tickets_resolution", "status <> 'completed' OR resolution IS NOT NULL");
            table.HasCheckConstraint("ck_tickets_cancel_reason", "status <> 'cancelled' OR cancel_reason IS NOT NULL");
        });
        builder.ConfigureEntityBase();

        builder.Property(t => t.Number).HasMaxLength(TicketNumber.MaxLength).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(Ticket.SubjectMaxLength).IsRequired();
        builder.Property(t => t.SearchText).HasMaxLength(Ticket.SubjectMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(Ticket.DescriptionMaxLength).IsRequired();
        builder.Property(t => t.HoldNote).HasMaxLength(Ticket.ReasonMaxLength);
        builder.Property(t => t.RootCause).HasMaxLength(Ticket.TextMaxLength);
        builder.Property(t => t.Resolution).HasMaxLength(Ticket.TextMaxLength);
        builder.Property(t => t.CancelReason).HasMaxLength(Ticket.ReasonMaxLength);
        builder.Property(t => t.Kind).HasEnumName();
        builder.Property(t => t.Status).HasEnumName();
        builder.Property(t => t.HoldReason).HasEnumName();
        builder.Property(t => t.Impact).HasEnumName();
        builder.Property(t => t.Urgency).HasEnumName();
        builder.Property(t => t.Priority).HasEnumName();
        builder.Property(t => t.Outcome).HasEnumName();

        builder.HasIndex(t => t.Number).IsUnique();
        builder.HasIndex(t => t.RequesterId);
        builder.HasIndex(t => new { t.TeamId, t.Status });
        builder.HasIndex(t => t.AssigneeId);

        // Lists sort by latest activity; queues filter by assignee or team first.
        builder.HasIndex(t => new { t.UpdatedAt, t.Id }, "ix_tickets_updated_at_id").IsDescending(true, true);
        builder.HasIndex(t => new { t.AssigneeId, t.UpdatedAt }, "ix_tickets_assignee_updated_at").IsDescending(false, true);
        builder.HasIndex(t => new { t.TeamId, t.UpdatedAt }, "ix_tickets_team_updated_at").IsDescending(false, true);

        // Substring search (LIKE '%x%') needs trigrams; a B-tree cannot serve it.
        builder.HasIndex(t => t.SearchText, "ix_tickets_search_text_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);

        builder.HasOne<Category>().WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(t => t.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(t => t.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RequestType>().WithMany().HasForeignKey(t => t.RequestTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
