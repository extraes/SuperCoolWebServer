using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace SuperCoolWebServer.Controllers;

[Route("links/[action]/{lnkName}")]
public class RedirectController : Controller
{
    [HttpGet]
    [ActionName("go")]
    public IActionResult GotoLink(string lnkName)
    {
        if (string.IsNullOrEmpty(lnkName) || lnkName.Any(c => c == '/' || c == '\\'))
            return BadRequest();

        if (PersistentData.values.links.TryGetValue(lnkName, out var link))
        {
            return Redirect(link);
        }

        return NotFound();
    }

    [HttpPut]
    [ActionName("set")]
    public IActionResult SetLink(string lnkName, string target, string auth)
    {
        if (Config.values.redirectAuth != auth)
            return Unauthorized();

        if (string.IsNullOrEmpty(lnkName) || lnkName.Any(c => c == '/' || c == '\\'))
            return BadRequest();

        PersistentData.values.links[lnkName] = target;
        PersistentData.WritePersistentData();

        string url = Request.GetDisplayUrl().Split('?')[0];

        return Created(url.Replace("set", "go"), null);
    }
}
