namespace L2Marker.Services;

/// <summary>
/// Known-good $/million-token pricing for specific Claude models, so Settings can offer a
/// one-click "use standard pricing" instead of the teacher having to look prices up by hand.
/// Not exhaustive - prices move and new models appear, so anything not listed here just isn't
/// offered as a preset; the teacher can still type prices in manually from Anthropic's pricing
/// page. Cache write is 1.25x the input price and cache read is 0.1x, per Anthropic's standard
/// 5-minute-TTL prompt caching multipliers.
/// </summary>
public static class ClaudePricingPresets
{
    public record Preset(decimal Input, decimal Output, decimal CacheWrite, decimal CacheRead);

    private static readonly Dictionary<string, Preset> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-haiku-4-5-20251001"] = new Preset(1.00m, 5.00m, 1.25m, 0.10m),
        ["claude-sonnet-5"] = new Preset(2.00m, 10.00m, 2.50m, 0.20m),
    };

    public static bool TryGet(string model, out Preset preset) => Presets.TryGetValue(model.Trim(), out preset!);
}
