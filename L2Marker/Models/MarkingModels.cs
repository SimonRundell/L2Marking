namespace L2Marker.Models;

/// <summary>Marking outcome for a single criterion, as returned by Claude.</summary>
public class CriterionResult
{
    public string Id { get; set; } = "";
    public bool Achieved { get; set; }
    public string Comment { get; set; } = "";
}

/// <summary>Full marking outcome for one student's submission.</summary>
public class StudentMarkingResult
{
    public string SourceFilePath { get; set; } = "";
    public string LearnerName { get; set; } = "";
    public List<CriterionResult> Criteria { get; set; } = new();
    public bool OverallAchieved { get; set; }
    public string OverallFeedback { get; set; } = "";
    public string FurtherActions { get; set; } = "";

    /// <summary>Set when marking failed for this student (API error, unreadable file, etc).</summary>
    public string? Error { get; set; }

    public string? GeneratedMarksheetPath { get; set; }

    /// <summary>Token usage this call was billed for, or null if the call never got a response (error before/without usage).</summary>
    public ApiUsage? Usage { get; set; }

    /// <summary>Local cost estimate for this one call, priced per the settings in effect when it was marked.</summary>
    public decimal EstimatedCostUsd { get; set; }

    public int AchievedCount => Criteria.Count(c => c.Achieved);
    public int TotalCount => Criteria.Count;
}
