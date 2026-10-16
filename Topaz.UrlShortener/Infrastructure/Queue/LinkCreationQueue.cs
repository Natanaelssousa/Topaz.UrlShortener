using System.Threading.Channels;
using Topaz.UrlShortener.Application.Results;

namespace Topaz.UrlShortener.Infrastructure.Queue;

public sealed class LinkCreationQueue
{
    private readonly Channel<LinkCreationJob> _channel =
        Channel.CreateBounded<LinkCreationJob>(
            new BoundedChannelOptions(100)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

    public async Task<CreateLinkResult> EnqueueAsync(
        string? originalUrl,
        string? alias,
        CancellationToken cancellationToken = default)
    {
        var job = new LinkCreationJob(originalUrl, alias);

        await _channel.Writer.WriteAsync(job, cancellationToken);

        return await job.Completion.WaitAsync(cancellationToken);
    }

    public IAsyncEnumerable<LinkCreationJob> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public void StopAccepting()
    {
        _channel.Writer.TryComplete();
    }

    public void CancelPending()
    {
        while (_channel.Reader.TryRead(out var job))
        {
            job.Cancel();
        }
    }
}