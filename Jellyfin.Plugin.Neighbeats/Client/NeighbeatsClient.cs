using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Jellyfin.Plugin.Neighbeats.Api;

namespace Jellyfin.Plugin.Neighbeats.Client;

/// <summary>
/// Talks to neighbeats' <c>serve</c> API over its Unix socket. See api.md for the
/// protocol; every failure is mapped to a <see cref="NeighbeatsException"/> with a
/// plain-words message, never a bare exception from inside the socket or JSON layer.
/// </summary>
public class NeighbeatsClient
{
    private readonly HttpClient _httpClient;
    private readonly Func<int> _timeoutSecondsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="NeighbeatsClient"/> class.
    /// </summary>
    /// <param name="httpClient">The typed <see cref="HttpClient"/>, configured to connect over the neighbeats Unix socket.</param>
    /// <param name="timeoutSecondsProvider">
    /// Returns the per-request timeout in seconds, read fresh on every call. Defaults to
    /// the plugin's configured value; tests can override it.
    /// </param>
    public NeighbeatsClient(HttpClient httpClient, Func<int>? timeoutSecondsProvider = null)
    {
        _httpClient = httpClient;
        _timeoutSecondsProvider = timeoutSecondsProvider ?? (() => Plugin.Instance?.Configuration.TimeoutSeconds ?? 30);
    }

    /// <summary>
    /// Calls <c>GET /v1/health</c>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>neighbeats' health answer.</returns>
    public Task<HealthResponse> HealthAsync(CancellationToken cancellationToken) =>
        SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "/v1/health"),
            r => r.Content.ReadFromJsonAsync<HealthResponse>(NeighbeatsJson.Options, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Calls <c>POST /v1/path</c>.
    /// </summary>
    /// <param name="request">The path request.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The route found.</returns>
    /// <exception cref="NeighbeatsException">
    /// Thrown with <see cref="NeighbeatsErrorKind.NoRoute"/> when no route exists.
    /// </exception>
    public Task<PathResponse> PathAsync(PathRequest request, CancellationToken cancellationToken) =>
        SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "/v1/path") { Content = JsonContent.Create(request, options: NeighbeatsJson.Options) },
            r => r.Content.ReadFromJsonAsync<PathResponse>(NeighbeatsJson.Options, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Calls <c>POST /v1/similar</c>.
    /// </summary>
    /// <param name="request">The similar request.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The seed track followed by its neighbours.</returns>
    public Task<SimilarResponse> SimilarAsync(SimilarRequest request, CancellationToken cancellationToken) =>
        SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "/v1/similar") { Content = JsonContent.Create(request, options: NeighbeatsJson.Options) },
            r => r.Content.ReadFromJsonAsync<SimilarResponse>(NeighbeatsJson.Options, cancellationToken),
            cancellationToken);

    private async Task<T> SendAsync<T>(
        Func<HttpRequestMessage> buildRequest,
        Func<HttpResponseMessage, Task<T?>> readSuccess,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSecondsProvider()));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(buildRequest(), linkedCts.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException)
        {
            throw new NeighbeatsException(NeighbeatsErrorKind.NotRunning, "neighbeats is not running", innerException: ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NeighbeatsException(NeighbeatsErrorKind.Timeout, "neighbeats did not answer in time", innerException: ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                var value = await readSuccess(response).ConfigureAwait(false);
                return value ?? throw new NeighbeatsException(NeighbeatsErrorKind.ServerError, "neighbeats sent an empty answer");
            }

            await ThrowForFailureAsync(response, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("unreachable"); // ThrowForFailureAsync always throws.
        }
    }

    private static async Task ThrowForFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var noRoute = await response.Content.ReadFromJsonAsync<PathResponse>(NeighbeatsJson.Options, cancellationToken).ConfigureAwait(false);
            throw new NeighbeatsException(
                NeighbeatsErrorKind.NoRoute,
                noRoute?.Error ?? "no route found",
                noRoute?.Why,
                noRoute?.Tracks);
        }

        ErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ErrorResponse>(NeighbeatsJson.Options, cancellationToken).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException)
        {
            // Fall through: neighbeats is expected to always answer with JSON, but if it
            // did not, the status code alone still tells us what kind of failure this is.
        }

        var kind = response.StatusCode switch
        {
            HttpStatusCode.NotFound => NeighbeatsErrorKind.NotFound,
            HttpStatusCode.BadRequest => NeighbeatsErrorKind.BadRequest,
            _ => NeighbeatsErrorKind.ServerError,
        };
        var message = error?.Error ?? $"neighbeats answered with {(int)response.StatusCode}";
        throw new NeighbeatsException(kind, message);
    }
}
