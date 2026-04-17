namespace Jellyfin.Plugin.AgeRating.RatingMappings;

/// <summary>
/// Rating-system descriptor: identifier (used as the key and stored in config),
/// display label, and an example rating the UI can use as placeholder text.
/// </summary>
/// <param name="Id">Short identifier, e.g. "BBFC".</param>
/// <param name="DisplayName">Human-readable label, e.g. "BBFC (UK)".</param>
/// <param name="ExampleRating">A representative rating from the system, e.g. "12A".</param>
public record SystemDescriptor(string Id, string DisplayName, string ExampleRating);
