using Topaz.UrlShortener.Domain;

namespace Topaz.UrlShortener.Application.Results;

public sealed record CreateLinkResult(
    CreateLinkStatus Status,
    ShortenedLink? Link = null,
    string? Error = null);