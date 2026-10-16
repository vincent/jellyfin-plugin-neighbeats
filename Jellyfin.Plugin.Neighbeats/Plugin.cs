using Jellyfin.Plugin.Neighbeats.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Neighbeats;

/// <summary>
/// The neighbeats plugin: builds Jellyfin playlists from neighbeats routes and similarity.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Neighbeats";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("446738be-385c-4691-a21a-20ed10df531e");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            },
            new PluginPageInfo
            {
                Name = "neighbeats-build",
                DisplayName = "Build a playlist",

                // Jellyfin's plugin-page listing (which populates the admin Dashboard menu)
                // requires elevation, but the page itself (GET web/ConfigurationPage?name=)
                // does not. Any authenticated user can open it directly; it is meant for
                // them, not for the admin Dashboard, so it stays out of that menu.
                EnableInMainMenu = false,
                EmbeddedResourcePath = $"{GetType().Namespace}.Web.build.html"
            }
        ];
    }
}
