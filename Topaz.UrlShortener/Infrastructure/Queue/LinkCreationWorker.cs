using Topaz.UrlShortener.Application.Services;

namespace Topaz.UrlShortener.Infrastructure.Queue;

public sealed class LinkCreationWorker : BackgroundService
{
    private readonly LinkCreationQueue _queue;
    private readonly LinkShorteningService _service;
    private readonly ILogger<LinkCreationWorker> _logger;

    public LinkCreationWorker(
        LinkCreationQueue queue,
        LinkShorteningService service,
        ILogger<LinkCreationWorker> logger)
    {
        _queue = queue;
        _service = service;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    var result = _service.Create(
                        job.OriginalUrl,
                        job.Alias);

                    job.Complete(result);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Erro inesperado ao criar link encurtado.");

                    job.Fail(exception);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento solicitado pela aplicação.
        }
        finally
        {
            _queue.StopAccepting();
            _queue.CancelPending();
        }
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        _queue.StopAccepting();

        await base.StopAsync(cancellationToken);
    }
}