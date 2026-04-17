using System;

namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// Represents a preview of a pending rating change.
/// </summary>
public class RatingPreviewDto
{
    /// <summary>Gets or sets the Jellyfin item ID.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the item name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the item type (Movie, Series, Episode).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the current official rating.</summary>
    public string? CurrentRating { get; set; }

    /// <summary>Gets or sets the proposed rating after conversion.</summary>
    public string ProposedRating { get; set; } = string.Empty;
}
