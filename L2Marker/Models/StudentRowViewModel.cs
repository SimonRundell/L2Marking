namespace L2Marker.Models;

/// <summary>One row in the main batch grid.</summary>
public class StudentRowViewModel
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public string LearnerName { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string Result { get; set; } = "";
    public StudentMarkingResult? MarkingResult { get; set; }
    public string Cost => MarkingResult?.Usage is not null ? $"${MarkingResult.EstimatedCostUsd:0.0000}" : "";
}
