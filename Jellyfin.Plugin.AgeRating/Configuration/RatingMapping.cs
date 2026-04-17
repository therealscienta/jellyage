namespace Jellyfin.Plugin.AgeRating.Configuration;

/// <summary>
/// A single source-to-target age rating mapping entry.
/// </summary>
public class RatingMapping
{
    /// <summary>
    /// Gets or sets the source rating value (e.g. "PG-13").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target rating value (e.g. "12A").
    /// </summary>
    public string Target { get; set; } = string.Empty;
}
