using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AgeRating.Configuration;
using Jellyfin.Plugin.AgeRating.RatingMappings;
using Jellyfin.Plugin.AgeRating.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AgeRating.Api;

/// <summary>
/// REST API controller for the Age Rating Converter plugin.
/// </summary>
[ApiController]
[Route("AgeRating")]
[Produces(MediaTypeNames.Application.Json)]
public class RatingController : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 500;

    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public RatingController(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Returns the list of rating systems the plugin supports as targets for
    /// "Load Built-in Defaults". Id is the key stored in configuration;
    /// DisplayName is for UI dropdowns.
    /// </summary>
    /// <returns>Ordered list of supported systems.</returns>
    [HttpGet("SupportedSystems")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SystemDescriptor>> GetSupportedSystems()
        => Ok(SystemRatings.SupportedSystems);

    /// <summary>
    /// Generates the built-in default mapping list for the given target system.
    /// Each returned entry maps one supported source-system rating to the
    /// target system's primary rating for the same age bucket. The frontend
    /// is expected to use this to populate the mapping table on the user's
    /// confirmed "Load Built-in Defaults" action; it is not auto-applied.
    /// </summary>
    /// <param name="target">Identifier of the target system, e.g. "BBFC".</param>
    /// <returns>Source-to-target rating pairs, or 400 if the target is missing/unknown.</returns>
    [HttpGet("DefaultMappings")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<RatingMapping>> GetDefaultMappings([FromQuery] string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return BadRequest("Missing required query parameter: target.");
        }

        if (!SystemRatings.All.ContainsKey(target))
        {
            return BadRequest($"Unknown target system: '{target}'.");
        }

        return Ok(DefaultMappings.Generate(target));
    }

    /// <summary>
    /// Unified paginated list of Movies and Series, optionally filtered by rating state,
    /// type, or name substring. Also returns global Unrated/Pending counts for badge display.
    /// </summary>
    /// <param name="filter">One of "all", "unrated", "pending". Defaults to "all".</param>
    /// <param name="type">One of "all", "Movie", "Series". Defaults to "all".</param>
    /// <param name="search">Case-insensitive name substring.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page (clamped to [1, 500]).</param>
    /// <returns>A paginated item list with aggregate counts.</returns>
    [HttpGet("Items")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ItemListDto> GetItems(
        [FromQuery] string? filter = "all",
        [FromQuery] string? type = "all",
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        var clampedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var clampedPage = Math.Max(1, page);

        var unratedSet = GetUnratedSet();
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var lookup = RatingConversionTask.BuildLookup(config);

        var kinds = type?.ToLowerInvariant() switch
        {
            "movie" => new[] { BaseItemKind.Movie },
            "series" => new[] { BaseItemKind.Series },
            _ => new[] { BaseItemKind.Movie, BaseItemKind.Series },
        };

        var all = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = kinds,
            IsVirtualItem = false,
        });

        var unratedCount = 0;
        var pendingCount = 0;
        var rows = new List<ItemRowDto>(all.Count);
        foreach (var item in all)
        {
            var source = item.OfficialRating;
            var custom = item.CustomRating;

            // "Unrated" in the UI means the item has no *effective* rating —
            // Jellyfin prefers CustomRating over OfficialRating at parental-
            // check time, so we mirror that precedence here.
            var effective = !string.IsNullOrWhiteSpace(custom) ? custom : source;
            var isUnrated = string.IsNullOrWhiteSpace(effective)
                            || unratedSet.Contains(effective!.Trim());

            string? proposed = null;
            if (!string.IsNullOrWhiteSpace(source)
                && lookup.TryGetValue(source.Trim(), out var target))
            {
                // Match RatingConversionTask.Run's skip logic so the Preview / Pending
                // badge reflects what the next run would actually change.
                var wouldSkipDueToOverwrite = !config.OverwriteExistingRatings
                                              && !string.IsNullOrWhiteSpace(custom);
                var alreadyMatches = string.Equals(custom, target, StringComparison.OrdinalIgnoreCase);
                if (!wouldSkipDueToOverwrite && !alreadyMatches)
                {
                    proposed = target;
                }
            }

            if (isUnrated)
            {
                unratedCount++;
            }

            if (proposed is not null)
            {
                pendingCount++;
            }

            rows.Add(new ItemRowDto
            {
                ItemId = item.Id,
                Name = item.Name,
                Type = item is MediaBrowser.Controller.Entities.TV.Series ? "Series" : "Movie",
                CurrentRating = source,
                CustomRating = custom,
                ProposedRating = proposed,
            });
        }

        IEnumerable<ItemRowDto> filtered = rows;
        switch ((filter ?? "all").ToLowerInvariant())
        {
            case "unrated":
                filtered = rows.Where(r =>
                {
                    var eff = !string.IsNullOrWhiteSpace(r.CustomRating) ? r.CustomRating : r.CurrentRating;
                    return string.IsNullOrWhiteSpace(eff) || unratedSet.Contains(eff!.Trim());
                });
                break;
            case "pending":
                filtered = rows.Where(r => r.ProposedRating is not null);
                break;
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            filtered = filtered.Where(r => r.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = ordered.Count;
        var paged = ordered
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToList();

        return Ok(new ItemListDto
        {
            Items = paged,
            TotalCount = total,
            Page = clampedPage,
            PageSize = clampedPageSize,
            UnratedCount = unratedCount,
            PendingCount = pendingCount,
        });
    }

    /// <summary>
    /// Sets the same rating on many items at once. Empty rating clears the value.
    /// </summary>
    /// <param name="request">Item IDs and target rating.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of items successfully updated.</returns>
    [HttpPost("BulkSetRating")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkSetRatingResponseDto>> BulkSetRating(
        [FromBody] BulkSetRatingRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ItemIds.Count == 0)
        {
            return Ok(new BulkSetRatingResponseDto { UpdatedCount = 0 });
        }

        var newRating = string.IsNullOrWhiteSpace(request.Rating) ? null : request.Rating.Trim();
        var updated = 0;
        foreach (var id in request.ItemIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = _libraryManager.GetItemById(id);
            if (item is null)
            {
                continue;
            }

            // Bulk-edit always writes the CustomRating lane (never OfficialRating).
            // Empty string clears the override and lets Jellyfin fall back to OfficialRating.
            item.CustomRating = newRating;
            await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
            updated++;
        }

        return Ok(new BulkSetRatingResponseDto { UpdatedCount = updated });
    }

    /// <summary>
    /// Previews what rating changes would be applied without writing anything.
    /// Kept for scripts and the legacy config page; not used by the new main page.
    /// </summary>
    /// <returns>List of pending changes.</returns>
    [HttpGet("Preview")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<RatingPreviewDto>> GetPreview()
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var lookup = RatingConversionTask.BuildLookup(config);

        var previews = GetMovieAndSeriesItems()
            .Select(i =>
            {
                var source = i.OfficialRating;
                var custom = i.CustomRating;
                if (string.IsNullOrWhiteSpace(source))
                {
                    return null;
                }

                if (!lookup.TryGetValue(source.Trim(), out var target))
                {
                    return null;
                }

                if (!config.OverwriteExistingRatings && !string.IsNullOrWhiteSpace(custom))
                {
                    return null;
                }

                if (string.Equals(custom, target, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return new RatingPreviewDto
                {
                    ItemId = i.Id,
                    Name = i.Name,
                    Type = i is MediaBrowser.Controller.Entities.TV.Series ? "Series" : "Movie",
                    CurrentRating = source,
                    ProposedRating = target,
                };
            })
            .Where(p => p is not null)
            .Select(p => p!);
        return Ok(previews);
    }

    /// <summary>
    /// Triggers rating conversion immediately without waiting for a library scan.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 when conversion completes.</returns>
    [HttpPost("ApplyNow")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ApplyNow(CancellationToken cancellationToken)
    {
        var task = HttpContext.RequestServices.GetService(typeof(RatingConversionTask)) as RatingConversionTask;
        if (task is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        await task.Run(new Progress<double>(), cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    private IReadOnlyList<BaseItem> GetMovieAndSeriesItems()
        => _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            IsVirtualItem = false,
        });

    private static HashSet<string> GetUnratedSet()
    {
        var raw = Plugin.Instance?.Configuration?.UnratedValues
                  ?? "NR,Not Rated,Unrated,Unknown,UR,0";
        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
