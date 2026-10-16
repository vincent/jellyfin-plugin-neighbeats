namespace Jellyfin.Plugin.Neighbeats.Client;

/// <summary>
/// The kind of failure a <see cref="NeighbeatsException"/> carries, used to map it to
/// an HTTP status and a plain-words message for the caller.
/// </summary>
public enum NeighbeatsErrorKind
{
    /// <summary>neighbeats' socket is missing, refused the connection, or the socket itself is not running.</summary>
    NotRunning,

    /// <summary>neighbeats rejected the request as malformed (400).</summary>
    BadRequest,

    /// <summary>The track is unknown to neighbeats or has not been analysed (404).</summary>
    NotFound,

    /// <summary>No route exists between the two tracks (422).</summary>
    NoRoute,

    /// <summary>neighbeats did not answer within the configured timeout.</summary>
    Timeout,

    /// <summary>neighbeats answered with an unexpected error (500 or anything else unmapped).</summary>
    ServerError,
}
