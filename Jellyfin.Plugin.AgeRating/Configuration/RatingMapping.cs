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

    /// <summary>
    /// Gets or sets a value indicating whether this mapping row was added
    /// manually by the admin (as opposed to being seeded from built-in defaults).
    /// Rows persisted before this field existed deserialise with the default
    /// <c>false</c>, which matches their provenance (they were seeded / edited
    /// before manual tagging existed).
    /// </summary>
    public bool IsManual { get; set; }
}
