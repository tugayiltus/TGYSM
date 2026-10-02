using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Tgysm.Api.Http;
using Tgysm.Application.Reports;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Reports;

public sealed class ReportFilterBinderTests
{
    private static ReportFilter Bind(string query) => ReportFilterBinder.Bind(new QueryCollection(QueryHelpers.ParseQuery(query)));

    [Fact]
    public void Every_field_round_trips_through_the_query_string()
    {
        var filter = new ReportFilter
        {
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 3, 31),
            Kind = TicketKind.Request,
            Priority = Priority.P2,
            CategoryId = Guid.NewGuid(),
            TeamId = Guid.NewGuid(),
            AssigneeId = Guid.NewGuid(),
            DepartmentId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            Status = TicketStatus.InProgress,
            SlaState = SlaStateFilter.AtRisk,
            AssetType = AssetType.Laptop,
            AssetStatus = AssetStatus.InStock,
        };

        Assert.Equal(filter, Bind(filter.ToQueryString()));
        Assert.Equal(new ReportFilter(), Bind(string.Empty));
    }

    [Theory]
    [InlineData("kind=nope", "kind")]
    [InlineData("teamId=not-a-guid", "teamId")]
    [InlineData("from=31.12.2026", "from")]
    [InlineData("slaState=late", "slaState")]
    public void Bad_values_are_validation_errors_with_the_invalid_filter_code(string query, string field)
    {
        var error = Assert.Throws<ValidationException>(() => Bind(query));

        var failure = Assert.Single(error.Errors);
        Assert.Equal(field, failure.PropertyName);
        Assert.Equal(ErrorCodes.ReportsInvalidFilter, failure.ErrorCode);
    }

    [Fact]
    public void From_after_to_is_a_validation_error()
    {
        var error = Assert.Throws<ValidationException>(() => Bind("from=2026-03-05&to=2026-03-04"));

        Assert.Equal(ErrorCodes.ReportsInvalidFilter, Assert.Single(error.Errors).ErrorCode);
    }
}
