using System.Globalization;
using Jellyfin.Plugin.Neighbeats.Api;
using Jellyfin.Plugin.Neighbeats.Client;
using Xunit;

namespace Jellyfin.Plugin.Neighbeats.Tests;

/// <summary>
/// Tests for <see cref="NeighbeatsClient"/> against a <see cref="FakeNeighbeatsServer"/>.
/// </summary>
public class NeighbeatsClientTests
{
    [Fact]
    public async Task HealthAsync_HappyPath_ReturnsHealth()
    {
        await using var server = new FakeNeighbeatsServer();
        server.Handler = ctx => TestHelpers.WriteJsonAsync(
            ctx,
            200,
            new HealthResponse("1.2.0", 25125, 9170, DateTimeOffset.Parse("2026-10-16T17:18:21+02:00", CultureInfo.InvariantCulture)));
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var health = await client.HealthAsync(CancellationToken.None);

            Assert.Equal("1.2.0", health.Version);
            Assert.Equal(25125, health.Tracks);
            Assert.Equal(9170, health.LyricVectors);
        }
    }

    [Fact]
    public async Task PathAsync_HappyPath_ReturnsRoute()
    {
        await using var server = new FakeNeighbeatsServer();
        server.Handler = ctx => TestHelpers.WriteJsonAsync(
            ctx,
            200,
            new PathResponse(
                true,
                [new TrackOut("item-a", "/music/a.mp3", "Artist A", "Song A", null, "seed")],
                4.21,
                ["taste: comfort mode for vincent"],
                null,
                null));
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var request = new PathRequest(
                new TrackRef("item-a", null),
                new TrackRef("item-b", null),
                15,
                "comfort",
                "vincent",
                null,
                null,
                15);

            var response = await client.PathAsync(request, CancellationToken.None);

            Assert.True(response.Found);
            Assert.Single(response.Tracks);
            Assert.Equal(4.21, response.Cost);
        }
    }

    [Fact]
    public async Task PathAsync_NotFound_ThrowsWithNeighbeatsMessage()
    {
        await using var server = new FakeNeighbeatsServer();
        server.Handler = ctx => TestHelpers.WriteJsonAsync(ctx, 404, new ErrorResponse("track not analysed"));
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var request = new PathRequest(new TrackRef("missing", null), new TrackRef("item-b", null), null, null, null, null, null, null);

            var ex = await Assert.ThrowsAsync<NeighbeatsException>(() => client.PathAsync(request, CancellationToken.None));

            Assert.Equal(NeighbeatsErrorKind.NotFound, ex.Kind);
            Assert.Equal("track not analysed", ex.Message);
        }
    }

    [Fact]
    public async Task PathAsync_NoRoute_ThrowsWithWhyAndPartialTracks()
    {
        await using var server = new FakeNeighbeatsServer();
        server.Handler = ctx => TestHelpers.WriteJsonAsync(
            ctx,
            422,
            new PathResponse(
                false,
                [new TrackOut("item-a", "/music/a.mp3", null, null, null, null)],
                null,
                null,
                "no route found",
                "ran out of neighbours after 3 hops"));
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var request = new PathRequest(new TrackRef("item-a", null), new TrackRef("item-b", null), null, null, null, null, null, null);

            var ex = await Assert.ThrowsAsync<NeighbeatsException>(() => client.PathAsync(request, CancellationToken.None));

            Assert.Equal(NeighbeatsErrorKind.NoRoute, ex.Kind);
            Assert.Equal("no route found", ex.Message);
            Assert.Equal("ran out of neighbours after 3 hops", ex.Why);
            Assert.NotNull(ex.PartialTracks);
            Assert.Single(ex.PartialTracks!);
        }
    }

    [Fact]
    public async Task PathAsync_NullItemId_Deserializes()
    {
        await using var server = new FakeNeighbeatsServer();
        server.Handler = ctx => TestHelpers.WriteJsonAsync(
            ctx,
            200,
            new PathResponse(
                true,
                [new TrackOut(null, "/music/unsynced.mp3", "Artist", "Title", 0.71, "similarity 0.71")],
                1.0,
                null,
                null,
                null));
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var request = new PathRequest(new TrackRef("item-a", null), new TrackRef(null, "/music/unsynced.mp3"), null, null, null, null, null, null);

            var response = await client.PathAsync(request, CancellationToken.None);

            var track = Assert.Single(response.Tracks);
            Assert.Null(track.ItemId);
            Assert.Equal("/music/unsynced.mp3", track.Path);
        }
    }

    [Fact]
    public async Task HealthAsync_SocketMissing_ThrowsNotRunning()
    {
        var missingSocketPath = Path.Combine(Path.GetTempPath(), $"neighbeats-missing-{Guid.NewGuid():N}.sock");
        var (client, http) = TestHelpers.BuildClient(missingSocketPath);
        using (http)
        {
            var ex = await Assert.ThrowsAsync<NeighbeatsException>(() => client.HealthAsync(CancellationToken.None));

            Assert.Equal(NeighbeatsErrorKind.NotRunning, ex.Kind);
        }
    }

    [Fact]
    public async Task HealthAsync_ServerTooSlow_ThrowsTimeout()
    {
        await using var server = new FakeNeighbeatsServer();
        server.Handler = async ctx =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ctx.RequestAborted).ConfigureAwait(false);
            await TestHelpers.WriteJsonAsync(ctx, 200, new HealthResponse("1.0.0", 0, 0, DateTimeOffset.UtcNow)).ConfigureAwait(false);
        };
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath, timeoutSeconds: 1);
        using (http)
        {
            var ex = await Assert.ThrowsAsync<NeighbeatsException>(() => client.HealthAsync(CancellationToken.None));

            Assert.Equal(NeighbeatsErrorKind.Timeout, ex.Kind);
        }
    }
}
