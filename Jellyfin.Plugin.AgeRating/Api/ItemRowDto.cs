using System;

namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// A single row in the unified items list: current rating plus the rating the
/// active mapping would assign, if any.
/// </summary>
public class ItemRowDto
{
    /// <summary>Gets or sets the Jellyfin item ID.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the item name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the item type ("Movie" or "Series").</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item's OfficialRating — the value sourced from
    /// metadata providers (TMDb/OMDb) and the NFO's <c>&lt;mpaa&gt;</c> tag.
    /// The plugin treats this as read-only; it's the source signal for the
    /// mapping lookup.
    /// </summary>
    public string? CurrentRating { get; set; }

    /// <summary>
    /// Gets or sets the item's CustomRating — the plugin's own lane
    /// (<c>&lt;customrating&gt;</c> in NFO). This is what automation writes
    /// and what bulk-edit sets. Jellyfin uses this for parental control
    /// when set, falling back to OfficialRating otherwise.
    /// </summary>
    public string? CustomRating { get; set; }

    /// <summary>
    /// Gets or sets the rating that would be assigned to CustomRating on the
    /// next conversion run, or null if no mapping applies or the current
    /// CustomRating already matches / would be protected.
    /// </summary>
    public string? ProposedRating { get; set; }
}
