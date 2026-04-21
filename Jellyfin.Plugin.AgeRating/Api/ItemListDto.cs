using System.Collections.Generic;

namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// Paginated response for the unified items endpoint.
/// </summary>
public class ItemListDto
{
    /// <summary>Gets or sets the items on the current page.</summary>
    public IReadOnlyList<ItemRowDto> Items { get; set; } = [];

    /// <summary>Gets or sets the total item count matching the current filter (before pagination).</summary>
    public int TotalCount { get; set; }

    /// <summary>Gets or sets the 1-based page number.</summary>
    public int Page { get; set; }

    /// <summary>Gets or sets the page size.</summary>
    public int PageSize { get; set; }

    /// <summary>Gets or sets the total unrated-items count across the whole library (filter-independent).</summary>
    public int UnratedCount { get; set; }

    /// <summary>Gets or sets the total pending-conversion count across the whole library (filter-independent).</summary>
    public int PendingCount { get; set; }

    /// <summary>Gets or sets the total count of items whose OfficialRating has no matching entry in the mapping table (filter-independent).</summary>
    public int NoMappingCount { get; set; }
}
