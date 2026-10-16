namespace Jellyfin.Plugin.Neighbeats.Api.Controllers;

/// <summary>
/// Answer to <c>POST Neighbeats/Path</c> and <c>POST Neighbeats/Similar</c>.
/// </summary>
/// <param name="PlaylistId">The created or updated playlist's id.</param>
/// <param name="Tracks">How many tracks ended up in the playlist.</param>
/// <param name="Skipped">The tracks neighbeats returned that could not be resolved in this library.</param>
/// <param name="Notes">neighbeats' own notes about taste or filtering applied.</param>
public record PlaylistBuildResponse(Guid PlaylistId, int Tracks, IReadOnlyList<TrackOut> Skipped, IReadOnlyList<string>? Notes);
