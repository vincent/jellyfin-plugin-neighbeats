namespace Jellyfin.Plugin.Neighbeats.Api.Controllers;

/// <summary>
/// Request body for <c>POST Neighbeats/Path</c>.
/// </summary>
/// <param name="FromItemId">The Jellyfin item id of the starting track.</param>
/// <param name="ToItemId">The Jellyfin item id of the destination track.</param>
/// <param name="Length">Exact number of tracks; omitted or 0 means the cheapest route, any length.</param>
/// <param name="Mode">Taste mode: comfort, explore, rediscover or none.</param>
/// <param name="Theme">Steer toward lyrics matching this phrase.</param>
public record PathPlaylistRequest(Guid FromItemId, Guid ToItemId, int? Length, string? Mode, string? Theme);
