using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Topaz.UrlShortener.Application.Interfaces;
using Topaz.UrlShortener.Application.Results;
using Topaz.UrlShortener.Application.Services;
using Topaz.UrlShortener.Domain;
using Topaz.UrlShortener.Infrastructure.Queue;
using Topaz.UrlShortener.Infrastructure.Storage;

namespace Topaz.UrlShortener.Tests;

[TestFixture]
public sealed class LinkCreationWorkerTests
{
    [Test]
    public async Task Worker_DeveProcessarNaOrdemDeEntradaNoCanal()
    {
        // Arrange
        var queue = new LinkCreationQueue();
        var store = new RecordingLinkStore();
        var generator = new FixedShortCodeGenerator();

        var service = new LinkShorteningService(store, generator);

        using var worker = new LinkCreationWorker(
            queue,
            service,
            NullLogger<LinkCreationWorker>.Instance);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));

        // Enfileiramos antes de iniciar o consumidor.
        // Há espaço no canal para os três itens.
        var first = queue.EnqueueAsync(
            "https://exemplo.com/primeiro",
            "primeiro",
            timeout.Token);

        var second = queue.EnqueueAsync(
            "https://exemplo.com/segundo",
            "segundo",
            timeout.Token);

        var third = queue.EnqueueAsync(
            "https://exemplo.com/terceiro",
            "terceiro",
            timeout.Token);

        try
        {
            // Act
            await worker.StartAsync(timeout.Token);

            var results = await Task.WhenAll(
                first,
                second,
                third).WaitAsync(timeout.Token);

            // Assert
            foreach (var result in results)
            {
                Assert.That(
                    result.Status,
                    Is.EqualTo(CreateLinkStatus.Success));
            }

            Assert.That(
                store.SavedCodes,
                Is.EqualTo(new[]
                {
                    "primeiro",
                    "segundo",
                    "terceiro"
                }));
        }
        finally
        {
            // Encerra o worker mesmo se alguma verificação falhar.
            using var stopTimeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(5));

            await worker.StopAsync(stopTimeout.Token);
        }
    }

    [Test]
    public async Task Worker_QuandoUmItemFalhar_DeveProcessarOProximo()
    {
        // Arrange
        var queue = new LinkCreationQueue();
        var store = new InMemoryLinkStore();
        var generator = new FailOnceShortCodeGenerator();

        var service = new LinkShorteningService(store, generator);

        using var worker = new LinkCreationWorker(
            queue,
            service,
            NullLogger<LinkCreationWorker>.Instance);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));

        // Sem alias, as duas solicitações precisam usar o gerador.
        var first = queue.EnqueueAsync(
            "https://exemplo.com/primeiro",
            null,
            timeout.Token);

        var second = queue.EnqueueAsync(
            "https://exemplo.com/segundo",
            null,
            timeout.Token);

        try
        {
            // Act
            await worker.StartAsync(timeout.Token);

            // Assert: a primeira solicitação recebe a exceção.
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await first.WaitAsync(timeout.Token);
            });

            // A segunda deve concluir normalmente.
            var result = await second.WaitAsync(timeout.Token);

            Assert.That(
                result.Status,
                Is.EqualTo(CreateLinkStatus.Success));

            Assert.That(result.Link, Is.Not.Null);
            Assert.That(result.Link!.Code, Is.EqualTo("novo1234"));

            Assert.That(
                result.Link!.OriginalUrl,
                Is.EqualTo("https://exemplo.com/segundo"));

            Assert.That(store.Find("novo1234"), Is.EqualTo(result.Link));
            Assert.That(generator.Calls, Is.EqualTo(2));
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(5));

            await worker.StopAsync(stopTimeout.Token);
        }
    }

    [Test]
    public async Task Worker_DeveExecutarUmaGeracaoPorVez()
    {
        // Arrange
        var queue = new LinkCreationQueue();
        var store = new InMemoryLinkStore();

        using var generator = new BlockingShortCodeGenerator();

        var service = new LinkShorteningService(store, generator);

        using var worker = new LinkCreationWorker(
            queue,
            service,
            NullLogger<LinkCreationWorker>.Instance);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));

        var first = queue.EnqueueAsync(
            "https://exemplo.com/primeiro",
            null,
            timeout.Token);

        var second = queue.EnqueueAsync(
            "https://exemplo.com/segundo",
            null,
            timeout.Token);

        // No .NET 8, StartAsync pode executar código do worker
        // antes de retornar. Usamos outra thread somente no teste
        // para conseguir liberar o gerador bloqueado.
        var startTask = Task.Run(
            () => worker.StartAsync(CancellationToken.None));

        try
        {
            // Act: aguarda a primeira geração começar.
            await generator.FirstStarted.WaitAsync(timeout.Token);

            // Enquanto a primeira está bloqueada,
            // a segunda não deve ter sido processada.
            Assert.That(generator.Calls, Is.EqualTo(1));
            Assert.That(first.IsCompleted, Is.False);
            Assert.That(second.IsCompleted, Is.False);

            generator.ReleaseFirst();

            await startTask.WaitAsync(timeout.Token);

            var results = await Task.WhenAll(
                first,
                second).WaitAsync(timeout.Token);

            // Assert
            foreach (var result in results)
            {
                Assert.That(
                    result.Status,
                    Is.EqualTo(CreateLinkStatus.Success));
            }

            Assert.That(generator.Calls, Is.EqualTo(2));
            Assert.That(generator.MaxConcurrent, Is.EqualTo(1));
        }
        finally
        {
            // Libera a geração mesmo se uma verificação falhar.
            generator.ReleaseFirst();

            await startTask.WaitAsync(TimeSpan.FromSeconds(5));

            using var stopTimeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(5));

            await worker.StopAsync(stopTimeout.Token);
        }
    }

    private sealed class BlockingShortCodeGenerator
    : IShortCodeGenerator, IDisposable
    {
        private readonly ManualResetEventSlim _releaseFirst = new(false);

        private readonly TaskCompletionSource<bool> _firstStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _calls;
        private int _active;
        private int _maxConcurrent;

        public Task FirstStarted => _firstStarted.Task;

        public int Calls => Volatile.Read(ref _calls);

        public int MaxConcurrent => Volatile.Read(ref _maxConcurrent);

        public void ReleaseFirst()
        {
            _releaseFirst.Set();
        }

        public string Generate()
        {
            var active = Interlocked.Increment(ref _active);

            try
            {
                UpdateMaxConcurrent(active);

                var call = Interlocked.Increment(ref _calls);

                if (call == 1)
                {
                    _firstStarted.TrySetResult(true);

                    if (!_releaseFirst.Wait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException(
                            "A primeira geração não foi liberada pelo teste.");
                    }
                }

                return $"cod{call:D5}";
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private void UpdateMaxConcurrent(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maxConcurrent);

                if (active <= current)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref _maxConcurrent,
                        active,
                        current) == current)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            _releaseFirst.Dispose();
        }
    }
    private sealed class FailOnceShortCodeGenerator : IShortCodeGenerator
    {
        public int Calls { get; private set; }

        public string Generate()
        {
            Calls++;

            if (Calls == 1)
            {
                throw new InvalidOperationException(
                    "Falha simulada no gerador.");
            }

            return "novo1234";
        }
    }
    private sealed class RecordingLinkStore : ILinkStore
    {
        private readonly InMemoryLinkStore _inner = new();
        private readonly ConcurrentQueue<string> _savedCodes = new();

        public string[] SavedCodes => _savedCodes.ToArray();

        public ShortenedLink? Find(string code)
        {
            return _inner.Find(code);
        }

        public bool TryAdd(ShortenedLink link)
        {
            var added = _inner.TryAdd(link);

            if (added)
            {
                _savedCodes.Enqueue(link.Code);
            }

            return added;
        }
    }

    private sealed class FixedShortCodeGenerator : IShortCodeGenerator
    {
        public string Generate()
        {
            return "abc12345";
        }
    }
}