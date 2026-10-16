using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Topaz.UrlShortener.Application.Results;
using Topaz.UrlShortener.DTOs;
using Topaz.UrlShortener.Infrastructure.Queue;

namespace Topaz.UrlShortener.Controllers;

[ApiController]
[Route("api/links")]
public sealed class LinksController : ControllerBase
{
    private readonly LinkCreationQueue _queue;

    public LinksController(LinkCreationQueue queue)
    {
        _queue = queue;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLinkRequest request,
        CancellationToken cancellationToken)
    {
        CreateLinkResult result;

        try
        {
            result = await _queue.EnqueueAsync(
                request.OriginalUrl,
                request.Alias,
                cancellationToken);
        }
        catch (ChannelClosedException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Serviço indisponível",
                detail: "A aplicação está encerrando. Tente novamente.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Processamento interrompido",
                detail: "A solicitação foi cancelada pelo encerramento da aplicação.");
        }

        switch (result.Status)
        {
            case CreateLinkStatus.Success:
                {
                    var link = result.Link!;

                    var shortUrl = Url.Link(
                        "ResolveShortLink",
                        new { code = link.Code });

                    if (shortUrl is null)
                    {
                        throw new InvalidOperationException(
                            "Não foi possível gerar a URL de redirecionamento.");
                    }

                    var response = new CreateLinkResponse(
                        link.Code,
                        shortUrl,
                        link.OriginalUrl);

                    return Created(shortUrl, response);
                }

            case CreateLinkStatus.InvalidInput:
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Entrada inválida",
                    detail: result.Error);

            case CreateLinkStatus.AliasConflict:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Alias indisponível",
                    detail: result.Error);

            case CreateLinkStatus.GenerationFailed:
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Geração indisponível",
                    detail: result.Error);

            default:
                throw new InvalidOperationException(
                    "Resultado de criação não reconhecido.");
        }
    }
}