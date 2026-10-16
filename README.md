# jellyfin-plugin-neighbeats

A Jellyfin plugin for [neighbeats](https://github.com/vincent/neighbeats): pick two
tracks (or one, for "similar") from inside Jellyfin and get a real Jellyfin playlist,
built by neighbeats, owned by the user who asked.

neighbeats never writes to Jellyfin — this plugin owns the playlist. See
[neighbeats' API spec](https://github.com/vincent/neighbeats/blob/main/docs/api.md)
for the protocol this plugin speaks.

## Status

Bootstrap in progress — see [ROADMAP.md](ROADMAP.md).

## Install

Dashboard → Plugins → Repositories → add
`https://raw.githubusercontent.com/vincent/jellyfin-plugin-neighbeats/main/manifest.json`,
then install "Neighbeats" from the catalog.

## Configure

Dashboard → Plugins → Neighbeats:

- **Socket path** — where this Jellyfin server can reach `neighbeats serve`'s Unix
  socket (default `/run/neighbeats/neighbeats.sock`). If Jellyfin runs in Docker, mount
  neighbeats' `run/` folder into the container at this path, and give both containers
  the same group (PGID) so Jellyfin can open the socket (mode `0660`).
- **Default playlist length**, **default mode**, **playlist name prefix**, **timeout**.
- **Test connection** calls neighbeats' health endpoint and shows its version and track
  count, or says plainly that neighbeats isn't reachable.

## Build a playlist

Any logged-in user (not just admins) can open
`<server>/web/ConfigurationPage?name=neighbeats-build` directly — it doesn't appear in
the admin Dashboard's own menu, since that menu is admin-only anyway. Pick two tracks
(or one, for similar), fill in the options, and **Build playlist** creates or updates a
playlist owned by that user and links straight to it.

## Releasing

Tag a release (`vX.Y.Z.W`, matching Jellyfin's 4-part `AssemblyVersion`) and publish it
on GitHub. `.github/workflows/publish.yaml` then builds the plugin zip with `jprm`,
attaches it to that release, and commits the new version into `manifest.json` at the
repo root — which is exactly the file the Install step above points at.
