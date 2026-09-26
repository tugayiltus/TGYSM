using System.Text;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests;

public sealed class AttachmentTypesTests
{
    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8.ToArray(), 1, 2, 3];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10];
    private static readonly byte[] Exe = [.. "MZ"u8.ToArray(), 0x90, 0, 3];

    [Theory]
    [InlineData("rapor.pdf", "application/pdf")]
    [InlineData("RAPOR.PDF", "application/pdf")]
    public void Pdf_is_detected_by_magic_bytes(string name, string expected) =>
        Assert.Equal(expected, AttachmentTypes.Detect(name, Pdf));

    [Fact]
    public void Images_are_detected_by_magic_bytes()
    {
        Assert.Equal("image/png", AttachmentTypes.Detect("ekran.png", Png));
        Assert.Equal("image/jpeg", AttachmentTypes.Detect("foto.jpg", Jpeg));
        Assert.Equal("image/jpeg", AttachmentTypes.Detect("foto.jpeg", Jpeg));
    }

    [Fact]
    public void Text_and_csv_must_be_utf8_without_nul()
    {
        Assert.Equal("text/plain; charset=utf-8", AttachmentTypes.Detect("not.txt", Encoding.UTF8.GetBytes("Yazıcı hatası: ş ğ")));
        Assert.Equal("text/csv; charset=utf-8", AttachmentTypes.Detect("liste.csv", Encoding.UTF8.GetBytes("a;b\n1;2")));
        Assert.Null(AttachmentTypes.Detect("not.txt", [0x61, 0x00, 0x62]));
        Assert.Null(AttachmentTypes.Detect("not.txt", [0xC3, 0x28]));
    }

    [Fact]
    public void Wrong_extension_or_unknown_content_is_refused()
    {
        Assert.Null(AttachmentTypes.Detect("setup.pdf", Exe));
        Assert.Null(AttachmentTypes.Detect("setup.exe", Exe));
        Assert.Null(AttachmentTypes.Detect("ekran.pdf", Png));
        Assert.Null(AttachmentTypes.Detect("rapor", Pdf));
        Assert.Null(AttachmentTypes.Detect("bos.png", []));
    }
}
