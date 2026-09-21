using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using L2Marker.Models;

namespace L2Marker.Services;

/// <summary>
/// Locates the model-answers and blank-marksheet-template files for a given unit inside the
/// reference folder, and parses the marksheet template's criteria table into a structured list.
/// </summary>
public static class UnitReferenceService
{
    private static readonly Regex CriterionIdPattern = new(@"^(\d+\.\d+)\s*(.*)$", RegexOptions.Compiled);

    public static UnitReference Load(string referenceFolder, int unitNumber)
    {
        var templatePath = FindTemplateFile(referenceFolder, unitNumber)
            ?? throw new FileNotFoundException(
                $"Could not find a blank 'Unit {unitNumber} Assessor Feedback to Learner.docx' template in {referenceFolder}");

        var modelAnswersPath = FindModelAnswersFile(referenceFolder, unitNumber)
            ?? throw new FileNotFoundException(
                $"Could not find 'NCFE_L2_Unit{unitNumber}_ModelAnswers.docx' in {referenceFolder}");

        var criteria = ParseCriteriaFromTemplate(templatePath);
        if (criteria.Count == 0)
            throw new InvalidOperationException($"No criteria rows were found in template: {templatePath}");

        var modelAnswersText = DocxTextExtractor.ExtractOrderedText(modelAnswersPath);
        var unitTitle = ExtractUnitTitle(templatePath, unitNumber);

        return new UnitReference
        {
            UnitNumber = unitNumber,
            UnitTitle = unitTitle,
            Criteria = criteria,
            ModelAnswersText = modelAnswersText,
            TemplatePath = templatePath
        };
    }

    private static string? FindTemplateFile(string folder, int unitNumber)
    {
        if (!Directory.Exists(folder)) return null;

        return Directory.GetFiles(folder, "*.docx", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith("~$"))
            .FirstOrDefault(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                return Regex.IsMatch(name, $@"Unit[\s_]*0?{unitNumber}(?!\d)", RegexOptions.IgnoreCase)
                       && name.Contains("Assessor Feedback", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static string? FindModelAnswersFile(string folder, int unitNumber)
    {
        if (!Directory.Exists(folder)) return null;

        return Directory.GetFiles(folder, "*.docx", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith("~$"))
            .FirstOrDefault(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                return Regex.IsMatch(name, $@"Unit[\s_]*0?{unitNumber}(?!\d)", RegexOptions.IgnoreCase)
                       && name.Contains("ModelAnswers", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static string ExtractUnitTitle(string templatePath, int unitNumber)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(templatePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            var firstPara = body?.Elements<Paragraph>()
                .Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text)))
                .FirstOrDefault(t => t.Contains("Unit", StringComparison.OrdinalIgnoreCase)
                                      && t.Contains($"{unitNumber:00}"));
            return firstPara?.Trim() ?? $"Unit {unitNumber}";
        }
        catch
        {
            return $"Unit {unitNumber}";
        }
    }

    /// <summary>
    /// Finds the flat criteria table in the blank template - the one whose header row starts with
    /// "Criteria" and ends with "Feedback" - and reads the criterion id + question text from column 0
    /// of every data row below the two header rows.
    /// </summary>
    private static List<CriterionDefinition> ParseCriteriaFromTemplate(string templatePath)
    {
        var results = new List<CriterionDefinition>();

        using var doc = WordprocessingDocument.Open(templatePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return results;

        foreach (var table in body.Elements<Table>())
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count < 3) continue;

            var headerCells = rows[0].Elements<TableCell>().Select(GetCellPlainText).ToList();
            if (headerCells.Count < 2) continue;
            if (!headerCells[0].Trim().Equals("Criteria", StringComparison.OrdinalIgnoreCase)) continue;
            if (!headerCells[^1].Trim().Equals("Feedback", StringComparison.OrdinalIgnoreCase)) continue;

            // Found it - rows[0] and rows[1] are headers, data starts at rows[2].
            foreach (var row in rows.Skip(2))
            {
                var firstCell = row.Elements<TableCell>().FirstOrDefault();
                if (firstCell is null) continue;
                var text = GetCellPlainText(firstCell).Replace(' ', ' ').Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                var match = CriterionIdPattern.Match(text);
                if (!match.Success) continue;

                results.Add(new CriterionDefinition
                {
                    Id = match.Groups[1].Value,
                    QuestionText = match.Groups[2].Value.Trim()
                });
            }

            if (results.Count > 0) break; // use the first matching table
        }

        return results;
    }

    private static string GetCellPlainText(TableCell cell)
    {
        var lines = cell.Elements<Paragraph>()
            .Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text)))
            .Where(t => !string.IsNullOrWhiteSpace(t));
        return string.Join(" ", lines);
    }
}
