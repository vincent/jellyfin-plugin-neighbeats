namespace Jellyfin.Plugin.Neighbeats.Api;

/// <summary>
/// Request body for <c>POST /v1/path</c>.
/// </summary>
/// <param name="From">The starting track.</param>
/// <param name="To">The destination track.</param>
/// <param name="Length">Exact number of tracks (1-200); 0 or <c>null</c> means the cheapest route, any length.</param>
/// <param name="Mode">Taste mode: comfort, explore, rediscover or none.</param>
/// <param name="User">Jellyfin user name whose synced history biases the route.</param>
/// <param name="Theme">Steer toward lyrics matching this phrase.</param>
/// <param name="AvoidArtists">Artists to never pass through.</param>
/// <param name="MaxMinutes">Leave out tracks longer than this; 0 means no limit.</param>
public record PathRequest(
    TrackRef From,
    TrackRef To,
    int? Length,
    string? Mode,
    string? User,
    string? Theme,
    IReadOnlyList<string>? AvoidArtists,
    int? MaxMinutes);
