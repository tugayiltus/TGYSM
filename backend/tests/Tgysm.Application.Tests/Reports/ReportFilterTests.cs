using FluentValidation;
using Tgysm.Application.Reports;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Tests.Reports;

public sealed class ReportFilterTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    // 2026-03-01 21:30 UTC is 2026-03-02 00:30 in Istanbul (+03:00).
    private static readonly DateTimeOffset LateEvening = new(2026, 3, 1, 21, 30, 0, TimeSpan.Zero);

    [Fact]
    public void From_after_to_is_rejected_with_the_invalid_filter_code()
    {
        var filter = new ReportFilter { From = new DateOnly(2026, 3, 5), To = new DateOnly(2026, 3, 4) };

        var error = Assert.Throws<ValidationException>(filter.EnsureValid);

        Assert.Equal(ErrorCodes.ReportsInvalidFilter, Assert.Single(error.Errors).ErrorCode);
    }

    [Fact]
    public void Same_day_range_and_open_ranges_are_valid()
    {
        new ReportFilter { From = new DateOnly(2026, 3, 5), To = new DateOnly(2026, 3, 5) }.EnsureValid();
        new ReportFilter { From = new DateOnly(2026, 3, 5) }.EnsureValid();
        new ReportFilter().EnsureValid();
    }

    [Fact]
    public void Day_window_uses_the_company_time_zone_and_includes_the_to_day()
    {
        var march2 = new ReportFilter { From = new DateOnly(2026, 3, 2), To = new DateOnly(2026, 3, 2) }.Window(Istanbul);
        var march1 = new ReportFilter { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 3, 1) }.Window(Istanbul);

        Assert.True(LateEvening >= march2.FromInclusive && LateEvening < march2.ToExclusive);
        Assert.False(LateEvening >= march1.FromInclusive && LateEvening < march1.ToExclusive);
        Assert.Equal(TimeSpan.FromDays(1), march2.ToExclusive!.Value - march2.FromInclusive!.Value);
    }

    [Fact]
    public void Open_ends_give_a_null_bound()
    {
        var window = new ReportFilter().Window(Istanbul);

        Assert.Null(window.FromInclusive);
        Assert.Null(window.ToExclusive);
    }

    [Fact]
    public void Query_string_lists_every_set_field_in_snake_case_and_skips_empty_ones()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var filter = new ReportFilter
        {
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 3, 31),
            Kind = TicketKind.Incident,
            Priority = Priority.P1,
            CategoryId = id,
            TeamId = id,
            AssigneeId = id,
            DepartmentId = id,
            LocationId = id,
            Status = TicketStatus.OnHold,
            SlaState = SlaStateFilter.OpenBreached,
            AssetType = AssetType.Laptop,
            AssetStatus = AssetStatus.InStock,
        };

        var query = filter.ToQueryString();

        Assert.Contains("from=2026-03-01", query);
        Assert.Contains("to=2026-03-31", query);
        Assert.Contains("kind=incident", query);
        Assert.Contains("priority=p1", query);
        Assert.Contains("status=on_hold", query);
        Assert.Contains("slaState=open_breached", query);
        Assert.Contains("assetStatus=in_stock", query);
        Assert.Contains($"teamId={id}", query);
        Assert.Equal(string.Empty, new ReportFilter().ToQueryString());
    }

    [Fact]
    public void Json_round_trips_every_filter_with_readable_values()
    {
        var id = Guid.NewGuid();
        var filter = new ReportFilter
        {
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 3, 31),
            Kind = TicketKind.Request,
            Priority = Priority.P2,
            CategoryId = id,
            TeamId = id,
            AssigneeId = id,
            DepartmentId = id,
            LocationId = id,
            Status = TicketStatus.OnHold,
            SlaState = SlaStateFilter.AtRisk,
            AssetType = AssetType.Phone,
            AssetStatus = AssetStatus.Maintenance,
        };

        var json = filter.ToJson();

        Assert.Equal(filter, ReportFilter.FromJson(json));
        Assert.Contains("\"status\":\"on_hold\"", json);
        Assert.Contains("\"from\":\"2026-03-01\"", json);
        Assert.Equal("{}", new ReportFilter().ToJson());
        Assert.Equal(new ReportFilter(), ReportFilter.FromJson("{}"));
    }

    [Theory]
    [InlineData(ReportName.Incidents, "incidents")]
    [InlineData(ReportName.Warranty, "warranty")]
    [InlineData(ReportName.Transfers, "transfers")]
    public void Report_names_use_lower_kebab_case_routes(ReportName name, string route)
    {
        Assert.Equal(route, ReportNames.ToRoute(name));
        Assert.Equal(name, ReportNames.Parse(route));
    }

    [Fact]
    public void Unknown_report_and_metric_are_validation_errors_with_their_codes()
    {
        var report = Assert.Throws<ValidationException>(() => ReportNames.Parse("nope"));
        var metric = Assert.Throws<ValidationException>(() => ReportCatalog.Find(ReportName.Incidents, "nope"));

        Assert.Equal(ErrorCodes.ReportsUnknownReport, Assert.Single(report.Errors).ErrorCode);
        Assert.Equal(ErrorCodes.ReportsUnknownMetric, Assert.Single(metric.Errors).ErrorCode);
    }

    [Fact]
    public void Ageing_bands_cover_zero_to_one_two_to_three_four_to_seven_and_eight_plus()
    {
        Assert.Equal(["0-1", "2-3", "4-7", "8+"], ReportBuckets.AgeingDays.Select(b => b.Key));
        Assert.Equal("2-3", ReportBuckets.AgeingBand(3).Key);
        Assert.Equal("8+", ReportBuckets.AgeingBand(400).Key);
        Assert.Equal("0-1", ReportBuckets.AgeingBand(0).Key);
        Assert.Equal("0-1", ReportBuckets.AgeingBand(-5).Key);
    }

    [Theory]
    [InlineData("1999-12-31", null)]
    [InlineData("2101-01-01", null)]
    [InlineData(null, "1999-12-31")]
    [InlineData(null, "2101-01-01")]
    public void Days_outside_the_supported_range_are_invalid_filters(string? from, string? to)
    {
        var filter = new ReportFilter { From = from is null ? null : DateOnly.Parse(from), To = to is null ? null : DateOnly.Parse(to) };

        var error = Assert.Throws<ValidationException>(filter.EnsureValid);

        Assert.Equal(ErrorCodes.ReportsInvalidFilter, Assert.Single(error.Errors).ErrorCode);
    }

    [Fact]
    public void Days_at_the_range_edges_are_valid()
    {
        new ReportFilter { From = ReportFilter.MinDay, To = ReportFilter.MaxDay }.EnsureValid();
    }

    [Fact]
    public void Export_limits_are_five_thousand_rows_and_one_day()
    {
        Assert.Equal(5000, ReportExportLimits.SyncMaxRows);
        Assert.Equal(TimeSpan.FromHours(24), ReportExportLimits.FileLifetime);
    }
}
