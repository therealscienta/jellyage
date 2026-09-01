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

    /// <summary>
    /// Gets or sets the unrated-items count within the current type and library scope,
    /// before the chip/search/rating filters narrow the list. This backs the "Unrated"
    /// chip's badge, so it must predict what clicking that chip returns. For the
    /// server-wide figure shown beside "Run Now", see <see cref="GlobalCountsDto"/>.
    /// </summary>
    public int UnratedCount { get; set; }

    /// <summary>
    /// Gets or sets the pending-conversion count within the current type and library scope,
    /// before the chip/search/rating filters narrow the list. This backs the "Has pending
    /// change" chip's badge. For the server-wide figure shown beside "Run Now", see
    /// <see cref="GlobalCountsDto"/>.
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Gets or sets the count of items that still need a mapping — their OfficialRating has no
    /// entry in the mapping table and no Custom rating has resolved them — within the current
    /// type and library scope, before the chip/search/rating filters narrow the list. This backs
    /// the "No mapping match" chip's badge, so — like the two counts above — it must predict what
    /// clicking that chip returns. Items already carrying a Custom rating are excluded: nothing
    /// will change them, so they are not actionable here. The unresolved mapping-table gap itself
    /// is reported per rating value by <c>GET /AgeRating/UnmappedRatings</c> instead.
    /// </summary>
    public int NoMappingCount { get; set; }
}
