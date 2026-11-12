using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("work_tasks", table =>
        {
            table.HasCheckConstraint("ck_work_tasks_status", SnakeCaseEnumConversion<WorkTaskStatus>.CheckSql("status"));
            table.HasCheckConstraint("ck_work_tasks_priority", SnakeCaseEnumConversion<Priority>.CheckSql("priority"));
            table.HasCheckConstraint("ck_work_tasks_hold_reason", SnakeCaseEnumConversion<HoldReason>.CheckSql("hold_reason"));
            table.HasCheckConstraint("ck_work_tasks_hold", "(status = 'on_hold') = (hold_reason IS NOT NULL)");
            table.HasCheckConstraint("ck_work_tasks_result", "status <> 'completed' OR result_note IS NOT NULL");
            table.HasCheckConstraint("ck_work_tasks_cancel_reason", "status <> 'cancelled' OR cancel_reason IS NOT NULL");
        });
        builder.ConfigureEntityBase();
        builder.Property(t => t.Number).HasMaxLength(TicketNumber.MaxLength).IsRequired();
        builder.Property(t => t.Title).HasMaxLength(WorkTask.TitleMaxLength).IsRequired();
        builder.Property(t => t.SearchText).HasMaxLength(WorkTask.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(WorkTask.DescriptionMaxLength);
        builder.Property(t => t.HoldNote).HasMaxLength(WorkTask.ReasonMaxLength);
        builder.Property(t => t.ResultNote).HasMaxLength(WorkTask.NoteMaxLength);
        builder.Property(t => t.CancelReason).HasMaxLength(WorkTask.ReasonMaxLength);
        builder.Property(t => t.Status).HasEnumName();
        builder.Property(t => t.Priority).HasEnumName();
        builder.Property(t => t.HoldReason).HasEnumName();
        builder.HasIndex(t => t.Number).IsUnique();
        builder.HasIndex(t => new { t.TeamId, t.Status });
        builder.HasIndex(t => new { t.AssigneeId, t.Status });
        builder.HasIndex(t => t.TicketId);
        builder.HasIndex(t => new { t.UpdatedAt, t.Id }, "ix_work_tasks_updated_at_id").IsDescending(true, true);
        // Substring search (LIKE '%x%') needs trigrams; a B-tree cannot serve it.
        builder.HasIndex(t => t.SearchText, "ix_work_tasks_search_text_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);
        builder.HasOne<Team>().WithMany().HasForeignKey(t => t.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(t => t.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("task_assignments", table => table.HasCheckConstraint("ck_task_assignments_reason", SnakeCaseEnumConversion<TransferReason>.CheckSql("reason")));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Reason).HasEnumName();
        builder.Property(a => a.Note).HasMaxLength(TaskAssignment.NoteMaxLength);
        builder.HasIndex(a => new { a.TaskId, a.OccurredAt });
        builder.HasOne<WorkTask>().WithMany().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(a => a.FromTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(a => a.ToTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.FromAssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ToAssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TaskEventConfiguration : IEntityTypeConfiguration<TaskEvent>
{
    public void Configure(EntityTypeBuilder<TaskEvent> builder)
    {
        builder.ToTable("task_events", table => table.HasCheckConstraint("ck_task_events_kind", SnakeCaseEnumConversion<TaskEventKind>.CheckSql("kind")));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasEnumName();
        builder.Property(e => e.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.TaskId, e.OccurredAt });
        builder.HasOne<WorkTask>().WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
