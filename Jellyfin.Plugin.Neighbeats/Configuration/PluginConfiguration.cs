using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Neighbeats.Configuration;

/// <summary>
/// Configuration for the neighbeats plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the path to the neighbeats `serve` Unix socket.
    /// </summary>
    public string SocketPath { get; set; } = "/run/neighbeats/neighbeats.sock";

    /// <summary>
    /// Gets or sets the default playlist length (number of tracks) for a path request
    /// when the caller does not specify one. 0 means the cheapest route, any length.
    /// </summary>
    public int DefaultLength { get; set; } = 15;

    /// <summary>
    /// Gets or sets the default taste mode for a path request: comfort, explore,
    /// rediscover or none.
    /// </summary>
    public string DefaultMode { get; set; } = "comfort";

    /// <summary>
    /// Gets or sets the prefix added to every playlist name this plugin creates.
    /// </summary>
    public string PlaylistPrefix { get; set; } = "neighbeats: ";

    /// <summary>
    /// Gets or sets the per-request timeout, in seconds, for calls to neighbeats.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether neighbeats serves Jellyfin's native
    /// <c>Items/{itemId}/InstantMix</c> endpoint, falling back to Jellyfin's own mix for
    /// any shortfall or when neighbeats cannot answer.
    /// </summary>
    public bool EnableInstantMixOverride { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether neighbeats contributes to the "More like
    /// this" rows on item detail pages, alongside Jellyfin's own similar-items logic.
    /// </summary>
    public bool EnableSimilarItemsOverride { get; set; }
}
