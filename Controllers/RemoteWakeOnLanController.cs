using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using SuperCoolWebServer.Data;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Controllers;

[Route("wol/[action]")]
public class RemoteWakeOnLanController : Controller
{
    [HttpPut]
    [ActionName("get")]
    [Authorize(Policy = nameof(Permissions.UseWakeOnLan))]
    public async Task<IActionResult> Wake(
        string mac,
        [FromServices] AuditLogWriter auditLog,
        string ip = "255.255.255.255")
    {
        try
        {
            Process.Start("wakeonlan", $"-i {ip} {mac}"); // TODO: use C# lib instead of shelling out
            //await WOL.WakeOnLan(mac);
            await auditLog.WriteAsync(
                HttpContext, null,
                AuditLogStrings.Actions.WOL_PACKET_SENT,
                AuditLogStrings.Entities.WOL_DEVICE,
                details: new { Mac = mac, DestinationIp = ip });
        }
        catch (Exception ex)
        {
            await auditLog.WriteAsync(
                HttpContext, null,
                AuditLogStrings.Actions.WOL_PACKET_FAILED,
                AuditLogStrings.Entities.WOL_DEVICE,
                details: new
                {
                    Mac = mac,
                    DestinationIp = ip,
                    ErrorType = ex.GetType().Name,
                    ex.Message,
                });
            throw;
        }

        return Ok();
    }
}
