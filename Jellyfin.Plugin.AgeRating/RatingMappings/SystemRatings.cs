using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.AgeRating.RatingMappings;

/// <summary>
/// Static catalogue of the 12 supported rating systems and the age bucket
/// each of their ratings maps to. This is the single source of truth for
/// the "Load Built-in Defaults" action: picking a target system produces
/// one RatingMapping per source rating across all other systems by going
/// (source rating → bucket → target system's primary rating for that bucket).
/// </summary>
/// <remarks>
/// Rating string literals match what typically appears in NFO files:
/// "FSK 12" with space, "MA15+" with no space, "PG-13" with hyphen, etc.
/// Order within a system matters: for any bucket, the <b>first</b> rating
/// listed is treated as the "primary" value for that system in that bucket,
/// so it's chosen when this system is the target.
/// </remarks>
public static class SystemRatings
{
    /// <summary>
    /// Gets the per-system rating lists. Each entry pairs a rating string with
    /// its age bucket. Order within a list is significant — the first entry
    /// per bucket is the system's primary value for that bucket.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<SystemRating>> All =
        new Dictionary<string, IReadOnlyList<SystemRating>>
        {
            ["MPAA"] =
            [
                new("G",      AgeBucket.All),
                new("PG",     AgeBucket.Family),
                new("PG-13",  AgeBucket.Teen),
                new("R",      AgeBucket.Mature),
                new("NC-17",  AgeBucket.Adult),
                new("NR",     AgeBucket.NotRated),
            ],
            ["BBFC"] =
            [
                new("U",    AgeBucket.All),
                new("PG",   AgeBucket.Mild),
                new("12A",  AgeBucket.Family),
                new("12",   AgeBucket.Family),
                new("15",   AgeBucket.Teen),
                new("18",   AgeBucket.Adult),
                new("R18",  AgeBucket.Restricted),
            ],
            ["FSK"] =
            [
                new("FSK 0",  AgeBucket.All),
                new("FSK 6",  AgeBucket.Mild),
                new("FSK 12", AgeBucket.Family),
                new("FSK 16", AgeBucket.Mature),
                new("FSK 18", AgeBucket.Adult),
            ],
            ["CNC"] =
            [
                new("TP", AgeBucket.All),
                new("10", AgeBucket.Mild),
                new("12", AgeBucket.Family),
                new("16", AgeBucket.Mature),
                new("18", AgeBucket.Adult),
            ],
            ["PEGI"] =
            [
                new("PEGI 3",  AgeBucket.All),
                new("PEGI 7",  AgeBucket.Mild),
                new("PEGI 12", AgeBucket.Family),
                new("PEGI 16", AgeBucket.Teen),
                new("PEGI 18", AgeBucket.Adult),
            ],
            ["Kijkwijzer"] =
            [
                new("AL", AgeBucket.All),
                new("6",  AgeBucket.Mild),
                new("9",  AgeBucket.Family),
                new("12", AgeBucket.Family),
                new("14", AgeBucket.Teen),
                new("16", AgeBucket.Mature),
                new("18", AgeBucket.Adult),
            ],
            ["EIRIN"] =
            [
                new("G",     AgeBucket.All),
                new("PG12",  AgeBucket.Family),
                new("R15+",  AgeBucket.Mature),
                new("R18+",  AgeBucket.Adult),
            ],
            ["KMRB"] =
            [
                new("All",        AgeBucket.All),
                new("12",         AgeBucket.Family),
                new("15",         AgeBucket.Teen),
                new("19",         AgeBucket.Adult),
                new("Restricted", AgeBucket.Restricted),
            ],
            ["ACB"] =
            [
                new("G",     AgeBucket.All),
                new("PG",    AgeBucket.Mild),
                new("M",     AgeBucket.Family),
                new("MA15+", AgeBucket.Mature),
                new("R18+",  AgeBucket.Adult),
                new("X18+",  AgeBucket.Restricted),
            ],
            ["HKCAT"] =
            [
                new("I",   AgeBucket.All),
                new("IIA", AgeBucket.Family),
                new("IIB", AgeBucket.Teen),
                new("III", AgeBucket.Adult),
            ],
            ["OFLC-NZ"] =
            [
                new("G",   AgeBucket.All),
                new("PG",  AgeBucket.Mild),
                new("M",   AgeBucket.Family),
                new("R13", AgeBucket.Teen),
                new("R15", AgeBucket.Teen),
                new("R16", AgeBucket.Mature),
                new("R18", AgeBucket.Adult),
            ],
            ["Russia"] =
            [
                new("0+",  AgeBucket.All),
                new("6+",  AgeBucket.Mild),
                new("12+", AgeBucket.Family),
                new("16+", AgeBucket.Mature),
                new("18+", AgeBucket.Adult),
            ],

            // ── Nordic systems ─────────────────────────────────────────────
            // Sweden — Statens medieråd (SMR).
            ["Sweden"] =
            [
                new("Btl", AgeBucket.All),
                new("7",   AgeBucket.Mild),
                new("11",  AgeBucket.Family),
                new("15",  AgeBucket.Teen),
            ],
            // Norway — Medietilsynet.
            ["Norway"] =
            [
                new("A",  AgeBucket.All),
                new("6",  AgeBucket.Mild),
                new("9",  AgeBucket.Family),
                new("12", AgeBucket.Family),
                new("15", AgeBucket.Teen),
                new("18", AgeBucket.Adult),
            ],
            // Denmark — Medierådet for Børn og Unge.
            ["Denmark"] =
            [
                new("A",  AgeBucket.All),
                new("7",  AgeBucket.Mild),
                new("11", AgeBucket.Family),
                new("15", AgeBucket.Teen),
            ],
            // Finland — National Audiovisual Institute (KAVI).
            ["Finland"] =
            [
                new("S",  AgeBucket.All),
                new("7",  AgeBucket.Mild),
                new("12", AgeBucket.Family),
                new("16", AgeBucket.Mature),
                new("18", AgeBucket.Adult),
            ],
            // Iceland — Smáís / KMF (self-regulatory body; ratings mirror Kijkwijzer).
            ["Iceland"] =
            [
                new("L",  AgeBucket.All),
                new("6",  AgeBucket.Mild),
                new("9",  AgeBucket.Family),
                new("12", AgeBucket.Family),
                new("14", AgeBucket.Teen),
                new("16", AgeBucket.Mature),
                new("18", AgeBucket.Adult),
            ],
        };

