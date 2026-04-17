namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// Response for the bulk-set-rating endpoint.
/// </summary>
public class BulkSetRatingResponseDto
{
    /// <summary>Gets or sets the number of items that were successfully updated.</summary>
    public int UpdatedCount { get; set; }
}
