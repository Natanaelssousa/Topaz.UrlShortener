namespace Topaz.UrlShortener.DTOs;

public sealed record CreateLinkResponse(
    string Code,
    string ShortUrl,
    string OriginalUrl);