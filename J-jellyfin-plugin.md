# J: Jellyfin plugin (bootstrap)

neighbeats is a personal project, cloned in ~/Workspace/Perso/neighbeats

Goal: from inside Jellyfin, pick two tracks (or one, for "similar") and get a real
Jellyfin playlist built by neighbeats, for the user who asked.

Starting point: commit `dfc3872`, `neighbeats serve`. HTTP + JSON on a Unix socket
(`/run/neighbeats/neighbeats.sock`, mode 0660), endpoints `GET /v1/health`,
`POST /v1/path` and `POST /v1/similar`. Answers carry Jellyfin item ids, or `null`
plus the path as Jellyfin sees it. Spec: [api.md](../api.md). neighbeats never writes
to Jellyfin. The plugin owns the playlist.

Sources: [jellyfin-plugin-template README](https://github.com/jellyfin/jellyfin-plugin-template)
and [Plugin Repositories post](https://jellyfin.org/posts/plugin-updates) (manifest format).
The template targets 10.11.x on net8/net9, and our server is 12.2.0, so every version
below is checked against the live server and NuGet, never copied from the docs.

## Decisions

- **Separate repository** `jellyfin-plugin-neighbeats`, created from the template
  (`dotnet new Jellyfin-plugin`). It is **GPLv3**: it links against GPLv3 Jellyfin
  packages, so it cannot be proprietary. neighbeats itself is unaffected, since it
  only talks over a socket.
- **Jellyfin builds the playlist** through `IPlaylistManager`, with the requesting user
  as owner. The plugin needs no API key and no neighbeats-side Jellyfin write.
- **Socket, not TCP**: one typed `HttpClient` whose `SocketsHttpHandler.ConnectCallback`
  opens a `UnixDomainSocketEndPoint` (snippet in api.md). The socket path is a plugin
  setting.
- **Item ids in, item ids out.** Send `item_id` **and** `path` for each endpoint (the
  item's `Path` is at hand in the plugin). For an answer with `item_id: null`, resolve
  `path` with `ILibraryManager.FindByPath(path, false)`. If that fails too, skip the
  track and say so.

## Tasks

### B0: verify before writing code

- [x] Server version → NuGet `Jellyfin.Controller` / `Jellyfin.Model` of the **same**
      version (a mismatch shows the plugin as `NotSupported`). Note the target framework
      those packages need (net8/9/10) and the matching `targetAbi`.
      **Found:** `12.2.0` exists on NuGet; its nuspec dependency group is `net10.0`.
      → `TargetFramework net10.0`, `build.yaml framework: net10.0`, `targetAbi: 12.2.0.0`.
- [x] Check in the package (not from memory) how a playlist is created and filled:
      `IPlaylistManager.CreatePlaylist(PlaylistCreationRequest)` (fields: `Name`,
      `ItemIdList`, `UserId`, `MediaType`), and whether `AddItemToPlaylistAsync` or a
      "replace items" call exists for re-running into the same playlist.
      **Found** (reflected on the real 12.2.0 DLLs): `PlaylistCreationRequest` has
      `Name`, `ItemIdList`, `MediaType`, `UserId`, `Users`, `Public`. `IPlaylistManager`
      also has `Task UpdatePlaylist(PlaylistUpdateRequest)`, where
      `PlaylistUpdateRequest { Id, UserId, Name, Ids, Users, Public }` — `Ids` fully
      replaces the playlist's items. So re-running A→B *can* update the same playlist
      instead of piling up copies (see Open questions below — now answered).
      `AddItemToPlaylistAsync(playlistId, itemIds, position?, userId)` and
      `RemoveItemFromPlaylistAsync(playlistId, entryIds)` exist too, but `UpdatePlaylist`
      is the clean one-call replace.
- [x] Check how a plugin endpoint gets the calling user (claims → `IUserManager`) and
      which authorization policy a normal (non-admin) user passes.
      **Found:** third-party plugins use plain `[Authorize]` (no policy) —
      `Policies.DefaultAuthorization` lives in `Jellyfin.Api.Constants`, which is **not**
      published as a NuGet package, so plugins can't reference it. Confirmed against
      [intro-skipper](https://github.com/ConfusedPolarBear/intro-skipper)'s
      `SkipIntroController`, which just uses `[Authorize]`. Any authenticated user
      (admin or not) passes it.
      For the user id/name: inject `MediaBrowser.Controller.Net.IAuthorizationContext`
      and call `GetAuthorizationInfo(HttpContext)` → `AuthorizationInfo.UserId` and
      `.User` (the full `User` entity, so `.Username` is free). This is the public,
      documented route. The raw claim (`ClaimTypes` constant `"Jellyfin-UserId"`, read
      from Jellyfin's own `Jellyfin.Api/Constants/InternalClaimTypes.cs`) is an
      internal, unpublished string — avoid depending on it directly.
      To go from a `Guid` to a full `User` elsewhere, `IUserManager.GetUserById(Guid)`.
- [ ] Docker: the Jellyfin container sees the socket. Mount `neighbeats/run` to
      `/run/neighbeats` and share the PGID, then
      `docker exec jellyfin curl --unix-socket /run/neighbeats/neighbeats.sock http://x/v1/health`.
      If the image has no curl, check from a throwaway container with the same mount and user.
      **Vincent to run** — this needs the live server/container.

### B1: skeleton that loads

- [x] Repo from the template. Rename the project, namespace and `pluginName` in
      `.vscode/settings.json`. Generate a fresh GUID (`uuidgen`) for `Plugin.Id` and
      `build.yaml` `guid`. Set `build.yaml` `name`, `targetAbi`, `framework`, `owner`
      and `category: General`.
      No `uuidgen` on this machine; used `python3 -c 'import uuid; print(uuid.uuid4())'`.
- [x] `PackageReference` with `<ExcludeAssets>runtime</ExcludeAssets>` on both Jellyfin
      packages. Without it the plugin does not register.
- [x] `Plugin : BasePlugin<PluginConfiguration>, IHasWebPages`, with the constructor
      `(IApplicationPaths, IXmlSerializer)` and `Name` "neighbeats".
- [x] `PluginConfiguration : BasePluginConfiguration` holds `SocketPath` (default
      `/run/neighbeats/neighbeats.sock`), `DefaultLength` (15), `DefaultMode` (comfort),
      `PlaylistPrefix` ("neighbeats: ") and `TimeoutSeconds` (30).
- [x] Dashboard config page (embedded HTML + vanilla JS; jQuery is on its way out)
      with the settings above and a **Test connection** button. The button calls the
      plugin's health endpoint and shows version and track count.
- [ ] Exit: the DLL is copied to `<jellyfin data>/plugins/neighbeats/`, the plugin
      shows **Active** after a restart, and the config page saves.
      **Vincent to run** — needs the live server. See the handoff commands in
      ROADMAP_1_foundation.md.

### B2: talk to serve

- [x] `NeighbeatsClient`, registered through `IPluginServiceRegistrator`
      (`AddHttpClient<NeighbeatsClient>().ConfigurePrimaryHttpMessageHandler(...)`), reads
      the socket path from the config on each connect, so a change needs no restart.
- [x] Records that mirror api.md: `TrackRef`, `TrackOut`, `PathRequest`, `PathResponse`,
      `SimilarRequest`, `SimilarResponse`, `HealthResponse`, using snake_case JSON
      (`JsonNamingPolicy.SnakeCaseLower`). One file per record (StyleCop forbids more
      than one type per file), not a single `Models.cs`.
- [x] Error mapping: socket missing or refused → "neighbeats is not running"; 404 →
      neighbeats' message (unknown or unanalysed track); 422 → "no route", with `why`;
      timeout → its own message. Never a bare 500.
- [x] Unit tests (xUnit) against a fake server on a temp Unix socket
      (`Kestrel ListenUnixSocket`): happy path, 404, 422, null item id. 7/7 passing.

### B3: build the playlist

- [x] `PlaylistBuilder`: takes the answer, resolves null ids via `FindByPath`, drops
      what still fails, and calls `CreatePlaylist` with `UserId` = caller,
      `MediaType` = Audio and name `prefix + "A → B"` (similar: `prefix + "like A"`).
      It returns the playlist id, the track count and the skipped tracks. Also: if a
      playlist of that name already exists for the caller, `UpdatePlaylist` replaces its
      items instead of creating a duplicate (see Open questions, now answered).
- [x] API controller `[Route("Neighbeats")]`, authenticated as a normal user:
      - `GET  Neighbeats/Health` passes the health answer through (used by the config page).
      - `POST Neighbeats/Path` `{fromItemId, toItemId, length?, mode?, theme?}`. The plugin
        adds the paths and the user name (for taste) itself, then answers
        `{playlistId, tracks, skipped, notes}`.
      - `POST Neighbeats/Similar` `{itemId, count?}`, same answer.
      4 more unit tests cover `PlaylistBuilder`'s resolve/skip/update-vs-create logic
      (`Moq` against `ILibraryManager`/`IPlaylistManager`). **11/11 tests passing**,
      `dotnet build -c Release`: 0 warnings, 0 errors.
- [ ] Exit: with curl and a user token, `POST Neighbeats/Path` with two item ids creates
      a playlist that shows up and plays in the web client and in one mobile client.
      **Vincent to run** — needs the live server. See the handoff commands in
      ROADMAP_1_foundation.md.

### B4: a way to click it

Jellyfin has no supported hook for adding buttons to the item page, so the bootstrap
ships the smallest UI that works and leaves the rest for later:

- [ ] A user-facing plugin page with two track pickers (search through the regular
      `/Items?searchTerm=&IncludeItemTypes=Audio` API), length, mode and theme, plus a
      **Build** button that links to the new playlist.
- [ ] Later, not in bootstrap: context-menu entries through a client-side script
      injection plugin, or an "Instant Mix"-style integration if the server exposes one.
      Investigate both after B3 works.

### B5: release

- [ ] GitHub Actions from the template (build + `jprm` packaging). Tagging produces the
      zip, its checksum and a `manifest.json` entry (`guid`, `name`, `description`,
      `owner`, `overview`, `category`, `versions[]` with `version`, `targetAbi`,
      `sourceUrl`, `checksum`, `timestamp`, `changelog`).
- [ ] Host the manifest (GitHub Pages or a raw file in the repo). Install by adding it
      under Dashboard → Plugins → Repositories.
- [ ] neighbeats side: link the plugin from README and [operations.md](../operations.md),
      and replace the plugin line in [later.md](later.md).

## Exit criteria

- A non-admin user builds a 15-track path between two songs from the plugin page, and
  it appears as their own playlist and plays.
- With neighbeats stopped, the page says so in plain words, and Jellyfin keeps running.
- A track added after the last `sync-jellyfin` still lands in the playlist (path fallback).

## Open questions

- ~~Does the server's `IPlaylistManager` allow replacing a playlist's items?~~ **Answered
  (B0):** yes, `UpdatePlaylist(PlaylistUpdateRequest { Id, Ids, ... })`. B3 will look up an
  existing playlist by name + owner first and update it in place, falling back to
  `CreatePlaylist` when none exists.
- Taste is keyed by Jellyfin user **name**. Is passing `User.Username` enough, or should
  neighbeats also accept the user id?
- A request takes about 1 s on the NAS. Does B3 need to be async with polling? Probably
  not. Measure first.
- Live replanning after a skip ([later.md](later.md)) would need a session or playback
  event hook in the plugin. That is out of bootstrap scope but worth keeping in mind
  when shaping the controller.
