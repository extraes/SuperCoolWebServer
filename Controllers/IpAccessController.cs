using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace SuperCoolWebServer.Controllers;

[Route("api/accesscontrol/{appId}/[action]")]
public class IpAccessController : Controller
{
    [HttpPost]
    [ActionName("limit")]
    public IActionResult LimitUsage(string appId, int newLimit, string appAuth, bool force = false)
    {

        if (!PersistentData.values.applicationAuth.TryGetValue(appId, out long existingAuth))
            return NotFound();
        if (existingAuth != Config.Hash(appAuth))
            return Unauthorized();

        //Array.Resize
        long[] hashes = PersistentData.values.applicationAccessedIpHashes[appId];
        if (!force && newLimit < hashes.Length)
            return BadRequest("Current usage limit is greater than suggest new limit. This would end up in a truncation of usages. If you wish to proceed, set 'force' to 'true'.");

        Array.Resize(ref hashes, newLimit);

        PersistentData.values.applicationAccessedIpHashes[appId] = hashes;
        PersistentData.WritePersistentData();
        return Ok();
    }

    [HttpPut]
    [ActionName("create")]
    public IActionResult CreateApp(string appId, string appAuth, string creationAuth)
    {
        if (Config.values.ipAccessAuth != creationAuth)
            return Unauthorized();

        if (PersistentData.values.applicationAuth.ContainsKey(appId))
            return Conflict();

        PersistentData.values.applicationAuth[appId] = Config.Hash(appAuth);
        PersistentData.values.applicationAccessedIpHashes[appId] = Array.Empty<long>();
        PersistentData.WritePersistentData();
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
