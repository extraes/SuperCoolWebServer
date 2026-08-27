using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using SuperCoolWebServer.Data;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Controllers;

[Route("links/[action]/{lnkName}")]
public class RedirectController : Controller
{
    [HttpGet]
    [ActionName("go")]
    public IActionResult GotoLink(string lnkName)
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
    [Authorize(Policy = nameof(Permissions.ManageLinks))]
    public async Task<IActionResult> SetLink(
        string lnkName,
        string target,
        [FromServices] AuditLogWriter auditLog)
    {
        if (string.IsNullOrEmpty(lnkName) || lnkName.Any(c => c == '/' || c == '\\'))
            return BadRequest();

        var existed = PersistentData.values.links.TryGetValue(lnkName, out var oldTarget);
        PersistentData.values.links[lnkName] = target;
        PersistentData.WritePersistentData();

        await auditLog.WriteAsync(
            HttpContext, null,
            existed ? AuditLogStrings.Actions.LINK_UPDATED : AuditLogStrings.Actions.LINK_CREATED,
            AuditLogStrings.Entities.LINK,
            details: new
            {
                Name = lnkName,
                OldTarget = oldTarget,
                NewTarget = target,
            });

        string url = Request.GetDisplayUrl().Split('?')[0];

        return Created(url.Replace("set", "go"), null);
    }

    [HttpDelete]
    [ActionName("unset")]
    [Authorize(Policy = nameof(Permissions.ManageLinks))]
    public async Task<IActionResult> UnsetLink(
        string lnkName,
        [FromServices] AuditLogWriter auditLog)
    {
        if (string.IsNullOrEmpty(lnkName) || lnkName.Any(c => c == '/' || c == '\\'))
            return BadRequest();

        if (!PersistentData.values.links.Remove(lnkName, out var oldTarget))
            return NotFound();

        PersistentData.WritePersistentData();
        await auditLog.WriteAsync(
            HttpContext, null,
            AuditLogStrings.Actions.LINK_DELETED,
            AuditLogStrings.Entities.LINK,
            details: new { Name = lnkName, Target = oldTarget });

        return Ok();
    }
}
