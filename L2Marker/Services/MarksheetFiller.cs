using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using L2Marker.Models;

namespace L2Marker.Services;

/// <summary>
/// Clones the unit's blank "Assessor Feedback to Learner" template and fills it in for one learner:
/// name, per-criterion achieved/not-achieved + feedback, and the overall verdict/feedback/actions.
/// Learner and assessor signatures/dates are deliberately left blank for manual sign-off.
/// </summary>
public static class MarksheetFiller
{
    public static string CreateFilledMarksheet(UnitReference unit, StudentMarkingResult result, string assessorName)
    {
        var studentDir = Path.GetDirectoryName(result.SourceFilePath) ?? ".";
        var safeName = MakeSafeFileName(result.LearnerName);
        var destPath = Path.Combine(studentDir, $"{safeName} Unit {unit.UnitNumber} FB.docx");
        destPath = AvoidOverwrite(destPath);

        File.Copy(unit.TemplatePath, destPath, overwrite: false);

        using (var doc = WordprocessingDocument.Open(destPath, true))
        {
            var body = doc.MainDocumentPart?.Document?.Body
                ?? throw new InvalidOperationException("Template document has no body.");

            var today = DateTime.Now.ToString("dd/MM/yyyy");
            var tables = body.Elements<Table>().ToList();

            FillLearnerInfoTable(tables, result.LearnerName);
            FillCriteriaTable(tables, unit, result, today);
            FillSingleValueTable(tables, "Has the learner achieved", result.OverallAchieved ? "ACHIEVED" : "NOT YET ACHIEVED", sameRow: true);
            FillSingleValueTable(tables, "Feedback from Assessor to Learner", result.OverallFeedback, sameRow: false);
            FillSingleValueTable(tables, "Any further actions", result.FurtherActions, sameRow: false);

            doc.MainDocumentPart!.Document.Save();
        }

        return destPath;
    }

    private static void FillLearnerInfoTable(List<Table> tables, string learnerName)
    {
        foreach (var table in tables)
        {
            var firstRow = table.Elements<TableRow>().FirstOrDefault();
            var cells = firstRow?.Elements<TableCell>().ToList();
            if (cells is null || cells.Count < 2) continue;

            if (GetCellText(cells[0]).Trim().Equals("Learner", StringComparison.OrdinalIgnoreCase))
            {
                SetCellText(cells[1], learnerName);
                return;
            }
        }
    }

    private static void FillCriteriaTable(List<Table> tables, UnitReference unit, StudentMarkingResult result, string today)
    {
        foreach (var table in tables)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 3) continue;

            // Row 0's "Attempt 1"/"Attempt 2" cells are merged (gridSpan), so it has fewer than 6
            // real <w:tc> elements - only check its first/last cell text, not the count.
            var header = rows[0].Elements<TableCell>().Select(GetCellText).ToList();
            if (header.Count < 2) continue;
            if (!header[0].Trim().Equals("Criteria", StringComparison.OrdinalIgnoreCase)) continue;
            if (!header[^1].Trim().Equals("Feedback", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var row in rows.Skip(2))
            {
                var cells = row.Elements<TableCell>().ToList();
                if (cells.Count < 6) continue;

                var idText = GetCellText(cells[0]).Replace(' ', ' ').Trim();
                var match = System.Text.RegularExpressions.Regex.Match(idText, @"^(\d+\.\d+)");
                if (!match.Success) continue;
                var id = match.Groups[1].Value;

                var criterionResult = result.Criteria.FirstOrDefault(c => c.Id == id);
                if (criterionResult is null) continue;

                SetCellText(cells[1], today);
                SetCellText(cells[2], criterionResult.Achieved ? "Achieved" : "Not Achieved");
                SetCellText(cells[5], criterionResult.Comment);
            }

            return; // only one such table expected
        }
    }

    /// <summary>Fills a small label/value table. When sameRow is true the value goes in column 1 of
    /// row 0 (e.g. "Has the learner achieved... | ANSWER"); otherwise it goes in column 0 of row 1
    /// (a label row followed by a blank value row).</summary>
    private static void FillSingleValueTable(List<Table> tables, string labelStartsWith, string value, bool sameRow)
    {
        foreach (var table in tables)
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0) continue;

            var firstRowCells = rows[0].Elements<TableCell>().ToList();
            if (firstRowCells.Count == 0) continue;

            var label = GetCellText(firstRowCells[0]).Trim();
            if (!label.StartsWith(labelStartsWith, StringComparison.OrdinalIgnoreCase)) continue;

            if (sameRow)
            {
                if (firstRowCells.Count < 2) continue;
                SetCellText(firstRowCells[1], value);
            }
            else
            {
                if (rows.Count < 2) continue;
                var valueCell = rows[1].Elements<TableCell>().FirstOrDefault();
                if (valueCell is null) continue;
                SetCellText(valueCell, value);
            }
            return;
        }
    }

    private static string GetCellText(TableCell cell)
    {
        var lines = cell.Elements<Paragraph>()
            .Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text)))
            .Where(t => !string.IsNullOrWhiteSpace(t));
        return string.Join(" ", lines);
    }

    /// <summary>Replaces a cell's content with the given text, reusing the formatting of whatever
    /// run/paragraph was already there so the marksheet's look and feel is preserved.</summary>
    private static void SetCellText(TableCell cell, string text)
    {
        var templateParagraph = cell.Elements<Paragraph>().FirstOrDefault();
        var templateParaProps = templateParagraph?.ParagraphProperties?.CloneNode(true) as ParagraphProperties;
        var templateRunProps = templateParagraph?.Descendants<Run>().FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;

        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0) lines = new[] { "" };

        cell.RemoveAllChildren<Paragraph>();

        foreach (var line in lines)
        {
            var paragraph = new Paragraph();
            if (templateParaProps is not null)
                paragraph.ParagraphProperties = (ParagraphProperties)templateParaProps.CloneNode(true);

            var run = new Run();
            if (templateRunProps is not null)
                run.RunProperties = (RunProperties)templateRunProps.CloneNode(true);

            run.AppendChild(new Text(line) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
            paragraph.AppendChild(run);
            cell.AppendChild(paragraph);
        }
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, ' ');
        return string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string AvoidOverwrite(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
