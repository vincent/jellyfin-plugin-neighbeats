using Jellyfin.Plugin.Neighbeats.Api;
using Jellyfin.Plugin.Neighbeats.Client;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Neighbeats.Mixing;

/// <summary>
/// Shared "ask neighbeats for tracks similar to this item" logic for the Instant Mix
/// override and the "More like this" provider: picks seed tracks, calls neighbeats once
/// per seed, and maps the merged answer back to ordered, deduped library items. Returns an
/// empty list - never throws - when neighbeats cannot answer, so both callers can treat
/// that the same as "neighbeats had nothing to add" and fall back to Jellyfin's own mix.
/// </summary>
/// <param name="neighbeatsClient">Talks to neighbeats' serve API.</param>
/// <param name="libraryManager">Resolves neighbeats' answers to library items and reads a folder's tracks for seeding.</param>
/// <param name="logger">Logs per-seed failures at information level; these are expected whenever neighbeats hasn't indexed a track yet.</param>
public class NeighbeatsSimilarityEngine(
    NeighbeatsClient neighbeatsClient,
    ILibraryManager libraryManager,
    ILogger<NeighbeatsSimilarityEngine> logger)
{
    /// <summary>
    /// Maximum number of seed tracks sampled from a folder (album, artist, or playlist).
    /// </summary>
    public const int MaxSeeds = 20;

    /// <summary>
    /// Finds tracks similar to <paramref name="item"/>. A song seeds from itself; an album,
    /// artist, or playlist seeds from up to <see cref="MaxSeeds"/> of its own tracks, evenly
    /// sampled. Any other item type yields no seeds and returns an empty list.
    /// </summary>
    /// <param name="item">The item Instant Mix or "More like this" was requested for.</param>
    /// <param name="excludeItemIds">Ids to leave out of the result (e.g. already-chosen items); the seeds themselves are always excluded too.</param>
    /// <param name="limit">The maximum number of items to return.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>Similar items, ranked by neighbeats, or empty if neighbeats had nothing usable.</returns>
    public async Task<IReadOnlyList<BaseItem>> GetSimilarAsync(
        BaseItem item,
        IReadOnlyCollection<Guid> excludeItemIds,
        int limit,
        CancellationToken cancellationToken)
    {
        var seeds = GetSeeds(item);
        if (seeds.Count == 0)
        {
            return [];
        }

        var exclude = new HashSet<Guid>(excludeItemIds);
        foreach (var seed in seeds)
        {
            exclude.Add(seed.Id);
        }

        var merged = new List<TrackOut>();
        foreach (var seed in seeds)
        {
            SimilarResponse answer;
            try
            {
                var request = new SimilarRequest(new TrackRef(seed.Id.ToString(), seed.Path), limit, null, null);
                answer = await neighbeatsClient.SimilarAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (NeighbeatsException ex)
            {
                logger.LogInformation("neighbeats could not answer for seed {SeedId}: {Message}", seed.Id, ex.Message);
                if (ex.Kind == NeighbeatsErrorKind.NotRunning)
                {
                    // neighbeats is down; further seeds will fail the same way.
                    break;
                }

                continue;
            }

            // Tracks[0] is the seed itself; the rest are its neighbours.
            merged.AddRange(answer.Tracks.Skip(1));
        }

        return NeighbeatsAnswerMapper.ToOrderedItems(libraryManager, merged, exclude, limit);
    }

    private static IReadOnlyList<Audio> GetSeeds(BaseItem item)
    {
        if (item is Audio audio)
        {
            return [audio];
        }

        if (item is not Folder folder)
        {
            return [];
        }

        var tracks = folder.GetRecursiveChildren(static i => i is Audio).Cast<Audio>().ToList();
        if (tracks.Count <= MaxSeeds)
        {
            return tracks;
        }

        // Evenly spaced sample rather than random, so repeated calls for the same item
        // return a stable set of seeds.
        var step = (double)tracks.Count / MaxSeeds;
        return Enumerable.Range(0, MaxSeeds).Select(i => tracks[(int)(i * step)]).ToList();
    }
}
