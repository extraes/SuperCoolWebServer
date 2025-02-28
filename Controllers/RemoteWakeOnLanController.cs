using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace SuperCoolWebServer.Controllers
{
    [Route("wol/[action]")]
    public class RemoteWakeOnLanController : Controller
    {
        [HttpPut]
        [ActionName("get")]
        public async Task<IActionResult> Wake(string auth, string mac, string ip = "255.255.255.255")
        {
            if (auth != Config.values.wolAuth)
            {
                return Unauthorized();
            }

            Process.Start("wakeonlan", $"-i {ip} {mac}"); // TODO: use C# lib instead of shelling out
            //await WOL.WakeOnLan(mac);

            return Ok();
        }
    }
}
