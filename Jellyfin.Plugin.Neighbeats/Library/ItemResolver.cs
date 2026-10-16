using Jellyfin.Plugin.Neighbeats.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Neighbeats.Library;

/// <summary>
/// Resolves a neighbeats <see cref="TrackOut"/> to the Jellyfin library item it names.
/// </summary>
public static class ItemResolver
{
    /// <summary>
    /// Tries <see cref="TrackOut.ItemId"/> first, falling back to <see cref="TrackOut.Path"/>.
    /// </summary>
    /// <param name="libraryManager">Used to resolve the track.</param>
    /// <param name="track">The track to resolve.</param>
    /// <returns>The resolved item, or <c>null</c> if neither resolved.</returns>
    public static BaseItem? Resolve(ILibraryManager libraryManager, TrackOut track)
    {
        if (!string.IsNullOrEmpty(track.ItemId)
            && Guid.TryParse(track.ItemId, out var parsed)
            && libraryManager.GetItemById(parsed) is { } byId)
        {
            return byId;
        }

        return libraryManager.FindByPath(track.Path, false);
    }
}
