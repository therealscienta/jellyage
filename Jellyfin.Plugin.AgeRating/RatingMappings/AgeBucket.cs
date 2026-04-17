namespace Jellyfin.Plugin.AgeRating.RatingMappings;

/// <summary>
/// Coarse age-tier bucket used to pivot between different rating systems.
/// Every supported rating value in <see cref="SystemRatings"/> maps to exactly
/// one of these buckets; the bucket is the middleman that lets a rating in
/// system X find its equivalent in system Y.
/// </summary>
public enum AgeBucket
{
    /// <summary>Suitable for all ages (≈ 0+).</summary>
    All,

    /// <summary>Mildly advisable for the very young (≈ 6–8+).</summary>
    Mild,

    /// <summary>Family/parental-guidance tier (≈ 10–12+).</summary>
    Family,

    /// <summary>Teen tier (≈ 13–15+).</summary>
    Teen,

    /// <summary>Mature tier (≈ 16–17+).</summary>
    Mature,

    /// <summary>Adults only (≈ 18+).</summary>
    Adult,

    /// <summary>Restricted/X-rated material.</summary>
    Restricted,

    /// <summary>Explicitly unrated / not rated.</summary>
    NotRated,
}
