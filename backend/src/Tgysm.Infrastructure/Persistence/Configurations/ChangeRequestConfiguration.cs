using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("change_requests", table =>
        {
            table.HasCheckConstraint("ck_change_requests_status", SnakeCaseEnumConversion<ChangeStatus>.CheckSql("status"));
            table.HasCheckConstraint("ck_change_requests_outcome", SnakeCaseEnumConversion<ChangeOutcome>.CheckSql("outcome"));
            table.HasCheckConstraint("ck_change_requests_risk", SnakeCaseEnumConversion<Level>.CheckSql("risk"));
            table.HasCheckConstraint("ck_change_requests_impact", SnakeCaseEnumConversion<Level>.CheckSql("impact"));
        });
        builder.ConfigureEntityBase();
        builder.Property(c => c.Number).HasMaxLength(TicketNumber.MaxLength).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(ChangeRequest.TitleMaxLength).IsRequired();
        builder.Property(c => c.SearchText).HasMaxLength(ChangeRequest.TitleMaxLength).IsRequired();
        builder.Property(c => c.Justification).HasMaxLength(ChangeRequest.TextMaxLength);
        builder.Property(c => c.AffectedService).HasMaxLength(ChangeRequest.AffectedServiceMaxLength);
        builder.Property(c => c.ImplementationPlan).HasMaxLength(ChangeRequest.TextMaxLength);
        builder.Property(c => c.VerificationPlan).HasMaxLength(ChangeRequest.TextMaxLength);
        builder.Property(c => c.BackoutPlan).HasMaxLength(ChangeRequest.TextMaxLength);
        builder.Property(c => c.VerificationNote).HasMaxLength(ChangeRequest.TextMaxLength);
        builder.Property(c => c.BackoutNote).HasMaxLength(ChangeRequest.TextMaxLength);
        builder.Property(c => c.ClosingNote).HasMaxLength(ChangeRequest.TextMaxLength);
        builder.Property(c => c.CancelReason).HasMaxLength(ChangeRequest.ReasonMaxLength);
        builder.Property(c => c.Status).HasEnumName();
        builder.Property(c => c.Outcome).HasEnumName();
        builder.Property(c => c.Risk).HasEnumName();
        builder.Property(c => c.Impact).HasEnumName();
        builder.HasIndex(c => c.Number).IsUnique();
        builder.HasIndex(c => new { c.OwnerTeamId, c.Status });
        builder.HasIndex(c => new { c.OwnerId, c.Status });
        builder.HasIndex(c => new { c.UpdatedAt, c.Id }, "ix_change_requests_updated_at_id").IsDescending(true, true);
        // Substring search (LIKE '%x%') needs trigrams; a B-tree cannot serve it.
        builder.HasIndex(c => c.SearchText, "ix_change_requests_search_text_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);
        builder.HasOne<Team>().WithMany().HasForeignKey(c => c.OwnerTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
