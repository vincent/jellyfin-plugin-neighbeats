namespace Jellyfin.Plugin.Neighbeats.Api;

/// <summary>
/// A track in a neighbeats answer. <see cref="ItemId"/> is <c>null</c> when neighbeats
/// does not know the track's Jellyfin item (added to the library after the last sync).
/// </summary>
/// <param name="ItemId">The Jellyfin item id, or <c>null</c> if unknown to neighbeats.</param>
/// <param name="Path">The file path as the media server sees it.</param>
/// <param name="Artist">The track's artist, if known.</param>
/// <param name="Title">The track's title, if known.</param>
/// <param name="Cosine">Similarity to the previous track (routes) or to the seed (similar).</param>
/// <param name="Reason">A short explanation of the hop or the likeness.</param>
public record TrackOut(string? ItemId, string Path, string? Artist, string? Title, double? Cosine, string? Reason);
