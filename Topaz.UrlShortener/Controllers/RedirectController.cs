using Microsoft.AspNetCore.Mvc;
using Topaz.UrlShortener.Application.Interfaces;

namespace Topaz.UrlShortener.Controllers;

[ApiController]
public sealed class RedirectController : ControllerBase
{
    private readonly ILinkStore _store;

    public RedirectController(ILinkStore store)
    {
        _store = store;
    }

    [HttpGet("/s/{code}", Name = "ResolveShortLink")]
    public IActionResult Get(string code)
    {
        var link = _store.Find(code);

        if (link is null)
        {
            return NotFound();
        }

        return Redirect(link.OriginalUrl);
    }
}