using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AgeRating.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AgeRating.Tasks;

/// <summary>
/// Converts age ratings on library items using the configured mapping table.
/// Runs automatically after every library scan.
/// </summary>
public class RatingConversionTask : ILibraryPostScanTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<RatingConversionTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingConversionTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{RatingConversionTask}"/> interface.</param>
    public RatingConversionTask(ILibraryManager libraryManager, ILogger<RatingConversionTask> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.EnableAutoConversion)
        {
            progress.Report(100);
            return;
        }

        var mappings = BuildLookup(config);
        if (mappings.Count == 0)
        {
            _logger.LogInformation("Age Rating Converter: no mappings configured, skipping.");
            progress.Report(100);
            return;
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            IsVirtualItem = false,
        });

        var total = items.Count;
        var converted = 0;
        var unmappedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i];

            var source = item.OfficialRating;
            if (string.IsNullOrWhiteSpace(source))
            {
                progress.Report(100.0 * i / total);
                continue;
            }

            if (!mappings.TryGetValue(source.Trim(), out var target))
            {
                unmappedCounts[source.Trim()] = unmappedCounts.GetValueOrDefault(source.Trim()) + 1;
                progress.Report(100.0 * i / total);
                continue;
            }

            // Respect an existing CustomRating unless the user opted in to overwrite.
            // This is where a hand-curated override (e.g. via the plugin's bulk edit
            // or Jellyfin's own metadata editor) is protected from automation.
            if (!config.OverwriteExistingRatings && !string.IsNullOrWhiteSpace(item.CustomRating))
            {
                progress.Report(100.0 * i / total);
                continue;
            }

            // Idempotency: nothing to do if CustomRating already matches the mapping target.
            if (string.Equals(item.CustomRating, target, StringComparison.OrdinalIgnoreCase))
            {
                progress.Report(100.0 * i / total);
                continue;
            }

            _logger.LogDebug("Age Rating Converter: '{Name}' Custom {Old} → {New} (source OfficialRating {Source})", item.Name, item.CustomRating ?? "(empty)", target, source);
            item.CustomRating = target;

            await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
            converted++;

            progress.Report(100.0 * i / total);
        }

        _logger.LogInformation("Age Rating Converter: converted {Count}/{Total} items.", converted, total);

        if (unmappedCounts.Count > 0)
        {
            var top = unmappedCounts
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .Select(kv => $"'{kv.Key}'×{kv.Value}");
            _logger.LogInformation(
                "Age Rating Converter: {Skipped} item(s) had no mapping. Unmapped source ratings: {Summary}",
                unmappedCounts.Values.Sum(),
                string.Join(", ", top));
        }

        progress.Report(100);
    }

    internal static Dictionary<string, string> BuildLookup(PluginConfiguration config)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(config.MappingTableJson))
        {
            // Empty table = no conversions. The plugin deliberately does NOT
            // silently fall back to a hardcoded default set here; that would
            // surprise admins who cleared the table intentionally. "Load
            // Built-in Defaults" is the explicit gesture for seeding rows.
            return lookup;
        }

        List<RatingMapping>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<RatingMapping>>(config.MappingTableJson);
        }
        catch (JsonException)
        {
            return lookup;
        }

        if (entries is null)
        {
            return lookup;
        }

        foreach (var m in entries)
        {
            if (!string.IsNullOrWhiteSpace(m.Source) && !string.IsNullOrWhiteSpace(m.Target))
            {
                lookup[m.Source.Trim()] = m.Target.Trim();
            }
        }

        return lookup;
    }
}
