# Roadmap 1: foundation (B0–B3)

Implementation plan for B0–B3 of [J-jellyfin-plugin.md](J-jellyfin-plugin.md). B4 (user
page) and B5 (release) are out of scope here.

## Context

neighbeats `serve` (neighbeats commit `dfc3872`) answers `GET /v1/health`, `POST /v1/path`
and `POST /v1/similar` over a Unix socket. Spec: `~/Workspace/Perso/neighbeats/docs/api.md`.

Goal: a plugin that loads on Jellyfin 12.2.0, talks to the socket, and lets an
authenticated (non-admin) user build a real playlist through `POST Neighbeats/Path` and
`POST Neighbeats/Similar`.

Working split: Vincent runs every deploy, restart and docker check on the server. Claude
builds, tests locally and writes out the exact commands to run.

Starting state: the repo has only `LICENSE` (GPLv3) and the roadmap, branch `main`, one commit.

## Already verified (2026-10-16)

- NuGet `Jellyfin.Controller` / `Jellyfin.Model` **12.2.0** exist, and the nuspec dependency
  group is **net10.0**. So: `TargetFramework net10.0`, `build.yaml framework: net10.0`,
  `targetAbi: 12.2.0.0`.
- Local SDK 10.0.203 is installed. `uuidgen`, `jprm` and `gh` are missing. Make GUIDs with
  `python3 -c 'import uuid; print(uuid.uuid4())'`. jprm is not needed until B5.
