using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Sla;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class BusinessCalendarConfiguration : IEntityTypeConfiguration<BusinessCalendar>
{
    public void Configure(EntityTypeBuilder<BusinessCalendar> builder)
    {
        builder.ToTable("business_calendar");
        builder.ConfigureEntityBase();
        builder.Property(c => c.TimeZoneId).HasColumnName("time_zone").HasMaxLength(IanaTimeZone.MaxLength).IsRequired();
        builder.Property(c => c.UpdatedBy).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
    }
}

internal sealed class BusinessHoursConfiguration : IEntityTypeConfiguration<BusinessHours>
{
    private const int FirstWeekday = (int)DayOfWeek.Sunday;
    private const int LastWeekday = (int)DayOfWeek.Saturday;

    public void Configure(EntityTypeBuilder<BusinessHours> builder)
    {
        builder.ToTable("business_hours", table =>
        {
            table.HasCheckConstraint("ck_business_hours_weekday", $"weekday BETWEEN {FirstWeekday} AND {LastWeekday}");
            table.HasCheckConstraint("ck_business_hours_range", "starts_at < ends_at");
        });
        builder.ConfigureEntityBase();
        builder.Property(h => h.Weekday).HasConversion<int>().IsRequired();
        builder.Property(h => h.StartsAt).IsRequired();
        builder.Property(h => h.EndsAt).IsRequired();

        // At most one working range per weekday; a missing weekday is closed.
        builder.HasIndex(h => h.Weekday).IsUnique();
    }
}

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("holidays");
        builder.ConfigureEntityBase();
        builder.Property(h => h.Date).IsRequired();
        builder.Property(h => h.Name).HasMaxLength(Holiday.NameMaxLength).IsRequired();
        builder.HasIndex(h => h.Date).IsUnique();
    }
}
