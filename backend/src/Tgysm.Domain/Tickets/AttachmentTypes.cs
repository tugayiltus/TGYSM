using System.Text;

namespace Tgysm.Domain.Tickets;

/// <summary>
/// Allowed attachment types (ADR-0012): PDF, PNG and JPEG by magic bytes; TXT and CSV when the whole file is valid
/// UTF-8 without NUL. The extension must match the content, so an executable renamed to .pdf is refused.
/// </summary>
public static class AttachmentTypes
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxFilesPerUpload = 5;

    private const string PdfType = "application/pdf";
    private const string PngType = "image/png";
    private const string JpegType = "image/jpeg";
    private const string TextType = "text/plain; charset=utf-8";
    private const string CsvType = "text/csv; charset=utf-8";
    private const byte Nul = 0;

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Returns the content type to store and serve, or null when the file is not allowed.</summary>
    public static string? Detect(string fileName, ReadOnlySpan<byte> content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" when content.StartsWith(PdfMagic) => PdfType,
            ".png" when content.StartsWith(PngMagic) => PngType,
            ".jpg" or ".jpeg" when content.StartsWith(JpegMagic) => JpegType,
            ".txt" when IsCleanText(content) => TextType,
            ".csv" when IsCleanText(content) => CsvType,
            _ => null,
        };
    }

    private static bool IsCleanText(ReadOnlySpan<byte> content)
    {
        if (content.Contains(Nul))
        {
            return false;
        }

        try
        {
            StrictUtf8.GetCharCount(content);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
