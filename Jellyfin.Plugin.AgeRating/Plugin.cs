using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.AgeRating.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AgeRating;

/// <summary>
/// Age Rating Converter plugin main class.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Age Rating Converter";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("3b4a2e9f-7c1d-4e8b-a562-9f3d1c8e4a07");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;

        // Configuration surface (mapping table + automation settings). Yielded first
        // so Jellyfin's Plugins → My Plugins → ⋮ → Settings link lands here. The
        // convention in jellyfin-web is to route "Settings" to the plugin's first
        // registered page.
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", ns),
        };

        // Primary day-to-day surface: promoted to the dashboard main menu so admins
        // can jump straight to it without drilling through Plugins.
        yield return new PluginPageInfo
        {
            Name = "AgeRatings",
            DisplayName = "Age Ratings",
            EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.mainPage.html", ns),
            EnableInMainMenu = true,
            MenuSection = "server",
            MenuIcon = "label",
        };
    }
}
