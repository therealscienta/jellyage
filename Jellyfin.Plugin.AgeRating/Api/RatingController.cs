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
    /// Returns the ordered list of rating strings for a given system.
    /// </summary>
    /// <param name="system">System identifier, e.g. "Sweden".</param>
    /// <returns>Rating strings in system order (primary ratings only).</returns>
    [HttpGet("SystemRatings")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<string>> GetSystemRatings([FromQuery] string? system)
    {
        if (string.IsNullOrWhiteSpace(system))
        {
            return BadRequest("Missing required query parameter: system.");
        }

        if (!SystemRatings.All.TryGetValue(system, out var ratings))
        {
            return BadRequest($"Unknown system: '{system}'.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var r in ratings)
        {
            if (seen.Add(r.Rating))
            {
                result.Add(r.Rating);
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Returns per-library persistence status so the UI can tell the admin
    /// whether Custom rating changes will be written back to NFO files on disk
    /// or kept only in Jellyfin's database.
    /// </summary>
    /// <returns>One entry per virtual folder (library).</returns>
    [HttpGet("LibraryPersistence")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<LibraryPersistenceDto>> GetLibraryPersistence()
    {
        var result = new List<LibraryPersistenceDto>();
        foreach (var vf in GetRelevantVirtualFolders())
        {
            var options = vf.LibraryOptions;
            var savers = options?.MetadataSavers ?? Array.Empty<string>();
            var nfoEnabled = Array.Exists(savers, s => string.Equals(s, "Nfo", StringComparison.OrdinalIgnoreCase));
            var saveLocal = options?.SaveLocalMetadata ?? false;

            result.Add(new LibraryPersistenceDto
            {
                Name = vf.Name ?? string.Empty,
                ItemId = vf.ItemId ?? string.Empty,
                NfoSaverEnabled = nfoEnabled,
                SaveLocalMetadata = saveLocal,
                // The Nfo saver's gate is "SaveLocalMetadata is on OR an NFO file already exists".
                // We can't check every item's filesystem here, so we report the coarse answer:
                // the library *will* persist changes if both the saver is on AND local metadata
                // is enabled. If only the saver is on, existing NFOs will still be updated — we
                // express that nuance in the UI copy, not in this boolean.
                PersistsToDisk = nfoEnabled && saveLocal,
            });
        }

        return Ok(result
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    /// <summary>
    /// Returns the Movie/TV/Mixed libraries the item list can be filtered to.
    /// Libraries whose virtual folder has no resolved item id are omitted — they
    /// cannot be used as a filter value.
    /// </summary>
    /// <returns>One entry per selectable library, ordered by name.</returns>
    [HttpGet("Libraries")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<LibraryChoiceDto>> GetLibraries()
    {
        var result = new List<LibraryChoiceDto>();
        foreach (var vf in GetRelevantVirtualFolders())
        {
            // An empty id would collide with the "All libraries" sentinel and produce
            // an option that silently means "no filter".
            if (string.IsNullOrWhiteSpace(vf.ItemId))
            {
                continue;
            }

            result.Add(new LibraryChoiceDto
            {
                ItemId = vf.ItemId,
                Name = vf.Name ?? string.Empty,
            });
        }

        return Ok(result
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    /// <summary>
    /// Returns server-wide unrated and pending-conversion counts, deliberately ignoring
    /// every list filter. The main page's Automation card shows these next to "Run Now",
    /// which always converts across all libraries — so scoping them to the current
    /// library or type would misreport what that button is about to do.
    /// </summary>
    /// <returns>Whole-server counts.</returns>
    [HttpGet("Counts")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<GlobalCountsDto> GetCounts()
    {
        var unratedSet = GetUnratedSet();
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var lookup = RatingConversionTask.BuildLookup(config);

        var unrated = 0;
        var pending = 0;
        var remap = 0;
        foreach (var item in GetMovieAndSeriesItems())
        {
            if (IsUnrated(item.CustomRating, item.OfficialRating, unratedSet))
            {
                unrated++;
            }

            if (ComputeProposedRating(item, lookup, overwriteExisting: false) is not null)
            {
                pending++;
            }

            if (ComputeProposedRating(item, lookup, overwriteExisting: true) is not null)
            {
                remap++;
            }
        }

        return Ok(new GlobalCountsDto
        {
            UnratedCount = unrated,
            PendingCount = pending,
            RemapCount = remap,
        });
    }

    /// <summary>
    /// Returns the count of library items grouped by their effective rating
    /// (CustomRating preferred over OfficialRating). Items with no effective rating
    /// or an unrated-value are excluded — they are already surfaced by the Unrated filter.
    /// </summary>
    /// <param name="libraryId">Virtual-folder (library) id to restrict counting to. Empty = all libraries.</param>
    /// <returns>Rating/Count pairs sorted alphabetically by rating.</returns>
    [HttpGet("RatingSummary")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<RatingSummaryEntryDto>> GetRatingSummary([FromQuery] string? libraryId = null)
    {
        if (!TryParseLibraryId(libraryId, out var ancestorIds))
        {
            return BadRequest($"Invalid libraryId: '{libraryId}'.");
        }

        var unratedSet = GetUnratedSet();
        var items = GetMovieAndSeriesItems(ancestorIds);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var custom = item.CustomRating;
            var source = item.OfficialRating;
            var eff = !string.IsNullOrWhiteSpace(custom) ? custom : source;
            if (string.IsNullOrWhiteSpace(eff) || unratedSet.Contains(eff.Trim()))
            {
                continue;
            }

            var key = eff.Trim();
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return Ok(counts
            .Select(kv => new RatingSummaryEntryDto { Rating = kv.Key, Count = kv.Value })
            .OrderBy(x => x.Rating, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    /// <summary>
    /// Unified paginated list of Movies and Series, optionally filtered by rating state,
    /// type, library, or name substring. Also returns Unrated/Pending counts for the
    /// chip badges — scoped by type and library, but not by the chip/search/rating
    /// filters, so each badge predicts what clicking that chip returns.
    /// </summary>
    /// <param name="filter">One of "all", "unrated", "pending". Defaults to "all".</param>
    /// <param name="type">One of "all", "Movie", "Series". Defaults to "all".</param>
    /// <param name="search">Case-insensitive name substring.</param>
    /// <param name="rating">Exact effective rating to filter by (case-insensitive). Empty = no filter.</param>
    /// <param name="libraryId">Virtual-folder (library) id to restrict results to. Empty = all libraries.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page (clamped to [1, 500]).</param>
    /// <returns>A paginated item list with aggregate counts.</returns>
    [HttpGet("Items")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ItemListDto> GetItems(
        [FromQuery] string? filter = "all",
        [FromQuery] string? type = "all",
        [FromQuery] string? search = null,
        [FromQuery] string? rating = null,
        [FromQuery] string? libraryId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        var clampedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var clampedPage = Math.Max(1, page);

        if (!TryParseLibraryId(libraryId, out var ancestorIds))
        {
            return BadRequest($"Invalid libraryId: '{libraryId}'.");
        }

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
            AncestorIds = ancestorIds,
        });

        var unratedCount = 0;
        var pendingCount = 0;
        var rows = new List<ItemRowDto>(all.Count);
        foreach (var item in all)
        {
            var source = item.OfficialRating;
            var custom = item.CustomRating;
            var proposed = ComputeProposedRating(item, lookup, overwriteExisting: false);

            if (IsUnrated(custom, source, unratedSet))
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

        if (!string.IsNullOrWhiteSpace(rating))
        {
            var ratingTrimmed = rating.Trim();
            filtered = filtered.Where(r =>
            {
                var eff = !string.IsNullOrWhiteSpace(r.CustomRating) ? r.CustomRating : r.CurrentRating;
                return string.Equals(eff?.Trim(), ratingTrimmed, StringComparison.OrdinalIgnoreCase);
            });
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
                var target = ComputeProposedRating(i, lookup, overwriteExisting: false);
                if (target is null)
                {
                    return null;
                }

                return new RatingPreviewDto
                {
                    ItemId = i.Id,
                    Name = i.Name,
                    Type = i is MediaBrowser.Controller.Entities.TV.Series ? "Series" : "Movie",
                    CurrentRating = i.OfficialRating,
                    ProposedRating = target,
                };
            })
            .Where(p => p is not null)
            .Select(p => p!);
        return Ok(previews);
    }

    /// <summary>
    /// Triggers rating conversion immediately without waiting for a library scan.
    /// Fills empty Custom ratings only — an existing value is never disturbed, so this
    /// is safe to press at any time. Use <see cref="RemapAll"/> to revise existing values.
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

        await task.RunManual(new Progress<double>(), cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Re-applies the mapping table to every item, <b>overwriting</b> Custom ratings that are
    /// already set — including hand-curated ones. Destructive, and the only route to that
    /// behaviour: routine conversion never overwrites. Intended for after a target-system or
    /// mapping-table change, behind a confirmation that quotes
    /// <see cref="GlobalCountsDto.RemapCount"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 when the re-map completes.</returns>
    [HttpPost("RemapAll")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RemapAll(CancellationToken cancellationToken)
    {
        var task = HttpContext.RequestServices.GetService(typeof(RatingConversionTask)) as RatingConversionTask;
        if (task is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        await task.RunRemapAll(new Progress<double>(), cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    private IReadOnlyList<BaseItem> GetMovieAndSeriesItems(Guid[]? ancestorIds = null)
        => _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            IsVirtualItem = false,
            AncestorIds = ancestorIds ?? [],
        });

    /// <summary>
    /// Enumerates the virtual folders (libraries) this plugin cares about: Movie, TV and
    /// Mixed libraries. A null CollectionType represents a "Mixed" library (movies + shows)
    /// so it stays in. Music/Books/BoxSets etc. still expose MetadataSavers, but the
    /// conversion task never touches them and their presence would just clutter the UI.
    /// </summary>
    /// <returns>The relevant virtual folders, in Jellyfin's own order.</returns>
    private List<MediaBrowser.Model.Entities.VirtualFolderInfo> GetRelevantVirtualFolders()
    {
        var result = new List<MediaBrowser.Model.Entities.VirtualFolderInfo>();
        foreach (var vf in _libraryManager.GetVirtualFolders())
        {
            var ct = vf.CollectionType;
            if (ct is not null
                && ct != MediaBrowser.Model.Entities.CollectionTypeOptions.movies
                && ct != MediaBrowser.Model.Entities.CollectionTypeOptions.tvshows)
            {
                continue;
            }

            result.Add(vf);
        }

        return result;
    }

    /// <summary>
    /// Parses the optional libraryId query parameter into the AncestorIds filter for
    /// <see cref="InternalItemsQuery"/>. A library's CollectionFolder is an ancestor of
    /// every Movie and Series inside it, so this scopes a query to one library.
    /// Empty/whitespace and Guid.Empty both mean "no library filter" and yield an empty
    /// array, which the repository treats as a no-op.
    /// </summary>
    /// <param name="libraryId">Raw query value; may be null.</param>
    /// <param name="ancestorIds">The resulting ancestor filter.</param>
    /// <returns>False when a value was supplied but is not a valid Guid.</returns>
    private static bool TryParseLibraryId(string? libraryId, out Guid[] ancestorIds)
    {
        ancestorIds = [];
        if (string.IsNullOrWhiteSpace(libraryId))
        {
            return true;
        }

        if (!Guid.TryParse(libraryId, out var parsed))
        {
            return false;
        }

        // An all-zero id would otherwise silently return an empty table, which is a
        // worse failure than simply ignoring it.
        if (!parsed.Equals(Guid.Empty))
        {
            ancestorIds = [parsed];
        }

        return true;
    }

    /// <summary>
    /// Decides what a conversion run would write to this item's CustomRating, or null when
    /// it would leave the item alone. Mirrors the skip logic in RatingConversionTask so the
    /// Pending badge and Preview can't drift from what a run actually does.
    /// </summary>
    /// <param name="item">The library item.</param>
    /// <param name="lookup">Source-to-target rating lookup.</param>
    /// <param name="overwriteExisting">
    /// False for the routine path (post-scan and Run Now), which only fills an empty
    /// CustomRating. True only for the explicit "Re-map all" action, which revises
    /// existing values — including hand-set ones.
    /// </param>
    /// <returns>The target rating, or null if nothing would change.</returns>
    private static string? ComputeProposedRating(BaseItem item, Dictionary<string, string> lookup, bool overwriteExisting)
    {
        var source = item.OfficialRating;
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (!lookup.TryGetValue(source.Trim(), out var target))
        {
            return null;
        }

        // An existing CustomRating is someone's decision. The routine path never revises it.
        if (!overwriteExisting && !string.IsNullOrWhiteSpace(item.CustomRating))
        {
            return null;
        }

        // Idempotency: nothing to do if CustomRating already matches.
        if (string.Equals(item.CustomRating, target, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return target;
    }

    /// <summary>
    /// Determines whether an item has no *effective* rating. Jellyfin prefers CustomRating
    /// over OfficialRating at parental-check time, so this mirrors that precedence.
    /// </summary>
    /// <param name="custom">The item's CustomRating.</param>
    /// <param name="official">The item's OfficialRating.</param>
    /// <param name="unratedSet">Values configured to count as "no rating".</param>
    /// <returns>True when the item is effectively unrated.</returns>
    private static bool IsUnrated(string? custom, string? official, HashSet<string> unratedSet)
    {
        var effective = !string.IsNullOrWhiteSpace(custom) ? custom : official;
        return string.IsNullOrWhiteSpace(effective) || unratedSet.Contains(effective.Trim());
    }

    private static HashSet<string> GetUnratedSet()
    {
        var raw = Plugin.Instance?.Configuration?.UnratedValues
                  ?? "NR,Not Rated,Unrated,Unknown,UR,0";
        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
