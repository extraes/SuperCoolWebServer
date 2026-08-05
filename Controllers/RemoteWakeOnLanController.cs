using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using SuperCoolWebServer.Models;

namespace SuperCoolWebServer.Controllers;

[Route("wol/[action]")]
public class RemoteWakeOnLanController : Controller
{
    [HttpPut]
    [ActionName("get")]
    [Authorize(Policy = nameof(Permissions.UseWakeOnLan))]
    public async Task<IActionResult> Wake(string mac, string ip = "255.255.255.255")
    {
        Process.Start("wakeonlan", $"-i {ip} {mac}"); // TODO: use C# lib instead of shelling out
        //await WOL.WakeOnLan(mac);

        return Ok();
    }
}