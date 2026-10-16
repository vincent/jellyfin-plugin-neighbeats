using System.Net.Mime;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Neighbeats.Mixing;

/// <summary>
/// Overrides Jellyfin's native <c>Items/{itemId}/InstantMix</c> with neighbeats' similarity
/// graph (see <see cref="NeighbeatsSimilarityEngine"/>), topping up any shortfall - or the
/// whole mix, when the override is disabled or neighbeats is unreachable - from Jellyfin's
/// own <see cref="IMusicManager.GetInstantMixFromItem"/>. <see cref="NeighbeatsControllerConvention"/>
/// removes the native action so this is the only handler left for the route.
/// </summary>
/// <param name="similarityEngine">Finds items similar to the requested one via neighbeats.</param>
/// <param name="libraryManager">Resolves the requested item.</param>
/// <param name="userManager">Resolves the requesting user, if given.</param>
/// <param name="musicManager">Jellyfin's native instant-mix logic, used as a fallback and to top up any shortfall.</param>
/// <param name="dtoService">Converts the resulting items to <see cref="BaseItemDto"/>.</param>
[ApiController]
[Authorize]
[Route("Items/{itemId}/InstantMix", Order = -1)]
[Produces(MediaTypeNames.Application.Json)]
public class NeighbeatsInstantMixController(
    NeighbeatsSimilarityEngine similarityEngine,
    ILibraryManager libraryManager,
    IUserManager userManager,
    IMusicManager musicManager,
    IDtoService dtoService) : ControllerBase
{
    private const int DefaultLimit = 200;

    /// <summary>
    /// Builds an instant mix for the given item.
    /// </summary>
    /// <param name="itemId">The seed item: a song, album, artist, or playlist.</param>
    /// <param name="userId">The requesting user, if any.</param>
    /// <param name="limit">The maximum number of items to return.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The mix, drawn from neighbeats first and topped up from Jellyfin's native mix.</returns>
    [HttpGet]
    public async Task<ActionResult<QueryResult<BaseItemDto>>> GetInstantMix(
        [FromRoute] Guid itemId,
        [FromQuery] Guid? userId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var item = libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return NotFound();
        }

        var user = userId is { } uid ? userManager.GetUserById(uid) : null;
        var effectiveLimit = limit ?? DefaultLimit;
        var dtoOptions = new DtoOptions();

        var seen = new HashSet<Guid> { item.Id };
        var mix = new List<BaseItem>();

        if (Plugin.Instance!.Configuration.EnableInstantMixOverride)
        {
            var neighbeatsItems = await similarityEngine
                .GetSimilarAsync(item, seen, effectiveLimit, cancellationToken)
                .ConfigureAwait(false);

            foreach (var candidate in neighbeatsItems)
            {
                if (seen.Add(candidate.Id))
                {
                    mix.Add(candidate);
                }
            }
        }

        if (mix.Count < effectiveLimit)
        {
            foreach (var candidate in musicManager.GetInstantMixFromItem(item, user, dtoOptions))
            {
                if (mix.Count >= effectiveLimit)
                {
                    break;
                }

                if (seen.Add(candidate.Id))
                {
                    mix.Add(candidate);
                }
            }
        }

        var dtos = dtoService.GetBaseItemDtos(mix, dtoOptions, user);
        return new QueryResult<BaseItemDto>(dtos);
    }
}
