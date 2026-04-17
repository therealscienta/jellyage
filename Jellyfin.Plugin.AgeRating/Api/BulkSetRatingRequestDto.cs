using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// Request body for the bulk-set-rating endpoint.
/// </summary>
public class BulkSetRatingRequestDto
{
    /// <summary>Gets or sets the Jellyfin item IDs to update.</summary>
    public IReadOnlyList<Guid> ItemIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the rating to set on every listed item. An empty string clears the rating.
    /// </summary>
    public string Rating { get; set; } = string.Empty;
}
