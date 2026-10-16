using System.Collections.Concurrent;
using Topaz.UrlShortener.Application.Interfaces;
using Topaz.UrlShortener.Domain;

namespace Topaz.UrlShortener.Infrastructure.Storage;

public sealed class InMemoryLinkStore : ILinkStore
{
    private readonly ConcurrentDictionary<string, ShortenedLink> _links =
        new(StringComparer.OrdinalIgnoreCase);

    public ShortenedLink? Find(string code)
    {
        return _links.TryGetValue(code, out var link)
            ? link
            : null;
    }

    public bool TryAdd(ShortenedLink link)
    {
        return _links.TryAdd(link.Code, link);
    }
}