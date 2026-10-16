using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Neighbeats.Mixing;

/// <summary>
/// Removes Jellyfin's native <c>InstantMixController.GetInstantMixFromItem</c> action so
/// <see cref="NeighbeatsInstantMixController"/> is the only handler left for
/// <c>Items/{itemId}/InstantMix</c>. Matches by name rather than type: the native
/// controller lives in Jellyfin's own server assembly, which this plugin does not (and
/// cannot) reference at compile time.
/// </summary>
/// <remarks>
/// This removal is unconditional - it does not depend on the plugin's configuration -
/// because the convention only runs once, at startup. <see cref="NeighbeatsInstantMixController"/>
/// itself checks <see cref="Configuration.PluginConfiguration.EnableInstantMixOverride"/> on
/// every request and falls back to Jellyfin's native mix logic when it is off, so the
/// setting still takes effect immediately without a server restart.
/// </remarks>
/// <param name="logger">Logs whether the native action was found and removed.</param>
public class NeighbeatsControllerConvention(ILogger<NeighbeatsControllerConvention> logger) : IControllerModelConvention
{
    /// <inheritdoc />
    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType.Name != "InstantMixController"
            || controller.ControllerType.Namespace is not { } ns
            || !ns.Contains("Jellyfin.Api.Controllers", StringComparison.Ordinal))
        {
            return;
        }

        var action = controller.Actions.FirstOrDefault(a => a.ActionName == "GetInstantMixFromItem");
        if (action is null)
        {
            logger.LogWarning("Could not find Jellyfin's native InstantMixController.GetInstantMixFromItem action to remove; neighbeats' Instant Mix override may not take effect.");
            return;
        }

        controller.Actions.Remove(action);
        logger.LogInformation("Removed Jellyfin's native InstantMixController.GetInstantMixFromItem; neighbeats now serves Items/{{itemId}}/InstantMix.");
    }
}
