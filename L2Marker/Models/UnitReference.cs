namespace L2Marker.Models;

/// <summary>One assessment criterion for a unit, e.g. "1.1 Define what is meant by CPU".</summary>
public class CriterionDefinition
{
    public string Id { get; set; } = "";
    public string QuestionText { get; set; } = "";
}

/// <summary>Everything needed to mark and report on one NCFE unit.</summary>
public class UnitReference
{
    public int UnitNumber { get; set; }
    public string UnitTitle { get; set; } = "";
    public List<CriterionDefinition> Criteria { get; set; } = new();

    /// <summary>Full extracted text of the model answers document, used as the marking rubric.</summary>
    public string ModelAnswersText { get; set; } = "";

    /// <summary>Path to the blank "Assessor Feedback to Learner" template for this unit.</summary>
    public string TemplatePath { get; set; } = "";
}
