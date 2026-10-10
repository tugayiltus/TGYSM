using System.Net;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeKit;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Notifications;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Email;

namespace Tgysm.Api.IntegrationTests.Reports;

/// <summary>
/// Acceptance criterion 15: the Turkish letters <c>ç ğ ı İ ö ş ü</c> (and their other case) survive every way text enters or leaves
/// the system as a file: CSV import, Excel export and the email body written to disk.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TurkishCharactersExportTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Letters = "ç ğ ı İ ö ş ü Ç Ğ I i Ö Ş Ü";
    private const string Subject = "Yazıcı arızası " + Letters;
    private const string Brand = "Öztürk Çağ";
    private const string Model = "Işıklı Ü-14";
    private const string Location = "Şişli Göğüs İç";
    private const string Tag = "DT-ÇĞ-1";

    private const string ImportPath = "/api/v1/assets/import";
    private const string PreviewPath = ImportPath + "/preview";
    private const string AssetsPath = "/api/v1/assets";
    private const string ReportsPath = "/api/v1/reports";
    private const string CsvType = "text/csv";
    private const string FileName = "cihazlar.csv";
    private const string Header = "Demirbaş No,Tür,Marka,Model,Seri No,Konum,Departman,Durum,Garanti Bitiş,Satın Alma,Notlar";
    private const string RecordsSheet = "Kayıtlar";
    private const string DayFormat = "yyyy-MM-dd";
    private const string PickupFrom = "TGYSM <no-reply@tgysm.local>";
    private const string EmlPattern = "*.eml";
    private const int Batch = 50;

    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.EmailSender.Reset();
    }

    public ValueTask DisposeAsync()
    {
        factory.EmailSender.Reset();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Csv_import_keeps_turkish_letters_in_names_tags_and_notes()
    {
        await TestData.ModelAsync(factory, Brand, Model);
        await TestData.LocationAsync(factory, Location);
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        var csv = $"{Header}\r\n{Tag},laptop,{Brand},{Model},,{Location},,in_stock,,,{Letters}";
        byte[] content = [.. Bom, .. Encoding.UTF8.GetBytes(csv)];

        using var preview = await it.PostFileAsync(PreviewPath, FileName, content, CsvType);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var job = await it.ReadJsonAsync<JsonElement>(preview);
        Assert.Empty(job.GetProperty("errors").EnumerateArray());
        using var commit = await it.PostAsync($"{ImportPath}/{job.GetProperty("jobId").GetGuid()}/commit");
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);

        await using var db = factory.Database.CreateDbContext();
        var id = await db.Assets.Select(a => a.Id).SingleAsync(Ct);
        using var read = await it.GetAsync($"{AssetsPath}/{id}");
        var asset = await it.ReadJsonAsync<JsonElement>(read);
        Assert.Equal(Tag, asset.GetProperty("assetTag").GetString());
        Assert.Equal(Brand, asset.GetProperty("brand").GetString());
        Assert.Equal(Model, asset.GetProperty("model").GetString());
        Assert.Equal(Location, asset.GetProperty("locationName").GetString());
        Assert.Equal(Letters, asset.GetProperty("notes").GetString());
    }

    [Fact]
    public async Task Excel_export_keeps_turkish_letters_of_a_ticket_subject()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee, Subject);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var window = $"from={today.AddDays(-1).ToString(DayFormat, System.Globalization.CultureInfo.InvariantCulture)}" +
                     $"&to={today.AddDays(1).ToString(DayFormat, System.Globalization.CultureInfo.InvariantCulture)}";

        using var response = await world.Admin.GetAsync($"{ReportsPath}/incidents/export?metric=volume&{window}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync(Ct));
        using var workbook = new XLWorkbook(stream);
        Assert.Contains(Subject, workbook.Worksheet(RecordsSheet).CellsUsed().Select(c => c.GetString()));
    }

    [Fact]
    public async Task Email_body_written_to_disk_keeps_turkish_letters_of_a_ticket_subject()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee, Subject);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(Batch, Lease, Ct);
            await scope.ServiceProvider.GetRequiredService<SendDueEmailsHandler>().HandleAsync(Batch, Lease, Ct);
        }

        var rendered = Assert.Single(factory.EmailSender.Sent);
        Assert.Contains(Subject, rendered.Body, StringComparison.Ordinal);
        var folder = Path.Combine(Path.GetTempPath(), "tgysm-turkish-" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new ServiceCollection();
            services.AddOptions<EmailOptions>().Configure(o =>
            {
                o.Mode = EmailModes.Pickup;
                o.From = PickupFrom;
                o.PickupFolder = folder;
            });
            await using var provider = services.BuildServiceProvider();
            var pickup = new PickupFolderEmailSender(provider.GetRequiredService<IOptionsMonitor<EmailOptions>>());

            await pickup.SendAsync(new EmailMessage(rendered.To, rendered.ToName, rendered.Subject, rendered.Body), Ct);

            var loaded = await MimeMessage.LoadAsync(Assert.Single(Directory.GetFiles(folder, EmlPattern)), Ct);
            Assert.Contains(Subject, loaded.TextBody, StringComparison.Ordinal);
            Assert.Equal(rendered.Subject, loaded.Subject);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
