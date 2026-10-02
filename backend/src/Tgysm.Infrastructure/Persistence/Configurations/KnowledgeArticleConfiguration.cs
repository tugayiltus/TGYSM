using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable("knowledge_articles", table =>
        {
            table.HasCheckConstraint("ck_knowledge_articles_status", SnakeCaseEnumConversion<ArticleStatus>.CheckSql("status"));
            table.HasCheckConstraint("ck_knowledge_articles_visibility", SnakeCaseEnumConversion<ArticleVisibility>.CheckSql("visibility"));
        });
        builder.ConfigureEntityBase();

        builder.Property(a => a.Title).HasMaxLength(ArticleLimits.TitleMaxLength).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(ArticleLimits.SummaryMaxLength).IsRequired();
        builder.Property(a => a.Body).HasMaxLength(ArticleLimits.BodyMaxLength).IsRequired();
        builder.Property(a => a.SearchText).IsRequired();
        builder.Property(a => a.Status).HasEnumName();
        builder.Property(a => a.Visibility).HasEnumName();

        builder.HasIndex(a => new { a.Status, a.UpdatedAt });
        builder.HasIndex(a => a.CategoryId);

        // Substring search (LIKE '%x%') needs trigrams; a B-tree cannot serve it.
        builder.HasIndex(a => a.SearchText).HasDatabaseName("ix_knowledge_articles_search_text_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);

        builder.HasOne<Category>().WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
