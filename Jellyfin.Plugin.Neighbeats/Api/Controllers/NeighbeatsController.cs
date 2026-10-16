using System.Net.Mime;
using Jellyfin.Plugin.Neighbeats.Client;
using Jellyfin.Plugin.Neighbeats.Playlists;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Neighbeats.Api.Controllers;

/// <summary>
/// Builds real Jellyfin playlists from neighbeats routes and similarity, for the
/// authenticated user who asked. Open to any authenticated user, not just admins.
/// </summary>
/// <param name="neighbeatsClient">Talks to neighbeats' serve API.</param>
/// <param name="playlistBuilder">Turns a neighbeats answer into a Jellyfin playlist.</param>
/// <param name="libraryManager">Resolves the request's Jellyfin item ids.</param>
/// <param name="authorizationContext">Identifies the calling user.</param>
[ApiController]
[Authorize]
[Route("Neighbeats")]
[Produces(MediaTypeNames.Application.Json)]
public class NeighbeatsController(
    NeighbeatsClient neighbeatsClient,
    PlaylistBuilder playlistBuilder,
    ILibraryManager libraryManager,
    IAuthorizationContext authorizationContext) : ControllerBase
{
    /// <summary>
    /// Passes neighbeats' health answer through, for the config page's Test connection button.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>neighbeats' health, or an error if it could not be reached.</returns>
    [HttpGet("Health")]
    public async Task<ActionResult<HealthResponse>> GetHealth(CancellationToken cancellationToken)
    {
        try
        {
            return await neighbeatsClient.HealthAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (NeighbeatsException ex)
        {
            return MapError(ex);
        }
    }

    /// <summary>
    /// Builds a playlist travelling from one track to another.
    /// </summary>
    /// <param name="request">The path request.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The built playlist, or an error if neighbeats could not route it.</returns>
    [HttpPost("Path")]
    public async Task<ActionResult<PlaylistBuildResponse>> PostPath(
        [FromBody] PathPlaylistRequest request,
        CancellationToken cancellationToken)
    {
        if (libraryManager.GetItemById(request.FromItemId) is not Audio from)
        {
            return NotFound(new ApiErrorBody("fromItemId is not a known audio track", null));
        }

        if (libraryManager.GetItemById(request.ToItemId) is not Audio to)
        {
            return NotFound(new ApiErrorBody("toItemId is not a known audio track", null));
        }

        var auth = await authorizationContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false);
        if (auth.User is null)
        {
            return Unauthorized();
        }

        var config = Plugin.Instance!.Configuration;

        var neighbeatsRequest = new PathRequest(
            new TrackRef(from.Id.ToString(), from.Path),
            new TrackRef(to.Id.ToString(), to.Path),
            request.Length ?? config.DefaultLength,
            request.Mode ?? config.DefaultMode,
            auth.User.Username,
            request.Theme,
            null,
            null);

        PathResponse answer;
        try
        {
            answer = await neighbeatsClient.PathAsync(neighbeatsRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (NeighbeatsException ex)
        {
            return MapError(ex);
        }

        var name = $"{config.PlaylistPrefix}{from.Name} → {to.Name}";
        return await BuildPlaylistResponseAsync(name, auth.UserId, answer.Tracks, answer.Notes).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a playlist of tracks that sound like one seed track.
    /// </summary>
    /// <param name="request">The similar request.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The built playlist, or an error if neighbeats could not answer.</returns>
    [HttpPost("Similar")]
    public async Task<ActionResult<PlaylistBuildResponse>> PostSimilar(
        [FromBody] SimilarPlaylistRequest request,
        CancellationToken cancellationToken)
    {
        if (libraryManager.GetItemById(request.ItemId) is not Audio seed)
        {
            return NotFound(new ApiErrorBody("itemId is not a known audio track", null));
        }

        var auth = await authorizationContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false);

        var neighbeatsRequest = new SimilarRequest(
            new TrackRef(seed.Id.ToString(), seed.Path),
            request.Count,
            null,
            null);

        SimilarResponse answer;
        try
        {
            answer = await neighbeatsClient.SimilarAsync(neighbeatsRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (NeighbeatsException ex)
        {
            return MapError(ex);
        }

        var name = $"{Plugin.Instance!.Configuration.PlaylistPrefix}like {seed.Name}";
        return await BuildPlaylistResponseAsync(name, auth.UserId, answer.Tracks, answer.Notes).ConfigureAwait(false);
    }

    private async Task<ActionResult<PlaylistBuildResponse>> BuildPlaylistResponseAsync(
        string name,
        Guid userId,
        IReadOnlyList<TrackOut> tracks,
        IReadOnlyList<string>? notes)
    {
        var build = await playlistBuilder.BuildAsync(name, userId, tracks).ConfigureAwait(false);
        if (build.PlaylistId is not { } playlistId)
        {
            return UnprocessableEntity(new ApiErrorBody("none of the tracks neighbeats found could be resolved in this library", null));
        }

        return new PlaylistBuildResponse(playlistId, build.Tracks.Count, build.Skipped, notes);
    }

    private ActionResult MapError(NeighbeatsException ex)
    {
        var status = ex.Kind switch
        {
            NeighbeatsErrorKind.NotRunning => StatusCodes.Status503ServiceUnavailable,
            NeighbeatsErrorKind.NotFound => StatusCodes.Status404NotFound,
            NeighbeatsErrorKind.NoRoute => StatusCodes.Status422UnprocessableEntity,
            NeighbeatsErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status502BadGateway,
        };
        return StatusCode(status, new ApiErrorBody(ex.Message, ex.Why));
    }
}
