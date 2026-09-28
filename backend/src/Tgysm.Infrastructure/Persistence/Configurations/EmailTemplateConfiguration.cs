using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Notifications;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("email_templates");
        builder.ConfigureEntityBase();
        builder.Property(t => t.EventType).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(EmailTemplate.SubjectMaxLength).IsRequired();
        builder.Property(t => t.Body).HasMaxLength(EmailTemplate.BodyMaxLength).IsRequired();
        builder.Property(t => t.UpdatedBy).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();
        builder.HasIndex(t => t.EventType).IsUnique();
    }
}
