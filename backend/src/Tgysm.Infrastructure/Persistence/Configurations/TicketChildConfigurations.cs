using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("approvals", table => table.HasCheckConstraint("ck_approvals_status", SnakeCaseEnumConversion<ApprovalStatus>.CheckSql("status")));
        builder.ConfigureEntityBase();
        builder.Property(a => a.Status).HasEnumName();
        builder.Property(a => a.Reason).HasMaxLength(Approval.ReasonMaxLength);

        // One open decision per ticket (spec §2.2).
        builder.HasIndex(a => a.TicketId).IsUnique().HasFilter("status = 'pending'").HasDatabaseName("ux_approvals_pending");
        builder.HasIndex(a => new { a.ApproverId, a.Status });
        builder.HasOne<Ticket>().WithMany().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ApproverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.DecidedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketAssignmentConfiguration : IEntityTypeConfiguration<TicketAssignment>
{
    public void Configure(EntityTypeBuilder<TicketAssignment> builder)
    {
        builder.ToTable("ticket_assignments", table => table.HasCheckConstraint("ck_ticket_assignments_reason", SnakeCaseEnumConversion<TransferReason>.CheckSql("reason")));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Reason).HasEnumName();
        builder.Property(a => a.Note).HasMaxLength(TicketAssignment.NoteMaxLength);
        builder.HasIndex(a => new { a.TicketId, a.OccurredAt });
        builder.HasOne<Ticket>().WithMany().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(a => a.FromTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(a => a.ToTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.FromAssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ToAssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.ToTable("ticket_comments", table => table.HasCheckConstraint("ck_ticket_comments_visibility", SnakeCaseEnumConversion<Visibility>.CheckSql("visibility")));
        builder.ConfigureEntityBase();
        builder.Property(c => c.Body).HasMaxLength(TicketComment.BodyMaxLength).IsRequired();
        builder.Property(c => c.Visibility).HasEnumName();
        builder.HasIndex(c => new { c.TicketId, c.CreatedAt });
        builder.HasOne<Ticket>().WithMany().HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("ticket_attachments", table =>
        {
            table.HasCheckConstraint("ck_ticket_attachments_visibility", SnakeCaseEnumConversion<Visibility>.CheckSql("visibility"));
            table.HasCheckConstraint("ck_ticket_attachments_scan_status", SnakeCaseEnumConversion<ScanStatus>.CheckSql("scan_status"));
        });
        builder.ConfigureEntityBase();
        builder.Property(a => a.OriginalName).HasMaxLength(TicketAttachment.NameMaxLength).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(TicketAttachment.ContentTypeMaxLength).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(TicketAttachment.StorageKeyMaxLength).IsRequired();
        builder.Property(a => a.Visibility).HasEnumName();
        builder.Property(a => a.ScanStatus).HasEnumName();
        builder.HasIndex(a => a.StorageKey).IsUnique();
        builder.HasIndex(a => a.TicketId);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TicketComment>().WithMany().HasForeignKey(a => a.CommentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UploadedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketEventConfiguration : IEntityTypeConfiguration<TicketEvent>
{
    public void Configure(EntityTypeBuilder<TicketEvent> builder)
    {
        builder.ToTable("ticket_events", table =>
        {
            table.HasCheckConstraint("ck_ticket_events_kind", SnakeCaseEnumConversion<TicketEventKind>.CheckSql("kind"));
            table.HasCheckConstraint("ck_ticket_events_visibility", SnakeCaseEnumConversion<Visibility>.CheckSql("visibility"));
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasEnumName();
        builder.Property(e => e.Visibility).HasEnumName();
        builder.Property(e => e.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.TicketId, e.OccurredAt });
        builder.HasOne<Ticket>().WithMany().HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketRelationConfiguration : IEntityTypeConfiguration<TicketRelation>
{
    public void Configure(EntityTypeBuilder<TicketRelation> builder)
    {
        builder.ToTable("ticket_relations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasIndex(r => new { r.IncidentId, r.RequestId }).IsUnique();
        builder.HasIndex(r => r.RequestId);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(r => r.IncidentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(r => r.RequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketAssetConfiguration : IEntityTypeConfiguration<TicketAsset>
{
    public void Configure(EntityTypeBuilder<TicketAsset> builder)
    {
        builder.ToTable("ticket_assets");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.HasIndex(l => new { l.TicketId, l.AssetId }).IsUnique();
        builder.HasIndex(l => l.AssetId);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(l => l.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(l => l.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
