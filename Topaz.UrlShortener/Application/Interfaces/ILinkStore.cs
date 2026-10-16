using Topaz.UrlShortener.Domain;

namespace Topaz.UrlShortener.Application.Interfaces;

public interface ILinkStore
{
    ShortenedLink? Find(string code);

    bool TryAdd(ShortenedLink link);
}