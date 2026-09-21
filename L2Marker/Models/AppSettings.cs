namespace L2Marker.Models;

/// <summary>Persisted application configuration, stored as settings.json next to the executable.</summary>
public class AppSettings
{
    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    public string ReferenceFolder { get; set; } =
        @"C:\Users\SPR\OneDrive - Exeter College\";

    /// <summary>Where generated marksheets are written. Blank (the default) means "next to each
    /// student's own file" - set this to send every generated marksheet to one folder instead.</summary>
    public string OutputFolder { get; set; } = "";

    public int MaxConcurrency { get; set; } = 3;

    public int MaxOutputTokens { get; set; } = 4096;

    public string AssessorName { get; set; } = "Simon Rundell";

    // --- Local spend tracking. Anthropic's API has no "check my balance" endpoint, so this is a
    // running total computed from each call's own reported token usage (see ApiUsage) priced at
    // these hand-set $/million-token rates - a local estimate, not a query against the real
    // invoice. Defaults match Claude Haiku 4.5's published pricing; update these if the model
    // changes or Anthropic's prices move. Cache write/read prices matter as soon as prompt
    // caching kicks in (see ClaudeMarkingService) - leaving them at 0 would under-report cost. ---

    public decimal InputPricePerMillionTokens { get; set; } = 1.00m;

    public decimal OutputPricePerMillionTokens { get; set; } = 5.00m;

    public decimal CacheWritePricePerMillionTokens { get; set; } = 1.25m;

    public decimal CacheReadPricePerMillionTokens { get; set; } = 0.10m;

    /// <summary>Teacher-set ceiling in USD, or null for no budget/warnings.</summary>
    public decimal? BudgetUsd { get; set; }

    /// <summary>Running lifetime total spent, accumulated locally from every call this app has made.</summary>
    public decimal SpentUsd { get; set; }
}
