namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// Server-wide unrated and pending-conversion counts, deliberately unaffected by
/// any list filter. These back the main page's Automation card, which sits next to
/// the "Run Now" button — and that button always converts every library, so the
/// numbers beside it must describe the same scope.
/// </summary>
public class GlobalCountsDto
{
    /// <summary>Gets or sets the number of Movies and Series with no effective rating, across all libraries.</summary>
    public int UnratedCount { get; set; }

    /// <summary>Gets or sets the number of Movies and Series whose next conversion run would change their CustomRating, across all libraries.</summary>
    public int PendingCount { get; set; }
}
