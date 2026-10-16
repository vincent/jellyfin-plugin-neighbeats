namespace Jellyfin.Plugin.Neighbeats.Api;

/// <summary>
/// Names a track for neighbeats: either field may be given, or both. <see cref="ItemId"/>
/// is tried first; <see cref="Path"/> is the file as the media server sees it and works
/// even for tracks neighbeats has not synced from Jellyfin yet.
/// </summary>
/// <param name="ItemId">The Jellyfin item id, or <c>null</c>.</param>
/// <param name="Path">The file path as the media server sees it, or <c>null</c>.</param>
public record TrackRef(string? ItemId, string? Path);
