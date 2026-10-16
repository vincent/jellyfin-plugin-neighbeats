# jellyfin-plugin-neighbeats

A Jellyfin plugin for [neighbeats](https://github.com/vincent/neighbeats): pick two
tracks (or one, for "similar") from inside Jellyfin and get a real Jellyfin playlist,
built by neighbeats, owned by the user who asked.

neighbeats never writes to Jellyfin — this plugin owns the playlist. See
[neighbeats' API spec](https://github.com/vincent/neighbeats/blob/main/docs/api.md)
for the protocol this plugin speaks.

## Status

Bootstrap in progress — see [J-jellyfin-plugin.md](J-jellyfin-plugin.md) and
[ROADMAP_1_foundation.md](ROADMAP_1_foundation.md).

## Install (once released)

Dashboard → Plugins → Repositories → add this plugin's manifest URL, then install
"neighbeats" from the catalog.

## Configure

Dashboard → Plugins → neighbeats:

- **Socket path** — where this Jellyfin server can reach `neighbeats serve`'s Unix
  socket (default `/run/neighbeats/neighbeats.sock`). If Jellyfin runs in Docker, mount
  neighbeats' `run/` folder into the container at this path, and give both containers
  the same group (PGID) so Jellyfin can open the socket (mode `0660`).
- **Default playlist length**, **default mode**, **playlist name prefix**, **timeout**.
- **Test connection** calls neighbeats' health endpoint and shows its version and track
  count, or says plainly that neighbeats isn't reachable.
