namespace Topaz.UrlShortener.Application.Results;

public enum CreateLinkStatus
{
    Success,
    InvalidInput,
    AliasConflict,
    GenerationFailed
}