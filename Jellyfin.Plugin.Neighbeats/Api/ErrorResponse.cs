namespace Jellyfin.Plugin.Neighbeats.Api;

/// <summary>
/// A plain error answer from neighbeats, used for 400 and 500 responses.
/// </summary>
/// <param name="Error">The error message.</param>
public record ErrorResponse(string Error);