- Reference plugin [Eeeeelias/playlist-generator](https://github.com/Eeeeelias/playlist-generator)
  (GPLv3, targets 12.1, pushed 2026-10-03) shows the 12.x API:
  - `PlaylistCreationRequest { Name, ItemIdList, MediaType = Jellyfin.Data.Enums.MediaType.Audio, UserId }`
  - `User` is in `Jellyfin.Database.Implementations.Entities`
  - delete through `ILibraryManager.DeleteItem(playlist, new DeleteOptions { DeleteFileLocation = true })`
  - It depends on the Playback Reporting plugin. We **don't**: neighbeats provides taste.

## Approach

Take the structure from the **official template** (Directory.Build.props, .slnx, analyzers,
.editorconfig, .vscode, build.yaml, workflows), fetched as raw files with no global
`dotnet new install`. Take the 12.x API details from playlist-generator. The template's
workflows are copied in but left alone until B5.

## B0: verify — done (2026-10-16), except the Docker check

- [x] Reflected on the real 12.2.0 DLLs (`~/.nuget/packages/jellyfin.{controller,model}/12.2.0`).
      Findings:
      - `IPlaylistManager.CreatePlaylist(PlaylistCreationRequest { Name, ItemIdList,
        MediaType, UserId, Users, Public })` → `PlaylistCreationResult.Id`.
      - `IPlaylistManager.UpdatePlaylist(PlaylistUpdateRequest { Id, UserId, Name, Ids,
        Users, Public })` — `Ids` **fully replaces** a playlist's items. A re-run of the
        same A→B can update the existing playlist instead of creating a new one.
        `AddItemToPlaylistAsync` / `RemoveItemFromPlaylistAsync` also exist, but
        `UpdatePlaylist` is the one-call replace B3 should use.
      - `ILibraryManager.FindByPath(string path, bool? isFolder)` and
        `GetItemById<T>(Guid id)` both exist and need no `User` argument.
      - `IAuthorizationContext.GetAuthorizationInfo(HttpContext)` →
        `Task<AuthorizationInfo>` with public `UserId` and `User` (full entity) properties.
      - No `Policies` class is exposed via NuGet (`Jellyfin.Api.Constants.Policies` lives
        in the main server, not a package). Searching `MediaBrowser.Controller`/`.Model`
        for policy-shaped types turned up nothing usable.
      - `IUserManager.GetUserById(Guid)` resolves a `Guid` to a full `User`.
- [x] Calling-user plan, confirmed against a real published plugin: third-party
      controllers use plain `[Authorize]` (no policy arg) —
      [intro-skipper](https://github.com/ConfusedPolarBear/intro-skipper)'s
      `SkipIntroController` does exactly this, and it passes for any authenticated user,
      admin or not. Inside the action, inject `IAuthorizationContext` and call
      `GetAuthorizationInfo(HttpContext)` for `.UserId` and `.User.Username` — this is the
      public, documented route. The raw `"Jellyfin-UserId"` claim (from Jellyfin's own
      unpublished `Jellyfin.Api/Constants/InternalClaimTypes.cs`) is an internal string;
      don't depend on it directly. No fallback claim-parsing needed.
- [x] Findings written into the B0 boxes and open questions of J-jellyfin-plugin.md.
- [ ] Docker socket check (**Vincent runs it**):
      `docker exec jellyfin curl --unix-socket /run/neighbeats/neighbeats.sock http://x/v1/health`.

## B1: skeleton

Namespace `Jellyfin.Plugin.Neighbeats`.

- [x] Repo files:
      - `Jellyfin.Plugin.Neighbeats.slnx`
      - `Directory.Build.props` (version 0.1.0.0)
      - `.editorconfig`, `.gitignore`
      - `.vscode/*` with pluginName `Jellyfin.Plugin.Neighbeats`
      - `build.yaml`: name `neighbeats`, fresh guid, owner `vincent`, category General,
        net10.0, targetAbi 12.2.0.0, artifact `Jellyfin.Plugin.Neighbeats.dll`
      - `README.md`, short: what it does, install, socket and PGID
- [x] `Jellyfin.Plugin.Neighbeats/Jellyfin.Plugin.Neighbeats.csproj`: net10.0, both Jellyfin
      packages at 12.2.0 with `<ExcludeAssets>runtime</ExcludeAssets>`, the template's
      analyzers, `TreatWarningsAsErrors`, `Configuration/configPage.html` embedded.
- [x] `Plugin.cs`: `BasePlugin<PluginConfiguration>, IHasWebPages`, constructor
      `(IApplicationPaths, IXmlSerializer)`, `Name => "neighbeats"`, static `Instance`.
- [x] `Configuration/PluginConfiguration.cs`: `SocketPath` (`/run/neighbeats/neighbeats.sock`),
      `DefaultLength = 15`, `DefaultMode = "comfort"`, `PlaylistPrefix = "neighbeats: "`,
      `TimeoutSeconds = 30`.
- [x] `Configuration/configPage.html`: vanilla JS (`ApiClient.getPluginConfiguration` /
      `updatePluginConfiguration`) with the fields above. A **Test connection** button calls
      `GET Neighbeats/Health` and shows the version and track count, or the error in plain words.
      Built `-c Release`: 0 warnings, 0 errors. Not yet deployed to a live server (that's on
      Vincent — see the handoff commands at the bottom of this file).

## B2: client — done

- [x] `Api/*.cs`: one file per record (`TrackRef`, `TrackOut`, `PathRequest`, `PathResponse`,
      `SimilarRequest`, `SimilarResponse`, `HealthResponse`, `ErrorResponse`) — StyleCop's
      SA1402/SA1649 forbid multiple types per file, so these aren't in one `Models.cs` as
      planned. They share `Api/NeighbeatsJson.cs`'s
      `JsonSerializerOptions { PropertyNamingPolicy = SnakeCaseLower, DefaultIgnoreCondition = WhenWritingNull, PropertyNameCaseInsensitive = true }`.
- [x] `Client/UnixSocketHandler.cs`: **not** a `SocketsHttpHandler` subclass — the type is
      sealed, so this is a static `Create(Func<string> socketPathProvider)` factory instead,
      whose `ConnectCallback` reads the provider on every connect.
- [x] `Client/NeighbeatsClient.cs`, `Client/NeighbeatsErrorKind.cs`,
      `Client/NeighbeatsException.cs`: typed `HttpClient` with `BaseAddress http://neighbeats`,
      methods `HealthAsync`, `PathAsync`, `SimilarAsync`. A linked CTS (caller token +
      per-call timeout from `Func<int> timeoutSecondsProvider`, defaulting to
      `Plugin.Instance.Configuration.TimeoutSeconds`) applies the timeout. Failures throw
      `NeighbeatsException(kind, message, why?, partialTracks?)`:

      | Kind | Cause | Message |
      |------|-------|---------|
      | NotRunning | `HttpRequestException` wrapping a `SocketException` | "neighbeats is not running" |
      | NotFound | 404 | neighbeats' `error` |
      | NoRoute | 422 | `error`, plus `why` and the partial `tracks` |
      | Timeout | `OperationCanceledException` not caused by the caller's token | its own message |
      | BadRequest / ServerError | 400 / 500 (anything else unmapped) | neighbeats' message, or a generic one if it sent no JSON body |
- [x] `PluginServiceRegistrator.cs`: `AddHttpClient<NeighbeatsClient>(...).ConfigurePrimaryHttpMessageHandler(() => UnixSocketHandler.Create(...))`.
      `PlaylistBuilder` registration deferred to B3.
- [x] `Jellyfin.Plugin.Neighbeats.Tests/` (xUnit 2.9.2, net10.0, `FrameworkReference Microsoft.AspNetCore.App`).
      `FakeNeighbeatsServer` is a minimal `WebApplication` on `ListenUnixSocket` with a temp
      path; `TestHelpers` builds a client against it. 7 tests, all passing:
      - health, happy path
      - path, happy path
      - 404 → `NotFound` with neighbeats' message
      - 422 → `NoRoute` with `Why` and `PartialTracks`
      - null `item_id` deserializes
      - missing socket → `NotRunning`
      - server too slow → `Timeout`

      The client tests should not touch Jellyfin types, so they need no Jellyfin runtime assets.

## B3: playlist

- [ ] `Playlists/PlaylistBuilder.cs` takes a list of `TrackOut` and a name. For each track:
      - parse `item_id` and look it up with `ILibraryManager.GetItemById`
      - if the id is null or unknown, try `FindByPath(path, false)`
      - if that fails too, add the track to `skipped` (artist – title, path)

      Then look up an existing playlist owned by the caller with the target name
      (`IPlaylistManager.GetPlaylists(userId)`); if found, call
      `UpdatePlaylist(new PlaylistUpdateRequest { Id, UserId, Ids = resolvedItemIds })` to
      replace its items; otherwise `CreatePlaylist` with the caller's `UserId` and
      `MediaType.Audio`. Return `{PlaylistId, Tracks, Skipped}`. Names: `prefix + "A → B"`
      for a path, `prefix + "like A"` for similar.
      - Keep id resolution separate from playlist creation, so the resolution logic can be
        unit-tested without the Jellyfin runtime.
- [ ] `Api/NeighbeatsController.cs`, `[ApiController, Route("Neighbeats"), Authorize]`:
      - `GET Health` passes the `HealthResponse` through.
      - `POST Path` `{fromItemId, toItemId, length?, mode?, theme?}`: look up both items
        (404 if unknown or not Audio), build `TrackRef{item_id, path}` for each, add
        `user = User.Username`, fill missing fields from the config defaults, call the client,
        then the builder. Answer: `{playlistId, tracks, skipped, notes}`.
      - `POST Similar` `{itemId, count?}`: same answer.
      - `NeighbeatsException` becomes JSON `{error, why?}` with status 503 (NotRunning),
        404, 422, 504 (Timeout) or 502 (anything else). Never a bare 500, and Jellyfin keeps running.
- [ ] Tick the boxes in J-jellyfin-plugin.md for what is really done. Write the B0 findings and
      the measured answers into its open questions.

## Verification

- [ ] `dotnet build -c Release` with no warnings, and `dotnet test` green against the fake
      Unix-socket server.
- [ ] Handoff commands for Vincent (Claude writes them out in full):
  1. Copy `bin/Release/net10.0/Jellyfin.Plugin.Neighbeats.dll` to `<data>/plugins/neighbeats/`
     and restart. The plugin shows **Active**, the config page saves, and Test connection
     shows the version and track count.
  2. Socket check: `docker exec jellyfin curl --unix-socket /run/neighbeats/neighbeats.sock http://x/v1/health`.
  3. With a non-admin user token:
     `curl -H 'Authorization: MediaBrowser Token="<token>"' -H 'Content-Type: application/json' -X POST <server>/Neighbeats/Path -d '{"fromItemId":"…","toItemId":"…"}'`.
     The playlist shows up and plays.
  4. Stop neighbeats-serve. Test connection and `Path` both say "neighbeats is not running",
     and Jellyfin keeps working.
- Commits: one per milestone (B0+B1, B2, B3) on `main`, each only after asking.
