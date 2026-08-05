using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Controllers;

[Route("links/[action]/{lnkName}")]
public class RedirectController : Controller
{
    [HttpGet]
    [ActionName("go")]
    public IActionResult GotoLink( string lnkName)
    {
        if (string.IsNullOrEmpty(lnkName) || lnkName.Any(c => c is '/' or '\\'))
            return BadRequest();

        if (PersistentData.values.links.TryGetValue(lnkName, out var link))
        {
            return Redirect(link);
        }

        return NotFound();
    }

    [HttpPut]
    [ActionName("set")]
    [Authorize(Policy = nameof(Permissions.CreateLinks))]
    public IActionResult SetLink(string lnkName, string target)
    {
        if (string.IsNullOrEmpty(lnkName) || lnkName.Any(c => c == '/' || c == '\\'))
            return BadRequest();

        PersistentData.values.links[lnkName] = target;
        PersistentData.WritePersistentData();

        string url = Request.GetDisplayUrl().Split('?')[0];

        return Created(url.Replace("set", "go"), null);
    }
}
