namespace Jellyfin.Plugin.Neighbeats.Api.Controllers;

/// <summary>
/// This plugin's own error body, returned whenever neighbeats could not be reached or
/// answered with a failure. Never a bare 500.
/// </summary>
/// <param name="Error">A plain-words message.</param>
/// <param name="Why">Where a route search broke, present only for a no-route failure.</param>
public record ApiErrorBody(string Error, string? Why);
