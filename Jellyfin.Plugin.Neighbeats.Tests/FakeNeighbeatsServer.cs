using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Neighbeats.Tests;

/// <summary>
/// A minimal stand-in for `neighbeats serve`, listening on a temp Unix socket so tests
/// can exercise <see cref="Client.NeighbeatsClient"/> against a real HTTP-over-socket
/// server instead of mocking <see cref="HttpClient"/> directly.
/// </summary>
public sealed class FakeNeighbeatsServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeNeighbeatsServer"/> class.
    /// </summary>
    public FakeNeighbeatsServer()
    {
        SocketPath = Path.Combine(Path.GetTempPath(), $"neighbeats-test-{Guid.NewGuid():N}.sock");

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.ListenUnixSocket(SocketPath));

        _app = builder.Build();
        _app.Run(async context =>
        {
            if (Handler is { } handler)
            {
                await handler(context).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
        });
    }

    /// <summary>
    /// Gets the Unix socket path this server listens on.
    /// </summary>
    public string SocketPath { get; }

    /// <summary>
    /// Gets or sets the handler that answers every request, set per test.
    /// </summary>
    public Func<HttpContext, Task>? Handler { get; set; }

    /// <summary>
    /// Starts listening on <see cref="SocketPath"/>.
    /// </summary>
    /// <returns>A task that completes once the server is listening.</returns>
    public Task StartAsync() => _app.StartAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(false);
        if (File.Exists(SocketPath))
        {
            File.Delete(SocketPath);
        }
    }
}
