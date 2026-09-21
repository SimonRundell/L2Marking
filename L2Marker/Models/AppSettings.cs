namespace L2Marker.Models;

/// <summary>Persisted application configuration, stored as settings.json next to the executable.</summary>
public class AppSettings
{
    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    public string ReferenceFolder { get; set; } =
        @"C:\Users\SPR\OneDrive - Exeter College\Planning\L2 Coding\Workbooks - All Units";

    public int MaxConcurrency { get; set; } = 3;

    public int MaxOutputTokens { get; set; } = 4096;

    public string AssessorName { get; set; } = "Simon Rundell";
}
