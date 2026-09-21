using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace L2Marker.Services;

/// <summary>
/// Pulls plain text out of .docx files using the OpenXML SDK, in reading order.
/// No Word installation is required. Images and screenshots are not read (text and tables only).
/// </summary>
public static class DocxTextExtractor
{
    /// <summary>Extracts the whole document body as plain text, preserving paragraph/table order.
    /// Heading paragraphs are prefixed with "## " so structure survives into the prompt.</summary>
    public static string ExtractOrderedText(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return "";

        var sb = new StringBuilder();
        foreach (var element in body.Elements())
        {
            switch (element)
            {
                case Paragraph para:
                    {
                        var text = GetParagraphText(para);
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                        var prefix = styleId.Contains("Heading", StringComparison.OrdinalIgnoreCase) ? "## " : "";
                        sb.AppendLine(prefix + text.Trim());
                        break;
                    }
                case Table table:
                    {
                        foreach (var row in table.Elements<TableRow>())
                        {
                            var cells = row.Elements<TableCell>()
                                .Select(GetCellText)
                                .Where(t => !string.IsNullOrWhiteSpace(t));
                            var line = string.Join(" | ", cells);
                            if (!string.IsNullOrWhiteSpace(line))
                                sb.AppendLine(line);
                        }
                        break;
                    }
            }
        }
        return sb.ToString();
    }

    /// <summary>Best-effort learner name: looks for a "Name" label cell in the document's first table,
    /// otherwise falls back to the file name with common suffixes stripped.</summary>
    public static string ExtractLearnerName(string path)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(path, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            var firstTable = body?.Elements<Table>().FirstOrDefault();
            if (firstTable is not null)
            {
                foreach (var row in firstTable.Elements<TableRow>())
                {
                    var cells = row.Elements<TableCell>().Select(GetCellText).ToList();
                    for (int i = 0; i < cells.Count - 1; i++)
                    {
                        if (cells[i].Trim().Equals("Name", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(cells[i + 1]))
                        {
                            return cells[i + 1].Trim();
                        }
                    }
                }
            }
        }
        catch
        {
            // fall through to filename guess
        }

        return GuessNameFromFileName(path);
    }

    private static string GuessNameFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        string[] junk =
        {
            "unit 1 assessment", "unit 2 assessment", "unit 3 assessment", "unit 4 assessment", "unit 5 assessment",
            "unit 1 assesment", "unit 2 assesment", "unit 3 assesment", "unit 4 assesment", "unit 5 assesment",
            "level 2 coding work book 1", "level 2 coding work book 2", "level 2 coding work book 3",
            "level 2 coding workbook 4", "level 2 coding workbook 5",
            "workbook", "resubmission", "resubmitted assessment", "attempt 1", "attempt 2"
        };
        var lower = name.ToLowerInvariant();
        foreach (var j in junk)
        {
            var idx = lower.IndexOf(j, StringComparison.Ordinal);
            if (idx >= 0)
            {
                name = name[..idx];
                lower = name.ToLowerInvariant();
            }
        }
        return name.Trim(' ', '-', '_', '.');
    }

    private static string GetParagraphText(Paragraph para)
    {
        return string.Concat(para.Descendants<Text>().Select(t => t.Text));
    }

    private static string GetCellText(TableCell cell)
    {
        var lines = cell.Elements<Paragraph>().Select(GetParagraphText).Where(t => !string.IsNullOrWhiteSpace(t));
        return string.Join(" / ", lines);
    }
}
