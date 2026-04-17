using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AgeRating.Configuration;

/// <summary>
/// Plugin configuration for Age Rating Converter.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether conversion runs automatically after each library scan.
    /// </summary>
    public bool EnableAutoConversion { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether items that already carry a rating are overwritten.
    /// When false, only items whose current rating matches a source entry are converted if they have
    /// no recognised target-system rating yet.
    /// </summary>
    public bool OverwriteExistingRatings { get; set; } = false;

    /// <summary>
    /// Gets or sets comma-separated rating values that are treated as "no rating".
    /// </summary>
    public string UnratedValues { get; set; } = "NR,Not Rated,Unrated,Unknown,UR,0";

    /// <summary>
    /// Gets or sets the JSON-serialised list of <see cref="RatingMapping"/> entries.
    /// An empty string means no mappings are configured and the conversion task
    /// becomes a no-op until the user either loads built-in defaults or adds rows
    /// manually. The plugin no longer silently falls back to a hardcoded set.
    /// </summary>
    public string MappingTableJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the target rating system used when generating
    /// built-in defaults (e.g. "BBFC", "FSK"). When set, "Load Built-in Defaults"
    /// emits rows mapping every supported source-system rating to this system's
    /// equivalent. Empty means no target has been chosen yet and the Load Defaults
    /// action is disabled in the UI.
    /// </summary>
    public string DefaultTargetSystem { get; set; } = string.Empty;
}