    /// <summary>
    /// Gets the list of supported rating-system identifiers in display order.
    /// The identifier is the key into <see cref="All"/> and is also what
    /// PluginConfiguration.DefaultTargetSystem stores. ExampleRating is a
    /// representative value from the system, used by the UI for placeholder
    /// text (preferring the Teen-bucket primary rating, falling back to
    /// Family or Mature, then the first rating available).
    /// </summary>
    public static readonly IReadOnlyList<SystemDescriptor> SupportedSystems = BuildSupportedSystems();

    private static List<SystemDescriptor> BuildSupportedSystems()
    {
        var order = new (string Id, string DisplayName)[]
        {
            ("MPAA",       "MPAA (US)"),
            ("BBFC",       "BBFC (UK)"),
            ("FSK",        "FSK (Germany)"),
            ("CNC",        "CNC (France)"),
            ("PEGI",       "PEGI (Europe)"),
            ("Kijkwijzer", "Kijkwijzer (Netherlands)"),
            ("Sweden",     "Sweden (SMR)"),
            ("Norway",     "Norway (Medietilsynet)"),
            ("Denmark",    "Denmark (Medierådet)"),
            ("Finland",    "Finland (KAVI)"),
            ("Iceland",    "Iceland"),
            ("EIRIN",      "EIRIN (Japan)"),
            ("KMRB",       "KMRB (South Korea)"),
            ("ACB",        "ACB (Australia)"),
            ("HKCAT",      "HKCAT (Hong Kong)"),
            ("OFLC-NZ",    "OFLC (New Zealand)"),
            ("Russia",     "Russia"),
        };

        var result = new List<SystemDescriptor>(order.Length);
        foreach (var entry in order)
        {
            var ratings = All[entry.Id];
            var example =
                ratings.FirstOrDefault(r => r.Bucket == AgeBucket.Teen)?.Rating
                ?? ratings.FirstOrDefault(r => r.Bucket == AgeBucket.Family)?.Rating
                ?? ratings.FirstOrDefault(r => r.Bucket == AgeBucket.Mature)?.Rating
                ?? ratings[0].Rating;
            result.Add(new SystemDescriptor(entry.Id, entry.DisplayName, example));
        }

        return result;
    }
}
