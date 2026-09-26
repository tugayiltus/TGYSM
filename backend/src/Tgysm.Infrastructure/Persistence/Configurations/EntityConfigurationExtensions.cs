using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Common;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal static class EntityConfigurationExtensions
{
    public static void ConfigureEntityBase<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Npgsql maps a uint row version to the xmin system column (ADR-0002).
        builder.Property(e => e.Version).IsRowVersion();
    }

    /// <summary>Uses a regular <c>version</c> column bumped by the domain instead of <c>xmin</c>.</summary>
    public static void UseApplicationManagedVersion<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity =>
        builder.Property(e => e.Version)
            .HasColumnName(ApplicationVersionColumn)
            .HasConversion<long>()
            .IsConcurrencyToken()
            .ValueGeneratedNever();

    private const string ApplicationVersionColumn = "version";

    /// <summary>Stores the enum as its snake_case name (<see cref="Converters.SnakeCaseEnumConversion{TEnum}"/>).</summary>
    public static PropertyBuilder<TEnum> HasEnumName<TEnum>(this PropertyBuilder<TEnum> property)
        where TEnum : struct, Enum =>
        property.HasConversion(Converters.SnakeCaseEnumConversion<TEnum>.Converter).HasMaxLength(Converters.SnakeCaseEnumConversion<TEnum>.MaxLength);

    /// <summary>Nullable variant of <see cref="HasEnumName{TEnum}(PropertyBuilder{TEnum})"/>; NULL stays NULL.</summary>
    public static PropertyBuilder<TEnum?> HasEnumName<TEnum>(this PropertyBuilder<TEnum?> property)
        where TEnum : struct, Enum =>
        property.HasConversion(Converters.SnakeCaseEnumConversion<TEnum>.Converter).HasMaxLength(Converters.SnakeCaseEnumConversion<TEnum>.MaxLength);
}
