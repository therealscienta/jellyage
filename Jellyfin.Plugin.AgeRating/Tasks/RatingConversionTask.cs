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

        await RunManual(progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the conversion unconditionally, ignoring <see cref="PluginConfiguration.EnableAutoConversion"/>.
    /// Used by the "Run Now" API action, which is deliberately independent of the
    /// "Run after library scan" automation toggle — the two are separate controls.
    /// Fills empty Custom ratings only; an existing value is never disturbed.
    /// </summary>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task RunManual(IProgress<double> progress, CancellationToken cancellationToken)
        => Convert(overwriteExisting: false, progress, cancellationToken);

    /// <summary>
    /// Re-applies the mapping table to every item, <b>overwriting</b> Custom ratings that
    /// are already set — including ones a person chose by hand. This is destructive and is
    /// only ever reached through an explicit, confirmed "Re-map all" action; routine runs
    /// (post-scan and Run Now) never take this path.
    /// </summary>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task RunRemapAll(IProgress<double> progress, CancellationToken cancellationToken)
        => Convert(overwriteExisting: true, progress, cancellationToken);

    private async Task Convert(bool overwriteExisting, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
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

            // Automation fills gaps; it does not revise decisions. An existing CustomRating
            // is someone's choice — made through this plugin's bulk edit or Jellyfin's own
            // metadata editor — and the plugin cannot tell those apart from its own earlier
            // writes, so it leaves all of them alone. Overwriting is reachable only through
            // the explicit, confirmed "Re-map all" action.
            if (!overwriteExisting && !string.IsNullOrWhiteSpace(item.CustomRating))
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

        _logger.LogInformation(
            "Age Rating Converter: converted {Count}/{Total} items ({Mode}).",
            converted,
            total,
            overwriteExisting ? "re-map all, overwriting existing custom ratings" : "fill empty only");

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
