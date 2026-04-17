namespace Jellyfin.Plugin.AgeRating.RatingMappings;

/// <summary>
/// A single rating within a system and the age bucket it represents.
/// </summary>
/// <param name="Rating">The rating string as it appears in NFO files, e.g. "PG-13".</param>
/// <param name="Bucket">Coarse age tier used for cross-system conversion.</param>
public record SystemRating(string Rating, AgeBucket Bucket);
