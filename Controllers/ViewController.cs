using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SuperCoolWebServer.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/")]
public class ViewController : Controller
{
    public IActionResult Index()
    {
        return View("/Views/IndexView.cshtml");
    }

    [Authorize]
    [Route("dashboard")]
    public IActionResult Dashboard()
    {
        return View("/Views/DashboardView.cshtml");
    }

    [Route("privacy")]
    public IActionResult Privacy()
    {
        return View("/Views/PrivacyView.cshtml");
    }
}