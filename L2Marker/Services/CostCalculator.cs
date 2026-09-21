using L2Marker.Models;

namespace L2Marker.Services;

/// <summary>Turns one call's billed token usage into a dollar cost, priced per AppSettings' rates.</summary>
public static class CostCalculator
{
    public static decimal CalculateCost(ApiUsage usage, AppSettings pricing) =>
        usage.InputTokens / 1_000_000m * pricing.InputPricePerMillionTokens
        + usage.OutputTokens / 1_000_000m * pricing.OutputPricePerMillionTokens
        + usage.CacheCreationInputTokens / 1_000_000m * pricing.CacheWritePricePerMillionTokens
        + usage.CacheReadInputTokens / 1_000_000m * pricing.CacheReadPricePerMillionTokens;
}
