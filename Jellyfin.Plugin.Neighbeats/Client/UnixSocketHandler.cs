using System.Net.Sockets;

namespace Jellyfin.Plugin.Neighbeats.Client;

/// <summary>
/// Builds an <see cref="HttpMessageHandler"/> that connects over a Unix domain socket
/// instead of TCP. The socket path is read on every connection attempt, so a change to
/// the plugin configuration takes effect without a restart.
/// </summary>
public static class UnixSocketHandler
{
    /// <summary>
    /// Creates a new <see cref="SocketsHttpHandler"/> wired to connect over a Unix socket.
    /// </summary>
    /// <param name="socketPathProvider">Returns the Unix socket path to connect to, read fresh on every connection.</param>
    /// <returns>The configured handler.</returns>
    public static SocketsHttpHandler Create(Func<string> socketPathProvider)
    {
        return new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPathProvider()), cancellationToken)
                        .ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
    }
}
