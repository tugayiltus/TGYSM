using System.Net;
using System.Text;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Tickets;

[Collection(ApiCollection.Name)]
public sealed class TicketCommentTests(ApiFactory factory) : IAsyncLifetime
{
    private const string PngType = "image/png";
    private const string PdfType = "application/pdf";
    private const int TooManyFiles = 6;
    private const int MaxBytes = 10 * 1024 * 1024;
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8.ToArray(), 1, 2, 3];

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Dictionary<string, string> Fields(string body, string visibility = "public") => new() { ["body"] = body, ["visibility"] = visibility };

    private static async Task<HttpResponseMessage> CommentAsync(ApiClient client, JsonElement ticket, string body, string visibility = "public", params (string, byte[], string)[] files) =>
        await client.PostMultipartAsync(TicketWorld.PathOf(ticket, "comments"), Fields(body, visibility), files);

    private static async Task<List<JsonElement>> CommentsAsync(ApiClient client, JsonElement ticket) =>
        [.. (await TicketWorld.ReadAsync(client, TicketWorld.PathOf(ticket, "comments"))).EnumerateArray()];

    private static string AttachmentPath(JsonElement ticket, JsonElement attachment) =>
        TicketWorld.PathOf(ticket, $"attachments/{attachment.GetProperty("id").GetGuid()}");

    [Fact]
    public async Task Employee_reply_with_an_image_is_visible_and_downloadable_for_both_sides()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var post = await CommentAsync(world.Employee, ticket, "Ekran görüntüsü ekte", "public", ("ekran.png", Png, PngType));
        var comments = await CommentsAsync(world.Desk, ticket);
        var attachment = comments.Single().GetProperty("attachments")[0];
        using var employeeDownload = await world.Employee.GetAsync(AttachmentPath(ticket, attachment));
        using var deskDownload = await world.Desk.GetAsync(AttachmentPath(ticket, attachment));

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.Equal("Ekran görüntüsü ekte", comments.Single().GetProperty("body").GetString());
        Assert.Equal("Ayşe Kaya", comments.Single().GetProperty("authorName").GetString());
        Assert.Equal("ekran.png", attachment.GetProperty("originalName").GetString());
        Assert.Equal(HttpStatusCode.OK, employeeDownload.StatusCode);
        Assert.Equal(Png, await deskDownload.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PngType, deskDownload.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", deskDownload.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task Internal_notes_and_their_files_stay_hidden_from_employees()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var note = await CommentAsync(world.Desk, ticket, "Tedarikçi ile görüşüldü", "internal", ("teklif.pdf", Pdf, PdfType));
        using var reply = await CommentAsync(world.Desk, ticket, "İnceliyoruz");
        var deskComments = await CommentsAsync(world.Desk, ticket);
        var internalFile = deskComments.Single(c => c.GetProperty("visibility").GetString() == "internal").GetProperty("attachments")[0];
        var employeeComments = await CommentsAsync(world.Employee, ticket);
        using var employeeDownload = await world.Employee.GetAsync(AttachmentPath(ticket, internalFile));
        var employeeTimeline = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(ticket, "timeline"));
        var deskTimeline = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket, "timeline"));

        Assert.Equal(2, deskComments.Count);
        Assert.Equal(["İnceliyoruz"], employeeComments.Select(c => c.GetProperty("body").GetString()));
        Assert.Equal(HttpStatusCode.NotFound, employeeDownload.StatusCode);
        Assert.DoesNotContain(employeeTimeline.EnumerateArray(), e => e.GetProperty("kind").GetString() == "internal_note");
        Assert.Contains(deskTimeline.EnumerateArray(), e => e.GetProperty("kind").GetString() == "internal_note");
    }

    [Fact]
    public async Task Employees_cannot_write_internal_notes_and_other_teams_cannot_comment()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var employeeNote = await CommentAsync(world.Employee, ticket, "Gizli", "internal");
        using var field = await CommentAsync(world.Field, ticket, "Merhaba");
        using var empty = await CommentAsync(world.Employee, ticket, " ");

        Assert.Equal(ErrorCodes.CommentInternalNotAllowed, await ApiClient.ReadCodeAsync(employeeNote));
        Assert.Equal(HttpStatusCode.NotFound, field.StatusCode);
        Assert.Contains(ErrorCodes.Required, (await ApiClient.ReadFieldCodesAsync(empty))["body"]);
    }

    [Fact]
    public async Task Attachment_limits_are_enforced()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var exe = (Name: "kurulum.pdf", Content: "MZ\u0090\u0000"u8.ToArray(), Type: PdfType);
        var nulText = (Name: "not.txt", Content: new byte[] { 0x61, 0x00 }, Type: "text/plain");
        var large = new byte[MaxBytes + 1];
        Png.CopyTo(large, 0);

        using var many = await CommentAsync(world.Employee, ticket, "Çok dosya", "public", [.. Enumerable.Range(0, TooManyFiles).Select(i => ($"e{i}.png", Png, PngType))]);
        using var disguised = await CommentAsync(world.Employee, ticket, "Dosya", "public", exe);
        using var withNul = await CommentAsync(world.Employee, ticket, "Dosya", "public", nulText);
        using var tooLarge = await CommentAsync(world.Employee, ticket, "Dosya", "public", ("buyuk.png", large, PngType));

        Assert.Contains(ErrorCodes.AttachmentTooMany, (await ApiClient.ReadFieldCodesAsync(many))["files"]);
        Assert.Contains(ErrorCodes.AttachmentTypeNotAllowed, (await ApiClient.ReadFieldCodesAsync(disguised))["files"]);
        Assert.Contains(ErrorCodes.AttachmentTypeNotAllowed, (await ApiClient.ReadFieldCodesAsync(withNul))["files"]);
        Assert.Contains(ErrorCodes.AttachmentTooLarge, (await ApiClient.ReadFieldCodesAsync(tooLarge))["files"]);
        Assert.Empty(await CommentsAsync(world.Desk, ticket));
    }

    [Fact]
    public async Task Out_of_scope_download_is_not_found()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        using var _ = await CommentAsync(world.Employee, ticket, "Ek", "public", ("ekran.png", Png, PngType));
        var attachment = (await CommentsAsync(world.Employee, ticket)).Single().GetProperty("attachments")[0];

        using var other = await world.OtherEmployee.GetAsync(AttachmentPath(ticket, attachment));
        using var field = await world.Field.GetAsync(AttachmentPath(ticket, attachment));
        var otherTicket = await TicketWorld.CreateIncidentAsync(world.OtherEmployee, "Başka");
        using var wrongTicket = await world.OtherEmployee.GetAsync(AttachmentPath(otherTicket, attachment));

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, field.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongTicket.StatusCode);
    }

    [Fact]
    public async Task Completed_ticket_still_accepts_comments()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        using var _ = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "complete"), new { version = TicketWorld.VersionOf(ticket), resolution = "Çözüldü", outcome = "resolved" });

        using var comment = await CommentAsync(world.Employee, ticket, Encoding.UTF8.GetString("Teşekkürler"u8));

        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
    }
}
