namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// Per-library persistence status: whether a plugin-written Custom rating
/// will be saved to an NFO on disk for items in this library, or only to
/// Jellyfin's database.
/// </summary>
public class LibraryPersistenceDto
{
    /// <summary>Gets or sets the library's display name (as shown in the Dashboard).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the virtual-folder item id, usable to deep-link to the library in the Dashboard.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the Nfo metadata saver is selected for this library.</summary>
    public bool NfoSaverEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the library's "Save artwork and metadata into media folders" option is enabled.</summary>
    public bool SaveLocalMetadata { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Custom rating changes written by this plugin will land in
    /// an NFO file on disk for this library. True when the Nfo saver is enabled and
    /// either SaveLocalMetadata is on or an NFO already exists alongside the item —
    /// see <see cref="NfoSaverEnabled"/> for the coarse toggle that most admins care about.
    /// </summary>
    public bool PersistsToDisk { get; set; }
}
