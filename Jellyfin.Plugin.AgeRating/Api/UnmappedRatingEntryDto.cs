namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// One row returned by the UnmappedRatings endpoint: a library OfficialRating
/// value that has no entry in the user's mapping table, and how many items
/// carry it.
/// </summary>
public class UnmappedRatingEntryDto
{
    /// <summary>Gets or sets the unmapped OfficialRating value (e.g. "TV-Y").</summary>
    public string Rating { get; set; } = string.Empty;

    /// <summary>Gets or sets how many Movies + Series have this OfficialRating.</summary>
    public int Count { get; set; }
}
