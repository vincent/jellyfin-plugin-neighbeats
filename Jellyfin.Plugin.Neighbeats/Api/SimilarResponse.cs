namespace Jellyfin.Plugin.Neighbeats.Api;

/// <summary>
/// Answer to <c>POST /v1/similar</c>. The seed comes first in <see cref="Tracks"/>.
/// </summary>
/// <param name="Tracks">The seed track, followed by its neighbours.</param>
/// <param name="Notes">Notes about taste or filtering applied.</param>
public record SimilarResponse(IReadOnlyList<TrackOut> Tracks, IReadOnlyList<string>? Notes);
