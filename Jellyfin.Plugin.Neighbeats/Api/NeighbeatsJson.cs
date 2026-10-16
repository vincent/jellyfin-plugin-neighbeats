using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Neighbeats.Api;

/// <summary>
/// The <see cref="JsonSerializerOptions"/> shared by every call to neighbeats' API,
/// matching its snake_case wire format (see api.md).
/// </summary>
public static class NeighbeatsJson
{
    /// <summary>
    /// Gets the shared serializer options: snake_case property names, nulls omitted
    /// on write, case-insensitive on read.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };
}
