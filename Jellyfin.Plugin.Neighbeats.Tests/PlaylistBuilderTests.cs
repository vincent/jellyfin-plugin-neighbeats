using Jellyfin.Plugin.Neighbeats.Api;
using Jellyfin.Plugin.Neighbeats.Playlists;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Playlists;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.Neighbeats.Tests;

/// <summary>
/// Tests for <see cref="PlaylistBuilder"/>'s track resolution and create-or-update logic,
/// against mocked <see cref="ILibraryManager"/> and <see cref="IPlaylistManager"/>.
/// </summary>
public class PlaylistBuilderTests
{
    private readonly Mock<ILibraryManager> _libraryManager = new();
    private readonly Mock<IPlaylistManager> _playlistManager = new();
    private readonly Guid _userId = Guid.NewGuid();

    public PlaylistBuilderTests()
    {
        _playlistManager.Setup(m => m.GetPlaylists(_userId)).Returns([]);
    }

    [Fact]
    public async Task BuildAsync_ResolvesByItemId_WhenItemExists()
    {
        var itemId = Guid.NewGuid();
        _libraryManager.Setup(m => m.GetItemById(itemId)).Returns(new Audio { Id = itemId });
        _playlistManager
            .Setup(m => m.CreatePlaylist(It.IsAny<PlaylistCreationRequest>()))
            .ReturnsAsync(new PlaylistCreationResult(Guid.NewGuid().ToString()));

        var builder = new PlaylistBuilder(_libraryManager.Object, _playlistManager.Object);
        var track = new TrackOut(itemId.ToString(), "/music/a.mp3", "Artist", "Title", null, null);

        var result = await builder.BuildAsync("neighbeats: A → B", _userId, [track]);

        Assert.NotNull(result.PlaylistId);
        Assert.Single(result.Tracks);
        Assert.Empty(result.Skipped);
        _libraryManager.Verify(m => m.FindByPath(It.IsAny<string>(), It.IsAny<bool?>()), Times.Never);
    }

    [Fact]
    public async Task BuildAsync_FallsBackToPath_WhenItemIdUnknown()
    {
        var resolvedId = Guid.NewGuid();
        _libraryManager.Setup(m => m.GetItemById(It.IsAny<Guid>())).Returns((BaseItem?)null);
        _libraryManager.Setup(m => m.FindByPath("/music/new.mp3", false)).Returns(new Audio { Id = resolvedId });
        _playlistManager
            .Setup(m => m.CreatePlaylist(It.IsAny<PlaylistCreationRequest>()))
            .ReturnsAsync(new PlaylistCreationResult(Guid.NewGuid().ToString()));

        var builder = new PlaylistBuilder(_libraryManager.Object, _playlistManager.Object);
        var track = new TrackOut(null, "/music/new.mp3", "Artist", "Title", null, null);

        var result = await builder.BuildAsync("neighbeats: like A", _userId, [track]);

        Assert.NotNull(result.PlaylistId);
        Assert.Single(result.Tracks);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public async Task BuildAsync_Skips_WhenNeitherIdNorPathResolve()
    {
        _libraryManager.Setup(m => m.GetItemById(It.IsAny<Guid>())).Returns((BaseItem?)null);
        _libraryManager.Setup(m => m.FindByPath(It.IsAny<string>(), It.IsAny<bool?>())).Returns((BaseItem?)null);

        var builder = new PlaylistBuilder(_libraryManager.Object, _playlistManager.Object);
        var track = new TrackOut(null, "/music/gone.mp3", "Artist", "Title", null, null);

        var result = await builder.BuildAsync("neighbeats: like A", _userId, [track]);

        Assert.Null(result.PlaylistId);
        Assert.Empty(result.Tracks);
        Assert.Single(result.Skipped);
        _playlistManager.Verify(m => m.CreatePlaylist(It.IsAny<PlaylistCreationRequest>()), Times.Never);
    }

    [Fact]
    public async Task BuildAsync_UpdatesExistingPlaylist_InsteadOfCreatingANewOne()
    {
        var itemId = Guid.NewGuid();
        var existingPlaylistId = Guid.NewGuid();
        _libraryManager.Setup(m => m.GetItemById(itemId)).Returns(new Audio { Id = itemId });
        _playlistManager
            .Setup(m => m.GetPlaylists(_userId))
            .Returns([new Playlist { Id = existingPlaylistId, Name = "neighbeats: A → B" }]);

        var builder = new PlaylistBuilder(_libraryManager.Object, _playlistManager.Object);
        var track = new TrackOut(itemId.ToString(), "/music/a.mp3", "Artist", "Title", null, null);

        var result = await builder.BuildAsync("neighbeats: A → B", _userId, [track]);

        Assert.Equal(existingPlaylistId, result.PlaylistId);
        _playlistManager.Verify(m => m.UpdatePlaylist(It.Is<PlaylistUpdateRequest>(r => r.Id == existingPlaylistId)), Times.Once);
        _playlistManager.Verify(m => m.CreatePlaylist(It.IsAny<PlaylistCreationRequest>()), Times.Never);
    }
}
