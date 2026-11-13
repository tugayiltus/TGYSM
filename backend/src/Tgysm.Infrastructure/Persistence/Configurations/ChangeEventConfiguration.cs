using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ChangeEventConfiguration : IEntityTypeConfiguration<ChangeEvent>
{
    public void Configure(EntityTypeBuilder<ChangeEvent> builder)
    {
        builder.ToTable("change_events", table => table.HasCheckConstraint("ck_change_events_kind", SnakeCaseEnumConversion<ChangeEventKind>.CheckSql("kind")));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasEnumName();
        builder.Property(e => e.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.ChangeId, e.OccurredAt });
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(e => e.ChangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
