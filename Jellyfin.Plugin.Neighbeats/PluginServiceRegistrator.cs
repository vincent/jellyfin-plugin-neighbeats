using Jellyfin.Plugin.Neighbeats.Client;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Neighbeats;

/// <summary>
/// Registers the neighbeats client with Jellyfin's dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection
            .AddHttpClient<NeighbeatsClient>(client => client.BaseAddress = new Uri("http://neighbeats"))
            .ConfigurePrimaryHttpMessageHandler(() => UnixSocketHandler.Create(() => Plugin.Instance!.Configuration.SocketPath));
    }
}
