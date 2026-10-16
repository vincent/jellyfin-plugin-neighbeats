using Jellyfin.Plugin.Neighbeats.Client;
using Jellyfin.Plugin.Neighbeats.Mixing;
using Jellyfin.Plugin.Neighbeats.Playlists;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Neighbeats;

/// <summary>
/// Registers the neighbeats client, the playlist builder, and the Instant Mix / "More like
/// this" overrides with Jellyfin's dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection
            .AddHttpClient<NeighbeatsClient>(client => client.BaseAddress = new Uri("http://neighbeats"))
            .ConfigurePrimaryHttpMessageHandler(() => UnixSocketHandler.Create(() => Plugin.Instance!.Configuration.SocketPath));

        serviceCollection.AddScoped<PlaylistBuilder>();

        serviceCollection.AddScoped<NeighbeatsSimilarityEngine>();
        serviceCollection.AddScoped<ILocalSimilarItemsProvider, NeighbeatsSimilarItemsProvider>();

        // ILoggerFactory is resolved lazily here (once options are first requested, after
        // the container is built), not eagerly: RegisterServices runs while the container
        // is still being assembled, so applicationHost.Resolve<T>() is not yet usable.
        serviceCollection.AddOptions<MvcOptions>().Configure<ILoggerFactory>(
            (options, loggerFactory) => options.Conventions.Add(new NeighbeatsControllerConvention(loggerFactory.CreateLogger<NeighbeatsControllerConvention>())));
    }
}
