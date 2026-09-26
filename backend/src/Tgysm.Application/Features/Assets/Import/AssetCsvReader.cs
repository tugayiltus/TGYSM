using System.Text;

namespace Tgysm.Application.Features.Assets.Import;

/// <summary>
/// Minimal RFC 4180 reader: quoted cells, doubled quotes, line breaks inside quotes, CRLF or LF.
/// The delimiter is taken from the header line, so files saved by Turkish Excel (<c>;</c>) are read too.
/// Cells are plain text; nothing is evaluated.
/// </summary>
internal static class AssetCsvReader
{
    private const char Quote = '"';
    private const char Comma = ',';
    private const char Semicolon = ';';
    private const char CarriageReturn = '\r';
    private const char LineFeed = '\n';

    public static List<List<string>> Read(string text)
    {
        var delimiter = DetectDelimiter(text);
        var records = new List<List<string>>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        void EndCell()
        {
            cells.Add(cell.ToString().Trim());
            cell.Clear();
        }

        void EndRecord()
        {
            EndCell();
            records.Add(cells);
            cells = [];
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c != Quote)
                {
                    cell.Append(c);
                }
                else if (i + 1 < text.Length && text[i + 1] == Quote)
                {
                    cell.Append(Quote);
                    i++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else if (c == Quote && cell.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                EndCell();
            }
            else if (c is CarriageReturn or LineFeed)
            {
                if (c == CarriageReturn && i + 1 < text.Length && text[i + 1] == LineFeed)
                {
                    i++;
                }

                EndRecord();
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            EndRecord();
        }

        return records;
    }

    private static char DetectDelimiter(string text)
    {
        var end = text.IndexOfAny([CarriageReturn, LineFeed]);
        var header = end < 0 ? text : text[..end];
        return header.Count(c => c == Semicolon) > header.Count(c => c == Comma) ? Semicolon : Comma;
    }
}
