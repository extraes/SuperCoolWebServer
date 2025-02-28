using Microsoft.AspNetCore.Mvc;

namespace SuperCoolWebServer.Controllers
{
    [Route("wol/[action]")]
    public class RemoteWakeOnLanController : Controller
    {
        [HttpPut]
        [ActionName("get")]
        public async Task<IActionResult> Wake(string auth, string mac)
        {
            if (auth != Config.values.wolAuth)
            {
                return Unauthorized();
            }

            await WOL.WakeOnLan(mac);


            return Ok();
        }
    }
}
