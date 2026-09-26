using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Catalog;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", table => table.HasCheckConstraint("ck_categories_scope", SnakeCaseEnumConversion<CategoryScope>.CheckSql("scope")));
        builder.ConfigureEntityBase();
        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(c => c.NormalizedName).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(c => c.Scope).HasEnumName();
        builder.HasIndex(c => new { c.ParentId, c.NormalizedName }).IsUnique().AreNullsDistinct(false);
        builder.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RequestTypeConfiguration : IEntityTypeConfiguration<RequestType>
{
    public void Configure(EntityTypeBuilder<RequestType> builder)
    {
        builder.ToTable("request_types");
        builder.ConfigureEntityBase();
        builder.Property(r => r.Name).HasMaxLength(RequestType.NameMaxLength).IsRequired();
        builder.Property(r => r.NormalizedName).HasMaxLength(RequestType.NameMaxLength).IsRequired();
        builder.HasIndex(r => r.NormalizedName).IsUnique();
    }
}

internal sealed class TicketCounterConfiguration : IEntityTypeConfiguration<TicketCounter>
{
    public void Configure(EntityTypeBuilder<TicketCounter> builder)
    {
        builder.ToTable("ticket_counters");
        builder.HasKey(c => new { c.Kind, c.Year });
        builder.Property(c => c.Kind).HasMaxLength(SnakeCaseEnumConversion<Domain.Tickets.TicketKind>.MaxLength);
    }
}
