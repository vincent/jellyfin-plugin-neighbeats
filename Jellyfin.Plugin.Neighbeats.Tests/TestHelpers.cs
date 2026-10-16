using System.Text.Json;
using Jellyfin.Plugin.Neighbeats.Api;
using Jellyfin.Plugin.Neighbeats.Client;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.Neighbeats.Tests;

/// <summary>
/// Shared helpers for building a <see cref="NeighbeatsClient"/> against a
/// <see cref="FakeNeighbeatsServer"/> and writing its canned JSON answers.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Builds a <see cref="NeighbeatsClient"/> connected to <paramref name="socketPath"/>,
    /// with a short timeout suited to tests.
    /// </summary>
    /// <param name="socketPath">The Unix socket path to connect to.</param>
    /// <param name="timeoutSeconds">The per-request timeout, in seconds.</param>
    /// <returns>The client, and the underlying <see cref="HttpClient"/> to dispose.</returns>
    public static (NeighbeatsClient Client, HttpClient HttpClient) BuildClient(string socketPath, int timeoutSeconds = 5)
    {
        var httpClient = new HttpClient(UnixSocketHandler.Create(() => socketPath))
        {
            BaseAddress = new Uri("http://neighbeats"),
        };
        var client = new NeighbeatsClient(httpClient, () => timeoutSeconds);
        return (client, httpClient);
    }

    /// <summary>
    /// Writes <paramref name="value"/> as the JSON response body, with the given status code.
    /// </summary>
    /// <param name="context">The request's <see cref="HttpContext"/>.</param>
    /// <param name="statusCode">The HTTP status code to answer with.</param>
    /// <param name="value">The value to serialize as the body.</param>
    /// <typeparam name="T">The type of <paramref name="value"/>.</typeparam>
    /// <returns>A task that completes once the response has been written.</returns>
    public static async Task WriteJsonAsync<T>(HttpContext context, int statusCode, T value)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(value, NeighbeatsJson.Options).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads and deserializes the request body as <typeparamref name="T"/>.
    /// </summary>
    /// <param name="context">The request's <see cref="HttpContext"/>.</param>
    /// <typeparam name="T">The type to deserialize the body as.</typeparam>
    /// <returns>The deserialized body.</returns>
    public static async Task<T?> ReadJsonAsync<T>(HttpContext context)
    {
        return await JsonSerializer.DeserializeAsync<T>(context.Request.Body, NeighbeatsJson.Options, context.RequestAborted).ConfigureAwait(false);
    }
}
