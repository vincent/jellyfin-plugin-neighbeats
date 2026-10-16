namespace Jellyfin.Plugin.Neighbeats.Api.Controllers;

/// <summary>
/// Request body for <c>POST Neighbeats/Similar</c>.
/// </summary>
/// <param name="ItemId">The Jellyfin item id of the seed track.</param>
/// <param name="Count">Number of neighbours to return.</param>
public record SimilarPlaylistRequest(Guid ItemId, int? Count);
