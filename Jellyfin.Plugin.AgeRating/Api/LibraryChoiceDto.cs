namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// A selectable library for the main page's "filter by library" dropdown.
/// Only Movie, TV and Mixed libraries are offered — those are the only ones
/// the conversion task touches.
/// </summary>
public class LibraryChoiceDto
{
    /// <summary>Gets or sets the virtual-folder item id, used as the libraryId query value.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Gets or sets the library's display name (as shown in the Dashboard).</summary>
    public string Name { get; set; } = string.Empty;
}
