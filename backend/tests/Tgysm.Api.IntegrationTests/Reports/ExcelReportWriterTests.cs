using ClosedXML.Excel;
using Microsoft.Extensions.Time.Testing;
using Tgysm.Application.Reports;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;
using Tgysm.Infrastructure.Reports;

namespace Tgysm.Api.IntegrationTests.Reports;

public sealed class ExcelReportWriterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async IAsyncEnumerable<IReadOnlyList<object?>> Rows(params IReadOnlyList<object?>[] rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }

        await Task.CompletedTask;
    }

    private static async Task<XLWorkbook> WriteAsync(ReportExportContent content)
    {
        var writer = new ExcelReportWriter(new TestClock(), new FakeTimeProvider(AssetReportFixture.Now));
        using var stream = new MemoryStream();
        await writer.WriteAsync(content, stream, Ct);
        return new XLWorkbook(new MemoryStream(stream.ToArray()));
    }

    [Fact]
    public async Task Turkish_headers_and_dotted_dotless_i_survive_the_round_trip_and_dates_use_the_company_zone()
    {
        var created = new DateTimeOffset(2026, 3, 10, 22, 30, 0, TimeSpan.Zero); // 11.03.2026 01:30 in Istanbul
        var content = new ReportExportContent(
            ReportName.Incidents, "volume", null, ReportDrillPart.Numerator, new ReportFilter(), "all",
            ExportColumns.Tickets,
            Rows(["INC-2026-000001", TicketKind.Incident, "İŞLEM ıslak şifre çağrı", TicketStatus.InProgress, Priority.P1, "Çözüm Ekibi", null, created, null]));

        using var workbook = await WriteAsync(content);

        var sheet = workbook.Worksheet("Kayıtlar");
        Assert.Equal(["Numara", "Tür", "Konu", "Durum", "Öncelik", "Ekip", "Atanan", "Oluşturulma", "Tamamlanma"], sheet.Row(1).Cells(1, 9).Select(c => c.GetString()));
        Assert.Equal("İŞLEM ıslak şifre çağrı", sheet.Cell(2, 3).GetString());
        Assert.Equal("Arıza", sheet.Cell(2, 2).GetString());
        Assert.Equal("Üzerinde çalışılıyor", sheet.Cell(2, 4).GetString());
        Assert.Equal("Çözüm Ekibi", sheet.Cell(2, 6).GetString());
        Assert.True(sheet.Cell(2, 7).IsEmpty());
        Assert.Equal(new DateTime(2026, 3, 11, 1, 30, 0), sheet.Cell(2, 8).GetDateTime());
        Assert.Equal("dd.MM.yyyy HH:mm", sheet.Cell(2, 8).Style.DateFormat.Format);
    }

    [Fact]
    public async Task A_subject_that_looks_like_a_formula_stays_text()
    {
        var content = new ReportExportContent(
            ReportName.Incidents, "volume", null, ReportDrillPart.Numerator, new ReportFilter(), "all",
            ExportColumns.Tickets,
            Rows(["INC-2026-000001", TicketKind.Incident, "=HYPERLINK(\"http://x\")", TicketStatus.Open, Priority.P2, "Ekip", "Ali Veli", AssetReportFixture.Now, null]));

        using var workbook = await WriteAsync(content);

        var cell = workbook.Worksheet("Kayıtlar").Cell(2, 3);
        Assert.False(cell.HasFormula);
        Assert.Equal("=HYPERLINK(\"http://x\")", cell.GetString());
    }
}
