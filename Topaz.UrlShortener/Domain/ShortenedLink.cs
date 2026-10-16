namespace Topaz.UrlShortener.Domain;

public sealed record ShortenedLink(
    string Code,
    string OriginalUrl);