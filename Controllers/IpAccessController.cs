using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using SuperCoolWebServer.Data;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Controllers;

[Route("api/accesscontrol/{appId}/[action]")]
public class IpAccessController : Controller
{
    [HttpPost]
    [ActionName("limit")]
    public async Task<IActionResult> LimitUsage(
        [FromServices] AuditLogWriter auditLog,
        string appId,
        int newLimit,
        string appAuth,
        bool force = false)
    {

        if (!PersistentData.values.applicationAuth.TryGetValue(appId, out long existingAuth))
            return NotFound();
        if (existingAuth != Config.Hash(appAuth))
        {
            await auditLog.WriteAsync(
                HttpContext, null,
                AuditLogStrings.Actions.ACCESS_APP_AUTHORIZATION_DENIED,
                AuditLogStrings.Entities.ACCESS_APP,
                details: new { AppId = appId, Operation = "limit" });
            return Unauthorized();
        }

        //Array.Resize
        long[] hashes = PersistentData.values.applicationAccessedIpHashes[appId];
        if (!force && newLimit < hashes.Length)
            return BadRequest("Current usage limit is greater than suggest new limit. This would end up in a truncation of usages. If you wish to proceed, set 'force' to 'true'.");

        var oldLimit = hashes.Length;
        Array.Resize(ref hashes, newLimit);

        PersistentData.values.applicationAccessedIpHashes[appId] = hashes;
        PersistentData.WritePersistentData();
        await auditLog.WriteAsync(
            HttpContext, null,
            AuditLogStrings.Actions.ACCESS_APP_LIMIT_CHANGED,
            AuditLogStrings.Entities.ACCESS_APP,
            details: new { AppId = appId, OldLimit = oldLimit, NewLimit = newLimit, Force = force });
        return Ok();
    }

    [HttpPut]
    [ActionName("create")]
    [Authorize(Policy = nameof(Permissions.ManageLinks))]
    public async Task<IActionResult> CreateApp(
        string appId,
        string appAuth,
        string creationAuth,
        [FromServices] AuditLogWriter auditLog)
    {
        if (PersistentData.values.applicationAuth.ContainsKey(appId))
            return Conflict();

        PersistentData.values.applicationAuth[appId] = Config.Hash(appAuth);
        PersistentData.values.applicationAccessedIpHashes[appId] = Array.Empty<long>();
        PersistentData.WritePersistentData();
        await auditLog.WriteAsync(
            HttpContext, null,
            AuditLogStrings.Actions.ACCESS_APP_CREATED,
            AuditLogStrings.Entities.ACCESS_APP,
            details: new { AppId = appId });
        return Ok();
    }

    [HttpGet]
    [ActionName("auth")]
    public IActionResult AuthorizeRequestingIP(string appId)
    {
        if (!Request.Headers.TryGetValue("cf-connecting-ip", out var ip))
            ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrEmpty(ip))
            return Unauthorized();

        if (!PersistentData.values.applicationAccessedIpHashes.TryGetValue(appId, out long[]? arr))
            return NotFound();

        string ipStr = ip!;

        long ipHash = Config.Hash(ipStr);

        if (arr.Contains(ipHash))
            return Ok();
        else if (arr.Contains(0))
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != 0)
                    continue;

                arr[i] = ipHash;
                break;
            }

            PersistentData.WritePersistentData();
            return Ok();
        }
        else return Unauthorized();
    }
}
