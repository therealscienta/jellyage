using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AgeRating.Configuration;

namespace Jellyfin.Plugin.AgeRating.RatingMappings;

/// <summary>
/// Generator for the built-in default mapping set. Given a target rating
/// system, emits a <see cref="RatingMapping"/> for each supported source
/// system's ratings, pointing at the target system's primary rating for
/// the same <see cref="AgeBucket"/>.
/// </summary>
/// <remarks>
/// "Primary" means the first rating listed in <see cref="SystemRatings.All"/>
/// for a given (system, bucket) pair. For BBFC's Family bucket that's "12A"
/// rather than "12", for example. Source ratings whose bucket has no
/// equivalent in the target system are skipped (rather than invented).
/// Duplicate (Source, Target) pairs are removed — the first occurrence wins.
/// </remarks>
public static class DefaultMappings
{
    /// <summary>
    /// Generate the mapping list for the given target system. Returns an
    /// empty list if the target system is unknown or not set.
    /// </summary>
    /// <param name="targetSystem">Identifier of the target system (e.g. "BBFC").</param>
    /// <returns>Source-to-target rating pairs suitable for the mapping table.</returns>
    public static IReadOnlyList<RatingMapping> Generate(string? targetSystem)
    {
        if (string.IsNullOrWhiteSpace(targetSystem)
            || !SystemRatings.All.TryGetValue(targetSystem, out var targetRatings))
        {
            return [];
        }

        // Build a bucket→primary lookup for the target system: first rating listed
        // per bucket wins. Callers pick a rating system thinking of their household's
        // preferred display form, so this is the value we'll emit into Target cells.
        var targetByBucket = new Dictionary<AgeBucket, string>();
        foreach (var t in targetRatings)
        {
            if (!targetByBucket.ContainsKey(t.Bucket))
            {
                targetByBucket[t.Bucket] = t.Rating;
            }
        }

        var seen = new HashSet<(string Source, string Target)>();
        var result = new List<RatingMapping>();

        foreach (var (systemId, ratings) in SystemRatings.All)
        {
            if (string.Equals(systemId, targetSystem, StringComparison.OrdinalIgnoreCase))
            {
                // Self-mapping rows (e.g. MPAA→MPAA when the admin picks MPAA) add no value
                // since the plugin's skip rule already treats source==target as a no-op.
                continue;
            }

            foreach (var sr in ratings)
            {
                if (!targetByBucket.TryGetValue(sr.Bucket, out var targetRating))
                {
                    continue;
                }

                var pair = (sr.Rating, targetRating);
                if (seen.Add(pair))
                {
                    result.Add(new RatingMapping { Source = sr.Rating, Target = targetRating });
                }
            }
        }

        // Sort by target rating's age position within the target system (softer
        // first), then by source alphabetically. Grouping rows by target makes
        // the mapping table easier to scan — admins can see at a glance every
        // source that lands on e.g. "12A".
        var targetRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < targetRatings.Count; i++)
        {
            if (!targetRank.ContainsKey(targetRatings[i].Rating))
            {
                targetRank[targetRatings[i].Rating] = i;
            }
        }

        result.Sort((a, b) =>
        {
            var ra = targetRank.TryGetValue(a.Target, out var va) ? va : int.MaxValue;
            var rb = targetRank.TryGetValue(b.Target, out var vb) ? vb : int.MaxValue;
            if (ra != rb)
            {
                return ra.CompareTo(rb);
            }

            return string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }
}
