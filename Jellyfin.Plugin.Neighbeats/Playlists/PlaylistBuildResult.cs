using Jellyfin.Plugin.Neighbeats.Api;

namespace Jellyfin.Plugin.Neighbeats.Playlists;

/// <summary>
/// The outcome of <see cref="PlaylistBuilder.BuildAsync"/>.
/// </summary>
/// <param name="PlaylistId">
/// The created or updated playlist's id, or <c>null</c> when none of the given tracks
/// could be resolved to a Jellyfin item and no playlist was touched.
/// </param>
/// <param name="Tracks">The tracks that were resolved and added to the playlist.</param>
/// <param name="Skipped">The tracks that could not be resolved to a Jellyfin item.</param>
public record PlaylistBuildResult(Guid? PlaylistId, IReadOnlyList<TrackOut> Tracks, IReadOnlyList<TrackOut> Skipped);
