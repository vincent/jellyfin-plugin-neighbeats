using Jellyfin.Plugin.Neighbeats.Api;

namespace Jellyfin.Plugin.Neighbeats.Client;

/// <summary>
/// A failure talking to neighbeats, carrying enough detail for the controller to
/// answer with a plain-words message instead of a bare 500.
/// </summary>
public class NeighbeatsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NeighbeatsException"/> class.
    /// </summary>
    /// <param name="kind">The kind of failure.</param>
    /// <param name="message">A plain-words message for the caller.</param>
    /// <param name="why">Where the search broke, for a <see cref="NeighbeatsErrorKind.NoRoute"/> failure.</param>
    /// <param name="partialTracks">The tracks found before the search broke, if any.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public NeighbeatsException(
        NeighbeatsErrorKind kind,
        string message,
        string? why = null,
        IReadOnlyList<TrackOut>? partialTracks = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        Why = why;
        PartialTracks = partialTracks;
    }

    /// <summary>
    /// Gets the kind of failure.
    /// </summary>
    public NeighbeatsErrorKind Kind { get; }

    /// <summary>
    /// Gets where the search broke, for a <see cref="NeighbeatsErrorKind.NoRoute"/> failure.
    /// </summary>
    public string? Why { get; }

    /// <summary>
    /// Gets the tracks found before the search broke, if any.
    /// </summary>
    public IReadOnlyList<TrackOut>? PartialTracks { get; }
}
