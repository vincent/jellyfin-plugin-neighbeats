using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Neighbeats.Api;
using Jellyfin.Plugin.Neighbeats.Library;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Playlists;

namespace Jellyfin.Plugin.Neighbeats.Playlists;

/// <summary>
/// Turns a neighbeats answer into a real Jellyfin playlist: resolves each track to a
/// Jellyfin item (by id, falling back to its path), skips what still fails, and creates
/// or updates the caller's playlist with what resolved. neighbeats never writes to
/// Jellyfin; this is the one place that does.
/// </summary>
/// <param name="libraryManager">Used to resolve tracks to Jellyfin items.</param>
/// <param name="playlistManager">Used to create or update the playlist.</param>
public class PlaylistBuilder(ILibraryManager libraryManager, IPlaylistManager playlistManager)
{
    /// <summary>
    /// Resolves <paramref name="tracks"/> and creates or updates the named playlist
    /// owned by <paramref name="userId"/> with what resolved. A playlist of the same
    /// name already owned by that user is updated in place rather than duplicated.
    /// </summary>
    /// <param name="name">The playlist's name.</param>
    /// <param name="userId">The owning Jellyfin user.</param>
    /// <param name="tracks">The tracks to resolve and add, in order.</param>
    /// <returns>The build's outcome.</returns>
    public async Task<PlaylistBuildResult> BuildAsync(string name, Guid userId, IReadOnlyList<TrackOut> tracks)
    {
        var resolved = new List<(TrackOut Track, Guid ItemId)>();
        var skipped = new List<TrackOut>();

        foreach (var track in tracks)
        {
            var itemId = ResolveItemId(track);
            if (itemId is { } id)
            {
                resolved.Add((track, id));
            }
            else
            {
                skipped.Add(track);
            }
        }

        if (resolved.Count == 0)
        {
            return new PlaylistBuildResult(null, [], skipped);
        }

        var itemIds = resolved.Select(r => r.ItemId).ToArray();
        var existing = playlistManager.GetPlaylists(userId)
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));

        Guid playlistId;
        if (existing is not null)
        {
            await playlistManager.UpdatePlaylist(new PlaylistUpdateRequest
            {
                Id = existing.Id,
                UserId = userId,
                Ids = itemIds,
            }).ConfigureAwait(false);
            playlistId = existing.Id;
        }
        else
        {
            var result = await playlistManager.CreatePlaylist(new PlaylistCreationRequest
            {
                Name = name,
                ItemIdList = itemIds,
                UserId = userId,
                MediaType = MediaType.Audio,
            }).ConfigureAwait(false);
            playlistId = Guid.Parse(result.Id);
        }

        return new PlaylistBuildResult(playlistId, resolved.Select(r => r.Track).ToList(), skipped);
    }

    private Guid? ResolveItemId(TrackOut track) => ItemResolver.Resolve(libraryManager, track)?.Id;
}
