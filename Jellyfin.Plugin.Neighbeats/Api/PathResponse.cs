namespace Jellyfin.Plugin.Neighbeats.Api;

/// <summary>
/// Answer to <c>POST /v1/path</c>, for both the 200 and 422 cases.
/// </summary>
/// <param name="Found">Whether a route was found.</param>
/// <param name="Tracks">The route's tracks, or as far as the search got when <paramref name="Found"/> is <c>false</c>.</param>
/// <param name="Cost">The route's cost, when found.</param>
/// <param name="Notes">Notes about taste or filtering applied to the route.</param>
/// <param name="Error">An error message, present only when <paramref name="Found"/> is <c>false</c>.</param>
/// <param name="Why">Where the search broke, present only when <paramref name="Found"/> is <c>false</c>.</param>
public record PathResponse(
    bool Found,
    IReadOnlyList<TrackOut> Tracks,
    double? Cost,
    IReadOnlyList<string>? Notes,
    string? Error,
    string? Why);
