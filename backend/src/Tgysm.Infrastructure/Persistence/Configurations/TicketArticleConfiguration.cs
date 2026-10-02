using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class TicketArticleConfiguration : IEntityTypeConfiguration<TicketArticle>
{
    public void Configure(EntityTypeBuilder<TicketArticle> builder)
    {
        builder.ToTable("ticket_articles");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.HasIndex(l => new { l.TicketId, l.ArticleId }).IsUnique();
        builder.HasIndex(l => l.ArticleId);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(l => l.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.LinkedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
