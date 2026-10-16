using Jellyfin.Plugin.Neighbeats.Api;
using Jellyfin.Plugin.Neighbeats.Client;
using Jellyfin.Plugin.Neighbeats.Mixing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.Neighbeats.Tests;

/// <summary>
/// Tests for <see cref="NeighbeatsSimilarityEngine"/>'s seed selection, per-seed merging,
/// and graceful degradation when neighbeats cannot answer.
/// </summary>
public class NeighbeatsSimilarityEngineTests
{
    private readonly Mock<ILibraryManager> _libraryManager = new();

    [Fact]
    public async Task GetSimilarAsync_Song_SeedsFromItself()
    {
        var seed = new Audio { Id = Guid.NewGuid(), Path = "/music/seed.mp3" };
        var neighbourId = Guid.NewGuid();
        var neighbour = new Audio { Id = neighbourId, Path = "/music/neighbour.mp3" };
        _libraryManager.Setup(m => m.GetItemById(neighbourId)).Returns(neighbour);

        var requestCount = 0;
        await using var server = new FakeNeighbeatsServer();
        server.Handler = async ctx =>
        {
            requestCount++;
            await TestHelpers.WriteJsonAsync(
                ctx,
                200,
                new SimilarResponse(
                    [
                        new TrackOut(seed.Id.ToString(), seed.Path, null, null, null, null),
                        new TrackOut(neighbourId.ToString(), neighbour.Path, "Artist", "Title", 0.9, "similar"),
                    ],
                    null)).ConfigureAwait(false);
        };
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var engine = new NeighbeatsSimilarityEngine(client, _libraryManager.Object, NullLogger<NeighbeatsSimilarityEngine>.Instance);

            var result = await engine.GetSimilarAsync(seed, [], 20, CancellationToken.None);

            Assert.Equal(1, requestCount);
            Assert.Single(result);
            Assert.Equal(neighbourId, result[0].Id);
        }
    }

    [Fact]
    public async Task GetSimilarAsync_Folder_SeedsFromEachChild_AndDedupesAcrossSeeds()
    {
        var trackA = new Audio { Id = Guid.NewGuid(), Path = "/music/a.mp3" };
        var trackB = new Audio { Id = Guid.NewGuid(), Path = "/music/b.mp3" };
        var album = new MusicAlbum { Id = Guid.NewGuid(), Path = "/music/album" };
        album.Children = [trackA, trackB];

        var sharedNeighbourId = Guid.NewGuid();
        var sharedNeighbour = new Audio { Id = sharedNeighbourId, Path = "/music/shared.mp3" };
        _libraryManager.Setup(m => m.GetItemById(sharedNeighbourId)).Returns(sharedNeighbour);

        var requestCount = 0;
        await using var server = new FakeNeighbeatsServer();
        server.Handler = async ctx =>
        {
            requestCount++;
            var request = await TestHelpers.ReadJsonAsync<SimilarRequest>(ctx).ConfigureAwait(false);
            await TestHelpers.WriteJsonAsync(
                ctx,
                200,
                new SimilarResponse(
                    [
                        new TrackOut(request!.Item.ItemId, request.Item.Path!, null, null, null, null),
                        new TrackOut(sharedNeighbourId.ToString(), sharedNeighbour.Path, "Artist", "Title", 0.8, "similar"),
                    ],
                    null)).ConfigureAwait(false);
        };
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var engine = new NeighbeatsSimilarityEngine(client, _libraryManager.Object, NullLogger<NeighbeatsSimilarityEngine>.Instance);

            var result = await engine.GetSimilarAsync(album, [], 20, CancellationToken.None);

            Assert.Equal(2, requestCount);
            Assert.Single(result);
            Assert.Equal(sharedNeighbourId, result[0].Id);
        }
    }

    [Fact]
    public async Task GetSimilarAsync_LargeFolder_SamplesUpToMaxSeeds()
    {
        var tracks = Enumerable.Range(0, 100)
            .Select(i => new Audio { Id = Guid.NewGuid(), Path = $"/music/{i}.mp3" })
            .ToList();
        var album = new MusicAlbum { Id = Guid.NewGuid(), Path = "/music/album" };
        album.Children = tracks.Cast<BaseItem>().ToList();

        var requestCount = 0;
        await using var server = new FakeNeighbeatsServer();
        server.Handler = async ctx =>
        {
            requestCount++;
            await TestHelpers.WriteJsonAsync(ctx, 200, new SimilarResponse([], null)).ConfigureAwait(false);
        };
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var engine = new NeighbeatsSimilarityEngine(client, _libraryManager.Object, NullLogger<NeighbeatsSimilarityEngine>.Instance);

            await engine.GetSimilarAsync(album, [], 20, CancellationToken.None);

            Assert.Equal(NeighbeatsSimilarityEngine.MaxSeeds, requestCount);
        }
    }

    [Fact]
    public async Task GetSimilarAsync_SeedNotFound_ContinuesToNextSeed()
    {
        var trackA = new Audio { Id = Guid.NewGuid(), Path = "/music/a.mp3" };
        var trackB = new Audio { Id = Guid.NewGuid(), Path = "/music/b.mp3" };
        var album = new MusicAlbum { Id = Guid.NewGuid(), Path = "/music/album" };
        album.Children = [trackA, trackB];

        var neighbourId = Guid.NewGuid();
        var neighbour = new Audio { Id = neighbourId, Path = "/music/neighbour.mp3" };
        _libraryManager.Setup(m => m.GetItemById(neighbourId)).Returns(neighbour);

        var requestCount = 0;
        await using var server = new FakeNeighbeatsServer();
        server.Handler = async ctx =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                await TestHelpers.WriteJsonAsync(ctx, 404, new ErrorResponse("track not analysed")).ConfigureAwait(false);
                return;
            }

            await TestHelpers.WriteJsonAsync(
                ctx,
                200,
                new SimilarResponse(
                    [
                        new TrackOut(trackB.Id.ToString(), trackB.Path, null, null, null, null),
                        new TrackOut(neighbourId.ToString(), neighbour.Path, "Artist", "Title", 0.8, "similar"),
                    ],
                    null)).ConfigureAwait(false);
        };
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var engine = new NeighbeatsSimilarityEngine(client, _libraryManager.Object, NullLogger<NeighbeatsSimilarityEngine>.Instance);

            var result = await engine.GetSimilarAsync(album, [], 20, CancellationToken.None);

            Assert.Equal(2, requestCount);
            Assert.Single(result);
            Assert.Equal(neighbourId, result[0].Id);
        }
    }

    [Fact]
    public async Task GetSimilarAsync_NeighbeatsNotRunning_StopsAfterFirstSeed_ReturnsEmpty()
    {
        var trackA = new Audio { Id = Guid.NewGuid(), Path = "/music/a.mp3" };
        var trackB = new Audio { Id = Guid.NewGuid(), Path = "/music/b.mp3" };
        var album = new MusicAlbum { Id = Guid.NewGuid(), Path = "/music/album" };
        album.Children = [trackA, trackB];

        var missingSocketPath = Path.Combine(Path.GetTempPath(), $"neighbeats-missing-{Guid.NewGuid():N}.sock");
        var (client, http) = TestHelpers.BuildClient(missingSocketPath);
        using (http)
        {
            var engine = new NeighbeatsSimilarityEngine(client, _libraryManager.Object, NullLogger<NeighbeatsSimilarityEngine>.Instance);

            var result = await engine.GetSimilarAsync(album, [], 20, CancellationToken.None);

            Assert.Empty(result);
        }
    }

    [Fact]
    public async Task GetSimilarAsync_UnsupportedItemType_ReturnsEmpty_WithoutCallingNeighbeats()
    {
        var missingSocketPath = Path.Combine(Path.GetTempPath(), $"neighbeats-missing-{Guid.NewGuid():N}.sock");
        var (client, http) = TestHelpers.BuildClient(missingSocketPath);
        using (http)
        {
            var engine = new NeighbeatsSimilarityEngine(client, _libraryManager.Object, NullLogger<NeighbeatsSimilarityEngine>.Instance);
            var unsupported = new Video { Id = Guid.NewGuid(), Path = "/movies/a.mkv" };

            var result = await engine.GetSimilarAsync(unsupported, [], 20, CancellationToken.None);

            Assert.Empty(result);
        }
    }

    [Fact]
    public async Task GetSimilarAsync_ExcludesSeedsAndGivenIds()
    {
        var seed = new Audio { Id = Guid.NewGuid(), Path = "/music/seed.mp3" };
        var excludedId = Guid.NewGuid();
        var excludedItem = new Audio { Id = excludedId, Path = "/music/excluded.mp3" };
        _libraryManager.Setup(m => m.GetItemById(excludedId)).Returns(excludedItem);

        await using var server = new FakeNeighbeatsServer();
        server.Handler = ctx => TestHelpers.WriteJsonAsync(
            ctx,
            200,
            new SimilarResponse(
                [
                    new TrackOut(seed.Id.ToString(), seed.Path, null, null, null, null),
                    new TrackOut(seed.Id.ToString(), seed.Path, null, null, null, "seed again"),
                    new TrackOut(excludedId.ToString(), excludedItem.Path, "Artist", "Title", 0.8, "similar"),
                ],
                null));
        await server.StartAsync();

        var (client, http) = TestHelpers.BuildClient(server.SocketPath);
        using (http)
        {
            var engine = new NeighbeatsSimilarityEngine(client, _libraryManager.Object, NullLogger<NeighbeatsSimilarityEngine>.Instance);

            var result = await engine.GetSimilarAsync(seed, [excludedId], 20, CancellationToken.None);

            Assert.Empty(result);
        }
    }
}
