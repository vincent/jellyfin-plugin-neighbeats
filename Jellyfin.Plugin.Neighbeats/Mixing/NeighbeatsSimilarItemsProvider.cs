using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Plugin.Neighbeats.Mixing;

/// <summary>
/// Backs Jellyfin's "More like this" rows with neighbeats' similarity graph (see
/// <see cref="NeighbeatsSimilarityEngine"/>). Registered alongside Jellyfin's own
/// similar-items providers, not in place of them: returning no results here just means
/// Jellyfin's other providers fill the row instead, which is what happens when the override
/// is disabled or neighbeats cannot answer.
/// </summary>
/// <param name="similarityEngine">Finds items similar to the requested one via neighbeats.</param>
public class NeighbeatsSimilarItemsProvider(NeighbeatsSimilarityEngine similarityEngine) : ILocalSimilarItemsProvider
{
    /// <inheritdoc />
    public string Name => "neighbeats";

    /// <inheritdoc />
    public MetadataPluginType Type => MetadataPluginType.LocalSimilarityProvider;

    /// <inheritdoc />
    public bool Supports(Type itemType) =>
        itemType == typeof(Audio)
        || itemType == typeof(MusicAlbum)
        || itemType == typeof(MusicArtist)
        || itemType == typeof(Playlist);

    /// <inheritdoc />
    public Task<IReadOnlyList<BaseItem>> GetSimilarItemsAsync(
        BaseItem item,
        SimilarItemsQuery query,
        CancellationToken cancellationToken)
    {
        if (!Plugin.Instance!.Configuration.EnableSimilarItemsOverride)
        {
            return Task.FromResult<IReadOnlyList<BaseItem>>([]);
        }

        var exclude = new HashSet<Guid>(query.ExcludeItemIds);
        var limit = query.Limit ?? 20;
        return similarityEngine.GetSimilarAsync(item, exclude, limit, cancellationToken);
    }
}
