namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// One row returned by the RatingSummary endpoint: an effective rating value and
/// how many library items carry it.
/// </summary>
public class RatingSummaryEntryDto
{
    /// <summary>Gets or sets the effective rating string (e.g. "TV-MA", "SE-11").</summary>
    public string Rating { get; set; } = string.Empty;

    /// <summary>Gets or sets how many items have this as their effective rating.</summary>
    public int Count { get; set; }
}
