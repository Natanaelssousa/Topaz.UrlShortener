namespace Topaz.UrlShortener.DTOs;

public sealed record CreateLinkRequest(
    string OriginalUrl,
    string? Alias);