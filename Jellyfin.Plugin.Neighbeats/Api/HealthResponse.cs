namespace Jellyfin.Plugin.Neighbeats.Api;

/// <summary>
/// Answer to <c>GET /v1/health</c>.
/// </summary>
/// <param name="Version">neighbeats' version.</param>
/// <param name="Tracks">Number of tracks in the index.</param>
/// <param name="LyricVectors">Number of tracks with lyric vectors.</param>
/// <param name="LoadedAt">When the index currently in memory was loaded.</param>
public record HealthResponse(string Version, int Tracks, int LyricVectors, DateTimeOffset LoadedAt);
