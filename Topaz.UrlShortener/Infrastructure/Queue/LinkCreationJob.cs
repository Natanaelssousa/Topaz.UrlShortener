using Topaz.UrlShortener.Application.Results;

namespace Topaz.UrlShortener.Infrastructure.Queue;

public sealed class LinkCreationJob
{
    public string? OriginalUrl { get; }
    public string? Alias { get; }

    private readonly TaskCompletionSource<CreateLinkResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<CreateLinkResult> Completion => _completion.Task;

    public LinkCreationJob(string? originalUrl, string? alias)
    {
        OriginalUrl = originalUrl;
        Alias = alias;
    }

    public void Complete(CreateLinkResult result)
    {
        _completion.TrySetResult(result);
    }

    public void Fail(Exception exception)
    {
        _completion.TrySetException(exception);
    }

    public void Cancel()
    {
        _completion.TrySetCanceled();
    }
}