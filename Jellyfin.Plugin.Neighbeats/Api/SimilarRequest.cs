namespace Jellyfin.Plugin.Neighbeats.Api;

/// <summary>
/// Request body for <c>POST /v1/similar</c>.
/// </summary>
/// <param name="Item">The seed track.</param>
/// <param name="Count">Number of neighbours to return.</param>
/// <param name="MaxPerArtist">Limit how many neighbours may share an artist.</param>
/// <param name="MaxMinutes">Leave out tracks longer than this; 0 means no limit.</param>
public record SimilarRequest(TrackRef Item, int? Count, int? MaxPerArtist, int? MaxMinutes);
