namespace L2Marker.Models;

/// <summary>
/// Token usage from one Claude API call, straight off the Messages API response's own
/// <c>usage</c> object - never estimated, since Claude reports exactly what it billed for.
/// </summary>
/// <param name="InputTokens">The uncached remainder only - not the full prompt size. See <see cref="CacheCreationInputTokens"/>/<see cref="CacheReadInputTokens"/>.</param>
/// <param name="CacheCreationInputTokens">Tokens written to a new cache entry this call (priced at a premium over plain input tokens). Zero on a call with no prompt caching.</param>
/// <param name="CacheReadInputTokens">Tokens served from an existing cache entry this call (priced at a steep discount). Zero on a cache miss or a call with no prompt caching.</param>
public record ApiUsage(int InputTokens, int OutputTokens, int CacheCreationInputTokens = 0, int CacheReadInputTokens = 0);
