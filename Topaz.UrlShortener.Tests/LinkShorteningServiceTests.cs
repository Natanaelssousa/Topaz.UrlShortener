using NUnit.Framework;
using Topaz.UrlShortener.Application.Interfaces;
using Topaz.UrlShortener.Application.Results;
using Topaz.UrlShortener.Application.Services;
using Topaz.UrlShortener.Infrastructure.Storage;
using Topaz.UrlShortener.Domain;

namespace Topaz.UrlShortener.Tests;

[TestFixture]
public sealed class LinkShorteningServiceTests
{
    [Test]
    public void Create_ComUrlValidaEAliasDisponivel_DeveCriarEArmazenarLink()
    {
        // Arrange: preparar as dependências e os dados.
        var store = new InMemoryLinkStore();
        var generator = new FixedShortCodeGenerator();

        var service = new LinkShorteningService(store, generator);

        const string originalUrl = "https://www.microsoft.com";
        const string alias = "microsoft";

        // Act: executar a operação.
        var result = service.Create(originalUrl, alias);

        // Assert: conferir o resultado e o armazenamento.
        Assert.That(result.Status, Is.EqualTo(CreateLinkStatus.Success));
        Assert.That(result.Link, Is.Not.Null);
        Assert.That(result.Link!.Code, Is.EqualTo(alias));
        Assert.That(result.Link!.OriginalUrl, Is.EqualTo(originalUrl));

        var storedLink = store.Find(alias);

        Assert.That(storedLink, Is.EqualTo(result.Link));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("isso-nao-e-uma-url")]
    [TestCase("ftp://exemplo.com/arquivo")]
    [TestCase("https://usuario:senha@exemplo.com")]
    public void Create_ComUrlInvalida_DeveRejeitarSemArmazenar(
    string? originalUrl)
    {
        // Arrange
        var store = new InMemoryLinkStore();
        var generator = new FixedShortCodeGenerator();
        var service = new LinkShorteningService(store, generator);

        // Act
        var result = service.Create(originalUrl, "teste");

        // Assert
        Assert.That(result.Status, Is.EqualTo(CreateLinkStatus.InvalidInput));
        Assert.That(result.Link, Is.Null);
        Assert.That(store.Find("teste"), Is.Null);
    }

    [TestCase("microsoft")]
    [TestCase("MICROSOFT")]
    public void Create_ComAliasJaUtilizado_DeveRejeitarSemAlterarLinkOriginal(
        string repeatedAlias)
    {
        // Arrange
        var store = new InMemoryLinkStore();
        var generator = new FixedShortCodeGenerator();
        var service = new LinkShorteningService(store, generator);

        var firstResult = service.Create(
            "https://www.microsoft.com",
            "microsoft");

        Assert.That(firstResult.Status, Is.EqualTo(CreateLinkStatus.Success));

        // Act
        var result = service.Create(
            "https://www.exemplo.com",
            repeatedAlias);

        // Assert
        Assert.That(result.Status, Is.EqualTo(CreateLinkStatus.AliasConflict));
        Assert.That(result.Link, Is.Null);

        var storedLink = store.Find("microsoft");

        Assert.That(storedLink, Is.Not.Null);
        Assert.That(
            storedLink!.OriginalUrl,
            Is.EqualTo("https://www.microsoft.com"));
    }

    [Test]
    public void Create_QuandoCodigoColidir_DeveTentarOutroSemSobrescrever()
    {
        // Arrange
        var store = new InMemoryLinkStore();

        var existingLink = new ShortenedLink(
            "abc12345",
            "https://www.microsoft.com");

        Assert.That(store.TryAdd(existingLink), Is.True);

        var generator = new SequenceShortCodeGenerator(
            "abc12345",
            "novo1234");

        var service = new LinkShorteningService(store, generator);

        // Act
        var result = service.Create("https://www.exemplo.com", null);

        // Assert
        Assert.That(result.Status, Is.EqualTo(CreateLinkStatus.Success));
        Assert.That(result.Link, Is.Not.Null);
        Assert.That(result.Link!.Code, Is.EqualTo("novo1234"));
        Assert.That(
            result.Link!.OriginalUrl,
            Is.EqualTo("https://www.exemplo.com"));

        Assert.That(store.Find("abc12345"), Is.EqualTo(existingLink));
        Assert.That(store.Find("novo1234"), Is.EqualTo(result.Link));
        Assert.That(generator.Calls, Is.EqualTo(2));
    }

    [Test]
    public void Create_QuandoTodasTentativasColidirem_DeveRetornarFalha()
    {
        // Arrange
        var store = new InMemoryLinkStore();

        var existingLink = new ShortenedLink(
            "abc12345",
            "https://www.microsoft.com");

        Assert.That(store.TryAdd(existingLink), Is.True);

        // Retorna o mesmo código em todas as chamadas.
        var generator = new SequenceShortCodeGenerator("abc12345");
        var service = new LinkShorteningService(store, generator);

        // Act
        var result = service.Create("https://www.exemplo.com", null);

        // Assert
        Assert.That(
            result.Status,
            Is.EqualTo(CreateLinkStatus.GenerationFailed));

        Assert.That(result.Link, Is.Null);
        Assert.That(generator.Calls, Is.EqualTo(10));
        Assert.That(store.Find("abc12345"), Is.EqualTo(existingLink));
    }

    private sealed class SequenceShortCodeGenerator : IShortCodeGenerator
    {
        private readonly string[] _codes;

        public int Calls { get; private set; }

        public SequenceShortCodeGenerator(params string[] codes)
        {
            if (codes.Length == 0)
            {
                throw new ArgumentException(
                    "Informe pelo menos um código.",
                    nameof(codes));
            }

            _codes = codes;
        }

        public string Generate()
        {
            // Quando a sequência termina, repete o último código.
            var index = Math.Min(Calls, _codes.Length - 1);

            Calls++;

            return _codes[index];
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