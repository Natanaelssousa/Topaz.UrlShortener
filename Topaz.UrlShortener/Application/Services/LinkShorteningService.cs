using Topaz.UrlShortener.Application.Interfaces;
using Topaz.UrlShortener.Application.Results;
using Topaz.UrlShortener.Domain;

namespace Topaz.UrlShortener.Application.Services;

public sealed class LinkShorteningService
{
    private const int MaxGenerationAttempts = 10;

    private readonly ILinkStore _store;
    private readonly IShortCodeGenerator _generator;

    public LinkShorteningService(
        ILinkStore store,
        IShortCodeGenerator generator)
    {
        _store = store;
        _generator = generator;
    }

    public CreateLinkResult Create(string? originalUrl, string? alias)
    {
        var normalizedUrl = originalUrl?.Trim();

        if (!IsValidUrl(normalizedUrl))
        {
            return new CreateLinkResult(
                CreateLinkStatus.InvalidInput,
                Error: "Informe uma URL absoluta com HTTP ou HTTPS.");
        }

        var normalizedAlias = alias?.Trim();

        if (!string.IsNullOrEmpty(normalizedAlias))
        {
            if (!IsValidAlias(normalizedAlias))
            {
                return new CreateLinkResult(
                    CreateLinkStatus.InvalidInput,
                    Error: "O alias deve ter entre 3 e 30 caracteres, " +
                           "usando apenas letras, números, hífen ou sublinhado.");
            }

            var customLink = new ShortenedLink(
                normalizedAlias,
                normalizedUrl!);

            if (!_store.TryAdd(customLink))
            {
                return new CreateLinkResult(
                    CreateLinkStatus.AliasConflict,
                    Error: "Esse alias já está em uso.");
            }

            return new CreateLinkResult(
                CreateLinkStatus.Success,
                Link: customLink);
        }

        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var code = _generator.Generate();

            var link = new ShortenedLink(code, normalizedUrl!);

            if (_store.TryAdd(link))
            {
                return new CreateLinkResult(
                    CreateLinkStatus.Success,
                    Link: link);
            }
        }

        return new CreateLinkResult(
            CreateLinkStatus.GenerationFailed,
            Error: "Não foi possível gerar um código disponível. Tente novamente.");
    }

    private static bool IsValidUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo);
    }

    private static bool IsValidAlias(string alias)
    {
        if (alias.Length is < 3 or > 30)
        {
            return false;
        }

        foreach (var character in alias)
        {
            var isAllowed =
                character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '-' or '_';

            if (!isAllowed)
            {
                return false;
            }
        }

        return true;
    }
}