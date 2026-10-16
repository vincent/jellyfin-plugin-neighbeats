using Jellyfin.Plugin.Neighbeats.Api;
using Jellyfin.Plugin.Neighbeats.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Neighbeats.Mixing;

/// <summary>
/// Maps a neighbeats similarity answer to an ordered, deduped list of Jellyfin library
/// items, for the Instant Mix override and the "More like this" provider.
/// </summary>
public static class NeighbeatsAnswerMapper
{
    /// <summary>
    /// Resolves <paramref name="tracks"/> in order, skipping tracks that don't resolve to a
    /// library item, duplicates, and ids in <paramref name="excludeItemIds"/>, stopping once
    /// <paramref name="limit"/> items have been collected.
    /// </summary>
    /// <param name="libraryManager">Used to resolve each track to a library item.</param>
    /// <param name="tracks">The merged, seed-first answer from one or more neighbeats calls.</param>
    /// <param name="excludeItemIds">Ids to leave out even if neighbeats returns them (typically the seeds themselves).</param>
    /// <param name="limit">The maximum number of items to return.</param>
    /// <returns>The resolved items, in the order neighbeats ranked them.</returns>
    public static IReadOnlyList<BaseItem> ToOrderedItems(
        ILibraryManager libraryManager,
        IEnumerable<TrackOut> tracks,
        IReadOnlyCollection<Guid> excludeItemIds,
        int limit)
    {
        var seen = new HashSet<Guid>(excludeItemIds);
        var result = new List<BaseItem>();

        foreach (var track in tracks)
        {
            if (result.Count >= limit)
            {
                break;
            }

            if (ItemResolver.Resolve(libraryManager, track) is { } item && seen.Add(item.Id))
            {
                result.Add(item);
            }
        }

        return result;
    }
}
